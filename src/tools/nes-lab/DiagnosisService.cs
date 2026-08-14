using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed record DiagnosisPacket(
    int SchemaVersion, string PacketId, string RunId, JsonElement Packet,
    int BudgetBytes, int UsedBytes, int IncludedEvidenceCount, int OmittedEvidenceCount, bool Truncated,
    IReadOnlyList<ContextCategoryAccounting>? Categories,
    string ReproductionCommand, string ArtifactPath, string? TraceArtifactPath,
    string? PacketArtifactPath,
    string? ResourceUri = null,
    LocalEvidenceRanking.Provenance? Ranking = null,
    int InputEvidenceCount = 0, int UniqueEvidenceCount = 0, int DuplicateEvidenceCount = 0);

public static class DiagnosisService
{
    public static async Task<DiagnosisPacket> BuildAsync(
        RunHistoryEntry run, string repositoryRoot, int budgetBytes, bool persist = true,
        bool rankLocal = false, string model = "nes-lab:devstral-24b",
        string endpoint = "http://localhost:11434/",
        CancellationToken cancellationToken = default)
    {
        List<ContextEvidence> evidence = [];
        evidence.Add(new ContextEvidence(ContextEvidenceKind.VerificationFailure, 125,
            JsonSerializer.Serialize(new
            {
                run.VerificationStatus,
                run.ExitPolicy,
                run.MatchesAcceptedBaseline,
                run.HasRegressions,
                run.HasResolvedBaselineCases
            }, LabResponseSerializer.Options), $"run://{run.Id}"));
        var log = await File.ReadAllTextAsync(run.ArtifactPath, cancellationToken).ConfigureAwait(false);
        var failures = VerificationOutputParser.ParseFailures(log);
        var reducedFailure = failures.Count == 0
            ? $"Outcome: {run.Outcome}. See retained log."
            : string.Join('\n', failures.Select(item => $"{item.Name}: {item.Diagnostic}"));
        evidence.Add(new ContextEvidence(ContextEvidenceKind.VerificationFailure, 120,
            Limit(reducedFailure, 1_800), run.ArtifactPath, Hash(log)));

        TraceArtifact? loadedTrace = null;
        if (run.TraceArtifactPath is { } tracePath && File.Exists(tracePath))
        {
            var trace = await new TraceArtifactReader().ReadAsync(tracePath, cancellationToken).ConfigureAwait(false);
            loadedTrace = trace;
            var selected = SelectWindow(trace, reducedFailure);
            var records = selected?.Records.TakeLast(16).ToArray() ?? trace.Records.TakeLast(8).ToArray();
            var traceContent = JsonSerializer.Serialize(new { trace.Run,
                evidenceKind = selected is null ? "terminal-fallback" : "checkpoint",
                checkpoint = selected is null ? null : new { selected.Name, selected.Kind,
                    selected.TriggerSource, selected.CpuClock, selected.FirstCpuClock, selected.LastCpuClock,
                    selected.DroppedRecordCount, selected.ResetGeneration },
                trace.BoundaryKind, trace.BoundaryCpuClock, trace.DroppedRecordCount, records },
                LabResponseSerializer.Options);
            evidence.Add(new ContextEvidence(ContextEvidenceKind.TraceWindow, 115,
                Limit(traceContent, 2_200),
                tracePath, trace.RomSha256));
        }

        var referenceManifest = Path.Combine(repositoryRoot, "src", "tools", "nes-lab", "reference-corpus.v1.json");
        if (File.Exists(referenceManifest))
        {
            var referenceStore = new ReferenceCorpusStore(referenceManifest,
                Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "references"));
            var referenceQuery = $"{run.CaseName} {reducedFailure}";
            foreach (var entry in referenceStore.FindEntries(referenceQuery, 5))
            {
                var status = referenceStore.Status().First(item => item.Id == entry.Id);
                var content = $"{entry.Title}\n" + string.Join('\n', entry.Claims.Select(claim =>
                    $"[{entry.Id}#{claim.Section}] {claim.Summary}")) + $"\nCanonical source: {entry.CanonicalUrl}\nPinned digest: {entry.ExpectedSha256}\nAvailability: {status.Availability}";
                evidence.Add(new ContextEvidence(ContextEvidenceKind.Reference, 103, content,
                    entry.CanonicalUrl, entry.ExpectedSha256));
            }
            if (run.CaseName is { } referenceCase)
            {
                try
                {
                    var readme = referenceStore.Show("accuracycoin-readme");
                    var excerpt = FocusReference(readme.Content, referenceCase, 40);
                    if (excerpt is not null)
                        evidence.Add(new ContextEvidence(ContextEvidenceKind.RomSource, 111,
                            $"AccuracyCoin observable expectation ({referenceCase}):\n{excerpt}",
                            readme.Entry.CanonicalUrl, readme.Sha256));
                }
                catch (FileNotFoundException) { }
            }
            if (loadedTrace is not null)
            {
                var available = referenceStore.Entries.Select(entry =>
                {
                    try { return referenceStore.Show(entry.Id); } catch { return null; }
                }).Where(item => item is not null).Cast<ReferenceDocument>()
                    .ToDictionary(item => item.Entry.Id, StringComparer.OrdinalIgnoreCase);
                foreach (var observation in HardwareRuleEvaluator.Evaluate(loadedTrace, available))
                    evidence.Add(new ContextEvidence(ContextEvidenceKind.TraceWindow, 114,
                        JsonSerializer.Serialize(observation, LabResponseSerializer.Options),
                        $"reference://{observation.ReferenceId}", observation.ReferenceDigest));
            }
        }

        var manifest = Path.Combine(repositoryRoot, "test", "conformance", "test-roms.json");
        var assets = Path.Combine(repositoryRoot, "test-roms", "nes-test-roms");
        if (run.CaseName is { } caseName && File.Exists(manifest))
        {
            var catalog = RomCatalog.Load(manifest, Directory.Exists(assets) ? assets : null);
            var matches = catalog.Entries.Where(item => item.Name.Equals(caseName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 1)
                evidence.Add(new ContextEvidence(ContextEvidenceKind.RomSource, 105,
                    JsonSerializer.Serialize(new { matches[0].Suite, matches[0].Name, matches[0].Protocols,
                        matches[0].KnownGap, matches[0].ExpectedSha256 }, LabResponseSerializer.Options),
                    manifest, catalog.ManifestSha256));
        }

        if (run.CaseName is { } sourceCase)
        {
            var encoded = ExtractAccuracyCoinResult(reducedFailure);
            if (AccuracyCoinSourceResolver.Resolve(repositoryRoot, sourceCase, encoded) is { } accuracySource)
                evidence.Add(new ContextEvidence(ContextEvidenceKind.RomSource, 109,
                    Limit(JsonSerializer.Serialize(accuracySource, LabResponseSerializer.Options), 4_000),
                    accuracySource.SourcePath, accuracySource.SourceSha256, accuracySource.RoutineLine));
            else if (FindRomSource(repositoryRoot, sourceCase) is { } sourceExcerpt)
                evidence.Add(sourceExcerpt);
        }

        var knowledgePath = Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "knowledge.db");
        if (run.CaseName is { } query && File.Exists(knowledgePath))
        {
            using var memory = new EngineeringMemoryStore(knowledgePath);
            foreach (var item in memory.Search(query, maximumResults: 8, includeRejectedHypotheses: true))
                evidence.Add(new ContextEvidence(ContextEvidenceKind.EngineeringMemory,
                    item.Kind is EngineeringMemoryKind.ConfirmedFact or EngineeringMemoryKind.Fix ? 95 : 60,
                    $"{item.Kind}: {item.Title}\n{item.Body}", $"{knowledgePath}#{item.Id}"));
        }

        var symbols = SelectSymbols(run.CaseName, reducedFailure);
        await using (var index = await RoslynSymbolIndex.OpenAsync(
            Path.Combine(repositoryRoot, "nes-emulator-3.slnx"), cancellationToken).ConfigureAwait(false))
            foreach (var symbol in symbols)
                evidence.AddRange((await SymbolContextPacketBuilder.CollectAsync(
                    index, repositoryRoot, symbol, cancellationToken).ConfigureAwait(false))
                    .Select(ConstrainSourceEvidence));

        foreach (var source in evidence.Where(item => item.Kind == ContextEvidenceKind.Declaration)
            .Select(item => item.Source).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var diff = await new ProcessCommandExecutor().ExecuteAsync(
                new VerificationCommand(VerificationScope.LabTests, "git", ["diff", "--", source]),
                repositoryRoot, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(diff.StandardOutput))
                evidence.Add(new ContextEvidence(ContextEvidenceKind.GitDiff, 85,
                    Limit(diff.StandardOutput, 3_000), $"git diff -- {source}"));
        }

        var reproduction = $"dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- verify --scope {Scope(run.Scope)}" +
            (run.CaseName is null ? "" : $" --case \"{run.CaseName}\" --trace-on-failure");
        evidence.Add(new ContextEvidence(ContextEvidenceKind.Guidance, 110,
            reproduction, "nes-lab reproduction command"));
        var ranked = await LocalEvidenceRanking.ApplyAsync(evidence, rankLocal, model, endpoint, cancellationToken)
            .ConfigureAwait(false);
        var packet = ContextPacketBuilder.Build(ranked.Evidence, budgetBytes);
        var packetId = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(packet.Content)))[..24];
        string? packetPath = null;
        string? resourceUri = null;
        if (persist)
        {
            var packetDirectory = Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "contexts");
            Directory.CreateDirectory(packetDirectory);
            packetPath = Path.Combine(packetDirectory, packetId + ".json");
            await File.WriteAllTextAsync(packetPath, packet.Content, cancellationToken).ConfigureAwait(false);
            var published = await new ImmutableArtifactStore(Path.Combine(repositoryRoot, ".artifacts", "nes-lab"))
                .PublishTextAsync("context", packet.Content, "application/json", true,
                    reproduction, cancellationToken).ConfigureAwait(false);
            resourceUri = ImmutableArtifactStore.Uri("context", published.Digest);
        }
        using var packetDocument = JsonDocument.Parse(packet.Content);
        return new DiagnosisPacket(2, packetId, run.Id, packetDocument.RootElement.Clone(),
            packet.BudgetBytes, packet.UsedBytes, packet.IncludedEvidenceCount,
            packet.OmittedEvidenceCount, packet.Truncated, packet.Categories, reproduction,
            run.ArtifactPath, run.TraceArtifactPath, packetPath, resourceUri,
            LocalEvidenceRanking.Describe(ranked.Ranking), packet.InputEvidenceCount,
            packet.UniqueEvidenceCount, packet.DuplicateEvidenceCount);
    }

    public static TraceCheckpointWindow? SelectWindow(TraceArtifact trace, string failure)
    {
        var windows = trace.Windows ?? [];
        if (windows.Count == 0) return null;
        string[] preferred = failure.Contains("DMC", StringComparison.OrdinalIgnoreCase)
            ? ["first-dmc-dma-request", "result-write", "terminal-state"]
            : failure.Contains("DMA", StringComparison.OrdinalIgnoreCase)
                ? ["first-oam-dma-request", "first-dmc-dma-request", "result-write", "terminal-state"]
                : ["first-unexpected-status", "result-write", "terminal-state", "test-entry"];
        return preferred.Select(name => windows.FirstOrDefault(window =>
                window.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(window => window is not null) ?? windows[^1];
    }

    private static IReadOnlyList<RoslynSymbolQuery> SelectSymbols(string? caseName, string failure) =>
        ($"{caseName} {failure}").Contains("DMA", StringComparison.OrdinalIgnoreCase) ?
            [Exact("CpuClockDriver", "Sheep.Emulation.Nes.Cpu.CpuClockDriver"),
             Exact("CpuBus", "Sheep.Emulation.Nes.Cpu.CpuBus")] :
        ($"{caseName} {failure}").Contains("PPU", StringComparison.OrdinalIgnoreCase) ? [new("Ppu", Kind: "NamedType")] :
        ($"{caseName} {failure}").Contains("APU", StringComparison.OrdinalIgnoreCase) ||
        ($"{caseName} {failure}").Contains("DMC", StringComparison.OrdinalIgnoreCase) ?
            [new("Apu", Kind: "NamedType")] : [new("NesSystem", Kind: "NamedType")];

    private static RoslynSymbolQuery Exact(string name, string qualified) =>
        new(name, ExactQualifiedName: qualified, Kind: "NamedType");

    private static ContextEvidence? FindRomSource(string repositoryRoot, string caseName)
    {
        var root = Path.Combine(repositoryRoot, "test-roms");
        if (!Directory.Exists(root)) return null;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".asm" or ".s"))
        {
            var lines = File.ReadAllLines(path);
            var index = Array.FindIndex(lines, line => line.Contains(caseName, StringComparison.OrdinalIgnoreCase));
            if (index < 0) continue;
            var start = Math.Max(0, index - 20);
            var excerpt = string.Join('\n', lines.Skip(start).Take(60));
            return new ContextEvidence(ContextEvidenceKind.RomSource, 108, Limit(excerpt, 1_800),
                Path.GetRelativePath(repositoryRoot, path), Hash(string.Join('\n', lines)), start + 1);
        }
        return null;
    }

    private static string Scope(VerificationScope scope) => scope.ToString().ToLowerInvariant();
    private static ContextEvidence ConstrainSourceEvidence(ContextEvidence item)
    {
        var ceiling = item.Kind switch
        {
            ContextEvidenceKind.Declaration => 1_800,
            ContextEvidenceKind.AffectedTest => 1_800,
            ContextEvidenceKind.Reference => 1_000,
            ContextEvidenceKind.Guidance => 500,
            _ => 1_500
        };
        return item with { Content = Limit(item.Content, ceiling) };
    }
    private static string Limit(string value, int maximum) => value.Length <= maximum ? value : value[..maximum] + "\n…[truncated]";
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static int? ExtractAccuracyCoinResult(string failure)
    {
        var marker = failure.IndexOf("value $", StringComparison.OrdinalIgnoreCase);
        if (marker < 0 || marker + 8 > failure.Length) return null;
        return int.TryParse(failure.AsSpan(marker + 7, 2), System.Globalization.NumberStyles.HexNumber,
            null, out var value) ? value : null;
    }
    private static string? FocusReference(string content, string marker, int maximumLines)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var index = Array.FindIndex(lines, line => line.Contains(marker, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;
        var start = Math.Max(0, index - 3);
        return string.Join('\n', lines.Skip(start).Take(maximumLines));
    }
}
