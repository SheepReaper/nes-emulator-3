using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Nes.Lab;

public static class TraceArtifactFactory
{
    public static TraceArtifact Create(IReadOnlyList<NesCpuClockTrace> trace,
        TraceArtifactMetadata metadata, int maximumRecords = TraceArtifact.DefaultMaximumRecords)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecords);
        var dropped = Math.Max(0, trace.Count - maximumRecords);
        var records = trace.Skip(dropped).Select(Map).ToArray();
        return new TraceArtifact(TraceArtifact.CurrentSchemaVersion, "nes-cpu-clock-trace",
            metadata.RomSha256, metadata.SourceCommit, metadata.CapturedAtUtc, metadata.Run,
            trace.Count, dropped, dropped != 0, records, Guid.NewGuid().ToString("N"), "manual",
            records.LastOrDefault()?.CpuClock, records.FirstOrDefault()?.CpuClock,
            records.LastOrDefault()?.CpuClock);
    }

    public static TraceCheckpointWindow CreateWindow(string name, string kind, string triggerSource,
        NesCpuClockTraceSnapshot snapshot, int resetGeneration = 0,
        int maximumRecords = TraceArtifact.DefaultMaximumRecords)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerSource);
        var records = snapshot.Records.TakeLast(maximumRecords).Select(Map).ToArray();
        return new TraceCheckpointWindow(name, kind, triggerSource, records.LastOrDefault()?.CpuClock,
            records.FirstOrDefault()?.CpuClock, records.LastOrDefault()?.CpuClock,
            snapshot.TotalRecordCount,
            snapshot.DroppedRecordCount + Math.Max(0, snapshot.Records.Count - maximumRecords),
            resetGeneration, records);
    }

    public static TraceArtifact Create(NesCpuClockTraceSnapshot snapshot,
        TraceArtifactMetadata metadata, int maximumRecords = TraceArtifact.DefaultMaximumRecords)
    {
        var artifact = Create(snapshot.Records, metadata, maximumRecords);
        return artifact with { OriginalRecordCount = snapshot.TotalRecordCount,
            DroppedRecordCount = snapshot.DroppedRecordCount,
            Truncated = snapshot.DroppedRecordCount > 0 };
    }

    private static TraceClockRecord Map(NesCpuClockTrace record) => new(record.CpuClock,
        record.Scanline, record.Dot, Map(record.Cpu), Map(record.Ppu), ToCamelCase(record.Actor),
        record.PendingBusAddress, record.NmiLine, record.IrqLine, Map(record.CpuBus),
        record.BusAccesses.Select(access => new TraceBusAccess(
            ToCamelCase(access.Kind), access.Address, access.Value)).ToArray(),
        record.Phase is null ? null : new TracePhaseState(record.Phase.StateLabel,
            record.Phase.CpuCycleKind, record.Phase.ApuBusPhase, record.Phase.CpuPpuRelativePhase,
            record.Phase.IrqSampledBeforeClock, record.Phase.NmiSampledBeforeClock,
            record.Phase.InstructionPollBoundary, record.Phase.DmaEvent,
            record.Phase.PreStateLabel, record.Phase.PostStateLabel,
            record.Phase.SchedulerAccumulator, record.Phase.MasterClockPosition));

    private static TraceCpuState Map(CpuDebugState state) => new(state.Accumulator, state.X,
        state.Y, state.StackPointer, state.ProgramCounter, state.Status, state.Opcode,
        state.CyclesRemaining, state.TotalCycles, state.IsInstructionBoundary);
    private static TracePpuState Map(PpuDebugState state) => new(state.Control, state.Mask,
        state.Status, state.OamAddress, state.VramAddress, state.TemporaryVramAddress,
        state.FineX, state.WriteToggle, state.DataBuffer, state.Scanline, state.Dot,
        state.FrameNumber, state.IsOddFrame, state.EvaluatedSpriteCount);
    private static TraceCpuBusState Map(NesCpuBusDebugState state) => new(state.OpenBus,
        state.InternalBus, state.LastCpuReadAddress, state.HasCpuReadAddress, state.OamDmaPending,
        state.OamDmaActive, state.OamDmaIndex, state.OamDmaReadPhase, state.DmcDmaPending,
        state.DmcDmaCycles, state.DmcDmaAddress);
    private static string ToCamelCase<T>(T value) where T : struct, Enum
    {
        var text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
