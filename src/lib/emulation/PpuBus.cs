using System;

using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;

public sealed class PpuBus(CartridgeSlot cartridgeSlot) : IBus
{
    private readonly byte[] _vRam = new byte[0x1000]; // Up to 4KB for four-screen nametables
    private readonly byte[] _paletteRam = new byte[0x20]; // 32 bytes of Palette RAM
    private ulong _ppuCycle;

    public byte Read(ushort address)
    {
        // PPU addresses are masked to 14 bits
        address &= 0x3FFF;
        ObservePpuAddress(address);

        return address switch
        {
            >= 0x0000 and <= 0x1FFF => cartridgeSlot.PpuRead(address),
            >= 0x2000 and <= 0x3EFF => _vRam[GetVramAddress(address)], // Nametable VRAM with mirroring
            >= 0x3F00 and <= 0x3FFF => _paletteRam[GetPaletteRamAddress(address)], // Palette RAM with mirroring
            _ => 0 // Should not happen with the 14-bit mask
        };
    }

    public void Write(ushort address, byte value)
    {
        // PPU addresses are masked to 14 bits
        address &= 0x3FFF;
        ObservePpuAddress(address);

        Action write = address switch
        {
            >= 0x0000 and <= 0x1FFF => () => cartridgeSlot.PpuWrite(address, value),
            >= 0x2000 and <= 0x3EFF => () => _vRam[GetVramAddress(address)] = value, // Nametable VRAM with mirroring
            >= 0x3F00 and <= 0x3FFF => () => _paletteRam[GetPaletteRamAddress(address)] = value, // Palette RAM with mirroring
            _ => () => { }
        };

        write();
    }

    private ushort GetVramAddress(ushort address)
    {
        // $3000-$3EFF mirrors $2000-$2EFF before cartridge mirroring is applied.
        var nametableAddress = (ushort)((address - 0x2000) & 0x0FFF);
        var table = nametableAddress / 0x0400;
        var offset = nametableAddress & 0x03FF;
        var mirroring = cartridgeSlot.Cartridge?.NametableMirroring ?? NametableMirroring.Horizontal;
        var physicalTable = mirroring switch
        {
            NametableMirroring.Vertical => table & 0x01,
            NametableMirroring.Horizontal => table >> 1,
            NametableMirroring.FourScreen => table,
            NametableMirroring.SingleScreenUpper => 1,
            _ => 0
        };
        return (ushort)((physicalTable * 0x0400) + offset);
    }

    private static ushort GetPaletteRamAddress(ushort address)
    {
        // Palette RAM is mirrored every 32 bytes.
        var mirroredAddress = (ushort)(address & 0x1F);

        // Addresses $3F10, $3F14, $3F18, $3F1C are mirrors of $3F00, $3F04, $3F08, $3F0C.
        if (mirroredAddress == 0x10 || mirroredAddress == 0x14 || mirroredAddress == 0x18 || mirroredAddress == 0x1C)
        {
            return (ushort)(mirroredAddress & 0x0F);
        }
        return mirroredAddress;
    }

    internal int VramSize => _vRam.Length;
    internal int PaletteRamSize => _paletteRam.Length;
    internal void CopyVram(int offset, Span<byte> destination) => _vRam.AsSpan(offset, destination.Length).CopyTo(destination);
    internal void CopyPaletteRam(int offset, Span<byte> destination) => _paletteRam.AsSpan(offset, destination.Length).CopyTo(destination);
    internal void WriteVram(int offset, ReadOnlySpan<byte> source) => source.CopyTo(_vRam.AsSpan(offset, source.Length));
    internal void WritePaletteRam(int offset, ReadOnlySpan<byte> source) => source.CopyTo(_paletteRam.AsSpan(offset, source.Length));

    internal byte Peek(ushort address)
    {
        address &= 0x3FFF;
        return address switch
        {
            <= 0x1FFF => cartridgeSlot.PpuPeek(address),
            <= 0x3EFF => _vRam[GetVramAddress(address)],
            _ => _paletteRam[GetPaletteRamAddress(address)]
        };
    }

    internal void AdvanceCycle() => _ppuCycle++;
    internal void ResetCycle() => _ppuCycle = 0;
    internal void NotifyPpuAddress(ushort address)
    {
        address &= 0x3FFF;
        if (address >= 0x3F00) address -= 0x1000;
        cartridgeSlot.NotifyPpuAddress(address, _ppuCycle);
    }

    private void ObservePpuAddress(ushort address)
    {
        if (address <= 0x2FFF)
            cartridgeSlot.NotifyPpuAddress(address, _ppuCycle);
        else if (address <= 0x3EFF)
            cartridgeSlot.NotifyPpuAddress((ushort)(address - 0x1000), _ppuCycle);
    }
}
