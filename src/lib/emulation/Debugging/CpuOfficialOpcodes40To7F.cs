namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Descriptors for 6502 official opcodes $40 to $7F.
/// </summary>
internal static class CpuOfficialOpcodes40To7F
{
    internal static void Populate(System.Action<byte, string, CpuAddressingMode, int, int, bool> add)
    {
        void I(byte op, string name, int cycles = 2) => add(op, name, CpuAddressingMode.Implied, 1, cycles, true);
        void A(byte op, string name, int cycles = 2) => add(op, name, CpuAddressingMode.Accumulator, 1, cycles, true);
        void M(byte op, string name, CpuAddressingMode mode, int cycles)
        {
            var length = mode is CpuAddressingMode.Immediate or CpuAddressingMode.ZeroPage or
                CpuAddressingMode.ZeroPageX or CpuAddressingMode.ZeroPageY or CpuAddressingMode.Relative or
                CpuAddressingMode.IndexedIndirect or CpuAddressingMode.IndirectIndexed ? 2 : 3;
            add(op, name, mode, length, cycles, true);
        }

        I(0x40, "RTI", 6);
        M(0x41, "EOR", CpuAddressingMode.IndexedIndirect, 6);
        M(0x45, "EOR", CpuAddressingMode.ZeroPage, 3);
        M(0x46, "LSR", CpuAddressingMode.ZeroPage, 5);
        I(0x48, "PHA", 3);
        M(0x49, "EOR", CpuAddressingMode.Immediate, 2);
        A(0x4A, "LSR");
        M(0x4C, "JMP", CpuAddressingMode.Absolute, 3);
        M(0x4D, "EOR", CpuAddressingMode.Absolute, 4);
        M(0x4E, "LSR", CpuAddressingMode.Absolute, 6);

        M(0x50, "BVC", CpuAddressingMode.Relative, 2);
        M(0x51, "EOR", CpuAddressingMode.IndirectIndexed, 5);
        M(0x55, "EOR", CpuAddressingMode.ZeroPageX, 4);
        M(0x56, "LSR", CpuAddressingMode.ZeroPageX, 6);
        I(0x58, "CLI");
        M(0x59, "EOR", CpuAddressingMode.AbsoluteY, 4);
        M(0x5D, "EOR", CpuAddressingMode.AbsoluteX, 4);
        M(0x5E, "LSR", CpuAddressingMode.AbsoluteX, 7);

        I(0x60, "RTS", 6);
        M(0x61, "ADC", CpuAddressingMode.IndexedIndirect, 6);
        M(0x65, "ADC", CpuAddressingMode.ZeroPage, 3);
        M(0x66, "ROR", CpuAddressingMode.ZeroPage, 5);
        I(0x68, "PLA", 4);
        M(0x69, "ADC", CpuAddressingMode.Immediate, 2);
        A(0x6A, "ROR");
        M(0x6C, "JMP", CpuAddressingMode.Indirect, 5);
        M(0x6D, "ADC", CpuAddressingMode.Absolute, 4);
        M(0x6E, "ROR", CpuAddressingMode.Absolute, 6);

        M(0x70, "BVS", CpuAddressingMode.Relative, 2);
        M(0x71, "ADC", CpuAddressingMode.IndirectIndexed, 5);
        M(0x75, "ADC", CpuAddressingMode.ZeroPageX, 4);
        M(0x76, "ROR", CpuAddressingMode.ZeroPageX, 6);
        I(0x78, "SEI");
        M(0x79, "ADC", CpuAddressingMode.AbsoluteY, 4);
        M(0x7D, "ADC", CpuAddressingMode.AbsoluteX, 4);
        M(0x7E, "ROR", CpuAddressingMode.AbsoluteX, 7);
    }
}
