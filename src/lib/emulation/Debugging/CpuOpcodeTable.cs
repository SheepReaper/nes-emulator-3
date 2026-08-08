using System;
using System.Collections.Generic;

namespace SR.Emulation.Nes;

internal sealed class CpuOpcodeDescriptor(
    byte opcode, string mnemonic, CpuAddressingMode mode, int length, int cycles, bool isOfficial)
{
    public byte Opcode { get; } = opcode;
    public string Mnemonic { get; } = mnemonic;
    public CpuAddressingMode Mode { get; } = mode;
    public int Length { get; } = length;
    public int Cycles { get; } = cycles;
    public bool IsOfficial { get; } = isOfficial;
}

internal static class CpuOpcodeTable
{
    private static readonly CpuOpcodeDescriptor?[] Entries = Build();

    public static CpuOpcodeDescriptor? Get(byte opcode) => Entries[opcode];
    public static bool IsOfficial(byte opcode) => Entries[opcode]?.IsOfficial == true;

    private static CpuOpcodeDescriptor?[] Build()
    {
        var entries = new CpuOpcodeDescriptor?[256];
        void Add(byte op, string name, CpuAddressingMode mode, int length, int cycles, bool isOfficial = true) =>
            entries[op] = new CpuOpcodeDescriptor(op, name, mode, length, cycles, isOfficial);
        void I(byte op, string name, int cycles = 2) => Add(op, name, CpuAddressingMode.Implied, 1, cycles);
        void A(byte op, string name, int cycles = 2) => Add(op, name, CpuAddressingMode.Accumulator, 1, cycles);
        void M(byte op, string name, CpuAddressingMode mode, int cycles)
        {
            var length = mode is CpuAddressingMode.Immediate or CpuAddressingMode.ZeroPage or
                CpuAddressingMode.ZeroPageX or CpuAddressingMode.ZeroPageY or CpuAddressingMode.Relative or
                CpuAddressingMode.IndexedIndirect or CpuAddressingMode.IndirectIndexed ? 2 : 3;
            Add(op, name, mode, length, cycles);
        }

        I(0x00,"BRK",7); M(0x01,"ORA",CpuAddressingMode.IndexedIndirect,6); M(0x05,"ORA",CpuAddressingMode.ZeroPage,3); M(0x06,"ASL",CpuAddressingMode.ZeroPage,5); I(0x08,"PHP",3); M(0x09,"ORA",CpuAddressingMode.Immediate,2); A(0x0A,"ASL"); M(0x0D,"ORA",CpuAddressingMode.Absolute,4); M(0x0E,"ASL",CpuAddressingMode.Absolute,6);
        M(0x10,"BPL",CpuAddressingMode.Relative,2); M(0x11,"ORA",CpuAddressingMode.IndirectIndexed,5); M(0x15,"ORA",CpuAddressingMode.ZeroPageX,4); M(0x16,"ASL",CpuAddressingMode.ZeroPageX,6); I(0x18,"CLC"); M(0x19,"ORA",CpuAddressingMode.AbsoluteY,4); M(0x1D,"ORA",CpuAddressingMode.AbsoluteX,4); M(0x1E,"ASL",CpuAddressingMode.AbsoluteX,7);
        M(0x20,"JSR",CpuAddressingMode.Absolute,6); M(0x21,"AND",CpuAddressingMode.IndexedIndirect,6); M(0x24,"BIT",CpuAddressingMode.ZeroPage,3); M(0x25,"AND",CpuAddressingMode.ZeroPage,3); M(0x26,"ROL",CpuAddressingMode.ZeroPage,5); I(0x28,"PLP",4); M(0x29,"AND",CpuAddressingMode.Immediate,2); A(0x2A,"ROL"); M(0x2C,"BIT",CpuAddressingMode.Absolute,4); M(0x2D,"AND",CpuAddressingMode.Absolute,4); M(0x2E,"ROL",CpuAddressingMode.Absolute,6);
        M(0x30,"BMI",CpuAddressingMode.Relative,2); M(0x31,"AND",CpuAddressingMode.IndirectIndexed,5); M(0x35,"AND",CpuAddressingMode.ZeroPageX,4); M(0x36,"ROL",CpuAddressingMode.ZeroPageX,6); I(0x38,"SEC"); M(0x39,"AND",CpuAddressingMode.AbsoluteY,4); M(0x3D,"AND",CpuAddressingMode.AbsoluteX,4); M(0x3E,"ROL",CpuAddressingMode.AbsoluteX,7);
        I(0x40,"RTI",6); M(0x41,"EOR",CpuAddressingMode.IndexedIndirect,6); M(0x45,"EOR",CpuAddressingMode.ZeroPage,3); M(0x46,"LSR",CpuAddressingMode.ZeroPage,5); I(0x48,"PHA",3); M(0x49,"EOR",CpuAddressingMode.Immediate,2); A(0x4A,"LSR"); M(0x4C,"JMP",CpuAddressingMode.Absolute,3); M(0x4D,"EOR",CpuAddressingMode.Absolute,4); M(0x4E,"LSR",CpuAddressingMode.Absolute,6);
        M(0x50,"BVC",CpuAddressingMode.Relative,2); M(0x51,"EOR",CpuAddressingMode.IndirectIndexed,5); M(0x55,"EOR",CpuAddressingMode.ZeroPageX,4); M(0x56,"LSR",CpuAddressingMode.ZeroPageX,6); I(0x58,"CLI"); M(0x59,"EOR",CpuAddressingMode.AbsoluteY,4); M(0x5D,"EOR",CpuAddressingMode.AbsoluteX,4); M(0x5E,"LSR",CpuAddressingMode.AbsoluteX,7);
        I(0x60,"RTS",6); M(0x61,"ADC",CpuAddressingMode.IndexedIndirect,6); M(0x65,"ADC",CpuAddressingMode.ZeroPage,3); M(0x66,"ROR",CpuAddressingMode.ZeroPage,5); I(0x68,"PLA",4); M(0x69,"ADC",CpuAddressingMode.Immediate,2); A(0x6A,"ROR"); M(0x6C,"JMP",CpuAddressingMode.Indirect,5); M(0x6D,"ADC",CpuAddressingMode.Absolute,4); M(0x6E,"ROR",CpuAddressingMode.Absolute,6);
        M(0x70,"BVS",CpuAddressingMode.Relative,2); M(0x71,"ADC",CpuAddressingMode.IndirectIndexed,5); M(0x75,"ADC",CpuAddressingMode.ZeroPageX,4); M(0x76,"ROR",CpuAddressingMode.ZeroPageX,6); I(0x78,"SEI"); M(0x79,"ADC",CpuAddressingMode.AbsoluteY,4); M(0x7D,"ADC",CpuAddressingMode.AbsoluteX,4); M(0x7E,"ROR",CpuAddressingMode.AbsoluteX,7);
        M(0x81,"STA",CpuAddressingMode.IndexedIndirect,6); M(0x84,"STY",CpuAddressingMode.ZeroPage,3); M(0x85,"STA",CpuAddressingMode.ZeroPage,3); M(0x86,"STX",CpuAddressingMode.ZeroPage,3); I(0x88,"DEY"); I(0x8A,"TXA"); M(0x8C,"STY",CpuAddressingMode.Absolute,4); M(0x8D,"STA",CpuAddressingMode.Absolute,4); M(0x8E,"STX",CpuAddressingMode.Absolute,4);
        M(0x90,"BCC",CpuAddressingMode.Relative,2); M(0x91,"STA",CpuAddressingMode.IndirectIndexed,6); M(0x94,"STY",CpuAddressingMode.ZeroPageX,4); M(0x95,"STA",CpuAddressingMode.ZeroPageX,4); M(0x96,"STX",CpuAddressingMode.ZeroPageY,4); I(0x98,"TYA"); M(0x99,"STA",CpuAddressingMode.AbsoluteY,5); I(0x9A,"TXS"); M(0x9D,"STA",CpuAddressingMode.AbsoluteX,5);
        M(0xA0,"LDY",CpuAddressingMode.Immediate,2); M(0xA1,"LDA",CpuAddressingMode.IndexedIndirect,6); M(0xA2,"LDX",CpuAddressingMode.Immediate,2); M(0xA4,"LDY",CpuAddressingMode.ZeroPage,3); M(0xA5,"LDA",CpuAddressingMode.ZeroPage,3); M(0xA6,"LDX",CpuAddressingMode.ZeroPage,3); I(0xA8,"TAY"); M(0xA9,"LDA",CpuAddressingMode.Immediate,2); I(0xAA,"TAX"); M(0xAC,"LDY",CpuAddressingMode.Absolute,4); M(0xAD,"LDA",CpuAddressingMode.Absolute,4); M(0xAE,"LDX",CpuAddressingMode.Absolute,4);
        M(0xB0,"BCS",CpuAddressingMode.Relative,2); M(0xB1,"LDA",CpuAddressingMode.IndirectIndexed,5); M(0xB4,"LDY",CpuAddressingMode.ZeroPageX,4); M(0xB5,"LDA",CpuAddressingMode.ZeroPageX,4); M(0xB6,"LDX",CpuAddressingMode.ZeroPageY,4); I(0xB8,"CLV"); M(0xB9,"LDA",CpuAddressingMode.AbsoluteY,4); I(0xBA,"TSX"); M(0xBC,"LDY",CpuAddressingMode.AbsoluteX,4); M(0xBD,"LDA",CpuAddressingMode.AbsoluteX,4); M(0xBE,"LDX",CpuAddressingMode.AbsoluteY,4);
        M(0xC0,"CPY",CpuAddressingMode.Immediate,2); M(0xC1,"CMP",CpuAddressingMode.IndexedIndirect,6); M(0xC4,"CPY",CpuAddressingMode.ZeroPage,3); M(0xC5,"CMP",CpuAddressingMode.ZeroPage,3); M(0xC6,"DEC",CpuAddressingMode.ZeroPage,5); I(0xC8,"INY"); M(0xC9,"CMP",CpuAddressingMode.Immediate,2); I(0xCA,"DEX"); M(0xCC,"CPY",CpuAddressingMode.Absolute,4); M(0xCD,"CMP",CpuAddressingMode.Absolute,4); M(0xCE,"DEC",CpuAddressingMode.Absolute,6);
        M(0xD0,"BNE",CpuAddressingMode.Relative,2); M(0xD1,"CMP",CpuAddressingMode.IndirectIndexed,5); M(0xD5,"CMP",CpuAddressingMode.ZeroPageX,4); M(0xD6,"DEC",CpuAddressingMode.ZeroPageX,6); I(0xD8,"CLD"); M(0xD9,"CMP",CpuAddressingMode.AbsoluteY,4); M(0xDD,"CMP",CpuAddressingMode.AbsoluteX,4); M(0xDE,"DEC",CpuAddressingMode.AbsoluteX,7);
        M(0xE0,"CPX",CpuAddressingMode.Immediate,2); M(0xE1,"SBC",CpuAddressingMode.IndexedIndirect,6); M(0xE4,"CPX",CpuAddressingMode.ZeroPage,3); M(0xE5,"SBC",CpuAddressingMode.ZeroPage,3); M(0xE6,"INC",CpuAddressingMode.ZeroPage,5); I(0xE8,"INX"); M(0xE9,"SBC",CpuAddressingMode.Immediate,2); I(0xEA,"NOP"); M(0xEC,"CPX",CpuAddressingMode.Absolute,4); M(0xED,"SBC",CpuAddressingMode.Absolute,4); M(0xEE,"INC",CpuAddressingMode.Absolute,6);
        M(0xF0,"BEQ",CpuAddressingMode.Relative,2); M(0xF1,"SBC",CpuAddressingMode.IndirectIndexed,5); M(0xF5,"SBC",CpuAddressingMode.ZeroPageX,4); M(0xF6,"INC",CpuAddressingMode.ZeroPageX,6); I(0xF8,"SED"); M(0xF9,"SBC",CpuAddressingMode.AbsoluteY,4); M(0xFD,"SBC",CpuAddressingMode.AbsoluteX,4); M(0xFE,"INC",CpuAddressingMode.AbsoluteX,7);

        // Stable unofficial NOP encodings already implemented by the CPU decoder.
        foreach (var op in new byte[] { 0x1A, 0x3A, 0x5A, 0x7A, 0xDA, 0xFA }) Add(op, "NOP", CpuAddressingMode.Implied, 1, 2, false);
        foreach (var op in new byte[] { 0x04, 0x44, 0x64 }) Add(op, "NOP", CpuAddressingMode.ZeroPage, 2, 3, false);
        Add(0x0C, "NOP", CpuAddressingMode.Absolute, 3, 4, false);
        foreach (var op in new byte[] { 0x14, 0x34, 0x54, 0x74, 0xD4, 0xF4 }) Add(op, "NOP", CpuAddressingMode.ZeroPageX, 2, 4, false);
        foreach (var op in new byte[] { 0x1C, 0x3C, 0x5C, 0x7C, 0xDC, 0xFC }) Add(op, "NOP", CpuAddressingMode.AbsoluteX, 3, 4, false);
        foreach (var op in new byte[] { 0x80, 0x82, 0x89, 0xC2, 0xE2 }) Add(op, "NOP", CpuAddressingMode.Immediate, 2, 2, false);
        Add(0xEB, "SBC", CpuAddressingMode.Immediate, 2, 2, false);
        Add(0x0B, "AAC", CpuAddressingMode.Immediate, 2, 2, false);
        Add(0x2B, "AAC", CpuAddressingMode.Immediate, 2, 2, false);
        Add(0x4B, "ASR", CpuAddressingMode.Immediate, 2, 2, false);
        Add(0x6B, "ARR", CpuAddressingMode.Immediate, 2, 2, false);
        Add(0xAB, "ATX", CpuAddressingMode.Immediate, 2, 2, false);
        Add(0xCB, "AXS", CpuAddressingMode.Immediate, 2, 2, false);
        Add(0x07, "SLO", CpuAddressingMode.ZeroPage, 2, 5, false);
        Add(0x27, "RLA", CpuAddressingMode.ZeroPage, 2, 5, false);
        Add(0x47, "SRE", CpuAddressingMode.ZeroPage, 2, 5, false);
        Add(0x67, "RRA", CpuAddressingMode.ZeroPage, 2, 5, false);
        Add(0x87, "AAX", CpuAddressingMode.ZeroPage, 2, 3, false);
        Add(0xA7, "LAX", CpuAddressingMode.ZeroPage, 2, 3, false);
        Add(0xC7, "DCP", CpuAddressingMode.ZeroPage, 2, 5, false);
        Add(0xE7, "ISC", CpuAddressingMode.ZeroPage, 2, 5, false);
        Add(0x17, "SLO", CpuAddressingMode.ZeroPageX, 2, 6, false);
        Add(0x37, "RLA", CpuAddressingMode.ZeroPageX, 2, 6, false);
        Add(0x57, "SRE", CpuAddressingMode.ZeroPageX, 2, 6, false);
        Add(0x77, "RRA", CpuAddressingMode.ZeroPageX, 2, 6, false);
        Add(0x97, "AAX", CpuAddressingMode.ZeroPageY, 2, 4, false);
        Add(0xB7, "LAX", CpuAddressingMode.ZeroPageY, 2, 4, false);
        Add(0xD7, "DCP", CpuAddressingMode.ZeroPageX, 2, 6, false);
        Add(0xF7, "ISC", CpuAddressingMode.ZeroPageX, 2, 6, false);
        Add(0x0F, "SLO", CpuAddressingMode.Absolute, 3, 6, false);
        Add(0x2F, "RLA", CpuAddressingMode.Absolute, 3, 6, false);
        Add(0x4F, "SRE", CpuAddressingMode.Absolute, 3, 6, false);
        Add(0x6F, "RRA", CpuAddressingMode.Absolute, 3, 6, false);
        Add(0x8F, "AAX", CpuAddressingMode.Absolute, 3, 4, false);
        Add(0xAF, "LAX", CpuAddressingMode.Absolute, 3, 4, false);
        Add(0xCF, "DCP", CpuAddressingMode.Absolute, 3, 6, false);
        Add(0xEF, "ISC", CpuAddressingMode.Absolute, 3, 6, false);
        Add(0x1F, "SLO", CpuAddressingMode.AbsoluteX, 3, 7, false);
        Add(0x3F, "RLA", CpuAddressingMode.AbsoluteX, 3, 7, false);
        Add(0x5F, "SRE", CpuAddressingMode.AbsoluteX, 3, 7, false);
        Add(0x7F, "RRA", CpuAddressingMode.AbsoluteX, 3, 7, false);
        Add(0x9C, "SYA", CpuAddressingMode.AbsoluteX, 3, 5, false);
        Add(0xDF, "DCP", CpuAddressingMode.AbsoluteX, 3, 7, false);
        Add(0xFF, "ISC", CpuAddressingMode.AbsoluteX, 3, 7, false);
        Add(0x1B, "SLO", CpuAddressingMode.AbsoluteY, 3, 7, false);
        Add(0x3B, "RLA", CpuAddressingMode.AbsoluteY, 3, 7, false);
        Add(0x5B, "SRE", CpuAddressingMode.AbsoluteY, 3, 7, false);
        Add(0x7B, "RRA", CpuAddressingMode.AbsoluteY, 3, 7, false);
        Add(0x9E, "SXA", CpuAddressingMode.AbsoluteY, 3, 5, false);
        Add(0xBF, "LAX", CpuAddressingMode.AbsoluteY, 3, 4, false);
        Add(0xDB, "DCP", CpuAddressingMode.AbsoluteY, 3, 7, false);
        Add(0xFB, "ISC", CpuAddressingMode.AbsoluteY, 3, 7, false);
        Add(0x03, "SLO", CpuAddressingMode.IndexedIndirect, 2, 8, false);
        Add(0x23, "RLA", CpuAddressingMode.IndexedIndirect, 2, 8, false);
        Add(0x43, "SRE", CpuAddressingMode.IndexedIndirect, 2, 8, false);
        Add(0x63, "RRA", CpuAddressingMode.IndexedIndirect, 2, 8, false);
        Add(0x83, "AAX", CpuAddressingMode.IndexedIndirect, 2, 6, false);
        Add(0xA3, "LAX", CpuAddressingMode.IndexedIndirect, 2, 6, false);
        Add(0xC3, "DCP", CpuAddressingMode.IndexedIndirect, 2, 8, false);
        Add(0xE3, "ISC", CpuAddressingMode.IndexedIndirect, 2, 8, false);
        Add(0x13, "SLO", CpuAddressingMode.IndirectIndexed, 2, 8, false);
        Add(0x33, "RLA", CpuAddressingMode.IndirectIndexed, 2, 8, false);
        Add(0x53, "SRE", CpuAddressingMode.IndirectIndexed, 2, 8, false);
        Add(0x73, "RRA", CpuAddressingMode.IndirectIndexed, 2, 8, false);
        Add(0xB3, "LAX", CpuAddressingMode.IndirectIndexed, 2, 5, false);
        Add(0xD3, "DCP", CpuAddressingMode.IndirectIndexed, 2, 8, false);
        Add(0xF3, "ISC", CpuAddressingMode.IndirectIndexed, 2, 8, false);
        return entries;
    }
}
