using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>Implements the MMC5/ExROM board assigned to iNES mapper 5.</summary>
public sealed class Mmc5Cart : Cartridge
{
    private const int PrgBankSize = 0x2000;
    private readonly InterruptLines _interrupts;
    private readonly Mmc5Registers _reg = new();
    private readonly Mmc5Multiplier _mul = new();
    private readonly Mmc5Ram _ram = new();

    public Mmc5Cart(
        byte[] prgRom,
        byte[] chr,
        NametableMirroring mirroring,
        bool chrWritable,
        InterruptLines interrupts)
        : base(prgRom, chr, mirroring, chrWritable)
    {
        if (prgRom.Length < 0x4000 || prgRom.Length % PrgBankSize != 0)
        {
            throw new ArgumentException("MMC5 PRG ROM must contain complete 8 KB banks.", nameof(prgRom));
        }
        if (chr.Length == 0 || chr.Length % 0x0400 != 0)
        {
            throw new ArgumentException("MMC5 CHR memory must contain complete 1 KB banks.", nameof(chr));
        }

        _interrupts = interrupts ?? throw new ArgumentNullException(nameof(interrupts));
        _reg.PrgBanks[4] = 0xFF;
    }

    public override byte CpuRead(ushort address)
    {
        if (address is >= 0x5C00 and <= 0x5FFF) return _ram.ReadExRam(address);
        if (address == 0x5205) return _mul.ReadLow();
        if (address == 0x5206) return _mul.ReadHigh();
        if (address is >= 0x6000 and <= 0x7FFF) return _ram.ReadBank(_reg.PrgBanks[0] & 0x07, address);
        if (address < 0x8000) return 0;

        var (bank, isRom) = Mmc5BankResolver.GetPrgMapping(address, _reg.PrgMode, _reg.PrgBanks);
        return isRom
            ? _prgRom[bank % (_prgRom.Length / PrgBankSize) * PrgBankSize + (address & 0x1FFF)]
            : _ram.ReadBank(bank & 0x07, address);
    }

    public override void CpuWrite(ushort address, byte value)
    {
        if (address is >= 0x5C00 and <= 0x5FFF)
        {
            if (_reg.ExRamMode != 3) _ram.WriteExRam(address, value);
            return;
        }
        if (address is >= 0x6000 and <= 0x7FFF)
        {
            if (_reg.PrgRamWritable) _ram.WriteBank(_reg.PrgBanks[0] & 0x07, address, value);
            return;
        }
        if (address >= 0x8000)
        {
            var (bank, isRom) = Mmc5BankResolver.GetPrgMapping(address, _reg.PrgMode, _reg.PrgBanks);
            if (!isRom && _reg.PrgRamWritable) _ram.WriteBank(bank & 0x07, address, value);
            return;
        }

        if (address == 0x5205) _mul.WriteMultiplicand(value);
        else if (address == 0x5206) _mul.WriteMultiplier(value);
        else _reg.Write(address, value);
    }

    public override byte PpuRead(ushort address) => address > 0x1FFF ? (byte)0
        : _chrRom[Mmc5BankResolver.GetChrAddress(address, _reg.ChrMode, _reg.UseBackgroundChrBanks, _reg.BackgroundChrBanks, _reg.SpriteChrBanks) % _chrRom.Length];

    public override void PpuWrite(ushort address, byte value)
    {
        if (address <= 0x1FFF && IsChrWritable)
        {
            var chr = Mmc5BankResolver.GetChrAddress(address, _reg.ChrMode, _reg.UseBackgroundChrBanks, _reg.BackgroundChrBanks, _reg.SpriteChrBanks);
            _chrRom[chr % _chrRom.Length] = value;
        }
    }

    internal override bool TryReadNametable(ushort address, Span<byte> ciram, out byte value) =>
        Mmc5NametableHandler.TryRead(address, ciram, _reg.NametableMapping, _reg.ExRamMode, _ram.ExRam, _reg.FillTile, _reg.FillAttribute, out value);

    internal override bool TryWriteNametable(ushort address, byte value, Span<byte> ciram) =>
        Mmc5NametableHandler.TryWrite(address, value, ciram, _reg.NametableMapping, _reg.ExRamMode, _ram.ExRam);

    internal override int CartridgeRamSize => _ram.CartridgeRamSize;
    internal override void CopyCartridgeRam(int offset, Span<byte> destination) => _ram.CopyCartridgeRam(offset, destination);
    internal override void WriteCartridgeRam(int offset, ReadOnlySpan<byte> source) => _ram.WriteCartridgeRam(offset, source);

    internal override void Reset()
    {
        _reg.Reset();
        _mul.Reset();
        _interrupts.MapperIrq = false;
    }
}