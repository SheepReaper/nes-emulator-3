namespace Sheep.Nes.Lab;

public sealed record TraceDiffWindowRecord(
    int Index,
    TraceClockRecord? Expected,
    TraceClockRecord? Actual);

public enum TraceDiffStatus { Equal, Diverged, Incompatible, CaptureWindowMismatch }

public sealed record TraceDiffResult(
    int SchemaVersion,
    bool Equal,
    int? DivergenceIndex,
    IReadOnlyList<string> Differences,
    IReadOnlyList<TraceDiffWindowRecord> Window,
    TraceDiffStatus Status = TraceDiffStatus.Diverged,
    ulong? DivergenceCpuClock = null,
    ulong? AlignedStartCpuClock = null,
    ulong? AlignedEndCpuClock = null,
    IReadOnlyList<string>? ProvenanceDifferences = null)
{
    public const int CurrentSchemaVersion = 2;
}

public static class TraceDiffEngine
{
    public static TraceDiffResult Diff(
        TraceArtifact expected,
        TraceArtifact actual,
        int contextRecords = 3)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        if (contextRecords < 0 || contextRecords > 31)
            throw new ArgumentOutOfRangeException(nameof(contextRecords));
        if (expected.SchemaVersion != TraceArtifact.CurrentSchemaVersion ||
            actual.SchemaVersion != TraceArtifact.CurrentSchemaVersion)
            return new TraceDiffResult(TraceDiffResult.CurrentSchemaVersion, false, null,
                ["schemaVersion"], [], TraceDiffStatus.Incompatible);

        var incompatibilities = Incompatibilities(expected, actual).ToList();
        var provenance = ProvenanceDifferences(expected, actual);
        foreach (var difference in provenance.Where(item => item is "sourceCommit" or "boundaryKind"))
            incompatibilities.Add(difference);
        if (incompatibilities.Count > 0)
            return new TraceDiffResult(TraceDiffResult.CurrentSchemaVersion, false, null, incompatibilities, [],
                TraceDiffStatus.Incompatible, ProvenanceDifferences: provenance);

        var expectedByClock = expected.Records.Select((record, index) => (record, index))
            .ToDictionary(item => item.record.CpuClock);
        var actualByClock = actual.Records.Select((record, index) => (record, index))
            .ToDictionary(item => item.record.CpuClock);
        var alignedClocks = expectedByClock.Keys.Intersect(actualByClock.Keys).Order().ToArray();
        if (alignedClocks.Length == 0)
            return new TraceDiffResult(TraceDiffResult.CurrentSchemaVersion, false, null, ["captureWindow"], [],
                TraceDiffStatus.CaptureWindowMismatch, ProvenanceDifferences: provenance);

        for (var alignedIndex = 0; alignedIndex < alignedClocks.Length; alignedIndex++)
        {
            var clock = alignedClocks[alignedIndex];
            var expectedItem = expectedByClock[clock];
            var actualItem = actualByClock[clock];
            var differences = GetDifferences(expectedItem.record, actualItem.record);
            if (differences.Count != 0)
                return Diverged(expectedByClock, actualByClock, alignedClocks, alignedIndex,
                    expectedItem.index, differences, contextRecords, provenance);
        }

        return new TraceDiffResult(
            TraceDiffResult.CurrentSchemaVersion, true, null, [], [], TraceDiffStatus.Equal,
            AlignedStartCpuClock: alignedClocks[0], AlignedEndCpuClock: alignedClocks[^1],
            ProvenanceDifferences: provenance);
    }

    private static TraceDiffResult Diverged(
        IReadOnlyDictionary<ulong, (TraceClockRecord record, int index)> expected,
        IReadOnlyDictionary<ulong, (TraceClockRecord record, int index)> actual,
        IReadOnlyList<ulong> clocks,
        int alignedIndex,
        int expectedIndex,
        IReadOnlyList<string> differences,
        int contextRecords,
        IReadOnlyList<string> provenance)
    {
        var start = Math.Max(0, alignedIndex - contextRecords);
        var end = Math.Min(clocks.Count - 1, alignedIndex + contextRecords);
        var window = Enumerable.Range(start, end - start + 1)
            .Select(item => clocks[item])
            .Select(clock => new TraceDiffWindowRecord(
                expected[clock].index, expected[clock].record, actual[clock].record))
            .ToArray();
        return new TraceDiffResult(
            TraceDiffResult.CurrentSchemaVersion, false, expectedIndex, differences, window,
            TraceDiffStatus.Diverged, clocks[alignedIndex], clocks[0], clocks[^1], provenance);
    }

    private static IReadOnlyList<string> Incompatibilities(TraceArtifact a, TraceArtifact b)
    {
        List<string> result = [];
        if (!a.RomSha256.Equals(b.RomSha256, StringComparison.OrdinalIgnoreCase)) result.Add("romSha256");
        if (!string.Equals(a.Run.Suite, b.Run.Suite, StringComparison.OrdinalIgnoreCase)) result.Add("suite");
        if (!string.Equals(a.Run.Case, b.Run.Case, StringComparison.OrdinalIgnoreCase)) result.Add("case");
        if (!a.Run.VideoStandard.Equals(b.Run.VideoStandard, StringComparison.OrdinalIgnoreCase)) result.Add("videoStandard");
        return result;
    }

    private static IReadOnlyList<string> ProvenanceDifferences(TraceArtifact a, TraceArtifact b)
    {
        List<string> result = [];
        if (!a.SourceCommit.Equals(b.SourceCommit, StringComparison.OrdinalIgnoreCase)) result.Add("sourceCommit");
        if (a.DroppedRecordCount != b.DroppedRecordCount) result.Add("droppedRecordCount");
        if (!string.Equals(a.BoundaryKind, b.BoundaryKind, StringComparison.OrdinalIgnoreCase)) result.Add("boundaryKind");
        return result;
    }

    private static IReadOnlyList<string> GetDifferences(
        TraceClockRecord expected,
        TraceClockRecord actual)
    {
        List<string> differences = [];
        Add(expected.CpuClock != actual.CpuClock, "cpuClock");
        Add(expected.Scanline != actual.Scanline, "scanline");
        Add(expected.Dot != actual.Dot, "dot");
        Add(expected.Actor != actual.Actor, "actor");
        Add(expected.PendingBusAddress != actual.PendingBusAddress, "pendingBusAddress");
        Add(expected.NmiLine != actual.NmiLine, "nmiLine");
        Add(expected.IrqLine != actual.IrqLine, "irqLine");
        CompareCpu(expected.Cpu, actual.Cpu, Add);
        ComparePpu(expected.Ppu, actual.Ppu, Add);
        CompareBus(expected.CpuBus, actual.CpuBus, Add);
        Add(!expected.BusAccesses.SequenceEqual(actual.BusAccesses), "busAccesses");
        return differences;

        void Add(bool differs, string name)
        {
            if (differs) differences.Add(name);
        }
    }

    private static void CompareCpu(TraceCpuState a, TraceCpuState b, Action<bool, string> add)
    {
        add(a.Accumulator != b.Accumulator, "cpu.accumulator");
        add(a.X != b.X, "cpu.x"); add(a.Y != b.Y, "cpu.y");
        add(a.StackPointer != b.StackPointer, "cpu.stackPointer");
        add(a.ProgramCounter != b.ProgramCounter, "cpu.programCounter");
        add(a.Status != b.Status, "cpu.status"); add(a.Opcode != b.Opcode, "cpu.opcode");
        add(a.CyclesRemaining != b.CyclesRemaining, "cpu.cyclesRemaining");
        add(a.TotalCycles != b.TotalCycles, "cpu.totalCycles");
        add(a.IsInstructionBoundary != b.IsInstructionBoundary, "cpu.isInstructionBoundary");
    }

    private static void ComparePpu(TracePpuState a, TracePpuState b, Action<bool, string> add)
    {
        add(a.Control != b.Control, "ppu.control"); add(a.Mask != b.Mask, "ppu.mask");
        add(a.Status != b.Status, "ppu.status"); add(a.OamAddress != b.OamAddress, "ppu.oamAddress");
        add(a.VramAddress != b.VramAddress, "ppu.vramAddress");
        add(a.TemporaryVramAddress != b.TemporaryVramAddress, "ppu.temporaryVramAddress");
        add(a.FineX != b.FineX, "ppu.fineX"); add(a.WriteToggle != b.WriteToggle, "ppu.writeToggle");
        add(a.DataBuffer != b.DataBuffer, "ppu.dataBuffer");
        add(a.Scanline != b.Scanline, "ppu.scanline"); add(a.Dot != b.Dot, "ppu.dot");
        add(a.FrameNumber != b.FrameNumber, "ppu.frameNumber");
        add(a.IsOddFrame != b.IsOddFrame, "ppu.isOddFrame");
        add(a.EvaluatedSpriteCount != b.EvaluatedSpriteCount, "ppu.evaluatedSpriteCount");
    }

    private static void CompareBus(TraceCpuBusState a, TraceCpuBusState b, Action<bool, string> add)
    {
        add(a.OpenBus != b.OpenBus, "cpuBus.openBus"); add(a.InternalBus != b.InternalBus, "cpuBus.internalBus");
        add(a.LastCpuReadAddress != b.LastCpuReadAddress, "cpuBus.lastCpuReadAddress");
        add(a.HasCpuReadAddress != b.HasCpuReadAddress, "cpuBus.hasCpuReadAddress");
        add(a.OamDmaPending != b.OamDmaPending, "cpuBus.oamDmaPending");
        add(a.OamDmaActive != b.OamDmaActive, "cpuBus.oamDmaActive");
        add(a.OamDmaIndex != b.OamDmaIndex, "cpuBus.oamDmaIndex");
        add(a.OamDmaReadPhase != b.OamDmaReadPhase, "cpuBus.oamDmaReadPhase");
        add(a.DmcDmaPending != b.DmcDmaPending, "cpuBus.dmcDmaPending");
        add(a.DmcDmaCycles != b.DmcDmaCycles, "cpuBus.dmcDmaCycles");
        add(a.DmcDmaAddress != b.DmcDmaAddress, "cpuBus.dmcDmaAddress");
    }
}
