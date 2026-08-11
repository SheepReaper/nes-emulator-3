using System;
using System.Collections.Generic;
using System.Linq;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Bounded circular buffer recording CPU clock cycle traces.
/// </summary>
internal sealed class NesCpuClockTracer
{
    private Queue<NesCpuClockTrace>? _trace;
    private int _capacity;
    private List<NesCpuBusAccess>? _currentAccesses;
    private long _totalRecordCount;
    private bool _preIrq;
    private bool _preNmi;
    private bool _preBoundary;
    private NesCpuBusDebugState? _preBus;

    internal bool IsTracing => _trace != null;

    internal void Enable(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        _capacity = capacity;
        _trace = new Queue<NesCpuClockTrace>(capacity);
        _currentAccesses = null;
        _totalRecordCount = 0;
    }

    internal void Disable()
    {
        _trace = null;
        _capacity = 0;
        _currentAccesses = null;
    }

    internal IReadOnlyList<NesCpuClockTrace> GetTrace() => _trace?.ToArray() ?? [];
    internal NesCpuClockTraceSnapshot GetSnapshot()
    {
        var records = GetTrace();
        return new NesCpuClockTraceSnapshot(records, _totalRecordCount,
            Math.Max(0, _totalRecordCount - records.Count));
    }

    internal void BeginClock(NesSystem nes)
    {
        if (_trace != null)
        {
            _currentAccesses = [];
            _preIrq = nes.Interrupts.Irq;
            _preNmi = nes.Interrupts.Nmi;
            _preBoundary = nes.Cpu.CaptureDebugState().IsInstructionBoundary;
            _preBus = nes.CpuBus.CaptureDebugState();
        }
    }

    internal void RecordAccess(NesDebugBreakKind kind, ushort address, byte value)
    {
        _currentAccesses?.Add(new NesCpuBusAccess(kind, address, value));
    }

    internal void CompleteClock(NesSystem nes, NesCpuClockActor actor)
    {
        if (_trace == null || _currentAccesses == null)
        {
            return;
        }

        if (_trace.Count == _capacity)
        {
            _trace.Dequeue();
        }

        var ppu = nes.Ppu.CaptureDebugState();
        _trace.Enqueue(new NesCpuClockTrace(
            nes.CpuClockCounter, ppu.Scanline, ppu.Dot,
            nes.Cpu.CaptureDebugState(), ppu, actor, nes.Cpu.PendingBusAddress,
            nes.Interrupts.Nmi, nes.Interrupts.Irq, nes.CpuBus.CaptureDebugState(),
            _currentAccesses.AsReadOnly(), new NesClockPhaseDebugState(
                "postCpuClock", CycleKind(_currentAccesses), (nes.CpuClockCounter & 1) == 0 ? "get" : "put",
                ppu.Dot % 3, _preIrq, _preNmi, _preBoundary,
                DmaEvent(actor, _preBus, nes.CpuBus.CaptureDebugState(), _currentAccesses),
                schedulerAccumulator: nes.SchedulerClockAccumulator,
                masterClockPosition: (ulong)Math.Max(0, ppu.Scanline) * (ulong)nes.Timing.DotsPerScanline + (ulong)ppu.Dot)));
        _totalRecordCount++;
        _currentAccesses = null;
    }

    private static string CycleKind(IReadOnlyList<NesCpuBusAccess> accesses) => accesses.LastOrDefault()?.Kind switch
    { NesDebugBreakKind.CpuWrite => "write", NesDebugBreakKind.CpuRead => "read", _ => "idle" };

    private static string DmaEvent(NesCpuClockActor actor, NesCpuBusDebugState? before,
        NesCpuBusDebugState after, IReadOnlyList<NesCpuBusAccess> accesses)
    {
        if (before is null) return "none";
        if (!before.OamDmaPending && !before.OamDmaActive && after.OamDmaPending) return "oamRequest";
        if (before.OamDmaPending && after.OamDmaActive) return "oamHaltAccepted";
        if (before.OamDmaActive && !after.OamDmaActive) return "oamCompletion";
        if (actor == NesCpuClockActor.DmcDma)
        {
            if (!before.DmcDmaPending) return "dmcRequestAndHaltAccepted";
            if (before.DmcDmaCycles == 0 && after.DmcDmaPending) return "dmcHaltAccepted";
            if (!after.DmcDmaPending) return "dmcFetchCompletion";
            return before.DmcDmaCycles > after.DmcDmaCycles && (before.DmcDmaCycles & 1) == 0
                ? "dmcAlignment" : "dmcDummy";
        }
        if (actor == NesCpuClockActor.OamDma)
            return before.OamDmaReadPhase == after.OamDmaReadPhase ? "oamHaltOrAlignment" :
                after.OamDmaReadPhase ? "oamWrite" : "oamFetch";
        if (before.DmcDmaPending && after.DmcDmaPending &&
            accesses.Any(item => item.Kind == NesDebugBreakKind.CpuWrite)) return "haltRetry";
        if (before.DmcDmaPending && !after.DmcDmaPending) return "haltAccepted";
        if (!before.DmcDmaPending && after.DmcDmaPending) return "request";
        return "none";
    }
}
