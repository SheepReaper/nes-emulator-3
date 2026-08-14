namespace Sheep.Nes.Lab.Tests;

public sealed class TraceQueryEngineTests
{
    [Fact]
    public void Query_AddressRangeUsesRecordedBusAccesses()
    {
        var artifact = Artifact(
            Record(1, accesses: [new("cpuRead", 0x2006, 1)]),
            Record(2, accesses: [new("cpuWrite", 0x4015, 2)]),
            Record(3, pendingAddress: 0x4015));

        var result = TraceQueryEngine.Query(artifact, new TraceQuery(
            AddressStart: 0x4000, AddressEnd: 0x4017));

        Assert.Equal([2UL], result.Records.Select(record => record.CpuClock));
        Assert.Equal(1, result.MatchCount);
    }

    [Fact]
    public void Query_CombinesActorAndInstructionBoundaryFilters()
    {
        var artifact = Artifact(
            Record(1, actor: "cpu", instructionBoundary: true),
            Record(2, actor: "dmcDma", instructionBoundary: true),
            Record(3, actor: "cpu", instructionBoundary: false));

        var result = TraceQueryEngine.Query(artifact, new TraceQuery(
            Actor: "cpu", InstructionBoundariesOnly: true));

        Assert.Equal([1UL], result.Records.Select(record => record.CpuClock));
    }

    [Fact]
    public void Query_InterruptEdgesComparesWithPrecedingClock()
    {
        var artifact = Artifact(
            Record(1),
            Record(2, nmi: true),
            Record(3, nmi: true, irq: true),
            Record(4, nmi: true, irq: true));

        var result = TraceQueryEngine.Query(artifact,
            new TraceQuery(InterruptEdgesOnly: true));

        Assert.Equal([2UL, 3UL], result.Records.Select(record => record.CpuClock));
    }

    [Fact]
    public void Query_DmaOverlapRequiresOamAndDmcActivity()
    {
        var artifact = Artifact(
            Record(1, actor: "oamDma", oamActive: true),
            Record(2, actor: "dmcDma", oamActive: true),
            Record(3, actor: "oamDma", oamActive: true, dmcPending: true));

        var result = TraceQueryEngine.Query(artifact,
            new TraceQuery(DmaOverlapOnly: true));

        Assert.Equal([2UL, 3UL], result.Records.Select(record => record.CpuClock));
    }

    [Fact]
    public void Query_ReturnsNewestBoundedMatchesAndReportsTruncation()
    {
        var artifact = Artifact(Enumerable.Range(1, 5).Select(index => Record((ulong)index)).ToArray());

        var result = TraceQueryEngine.Query(artifact, new TraceQuery(MaximumRecords: 2));

        Assert.Equal(5, result.MatchCount);
        Assert.True(result.Truncated);
        Assert.Equal([4UL, 5UL], result.Records.Select(record => record.CpuClock));
    }

    [Fact]
    public void Query_RejectsReversedRange()
    {
        Assert.Throws<ArgumentException>(() => TraceQueryEngine.Query(
            Artifact(), new TraceQuery(AddressStart: 0x2001, AddressEnd: 0x2000)));
    }

    [Fact]
    public void Query_SearchesCheckpointWindowsAndDeduplicatesOverlappingRecords()
    {
        var dmc = Record(20, actor: "dmcDma");
        var status = Record(21, accesses: [new("cpuRead", 0x4015, 0x10)]);
        var artifact = Artifact(Record(30)) with
        {
            Windows =
            [
                Window("dmc", dmc, status),
                Window("overlap", status, Record(22))
            ]
        };

        var actorResult = TraceQueryEngine.Query(artifact, new TraceQuery(Actor: "DmcDma"));
        var addressResult = TraceQueryEngine.Query(artifact, new TraceQuery(AddressStart: 0x4015));

        Assert.Equal([20UL], actorResult.Records.Select(record => record.CpuClock));
        Assert.Equal([21UL], addressResult.Records.Select(record => record.CpuClock));
        Assert.Equal(1, addressResult.MatchCount);
    }

    [Fact]
    public void Query_DoesNotInferInterruptEdgeAcrossCheckpointWindowGap()
    {
        var artifact = Artifact() with
        {
            Windows = [Window("before", Record(1)), Window("after", Record(10, nmi: true))]
        };

        var result = TraceQueryEngine.Query(artifact, new TraceQuery(InterruptEdgesOnly: true));

        Assert.Empty(result.Records);
    }

    private static TraceArtifact Artifact(params TraceClockRecord[] records) => new(
        TraceArtifact.CurrentSchemaVersion, "nes-cpu-clock-trace", "hash", "commit",
        DateTimeOffset.UnixEpoch, new TraceRunMetadata("rom", "NTSC", null, null),
        records.Length, 0, false, records);

    private static TraceCheckpointWindow Window(string name, params TraceClockRecord[] records) => new(
        name, "hardware", "test", records.LastOrDefault()?.CpuClock,
        records.FirstOrDefault()?.CpuClock, records.LastOrDefault()?.CpuClock,
        records.Length, 0, 0, records);

    private static TraceClockRecord Record(
        ulong clock,
        string actor = "cpu",
        bool instructionBoundary = false,
        bool nmi = false,
        bool irq = false,
        bool oamActive = false,
        bool dmcPending = false,
        ushort pendingAddress = 0,
        IReadOnlyList<TraceBusAccess>? accesses = null) => new(
        clock, 0, 0,
        new TraceCpuState(0, 0, 0, 0, 0, 0, 0, 0, clock, instructionBoundary),
        new TracePpuState(0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, false, 0),
        actor, pendingAddress, nmi, irq,
        new TraceCpuBusState(0, 0, 0, false, false, oamActive, 0, false,
            dmcPending, 0, 0),
        accesses ?? []);
}
