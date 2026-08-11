using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class AudioTests
{
    [Fact]
    public void StatusReportsLengthCountersAndDisablingAChannelClearsItsLength()
    {
        var apu = new Apu(new InterruptLines());
        apu.Write(0x4015, 0x01);
        apu.Write(0x4003, 0x00);
        apu.Clock();

        Assert.Equal(0x01, apu.Read(0x4015) & 0x01);

        apu.Write(0x4015, 0x00);
        Assert.Equal(0, apu.Read(0x4015) & 0x01);
    }

    [Fact]
    public void FourStepSequencerRaisesFrameIrqAndStatusReadClearsOnlyThatFlag()
    {
        var interrupts = new InterruptLines();
        var apu = new Apu(interrupts);

        for (var cycle = 0; cycle < 29_829; cycle++)
        {
            apu.Clock();
        }

        Assert.True(interrupts.ApuFrameIrq);
        apu.Clock();
        apu.Clock();
        Assert.Equal(0x40, apu.Read(0x4015) & 0x40);
        apu.Clock();
        apu.Clock();
        Assert.False(interrupts.ApuFrameIrq);
    }
}
