using System;

using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;
public sealed class CpuBus(Ppu ppu, Apu apu, CartridgeSlot cartridgeSlot) : IBus
{
    private readonly byte[] _ram = new byte[0x0800]; // 2KB of CPU RAM
    private readonly byte[] _controllerState = new byte[2];
    private readonly byte[] _controllerShift = new byte[2];
    private bool _controllerStrobe;
    private bool _oamDmaPending;
    private bool _oamDmaActive;
    private byte _oamDmaPage;
    private int _oamDmaIndex;
    private int _oamDmaStartupCycles;
    private bool _oamDmaReadPhase;
    private bool _oamDmaRealign;
    private byte _oamDmaLatch;
    private bool _dmcDmaPending;
    private int _dmcDmaCycles;
    private ushort _dmcDmaAddress;
    private Action<byte>? _dmcDmaCompleted;
    private ushort _lastCpuReadAddress;
    private bool _hasCpuReadAddress;
    internal Action<NesDebugBreakKind, ushort, byte>? DebugAccessed { get; set; }

    public byte Read(ushort address)
    {
        byte value = address switch
        {
            >= 0x0000 and <= 0x1FFF => _ram[address & 0x07FF], // RAM access with mirroring
            >= 0x2000 and <= 0x3FFF => ppu.Read(address),      // PPU registers, mirrored
            >= 0x4000 and <= 0x4014 => 0,                     // Write-only APU and DMA registers
            0x4015 => apu.Read(address),                       // APU status
            0x4016 => ReadController(0),                       // Controller port 1
            0x4017 => ReadController(1),                       // Controller port 2
            >= 0x4018 and <= 0x401F => 0,                      // Disabled APU/test registers
            >= 0x4020 and <= 0xFFFF => cartridgeSlot.CpuRead(address)
        };
        DebugAccessed?.Invoke(NesDebugBreakKind.CpuRead, address, value);
        _lastCpuReadAddress = address;
        _hasCpuReadAddress = true;
        return value;
    }

    public void Write(ushort address, byte value)
    {
        Action write = address switch
        {
            >= 0x0000 and <= 0x1FFF => () => _ram[address & 0x07FF] = value, // RAM access with mirroring
            >= 0x2000 and <= 0x3FFF => () => ppu.Write(address, value),      // PPU registers, mirrored
            >= 0x4000 and <= 0x4013 => () => apu.Write(address, value),
            0x4014 => () => DoDmaTransfer(value),               // OAMDMA transfer
            0x4015 => () => apu.Write(address, value),           // APU channel enables
            0x4016 => () => WriteControllerStrobe(value),
            0x4017 => () => apu.Write(address, value),           // APU frame counter
            >= 0x4018 and <= 0x401F => () => { },                // Disabled APU/test registers
            >= 0x4020 and <= 0xFFFF => () => cartridgeSlot.CpuWrite(address, value)
        };

        write();
        DebugAccessed?.Invoke(NesDebugBreakKind.CpuWrite, address, value);
    }

    private void DoDmaTransfer(byte page)
    {
        _oamDmaPage = page;
        _oamDmaPending = true;
    }

    internal void RequestDmcDma(ushort address, Action<byte> completed)
    {
        if (_dmcDmaPending) return;
        _dmcDmaPending = true;
        _dmcDmaCycles = 0;
        _dmcDmaAddress = address;
        _dmcDmaCompleted = completed;
    }

    internal bool ClockDma(ulong cpuClock)
    {
        if (_oamDmaPending)
        {
            _oamDmaPending = false;
            _oamDmaActive = true;
            _oamDmaIndex = 0;
            _oamDmaReadPhase = true;
            _oamDmaStartupCycles = (cpuClock & 1) == 0 ? 1 : 2;
        }

        if (_dmcDmaPending && _oamDmaActive)
        {
            // OAM continues through the DMC halt/dummy/alignment setup. Only
            // the DMC get displaces an OAM cycle; OAM then spends one cycle
            // realigning to a get phase (the common overlap costs two clocks).
            if (_dmcDmaCycles == 0) _dmcDmaCycles = 4;
            if (--_dmcDmaCycles > 0) return ClockOamDmaCycle();

            var value = ReadDmaSource(_dmcDmaAddress);
            var completed = _dmcDmaCompleted;
            _dmcDmaPending = false;
            _dmcDmaCompleted = null;
            _oamDmaRealign = true;
            completed?.Invoke(value);
            return true;
        }

        if (_oamDmaRealign)
        {
            _oamDmaRealign = false;
            return true;
        }

        if (_dmcDmaPending)
        {
            // Halt, dummy, optional alignment, then a get cycle. Repeating the
            // CPU's last read preserves the side effects observable at I/O.
            if (_dmcDmaCycles == 0) _dmcDmaCycles = (cpuClock & 1) == 0 ? 4 : 3;
            _dmcDmaCycles--;
            if (_dmcDmaCycles > 0)
            {
                if (_hasCpuReadAddress) _ = Read(_lastCpuReadAddress);
                return true;
            }

            var value = ReadDmaSource(_dmcDmaAddress);
            var completed = _dmcDmaCompleted;
            _dmcDmaPending = false;
            _dmcDmaCompleted = null;
            completed?.Invoke(value);
            return true;
        }

        if (!_oamDmaActive) return false;

        return ClockOamDmaCycle();
    }

    private bool ClockOamDmaCycle()
    {

        if (_oamDmaStartupCycles > 0)
        {
            _oamDmaStartupCycles--;
            if (_hasCpuReadAddress) _ = Read(_lastCpuReadAddress);
            return true;
        }

        if (_oamDmaReadPhase)
        {
            _oamDmaLatch = ReadDmaSource((ushort)((_oamDmaPage << 8) | _oamDmaIndex));
            _oamDmaReadPhase = false;
        }
        else
        {
            ppu.DmaWriteByte(_oamDmaLatch);
            _oamDmaIndex++;
            _oamDmaReadPhase = true;
            if (_oamDmaIndex == 256) _oamDmaActive = false;
        }
        return true;
    }

    private byte ReadDmaSource(ushort address) => address switch
    {
        <= 0x1FFF => _ram[address & 0x07FF],
        <= 0x3FFF => ppu.Read(address),
        <= 0x4014 => 0,
        0x4015 => apu.Read(address),
        0x4016 => ReadController(0),
        0x4017 => ReadController(1),
        <= 0x401F => 0,
        _ => cartridgeSlot.CpuRead(address)
    };

    public void SetControllerState(int controller, byte buttons)
    {
        if ((uint)controller >= _controllerState.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(controller));
        }

        _controllerState[controller] = buttons;
        if (_controllerStrobe) _controllerShift[controller] = buttons;
    }

    private byte ReadController(int controller)
    {
        var value = (byte)(_controllerShift[controller] & 0x01);
        if (!_controllerStrobe)
        {
            _controllerShift[controller] = (byte)((_controllerShift[controller] >> 1) | 0x80);
        }
        return value;
    }

    private void WriteControllerStrobe(byte value)
    {
        _controllerStrobe = (value & 0x01) != 0;
        _controllerShift[0] = _controllerState[0];
        _controllerShift[1] = _controllerState[1];
    }

    internal int RamSize => _ram.Length;
    internal void CopyRam(int offset, Span<byte> destination) => _ram.AsSpan(offset, destination.Length).CopyTo(destination);
    internal void WriteRam(int offset, ReadOnlySpan<byte> source) => source.CopyTo(_ram.AsSpan(offset, source.Length));

    internal byte Peek(ushort address) => address switch
    {
        <= 0x1FFF => _ram[address & 0x07FF],
        <= 0x3FFF => ppu.PeekRegister(address),
        <= 0x4015 => apu.Peek(address),
        0x4016 => (byte)(_controllerState[0] & 1),
        0x4017 => (byte)(_controllerState[1] & 1),
        <= 0x401F => 0,
        _ => cartridgeSlot.CpuPeek(address)
    };
}
