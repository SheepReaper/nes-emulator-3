using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Sheep.Nes.Lab;

public enum EngineeringMemoryKind
{
    ConfirmedFact,
    Observation,
    Hypothesis,
    RejectedHypothesis,
    Fix,
    RegressionTest,
    Decision,
    KnownGap
}

public sealed record EngineeringProvenance(
    string Kind,
    string Source,
    string SourceHash,
    int? LineNumber,
    string? Commit);

public sealed record EngineeringMemoryEntry(
    long Id,
    EngineeringMemoryKind Kind,
    string Title,
    string Body,
    IReadOnlyList<EngineeringProvenance> Provenance,
    DateTimeOffset CreatedAtUtc,
    long? SupersedesId = null,
    bool IsStale = false,
    IReadOnlyList<string>? AffectedSymbols = null,
    IReadOnlyList<string>? AffectedTests = null);

public sealed record EngineeringMemoryValidation(long Id, bool IsStale, IReadOnlyList<string> Reasons);

public sealed class EngineeringMemoryStore : IDisposable
{
    private readonly SqliteConnection _connection;

    public EngineeringMemoryStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var absolutePath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        _connection = new SqliteConnection($"Data Source={absolutePath}");
        _connection.Open();
        Initialize();
    }

    public long Add(EngineeringMemoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Body);
        if (entry.Provenance.Count == 0)
            throw new ArgumentException("Engineering memory requires at least one provenance record.", nameof(entry));
        if (entry.Provenance.Any(item =>
                string.IsNullOrWhiteSpace(item.Kind) ||
                string.IsNullOrWhiteSpace(item.Source) ||
                string.IsNullOrWhiteSpace(item.SourceHash)))
            throw new ArgumentException("Every provenance record requires kind, source, and source hash.", nameof(entry));

        using var transaction = _connection.BeginTransaction();
        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO engineering_memory(kind, title, body, provenance_json, created_at_utc,
              supersedes_id,is_stale,affected_symbols_json,affected_tests_json)
            VALUES ($kind, $title, $body, $provenance, $created,$supersedes,$stale,$symbols,$tests);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$kind", entry.Kind.ToString());
        insert.Parameters.AddWithValue("$title", entry.Title);
        insert.Parameters.AddWithValue("$body", entry.Body);
        insert.Parameters.AddWithValue("$provenance", JsonSerializer.Serialize(entry.Provenance));
        insert.Parameters.AddWithValue("$created", entry.CreatedAtUtc.ToString("O"));
        insert.Parameters.AddWithValue("$supersedes", entry.SupersedesId ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$stale", entry.IsStale ? 1 : 0);
        insert.Parameters.AddWithValue("$symbols", JsonSerializer.Serialize(entry.AffectedSymbols ?? []));
        insert.Parameters.AddWithValue("$tests", JsonSerializer.Serialize(entry.AffectedTests ?? []));
        var id = (long)(insert.ExecuteScalar() ?? throw new InvalidOperationException("SQLite did not return an entry ID."));

        using var index = _connection.CreateCommand();
        index.Transaction = transaction;
        index.CommandText = "INSERT INTO engineering_memory_fts(rowid, title, body) VALUES ($id, $title, $body);";
        index.Parameters.AddWithValue("$id", id);
        index.Parameters.AddWithValue("$title", entry.Title);
        index.Parameters.AddWithValue("$body", entry.Body);
        index.ExecuteNonQuery();
        transaction.Commit();
        return id;
    }

    public IReadOnlyList<EngineeringMemoryEntry> Search(
        string query,
        EngineeringMemoryKind? kind = null,
        int maximumResults = 32,
        bool includeRejectedHypotheses = false,
        bool includeStale = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT m.id, m.kind, m.title, m.body, m.provenance_json, m.created_at_utc,
              m.supersedes_id,m.is_stale,m.affected_symbols_json,m.affected_tests_json
            FROM engineering_memory_fts
            JOIN engineering_memory m ON m.id = engineering_memory_fts.rowid
            WHERE engineering_memory_fts MATCH $query
              AND ($kind IS NULL OR m.kind = $kind)
              AND ($include_rejected = 1 OR m.kind != 'RejectedHypothesis')
              AND ($include_stale = 1 OR m.is_stale = 0)
            ORDER BY bm25(engineering_memory_fts), m.id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$query", QuoteFtsPhrase(query));
        command.Parameters.AddWithValue("$kind", kind?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$include_rejected", includeRejectedHypotheses ? 1 : 0);
        command.Parameters.AddWithValue("$include_stale", includeStale ? 1 : 0);
        command.Parameters.AddWithValue("$limit", maximumResults);
        using var reader = command.ExecuteReader();
        List<EngineeringMemoryEntry> entries = [];
        while (reader.Read())
        {
            entries.Add(new EngineeringMemoryEntry(
                reader.GetInt64(0),
                Enum.Parse<EngineeringMemoryKind>(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                JsonSerializer.Deserialize<EngineeringProvenance[]>(reader.GetString(4)) ?? [],
                DateTimeOffset.Parse(reader.GetString(5)),
                reader.IsDBNull(6) ? null : reader.GetInt64(6), reader.GetBoolean(7),
                JsonSerializer.Deserialize<string[]>(reader.GetString(8)) ?? [],
                JsonSerializer.Deserialize<string[]>(reader.GetString(9)) ?? []));
        }
        return entries;
    }

    public EngineeringMemoryEntry Get(long id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT id,kind,title,body,provenance_json,created_at_utc,supersedes_id,is_stale,affected_symbols_json,affected_tests_json FROM engineering_memory WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new KeyNotFoundException($"No engineering memory entry has ID {id}.");
        return new EngineeringMemoryEntry(reader.GetInt64(0), Enum.Parse<EngineeringMemoryKind>(reader.GetString(1)),
            reader.GetString(2), reader.GetString(3),
            JsonSerializer.Deserialize<EngineeringProvenance[]>(reader.GetString(4)) ?? [],
            DateTimeOffset.Parse(reader.GetString(5)), reader.IsDBNull(6) ? null : reader.GetInt64(6),
            reader.GetBoolean(7), JsonSerializer.Deserialize<string[]>(reader.GetString(8)) ?? [],
            JsonSerializer.Deserialize<string[]>(reader.GetString(9)) ?? []);
    }

    public long Supersede(long id, EngineeringMemoryEntry replacement)
    {
        _ = Get(id);
        return Add(replacement with { Id = 0, SupersedesId = id });
    }

    public IReadOnlyList<EngineeringMemoryEntry> Stale() => All().Where(item => item.IsStale).ToArray();

    public IReadOnlyList<EngineeringMemoryValidation> Validate(string repositoryRoot)
    {
        var root = Path.GetFullPath(repositoryRoot);
        List<EngineeringMemoryValidation> results = [];
        foreach (var entry in All())
        {
            List<string> reasons = [];
            foreach (var provenance in entry.Provenance)
            {
                var candidate = Path.GetFullPath(Path.Combine(root, provenance.Source.Split('#')[0]));
                if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(candidate)) { reasons.Add($"Missing source: {provenance.Source}"); continue; }
                var digest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(candidate)));
                if (!digest.Equals(provenance.SourceHash, StringComparison.OrdinalIgnoreCase))
                    reasons.Add($"Hash changed: {provenance.Source}");
            }
            using var update = _connection.CreateCommand();
            update.CommandText = "UPDATE engineering_memory SET is_stale=$stale WHERE id=$id";
            update.Parameters.AddWithValue("$stale", reasons.Count == 0 ? 0 : 1);
            update.Parameters.AddWithValue("$id", entry.Id);
            update.ExecuteNonQuery();
            results.Add(new EngineeringMemoryValidation(entry.Id, reasons.Count != 0, reasons));
        }
        return results;
    }

    public IReadOnlyList<EngineeringMemoryEntry> All()
    {
        List<long> ids = [];
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "SELECT id FROM engineering_memory ORDER BY id";
            using var reader = command.ExecuteReader();
            while (reader.Read()) ids.Add(reader.GetInt64(0));
        }
        return ids.Select(Get).ToArray();
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearPool(_connection);
    }

    private void Initialize()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS engineering_memory (
                id INTEGER PRIMARY KEY,
                kind TEXT NOT NULL,
                title TEXT NOT NULL,
                body TEXT NOT NULL,
                provenance_json TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                supersedes_id INTEGER NULL,
                is_stale INTEGER NOT NULL DEFAULT 0,
                affected_symbols_json TEXT NOT NULL DEFAULT '[]',
                affected_tests_json TEXT NOT NULL DEFAULT '[]'
            );
            CREATE VIRTUAL TABLE IF NOT EXISTS engineering_memory_fts
            USING fts5(title, body);
            """;
        command.ExecuteNonQuery();
        EnsureColumn("supersedes_id", "INTEGER NULL");
        EnsureColumn("is_stale", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn("affected_symbols_json", "TEXT NOT NULL DEFAULT '[]'");
        EnsureColumn("affected_tests_json", "TEXT NOT NULL DEFAULT '[]'");
    }

    private void EnsureColumn(string name, string declaration)
    {
        using var check = _connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('engineering_memory') WHERE name=$name";
        check.Parameters.AddWithValue("$name", name);
        if (Convert.ToInt32(check.ExecuteScalar()) != 0) return;
        using var alter = _connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE engineering_memory ADD COLUMN {name} {declaration}";
        alter.ExecuteNonQuery();
    }

    private static string QuoteFtsPhrase(string query) =>
        $"\"{query.Replace("\"", "\"\"")}\"";
}
