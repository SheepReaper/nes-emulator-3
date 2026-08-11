using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class PpuOddFrameTimingTests
{
    [Fact]
    public void NtscOddRenderedFrameSkipsOnePpuClock()
    {
        var nes = new NesSystem();
        nes.LoadRom(PpuTestHelper.CreateSolidBackgroundRom());
        var frames = 0;
        nes.FrameReady += (_, _) => frames++;
        while (frames < 1)
        {
            nes.Clock();
        }

        var clocks = 0;
        while (frames < 3)
        {
            nes.Clock();
            clocks++;
        }

        Assert.Equal((341 * 262 * 2) - 1, clocks);
    }
}
