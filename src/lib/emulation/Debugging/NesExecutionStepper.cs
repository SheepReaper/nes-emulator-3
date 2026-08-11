using System;
using System.Collections.Generic;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Execution stepper and loop controller for debugger.
/// </summary>
internal static class NesExecutionStepper
{
    internal static NesRunResult StepUntil(
        NesSystem nes,
        NesBreakpointManager bp,
        Func<ulong, ulong, int, bool> completed)
    {
        var frames = new List<FrameReadyEventArgs>();
        NesRunResult result;
        var startingCpu = nes.CpuClockCounter;
        var startingFrame = nes.CurrentFrameNumber;
        var dots = 0;
        bp.SuppressBreakpoints = true;
        try
        {
            do
            {
                if (!nes.ExecuteDotLocked(false, out var frame))
                {
                    break;
                }
                dots++;
                if (frame != null)
                {
                    frames.Add(frame);
                }
            }
            while (!completed(nes.CpuClockCounter, nes.CurrentFrameNumber, dots));
        }
        finally
        {
            bp.SuppressBreakpoints = false;
        }

        result = new NesRunResult(
            dots, nes.CpuClockCounter - startingCpu,
            nes.CurrentFrameNumber - startingFrame, NesRunStopReason.Completed);

        foreach (var frame in frames)
        {
            nes.RaiseFrameReady(frame);
        }
        return result;
    }
}
