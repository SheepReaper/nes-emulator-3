using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

public sealed class Mmc3Cart : Cartridge
{
    private const int PrgBankSize = 0x2000;
    private const int ChrBankSize = 0x0400;

    private readonly Mmc3IrqTracker _irqTracker;
    private readonly Mmc3Registers _reg = new();
    private readonly CartridgeRam8K _prgRam = new();
    private readonly bool _fourScreenMirroring;

    public Mmc3Cart(
        byte[] prgRom,
        byte[] chrRom,
        NametableMirroring nametableMirroring,
        bool chrWritable,
        InterruptLines interrupts)
        : base(prgRom, chrRom, nametableMirroring, chrWritable)
    {
        if (prgRom.Length < PrgBankSize * 2 || prgRom.Length % PrgBankSize != 0)
        {
            throw new ArgumentException("MMC3 PRG ROM must contain at least two complete 8 KB banks.", nameof(prgRom));
        }
        if (chrRom.Length == 0 || chrRom.Length % ChrBankSize != 0)
        {
            throw new ArgumentException("MMC3 CHR memory must contain complete 1 KB banks.", nameof(chrRom));
        }

        _irqTracker = new Mmc3IrqTracker(interrupts ?? throw new ArgumentNullException(nameof(interrupts)));
        _fourScreenMirroring = nametableMirroring == NametableMirroring.FourScreen;
    }

    public override byte CpuRead(ushort address)
    {
        if (address is >= 0x6000 and <= 0x7FFF) return _prgRam.Read(address);
        if (address < 0x8000) return 0;

        var slot = (address - 0x8000) / PrgBankSize;
        var bank = Mmc3BankResolver.GetPrgBank(slot, _prgRom.Length, _reg.BankSelect, _reg.BankRegisters);
        return _prgRom[(bank * PrgBankSize) + (address & (PrgBankSize - 1))];
    }

    public override void CpuWrite(ushort address, byte value)
    {
        if (address is >= 0x6000 and <= 0x7FFF)
        {
            _prgRam.Write(address, value);
            return;
        }
        if (address < 0x8000) return;

        switch (address & 0xE001)
        {
            case 0x8000: _reg.WriteBankSelect(value); break;
            case 0x8001: _reg.WriteBankData(value); break;
            case 0xA000 when !_fourScreenMirroring:
                NametableMirroring = (value & 1) == 0 ? NametableMirroring.Vertical : NametableMirroring.Horizontal;
                break;
            case 0xA001:
                _prgRam.Enabled = (value & 0x80) != 0;
                _prgRam.WriteProtected = (value & 0x40) != 0;
                break;
            case 0xC000: _irqTracker.SetLatch(value); break;
            case 0xC001: _irqTracker.Reload(); break;
            case 0xE000: _irqTracker.Disable(); break;
            case 0xE001: _irqTracker.Enable(); break;
        }
    }

    public override byte PpuRead(ushort address) => address > 0x1FFF
        ? (byte)0
        : _chrRom[Mmc3BankResolver.GetChrAddress(address, _chrRom.Length, _reg.BankSelect, _reg.BankRegisters)];

    public override void PpuWrite(ushort address, byte value)
    {
        if (address <= 0x1FFF && IsChrWritable)
        {
            _chrRom[Mmc3BankResolver.GetChrAddress(address, _chrRom.Length, _reg.BankSelect, _reg.BankRegisters)] = value;
        }
    }

    internal override int CartridgeRamSize => _prgRam.Size;
    internal override void CopyCartridgeRam(int offset, Span<byte> destination) => _prgRam.CopyTo(offset, destination);
    internal override void WriteCartridgeRam(int offset, ReadOnlySpan<byte> source) => _prgRam.WriteFrom(offset, source);

    internal override void NotifyPpuAddress(ushort address, ulong ppuCycle) => _irqTracker.NotifyPpuAddress(address);
    internal override void NotifyCpuClock() => _irqTracker.NotifyCpuClock();

    internal override void Reset()
    {
        _reg.Reset();
        _prgRam.Reset();
        _irqTracker.Reset();
    }
}