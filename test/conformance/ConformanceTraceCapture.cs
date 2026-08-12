using Sheep.Nes.Lab;

namespace Sheep.Emulation.Nes.ConformanceTests;

internal sealed class ConformanceTraceCapture(
    NesSystem nes, string path, string mode, int capacity, TraceArtifactMetadata metadata)
{
    private bool started;
    private readonly List<TraceCheckpointWindow> windows = [];
    private const int MaximumRecordsPerCheckpointWindow = 256;

    internal static ConformanceTraceCapture? FromEnvironment(
        NesSystem nes, string romName, string romSha256, string videoStandard, string suite, string caseName)
    {
        var path = Environment.GetEnvironmentVariable("NES_LAB_TRACE_PATH");
        var selectedCase = Environment.GetEnvironmentVariable("NES_LAB_TRACE_CASE");
        if (string.IsNullOrWhiteSpace(path) ||
            !string.Equals(selectedCase, caseName, StringComparison.OrdinalIgnoreCase)) return null;
        var configured = Environment.GetEnvironmentVariable("NES_LAB_TRACE_CAPACITY");
        var capacity = int.TryParse(configured, out var parsed) && parsed is > 0 and <= 16_384
            ? parsed : TraceArtifact.DefaultMaximumRecords;
        return new ConformanceTraceCapture(nes, path,
            Environment.GetEnvironmentVariable("NES_LAB_TRACE_MODE") ?? "failure", capacity,
            new TraceArtifactMetadata(romSha256,
                Environment.GetEnvironmentVariable("NES_LAB_SOURCE_COMMIT") ?? "unknown",
                DateTimeOffset.UtcNow, new TraceRunMetadata(romName, videoStandard, suite, caseName)));
    }

    internal void Start()
    {
        if (started) return;
        nes.Debugger.EnableCpuClockTracing(capacity);
        started = true;
    }

    internal void MarkBoundary() => MarkCheckpoint("terminal-state", "terminal", "harness");

    internal void MarkCheckpoint(string name, string kind, string triggerSource, int resetGeneration = 0,
        int maximumRecords = MaximumRecordsPerCheckpointWindow)
    {
        if (!started) return;
        var boundarySnapshot = nes.Debugger.GetCpuClockTraceSnapshot();
        windows.Add(TraceArtifactFactory.CreateWindow(
            name, kind, triggerSource, boundarySnapshot, resetGeneration,
            Math.Min(capacity, Math.Clamp(maximumRecords, 1, MaximumRecordsPerCheckpointWindow))));
    }

    internal void Complete(bool passed, int resetCount)
    {
        if (!started) return;
        var snapshot = nes.Debugger.GetCpuClockTraceSnapshot();
        nes.Debugger.DisableCpuClockTracing();
        if (passed && !mode.Equals("always", StringComparison.OrdinalIgnoreCase)) return;
        var artifact = TraceArtifactFactory.Create(snapshot, metadata, capacity) with
        {
            BoundaryKind = "terminal",
            BoundaryCpuClock = snapshot.Records.LastOrDefault()?.CpuClock,
            ResetCount = resetCount,
            Windows = windows.Append(TraceArtifactFactory.CreateWindow(
                "terminal-state", "terminal", "harness", snapshot, resetCount,
                Math.Min(capacity, MaximumRecordsPerCheckpointWindow))).ToArray()
        };
        new TraceArtifactWriter().WriteAsync(artifact, path).GetAwaiter().GetResult();
    }
}
