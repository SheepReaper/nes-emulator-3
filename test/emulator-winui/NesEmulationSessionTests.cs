using EmuSheep;
using SR.Emulation.Nes;
using Xunit;

namespace EmuSheep.Tests;

public sealed class NesEmulationSessionTests
{
    [Fact]
    public async Task Start_PublishesCopyableFramesFromLoadedRom()
    {
        await using var session = new NesEmulationSession(CreateMapperZeroRom());
        var frameAvailable = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.FrameAvailable += (_, _) => frameAvailable.TrySetResult();

        session.Start();
        await frameAvailable.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        byte[] frame = new byte[Nes.FrameBufferSize];

        var copied = session.TryCopyLatestFrame(frame, out var frameNumber);

        Assert.True(copied);
        Assert.True(frameNumber > 0);
        Assert.All(frame.Where((_, index) => index % 4 == 3), alpha => Assert.Equal(0xFF, alpha));
    }

    [Fact]
    public async Task StopAsync_StopsTheSessionAndIsIdempotent()
    {
        await using var session = new NesEmulationSession(CreateMapperZeroRom());
        session.Start();

        await session.StopAsync();
        await session.StopAsync();

        Assert.False(session.IsRunning);
    }

    [Fact]
    public async Task StopAsync_DoesNotPublishFramesAfterItReturns()
    {
        await using var session = new NesEmulationSession(CreateMapperZeroRom());
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
        await using var session = new NesEmulationSession(CreateMapperZeroRom());
        byte[] frame = new byte[Nes.FrameBufferSize];

        var copied = session.TryCopyLatestFrame(frame, out var frameNumber);

        Assert.False(copied);
        Assert.Equal(0UL, frameNumber);
    }

    private static byte[] CreateMapperZeroRom()
    {
        var rom = new byte[16 + 16 * 1024 + 8 * 1024];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = 1;
        rom[5] = 1;
        Array.Fill(rom, (byte)0xEA, 16, 16 * 1024);
        rom[16 + 0x3FFA] = 0x00;
        rom[16 + 0x3FFB] = 0x80;
        rom[16 + 0x3FFC] = 0x00;
        rom[16 + 0x3FFD] = 0x80;
        rom[16 + 0x3FFE] = 0x00;
        rom[16 + 0x3FFF] = 0x80;
        return rom;
    }
}
