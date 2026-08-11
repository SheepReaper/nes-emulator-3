using System;
using System.Collections.Generic;

namespace Sheep.Emulation.Nes;

/// <summary>
/// Executes batch runs when breakpoints are disabled (optimized fast path).
/// </summary>
internal static class NesFastBatchRunner
{
    internal static void Run(
        NesSystem nes,
        Ppu ppu,
        int cpuDivisor,
        int ppuDivisor,
        ref int cpuAccumulator,
        Action clockCpuCore,
        int maximumDots,
        bool untilFrame,
        ref int dots,
        ref List<FrameReadyEventArgs>? framesToRaise)
    {
        while (dots < maximumDots)
        {
            var dotsUntilCpuClock = (cpuDivisor - cpuAccumulator + ppuDivisor - 1) / ppuDivisor;
            var batchDots = Math.Min(maximumDots - dots, dotsUntilCpuClock);
            if (untilFrame)
            {
                batchDots = 1;
            }

            if (batchDots > 1 && !ppu.RenderingEnabled && ppu.TryClockBatch(batchDots))
            {
                cpuAccumulator += batchDots * ppuDivisor;
                dots += batchDots;
                if (cpuAccumulator >= cpuDivisor)
                {
                    clockCpuCore();
                }
                continue;
            }

            ppu.ClockDots(batchDots);
            cpuAccumulator += batchDots * ppuDivisor;
            dots += batchDots;
            if (cpuAccumulator >= cpuDivisor)
            {
                clockCpuCore();
            }

            var frame = nes.TakePendingFrameInternal();
            if (frame != null)
            {
                (framesToRaise ??= []).Add(frame);
            }
            if (untilFrame && frame != null)
            {
                break;
            }
        }
    }
}
