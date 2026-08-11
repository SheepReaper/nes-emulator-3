using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>Implements iNES mapper 11 (Color Dreams).</summary>
public sealed class ColorDreamsCart : Cartridge
{
    private const int PrgBankSize = 0x8000;
    private const int ChrBankSize = 0x2000;
    private int _prgBank;
    private int _chrBank;

    public ColorDreamsCart(byte[] prgRom, byte[] chrRom, NametableMirroring mirroring)
        : base(prgRom, chrRom, mirroring, false)
    {
        if (prgRom.Length < PrgBankSize || prgRom.Length % PrgBankSize != 0)
            throw new ArgumentException("Color Dreams PRG ROM must contain complete 32 KB banks.", nameof(prgRom));
        if (chrRom.Length < ChrBankSize || chrRom.Length % ChrBankSize != 0)
            throw new ArgumentException("Color Dreams CHR ROM must contain complete 8 KB banks.", nameof(chrRom));
    }

    public override byte CpuRead(ushort address) => address >= 0x8000
        ? _prgRom[_prgBank * PrgBankSize + (address & 0x7FFF)]
        : (byte)0;

    internal override bool CpuReadDrivesDataBus(ushort address) => address >= 0x8000;

    public override void CpuWrite(ushort address, byte value)
    {
        if (address < 0x8000) return;
        var latched = value & CpuRead(address);
        _prgBank = (latched & 0x03) % (_prgRom.Length / PrgBankSize);
        _chrBank = ((latched >> 4) & 0x0F) % (_chrRom.Length / ChrBankSize);
    }

    public override byte PpuRead(ushort address) =>
        address <= 0x1FFF ? _chrRom[_chrBank * ChrBankSize + address] : (byte)0;

    public override void PpuWrite(ushort address, byte value) { }

    internal override void Reset() => (_prgBank, _chrBank) = (0, 0);
}
