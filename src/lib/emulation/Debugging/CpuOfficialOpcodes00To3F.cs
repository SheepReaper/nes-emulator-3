namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Descriptors for 6502 official opcodes $00 to $3F.
/// </summary>
internal static class CpuOfficialOpcodes00To3F
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

        I(0x00, "BRK", 7);
        M(0x01, "ORA", CpuAddressingMode.IndexedIndirect, 6);
        M(0x05, "ORA", CpuAddressingMode.ZeroPage, 3);
        M(0x06, "ASL", CpuAddressingMode.ZeroPage, 5);
        I(0x08, "PHP", 3);
        M(0x09, "ORA", CpuAddressingMode.Immediate, 2);
        A(0x0A, "ASL");
        M(0x0D, "ORA", CpuAddressingMode.Absolute, 4);
        M(0x0E, "ASL", CpuAddressingMode.Absolute, 6);

        M(0x10, "BPL", CpuAddressingMode.Relative, 2);
        M(0x11, "ORA", CpuAddressingMode.IndirectIndexed, 5);
        M(0x15, "ORA", CpuAddressingMode.ZeroPageX, 4);
        M(0x16, "ASL", CpuAddressingMode.ZeroPageX, 6);
        I(0x18, "CLC");
        M(0x19, "ORA", CpuAddressingMode.AbsoluteY, 4);
        M(0x1D, "ORA", CpuAddressingMode.AbsoluteX, 4);
        M(0x1E, "ASL", CpuAddressingMode.AbsoluteX, 7);

        M(0x20, "JSR", CpuAddressingMode.Absolute, 6);
        M(0x21, "AND", CpuAddressingMode.IndexedIndirect, 6);
        M(0x24, "BIT", CpuAddressingMode.ZeroPage, 3);
        M(0x25, "AND", CpuAddressingMode.ZeroPage, 3);
        M(0x26, "ROL", CpuAddressingMode.ZeroPage, 5);
        I(0x28, "PLP", 4);
        M(0x29, "AND", CpuAddressingMode.Immediate, 2);
        A(0x2A, "ROL");
        M(0x2C, "BIT", CpuAddressingMode.Absolute, 4);
        M(0x2D, "AND", CpuAddressingMode.Absolute, 4);
        M(0x2E, "ROL", CpuAddressingMode.Absolute, 6);

        M(0x30, "BMI", CpuAddressingMode.Relative, 2);
        M(0x31, "AND", CpuAddressingMode.IndirectIndexed, 5);
        M(0x35, "AND", CpuAddressingMode.ZeroPageX, 4);
        M(0x36, "ROL", CpuAddressingMode.ZeroPageX, 6);
        I(0x38, "SEC");
        M(0x39, "AND", CpuAddressingMode.AbsoluteY, 4);
        M(0x3D, "AND", CpuAddressingMode.AbsoluteX, 4);
        M(0x3E, "ROL", CpuAddressingMode.AbsoluteX, 7);
    }
}
