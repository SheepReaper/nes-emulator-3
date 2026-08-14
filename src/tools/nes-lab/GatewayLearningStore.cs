using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Sheep.Nes.Lab;

public enum MemoryProposalStatus { Pending, Accepted, Rejected }
public sealed record EvidenceOutcomeTelemetry(string? Model = null, string? Provider = null,
    long? CloudInputTokens = null, long? CloudOutputTokens = null, int? DiagnosticIterations = null,
    double? ElapsedMilliseconds = null, string? VerificationResult = null,
    IReadOnlyList<long>? AcceptedProposalIds = null, IReadOnlyList<long>? AcceptedFixIds = null,
    long? TokensAvoided = null, long? CachedTokens = null, int? GatewayCalls = null,
    double? TimeToUsefulEvidenceMilliseconds = null, double? TimeToDiagnosisMilliseconds = null,
    double? TimeToPassingVerificationMilliseconds = null, int? DirectSourceReads = null);
public sealed record EvidenceFeedback(long Id, string PacketId, IReadOnlyList<string> UsefulEvidenceIds,
    IReadOnlyList<string> NotUsefulEvidenceIds, string? Outcome, string? RunId, DateTimeOffset CreatedAtUtc,
    EvidenceOutcomeTelemetry? Telemetry = null);
public sealed record EvidenceOutcomeMetrics(double EvidenceUseRate, int FeedbackCount,
    long? TokensAvoided, double? AverageTimeToUsefulEvidenceMilliseconds,
    double? AverageTimeToPassingVerificationMilliseconds, string HostTelemetryStatus,
    double? AverageTimeToDiagnosisMilliseconds = null, double? AverageDirectSourceReads = null,
    double? AverageGatewayCalls = null);
public sealed record MemoryProposal(long Id, EngineeringMemoryKind Kind, string Title, string Body,
    IReadOnlyList<EngineeringProvenance> Provenance, string PacketId, MemoryProposalStatus Status,
    DateTimeOffset CreatedAtUtc, long? AcceptedMemoryId = null);

public sealed class GatewayLearningStore : IDisposable
{
    private readonly SqliteConnection connection;
    public GatewayLearningStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connection = new($"Data Source={Path.GetFullPath(path)};Pooling=False"); connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
CREATE TABLE IF NOT EXISTS evidence_feedback(id INTEGER PRIMARY KEY,packet_id TEXT NOT NULL,useful_json TEXT NOT NULL,
 not_useful_json TEXT NOT NULL,outcome TEXT,run_id TEXT,created_at TEXT NOT NULL,
 UNIQUE(packet_id,useful_json,not_useful_json,outcome,run_id));
CREATE TABLE IF NOT EXISTS memory_proposals(id INTEGER PRIMARY KEY,kind TEXT NOT NULL,title TEXT NOT NULL,body TEXT NOT NULL,
 provenance_json TEXT NOT NULL,packet_id TEXT NOT NULL,status TEXT NOT NULL,created_at TEXT NOT NULL,accepted_memory_id INTEGER);
""";
        command.ExecuteNonQuery();
        EnsureColumn("evidence_feedback", "telemetry_json", "TEXT");
    }

    public EvidenceFeedback Record(string packetId, IReadOnlyList<string> useful, IReadOnlyList<string> notUseful,
        string? outcome = null, string? runId = null, EvidenceOutcomeTelemetry? telemetry = null)
    {
        ValidateId(packetId, "packet-");
        foreach (var evidenceId in useful.Concat(notUseful)) ValidateId(evidenceId, "evidence-");
        if (useful.Intersect(notUseful, StringComparer.OrdinalIgnoreCase).Any())
            throw new ArgumentException("An evidence ID cannot be both useful and not useful.");
        var usefulJson = JsonSerializer.Serialize(useful.Distinct().Order().ToArray());
        var notJson = JsonSerializer.Serialize(notUseful.Distinct().Order().ToArray());
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO evidence_feedback(packet_id,useful_json,not_useful_json,outcome,run_id,created_at,telemetry_json) VALUES($p,$u,$n,$o,$r,$c,$t); SELECT id FROM evidence_feedback WHERE packet_id=$p AND useful_json=$u AND not_useful_json=$n AND outcome IS $o AND run_id IS $r;";
        command.Parameters.AddWithValue("$p", packetId); command.Parameters.AddWithValue("$u", usefulJson);
        command.Parameters.AddWithValue("$n", notJson); command.Parameters.AddWithValue("$o", outcome ?? "");
        command.Parameters.AddWithValue("$r", runId ?? ""); command.Parameters.AddWithValue("$c", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$t", telemetry is null ? (object)DBNull.Value : JsonSerializer.Serialize(telemetry));
        var feedbackId = Convert.ToInt64(command.ExecuteScalar());
        return AllFeedback().Single(item => item.Id == feedbackId);
    }

    public IReadOnlyList<EvidenceFeedback> AllFeedback() => ReadFeedback("SELECT id,packet_id,useful_json,not_useful_json,outcome,run_id,created_at,telemetry_json FROM evidence_feedback ORDER BY id DESC");
    public EvidenceOutcomeMetrics Metrics()
    {
        var all = AllFeedback();
        var useful = all.Sum(item => item.UsefulEvidenceIds.Count); var total = useful + all.Sum(item => item.NotUsefulEvidenceIds.Count);
        var measuredTokens = all.Select(item => item.Telemetry?.TokensAvoided).Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        var usefulTimes = all.Select(item => item.Telemetry?.TimeToUsefulEvidenceMilliseconds ?? item.Telemetry?.ElapsedMilliseconds)
            .Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        var passingTimes = all.Where(item => item.Telemetry?.VerificationResult?.Equals("passed", StringComparison.OrdinalIgnoreCase) == true)
            .Select(item => item.Telemetry!.TimeToPassingVerificationMilliseconds ?? item.Telemetry.ElapsedMilliseconds)
            .Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        var diagnosisTimes = all.Select(item => item.Telemetry?.TimeToDiagnosisMilliseconds)
            .Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        var sourceReads = all.Select(item => item.Telemetry?.DirectSourceReads)
            .Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        var gatewayCalls = all.Select(item => item.Telemetry?.GatewayCalls)
            .Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return new(total == 0 ? 0 : useful / (double)total, all.Count,
            measuredTokens.Length == 0 ? null : measuredTokens.Sum(), usefulTimes.Length == 0 ? null : usefulTimes.Average(),
            passingTimes.Length == 0 ? null : passingTimes.Average(),
            all.Any(item => item.Telemetry?.CloudInputTokens is not null || item.Telemetry?.CloudOutputTokens is not null)
                ? "reported" : "Unavailable",
            diagnosisTimes.Length == 0 ? null : diagnosisTimes.Average(),
            sourceReads.Length == 0 ? null : sourceReads.Average(),
            gatewayCalls.Length == 0 ? null : gatewayCalls.Average());
    }
    public IReadOnlyDictionary<string, int> EvidenceWeights()
    {
        Dictionary<string, int> weights = new(StringComparer.OrdinalIgnoreCase);
        foreach (var feedback in AllFeedback())
        { foreach (var id in feedback.UsefulEvidenceIds) weights[id] = Math.Min(10, weights.GetValueOrDefault(id) + 1);
          foreach (var id in feedback.NotUsefulEvidenceIds) weights[id] = Math.Max(-10, weights.GetValueOrDefault(id) - 1); }
        return weights;
    }

    public static IReadOnlyDictionary<string, int> TryReadWeights(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, int>();
        using var read = new SqliteConnection($"Data Source={Path.GetFullPath(path)};Mode=ReadOnly;Pooling=False");
        read.Open();
        using var exists = read.CreateCommand(); exists.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='evidence_feedback'";
        if (Convert.ToInt32(exists.ExecuteScalar()) == 0) return new Dictionary<string, int>();
        using var command = read.CreateCommand(); command.CommandText = "SELECT useful_json,not_useful_json FROM evidence_feedback";
        using var reader = command.ExecuteReader(); Dictionary<string,int> weights=new(StringComparer.OrdinalIgnoreCase);
        while(reader.Read()) { foreach(var id in JsonSerializer.Deserialize<string[]>(reader.GetString(0))??[]) weights[id]=Math.Min(10,weights.GetValueOrDefault(id)+1); foreach(var id in JsonSerializer.Deserialize<string[]>(reader.GetString(1))??[]) weights[id]=Math.Max(-10,weights.GetValueOrDefault(id)-1); }
        return weights;
    }

    public long Propose(EngineeringMemoryKind kind, string title, string body,
        IReadOnlyList<EngineeringProvenance> provenance, string packetId)
    {
        if (kind is not (EngineeringMemoryKind.ConfirmedFact or EngineeringMemoryKind.Fix or EngineeringMemoryKind.RegressionTest))
            throw new ArgumentException("Proposals may create only ConfirmedFact, Fix, or RegressionTest memory.");
        ValidateId(packetId, "packet-");
        if (provenance.Count == 0) throw new ArgumentException("A proposal requires provenance.");
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO memory_proposals(kind,title,body,provenance_json,packet_id,status,created_at) VALUES($k,$t,$b,$p,$i,'Pending',$c); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$k", kind.ToString()); command.Parameters.AddWithValue("$t", title);
        command.Parameters.AddWithValue("$b", body); command.Parameters.AddWithValue("$p", JsonSerializer.Serialize(provenance));
        command.Parameters.AddWithValue("$i", packetId); command.Parameters.AddWithValue("$c", DateTimeOffset.UtcNow.ToString("O"));
        return Convert.ToInt64(command.ExecuteScalar());
    }
    public IReadOnlyList<MemoryProposal> Proposals() => ReadProposals();
    public MemoryProposal GetProposal(long id) => ReadProposals(id).SingleOrDefault() ?? throw new KeyNotFoundException($"Proposal {id} was not found.");
    public MemoryProposal Reject(long id) => Transition(id, MemoryProposalStatus.Rejected, null);
    public MemoryProposal Accept(long id, EngineeringMemoryStore memory)
    {
        var proposal = GetProposal(id);
        if (proposal.Status != MemoryProposalStatus.Pending) throw new InvalidOperationException("Only pending proposals can be accepted.");
        var memoryId = memory.Add(new(0, proposal.Kind, proposal.Title, proposal.Body, proposal.Provenance, DateTimeOffset.UtcNow));
        return Transition(id, MemoryProposalStatus.Accepted, memoryId);
    }
    private MemoryProposal Transition(long id, MemoryProposalStatus status, long? memoryId)
    { _ = GetProposal(id); using var command = connection.CreateCommand(); command.CommandText = "UPDATE memory_proposals SET status=$s,accepted_memory_id=$m WHERE id=$id";
      command.Parameters.AddWithValue("$s", status.ToString()); command.Parameters.AddWithValue("$m", memoryId ?? (object)DBNull.Value); command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery(); return GetProposal(id); }
    private IReadOnlyList<EvidenceFeedback> ReadFeedback(string sql)
    { using var command = connection.CreateCommand(); command.CommandText = sql; using var reader = command.ExecuteReader(); List<EvidenceFeedback> rows=[];
      while(reader.Read()) rows.Add(new(reader.GetInt64(0),reader.GetString(1),JsonSerializer.Deserialize<string[]>(reader.GetString(2))??[],JsonSerializer.Deserialize<string[]>(reader.GetString(3))??[],reader.IsDBNull(4)||reader.GetString(4).Length==0?null:reader.GetString(4),reader.IsDBNull(5)||reader.GetString(5).Length==0?null:reader.GetString(5),DateTimeOffset.Parse(reader.GetString(6)),reader.FieldCount < 8 || reader.IsDBNull(7) ? null : JsonSerializer.Deserialize<EvidenceOutcomeTelemetry>(reader.GetString(7)))); return rows; }
    private IReadOnlyList<MemoryProposal> ReadProposals(long? id=null)
    { using var command=connection.CreateCommand(); command.CommandText="SELECT id,kind,title,body,provenance_json,packet_id,status,created_at,accepted_memory_id FROM memory_proposals"+(id is null?" ORDER BY id DESC":" WHERE id=$id"); if(id is not null) command.Parameters.AddWithValue("$id",id); using var reader=command.ExecuteReader(); List<MemoryProposal> rows=[];
      while(reader.Read()) rows.Add(new(reader.GetInt64(0),Enum.Parse<EngineeringMemoryKind>(reader.GetString(1)),reader.GetString(2),reader.GetString(3),JsonSerializer.Deserialize<EngineeringProvenance[]>(reader.GetString(4))??[],reader.GetString(5),Enum.Parse<MemoryProposalStatus>(reader.GetString(6)),DateTimeOffset.Parse(reader.GetString(7)),reader.IsDBNull(8)?null:reader.GetInt64(8))); return rows; }
    private static void ValidateId(string id,string prefix) { if(!id.StartsWith(prefix,StringComparison.Ordinal)||id.Length!=prefix.Length+64) throw new ArgumentException($"Invalid immutable {prefix.TrimEnd('-')} ID '{id}'."); }
    private void EnsureColumn(string table, string column, string type)
    { using var check=connection.CreateCommand(); check.CommandText=$"PRAGMA table_info({table})"; using var reader=check.ExecuteReader(); while(reader.Read()) if(reader.GetString(1).Equals(column,StringComparison.OrdinalIgnoreCase)) return; reader.Close(); using var alter=connection.CreateCommand(); alter.CommandText=$"ALTER TABLE {table} ADD COLUMN {column} {type}"; alter.ExecuteNonQuery(); }
    public void Dispose()=>connection.Dispose();
}
