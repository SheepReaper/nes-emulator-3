namespace EmuSheep;

internal sealed class ActualFrameRateCounter
{
    private readonly long _stopwatchFrequency;
    private readonly long _reportIntervalTicks;
    private long _windowStartTicks;
    private ulong _completedFrames;

    internal ActualFrameRateCounter(long stopwatchFrequency, double reportIntervalMilliseconds = 500)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stopwatchFrequency);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reportIntervalMilliseconds);
        _stopwatchFrequency = stopwatchFrequency;
        _reportIntervalTicks = Math.Max(
            1, (long)Math.Ceiling(reportIntervalMilliseconds / 1_000 * stopwatchFrequency));
    }

    internal bool TryRecord(ulong completedFrames, long elapsedStopwatchTicks, out double framesPerSecond)
    {
        _completedFrames += completedFrames;
        var elapsedTicks = elapsedStopwatchTicks - _windowStartTicks;
        if (elapsedTicks < _reportIntervalTicks)
        {
            framesPerSecond = 0;
            return false;
        }

        framesPerSecond = _completedFrames * (double)_stopwatchFrequency / elapsedTicks;
        _windowStartTicks = elapsedStopwatchTicks;
        _completedFrames = 0;
        return true;
    }
}
