using System;
using System.Runtime.CompilerServices;

namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Pixel rendering, palette lookup, and RGBA pixel writing for PPU.
/// </summary>
internal static class PpuPixelRenderer
{
    internal static void RenderPixel(
        int x,
        int y,
        bool isPal,
        bool renderingEnabled,
        bool grayscale,
        int paletteEmphasisOffset,
        uint[] colorLookup,
        byte[] renderFrame,
        PpuScrollRegisters scroll,
        PpuBackgroundPipeline background,
        PpuSpriteRenderer sprites,
        bool showBackground,
        bool showBackgroundLeft,
        bool showSprites,
        bool showSpritesLeft,
        Func<ushort, byte> peekPalette,
        Action setSprite0Hit)
    {
        if (isPal && (y == 0 || x < 2 || x >= Ppu.FrameWidth - 2))
        {
            WriteBorderPixel(x, y, renderFrame);
            return;
        }

        if (!renderingEnabled)
        {
            RenderBlankPixel(x, y, grayscale, paletteEmphasisOffset, colorLookup, renderFrame, scroll.VramAddress, peekPalette);
            return;
        }

        var (bgPixel, bgPalette) = (showBackground && (x >= 8 || showBackgroundLeft))
            ? background.SamplePixel(scroll.FineXScroll)
            : (0, 0);

        var (spPixel, spPalette, spBehind, spZero) = (showSprites && (x >= 8 || showSpritesLeft))
            ? sprites.SamplePixel(x)
            : (0, 0, false, false);

        var paletteAddress = PpuPaletteResolver.ResolveAddress(bgPixel, bgPalette, spPixel, spPalette, spBehind, spZero, x, setSprite0Hit);
        var color = peekPalette((ushort)paletteAddress) & 0x3F;
        if (grayscale)
        {
            color &= 0x30;
        }

        WriteRgbaPixel(x, y, color, paletteEmphasisOffset, colorLookup, renderFrame);
    }

    internal static void RenderBlankPixel(
        int x,
        int y,
        bool grayscale,
        int emphasisOffset,
        uint[] lookup,
        byte[] frame,
        ushort vramAddress,
        Func<ushort, byte> peekPalette)
    {
        var blankPaletteAddress = (vramAddress & 0x3F00) == 0x3F00 ? vramAddress & 0x3FFF : 0x3F00;
        var blankColor = peekPalette((ushort)blankPaletteAddress) & 0x3F;
        if (grayscale)
        {
            blankColor &= 0x30;
        }
        WriteRgbaPixel(x, y, blankColor, emphasisOffset, lookup, frame);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteRgbaPixel(int x, int y, int color, int emphasisOffset, uint[] lookup, byte[] frame)
    {
        var offset = ((y * Ppu.FrameWidth) + x) * 4;
        var rgba = lookup[emphasisOffset + (color & 0x3F)];
        frame[offset] = (byte)rgba;
        frame[offset + 1] = (byte)(rgba >> 8);
        frame[offset + 2] = (byte)(rgba >> 16);
        frame[offset + 3] = 0xFF;
    }

    private static void WriteBorderPixel(int x, int y, byte[] frame)
    {
        var borderOffset = ((y * Ppu.FrameWidth) + x) * 4;
        frame[borderOffset] = 0;
        frame[borderOffset + 1] = 0;
        frame[borderOffset + 2] = 0;
        frame[borderOffset + 3] = 0xFF;
    }
}
