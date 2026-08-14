using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Nes.Lab.Tests;

public sealed class TraceArtifactFactoryTests
{
    [Fact]
    public void Create_MapsTraceAndProvenanceIntoStableContract()
    {
        var trace = CreateTrace(42, NesCpuClockActor.DmcDma,
            [new NesCpuBusAccess(NesDebugBreakKind.CpuRead, 0x4015, 0x80)]);
        var metadata = new TraceArtifactMetadata(
            "ABCD", "deadbeef", new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero),
            new TraceRunMetadata("test.nes", "NTSC", "suite", "case"));

        var artifact = TraceArtifactFactory.Create([trace], metadata);

        Assert.Equal(TraceArtifact.CurrentSchemaVersion, artifact.SchemaVersion);
        Assert.Equal("nes-cpu-clock-trace", artifact.ArtifactKind);
        Assert.Equal("ABCD", artifact.RomSha256);
        Assert.Equal("deadbeef", artifact.SourceCommit);
        Assert.Equal(1, artifact.OriginalRecordCount);
        Assert.False(artifact.Truncated);
        var record = Assert.Single(artifact.Records);
        Assert.Equal((ulong)42, record.CpuClock);
        Assert.Equal("dmcDma", record.Actor);
        Assert.Equal(0xC000, record.Cpu.ProgramCounter);
        Assert.Equal(0x2007, record.PendingBusAddress);
        var access = Assert.Single(record.BusAccesses);
        Assert.Equal("cpuRead", access.Kind);
        Assert.Equal(0x4015, access.Address);
        Assert.Equal(0x80, access.Value);
    }

    [Fact]
    public void Create_V4PreservesExplicitPrePostAndSchedulerPhase()
    {
        var original = CreateTrace(9, NesCpuClockActor.Cpu, []);
        var traced = new NesCpuClockTrace(original.CpuClock, original.Scanline, original.Dot,
            original.Cpu, original.Ppu, original.Actor, original.PendingBusAddress,
            original.NmiLine, original.IrqLine, original.CpuBus, original.BusAccesses,
            new("postCpuClock", "read", "get", 1, true, false, true, "haltRetry",
                "preCpuClock", "postCpuClock", 2, 4126));

        var phase = Assert.Single(TraceArtifactFactory.Create([traced], Metadata()).Records).Phase!;

        Assert.Equal("preCpuClock", phase.PreStateLabel);
        Assert.Equal("postCpuClock", phase.PostStateLabel);
        Assert.Equal(2, phase.SchedulerAccumulator);
        Assert.Equal((ulong)4126, phase.MasterClockPosition);
    }

    [Fact]
    public void Create_KeepsNewestRecordsWithinBound()
    {
        var traces = Enumerable.Range(1, 5)
            .Select(index => CreateTrace((ulong)index, NesCpuClockActor.Cpu, []))
            .ToArray();

        var artifact = TraceArtifactFactory.Create(traces, Metadata(), maximumRecords: 3);

        Assert.True(artifact.Truncated);
        Assert.Equal(5, artifact.OriginalRecordCount);
        Assert.Equal(2, artifact.DroppedRecordCount);
        Assert.Equal([3UL, 4UL, 5UL], artifact.Records.Select(record => record.CpuClock));
    }

    [Fact]
    public void Create_FromDebuggerSnapshotPreservesDroppedRecordCount()
    {
        var snapshot = new NesCpuClockTraceSnapshot(
            [CreateTrace(4, NesCpuClockActor.Cpu, []), CreateTrace(5, NesCpuClockActor.Cpu, [])],
            5, 3);

        var artifact = TraceArtifactFactory.Create(snapshot, Metadata());

        Assert.Equal(5, artifact.OriginalRecordCount);
        Assert.Equal(3, artifact.DroppedRecordCount);
        Assert.True(artifact.Truncated);
    }

    [Fact]
    public void Create_RejectsInvalidBound()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TraceArtifactFactory.Create([], Metadata(), maximumRecords: 0));
    }

    [Fact]
    public void CreateWindow_RecordsCheckpointProvenanceAndBoundedClocks()
    {
        var snapshot = new NesCpuClockTraceSnapshot(
            Enumerable.Range(1, 4).Select(index =>
                CreateTrace((ulong)index, NesCpuClockActor.Cpu, [])).ToArray(), 4, 0);

        var window = TraceArtifactFactory.CreateWindow(
            "first-dmc", "hardware", "APU request", snapshot, 2, maximumRecords: 2);

        Assert.Equal("first-dmc", window.Name);
        Assert.Equal("hardware", window.Kind);
        Assert.Equal("APU request", window.TriggerSource);
        Assert.Equal(2, window.ResetGeneration);
        Assert.Equal([3UL, 4UL], window.Records.Select(record => record.CpuClock));
        Assert.Equal(2, window.DroppedRecordCount);
    }

    private static TraceArtifactMetadata Metadata() => new(
        "hash", "commit", DateTimeOffset.UnixEpoch,
        new TraceRunMetadata("test.nes", "NTSC", null, null));

    private static NesCpuClockTrace CreateTrace(
        ulong clock,
        NesCpuClockActor actor,
        IReadOnlyList<NesCpuBusAccess> accesses)
    {
        var cpu = new CpuDebugState(1, 2, 3, 0xFD, 0xC000, 0x24, 0xEA, 1, clock, false);
        var ppu = new PpuDebugState(0x80, 0x1E, 0x20, 4, 0x2100, 0x2200, 3, true,
            0xAA, 12, 34, 5, true, 6);
        var bus = new NesCpuBusDebugState(0x11, 0x22, 0x1234, true,
            true, false, 7, true, true, 2, 0xC123);
        return new NesCpuClockTrace(clock, 12, 34, cpu, ppu, actor, 0x2007,
            true, false, bus, accesses);
    }
}
