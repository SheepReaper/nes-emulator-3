using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Sheep.Nes.Lab;

public sealed record RunHistoryEntry(
    string Id, DateTimeOffset CompletedAtUtc, VerificationScope Scope, string? CaseName,
    VerificationOutcome Outcome, bool Success, bool Cached, long DurationMilliseconds,
    int RawOutputBytes, string ArtifactPath, string? TraceArtifactPath,
    string Fingerprint, string SourceRevision, string? FailureSignature,
    bool SourceDirty = false, string? WorkingTreeDigest = null,
    string? LabVersion = null, int ContractSchemaVersion = 0,
    string? ResourceUri = null, string? LogResourceUri = null, string? TraceResourceUri = null,
    VerificationStatus? VerificationStatus = null,
    VerificationExitPolicy ExitPolicy = VerificationExitPolicy.Strict,
    bool? MatchesAcceptedBaseline = null, bool HasRegressions = false,
    bool HasResolvedBaselineCases = false);

public sealed record RunMetrics(int Total, int Passed, int Failed, int InfrastructureFailures,
    int Cancelled, int CacheHits, long RawBytes, long ReducedBytes,
    int AcceptedBaseline = 0, int ImprovedBaseline = 0, int Regressions = 0,
    int StrictPolicyRuns = 0, int BaselineAwarePolicyRuns = 0);

public interface IVerificationRunSink
{
    Task RecordAsync(VerificationCommand command, VerificationResult result, CancellationToken cancellationToken);
}

public sealed class RunHistoryStore : IVerificationRunSink, IDisposable
{
    private readonly SqliteConnection connection;
    private readonly string repositoryRoot;

    public RunHistoryStore(string databasePath, string repositoryRoot)
    {
        this.repositoryRoot = Path.GetFullPath(repositoryRoot);
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connection = new SqliteConnection($"Data Source={fullPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA busy_timeout=5000;
            CREATE TABLE IF NOT EXISTS runs (
              id TEXT PRIMARY KEY, completed_utc TEXT NOT NULL, scope TEXT NOT NULL,
              case_name TEXT NULL, outcome TEXT NOT NULL, success INTEGER NOT NULL,
              cached INTEGER NOT NULL, duration_ms INTEGER NOT NULL, raw_bytes INTEGER NOT NULL,
              reduced_bytes INTEGER NOT NULL DEFAULT 0, artifact_path TEXT NOT NULL,
              trace_artifact_path TEXT NULL, fingerprint TEXT NOT NULL,
              source_revision TEXT NOT NULL, failure_signature TEXT NULL, failure_text TEXT NULL,
              source_dirty INTEGER NOT NULL DEFAULT 0, working_tree_digest TEXT NULL,
              lab_version TEXT NULL, contract_schema_version INTEGER NOT NULL DEFAULT 0);
            CREATE INDEX IF NOT EXISTS ix_runs_completed ON runs(completed_utc DESC);
            CREATE INDEX IF NOT EXISTS ix_runs_case ON runs(case_name, completed_utc DESC);
            """;
        command.ExecuteNonQuery();
        EnsureColumn("source_dirty", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn("working_tree_digest", "TEXT NULL");
        EnsureColumn("lab_version", "TEXT NULL");
        EnsureColumn("contract_schema_version", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn("resource_uri", "TEXT NULL");
        EnsureColumn("log_resource_uri", "TEXT NULL");
        EnsureColumn("trace_resource_uri", "TEXT NULL");
        EnsureColumn("verification_status", "TEXT NULL");
        EnsureColumn("exit_policy", "TEXT NOT NULL DEFAULT 'Strict'");
        EnsureColumn("matches_accepted_baseline", "INTEGER NULL");
        EnsureColumn("has_regressions", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn("has_resolved_baseline_cases", "INTEGER NOT NULL DEFAULT 0");
    }

    public void RecordSemantics(VerificationBatchResult result, VerificationSemantics semantics)
    {
        foreach (var verification in result.Results)
        {
            var itemSemantics = verification.Scope == VerificationScope.Conformance
                ? semantics
                : new VerificationSemantics(verification.Success, null, !verification.Success, false,
                    verification.Success ? VerificationStatus.Passed
                    : verification.Outcome is VerificationOutcome.InfrastructureFailure or VerificationOutcome.Cancelled
                        ? VerificationStatus.InfrastructureFailure : VerificationStatus.Regression,
                    semantics.ExitPolicy);
            using var update = connection.CreateCommand();
            update.CommandText = """
                UPDATE runs SET verification_status=$status,exit_policy=$policy,
                  matches_accepted_baseline=$matches,has_regressions=$regressions,
                  has_resolved_baseline_cases=$resolved
                WHERE id=(SELECT id FROM runs WHERE artifact_path=$artifact ORDER BY completed_utc DESC LIMIT 1);
                """;
            update.Parameters.AddWithValue("$status", itemSemantics.VerificationStatus.ToString());
            update.Parameters.AddWithValue("$policy", itemSemantics.ExitPolicy.ToString());
            update.Parameters.AddWithValue("$matches", itemSemantics.MatchesAcceptedBaseline is null
                ? DBNull.Value : itemSemantics.MatchesAcceptedBaseline.Value ? 1 : 0);
            update.Parameters.AddWithValue("$regressions", itemSemantics.HasRegressions ? 1 : 0);
            update.Parameters.AddWithValue("$resolved", itemSemantics.HasResolvedBaselineCases ? 1 : 0);
            update.Parameters.AddWithValue("$artifact", Path.GetFullPath(verification.ArtifactPath));
            update.ExecuteNonQuery();
        }
    }

    public async Task RecordAsync(VerificationCommand command, VerificationResult result,
        CancellationToken cancellationToken)
    {
        var details = await VerificationFingerprint.CreateDetailsAsync(
            command, repositoryRoot, cancellationToken).ConfigureAwait(false);
        var provenance = RepositoryProvenance.Capture(repositoryRoot);
        var id = Guid.NewGuid().ToString("N");
        var artifacts = new ImmutableArtifactStore(Path.Combine(repositoryRoot, ".artifacts", "nes-lab"));
        var log = await artifacts.PublishFileAsync("log", result.ArtifactPath, "text/plain",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        ImmutableArtifactMetadata? trace = null;
        if (result.TraceArtifactPath is { } tracePath && File.Exists(tracePath))
            trace = await artifacts.PublishFileAsync("trace", tracePath, "application/json",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        var logUri = ImmutableArtifactStore.Uri("log", log.Digest);
        var traceUri = trace is null ? null : ImmutableArtifactStore.Uri("trace", trace.Digest);
        var runJson = JsonSerializer.Serialize(new { schemaVersion = 1, id, command, result,
            provenance, fingerprint = details.Fingerprint, logResourceUri = logUri,
            traceResourceUri = traceUri }, LabResponseSerializer.Options);
        var runArtifact = await artifacts.PublishTextAsync("run", runJson, "application/json",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var runUri = ImmutableArtifactStore.Uri("run", runArtifact.Digest);
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO runs (id,completed_utc,scope,case_name,outcome,success,cached,duration_ms,
              raw_bytes,reduced_bytes,artifact_path,trace_artifact_path,fingerprint,source_revision,
              failure_signature,failure_text,source_dirty,working_tree_digest,lab_version,
              contract_schema_version,resource_uri,log_resource_uri,trace_resource_uri)
              VALUES ($id,$completed,$scope,$case,$outcome,$success,
              $cached,$duration,$raw,$reduced,$artifact,$trace,$fingerprint,$revision,$failure,$failureText,
              $dirty,$tree,$version,$contractSchema,$resourceUri,$logResourceUri,$traceResourceUri);
            """;
        insert.Parameters.AddWithValue("$id", id);
        insert.Parameters.AddWithValue("$completed", DateTimeOffset.UtcNow.ToString("O"));
        insert.Parameters.AddWithValue("$scope", result.Scope.ToString());
        insert.Parameters.AddWithValue("$case", CaseName(command) ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$outcome", result.Outcome.ToString());
        insert.Parameters.AddWithValue("$success", result.Success ? 1 : 0);
        insert.Parameters.AddWithValue("$cached", result.Cached ? 1 : 0);
        insert.Parameters.AddWithValue("$duration", result.DurationMilliseconds);
        insert.Parameters.AddWithValue("$raw", result.RawOutputBytes);
        insert.Parameters.AddWithValue("$reduced", Encoding.UTF8.GetByteCount(
            JsonSerializer.Serialize(result, LabResponseSerializer.Options)));
        insert.Parameters.AddWithValue("$artifact", Path.GetFullPath(result.ArtifactPath));
        insert.Parameters.AddWithValue("$trace", result.TraceArtifactPath is null ? DBNull.Value : Path.GetFullPath(result.TraceArtifactPath));
        insert.Parameters.AddWithValue("$fingerprint", details.Fingerprint);
        insert.Parameters.AddWithValue("$revision", provenance.Head);
        insert.Parameters.AddWithValue("$failure", FailureSignature(result) ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$failureText", FailureText(result) ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$dirty", provenance.IsDirty ? 1 : 0);
        insert.Parameters.AddWithValue("$tree", provenance.WorkingTreeDigest);
        insert.Parameters.AddWithValue("$version", provenance.LabVersion);
        insert.Parameters.AddWithValue("$contractSchema", provenance.ContractSchemaVersion);
        insert.Parameters.AddWithValue("$resourceUri", runUri);
        insert.Parameters.AddWithValue("$logResourceUri", logUri);
        insert.Parameters.AddWithValue("$traceResourceUri", traceUri ?? (object)DBNull.Value);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public RunHistoryEntry? Latest(VerificationScope? scope = null, string? caseName = null, bool failuresOnly = false) =>
        Query(scope, caseName, failuresOnly, null, 1).SingleOrDefault();

    public RunHistoryEntry? LatestFull(VerificationScope scope)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,completed_utc,scope,case_name,outcome,success,cached,duration_ms,raw_bytes,artifact_path,trace_artifact_path,fingerprint,source_revision,failure_signature,source_dirty,working_tree_digest,lab_version,contract_schema_version,resource_uri,log_resource_uri,trace_resource_uri,verification_status,exit_policy,matches_accepted_baseline,has_regressions,has_resolved_baseline_cases FROM runs WHERE scope=$scope AND case_name IS NULL ORDER BY completed_utc DESC LIMIT 1";
        command.Parameters.AddWithValue("$scope", scope.ToString());
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public IReadOnlyList<RunHistoryEntry> SearchFailures(string query, int maximumResults = 32) =>
        Query(null, null, true, query, maximumResults);

    public RunHistoryEntry Get(string id) => QueryById(id) ??
        throw new KeyNotFoundException($"No nes-lab run has ID '{id}'.");

    public RunMetrics Metrics()
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*), SUM(success), SUM(CASE WHEN outcome='TestFailure' THEN 1 ELSE 0 END),
              SUM(CASE WHEN outcome='InfrastructureFailure' THEN 1 ELSE 0 END),
              SUM(CASE WHEN outcome='Cancelled' THEN 1 ELSE 0 END), SUM(cached),
              SUM(raw_bytes), SUM(reduced_bytes),
              SUM(CASE WHEN verification_status='AcceptedBaseline' THEN 1 ELSE 0 END),
              SUM(CASE WHEN verification_status='ImprovedBaseline' THEN 1 ELSE 0 END),
              SUM(CASE WHEN has_regressions=1 THEN 1 ELSE 0 END),
              SUM(CASE WHEN exit_policy='Strict' THEN 1 ELSE 0 END),
              SUM(CASE WHEN exit_policy='BaselineAware' THEN 1 ELSE 0 END) FROM runs;
            """;
        using var reader = command.ExecuteReader();
        reader.Read();
        return new RunMetrics(reader.GetInt32(0), Value(1), Value(2), Value(3), Value(4),
            Value(5), Value64(6), Value64(7), Value(8), Value(9), Value(10), Value(11), Value(12));
        int Value(int ordinal) => reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        long Value64(int ordinal) => reader.IsDBNull(ordinal) ? 0 : reader.GetInt64(ordinal);
    }

    private IReadOnlyList<RunHistoryEntry> Query(VerificationScope? scope, string? caseName,
        bool failuresOnly, string? search, int maximum)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,completed_utc,scope,case_name,outcome,success,cached,duration_ms,raw_bytes,
              artifact_path,trace_artifact_path,fingerprint,source_revision,failure_signature,
              source_dirty,working_tree_digest,lab_version,contract_schema_version
              ,resource_uri,log_resource_uri,trace_resource_uri,verification_status,exit_policy,
              matches_accepted_baseline,has_regressions,has_resolved_baseline_cases
            FROM runs WHERE ($scope IS NULL OR scope=$scope) AND ($case IS NULL OR case_name=$case)
              AND ($failures=0 OR success=0)
              AND ($search IS NULL OR case_name LIKE $pattern OR failure_signature LIKE $pattern OR failure_text LIKE $pattern)
            ORDER BY completed_utc DESC LIMIT $maximum;
            """;
        command.Parameters.AddWithValue("$scope", scope?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$case", caseName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$failures", failuresOnly ? 1 : 0);
        command.Parameters.AddWithValue("$search", search ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$pattern", search is null ? DBNull.Value : $"%{search}%");
        command.Parameters.AddWithValue("$maximum", maximum);
        using var reader = command.ExecuteReader();
        List<RunHistoryEntry> result = [];
        while (reader.Read()) result.Add(Read(reader));
        return result;
    }

    private RunHistoryEntry? QueryById(string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,completed_utc,scope,case_name,outcome,success,cached,duration_ms,raw_bytes,artifact_path,trace_artifact_path,fingerprint,source_revision,failure_signature,source_dirty,working_tree_digest,lab_version,contract_schema_version,resource_uri,log_resource_uri,trace_resource_uri,verification_status,exit_policy,matches_accepted_baseline,has_regressions,has_resolved_baseline_cases FROM runs WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    private static RunHistoryEntry Read(SqliteDataReader reader) => new(
        reader.GetString(0), DateTimeOffset.Parse(reader.GetString(1)), Enum.Parse<VerificationScope>(reader.GetString(2)),
        reader.IsDBNull(3) ? null : reader.GetString(3), Enum.Parse<VerificationOutcome>(reader.GetString(4)),
        reader.GetBoolean(5), reader.GetBoolean(6), reader.GetInt64(7), reader.GetInt32(8), reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetString(11), reader.GetString(12),
        reader.IsDBNull(13) ? null : reader.GetString(13), reader.GetBoolean(14),
        reader.IsDBNull(15) ? null : reader.GetString(15),
        reader.IsDBNull(16) ? null : reader.GetString(16), reader.GetInt32(17),
        reader.IsDBNull(18) ? null : reader.GetString(18),
        reader.IsDBNull(19) ? null : reader.GetString(19),
        reader.IsDBNull(20) ? null : reader.GetString(20),
        reader.IsDBNull(21) ? null : Enum.Parse<VerificationStatus>(reader.GetString(21)),
        reader.IsDBNull(22) ? VerificationExitPolicy.Strict : Enum.Parse<VerificationExitPolicy>(reader.GetString(22)),
        reader.IsDBNull(23) ? null : reader.GetBoolean(23), reader.GetBoolean(24), reader.GetBoolean(25));

    private void EnsureColumn(string name, string declaration)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('runs') WHERE name=$name";
        check.Parameters.AddWithValue("$name", name);
        if (Convert.ToInt32(check.ExecuteScalar()) != 0) return;
        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE runs ADD COLUMN {name} {declaration}";
        alter.ExecuteNonQuery();
    }

    private static string? CaseName(VerificationCommand command)
    {
        for (var index = 0; index + 1 < command.Arguments.Count; index++)
            if (command.Arguments[index] == "--filter-display-name") return command.Arguments[index + 1].Trim('*');
        return null;
    }

    private static string? FailureSignature(VerificationResult result)
    {
        if (result.Failures.Count == 0) return null;
        var text = string.Join('\n', result.Failures.Select(item => $"{item.Name}|{item.Diagnostic}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..24];
    }

    private static string? FailureText(VerificationResult result) => result.Failures.Count == 0 ? null :
        string.Join('\n', result.Failures.Select(item => $"{item.Name}|{item.Diagnostic}"));

    public void Dispose() { connection.Dispose(); SqliteConnection.ClearPool(connection); }
}
