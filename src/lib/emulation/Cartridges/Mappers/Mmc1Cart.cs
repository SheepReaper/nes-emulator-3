using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

public sealed class Mmc1Cart : Cartridge
{
    private const int PrgBankSize = 0x4000;
    private const int ChrBankSize = 0x1000;

    private readonly Mmc1ShiftRegister _registers = new();
    private readonly byte[] _prgRam = new byte[0x2000];
    private readonly bool _fourScreenMirroring;

    public Mmc1Cart(
        byte[] prgRom,
        byte[] chrRom,
        NametableMirroring nametableMirroring,
        bool chrWritable)
        : base(prgRom, chrRom, nametableMirroring, chrWritable)
    {
        if (prgRom.Length < PrgBankSize || prgRom.Length % PrgBankSize != 0)
        {
            throw new ArgumentException("MMC1 PRG ROM must contain complete 16 KB banks.", nameof(prgRom));
        }
        if (chrRom.Length == 0 || chrRom.Length % ChrBankSize != 0)
        {
            throw new ArgumentException("MMC1 CHR memory must contain complete 4 KB banks.", nameof(chrRom));
        }

        _fourScreenMirroring = nametableMirroring == NametableMirroring.FourScreen;
        UpdateMirroring();
    }

    public override byte CpuRead(ushort address)
    {
        if (address is >= 0x6000 and <= 0x7FFF)
        {
            return (_registers.PrgBank & 0x10) == 0 ? _prgRam[address - 0x6000] : (byte)0;
        }
        if (address < 0x8000)
        {
            return 0;
        }

        var bank = Mmc1BankResolver.GetPrgBank(address, _prgRom.Length, _registers.Control, _registers.PrgBank, _registers.ChrBank0);
        return _prgRom[(bank * PrgBankSize) + (address & (PrgBankSize - 1))];
    }

    public override void CpuWrite(ushort address, byte value)
    {
        if (address is >= 0x6000 and <= 0x7FFF)
        {
            if ((_registers.PrgBank & 0x10) == 0)
            {
                _prgRam[address - 0x6000] = value;
            }
            return;
        }
        if (address >= 0x8000 && _registers.Write(address, value, out var controlChanged) && controlChanged)
        {
            UpdateMirroring();
        }
    }

    public override byte PpuRead(ushort address) => address > 0x1FFF ? (byte)0
        : _chrRom[Mmc1BankResolver.GetChrAddress(address, _chrRom.Length, _registers.Control, _registers.ChrBank0, _registers.ChrBank1)];

    public override void PpuWrite(ushort address, byte value)
    {
        if (address <= 0x1FFF && IsChrWritable)
        {
            _chrRom[Mmc1BankResolver.GetChrAddress(address, _chrRom.Length, _registers.Control, _registers.ChrBank0, _registers.ChrBank1)] = value;
        }
    }

    internal override int CartridgeRamSize => _prgRam.Length;
    internal override void CopyCartridgeRam(int offset, Span<byte> destination) =>
        _prgRam.AsSpan(offset, destination.Length).CopyTo(destination);
    internal override void WriteCartridgeRam(int offset, ReadOnlySpan<byte> source) =>
        source.CopyTo(_prgRam.AsSpan(offset, source.Length));

    internal override void Reset()
    {
        _registers.Reset();
        UpdateMirroring();
    }

    private void UpdateMirroring()
    {
        if (_fourScreenMirroring)
        {
            return;
        }
        NametableMirroring = (_registers.Control & 0x03) switch
        {
            0 => NametableMirroring.SingleScreenLower,
            1 => NametableMirroring.SingleScreenUpper,
            2 => NametableMirroring.Vertical,
            _ => NametableMirroring.Horizontal
        };
    }
}