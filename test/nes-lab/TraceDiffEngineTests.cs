namespace Sheep.Nes.Lab.Tests;

public sealed class TraceDiffEngineTests
{
    [Fact]
    public void Diff_ReportsFirstSemanticDivergenceWithContext()
    {
        var expected = Artifact(Record(1), Record(2), Record(3, pc: 0x8000), Record(4));
        var actual = Artifact(Record(1), Record(2), Record(3, pc: 0x8001), Record(4, actor: "dmcDma"));

        var result = TraceDiffEngine.Diff(expected, actual, contextRecords: 1);

        Assert.False(result.Equal);
        Assert.Equal(2, result.DivergenceIndex);
        Assert.Contains("cpu.programCounter", result.Differences);
        Assert.Equal([1, 2, 3], result.Window.Select(item => item.Index));
    }

    [Fact]
    public void Diff_ShiftedTailsAlignByCpuClock()
    {
        var result = TraceDiffEngine.Diff(
            Artifact(Record(1), Record(2), Record(3)), Artifact(Record(2), Record(3)));

        Assert.True(result.Equal);
        Assert.Equal(TraceDiffStatus.Equal, result.Status);
        Assert.Equal((ulong)2, result.AlignedStartCpuClock);
        Assert.Equal((ulong)3, result.AlignedEndCpuClock);
    }

    [Fact]
    public void Diff_IncompatibleRomDoesNotReportSemanticDivergence()
    {
        var expected = Artifact(Record(1));
        var actual = expected with { RomSha256 = "other", SourceCommit = "other" };

        var result = TraceDiffEngine.Diff(expected, actual);

        Assert.False(result.Equal);
        Assert.Equal(TraceDiffStatus.Incompatible, result.Status);
        Assert.Null(result.DivergenceIndex);
        Assert.Empty(result.Window);
    }

    [Fact]
    public void Diff_DifferentSourceCommitsDoNotReportSemanticDivergence()
    {
        var expected = Artifact(Record(1));
        var actual = expected with { SourceCommit = "other" };

        var result = TraceDiffEngine.Diff(expected, actual);

        Assert.Equal(TraceDiffStatus.Incompatible, result.Status);
        Assert.Contains("sourceCommit", result.Differences);
        Assert.Null(result.DivergenceIndex);
    }

    [Fact]
    public void Diff_NonOverlappingCaptureWindowsReturnMismatch()
    {
        var result = TraceDiffEngine.Diff(
            Artifact(Record(1), Record(2)), Artifact(Record(5), Record(6)));

        Assert.Equal(TraceDiffStatus.CaptureWindowMismatch, result.Status);
        Assert.Null(result.DivergenceIndex);
    }

    private static TraceArtifact Artifact(params TraceClockRecord[] records) => new(
        TraceArtifact.CurrentSchemaVersion, "nes-cpu-clock-trace", "hash", "commit", DateTimeOffset.UnixEpoch,
        new TraceRunMetadata("rom", "NTSC", null, null), records.Length, 0, false, records);

    private static TraceClockRecord Record(ulong clock, ushort pc = 0, string actor = "cpu") => new(
        clock, 0, 0, new TraceCpuState(0, 0, 0, 0, pc, 0, 0, 0, clock, false),
        new TracePpuState(0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, false, 0),
        actor, 0, false, false,
        new TraceCpuBusState(0, 0, 0, false, false, false, 0, false, false, 0, 0), []);
}
