namespace Sheep.Nes.Lab;

public sealed record TraceRunMetadata(
    string RomName,
    string VideoStandard,
    string? Suite,
    string? Case);

public sealed record TraceArtifactMetadata(
    string RomSha256,
    string SourceCommit,
    DateTimeOffset CapturedAtUtc,
    TraceRunMetadata Run);

public sealed record TraceArtifact(
    int SchemaVersion,
    string ArtifactKind,
    string RomSha256,
    string SourceCommit,
    DateTimeOffset CapturedAtUtc,
    TraceRunMetadata Run,
    long OriginalRecordCount,
    long DroppedRecordCount,
    bool Truncated,
    IReadOnlyList<TraceClockRecord> Records,
    string? CaptureId = null,
    string? BoundaryKind = null,
    ulong? BoundaryCpuClock = null,
    ulong? FirstCpuClock = null,
    ulong? LastCpuClock = null,
    int ResetCount = 0,
    IReadOnlyList<TraceCheckpointWindow>? Windows = null)
{
    public const int CurrentSchemaVersion = 4;
    public const int DefaultMaximumRecords = 2_048;
}

public sealed record TraceCheckpointWindow(
    string Name,
    string Kind,
    string TriggerSource,
    ulong? CpuClock,
    ulong? FirstCpuClock,
    ulong? LastCpuClock,
    long OriginalRecordCount,
    long DroppedRecordCount,
    int ResetGeneration,
    IReadOnlyList<TraceClockRecord> Records);

public sealed record TraceClockRecord(
    ulong CpuClock,
    int Scanline,
    int Dot,
    TraceCpuState Cpu,
    TracePpuState Ppu,
    string Actor,
    ushort PendingBusAddress,
    bool NmiLine,
    bool IrqLine,
    TraceCpuBusState CpuBus,
    IReadOnlyList<TraceBusAccess> BusAccesses,
    TracePhaseState? Phase = null);

public sealed record TracePhaseState(string StateLabel, string CpuCycleKind, string ApuBusPhase,
    int CpuPpuRelativePhase, bool IrqSampledBeforeClock, bool NmiSampledBeforeClock,
    bool InstructionPollBoundary, string DmaEvent, string? PreStateLabel = null,
    string? PostStateLabel = null, int? SchedulerAccumulator = null,
    ulong? MasterClockPosition = null);

public sealed record TraceCpuState(
    byte Accumulator,
    byte X,
    byte Y,
    byte StackPointer,
    ushort ProgramCounter,
    byte Status,
    byte Opcode,
    int CyclesRemaining,
    ulong TotalCycles,
    bool IsInstructionBoundary);

public sealed record TracePpuState(
    byte Control,
    byte Mask,
    byte Status,
    byte OamAddress,
    ushort VramAddress,
    ushort TemporaryVramAddress,
    byte FineX,
    bool WriteToggle,
    byte DataBuffer,
    int Scanline,
    int Dot,
    ulong FrameNumber,
    bool IsOddFrame,
    int EvaluatedSpriteCount);

public sealed record TraceCpuBusState(
    byte OpenBus,
    byte InternalBus,
    ushort LastCpuReadAddress,
    bool HasCpuReadAddress,
    bool OamDmaPending,
    bool OamDmaActive,
    int OamDmaIndex,
    bool OamDmaReadPhase,
    bool DmcDmaPending,
    int DmcDmaCycles,
    ushort DmcDmaAddress);

public sealed record TraceBusAccess(string Kind, ushort Address, byte Value);
