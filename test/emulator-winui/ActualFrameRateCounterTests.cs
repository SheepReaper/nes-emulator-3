using Xunit;

namespace EmuSheep.Tests;

public sealed class ActualFrameRateCounterTests
{
    [Fact]
    public void TryRecord_ReportsCompletedFramesOverActualElapsedTime()
    {
        var counter = new ActualFrameRateCounter(1_000, reportIntervalMilliseconds: 500);

        Assert.False(counter.TryRecord(30, 250, out _));
        Assert.True(counter.TryRecord(30, 1_000, out var framesPerSecond));

        Assert.Equal(60, framesPerSecond, 6);
    }

    [Fact]
    public void TryRecord_StartsANewMeasurementWindowAfterReporting()
    {
        var counter = new ActualFrameRateCounter(1_000, reportIntervalMilliseconds: 500);
        Assert.True(counter.TryRecord(60, 1_000, out _));

        Assert.False(counter.TryRecord(15, 1_250, out _));
        Assert.True(counter.TryRecord(15, 1_500, out var framesPerSecond));

        Assert.Equal(60, framesPerSecond, 6);
    }
}
