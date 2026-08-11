namespace Sheep.Emulation.Nes.Debugging;

public enum CpuAddressingMode
{
    Implied, Accumulator, Immediate, ZeroPage, ZeroPageX, ZeroPageY, Relative,
    Absolute, AbsoluteX, AbsoluteY, Indirect, IndexedIndirect, IndirectIndexed
}