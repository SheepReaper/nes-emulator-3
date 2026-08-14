using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sheep.Emulation.Nes;
using Sheep.Emulation.Nes.Debugging;
using Sheep.Emulation.Nes.Input;
using Sheep.Emulation.Nes.Timing;

namespace Sheep.Nes.Lab;

public sealed record ExperimentRomSource(string? Path = null, string? ManifestCase = null,
    string? Suite = null, string? ArtifactUri = null);
public sealed record ExperimentStopCondition(string Kind, ulong Value = 0, ushort? Address = null);
public sealed record ExperimentControllerChange(ulong BeforePpuDot, int Port, NesControllerButton Buttons);
public sealed record ExperimentCapturePoint(string Id, ulong AtPpuDot, bool Snapshot = true,
    bool Frame = false, bool Audio = false, bool Trace = false);
public sealed record NesExperimentScenario(int SchemaVersion, string Name, ExperimentRomSource Rom,
    string VideoStandard, ExperimentStopCondition Stop, ExperimentControllerChange[] ControllerChanges,
    ExperimentCapturePoint[] CapturePoints, int MaximumTraceRecords = 4096,
    int MaximumAudioSamples = 48000, ulong MaximumPpuDots = 60_000_000);
public sealed record ExperimentCaptureResult(string Id, ulong PpuDot, ulong CpuClock,
    string? SnapshotUri, string? SnapshotSha256, string? FrameUri, string? FrameSha256,
    string? AudioUri, string? AudioSha256, int AudioSampleCount, string? TraceUri,
    string? TraceSha256, IReadOnlyDictionary<string, int> DmaEvents,
    int IrqTransitions, int NmiTransitions);
public sealed record NesExperimentResult(int SchemaVersion, string Name, string ScenarioSha256,
    string RomSha256, ulong PpuDots, ulong CpuClocks, ulong Frames, string StopReason,
    IReadOnlyList<ExperimentCaptureResult> Captures, string ReproductionCommand,
    string? ResourceUri = null, string? ScenarioUri = null);
public sealed record ExperimentDifference(string CaptureId, string Category, string? Left, string? Right);
public sealed record ExperimentComparison(bool Equal, IReadOnlyList<ExperimentDifference> Differences,
    string LeftScenarioSha256, string RightScenarioSha256);

public sealed class NesExperimentService(string repositoryRoot)
{
    private readonly string root = Path.GetFullPath(repositoryRoot);
    private readonly ImmutableArtifactStore artifacts = new(Path.Combine(Path.GetFullPath(repositoryRoot), ".artifacts", "nes-lab"));

    public async Task<NesExperimentResult> RunAsync(string scenarioPath, bool mcpSafe = false,
        CancellationToken cancellationToken = default)
    {
        var scenarioBytes = await File.ReadAllBytesAsync(scenarioPath, cancellationToken);
        if (scenarioBytes.Length > 64 * 1024) throw new InvalidDataException("Experiment scenario exceeds 64 KiB.");
        var scenario = Deserialize(scenarioBytes);
        var reproduction = $"nes-lab experiment run --scenario \"{Path.GetRelativePath(root, Path.GetFullPath(scenarioPath))}\"";
        return await RunScenarioAsync(scenario, mcpSafe, reproduction, null, cancellationToken);
    }

    public async Task<NesExperimentResult> RunInlineAsync(string json, bool mcpSafe = false,
        CancellationToken cancellationToken = default)
    {
        var input = Encoding.UTF8.GetBytes(json);
        if (input.Length > 64 * 1024) throw new InvalidDataException("Inline experiment scenario exceeds 64 KiB.");
        var scenario = Deserialize(input);
        Validate(scenario, mcpSafe);
        var canonical = JsonSerializer.SerializeToUtf8Bytes(scenario, LabResponseSerializer.Options);
        var metadata = await artifacts.PublishBytesAsync("scenario", canonical,
            "application/vnd.nes-lab.experiment-scenario+json", pinned: true,
            reproductionCommand: "nes-lab experiment run --inline <canonical-json>", cancellationToken: cancellationToken);
        var uri = ImmutableArtifactStore.Uri("scenario", metadata.Digest);
        return await RunScenarioAsync(scenario, mcpSafe, $"nes-lab experiment run --scenario-uri {uri}", uri,
            cancellationToken);
    }

    public async Task<NesExperimentResult> RunArtifactAsync(string scenarioUri, bool mcpSafe = false,
        CancellationToken cancellationToken = default)
    {
        if (!ImmutableArtifactStore.TryParseUri(scenarioUri, out var kind, out _) || kind != "scenario")
            throw new ArgumentException("An immutable scenario artifact URI is required.");
        var path = await artifacts.ResolveVerifiedDataPathAsync(scenarioUri, cancellationToken);
        var scenario = Deserialize(await File.ReadAllBytesAsync(path, cancellationToken));
        return await RunScenarioAsync(scenario, mcpSafe, $"nes-lab experiment run --scenario-uri {scenarioUri}",
            scenarioUri, cancellationToken);
    }

    private async Task<NesExperimentResult> RunScenarioAsync(NesExperimentScenario scenario, bool mcpSafe,
        string reproduction, string? scenarioUri, CancellationToken cancellationToken)
    {
        Validate(scenario, mcpSafe);
        var canonical = JsonSerializer.SerializeToUtf8Bytes(scenario, LabResponseSerializer.Options);
        var scenarioHash = Hash(canonical);
        var rom = await ResolveRomAsync(scenario.Rom, mcpSafe, cancellationToken);
        var romHash = Hash(rom);
        var nes = new NesSystem(NesVideoStandard.Ntsc);
        nes.LoadRom(rom);
        nes.Debugger.EnableCpuClockTracing(scenario.MaximumTraceRecords);
        var changes = scenario.ControllerChanges.OrderBy(item => item.BeforePpuDot).ToArray();
        var points = scenario.CapturePoints.OrderBy(item => item.AtPpuDot).ToArray();
        var changeIndex = 0; var pointIndex = 0; ulong dots = 0; ulong clocks = 0; ulong frames = 0;
        List<ExperimentCaptureResult> captures = [];
        while (dots < scenario.MaximumPpuDots && !Stopped(scenario.Stop, nes, dots, clocks, frames))
        {
            cancellationToken.ThrowIfCancellationRequested();
            while (changeIndex < changes.Length && changes[changeIndex].BeforePpuDot == dots)
            { var change = changes[changeIndex++]; nes.SetControllerState(change.Port, change.Buttons); }
            var result = nes.RunForPpuDots(1); dots++; clocks += result.CpuClocks; frames += result.Frames;
            while (pointIndex < points.Length && points[pointIndex].AtPpuDot <= dots)
                captures.Add(await CaptureAsync(nes, points[pointIndex++], dots, clocks, scenario, romHash,
                    scenarioHash, reproduction, cancellationToken));
        }
        if (points.Length == 0 || captures.Count == 0 || captures[^1].PpuDot != dots)
            captures.Add(await CaptureAsync(nes, new("final", dots, true, true, true, true), dots, clocks,
                scenario, romHash, scenarioHash, reproduction, cancellationToken));
        nes.Debugger.DisableCpuClockTracing();
        var resultArtifact = new NesExperimentResult(1, scenario.Name, scenarioHash, romHash, dots, clocks, frames,
            dots >= scenario.MaximumPpuDots && !Stopped(scenario.Stop, nes, dots, clocks, frames)
                ? "maximumPpuDots" : scenario.Stop.Kind, captures, reproduction, ScenarioUri: scenarioUri);
        var json = JsonSerializer.Serialize(resultArtifact, LabResponseSerializer.Options);
        var published = await artifacts.PublishTextAsync("experiment", json, "application/json", true,
            reproduction, cancellationToken);
        return resultArtifact with { ResourceUri = ImmutableArtifactStore.Uri("experiment", published.Digest) };
    }

    public async Task<ExperimentComparison> CompareAsync(string leftUri, string rightUri,
        CancellationToken cancellationToken = default)
    {
        var left = await ReadResultAsync(leftUri, cancellationToken);
        var right = await ReadResultAsync(rightUri, cancellationToken);
        List<ExperimentDifference> differences = [];
        foreach (var capture in left.Captures)
        {
            var other = right.Captures.FirstOrDefault(item => item.Id == capture.Id && item.PpuDot == capture.PpuDot);
            if (other is null) { differences.Add(new(capture.Id, "capture", capture.PpuDot.ToString(), null)); continue; }
            Compare("snapshot", capture.SnapshotSha256, other.SnapshotSha256);
            Compare("frame", capture.FrameSha256, other.FrameSha256);
            Compare("audio", capture.AudioSha256, other.AudioSha256);
            Compare("trace", capture.TraceSha256, other.TraceSha256);
            void Compare(string category, string? a, string? b)
            { if (!string.Equals(a, b, StringComparison.Ordinal)) differences.Add(new(capture.Id, category, a, b)); }
        }
        return new(differences.Count == 0, differences, left.ScenarioSha256, right.ScenarioSha256);
    }

    private async Task<ExperimentCaptureResult> CaptureAsync(NesSystem nes, ExperimentCapturePoint point,
        ulong dots, ulong clocks, NesExperimentScenario scenario, string romHash, string scenarioHash,
        string reproduction, CancellationToken cancellationToken)
    {
        string? snapshotUri = null, snapshotHash = null, frameUri = null, frameHash = null,
            audioUri = null, audioHash = null, traceUri = null, traceHash = null;
        if (point.Snapshot)
        {
            var snapshot = nes.Debugger.CaptureSnapshot(new NesDebugSnapshotOptions { Sections = NesDebugSnapshotSections.Core });
            var text = JsonSerializer.Serialize(snapshot, LabResponseSerializer.Options);
            var item = await artifacts.PublishTextAsync("snapshot", text, "application/json", reproductionCommand: reproduction, cancellationToken: cancellationToken);
            snapshotHash = item.Digest; snapshotUri = ImmutableArtifactStore.Uri("snapshot", item.Digest);
        }
        if (point.Frame)
        {
            var bytes = new byte[NesSystem.FrameBufferSize];
            if (nes.TryCopyFrame(bytes, out _))
            { var item = await artifacts.PublishBytesAsync("frame", bytes, "application/octet-stream", reproductionCommand: reproduction, cancellationToken: cancellationToken); frameHash = item.Digest; frameUri = ImmutableArtifactStore.Uri("frame", item.Digest); }
        }
        var audioCount = 0;
        if (point.Audio)
        {
            var samples = new float[Math.Min(scenario.MaximumAudioSamples, nes.BufferedAudioSampleCount)];
            audioCount = nes.ReadAudioSamples(samples);
            var bytes = new byte[audioCount * sizeof(float)];
            for (var i = 0; i < audioCount; i++) BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), samples[i]);
            var item = await artifacts.PublishBytesAsync("audio", bytes, "application/octet-stream", reproductionCommand: reproduction, cancellationToken: cancellationToken); audioHash = item.Digest; audioUri = ImmutableArtifactStore.Uri("audio", item.Digest);
        }
        var trace = nes.Debugger.GetCpuClockTraceSnapshot();
        if (point.Trace)
        {
            var artifact = TraceArtifactFactory.Create(trace, new TraceArtifactMetadata(romHash, "working-tree",
                DateTimeOffset.UnixEpoch, new TraceRunMetadata(scenario.Name, "Ntsc", "experiment", point.Id)), scenario.MaximumTraceRecords)
                with { CaptureId = Hash(Encoding.UTF8.GetBytes($"{scenarioHash}:{point.Id}:{dots}")) };
            var text = JsonSerializer.Serialize(artifact, LabResponseSerializer.Options);
            var item = await artifacts.PublishTextAsync("trace", text, "application/json", reproductionCommand: reproduction, cancellationToken: cancellationToken); traceHash = item.Digest; traceUri = ImmutableArtifactStore.Uri("trace", item.Digest);
        }
        var records = trace.Records;
        var dma = records.Where(item => item.Phase is not null).GroupBy(item => item.Phase!.DmaEvent)
            .Where(group => group.Key != "none").ToDictionary(group => group.Key, group => group.Count());
        return new(point.Id, dots, clocks, snapshotUri, snapshotHash, frameUri, frameHash, audioUri,
            audioHash, audioCount, traceUri, traceHash, dma, Transitions(records, item => item.IrqLine),
            Transitions(records, item => item.NmiLine));
    }

    private async Task<byte[]> ResolveRomAsync(ExperimentRomSource source, bool mcpSafe, CancellationToken token)
    {
        if (source.Path is not null)
        {
            if (mcpSafe) throw new UnauthorizedAccessException("MCP experiments cannot use custom ROM paths.");
            var path = Path.IsPathRooted(source.Path) ? Path.GetFullPath(source.Path) :
                Path.GetFullPath(Path.Combine(root, source.Path));
            return await File.ReadAllBytesAsync(path, token);
        }
        if (source.ArtifactUri is not null)
        { var path = await artifacts.ResolveVerifiedDataPathAsync(source.ArtifactUri, token); return await File.ReadAllBytesAsync(path, token); }
        if (source.ManifestCase is not null)
        {
            var assets = Path.Combine(root, "test-roms", "nes-test-roms");
            var catalog = RomCatalog.Load(Path.Combine(root, "test", "conformance", "test-roms.json"), assets);
            var entry = catalog.Find(source.Suite, source.ManifestCase);
            if (entry.Availability != RomAvailability.InstalledVerified || entry.RomPath is null)
                throw new FileNotFoundException($"Manifest ROM '{source.ManifestCase}' is not installed and checksum-valid.");
            return await File.ReadAllBytesAsync(entry.RomPath, token);
        }
        throw new InvalidDataException("A ROM path, manifest case, or immutable artifact is required.");
    }

    private async Task<NesExperimentResult> ReadResultAsync(string uri, CancellationToken token)
    {
        if (!ImmutableArtifactStore.TryParseUri(uri, out var kind, out var digest) || kind != "experiment")
            throw new ArgumentException("An immutable experiment URI is required.");
        var resource = await artifacts.ReadAsync(kind, digest, token);
        return JsonSerializer.Deserialize<NesExperimentResult>(resource.Text!, LabResponseSerializer.Options)
            ?? throw new InvalidDataException("Experiment artifact is invalid.");
    }

    private static bool Stopped(ExperimentStopCondition stop, NesSystem nes, ulong dots, ulong clocks, ulong frames) =>
        stop.Kind.ToLowerInvariant() switch
        {
            "ppudots" => dots >= stop.Value, "cpuclocks" => clocks >= stop.Value,
            "frames" => frames >= stop.Value, "pc" => nes.Debugger.ProgramCounter == (ushort)stop.Value,
            "terminalprotocol" => dots >= stop.Value && nes.Debugger.PeekCpuMemory(stop.Address ?? 0x6000) != 0x80,
            _ => throw new InvalidDataException($"Unknown stop condition '{stop.Kind}'.")
        };

    private static void Validate(NesExperimentScenario scenario, bool mcpSafe)
    {
        if (scenario.SchemaVersion != 1) throw new InvalidDataException("Only experiment schema v1 is supported.");
        if (!scenario.VideoStandard.Equals("Ntsc", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Only NTSC experiments are supported.");
        if (scenario.MaximumTraceRecords is < 1 or > 4096) throw new InvalidDataException("maximumTraceRecords must be 1..4096.");
        if (scenario.MaximumAudioSamples is < 0 or > 48000) throw new InvalidDataException("maximumAudioSamples must be 0..48000.");
        if (scenario.MaximumPpuDots is < 1 or > 100_000_000) throw new InvalidDataException("maximumPpuDots must be 1..100000000.");
        if (scenario.CapturePoints.Length > 32) throw new InvalidDataException("Experiment scenarios support at most 32 capture points.");
        if (scenario.ControllerChanges.Length > 1024) throw new InvalidDataException("Experiment scenarios support at most 1024 controller changes.");
        if (scenario.CapturePoints.Select(point => point.Id).Distinct(StringComparer.Ordinal).Count() != scenario.CapturePoints.Length)
            throw new InvalidDataException("Experiment capture IDs must be unique.");
        var maximumStop = scenario.Stop.Kind.ToLowerInvariant() switch
        {
            "ppudots" => 100_000_000UL, "cpuclocks" => 40_000_000UL, "frames" => 10_000UL,
            "pc" => ushort.MaxValue, "terminalprotocol" => 100_000_000UL,
            _ => throw new InvalidDataException($"Unknown stop condition '{scenario.Stop.Kind}'.")
        };
        if (scenario.Stop.Value > maximumStop) throw new InvalidDataException("Experiment stop condition exceeds its safety limit.");
        if (scenario.ControllerChanges.Any(change => change.Port is < 0 or > 1)) throw new InvalidDataException("Controller ports must be 0 or 1.");
        if (mcpSafe && scenario.Rom.Path is not null) throw new UnauthorizedAccessException("MCP experiments cannot use custom ROM paths.");
    }
    private static NesExperimentScenario Deserialize(ReadOnlySpan<byte> bytes) =>
        JsonSerializer.Deserialize<NesExperimentScenario>(bytes, LabResponseSerializer.Options)
        ?? throw new InvalidDataException("Experiment scenario is empty.");
    private static int Transitions(IReadOnlyList<NesCpuClockTrace> records, Func<NesCpuClockTrace, bool> selector) =>
        records.Zip(records.Skip(1)).Count(pair => selector(pair.First) != selector(pair.Second));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
