using System;

using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;
public sealed class CpuBus(Cpu cpu, Ppu ppu, Apu apu, CartridgeSlot cartridgeSlot) : IBus
{
    private readonly byte[] _ram = new byte[0x0800]; // 2KB of CPU RAM

    public byte Read(ushort address)
    {
        return address switch
        {
            >= 0x0000 and <= 0x1FFF => _ram[address & 0x07FF], // RAM access with mirroring
            >= 0x2000 and <= 0x3FFF => ppu.Read(address),      // PPU registers, mirrored
            >= 0x8000 and <= 0xFFFF => cartridgeSlot.CpuRead(address),
            _ => 0
        };
    }

    public void Write(ushort address, byte value)
    {
        Action write = address switch
        {
            >= 0x0000 and <= 0x1FFF => () => _ram[address & 0x07FF] = value, // RAM access with mirroring
            >= 0x2000 and <= 0x3FFF => () => ppu.Write(address, value),      // PPU registers, mirrored
            0x4014 => () => DoDmaTransfer(value), // OAMDMA transfer
            >= 0x8000 and <= 0xFFFF => () => cartridgeSlot.CpuWrite(address, value),
            _ => () => { }
        };

        write();
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
}
