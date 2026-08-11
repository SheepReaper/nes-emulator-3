using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>Implements the BNROM variant of iNES mapper 34.</summary>
public sealed class BnromCart : Cartridge
{
    private const int PrgBankSize = 0x8000;
    private int _prgBank;

    public BnromCart(byte[] prgRom, byte[] chr, NametableMirroring mirroring, bool chrWritable)
        : base(prgRom, chr, mirroring, chrWritable)
    {
        if (prgRom.Length < PrgBankSize || prgRom.Length % PrgBankSize != 0)
            throw new ArgumentException("BNROM PRG ROM must contain complete 32 KB banks.", nameof(prgRom));
    }

    public override byte CpuRead(ushort address) => address >= 0x8000
        ? _prgRom[_prgBank * PrgBankSize + (address & 0x7FFF)]
        : (byte)0;

    internal override bool CpuReadDrivesDataBus(ushort address) => address >= 0x8000;

    public override void CpuWrite(ushort address, byte value)
    {
        if (address >= 0x8000)
            _prgBank = (value & CpuRead(address)) % (_prgRom.Length / PrgBankSize);
    }

    public override byte PpuRead(ushort address) => address <= 0x1FFF ? _chrRom[address] : (byte)0;

    public override void PpuWrite(ushort address, byte value)
    {
        if (IsChrWritable && address <= 0x1FFF) _chrRom[address] = value;
    }

    internal override void Reset() => _prgBank = 0;
}
