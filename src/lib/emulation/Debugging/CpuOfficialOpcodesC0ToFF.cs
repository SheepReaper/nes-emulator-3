namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Descriptors for 6502 official opcodes $C0 to $FF.
/// </summary>
internal static class CpuOfficialOpcodesC0ToFF
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

        M(0xC0, "CPY", CpuAddressingMode.Immediate, 2);
        M(0xC1, "CMP", CpuAddressingMode.IndexedIndirect, 6);
        M(0xC4, "CPY", CpuAddressingMode.ZeroPage, 3);
        M(0xC5, "CMP", CpuAddressingMode.ZeroPage, 3);
        M(0xC6, "DEC", CpuAddressingMode.ZeroPage, 5);
        I(0xC8, "INY");
        M(0xC9, "CMP", CpuAddressingMode.Immediate, 2);
        I(0xCA, "DEX");
        M(0xCC, "CPY", CpuAddressingMode.Absolute, 4);
        M(0xCD, "CMP", CpuAddressingMode.Absolute, 4);
        M(0xCE, "DEC", CpuAddressingMode.Absolute, 6);

        M(0xD0, "BNE", CpuAddressingMode.Relative, 2);
        M(0xD1, "CMP", CpuAddressingMode.IndirectIndexed, 5);
        M(0xD5, "CMP", CpuAddressingMode.ZeroPageX, 4);
        M(0xD6, "DEC", CpuAddressingMode.ZeroPageX, 6);
        I(0xD8, "CLD");
        M(0xD9, "CMP", CpuAddressingMode.AbsoluteY, 4);
        M(0xDD, "CMP", CpuAddressingMode.AbsoluteX, 4);
        M(0xDE, "DEC", CpuAddressingMode.AbsoluteX, 7);

        M(0xE0, "CPX", CpuAddressingMode.Immediate, 2);
        M(0xE1, "SBC", CpuAddressingMode.IndexedIndirect, 6);
        M(0xE4, "CPX", CpuAddressingMode.ZeroPage, 3);
        M(0xE5, "SBC", CpuAddressingMode.ZeroPage, 3);
        M(0xE6, "INC", CpuAddressingMode.ZeroPage, 5);
        I(0xE8, "INX");
        M(0xE9, "SBC", CpuAddressingMode.Immediate, 2);
        I(0xEA, "NOP");
        M(0xEC, "CPX", CpuAddressingMode.Absolute, 4);
        M(0xED, "SBC", CpuAddressingMode.Absolute, 4);
        M(0xEE, "INC", CpuAddressingMode.Absolute, 6);

        M(0xF0, "BEQ", CpuAddressingMode.Relative, 2);
        M(0xF1, "SBC", CpuAddressingMode.IndirectIndexed, 5);
        M(0xF5, "SBC", CpuAddressingMode.ZeroPageX, 4);
        M(0xF6, "INC", CpuAddressingMode.ZeroPageX, 6);
        I(0xF8, "SED");
        M(0xF9, "SBC", CpuAddressingMode.AbsoluteY, 4);
        M(0xFD, "SBC", CpuAddressingMode.AbsoluteX, 4);
        M(0xFE, "INC", CpuAddressingMode.AbsoluteX, 7);
    }
}
