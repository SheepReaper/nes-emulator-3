using System;

namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Fast batch clocking optimization when PPU rendering is disabled.
/// </summary>
internal static class PpuBatchClock
{
    internal static bool TryClockBatch(
        int count,
        bool renderingEnabled,
        PpuState state,
        PpuTiming time,
        PpuBus? ppuBus,
        InterruptLines interrupts,
        ref ulong elapsedPpuDots,
        bool grayscale,
        int paletteEmphasisOffset,
        uint[] colorLookup,
        byte[] renderFrame,
        PpuScrollRegisters scroll,
        Func<ushort, byte> peekPalette)
    {
        if (count <= 1 || renderingEnabled || state.VblankNmiDelayDots != 0)
        {
            return false;
        }
        if (time.Cycle + count >= time.DotsPerScanline)
        {
            return false;
        }
        if (time.Phase == PpuPhase.VBlank && time.Scanline == 241 && time.Cycle <= 1 && time.Cycle + count > 1)
        {
            return false;
        }
        if (time.Phase == PpuPhase.PreRender && time.Cycle <= 1 && time.Cycle + count > 1)
        {
            return false;
        }
        if (time.Phase == PpuPhase.PreRender && time.Cycle <= time.DotsPerScanline - 3 && time.Cycle + count > time.DotsPerScanline - 3)
        {
            return false;
        }
        if (time.Phase == PpuPhase.Visible && time.Scanline == Ppu.FrameHeight - 1 && time.Cycle + count > time.DotsPerScanline - 1)
        {
            return false;
        }

        ppuBus?.AdvanceCycles(count);
        interrupts.AdvancePpuDots(count);
        elapsedPpuDots += (ulong)count;
        if (time.Phase == PpuPhase.Visible)
        {
            var first = Math.Max(1, time.Cycle);
            var last = Math.Min(257, time.Cycle + count);
            for (var c = first; c < last; c++)
            {
                PpuPixelRenderer.RenderBlankPixel(c - 1, time.Scanline, grayscale, paletteEmphasisOffset, colorLookup, renderFrame, scroll.VramAddress, peekPalette);
            }
        }
        time.AdvanceCycleDirect(count);
        return true;
    }
}
