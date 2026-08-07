using System;

using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;

public sealed class PpuBus(CartridgeSlot cartridgeSlot) : IBus
{
    private readonly byte[] _vRam = new byte[0x800]; // 2KB of VRAM for nametables
    private readonly byte[] _paletteRam = new byte[0x20]; // 32 bytes of Palette RAM

    public byte Read(ushort address)
    {
        // PPU addresses are masked to 14 bits
        address &= 0x3FFF;

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

        Action write = address switch
        {
            >= 0x0000 and <= 0x1FFF => () => cartridgeSlot.PpuWrite(address, value),
            >= 0x2000 and <= 0x3EFF => () => _vRam[GetVramAddress(address)] = value, // Nametable VRAM with mirroring
            >= 0x3F00 and <= 0x3FFF => () => _paletteRam[GetPaletteRamAddress(address)] = value, // Palette RAM with mirroring
            _ => () => { }
        };

        write();
    }

    private static ushort GetVramAddress(ushort address)
    {
        // TODO: Implement proper mirroring based on cartridge settings (Horizontal/Vertical)
        // For now, using Vertical mirroring as a default.
        // $2000-$27FF maps to the 2KB VRAM.
        // $2800-$2FFF is a mirror of $2000-$27FF.
        // $3000-$3EFF is a mirror of $2000-$2EFF.
        return (ushort)(address & 0x07FF);
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
}
