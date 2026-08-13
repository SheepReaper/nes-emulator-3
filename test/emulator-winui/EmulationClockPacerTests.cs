using Xunit;

namespace EmuSheep.Tests;

public sealed class EmulationClockPacerTests
{
    private const long StopwatchFrequency = 10_000_000;

    [Fact]
    public void GetBudget_TracksTheConfiguredCpuFrequencyWithoutFrameRounding()
    {
        var pacer = new EmulationClockPacer(21_477_272, 12, StopwatchFrequency, leadMilliseconds: 0,
            maximumRecoverableLagMilliseconds: 2_000);
        _ = pacer.GetBudget(0, executedCpuClocks: 0, speedMultiplier: 1);

        var budget = pacer.GetBudget(StopwatchFrequency, executedCpuClocks: 0, speedMultiplier: 1);

        Assert.Equal(1_789_772UL, budget.CpuClocksToRun);
        Assert.Equal(0L, budget.DelayStopwatchTicks);
    }

    [Fact]
    public void GetBudget_ReturnsWallClockDelayWhenSimulationIsAhead()
    {
        var pacer = new EmulationClockPacer(21_477_272, 12, StopwatchFrequency, leadMilliseconds: 0);
        _ = pacer.GetBudget(0, executedCpuClocks: 0, speedMultiplier: 1);

        var budget = pacer.GetBudget(StopwatchFrequency, executedCpuClocks: 1_789_773, speedMultiplier: 1);

        Assert.Equal(0UL, budget.CpuClocksToRun);
        Assert.InRange(budget.DelayStopwatchTicks, 1L, 20L);
    }

    [Fact]
    public void GetBudget_AppliesRequestedSpeedWithoutChangingHardwareFrequency()
    {
        var pacer = new EmulationClockPacer(21_477_272, 12, StopwatchFrequency, leadMilliseconds: 0,
            maximumRecoverableLagMilliseconds: 2_000);
        _ = pacer.GetBudget(0, executedCpuClocks: 0, speedMultiplier: 2);

        var budget = pacer.GetBudget(StopwatchFrequency / 2, executedCpuClocks: 0, speedMultiplier: 2);

        Assert.Equal(1_789_772UL, budget.CpuClocksToRun);
    }

    [Fact]
    public void GetBudget_RebasesAfterAnUnrecoverableHostStall()
    {
        var pacer = new EmulationClockPacer(21_477_272, 12, StopwatchFrequency, leadMilliseconds: 0,
            maximumRecoverableLagMilliseconds: 250);
        _ = pacer.GetBudget(0, executedCpuClocks: 0, speedMultiplier: 1);

        var budget = pacer.GetBudget(StopwatchFrequency, executedCpuClocks: 10, speedMultiplier: 1);

        Assert.True(budget.Rebased);
        Assert.Equal(0UL, budget.CpuClocksToRun);
        Assert.Equal(10UL, pacer.ExecutedClockOrigin);
    }
}
