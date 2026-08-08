using System;

using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;
public sealed class CpuBus(Cpu cpu, Ppu ppu, Apu apu, CartridgeSlot cartridgeSlot) : IBus
{
    private readonly byte[] _ram = new byte[0x0800]; // 2KB of CPU RAM
    private readonly byte[] _controllerState = new byte[2];
    private readonly byte[] _controllerShift = new byte[2];
    private bool _controllerStrobe;
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
        // DMA transfer stalls the CPU.
        // 513 cycles for read/write, +1 if on an odd CPU clock cycle.
        cpu.Stall(513);
        if (cpu.IsOnOddCycle()) cpu.Stall(1);

        var startAddress = (ushort)(page << 8);
        Span<byte> data = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            data[i] = Read((ushort)(startAddress + i));
        }
        ppu.DmaTransfer(data);
    }

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
