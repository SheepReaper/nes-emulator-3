using System;
using System.Collections.Generic;

namespace SR.Emulation.Nes;

public enum NesExecutionState { Running, Paused }
public enum NesRunStopReason { Completed, Paused, Breakpoint }
public enum NesDebugBreakKind { Execute, CpuRead, CpuWrite }
public enum NesDebugPauseReason { Manual, StepCompleted, Breakpoint }

[Flags]
public enum NesDebugSnapshotSections
{
    None = 0,
    Cpu = 1 << 0,
    Ppu = 1 << 1,
    Apu = 1 << 2,
    Timing = 1 << 3,
    Memory = 1 << 4,
    Disassembly = 1 << 5,
    PatternTables = 1 << 6,
    Core = Cpu | Ppu | Apu | Timing,
    All = Core | Memory | Disassembly | PatternTables
}

[Flags]
public enum NesMemoryRegion
{
    None = 0,
    CpuRam = 1 << 0,
    PpuVram = 1 << 1,
    PaletteRam = 1 << 2,
    Oam = 1 << 3,
    PrgRom = 1 << 4,
    Chr = 1 << 5,
    CartridgeRam = 1 << 6,
    All = CpuRam | PpuVram | PaletteRam | Oam | PrgRom | Chr | CartridgeRam
}

public sealed class NesDebugSnapshotOptions
{
    public NesDebugSnapshotSections Sections { get; set; } = NesDebugSnapshotSections.Core;
    public NesMemoryRegion MemoryRegions { get; set; } = NesMemoryRegion.None;
    public ushort? DisassemblyStartAddress { get; set; }
    public int DisassemblyInstructionCount { get; set; } = 32;
    public int PatternPalette { get; set; }
}

public sealed class NesDebugSnapshot(
    CpuDebugState? cpu,
    PpuDebugState? ppu,
    ApuDebugState? apu,
    NesTimingDebugState? timing,
    IReadOnlyList<MemoryRegionSnapshot>? memory,
    IReadOnlyList<DisassembledInstruction>? disassembly,
    IReadOnlyList<PatternTableSnapshot>? patternTables)
{
    public CpuDebugState? Cpu { get; } = cpu;
    public PpuDebugState? Ppu { get; } = ppu;
    public ApuDebugState? Apu { get; } = apu;
    public NesTimingDebugState? Timing { get; } = timing;
    public IReadOnlyList<MemoryRegionSnapshot>? Memory { get; } = memory;
    public IReadOnlyList<DisassembledInstruction>? Disassembly { get; } = disassembly;
    public IReadOnlyList<PatternTableSnapshot>? PatternTables { get; } = patternTables;
}

public sealed class CpuDebugState(
    byte accumulator, byte x, byte y, byte stackPointer, ushort programCounter, byte status,
    byte opcode, int cyclesRemaining, ulong totalCycles, bool isInstructionBoundary)
{
    public byte Accumulator { get; } = accumulator;
    public byte X { get; } = x;
    public byte Y { get; } = y;
    public byte StackPointer { get; } = stackPointer;
    public ushort ProgramCounter { get; } = programCounter;
    public byte Status { get; } = status;
    public byte Opcode { get; } = opcode;
    public int CyclesRemaining { get; } = cyclesRemaining;
    public ulong TotalCycles { get; } = totalCycles;
    public bool IsInstructionBoundary { get; } = isInstructionBoundary;
}

public readonly struct CpuRegisterValues(
    byte accumulator, byte x, byte y, byte stackPointer, ushort programCounter, byte status)
{
    public byte Accumulator { get; } = accumulator;
    public byte X { get; } = x;
    public byte Y { get; } = y;
    public byte StackPointer { get; } = stackPointer;
    public ushort ProgramCounter { get; } = programCounter;
    public byte Status { get; } = status;
}

public sealed class PpuDebugState(
    byte control, byte mask, byte status, byte oamAddress, ushort vramAddress,
    ushort temporaryVramAddress, byte fineX, bool writeToggle, byte dataBuffer,
    int scanline, int dot, ulong frameNumber, bool oddFrame, int evaluatedSpriteCount)
{
    public byte Control { get; } = control;
    public byte Mask { get; } = mask;
    public byte Status { get; } = status;
    public byte OamAddress { get; } = oamAddress;
    public ushort VramAddress { get; } = vramAddress;
    public ushort TemporaryVramAddress { get; } = temporaryVramAddress;
    public byte FineX { get; } = fineX;
    public bool WriteToggle { get; } = writeToggle;
    public byte DataBuffer { get; } = dataBuffer;
    public int Scanline { get; } = scanline;
    public int Dot { get; } = dot;
    public ulong FrameNumber { get; } = frameNumber;
    public bool IsOddFrame { get; } = oddFrame;
    public int EvaluatedSpriteCount { get; } = evaluatedSpriteCount;
}

public sealed class ApuDebugState(
    bool isImplemented, ReadOnlyMemory<byte> registers, int frameCycle = 0,
    bool fiveStepMode = false, bool frameIrq = false, bool dmcIrq = false,
    byte pulse1Length = 0, byte pulse2Length = 0, byte triangleLength = 0,
    byte noiseLength = 0, byte pulse1Output = 0, byte pulse2Output = 0,
    byte triangleOutput = 0, byte noiseOutput = 0, byte dmcOutput = 0,
    ushort dmcAddress = 0, ushort dmcBytesRemaining = 0)
{
    public bool IsImplemented { get; } = isImplemented;
    public ReadOnlyMemory<byte> Registers { get; } = registers;
    public int FrameCycle { get; } = frameCycle;
    public bool FiveStepMode { get; } = fiveStepMode;
    public bool FrameIrq { get; } = frameIrq;
    public bool DmcIrq { get; } = dmcIrq;
    public byte Pulse1Length { get; } = pulse1Length;
    public byte Pulse2Length { get; } = pulse2Length;
    public byte TriangleLength { get; } = triangleLength;
    public byte NoiseLength { get; } = noiseLength;
    public byte Pulse1Output { get; } = pulse1Output;
    public byte Pulse2Output { get; } = pulse2Output;
    public byte TriangleOutput { get; } = triangleOutput;
    public byte NoiseOutput { get; } = noiseOutput;
    public byte DmcOutput { get; } = dmcOutput;
    public ushort DmcAddress { get; } = dmcAddress;
    public ushort DmcBytesRemaining { get; } = dmcBytesRemaining;
}

public sealed class NesTimingDebugState(
    NesVideoStandard videoStandard, NesExecutionState executionState, ulong cpuClocks,
    ulong frameNumber, double requestedSpeedMultiplier)
{
    public NesVideoStandard VideoStandard { get; } = videoStandard;
    public NesExecutionState ExecutionState { get; } = executionState;
    public ulong CpuClocks { get; } = cpuClocks;
    public ulong FrameNumber { get; } = frameNumber;
    public double RequestedSpeedMultiplier { get; } = requestedSpeedMultiplier;
}

public sealed class MemoryRegionSnapshot(NesMemoryRegion region, ReadOnlyMemory<byte> data, bool isWritable)
{
    public NesMemoryRegion Region { get; } = region;
    public ReadOnlyMemory<byte> Data { get; } = data;
    public bool IsWritable { get; } = isWritable;
}

public enum CpuAddressingMode
{
    Implied, Accumulator, Immediate, ZeroPage, ZeroPageX, ZeroPageY, Relative,
    Absolute, AbsoluteX, AbsoluteY, Indirect, IndexedIndirect, IndirectIndexed
}

public sealed class DisassembledInstruction(
    ushort address, ReadOnlyMemory<byte> bytes, string mnemonic, string operand,
    CpuAddressingMode addressingMode, bool isCurrent)
{
    public ushort Address { get; } = address;
    public ReadOnlyMemory<byte> Bytes { get; } = bytes;
    public string Mnemonic { get; } = mnemonic;
    public string Operand { get; } = operand;
    public CpuAddressingMode AddressingMode { get; } = addressingMode;
    public int Length => Bytes.Length;
    public bool IsCurrent { get; } = isCurrent;
}

public sealed class PatternTableSnapshot(int tableIndex, int paletteIndex, ReadOnlyMemory<byte> rgba)
{
    public const int Width = 128;
    public const int Height = 128;
    public int TableIndex { get; } = tableIndex;
    public int PaletteIndex { get; } = paletteIndex;
    public ReadOnlyMemory<byte> Rgba { get; } = rgba;
}

public sealed class NesBreakpoint(
    long id, NesDebugBreakKind kind, ushort startAddress, ushort endAddress, bool isEnabled)
{
    public long Id { get; } = id;
    public NesDebugBreakKind Kind { get; } = kind;
    public ushort StartAddress { get; } = startAddress;
    public ushort EndAddress { get; } = endAddress;
    public bool IsEnabled { get; } = isEnabled;
}

public sealed class BreakOccurredEventArgs(
    NesBreakpoint breakpoint, ushort address, byte? value, ushort programCounter) : EventArgs
{
    public NesBreakpoint Breakpoint { get; } = breakpoint;
    public ushort Address { get; } = address;
    public byte? Value { get; } = value;
    public ushort ProgramCounter { get; } = programCounter;
}

public sealed class ExecutionStateChangedEventArgs(
    NesExecutionState previous, NesExecutionState current, NesDebugPauseReason reason) : EventArgs
{
    public NesExecutionState Previous { get; } = previous;
    public NesExecutionState Current { get; } = current;
    public NesDebugPauseReason Reason { get; } = reason;
}

public readonly struct NesRunResult(
    int ppuDots, ulong cpuClocks, ulong frames, NesRunStopReason stopReason)
{
    public int PpuDots { get; } = ppuDots;
    public ulong CpuClocks { get; } = cpuClocks;
    public ulong Frames { get; } = frames;
    public NesRunStopReason StopReason { get; } = stopReason;
}

public interface INesDebugger
{
    bool IsPaused { get; }
    NesExecutionState ExecutionState { get; }
    event EventHandler<ExecutionStateChangedEventArgs>? ExecutionStateChanged;
    event EventHandler<BreakOccurredEventArgs>? BreakOccurred;
    void Pause();
    void Resume();
    NesDebugSnapshot CaptureSnapshot(NesDebugSnapshotOptions? options = null);
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
