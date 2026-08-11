using System;
namespace Sheep.Emulation.Nes.Debugging;

public sealed class BreakOccurredEventArgs(
    NesBreakpoint breakpoint, ushort address, byte? value, ushort programCounter) : EventArgs
{
    public NesBreakpoint Breakpoint { get; } = breakpoint;
    public ushort Address { get; } = address;
    public byte? Value { get; } = value;
    public ushort ProgramCounter { get; } = programCounter;
}