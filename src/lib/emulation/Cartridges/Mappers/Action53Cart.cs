using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>Implements the Action 53 multicart board assigned to iNES mapper 28.</summary>
public sealed class Action53Cart : Cartridge
{
    private const int PrgBankSize = 0x4000;
    private const int ChrBankSize = 0x2000;
    private byte _selectedRegister;
    private byte _chrBank;
    private byte _innerBank;
    private byte _mode;
    private byte _outerBank;

    public Action53Cart(byte[] prgRom, byte[] chrRam)
        : base(prgRom, chrRam, NametableMirroring.SingleScreenLower, true)
    {
        if (prgRom.Length < PrgBankSize * 2 || prgRom.Length % PrgBankSize != 0)
            throw new ArgumentException("Action 53 PRG ROM must contain complete 16 KB banks.", nameof(prgRom));
        if (chrRam.Length != ChrBankSize * 4)
            throw new ArgumentException("Action 53 requires 32 KB of CHR RAM.", nameof(chrRam));

        _mode = 0x0C;
        _outerBank = (byte)((prgRom.Length / PrgBankSize - 1) >> 1);
        UpdateMirroring();
    }

    public override byte CpuRead(ushort address)
    {
        if (address < 0x8000) return 0;
        var bank = GetPrgBank(address);
        return _prgRom[bank * PrgBankSize + (address & 0x3FFF)];
    }

    internal override bool CpuReadDrivesDataBus(ushort address) => address >= 0x8000;

    public override void CpuWrite(ushort address, byte value)
    {
        if (address is >= 0x5000 and <= 0x5FFF)
        {
            _selectedRegister = (byte)(value & 0x81);
            return;
        }
        if (address < 0x8000) return;

        switch (_selectedRegister)
        {
            case 0x00: _chrBank = (byte)(value & 0x03); UpdateOneScreenMirroring(value); break;
            case 0x01: _innerBank = (byte)(value & 0x0F); UpdateOneScreenMirroring(value); break;
            case 0x80: _mode = (byte)(value & 0x3F); UpdateMirroring(); break;
            case 0x81: _outerBank = value; break;
        }
    }

    public override byte PpuRead(ushort address) => address <= 0x1FFF
        ? _chrRom[_chrBank * ChrBankSize + address]
        : (byte)0;

    public override void PpuWrite(ushort address, byte value)
    {
        if (address <= 0x1FFF) _chrRom[_chrBank * ChrBankSize + address] = value;
    }

    private int GetPrgBank(ushort address)
    {
        var cpuA14 = (address >> 14) & 1;
        var prgMode = (_mode >> 2) & 3;
        var outerSize = (_mode >> 4) & 3;
        var outer = _outerBank << 1;
        var fixedBank = prgMode == 2 ? cpuA14 == 0 : prgMode == 3 && cpuA14 != 0;
        int bank;
        if (fixedBank)
        {
            bank = outer | cpuA14;
        }
        else
        {
            var current = prgMode <= 1 ? (_innerBank << 1) | cpuA14 : _innerBank;
            var mask = (2 << outerSize) - 1;
            bank = outer ^ ((outer ^ current) & mask);
        }
        return bank % (_prgRom.Length / PrgBankSize);
    }

    private void UpdateOneScreenMirroring(byte value)
    {
        if ((_mode & 2) == 0)
        {
            _mode = (byte)((_mode & ~1) | ((value >> 4) & 1));
            UpdateMirroring();
        }
    }

    private void UpdateMirroring()
    {
        NametableMirroring = (_mode & 3) switch
        {
            0 => NametableMirroring.SingleScreenLower,
            1 => NametableMirroring.SingleScreenUpper,
            2 => NametableMirroring.Vertical,
            _ => NametableMirroring.Horizontal
        };
    }
}
