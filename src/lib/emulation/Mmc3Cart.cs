using System;

namespace SR.Emulation.Nes;

public sealed class Mmc3Cart : Cartridge
{
    private const int PrgBankSize = 0x2000;
    private const int ChrBankSize = 0x0400;

    private readonly InterruptLines _interrupts;
    private readonly byte[] _bankRegisters = new byte[8];
    private readonly byte[] _prgRam = new byte[0x2000];
    private readonly bool _fourScreenMirroring;
    private byte _bankSelect;
    private bool _prgRamEnabled = true;
    private bool _prgRamWriteProtected;
    private byte _irqLatch;
    private byte _irqCounter;
    private bool _irqReload;
    private bool _irqEnabled;
    private bool _a12High;
    private int _a12LowCpuClocks;

    public Mmc3Cart(
        byte[] prgRom,
        byte[] chrRom,
        NametableMirroring nametableMirroring,
        bool chrWritable,
        InterruptLines interrupts)
        : base(prgRom, chrRom, nametableMirroring, chrWritable)
    {
        if (interrupts == null) throw new ArgumentNullException(nameof(interrupts));
        if (prgRom.Length < PrgBankSize * 2 || prgRom.Length % PrgBankSize != 0)
            throw new ArgumentException("MMC3 PRG ROM must contain at least two complete 8 KB banks.", nameof(prgRom));
        if (chrRom.Length == 0 || chrRom.Length % ChrBankSize != 0)
            throw new ArgumentException("MMC3 CHR memory must contain complete 1 KB banks.", nameof(chrRom));

        _interrupts = interrupts;
        _fourScreenMirroring = nametableMirroring == NametableMirroring.FourScreen;
    }

    public override byte CpuRead(ushort address)
    {
        if (address is >= 0x6000 and <= 0x7FFF)
            return _prgRamEnabled ? _prgRam[address - 0x6000] : (byte)0;
        if (address < 0x8000) return 0;

        var slot = (address - 0x8000) / PrgBankSize;
        var bank = GetPrgBank(slot);
        return _prgRom[(bank * PrgBankSize) + (address & (PrgBankSize - 1))];
    }

    public override void CpuWrite(ushort address, byte value)
    {
        if (address is >= 0x6000 and <= 0x7FFF)
        {
            if (_prgRamEnabled && !_prgRamWriteProtected) _prgRam[address - 0x6000] = value;
            return;
        }
        if (address < 0x8000) return;

        switch (address & 0xE001)
        {
            case 0x8000: _bankSelect = value; break;
            case 0x8001: _bankRegisters[_bankSelect & 0x07] = value; break;
            case 0xA000:
                if (!_fourScreenMirroring)
                    NametableMirroring = (value & 1) == 0 ? NametableMirroring.Vertical : NametableMirroring.Horizontal;
                break;
            case 0xA001:
                _prgRamEnabled = (value & 0x80) != 0;
                _prgRamWriteProtected = (value & 0x40) != 0;
                break;
            case 0xC000: _irqLatch = value; break;
            case 0xC001: _irqCounter = 0; _irqReload = true; break;
            case 0xE000: _irqEnabled = false; _interrupts.MapperIrq = false; break;
            case 0xE001: _irqEnabled = true; break;
        }
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

    internal override void NotifyPpuAddress(ushort address, ulong ppuCycle)
    {
        var a12High = (address & 0x1000) != 0;
        if (!a12High)
        {
            if (_a12High) _a12LowCpuClocks = 0;
            _a12High = false;
            return;
        }

        if (!_a12High && _a12LowCpuClocks >= 4)
        {
            ClockIrqCounter();
        }
        _a12High = true;
    }

    internal override void NotifyCpuClock()
    {
        if (!_a12High && _a12LowCpuClocks < 4) _a12LowCpuClocks++;
    }

    internal override void Reset()
    {
        Array.Clear(_bankRegisters, 0, _bankRegisters.Length);
        _bankSelect = 0;
        _prgRamEnabled = true;
        _prgRamWriteProtected = false;
        _irqLatch = 0;
        _irqCounter = 0;
        _irqReload = false;
        _irqEnabled = false;
        _a12High = false;
        _a12LowCpuClocks = 0;
        _interrupts.MapperIrq = false;
    }

    private int GetPrgBank(int slot)
    {
        var lastBank = (_prgRom.Length / PrgBankSize) - 1;
        var secondLastBank = lastBank - 1;
        var prgMode = (_bankSelect & 0x40) != 0;
        var bank = slot switch
        {
            0 => prgMode ? secondLastBank : _bankRegisters[6] & 0x3F,
            1 => _bankRegisters[7] & 0x3F,
            2 => prgMode ? _bankRegisters[6] & 0x3F : secondLastBank,
            _ => lastBank
        };
        return bank % (lastBank + 1);
    }

    private int GetChrAddress(ushort address)
    {
        var slot = address / ChrBankSize;
        var inverted = (_bankSelect & 0x80) != 0;
        var bank = (inverted, slot) switch
        {
            (false, 0) => _bankRegisters[0] & 0xFE,
            (false, 1) => _bankRegisters[0] | 0x01,
            (false, 2) => _bankRegisters[1] & 0xFE,
            (false, 3) => _bankRegisters[1] | 0x01,
            (false, 4) => _bankRegisters[2],
            (false, 5) => _bankRegisters[3],
            (false, 6) => _bankRegisters[4],
            (false, _) => _bankRegisters[5],
            (true, 0) => _bankRegisters[2],
            (true, 1) => _bankRegisters[3],
            (true, 2) => _bankRegisters[4],
            (true, 3) => _bankRegisters[5],
            (true, 4) => _bankRegisters[0] & 0xFE,
            (true, 5) => _bankRegisters[0] | 0x01,
            (true, 6) => _bankRegisters[1] & 0xFE,
            (true, _) => _bankRegisters[1] | 0x01
        };
        bank %= _chrRom.Length / ChrBankSize;
        return (bank * ChrBankSize) + (address & (ChrBankSize - 1));
    }

    private void ClockIrqCounter()
    {
        if (_irqCounter == 0 || _irqReload)
        {
            _irqCounter = _irqLatch;
            _irqReload = false;
        }
        else
        {
            _irqCounter--;
        }

        if (_irqCounter == 0 && _irqEnabled)
        {
            _interrupts.MapperIrq = true;
        }
    }
}
