namespace Sheep.Emulation.Nes.Debugging;

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