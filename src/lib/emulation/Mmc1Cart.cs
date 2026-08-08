using System;

namespace SR.Emulation.Nes;

public sealed class Mmc1Cart : Cartridge
{
    private const int PrgBankSize = 0x4000;
    private const int ChrBankSize = 0x1000;

    private readonly byte[] _prgRam = new byte[0x2000];
    private readonly bool _fourScreenMirroring;
    private byte _shiftRegister = 0x10;
    private byte _control = 0x0C;
    private byte _chrBank0;
    private byte _chrBank1;
    private byte _prgBank;

    public Mmc1Cart(
        byte[] prgRom,
        byte[] chrRom,
        NametableMirroring nametableMirroring,
        bool chrWritable)
        : base(prgRom, chrRom, nametableMirroring, chrWritable)
    {
        if (prgRom.Length < PrgBankSize * 2 || prgRom.Length % PrgBankSize != 0)
            throw new ArgumentException("MMC1 PRG ROM must contain at least two complete 16 KB banks.", nameof(prgRom));
        if (chrRom.Length == 0 || chrRom.Length % ChrBankSize != 0)
            throw new ArgumentException("MMC1 CHR memory must contain complete 4 KB banks.", nameof(chrRom));

        _fourScreenMirroring = nametableMirroring == NametableMirroring.FourScreen;
        UpdateMirroring();
    }

    public override byte CpuRead(ushort address)
    {
        if (address is >= 0x6000 and <= 0x7FFF)
            return IsPrgRamEnabled ? _prgRam[address - 0x6000] : (byte)0;
        if (address < 0x8000) return 0;

        var bank = GetPrgBank(address);
        return _prgRom[(bank * PrgBankSize) + (address & (PrgBankSize - 1))];
    }

    public override void CpuWrite(ushort address, byte value)
    {
        if (address is >= 0x6000 and <= 0x7FFF)
        {
            if (IsPrgRamEnabled) _prgRam[address - 0x6000] = value;
            return;
        }
        if (address < 0x8000) return;

        if ((value & 0x80) != 0)
        {
            _shiftRegister = 0x10;
            _control |= 0x0C;
            return;
        }

        var completesWrite = (_shiftRegister & 1) != 0;
        _shiftRegister = (byte)((_shiftRegister >> 1) | ((value & 1) << 4));
        if (!completesWrite) return;

        switch (address & 0xE000)
        {
            case 0x8000: _control = _shiftRegister; UpdateMirroring(); break;
            case 0xA000: _chrBank0 = _shiftRegister; break;
            case 0xC000: _chrBank1 = _shiftRegister; break;
            case 0xE000: _prgBank = _shiftRegister; break;
        }
        _shiftRegister = 0x10;
    }

    public override byte PpuRead(ushort address)
    {
        if (address > 0x1FFF) return 0;
        return _chrRom[GetChrAddress(address)];
    }

    public override void PpuWrite(ushort address, byte value)
    {
        if (address <= 0x1FFF && IsChrWritable) _chrRom[GetChrAddress(address)] = value;
    }

    internal override int CartridgeRamSize => _prgRam.Length;
    internal override void CopyCartridgeRam(int offset, Span<byte> destination) =>
        _prgRam.AsSpan(offset, destination.Length).CopyTo(destination);
    internal override void WriteCartridgeRam(int offset, ReadOnlySpan<byte> source) =>
        source.CopyTo(_prgRam.AsSpan(offset, source.Length));

    internal override void Reset()
    {
        _shiftRegister = 0x10;
        _control = 0x0C;
        _chrBank0 = 0;
        _chrBank1 = 0;
        _prgBank = 0;
        UpdateMirroring();
    }

    private bool IsPrgRamEnabled => (_prgBank & 0x10) == 0;

    private int GetPrgBank(ushort address)
    {
        var totalBanks = _prgRom.Length / PrgBankSize;
        var outerBank = totalBanks > 16 && (_chrBank0 & 0x10) != 0 ? 16 : 0;
        var banksInBlock = Math.Min(16, totalBanks - outerBank);
        var selectedBank = outerBank + ((_prgBank & 0x0F) % banksInBlock);
        var firstBank = outerBank;
        var lastBank = outerBank + banksInBlock - 1;
        var mode = (_control >> 2) & 0x03;

        if (mode <= 1)
        {
            var lowerBank = outerBank + ((_prgBank & 0x0E) % banksInBlock);
            return address < 0xC000 ? lowerBank : Math.Min(lowerBank + 1, lastBank);
        }
        if (mode == 2) return address < 0xC000 ? firstBank : selectedBank;
        return address < 0xC000 ? selectedBank : lastBank;
    }

    private int GetChrAddress(ushort address)
    {
        var totalBanks = _chrRom.Length / ChrBankSize;
        int bank;
        if ((_control & 0x10) == 0)
        {
            var lowerBank = (_chrBank0 & 0x1E) % totalBanks;
            bank = address < 0x1000 ? lowerBank : (lowerBank + 1) % totalBanks;
        }
        else
        {
            bank = (address < 0x1000 ? _chrBank0 : _chrBank1) % totalBanks;
        }
        return (bank * ChrBankSize) + (address & (ChrBankSize - 1));
    }

    private void UpdateMirroring()
    {
        if (_fourScreenMirroring) return;
        NametableMirroring = (_control & 0x03) switch
        {
            0 => NametableMirroring.SingleScreenLower,
            1 => NametableMirroring.SingleScreenUpper,
            2 => NametableMirroring.Vertical,
            _ => NametableMirroring.Horizontal
        };
    }
}
