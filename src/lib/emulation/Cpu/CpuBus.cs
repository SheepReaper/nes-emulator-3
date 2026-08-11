using System;
using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes.Cpu;

public sealed class CpuBus(Ppu ppu, Apu apu, CartridgeSlot cartridgeSlot) : IBus
{
    private readonly byte[] _ram = new byte[0x0800];
    private readonly CpuControllerPorts _controllers = new();
    private readonly CpuDmaController _dma = new();
    private byte _openBus;
    private byte _internalBus;
    private ushort _lastCpuReadAddress;
    private bool _hasCpuReadAddress;
    private Cpu? _cpu;

    internal Action<NesDebugBreakKind, ushort, byte>? DebugAccessed { get; set; }
    internal Action<NesDebugBreakKind, ushort, byte>? DebugObserved { get; set; }
    internal bool HasCpuReadAddress => _hasCpuReadAddress;
    internal ushort LastCpuReadAddress => _lastCpuReadAddress;

    internal void ConnectCpu(Cpu cpu) => _cpu = cpu;
    internal void RecordCpuReadAddress(ushort address)
    {
        _lastCpuReadAddress = address;
        _hasCpuReadAddress = true;
    }

    internal ushort GetDmcDummyReadAddress() =>
        _cpu != null ? _cpu.PendingBusAddress : (_hasCpuReadAddress ? _lastCpuReadAddress : (ushort)0);

    public byte Read(ushort address)
    {
        byte value = address switch
        {
            <= 0x1FFF => _ram[address & 0x07FF],
            <= 0x3FFF => ppu.Read(address),
            <= 0x4014 => _openBus,
            0x4015 => (byte)(apu.Read(address) | (_internalBus & 0x20)),
            0x4016 => _controllers.ReadController(0, _openBus),
            0x4017 => _controllers.ReadController(1, _openBus),
            <= 0x401F => _openBus,
            _ => cartridgeSlot.CpuReadOrOpenBus(address, _openBus)
        };
        DebugAccessed?.Invoke(NesDebugBreakKind.CpuRead, address, value);
        DebugObserved?.Invoke(NesDebugBreakKind.CpuRead, address, value);
        if (address != 0x4015) _openBus = value;
        _internalBus = value;
        _lastCpuReadAddress = address;
        _hasCpuReadAddress = true;
        return value;
    }

    public void Write(ushort address, byte value)
    {
        _dma.NotifyWriteCycle();
        _openBus = value;
        _internalBus = value;
        switch (address)
        {
            case <= 0x1FFF: _ram[address & 0x07FF] = value; break;
            case <= 0x3FFF: ppu.Write(address, value); break;
            case <= 0x4013: apu.Write(address, value); break;
            case 0x4014: _dma.TriggerOamDma(value); break;
            case 0x4015: apu.Write(address, value); break;
            case 0x4016: _controllers.WriteStrobe(value, apu); break;
            case 0x4017: apu.Write(address, value); break;
            case <= 0x401F: break;
            default: cartridgeSlot.CpuWrite(address, value); break;
        }
        DebugAccessed?.Invoke(NesDebugBreakKind.CpuWrite, address, value);
        DebugObserved?.Invoke(NesDebugBreakKind.CpuWrite, address, value);
    }

    internal void RequestDmcDma(ushort address, Action<byte>? completed) => _dma.RequestDmcDma(address, completed);
    internal void AbortDmcDma() => _dma.AbortDmcDma();

    internal bool ClockDma(ulong cpuClock, ushort? cpuReadAddress = null) => ClockDma(cpuClock, cpuReadAddress, out _);

    internal bool ClockDma(ulong cpuClock, ushort? cpuReadAddress, out NesCpuClockActor actor) =>
        _dma.ClockDma(cpuClock, cpuReadAddress, this, ppu, out actor);
    internal void NotifyWriteCycle() => _dma.NotifyWriteCycle();

    internal byte ReadDmaSource(ushort address) =>
        CpuDmaReader.ReadDmaSource(address, _ram, ppu, apu, cartridgeSlot, _controllers, ref _openBus, _internalBus);

    internal byte ReadOamDmaSource(ushort address) =>
        CpuDmaReader.ReadOamDmaSource(address, _ram, ppu, apu, cartridgeSlot, _controllers, ref _openBus, _hasCpuReadAddress, _lastCpuReadAddress);

    public void SetControllerState(int controller, byte buttons) => _controllers.SetControllerState(controller, buttons);

    internal int RamSize => _ram.Length;
    internal void CopyRam(int offset, Span<byte> destination) => _ram.AsSpan(offset, destination.Length).CopyTo(destination);
    internal void WriteRam(int offset, ReadOnlySpan<byte> source) => source.CopyTo(_ram.AsSpan(offset, source.Length));

    internal NesCpuBusDebugState CaptureDebugState() => new(
        _openBus, _internalBus, _lastCpuReadAddress, _hasCpuReadAddress,
        _dma.OamDmaPending, _dma.OamDmaActive, _dma.OamDmaIndex, _dma.OamDmaReadPhase,
        _dma.DmcDmaPending, _dma.DmcDmaCycles, _dma.DmcDmaAddress);

    internal byte Peek(ushort address) => address switch
    {
        <= 0x1FFF => _ram[address & 0x07FF],
        <= 0x3FFF => ppu.PeekRegister(address),
        <= 0x4015 => apu.Peek(address),
        0x4016 => _controllers.Peek(0),
        0x4017 => _controllers.Peek(1),
        <= 0x401F => 0,
        _ => cartridgeSlot.CpuPeek(address)
    };
}
