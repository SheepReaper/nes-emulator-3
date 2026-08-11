using System;

using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes;

/// <summary>
/// Execution and dot-level stepping coordinator for NesSystem.
/// </summary>
internal static class NesExecutionEngine
{
    internal static void Clock(
        NesSystem nes,
        NesSystemUnits u,
        Action<FrameReadyEventArgs>? onFrameReady)
    {
        FrameReadyEventArgs? frameReady;
        lock (nes.SyncRoot)
        {
            if (nes.IsPausedLocked)
            {
                return;
            }
            _ = NesClockDriver.ExecuteDotLocked(nes, u.Debugger, u.Debugger.HasEnabledBreakpointsLocked, out frameReady);
        }

        if (frameReady != null)
        {
            onFrameReady?.Invoke(frameReady);
        }
        u.Debugger.DispatchPendingEvents();
    }

    internal static NesRunResult RunForPpuDots(NesSystem nes, int count) =>
        count < 0 ? throw new ArgumentOutOfRangeException(nameof(count)) : nes.RunBatchInternal(count, false);

    internal static NesRunResult RunUntilFrame(NesSystem nes) =>
        nes.RunBatchInternal(int.MaxValue, true);
}
