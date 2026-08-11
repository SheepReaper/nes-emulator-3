using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class PpuRenderingAndSpriteTests
{
    [Fact]
    public void MapperZeroBackgroundRendersThroughThePublicFrameApi()
    {
        var nes = new NesSystem();
        nes.LoadRom(PpuTestHelper.CreateSolidBackgroundRom());
        var completed = false;
        nes.FrameReady += (_, _) => completed = true;

        for (var i = 0; i < 341 * 262 && !completed; i++)
        {
            nes.Clock();
        }

        var pixels = new byte[NesSystem.FrameBufferSize];
        Assert.True(nes.TryCopyFrame(pixels, out _));
        var offset = ((20 * NesSystem.FrameWidth) + 20) * NesSystem.BytesPerPixel;
        Assert.Equal(236, pixels[offset]);
        Assert.Equal(238, pixels[offset + 1]);
        Assert.Equal(236, pixels[offset + 2]);
        Assert.Equal(255, pixels[offset + 3]);
    }

    [Fact]
    public void OamDmaSpriteRendersThroughThePublicFrameApi()
    {
        var nes = new NesSystem();
        nes.LoadRom(PpuTestHelper.CreateSpriteRom());
        var completed = false;
        nes.FrameReady += (_, _) => completed = true;

        for (var i = 0; i < 341 * 262 && !completed; i++)
        {
            nes.Clock();
        }

        var pixels = new byte[NesSystem.FrameBufferSize];
        Assert.True(nes.TryCopyFrame(pixels, out _));
        var offset = ((32 * NesSystem.FrameWidth) + 42) * NesSystem.BytesPerPixel;
        Assert.Equal(152, pixels[offset]);
        Assert.Equal(34, pixels[offset + 1]);
        Assert.Equal(32, pixels[offset + 2]);
    }

    [Theory]
    [InlineData(0x0B, 0x16, 152, 150, 152)]
    [InlineData(0x2A, 0x30, 236, 178, 177)]
    public void PpuMaskColorEffectsAreAppliedToRgbaOutput(
        byte mask, byte paletteColor, byte red, byte green, byte blue)
    {
        var nes = new NesSystem();
        nes.LoadRom(PpuTestHelper.CreateSolidBackgroundRom(mask, paletteColor));
        var completed = false;
        nes.FrameReady += (_, _) => completed = true;
        for (var i = 0; i < 341 * 262 && !completed; i++)
        {
            nes.Clock();
        }

        var pixels = new byte[NesSystem.FrameBufferSize];
        Assert.True(nes.TryCopyFrame(pixels, out _));
        var offset = ((20 * NesSystem.FrameWidth) + 20) * NesSystem.BytesPerPixel;
        Assert.Equal(red, pixels[offset]);
        Assert.Equal(green, pixels[offset + 1]);
        Assert.Equal(blue, pixels[offset + 2]);
    }

    [Fact]
    public void PalUsesItsOwnPaletteAndBlackPictureBorder()
    {
        var nes = new NesSystem(NesVideoStandard.Pal);
        nes.LoadRom(PpuTestHelper.CreateSolidBackgroundRom());
        var completed = false;
        nes.FrameReady += (_, _) => completed = true;
        while (!completed)
        {
            nes.Clock();
        }

        var pixels = new byte[NesSystem.FrameBufferSize];
        Assert.True(nes.TryCopyFrame(pixels, out _));
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, pixels.AsSpan(0, 4).ToArray());
        var content = ((20 * NesSystem.FrameWidth) + 20) * 4;
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, pixels.AsSpan(content, 4).ToArray());
    }
}
