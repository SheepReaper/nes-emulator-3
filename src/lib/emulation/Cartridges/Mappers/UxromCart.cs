using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>Implements iNES mapper 2 (UxROM).</summary>
public sealed class UxromCart : Cartridge
{
    private const int PrgBankSize = 0x4000;
    private int _selectedBank;

    public UxromCart(byte[] prgRom, byte[] chr, NametableMirroring mirroring, bool chrWritable)
        : base(prgRom, chr, mirroring, chrWritable)
    {
        if (prgRom.Length < PrgBankSize * 2 || prgRom.Length % PrgBankSize != 0)
            throw new ArgumentException("UxROM PRG ROM must contain at least two complete 16 KB banks.", nameof(prgRom));
    }

    public override byte CpuRead(ushort address)
    {
        if (address < 0x8000) return 0;
        var bank = address < 0xC000 ? _selectedBank : (_prgRom.Length / PrgBankSize) - 1;
        return _prgRom[bank * PrgBankSize + (address & 0x3FFF)];
    }

    internal override bool CpuReadDrivesDataBus(ushort address) => address >= 0x8000;

    public override void CpuWrite(ushort address, byte value)
    {
        if (address >= 0x8000)
            _selectedBank = (value & CpuRead(address)) % (_prgRom.Length / PrgBankSize);
    }

    public override byte PpuRead(ushort address) => address <= 0x1FFF ? _chrRom[address] : (byte)0;

    public override void PpuWrite(ushort address, byte value)
    {
        if (address <= 0x1FFF && IsChrWritable) _chrRom[address] = value;
    }

    internal override void Reset() => _selectedBank = 0;
}
