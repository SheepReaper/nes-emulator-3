using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>Implements the NINA-001/NINA-002 variant of iNES mapper 34.</summary>
public sealed class Nina001Cart : Cartridge
{
    private const int PrgBankSize = 0x8000;
    private const int ChrBankSize = 0x1000;
    private readonly byte[] _prgRam = new byte[0x2000];
    private int _prgBank;
    private int _chrBank0;
    private int _chrBank1 = 1;

    public Nina001Cart(byte[] prgRom, byte[] chrRom, NametableMirroring mirroring)
        : base(prgRom, chrRom, mirroring, false)
    {
        if (prgRom.Length < PrgBankSize || prgRom.Length % PrgBankSize != 0)
            throw new ArgumentException("NINA-001 PRG ROM must contain complete 32 KB banks.", nameof(prgRom));
        if (chrRom.Length > 0x1000 * 16 || chrRom.Length % ChrBankSize != 0)
            throw new ArgumentException("NINA-001 CHR ROM must contain at most sixteen complete 4 KB banks.", nameof(chrRom));
    }

    public override byte CpuRead(ushort address)
    {
        return address is >= 0x6000 and <= 0x7FFF
            ? _prgRam[address - 0x6000]
            : address >= 0x8000
            ? _prgRom[_prgBank * PrgBankSize + (address & 0x7FFF)]
            : (byte)0;
    }

    public override void CpuWrite(ushort address, byte value)
    {
        if (address is < 0x6000 or > 0x7FFF) return;
        _prgRam[address - 0x6000] = value;
        switch (address)
        {
            case 0x7FFD: _prgBank = (value & 1) % (_prgRom.Length / PrgBankSize); break;
            case 0x7FFE: _chrBank0 = (value & 0x0F) % (_chrRom.Length / ChrBankSize); break;
            case 0x7FFF: _chrBank1 = (value & 0x0F) % (_chrRom.Length / ChrBankSize); break;
        }
    }

    public override byte PpuRead(ushort address)
    {
        if (address > 0x1FFF) return 0;
        var bank = address < 0x1000 ? _chrBank0 : _chrBank1;
        return _chrRom[bank * ChrBankSize + (address & 0x0FFF)];
    }

    public override void PpuWrite(ushort address, byte value) { }

    internal override int CartridgeRamSize => _prgRam.Length;
    internal override void CopyCartridgeRam(int offset, Span<byte> destination) =>
        _prgRam.AsSpan(offset, destination.Length).CopyTo(destination);
    internal override void WriteCartridgeRam(int offset, ReadOnlySpan<byte> source) =>
        source.CopyTo(_prgRam.AsSpan(offset, source.Length));

    internal override void Reset()
    {
        _prgBank = 0;
        _chrBank0 = 0;
        _chrBank1 = 1;
    }
}