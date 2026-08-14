using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed record GatewayBenchmarkEvidence(string Path, string? SymbolId, string[] Markers);
public sealed record GatewayBenchmarkFixture(string Name, string Task, string[] RequiredPaths,
    string[] RelevantPaths, string[] DistractorPaths, string Verification,
    GatewayBenchmarkEvidence[]? RequiredEvidence = null, string[]? ReferenceTopics = null,
    double MinimumPrecisionAt5 = .40, double MinimumReciprocalRank = .50);
public sealed record GatewayBenchmarkCorpus(int Version, IReadOnlyList<GatewayBenchmarkFixture> Tasks,
    string Digest);
public sealed record GatewayBenchmarkCase(string Name, int BudgetBytes, double RequiredEvidenceRecall,
    double PrecisionAtK, double ReciprocalRank, IReadOnlyList<string> FalsePositives,
    IReadOnlyList<string> Omissions, double PacketEvidenceRecall, IReadOnlyList<string> PacketOmissions,
    int RawCandidateBytes, int PacketBytes, int CliBytes, int McpBytes,
    long LatencyMilliseconds, bool RoslynCacheUsed, bool WithinResponseLimit,
    string VerificationCommand, bool RequiredEvidenceComplete = true);
public sealed record GatewayBenchmarkResult(int CorpusVersion, string CorpusDigest, int Total, int Passed,
    IReadOnlyList<GatewayBenchmarkCase> Cases, GatewayAgentBenchmarkResult? AgentTier = null,
    IReadOnlyList<string>? ProfileCoverageGaps = null);
public sealed record GatewayAgentBenchmarkCase(string Name, bool Passed, IReadOnlyList<string> CitationIds,
    string Response, double LatencyMilliseconds, string? Failure);
public sealed record GatewayAgentBenchmarkResult(string Model, int Passed, int Total,
    IReadOnlyList<GatewayAgentBenchmarkCase> Cases);

public static class GatewayBenchmark
{
    public static GatewayBenchmarkCorpus LoadCorpus(string repositoryRoot, int? requestedVersion = null)
    {
        var version = requestedVersion ?? 1;
        var path = Path.Combine(repositoryRoot, "src", "tools", "nes-lab", $"gateway-corpus.v{version}.json");
        var bytes = File.ReadAllBytes(path);
        using var document = JsonDocument.Parse(bytes);
        var parsedVersion = document.RootElement.GetProperty("version").GetInt32();
        var tasks = document.RootElement.GetProperty("tasks").Deserialize<GatewayBenchmarkFixture[]>(
            LabResponseSerializer.Options) ?? throw new InvalidDataException("Gateway corpus has no tasks.");
        return new(parsedVersion, tasks, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    public static async Task<GatewayBenchmarkResult> RunAsync(string repositoryRoot, int? corpusVersion = null,
        bool evaluateLocalAgent = false, string model = "nes-lab:devstral-24b",
        CancellationToken cancellationToken = default)
    {
        var corpus = LoadCorpus(repositoryRoot, corpusVersion);
        var profileGaps = ValidateProfileCoverage(repositoryRoot, corpus);
        var solution = Path.Combine(repositoryRoot, "nes-emulator-3.slnx");
        var cacheDirectory = Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "roslyn");
        var cacheUsed = Directory.Exists(cacheDirectory);
        RepositoryTextIndex.Build(repositoryRoot, Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "retrieval.sqlite"));
        await using var index = await RoslynSymbolIndex.OpenAsync(solution, cancellationToken, persistCache: true);
        List<GatewayBenchmarkCase> cases = [];
        List<GatewayAgentBenchmarkCase> agentCases = [];
        foreach (var fixture in corpus.Tasks)
        foreach (var budget in new[] { 2_048, 16_384 })
        {
            var stopwatch = Stopwatch.StartNew();
            var retrieval = await HybridTaskRetriever.RetrieveAsync(index, repositoryRoot, fixture.Task, cancellationToken);
            var requiredEvidence = fixture.RequiredEvidence is { Length: > 0 }
                ? fixture.RequiredEvidence : fixture.RequiredPaths.Select(path =>
                    new GatewayBenchmarkEvidence(path, null, [Path.GetFileNameWithoutExtension(path)])).ToArray();
            var markedEvidence = retrieval.Evidence.Select(item =>
            {
                var requirement = requiredEvidence.FirstOrDefault(required => Normalize(item.Source).EndsWith(
                    Normalize(required.Path), StringComparison.OrdinalIgnoreCase));
                return requirement is null ? item : item with
                {
                    Required = true,
                    RequiredMarkers = requirement.Markers
                };
            }).ToArray();
            var packet = ContextPacketBuilder.Build(markedEvidence, budget);
            stopwatch.Stop();
            var ranked = retrieval.Evidence.Select(item => Normalize(item.Source)).ToArray();
            var required = requiredEvidence.Select(item => Normalize(item.Path)).ToArray();
            var relevant = required.Concat(fixture.RelevantPaths.Select(Normalize)).ToArray();
            var hitRanks = required.Select(path => Array.FindIndex(ranked, candidate => candidate.EndsWith(path,
                StringComparison.OrdinalIgnoreCase))).ToArray();
            var recall = hitRanks.Count(rank => rank >= 0) / (double)Math.Max(1, required.Length);
            var first = hitRanks.Where(rank => rank >= 0).DefaultIfEmpty(-1).Min();
            var topK = ranked.Take(Math.Max(relevant.Length, 5)).ToArray();
            var precision = topK.Count(candidate => relevant.Any(path => candidate.EndsWith(path,
                StringComparison.OrdinalIgnoreCase))) / (double)Math.Max(1, topK.Length);
            var falsePositives = ranked.Where(candidate => fixture.DistractorPaths.Any(path => candidate.EndsWith(
                Normalize(path), StringComparison.OrdinalIgnoreCase))).Distinct().ToArray();
            var packetOmissions = requiredEvidence.Where(requiredItem =>
                !packet.Content.Contains(Normalize(requiredItem.Path), StringComparison.OrdinalIgnoreCase) ||
                requiredItem.Markers.Any(marker => !packet.Content.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                .Select(item => item.Path).ToArray();
            var packetRecall = (requiredEvidence.Length - packetOmissions.Length) /
                (double)Math.Max(1, requiredEvidence.Length);
            var rawBytes = retrieval.Evidence.Sum(item => Encoding.UTF8.GetByteCount(item.Content));
            var cliBytes = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, packet }).Length;
            var mcpBytes = JsonSerializer.SerializeToUtf8Bytes(new
                { content = new[] { new { type = "text", text = packet.Content } } }).Length;
            cases.Add(new(fixture.Name, budget, recall, precision, first < 0 ? 0 : 1d / (first + 1),
                falsePositives, required.Where((_, i) => hitRanks[i] < 0).ToArray(), packetRecall, packetOmissions, rawBytes,
                packet.UsedBytes, cliBytes, mcpBytes, stopwatch.ElapsedMilliseconds, cacheUsed,
                mcpBytes <= McpResponseLimits.MaximumInspectionBytes, fixture.Verification,
                packet.RequiredEvidence?.All(item => item.Status is RequiredEvidenceStatus.Complete or RequiredEvidenceStatus.Excerpted) == true));
            if (evaluateLocalAgent && budget == 16_384)
            {
                var packetEvidence = retrieval.Evidence.Where(item => packet.Content.Contains(item.EvidenceId!, StringComparison.Ordinal)).ToArray();
                var candidates = packetEvidence.Select(item => new EvidenceCandidate(item.EvidenceId!, item.Kind.ToString(),
                    item.Content, item.Source, item.Priority)).ToArray();
                var watch = Stopwatch.StartNew();
                try
                {
                    using var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434/"), Timeout = TimeSpan.FromMinutes(5) };
                    var response = await new OllamaEvidenceModel(http, model).SelectAsync(candidates, 8, cancellationToken);
                    watch.Stop();
                    var known = response.SelectedIds.All(id => candidates.Any(candidate => candidate.Id == id));
                    var citesRequired = response.SelectedIds.Any(id => candidates.Any(candidate => candidate.Id == id &&
                        required.Any(path => Normalize(candidate.Source).EndsWith(path, StringComparison.OrdinalIgnoreCase))));
                    agentCases.Add(new(fixture.Name, known && citesRequired && response.Summary.Length > 0,
                        response.SelectedIds, response.Summary, watch.Elapsed.TotalMilliseconds,
                        known && citesRequired ? null : "Response omitted required repository evidence or returned an unknown citation."));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                { watch.Stop(); agentCases.Add(new(fixture.Name, false, [], "", watch.Elapsed.TotalMilliseconds, exception.Message)); }
            }
        }
        var agent = evaluateLocalAgent ? new GatewayAgentBenchmarkResult(model, agentCases.Count(item => item.Passed),
            agentCases.Count, agentCases) : null;
        var passed = cases.Count(item => PassesQualityGate(item,
                corpus.Tasks.First(task => task.Name == item.Name).MinimumPrecisionAt5,
                corpus.Tasks.First(task => task.Name == item.Name).MinimumReciprocalRank));
        if (profileGaps.Count > 0) passed = 0;
        return new(corpus.Version, corpus.Digest, cases.Count,
            passed, cases, agent, profileGaps);
    }

    public static bool PassesQualityGate(GatewayBenchmarkCase item, double minimumPrecisionAt5, double minimumReciprocalRank) =>
        item.RequiredEvidenceRecall == 1 && item.PacketEvidenceRecall == 1 &&
        item.RequiredEvidenceComplete &&
        item.PrecisionAtK >= minimumPrecisionAt5 && item.ReciprocalRank >= minimumReciprocalRank &&
        item.FalsePositives.Count == 0 && item.WithinResponseLimit;

    private static string Normalize(string value) => value.Replace('\\', '/');

    public static IReadOnlyList<string> ValidateProfileCoverage(string repositoryRoot, GatewayBenchmarkCorpus corpus)
    {
        var profiles = RetrievalProfiles.Load(repositoryRoot);
        var references = new ReferenceCorpusStore(Path.Combine(repositoryRoot, "src", "tools", "nes-lab", "reference-corpus.v1.json"),
            Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "references")).Entries;
        List<string> gaps = [];
        foreach (var task in corpus.Tasks)
        {
            var seeds = RetrievalProfiles.MatchSeedPaths(profiles, task.Task).Select(Normalize).ToArray();
            var subsystem = RetrievalProfiles.MatchSubsystemPaths(profiles, task.Task).Select(Normalize).ToArray();
            foreach (var required in task.RequiredEvidence ?? [])
                if (!seeds.Any(path => path.EndsWith(Normalize(required.Path), StringComparison.OrdinalIgnoreCase)) &&
                    !subsystem.Any(path => Normalize(required.Path).Contains(path, StringComparison.OrdinalIgnoreCase)))
                    gaps.Add($"{task.Name}:unprofiled-evidence:{required.Path}");
            foreach (var topic in task.ReferenceTopics ?? [])
                if (!references.Any(reference => reference.Topics.Contains(topic, StringComparer.OrdinalIgnoreCase) ||
                    reference.Aliases.Contains(topic, StringComparer.OrdinalIgnoreCase)))
                    gaps.Add($"{task.Name}:unrecognized-reference-topic:{topic}");
        }
        return gaps;
    }
}
