using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class PpuFrameEventTests
{
    [Theory]
    [InlineData(NesVideoStandard.Ntsc, 341 * 262)]
    [InlineData(NesVideoStandard.Pal, 341 * 312)]
    public void CompletedFrameRaisesNotificationAndCanBeCopied(NesVideoStandard standard, int maximumClocks)
    {
        var nes = new NesSystem(standard);
        FrameReadyEventArgs? notification = null;
        nes.FrameReady += (_, args) => notification = args;

        for (var i = 0; i < maximumClocks && notification is null; i++)
        {
            nes.Clock();
        }

        Assert.NotNull(notification);
        Assert.Equal(1UL, notification.FrameNumber);
        Assert.Equal(standard, notification.VideoStandard);

        var pixels = new byte[NesSystem.FrameBufferSize];
        Assert.True(nes.TryCopyFrame(pixels, out var copiedFrame));
        Assert.Equal(notification.FrameNumber, copiedFrame);
        for (var i = 3; i < pixels.Length; i += 4)
        {
            Assert.Equal(0xFF, pixels[i]);
        }
    }

    [Fact]
    public void FrameReadySubscriberExceptionsPropagateFromClock()
    {
        var nes = new NesSystem();
        EventHandler<FrameReadyEventArgs> handler = (_, _) => throw new InvalidOperationException("consumer failed");
        nes.FrameReady += handler;

        Assert.Throws<InvalidOperationException>(() =>
        {
            for (var i = 0; i < 341 * 262; i++)
            {
                nes.Clock();
            }
        });
        nes.FrameReady -= handler;
        nes.Clock();
        Assert.True(nes.TryCopyFrame(new byte[NesSystem.FrameBufferSize], out var frameNumber));
        Assert.Equal(1UL, frameNumber);
    }
}
