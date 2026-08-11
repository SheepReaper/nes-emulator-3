using System.Collections.Generic;

namespace Sheep.Emulation.Nes.Debugging;

public sealed class NesCpuClockTraceSnapshot
{
    public NesCpuClockTraceSnapshot(
        IReadOnlyList<NesCpuClockTrace> records, long totalRecordCount, long droppedRecordCount)
    {
        Records = records;
        TotalRecordCount = totalRecordCount;
        DroppedRecordCount = droppedRecordCount;
    }

    public IReadOnlyList<NesCpuClockTrace> Records { get; }
    public long TotalRecordCount { get; }
    public long DroppedRecordCount { get; }
}

public enum NesCpuClockActor { Cpu, OamDma, DmcDma }

public sealed class NesClockPhaseDebugState(
    string stateLabel, string cpuCycleKind, string apuBusPhase, int cpuPpuRelativePhase,
    bool irqSampledBeforeClock, bool nmiSampledBeforeClock, bool instructionPollBoundary, string dmaEvent,
    string preStateLabel = "preCpuClock", string postStateLabel = "postCpuClock",
    int schedulerAccumulator = 0, ulong masterClockPosition = 0)
{
    public string StateLabel { get; } = stateLabel;
    public string CpuCycleKind { get; } = cpuCycleKind;
    public string ApuBusPhase { get; } = apuBusPhase;
    public int CpuPpuRelativePhase { get; } = cpuPpuRelativePhase;
    public bool IrqSampledBeforeClock { get; } = irqSampledBeforeClock;
    public bool NmiSampledBeforeClock { get; } = nmiSampledBeforeClock;
    public bool InstructionPollBoundary { get; } = instructionPollBoundary;
    public string DmaEvent { get; } = dmaEvent;
    public string PreStateLabel { get; } = preStateLabel;
    public string PostStateLabel { get; } = postStateLabel;
    public int SchedulerAccumulator { get; } = schedulerAccumulator;
    public ulong MasterClockPosition { get; } = masterClockPosition;
}

public sealed class NesCpuBusAccess(NesDebugBreakKind kind, ushort address, byte value)
{
    public NesDebugBreakKind Kind { get; } = kind;
    public ushort Address { get; } = address;
    public byte Value { get; } = value;
}

public sealed class NesCpuBusDebugState(
    byte openBus, byte internalBus, ushort lastCpuReadAddress, bool hasCpuReadAddress,
    bool oamDmaPending, bool oamDmaActive, int oamDmaIndex, bool oamDmaReadPhase,
    bool dmcDmaPending, int dmcDmaCycles, ushort dmcDmaAddress)
{
    public byte OpenBus { get; } = openBus;
    public byte InternalBus { get; } = internalBus;
    public ushort LastCpuReadAddress { get; } = lastCpuReadAddress;
    public bool HasCpuReadAddress { get; } = hasCpuReadAddress;
    public bool OamDmaPending { get; } = oamDmaPending;
    public bool OamDmaActive { get; } = oamDmaActive;
    public int OamDmaIndex { get; } = oamDmaIndex;
    public bool OamDmaReadPhase { get; } = oamDmaReadPhase;
    public bool DmcDmaPending { get; } = dmcDmaPending;
    public int DmcDmaCycles { get; } = dmcDmaCycles;
    public ushort DmcDmaAddress { get; } = dmcDmaAddress;
}

public sealed class NesCpuClockTrace(
    ulong cpuClock, int scanline, int dot, CpuDebugState cpu, PpuDebugState ppu,
    NesCpuClockActor actor, ushort pendingBusAddress, bool nmiLine, bool irqLine,
    NesCpuBusDebugState cpuBus, IReadOnlyList<NesCpuBusAccess> busAccesses,
    NesClockPhaseDebugState? phase = null)
{
    public ulong CpuClock { get; } = cpuClock;
    public int Scanline { get; } = scanline;
    public int Dot { get; } = dot;
    public CpuDebugState Cpu { get; } = cpu;
    public PpuDebugState Ppu { get; } = ppu;
    public NesCpuClockActor Actor { get; } = actor;
    public ushort PendingBusAddress { get; } = pendingBusAddress;
    public bool NmiLine { get; } = nmiLine;
    public bool IrqLine { get; } = irqLine;
    public NesCpuBusDebugState CpuBus { get; } = cpuBus;
    public IReadOnlyList<NesCpuBusAccess> BusAccesses { get; } = busAccesses;
    public NesClockPhaseDebugState? Phase { get; } = phase;
}
