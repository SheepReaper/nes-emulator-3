namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// VRAM and Palette RAM address mapping and mirroring calculations for PPU bus.
/// </summary>
internal static class PpuBusAddressMapper
{
    internal static ushort GetVramAddress(ushort address, NametableMirroring mirroring)
    {
        var nametableAddress = (ushort)((address - 0x2000) & 0x0FFF);
        var table = nametableAddress / 0x0400;
        var offset = nametableAddress & 0x03FF;
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

    internal static ushort GetPaletteRamAddress(ushort address)
    {
        var mirroredAddress = (ushort)(address & 0x1F);
        return (mirroredAddress & 0x13) == 0x10 ? (ushort)(mirroredAddress & 0x0F) : mirroredAddress;
    }
}
