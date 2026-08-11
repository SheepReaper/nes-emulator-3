namespace Sheep.Emulation.Nes.Debugging;

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