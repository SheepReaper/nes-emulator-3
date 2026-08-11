using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class PpuTests
{
    [Fact]
    public void RegisterBitfieldsReflectTheirBackingByte()
    {
        var control = new PpuCtrl { Value = 0xBF };
        Assert.True(control.VramIncrement);
        Assert.True(control.SpritePatternTableAddress);
        Assert.True(control.BackgroundPatternTableAddress);
        Assert.True(control.SpriteSize);
        Assert.False(control.PpuMasterSlaveSelect);
        Assert.True(control.VBlankNmiEnable);
        Assert.Equal(0x2C00, control.BaseNametableAddress);

        var mask = new PpuMask { Value = 0xFF };
        Assert.True(mask.Grayscale);
        Assert.True(mask.ShowBackgroundLeft);
        Assert.True(mask.ShowSpritesLeft);
        Assert.True(mask.ShowBackground);
        Assert.True(mask.ShowSprites);
        Assert.True(mask.EmphasizeRed);
        Assert.True(mask.EmphasizeGreen);
        Assert.True(mask.EmphasizeBlue);

        var status = new PpuStatus { Value = 0xE0 };
        Assert.True(status.SpriteOverflow);
        Assert.True(status.Sprite0Hit);
        Assert.True(status.VBlank);
        status.Sprite0Hit = false;
        Assert.Equal(0xA0, status.Value);
    }

    [Fact]
    public void NesExposesPortableFrameDescription()
    {
        var nes = new NesSystem(NesVideoStandard.Pal);
        Assert.Equal(NesVideoStandard.Pal, nes.VideoStandard);
        Assert.Equal(256, NesSystem.FrameWidth);
        Assert.Equal(240, NesSystem.FrameHeight);
        Assert.Equal(4, NesSystem.BytesPerPixel);
        Assert.Equal(256 * 240 * 4, NesSystem.FrameBufferSize);
    }

    [Fact]
    public void TryCopyFrameReturnsFalseUntilAFrameIsPublished()
    {
        var nes = new NesSystem();
        var pixels = new byte[NesSystem.FrameBufferSize];
        Assert.False(nes.TryCopyFrame(pixels, out var frameNumber));
        Assert.Equal(0UL, frameNumber);
    }

    [Fact]
    public void TryCopyFrameRejectsAnUndersizedDestination()
    {
        var nes = new NesSystem();
        Assert.Throws<ArgumentException>(() => nes.TryCopyFrame(new byte[NesSystem.FrameBufferSize - 1], out _));
    }

    [Theory]
    [InlineData(NesVideoStandard.Ntsc, 16UL)]
    [InlineData(NesVideoStandard.Pal, 15UL)]
    public void VideoStandardUsesTheCorrectCpuToPpuClockRatio(NesVideoStandard standard, ulong expectedCpuClocks)
    {
        var nes = new NesSystem(standard);
        for (var i = 0; i < 48; i++)
        {
            nes.Clock();
        }
        Assert.Equal(expectedCpuClocks, nes.CpuClockCounter);
    }

    public static TheoryData<NesVideoStandard, Type, int, int, int, int, ApuRegion> TimingCases => new()
    {
        { NesVideoStandard.Ntsc, typeof(NtscTiming), 21_477_272, 12, 4, 262, ApuRegion.Ntsc },
        { NesVideoStandard.Pal, typeof(PalTiming), 26_601_712, 16, 5, 312, ApuRegion.Pal }
    };

    [Theory]
    [MemberData(nameof(TimingCases))]
    public void VideoStandardSelectsTheSharedTimingProfile(
        NesVideoStandard standard,
        Type timingType,
        int masterClockHz,
        int cpuDivisor,
        int ppuDivisor,
        int scanlines,
        ApuRegion apuRegion)
    {
        var nes = new NesSystem(standard);
        Assert.IsType(timingType, nes.Timing);
        Assert.Equal(masterClockHz, nes.Timing.MasterClockHz);
        Assert.Equal(cpuDivisor, nes.Timing.CpuDivisor);
        Assert.Equal(ppuDivisor, nes.Timing.PpuDivisor);
        Assert.Equal(scanlines, nes.Timing.ScanlinesPerFrame);
        Assert.Equal(341, nes.Timing.DotsPerScanline);
        Assert.Equal(apuRegion, nes.Timing.ApuRegion);
    }
}
