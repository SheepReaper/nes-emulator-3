using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>Implements the VRC2a board assigned to iNES mapper 22.</summary>
public sealed class Vrc2aCart : Cartridge
{
    private const int PrgBankSize = 0x2000;
    private const int ChrBankSize = 0x0400;
    private readonly byte[] _chrRegisters = new byte[8];
    private int _prgBank0;
    private int _prgBank1;
    private byte _latch;

    public Vrc2aCart(byte[] prgRom, byte[] chrRom, NametableMirroring mirroring)
        : base(prgRom, chrRom, mirroring, false)
    {
        if (prgRom.Length < PrgBankSize * 2 || prgRom.Length % PrgBankSize != 0)
            throw new ArgumentException("VRC2a PRG ROM must contain complete 8 KB banks.", nameof(prgRom));
        if (chrRom.Length < ChrBankSize * 8 || chrRom.Length % ChrBankSize != 0)
            throw new ArgumentException("VRC2a CHR ROM must contain complete 1 KB banks.", nameof(chrRom));
    }

    public override byte CpuRead(ushort address)
    {
        if (address is >= 0x6000 and <= 0x6FFF) return _latch;
        if (address < 0x8000) return 0;

        var bankCount = _prgRom.Length / PrgBankSize;
        var bank = address switch
        {
            < 0xA000 => _prgBank0,
            < 0xC000 => _prgBank1,
            < 0xE000 => bankCount - 2,
            _ => bankCount - 1
        };
        return _prgRom[bank * PrgBankSize + (address & 0x1FFF)];
    }

    internal override bool CpuReadDrivesDataBus(ushort address) =>
        address is >= 0x6000 and <= 0x6FFF or >= 0x8000;

    public override void CpuWrite(ushort address, byte value)
    {
        if (address is >= 0x6000 and <= 0x6FFF)
        {
            _latch = (byte)(value & 1);
            return;
        }

        var bankCount = _prgRom.Length / PrgBankSize;
        switch (address & 0xF000)
        {
            case 0x8000: _prgBank0 = value % bankCount; break;
            case 0x9000:
                NametableMirroring = (value & 1) == 0
                    ? NametableMirroring.Vertical
                    : NametableMirroring.Horizontal;
                break;
            case 0xA000: _prgBank1 = value % bankCount; break;
            case >= 0xB000 and <= 0xE000: WriteChrRegister(address, value); break;
        }
    }

    public override byte PpuRead(ushort address)
    {
        if (address > 0x1FFF) return 0;
        var slot = address / ChrBankSize;
        var bank = (_chrRegisters[slot] >> 1) % (_chrRom.Length / ChrBankSize);
        return _chrRom[bank * ChrBankSize + (address & 0x03FF)];
    }

    public override void PpuWrite(ushort address, byte value) { }

    internal override void Reset()
    {
        _prgBank0 = 0;
        _prgBank1 = 0;
        _latch = 0;
        Array.Clear(_chrRegisters, 0, _chrRegisters.Length);
    }

    private void WriteChrRegister(ushort address, byte value)
    {
        var slot = ((address >> 12) - 0x0B) * 2 + (address & 1);
        if ((address & 2) == 0)
            _chrRegisters[slot] = (byte)((_chrRegisters[slot] & 0xF0) | (value & 0x0F));
        else
            _chrRegisters[slot] = (byte)((_chrRegisters[slot] & 0x0F) | ((value & 0x0F) << 4));
    }
}
