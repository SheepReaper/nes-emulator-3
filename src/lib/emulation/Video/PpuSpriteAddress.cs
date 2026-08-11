namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Helper for calculating sprite pattern addresses.
/// </summary>
internal static class PpuSpriteAddress
{
    internal static ushort GetAddress(byte tile, int row, bool spriteSize16, bool spritePatternTableAddress)
    {
        if (!spriteSize16)
        {
            var table = spritePatternTableAddress ? 0x1000 : 0;
            return (ushort)(table + (tile * 16) + row);
        }

        var tableAddress = (tile & 0x01) * 0x1000;
        var tileIndex = tile & 0xFE;
        if (row >= 8)
        {
            tileIndex++;
            row -= 8;
        }
        return (ushort)(tableAddress + (tileIndex * 16) + row);
    }
}
