using System;
using System.Collections.Generic;
using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes;

/// <summary>
/// Batch execution coordinator for dots, scanlines, and frames.
/// </summary>
internal static class NesBatchRunner
{
    internal static NesRunResult RunBatch(
        NesSystem nes,
        Ppu ppu,
        Audio.Apu apu,
        NesDebugger debugger,
        int cpuDivisor,
        int ppuDivisor,
        ref int cpuAccumulator,
        Action clockCpuCore,
        int maximumDots,
        bool untilFrame,
        Action<FrameReadyEventArgs> raiseFrameReady)
    {
        List<FrameReadyEventArgs>? framesToRaise = null;
        NesRunResult result;
        lock (nes.SyncRoot)
        {
            if (nes.IsPausedLocked)
            {
                return new NesRunResult(0, 0, 0, NesRunStopReason.Paused);
            }

            var startingCpu = nes.CpuClockCounter;
            var startingFrame = ppu.FrameNumber;
            var dots = 0;
            var stopReason = NesRunStopReason.Completed;

            if (!debugger.HasEnabledBreakpointsLocked)
            {
                NesFastBatchRunner.Run(
                    nes, ppu, cpuDivisor, ppuDivisor, ref cpuAccumulator,
                    clockCpuCore, maximumDots, untilFrame, ref dots, ref framesToRaise);
            }
            else
            {
                NesDebugBatchRunner.Run(
                    nes, maximumDots, untilFrame, ref dots, ref stopReason, ref framesToRaise);
            }

            apu.FlushAudioSamples();
            result = new NesRunResult(
                dots, nes.CpuClockCounter - startingCpu,
                ppu.FrameNumber - startingFrame, stopReason);
        }

        if (framesToRaise != null)
        {
            foreach (var frame in framesToRaise)
            {
                raiseFrameReady(frame);
            }
        }

        debugger.DispatchPendingEvents();
        return result;
    }
}
