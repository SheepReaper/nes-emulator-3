namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Handles cycle 1 VBlank/pre-render interrupt flags and suppression logic.
/// </summary>
internal static class PpuCycleEvents
{
    internal static void HandleCycle1(
        int scanline,
        int preRenderScanline,
        PpuState state,
        InterruptLines interrupts)
    {
        if (scanline == preRenderScanline)
        {
            state.Status.VBlank = false;
            state.Status.Sprite0Hit = false;
            state.Status.SpriteOverflow = false;
            interrupts.Nmi = false;
            interrupts.CancelShortNmiEdge(2);
            state.VblankNmiDelayDots = 0;
            state.SuppressVblank = false;
        }
        else if (scanline == 241)
        {
            if (!state.SuppressVblank)
            {
                state.Status.VBlank = true;
                if (state.Ctrl.VBlankNmiEnable)
                {
                    state.VblankNmiDelayDots = 2;
                }
            }
            state.SuppressVblank = false;
        }
    }
}
