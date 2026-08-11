using System;

namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Step-by-step dot clocking and event execution pipeline for PPU.
/// </summary>
internal static class PpuDotClockDriver
{
    internal static ulong? ClockSingleDot(
        ref ulong elapsedPpuDots,
        PpuTiming time,
        PpuState state,
        PpuMaskSettings mask,
        PpuScrollRegisters scroll,
        PpuBackgroundPipeline bg,
        PpuSpriteRenderer sprites,
        PpuOam oam,
        PpuFrameManager frame,
        PpuBus? ppuBus,
        InterruptLines interrupts,
        bool isPal,
        uint[] colorLookup,
        Func<ushort, byte> readBus,
        Func<ushort, byte> peekPalette)
    {
        elapsedPpuDots++;
        ppuBus?.AdvanceCycle();
        interrupts.AdvancePpuDot();
        state.AdvanceDot();

        var preRenderScanline = time.PreRenderScanline;
        if (time.Cycle == 1)
        {
            PpuCycleEvents.HandleCycle1(time.Scanline, preRenderScanline, state, interrupts);
        }

        if ((time.Scanline < Ppu.FrameHeight || time.Scanline == preRenderScanline) && mask.RenderingEnabled)
        {
            PpuBackgroundPipelineDriver.Clock(
                time.Cycle, time.Scanline, preRenderScanline, time.DotsPerScanline,
                time, scroll, bg, sprites, oam, state, ppuBus, readBus);
            if (time.Cycle is >= 257 and <= 320)
            {
                sprites.FetchSpritePatterns(time.Cycle, state.Ctrl.SpriteSize, state.Ctrl.SpritePatternTableAddress, readBus);
            }
        }

        if (time.Scanline < Ppu.FrameHeight && time.Cycle is >= 1 and <= 256)
        {
            PpuPixelRenderer.RenderPixel(
                time.Cycle - 1, time.Scanline, isPal, mask.RenderingEnabled, mask.Grayscale,
                mask.PaletteEmphasisOffset, colorLookup, frame.RenderFrame, scroll, bg, sprites,
                mask.ShowBackground, mask.ShowBackgroundLeft, mask.ShowSprites, mask.ShowSpritesLeft,
                peekPalette, () => state.Status.Sprite0Hit = true);
        }

        var completed = time.Scanline == Ppu.FrameHeight - 1 && time.Cycle == time.DotsPerScanline - 1
            ? frame.PublishFrame()
            : (ulong?)null;
        time.Advance(mask.RenderingEnabled);
        return completed;
    }
}
