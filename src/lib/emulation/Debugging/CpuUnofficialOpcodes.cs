namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Unofficial/illegal 6502 opcode table definitions.
/// </summary>
internal static class CpuUnofficialOpcodes
{
    internal static void Populate(System.Action<byte, string, CpuAddressingMode, int, int, bool> add)
    {
        foreach (var op in new byte[] { 0x1A, 0x3A, 0x5A, 0x7A, 0xDA, 0xFA })
        {
            add(op, "NOP", CpuAddressingMode.Implied, 1, 2, false);
        }

        foreach (var op in new byte[] { 0x04, 0x44, 0x64 })
        {
            add(op, "NOP", CpuAddressingMode.ZeroPage, 2, 3, false);
        }

        add(0x0C, "NOP", CpuAddressingMode.Absolute, 3, 4, false);

        foreach (var op in new byte[] { 0x14, 0x34, 0x54, 0x74, 0xD4, 0xF4 })
        {
            add(op, "NOP", CpuAddressingMode.ZeroPageX, 2, 4, false);
        }

        foreach (var op in new byte[] { 0x1C, 0x3C, 0x5C, 0x7C, 0xDC, 0xFC })
        {
            add(op, "NOP", CpuAddressingMode.AbsoluteX, 3, 4, false);
        }

        foreach (var op in new byte[] { 0x80, 0x82, 0x89, 0xC2, 0xE2 })
        {
            add(op, "NOP", CpuAddressingMode.Immediate, 2, 2, false);
        }

        add(0xEB, "SBC", CpuAddressingMode.Immediate, 2, 2, false);
        add(0x0B, "AAC", CpuAddressingMode.Immediate, 2, 2, false);
        add(0x2B, "AAC", CpuAddressingMode.Immediate, 2, 2, false);
        add(0x4B, "ASR", CpuAddressingMode.Immediate, 2, 2, false);
        add(0x6B, "ARR", CpuAddressingMode.Immediate, 2, 2, false);
        add(0xAB, "ATX", CpuAddressingMode.Immediate, 2, 2, false);
        add(0x8B, "XAA", CpuAddressingMode.Immediate, 2, 2, false);
        add(0x93, "AHX", CpuAddressingMode.IndirectIndexed, 2, 6, false);
        add(0x9B, "TAS", CpuAddressingMode.AbsoluteY, 3, 5, false);
        add(0x9F, "AHX", CpuAddressingMode.AbsoluteY, 3, 5, false);
        add(0xBB, "LAS", CpuAddressingMode.AbsoluteY, 3, 4, false);
        add(0xCB, "AXS", CpuAddressingMode.Immediate, 2, 2, false);
        add(0x07, "SLO", CpuAddressingMode.ZeroPage, 2, 5, false);
        add(0x27, "RLA", CpuAddressingMode.ZeroPage, 2, 5, false);
        add(0x47, "SRE", CpuAddressingMode.ZeroPage, 2, 5, false);
        add(0x67, "RRA", CpuAddressingMode.ZeroPage, 2, 5, false);
        add(0x87, "AAX", CpuAddressingMode.ZeroPage, 2, 3, false);
        add(0xA7, "LAX", CpuAddressingMode.ZeroPage, 2, 3, false);
        add(0xC7, "DCP", CpuAddressingMode.ZeroPage, 2, 5, false);
        add(0xE7, "ISC", CpuAddressingMode.ZeroPage, 2, 5, false);
        add(0x17, "SLO", CpuAddressingMode.ZeroPageX, 2, 6, false);
        add(0x37, "RLA", CpuAddressingMode.ZeroPageX, 2, 6, false);
        add(0x57, "SRE", CpuAddressingMode.ZeroPageX, 2, 6, false);
        add(0x77, "RRA", CpuAddressingMode.ZeroPageX, 2, 6, false);
        add(0x97, "AAX", CpuAddressingMode.ZeroPageY, 2, 4, false);
        add(0xB7, "LAX", CpuAddressingMode.ZeroPageY, 2, 4, false);
        add(0xD7, "DCP", CpuAddressingMode.ZeroPageX, 2, 6, false);
        add(0xF7, "ISC", CpuAddressingMode.ZeroPageX, 2, 6, false);
        add(0x0F, "SLO", CpuAddressingMode.Absolute, 3, 6, false);
        add(0x2F, "RLA", CpuAddressingMode.Absolute, 3, 6, false);
        add(0x4F, "SRE", CpuAddressingMode.Absolute, 3, 6, false);
        add(0x6F, "RRA", CpuAddressingMode.Absolute, 3, 6, false);
        add(0x8F, "AAX", CpuAddressingMode.Absolute, 3, 4, false);
        add(0xAF, "LAX", CpuAddressingMode.Absolute, 3, 4, false);
        add(0xCF, "DCP", CpuAddressingMode.Absolute, 3, 6, false);
        add(0xEF, "ISC", CpuAddressingMode.Absolute, 3, 6, false);
        add(0x1F, "SLO", CpuAddressingMode.AbsoluteX, 3, 7, false);
        add(0x3F, "RLA", CpuAddressingMode.AbsoluteX, 3, 7, false);
        add(0x5F, "SRE", CpuAddressingMode.AbsoluteX, 3, 7, false);
        add(0x7F, "RRA", CpuAddressingMode.AbsoluteX, 3, 7, false);
        add(0x9C, "SYA", CpuAddressingMode.AbsoluteX, 3, 5, false);
        add(0xDF, "DCP", CpuAddressingMode.AbsoluteX, 3, 7, false);
        add(0xFF, "ISC", CpuAddressingMode.AbsoluteX, 3, 7, false);
        add(0x1B, "SLO", CpuAddressingMode.AbsoluteY, 3, 7, false);
        add(0x3B, "RLA", CpuAddressingMode.AbsoluteY, 3, 7, false);
        add(0x5B, "SRE", CpuAddressingMode.AbsoluteY, 3, 7, false);
        add(0x7B, "RRA", CpuAddressingMode.AbsoluteY, 3, 7, false);
        add(0x9E, "SXA", CpuAddressingMode.AbsoluteY, 3, 5, false);
        add(0xBF, "LAX", CpuAddressingMode.AbsoluteY, 3, 4, false);
        add(0xDB, "DCP", CpuAddressingMode.AbsoluteY, 3, 7, false);
        add(0xFB, "ISC", CpuAddressingMode.AbsoluteY, 3, 7, false);
        add(0x03, "SLO", CpuAddressingMode.IndexedIndirect, 2, 8, false);
        add(0x23, "RLA", CpuAddressingMode.IndexedIndirect, 2, 8, false);
        add(0x43, "SRE", CpuAddressingMode.IndexedIndirect, 2, 8, false);
        add(0x63, "RRA", CpuAddressingMode.IndexedIndirect, 2, 8, false);
        add(0x83, "AAX", CpuAddressingMode.IndexedIndirect, 2, 6, false);
        add(0xA3, "LAX", CpuAddressingMode.IndexedIndirect, 2, 6, false);
        add(0xC3, "DCP", CpuAddressingMode.IndexedIndirect, 2, 8, false);
        add(0xE3, "ISC", CpuAddressingMode.IndexedIndirect, 2, 8, false);
        add(0x13, "SLO", CpuAddressingMode.IndirectIndexed, 2, 8, false);
        add(0x33, "RLA", CpuAddressingMode.IndirectIndexed, 2, 8, false);
        add(0x53, "SRE", CpuAddressingMode.IndirectIndexed, 2, 8, false);
        add(0x73, "RRA", CpuAddressingMode.IndirectIndexed, 2, 8, false);
        add(0xB3, "LAX", CpuAddressingMode.IndirectIndexed, 2, 5, false);
        add(0xD3, "DCP", CpuAddressingMode.IndirectIndexed, 2, 8, false);
        add(0xF3, "ISC", CpuAddressingMode.IndirectIndexed, 2, 8, false);
    }
}
