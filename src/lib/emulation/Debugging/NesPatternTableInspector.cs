using System;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Snapshot generator for pattern table tiles and colors.
/// </summary>
internal static class NesPatternTableInspector
{
    internal static PatternTableSnapshot Capture(NesSystem nes, int tableIndex, int paletteIndex)
    {
        if ((uint)tableIndex > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(tableIndex));
        }
        if ((uint)paletteIndex > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(paletteIndex));
        }

        var rgba = new byte[PatternTableSnapshot.Width * PatternTableSnapshot.Height * 4];
        var ppuMask = nes.Ppu.CaptureDebugState().Mask;
        var patternBase = tableIndex * 0x1000;
        for (var tileY = 0; tileY < 16; tileY++)
        {
            for (var tileX = 0; tileX < 16; tileX++)
            {
                var tileAddress = patternBase + ((tileY * 16 + tileX) * 16);
                for (var row = 0; row < 8; row++)
                {
                    var low = nes.PpuBus.Peek((ushort)(tileAddress + row));
                    var high = nes.PpuBus.Peek((ushort)(tileAddress + row + 8));
                    for (var column = 0; column < 8; column++)
                    {
                        var bit = 7 - column;
                        var pixel = ((high >> bit) & 1) * 2 + ((low >> bit) & 1);
                        var paletteAddress = pixel == 0 ? 0x3F00 : 0x3F00 + (paletteIndex * 4) + pixel;
                        var color = nes.PpuBus.Peek((ushort)paletteAddress) & 0x3F;
                        if ((ppuMask & 1) != 0)
                        {
                            color &= 0x30;
                        }
                        NesPalette.GetColor(nes.VideoStandard, color, ppuMask, out var red, out var green, out var blue);
                        var x = tileX * 8 + column;
                        var y = tileY * 8 + row;
                        var offset = (y * PatternTableSnapshot.Width + x) * 4;
                        rgba[offset] = red;
                        rgba[offset + 1] = green;
                        rgba[offset + 2] = blue;
                        rgba[offset + 3] = 0xFF;
                    }
                }
            }
        }
        return new PatternTableSnapshot(tableIndex, paletteIndex, rgba);
    }
}
