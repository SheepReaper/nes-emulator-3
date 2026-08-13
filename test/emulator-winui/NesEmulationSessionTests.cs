using Sheep.Emulation.Nes;

using Xunit;

namespace EmuSheep.Tests;

public sealed class NesEmulationSessionTests
{
    [Fact]
    public async Task SpeedMultiplierIsHostPacingPolicyAndMustBePositiveAndFinite()
    {
        await using var session = new NesEmulationSession(NesEmulationTestHelper.CreateMapperZeroRom());
        session.SetSpeedMultiplier(2.5);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetSpeedMultiplier(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetSpeedMultiplier(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetSpeedMultiplier(double.PositiveInfinity));
    }

    [Fact]
    public async Task Start_PublishesCopyableFramesFromLoadedRom()
    {
        await using var session = new NesEmulationSession(NesEmulationTestHelper.CreateMapperZeroRom());
        var frameAvailable = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.FrameAvailable += (_, _) => frameAvailable.TrySetResult();

        session.Start();
        await frameAvailable.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        byte[] frame = new byte[NesSystem.FrameBufferSize];

        var copied = session.TryCopyLatestFrame(frame, out var frameNumber);

        Assert.True(copied);
        Assert.True(frameNumber > 0);
        Assert.All(frame.Where((_, index) => index % 4 == 3), alpha => Assert.Equal(0xFF, alpha));
    }

    [Fact]
    public async Task StopAsync_StopsTheSessionAndIsIdempotent()
    {
        await using var session = new NesEmulationSession(NesEmulationTestHelper.CreateMapperZeroRom());
        session.Start();

        await session.StopAsync();
        await session.StopAsync();

        Assert.False(session.IsRunning);
    }

    [Fact]
    public async Task StopAsync_DoesNotPublishFramesAfterItReturns()
    {
        await using var session = new NesEmulationSession(NesEmulationTestHelper.CreateMapperZeroRom());
        var firstFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frameCount = 0;
        session.FrameAvailable += (_, _) =>
        {
            Interlocked.Increment(ref frameCount);
            firstFrame.TrySetResult();
        };
        session.Start();
        await firstFrame.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await session.StopAsync();
        var countAfterStop = Volatile.Read(ref frameCount);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(countAfterStop, Volatile.Read(ref frameCount));
    }

    [Fact]
    public async Task TryCopyLatestFrame_ReturnsFalseBeforeFirstFrame()
    {
        await using var session = new NesEmulationSession(NesEmulationTestHelper.CreateMapperZeroRom());
        byte[] frame = new byte[NesSystem.FrameBufferSize];

        var copied = session.TryCopyLatestFrame(frame, out var frameNumber);

        Assert.False(copied);
        Assert.Equal(0UL, frameNumber);
    }
}
