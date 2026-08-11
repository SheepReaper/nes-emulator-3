using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>
/// ExRAM and fill mode nametable reading/writing for MMC5 mapper.
/// </summary>
internal static class Mmc5NametableHandler
{
    internal static bool TryRead(
        ushort address,
        Span<byte> ciram,
        byte nametableMapping,
        byte exRamMode,
        byte[] exRam,
        byte fillTile,
        byte fillAttribute,
        out byte value)
    {
        var normalized = (address - 0x2000) & 0x0FFF;
        var source = (nametableMapping >> (normalized / 0x0400 * 2)) & 3;
        var offset = normalized & 0x03FF;
        value = source switch
        {
            0 => ciram[offset],
            1 => ciram[0x0400 + offset],
            2 when exRamMode <= 1 => exRam[offset],
            2 => 0,
            _ when offset < 0x03C0 => fillTile,
            _ => fillAttribute
        };
        return true;
    }

    internal static bool TryWrite(
        ushort address,
        byte value,
        Span<byte> ciram,
        byte nametableMapping,
        byte exRamMode,
        byte[] exRam)
    {
        var normalized = (address - 0x2000) & 0x0FFF;
        var source = (nametableMapping >> (normalized / 0x0400 * 2)) & 3;
        var offset = normalized & 0x03FF;
        if (source == 0)
        {
            ciram[offset] = value;
        }
        else if (source == 1)
        {
            ciram[0x0400 + offset] = value;
        }
        else if (source == 2 && exRamMode == 0)
        {
            exRam[offset] = value;
        }
        return true;
    }
}
