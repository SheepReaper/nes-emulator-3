using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>Implements iNES mapper 7 (AxROM).</summary>
public sealed class AxromCart : Cartridge
{
    private const int PrgBankSize = 0x8000;
    private int _prgBank;

    public AxromCart(byte[] prgRom, byte[] chrRam)
        : base(prgRom, chrRam, NametableMirroring.SingleScreenLower, true)
    {
        if (prgRom.Length < 0x4000 || prgRom.Length % 0x4000 != 0)
            throw new ArgumentException("AxROM PRG ROM must contain complete 16 KB mirrorable banks.", nameof(prgRom));
    }

    public override byte CpuRead(ushort address) => address >= 0x8000
        ? _prgRom[(_prgBank * PrgBankSize + (address & 0x7FFF)) % _prgRom.Length]
        : (byte)0;

    internal override bool CpuReadDrivesDataBus(ushort address) => address >= 0x8000;

    public override void CpuWrite(ushort address, byte value)
    {
        if (address < 0x8000) return;
        _prgBank = (value & 0x07) % Math.Max(1, _prgRom.Length / PrgBankSize);
        NametableMirroring = (value & 0x10) == 0
            ? NametableMirroring.SingleScreenLower
            : NametableMirroring.SingleScreenUpper;
    }

    public override byte PpuRead(ushort address) => address <= 0x1FFF ? _chrRom[address] : (byte)0;

    public override void PpuWrite(ushort address, byte value)
    {
        if (address <= 0x1FFF) _chrRom[address] = value;
    }

    internal override void Reset()
    {
        _prgBank = 0;
        NametableMirroring = NametableMirroring.SingleScreenLower;
    }
}
