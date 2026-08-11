namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Descriptors for 6502 official opcodes $80 to $BF.
/// </summary>
internal static class CpuOfficialOpcodes80ToBF
{
    internal static void Populate(System.Action<byte, string, CpuAddressingMode, int, int, bool> add)
    {
        void I(byte op, string name, int cycles = 2) => add(op, name, CpuAddressingMode.Implied, 1, cycles, true);
        void M(byte op, string name, CpuAddressingMode mode, int cycles)
        {
            var length = mode is CpuAddressingMode.Immediate or CpuAddressingMode.ZeroPage or
                CpuAddressingMode.ZeroPageX or CpuAddressingMode.ZeroPageY or CpuAddressingMode.Relative or
                CpuAddressingMode.IndexedIndirect or CpuAddressingMode.IndirectIndexed ? 2 : 3;
            add(op, name, mode, length, cycles, true);
        }

        M(0x81, "STA", CpuAddressingMode.IndexedIndirect, 6);
        M(0x84, "STY", CpuAddressingMode.ZeroPage, 3);
        M(0x85, "STA", CpuAddressingMode.ZeroPage, 3);
        M(0x86, "STX", CpuAddressingMode.ZeroPage, 3);
        I(0x88, "DEY");
        I(0x8A, "TXA");
        M(0x8C, "STY", CpuAddressingMode.Absolute, 4);
        M(0x8D, "STA", CpuAddressingMode.Absolute, 4);
        M(0x8E, "STX", CpuAddressingMode.Absolute, 4);

        M(0x90, "BCC", CpuAddressingMode.Relative, 2);
        M(0x91, "STA", CpuAddressingMode.IndirectIndexed, 6);
        M(0x94, "STY", CpuAddressingMode.ZeroPageX, 4);
        M(0x95, "STA", CpuAddressingMode.ZeroPageX, 4);
        M(0x96, "STX", CpuAddressingMode.ZeroPageY, 4);
        I(0x98, "TYA");
        M(0x99, "STA", CpuAddressingMode.AbsoluteY, 5);
        I(0x9A, "TXS");
        M(0x9D, "STA", CpuAddressingMode.AbsoluteX, 5);

        M(0xA0, "LDY", CpuAddressingMode.Immediate, 2);
        M(0xA1, "LDA", CpuAddressingMode.IndexedIndirect, 6);
        M(0xA2, "LDX", CpuAddressingMode.Immediate, 2);
        M(0xA4, "LDY", CpuAddressingMode.ZeroPage, 3);
        M(0xA5, "LDA", CpuAddressingMode.ZeroPage, 3);
        M(0xA6, "LDX", CpuAddressingMode.ZeroPage, 3);
        I(0xA8, "TAY");
        M(0xA9, "LDA", CpuAddressingMode.Immediate, 2);
        I(0xAA, "TAX");
        M(0xAC, "LDY", CpuAddressingMode.Absolute, 4);
        M(0xAD, "LDA", CpuAddressingMode.Absolute, 4);
        M(0xAE, "LDX", CpuAddressingMode.Absolute, 4);

        M(0xB0, "BCS", CpuAddressingMode.Relative, 2);
        M(0xB1, "LDA", CpuAddressingMode.IndirectIndexed, 5);
        M(0xB4, "LDY", CpuAddressingMode.ZeroPageX, 4);
        M(0xB5, "LDA", CpuAddressingMode.ZeroPageX, 4);
        M(0xB6, "LDX", CpuAddressingMode.ZeroPageY, 4);
        I(0xB8, "CLV");
        M(0xB9, "LDA", CpuAddressingMode.AbsoluteY, 4);
        I(0xBA, "TSX");
        M(0xBC, "LDY", CpuAddressingMode.AbsoluteX, 4);
        M(0xBD, "LDA", CpuAddressingMode.AbsoluteX, 4);
        M(0xBE, "LDX", CpuAddressingMode.AbsoluteY, 4);
    }
}
