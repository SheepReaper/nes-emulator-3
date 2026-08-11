using System.Collections.Generic;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Executes batch runs when breakpoints are enabled (dot-by-dot check).
/// </summary>
internal static class NesDebugBatchRunner
{
    internal static void Run(
        NesSystem nes,
        int maximumDots,
        bool untilFrame,
        ref int dots,
        ref NesRunStopReason stopReason,
        ref List<FrameReadyEventArgs>? framesToRaise)
    {
        while (dots < maximumDots)
        {
            if (!nes.ExecuteDotLocked(true, out var frame))
            {
                stopReason = NesRunStopReason.Breakpoint;
                break;
            }
            dots++;
            if (frame != null)
            {
                (framesToRaise ??= []).Add(frame);
            }
            if (nes.IsPausedLocked)
            {
                stopReason = NesRunStopReason.Breakpoint;
                break;
            }
            if (untilFrame && frame != null)
            {
                break;
            }
        }
    }
}
