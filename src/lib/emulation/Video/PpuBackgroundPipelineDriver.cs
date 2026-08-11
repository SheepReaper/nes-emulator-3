using System;

namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Background fetch sequencer for PPU scanlines.
/// </summary>
internal static class PpuBackgroundPipelineDriver
{
    internal static void Clock(
        int cycle,
        int scanline,
        int preRenderScanline,
        int dotsPerScanline,
        PpuTiming time,
        PpuScrollRegisters scroll,
        PpuBackgroundPipeline bg,
        PpuSpriteRenderer sprites,
        PpuOam oam,
        PpuState state,
        PpuBus? ppuBus,
        Func<ushort, byte> readBus)
    {
        if (cycle == 1)
        {
            sprites.BeginScanline();
        }

        if ((cycle is >= 2 and <= 257) || (cycle is >= 321 and <= 337))
        {
            bg.Shift();
            switch ((cycle - 1) & 0x07)
            {
                case 0:
                    bg.Load();
                    bg.NextTileId = readBus((ushort)(0x2000 | (scroll.VramAddress & 0x0FFF)));
                    break;
                case 2:
                    var attribute = readBus((ushort)(0x23C0 | (scroll.VramAddress & 0x0C00) |
                        ((scroll.VramAddress >> 4) & 0x38) | ((scroll.VramAddress >> 2) & 0x07)));
                    var shift = ((scroll.VramAddress >> 4) & 0x04) | (scroll.VramAddress & 0x02);
                    bg.NextTileAttribute = (byte)((attribute >> shift) & 0x03);
                    break;
                case 3:
                    ppuBus?.NotifyPpuAddress(GetBackgroundPatternAddress(0, state, scroll, bg));
                    break;
                case 4:
                    bg.NextTileLow = readBus(GetBackgroundPatternAddress(0, state, scroll, bg));
                    break;
                case 6:
                    bg.NextTileHigh = readBus(GetBackgroundPatternAddress(8, state, scroll, bg));
                    break;
                case 7:
                    scroll.IncrementHorizontal();
                    break;
            }
        }

        if (cycle == 65)
        {
            sprites.EvaluateSprites(oam, scanline, time.ScanlinesPerFrame, state.Ctrl.SpriteSize, state.Ctrl.SpritePatternTableAddress);
        }

        if (cycle >= 65 && cycle <= 256 && cycle == sprites.OverflowDot)
        {
            state.Status.SpriteOverflow = true;
        }

        if (cycle == 256)
        {
            scroll.IncrementVertical();
        }

        if (cycle == 257)
        {
            bg.Load();
            scroll.CopyHorizontal();
        }

        if (scanline == preRenderScanline && cycle is >= 280 and <= 304)
        {
            scroll.CopyVertical();
        }

        if (cycle == dotsPerScanline - 3 || cycle == dotsPerScanline - 1)
        {
            bg.NextTileId = readBus((ushort)(0x2000 | (scroll.VramAddress & 0x0FFF)));
        }
    }

    private static ushort GetBackgroundPatternAddress(
        int planeOffset,
        PpuState state,
        PpuScrollRegisters scroll,
        PpuBackgroundPipeline bg)
    {
        var table = state.Ctrl.BackgroundPatternTableAddress ? 0x1000 : 0;
        var fineY = (scroll.VramAddress >> 12) & 0x07;
        return (ushort)(table + (bg.NextTileId * 16) + fineY + planeOffset);
    }
}
