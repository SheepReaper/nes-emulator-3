using System;
using System.Collections.Generic;

namespace Sheep.Emulation.Nes.Debugging;

public sealed class NesDebugger : INesDebugger
{
    private readonly NesSystem _nes;
    private readonly NesBreakpointManager _bp = new();
    private readonly NesCpuClockTracer _tracer = new();

    internal NesDebugger(NesSystem nes) => _nes = nes;

    internal bool HasEnabledBreakpointsLocked => _bp.HasEnabledBreakpoints;
    public bool IsPaused { get { lock (_nes.SyncRoot) return _nes.IsPausedLocked; } }
    public NesExecutionState ExecutionState => IsPaused ? NesExecutionState.Paused : NesExecutionState.Running;
    public ushort ProgramCounter { get { lock (_nes.SyncRoot) return _nes.Cpu.ProgramCounter; } }

    public event EventHandler<ExecutionStateChangedEventArgs>? ExecutionStateChanged;
    public event EventHandler<BreakOccurredEventArgs>? BreakOccurred;

    public void Pause() => NesStateController.SetExecutionState(_nes, true, NesDebugPauseReason.Manual, e => ExecutionStateChanged?.Invoke(this, e));
    public void Resume() => NesStateController.SetExecutionState(_nes, false, NesDebugPauseReason.Manual, e => ExecutionStateChanged?.Invoke(this, e));

    public void EnableCpuClockTracing(int capacity)
    {
        lock (_nes.SyncRoot)
        {
            _tracer.Enable(capacity);
            NesDebugHookManager.Refresh(_nes, _bp, _tracer);
        }
    }

    public void DisableCpuClockTracing()
    {
        lock (_nes.SyncRoot)
        {
            _tracer.Disable();
            NesDebugHookManager.Refresh(_nes, _bp, _tracer);
        }
    }

    public IReadOnlyList<NesCpuClockTrace> GetCpuClockTrace()
    {
        lock (_nes.SyncRoot) return _tracer.GetTrace();
    }

    public NesCpuClockTraceSnapshot GetCpuClockTraceSnapshot()
    {
        lock (_nes.SyncRoot) return _tracer.GetSnapshot();
    }

    public NesDebugSnapshot CaptureSnapshot(NesDebugSnapshotOptions? options = null)
    {
        lock (_nes.SyncRoot) return NesSnapshotBuilder.Capture(_nes, ExecutionState, options);
    }

    public NesRunResult StepPpuDot() => NesStepController.StepPpuDot(_nes, _bp);
    public NesRunResult StepCpuCycle() => NesStepController.StepCpuCycle(_nes, _bp);
    public NesRunResult StepInstruction() => NesStepController.StepInstruction(_nes, _bp);
    public NesRunResult StepFrame() => NesStepController.StepFrame(_nes, _bp);

    public int GetMemoryRegionSize(NesMemoryRegion region)
    {
        NesMemoryAccessBridge.ValidateSingleRegion(region);
        lock (_nes.SyncRoot) return NesMemoryInspector.GetSize(_nes, region);
    }

    public void CopyMemoryRegion(NesMemoryRegion region, int offset, Span<byte> destination) =>
        NesDebugMutator.CopyMemoryRegion(_nes, region, offset, destination);

    public void WriteMemoryRegion(NesMemoryRegion region, int offset, ReadOnlySpan<byte> source) =>
        NesDebugMutator.WriteMemoryRegion(_nes, region, offset, source);

    public byte PeekCpuMemory(ushort address) => NesDebugInspector.PeekCpu(_nes, address);
    public byte PeekPpuMemory(ushort address) => NesDebugInspector.PeekPpu(_nes, address);

    public void SetCpuRegisters(CpuRegisterValues registers) =>
        NesDebugMutator.SetCpuRegisters(_nes, registers);

    public void WritePpuRegister(ushort register, byte value) =>
        NesDebugMutator.WritePpuRegister(_nes, register, value);

    public void WriteApuRegister(ushort register, byte value) =>
        NesDebugMutator.WriteApuRegister(_nes, register, value);

    public IReadOnlyList<DisassembledInstruction> Disassemble(ushort startAddress, int count) =>
        NesDebugInspector.Disassemble(_nes, startAddress, count);

    public IReadOnlyList<DisassembledInstruction> DisassembleAtProgramCounter(int count = 32) =>
        NesDebugInspector.DisassembleAtPc(_nes, count);

    public PatternTableSnapshot CapturePatternTable(int tableIndex, int paletteIndex) =>
        NesDebugInspector.CapturePatternTable(_nes, tableIndex, paletteIndex);

    public NesBreakpoint AddBreakpoint(NesDebugBreakKind kind, ushort start, ushort? end = null) =>
        NesBreakpointController.Add(_nes, _bp, _tracer, kind, start, end);

    public bool SetBreakpointEnabled(long id, bool enabled) =>
        NesBreakpointController.SetEnabled(_nes, _bp, _tracer, id, enabled);

    public bool RemoveBreakpoint(long id) =>
        NesBreakpointController.Remove(_nes, _bp, _tracer, id);

    public IReadOnlyList<NesBreakpoint> GetBreakpoints() =>
        NesBreakpointController.GetAll(_nes, _bp);

    public void ClearBreakpoints() =>
        NesBreakpointController.Clear(_nes, _bp, _tracer);

    internal bool TryBreakBeforeCpuClockLocked() => NesBreakpointDispatcher.TryBreakBeforeCpuClock(_nes, _bp);
    internal void CompleteDotLocked() => NesBreakpointDispatcher.CompleteDot(_nes, _bp);

    internal void DispatchPendingEvents() =>
        NesBreakpointDispatcher.DispatchPendingEvents(_nes, _bp,
            e => ExecutionStateChanged?.Invoke(this, e),
            e => BreakOccurred?.Invoke(this, e));

    internal void ResetTransientStateLocked() => _bp.ResetTransient();
    internal void FinishResetLocked() => _bp.SuppressBreakpoints = false;
    internal void BeginCpuClockTraceLocked() => _tracer.BeginClock(_nes);
    internal void CompleteCpuClockTraceLocked(NesCpuClockActor actor) => _tracer.CompleteClock(_nes, actor);
}
