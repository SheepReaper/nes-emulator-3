using System;

namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Palette address resolution and pixel composition rules.
/// </summary>
internal static class PpuPaletteResolver
{
    internal static int ResolveAddress(
        int bgPixel,
        int bgPalette,
        int spPixel,
        int spPalette,
        bool spBehind,
        bool spZero,
        int x,
        Action setSprite0Hit)
    {
        if (bgPixel == 0 && spPixel == 0)
        {
            return 0x3F00;
        }
        if (bgPixel == 0)
        {
            return 0x3F10 + (spPalette * 4) + spPixel;
        }
        if (spPixel == 0)
        {
            return 0x3F00 + (bgPalette * 4) + bgPixel;
        }

        if (spZero && x < 255)
        {
            setSprite0Hit();
        }

        return spBehind
            ? 0x3F00 + (bgPalette * 4) + bgPixel
            : 0x3F10 + (spPalette * 4) + spPixel;
    }
}
