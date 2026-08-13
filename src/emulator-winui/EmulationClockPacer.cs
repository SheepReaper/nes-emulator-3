namespace EmuSheep;

internal readonly record struct EmulationPacingBudget(
    ulong CpuClocksToRun,
    long DelayStopwatchTicks,
    bool Rebased);

internal sealed class EmulationClockPacer
{
    private readonly double _cpuClocksPerSecond;
    private readonly long _stopwatchFrequency;
    private readonly double _leadSeconds;
    private readonly double _maximumRecoverableLagSeconds;
    private long _elapsedTickOrigin;
    private bool _initialized;

    internal EmulationClockPacer(
        int masterClockHz,
        int cpuDivisor,
        long stopwatchFrequency,
        double leadMilliseconds = 8,
        double maximumRecoverableLagMilliseconds = 250)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(masterClockHz);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cpuDivisor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stopwatchFrequency);
        ArgumentOutOfRangeException.ThrowIfNegative(leadMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecoverableLagMilliseconds);

        _cpuClocksPerSecond = (double)masterClockHz / cpuDivisor;
        _stopwatchFrequency = stopwatchFrequency;
        _leadSeconds = leadMilliseconds / 1_000;
        _maximumRecoverableLagSeconds = maximumRecoverableLagMilliseconds / 1_000;
    }

    internal ulong ExecutedClockOrigin { get; private set; }

    internal EmulationPacingBudget GetBudget(
        long elapsedStopwatchTicks,
        ulong executedCpuClocks,
        double speedMultiplier)
    {
        if (!double.IsFinite(speedMultiplier) || speedMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));

        if (!_initialized)
        {
            _initialized = true;
            _elapsedTickOrigin = elapsedStopwatchTicks;
            ExecutedClockOrigin = executedCpuClocks;
        }

        var elapsedSeconds = Math.Max(0, elapsedStopwatchTicks - _elapsedTickOrigin) /
            (double)_stopwatchFrequency;
        var clocksPerWallSecond = _cpuClocksPerSecond * speedMultiplier;
        var target = ExecutedClockOrigin + (ulong)Math.Floor(
            (elapsedSeconds + _leadSeconds) * clocksPerWallSecond);

        if (target > executedCpuClocks)
        {
            var lagClocks = target - executedCpuClocks;
            if (lagClocks / clocksPerWallSecond > _maximumRecoverableLagSeconds)
            {
                _elapsedTickOrigin = elapsedStopwatchTicks;
                ExecutedClockOrigin = executedCpuClocks;
                return new EmulationPacingBudget(0, 0, true);
            }

            return new EmulationPacingBudget(lagClocks, 0, false);
        }

        var aheadClocks = executedCpuClocks - target + 1;
        var delayTicks = (long)Math.Ceiling(
            aheadClocks / clocksPerWallSecond * _stopwatchFrequency);
        return new EmulationPacingBudget(0, Math.Max(1, delayTicks), false);
    }
}
