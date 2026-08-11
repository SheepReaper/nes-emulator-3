using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class PpuTimingAndInterruptTests
{
    [Fact]
    public void StatusReadOneDotBeforeVblankDoesNotSuppressThatFramesFlag()
    {
        var ppu = PpuTestHelper.CreatePpu(out _);
        for (var i = 0; i < 241 * 341; i++)
        {
            ppu.Clock();
        }

        Assert.Equal(0, ppu.Read(0x2002) & 0x80);
        ppu.Clock();
        ppu.Clock();
        Assert.NotEqual(0, ppu.Read(0x2002) & 0x80);
    }

    [Fact]
    public void StatusReadOnTheVblankSetDotSuppressesThatFramesFlag()
    {
        var ppu = PpuTestHelper.CreatePpu(out _);
        for (var i = 0; i < (241 * 341) + 1; i++)
        {
            ppu.Clock();
        }

        Assert.Equal(0, ppu.Read(0x2002) & 0x80);
        ppu.Clock();

        Assert.Equal(0, ppu.Read(0x2002) & 0x80);
    }

    [Fact]
    public void EnablingNmiDuringVblankRaisesNmiAndStatusReadClearsIt()
    {
        var interrupts = new InterruptLines();
        var ppu = new Ppu(interrupts);
        ppu.ConnectBus(new MemoryBus());
        ppu.Reset();
        for (var i = 0; i < (241 * 341) + 2; i++)
        {
            ppu.Clock();
        }

        ppu.Write(0x2000, 0x80);
        Assert.True(interrupts.Nmi);
        Assert.NotEqual(0, ppu.Read(0x2002) & 0x80);
        Assert.False(interrupts.Nmi);
    }

    [Fact]
    public void VblankNmiOutputIsDelayedTwoPpuDotsAfterTheStatusFlagIsSet()
    {
        var interrupts = new InterruptLines();
        var ppu = new Ppu(interrupts);
        ppu.ConnectBus(new MemoryBus());
        ppu.Reset();
        ppu.Write(0x2000, 0x80);
        for (var i = 0; i < (241 * 341) + 2; i++)
        {
            ppu.Clock();
        }

        Assert.False(interrupts.Nmi);
        ppu.Clock();
        Assert.False(interrupts.Nmi);
        ppu.Clock();
        Assert.True(interrupts.Nmi);
    }

    [Fact]
    public void OddFrameSkipSamplesRenderingEnableOneDotBeforeTheSkip()
    {
        var ppu = PpuTestHelper.CreatePpu(out _);
        PpuReflectionHelper.SetPrivateField(ppu, "_oddFrame", true);
        PpuReflectionHelper.SetPrivateField(ppu, "_scanline", 261);
        PpuReflectionHelper.SetPrivateField(ppu, "_cycle", 338);

        ppu.Clock();
        ppu.Write(0x2001, 0x08);
        ppu.Clock();

        Assert.Equal(261, PpuReflectionHelper.GetPrivateField<int>(ppu, "_scanline"));
        Assert.Equal(340, PpuReflectionHelper.GetPrivateField<int>(ppu, "_cycle"));
    }

    [Fact]
    public void OddFrameSkipStillOccursWhenRenderingIsDisabledAfterItsSampleDot()
    {
        var ppu = PpuTestHelper.CreatePpu(out _);
        PpuReflectionHelper.SetPrivateField(ppu, "_oddFrame", true);
        PpuReflectionHelper.SetPrivateField(ppu, "_scanline", 261);
        PpuReflectionHelper.SetPrivateField(ppu, "_cycle", 338);
        ppu.Write(0x2001, 0x08);

        ppu.Clock();
        ppu.Write(0x2001, 0x00);
        ppu.Clock();

        Assert.Equal(0, PpuReflectionHelper.GetPrivateField<int>(ppu, "_scanline"));
        Assert.Equal(0, PpuReflectionHelper.GetPrivateField<int>(ppu, "_cycle"));
    }
}
