using System;

namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Handles NES hardware sprite evaluation algorithm and sprite overflow bug.
/// </summary>
internal static class PpuSpriteEvaluation
{
    internal static bool Evaluate(
        PpuOam oam,
        int nextScanline,
        bool spriteSize16,
        bool spritePatternTableAddress,
        SpriteRenderData[] sprites,
        out int spriteCount,
        out int overflowDot)
    {
        spriteCount = 0;
        overflowDot = -1;
        var height = spriteSize16 ? 16 : 8;
        var spriteIndex = 0;
        var dot = 65;

        // Primary evaluation: copy up to 8 sprites
        for (; spriteIndex < 64 && spriteCount < 8; spriteIndex++)
        {
            var address = spriteIndex * 4;
            var top = oam[address] + 1;
            var row = nextScanline - top;
            if (row < 0 || row >= height)
            {
                dot += 2;
                continue;
            }

            dot += 8;
            var tile = oam[address + 1];
            var attributes = oam[address + 2];
            if ((attributes & 0x80) != 0) row = height - 1 - row;
            var patternAddress = PpuSpriteAddress.GetAddress(tile, row, spriteSize16, spritePatternTableAddress);
            sprites[spriteCount++] = new SpriteRenderData(oam[address + 3], attributes, patternAddress, spriteIndex == 0);
        }

        if (spriteCount < 8) return false;

        // Secondary evaluation: overflow detection with hardware m-offset glitch
        var n = spriteIndex;
        var m = 0;
        while (n < 64)
        {
            dot += 2;
            var byteAddress = (n * 4) + m;
            var byteVal = oam[byteAddress];
            var top = byteVal + 1;
            var row = nextScanline - top;
            if (row >= 0 && row < height)
            {
                overflowDot = Math.Min(dot, 256);
                return true;
            }
            n++;
            m = (m + 1) & 0x03;
        }

        return false;
    }
}
