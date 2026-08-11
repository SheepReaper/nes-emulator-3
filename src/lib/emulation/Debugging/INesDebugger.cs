using System;
using System.Collections.Generic;
namespace Sheep.Emulation.Nes.Debugging;

public interface INesDebugger
{
    bool IsPaused { get; }
    NesExecutionState ExecutionState { get; }
    /// <summary>Gets the CPU program counter at the current emulated cycle.</summary>
    ushort ProgramCounter { get; }
    event EventHandler<ExecutionStateChangedEventArgs>? ExecutionStateChanged;
    event EventHandler<BreakOccurredEventArgs>? BreakOccurred;
    void Pause();
    void Resume();
    NesDebugSnapshot CaptureSnapshot(NesDebugSnapshotOptions? options = null);
    void EnableCpuClockTracing(int capacity);
    void DisableCpuClockTracing();
    IReadOnlyList<NesCpuClockTrace> GetCpuClockTrace();
    NesCpuClockTraceSnapshot GetCpuClockTraceSnapshot();
    NesRunResult StepPpuDot();
    NesRunResult StepCpuCycle();
    NesRunResult StepInstruction();
    NesRunResult StepFrame();
    int GetMemoryRegionSize(NesMemoryRegion region);
    void CopyMemoryRegion(NesMemoryRegion region, int offset, Span<byte> destination);
    void WriteMemoryRegion(NesMemoryRegion region, int offset, ReadOnlySpan<byte> source);
    byte PeekCpuMemory(ushort address);
    byte PeekPpuMemory(ushort address);
    void SetCpuRegisters(CpuRegisterValues registers);
    void WritePpuRegister(ushort register, byte value);
    void WriteApuRegister(ushort register, byte value);
    IReadOnlyList<DisassembledInstruction> Disassemble(ushort startAddress, int instructionCount);
    IReadOnlyList<DisassembledInstruction> DisassembleAtProgramCounter(int instructionCount = 32);
    PatternTableSnapshot CapturePatternTable(int tableIndex, int paletteIndex);
    NesBreakpoint AddBreakpoint(NesDebugBreakKind kind, ushort startAddress, ushort? endAddress = null);
    bool SetBreakpointEnabled(long id, bool enabled);
    bool RemoveBreakpoint(long id);
    IReadOnlyList<NesBreakpoint> GetBreakpoints();
    void ClearBreakpoints();
}
