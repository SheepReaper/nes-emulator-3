using System.Diagnostics;

namespace EmuSheep;

internal static class NesEmulationTimingHelper
{
    internal static async Task WaitForBudgetAsync(long delayStopwatchTicks, CancellationToken cancellationToken)
    {
        var delayMilliseconds = delayStopwatchTicks * 1_000.0 / Stopwatch.Frequency;
        if (delayMilliseconds > 2)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds - 1), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
