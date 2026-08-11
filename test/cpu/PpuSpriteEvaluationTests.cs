using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class PpuSpriteEvaluationTests
{
    [Fact]
    public void MoreThanEightSpritesOnTheNextScanlineSetsOverflow()
    {
        var ppu = PpuTestHelper.CreatePpu(out _);
        PpuTestHelper.FillOam(ppu, 0xFF);
        ppu.Write(0x2003, 0);
        for (var sprite = 0; sprite < 9; sprite++)
        {
            ppu.Write(0x2004, 9);
            ppu.Write(0x2004, 0);
            ppu.Write(0x2004, 0);
            ppu.Write(0x2004, (byte)(sprite * 8));
        }
        ppu.Write(0x2001, 0x18);

        for (var i = 0; i < (9 * 341) + 258; i++)
        {
            ppu.Clock();
        }

        Assert.NotEqual(0, ppu.Read(0x2002) & 0x20);
    }

    [Fact]
    public void OpaqueSpriteZeroOverOpaqueBackgroundSetsSpriteZeroHit()
    {
        var ppu = PpuTestHelper.CreatePpu(out var bus);
        for (var row = 0; row < 8; row++)
        {
            bus.Memory[row] = 0xFF;
        }
        PpuTestHelper.FillOam(ppu, 0xFF);
        ppu.Write(0x2003, 0);
        ppu.Write(0x2004, 9);
        ppu.Write(0x2004, 0);
        ppu.Write(0x2004, 0);
        ppu.Write(0x2004, 20);
        ppu.Write(0x2001, 0x1E);

        for (var i = 0; i < (11 * 341); i++)
        {
            ppu.Clock();
        }

        Assert.NotEqual(0, ppu.Read(0x2002) & 0x40);
    }

    [Fact]
    public void BackgroundPrefetch_RendersTheFirstVisibleTileFromTheCurrentNametableRow()
    {
        var ppu = PpuTestHelper.CreatePpu(out var bus);
        for (var index = 0; index < 30 * 32; index++)
        {
            bus.Memory[0x2000 + index] = 1;
        }
        for (var row = 0; row < 30; row++)
        {
            bus.Memory[0x2000 + (row * 32) + 1] = 2;
            bus.Memory[0x2000 + (row * 32) + 31] = 3;
        }
        for (var patternRow = 0; patternRow < 8; patternRow++)
        {
            bus.Memory[0x0010 + patternRow] = 0xFF;
            bus.Memory[0x0028 + patternRow] = 0xFF;
            bus.Memory[0x0030 + patternRow] = 0xFF;
            bus.Memory[0x0038 + patternRow] = 0xFF;
        }
        bus.Memory[0x3F00] = 0x0F;
        bus.Memory[0x3F01] = 0x30;
        bus.Memory[0x3F02] = 0x16;
        bus.Memory[0x3F03] = 0x12;
        ppu.Write(0x2001, 0x0A);
        ppu.Write(0x2005, 3);
        ppu.Write(0x2005, 0);

        for (var clock = 0; clock < 341 * 262 * 2; clock++)
        {
            ppu.Clock();
        }

        var frame = new byte[NesSystem.FrameBufferSize];
        Assert.True(ppu.TryCopyFrame(frame, out _));
        var firstPixel = frame.AsSpan((20 * NesSystem.FrameWidth) * 4, 4).ToArray();
        var firstPixelOfSecondTile = frame.AsSpan(((20 * NesSystem.FrameWidth) + 5) * 4, 4).ToArray();
        var knownFirstTilePixel = frame.AsSpan(((20 * NesSystem.FrameWidth) + 16) * 4, 4).ToArray();
        var knownSecondTilePixel = frame.AsSpan(((20 * NesSystem.FrameWidth) + 8) * 4, 4).ToArray();
        Assert.Equal(knownFirstTilePixel, firstPixel);
        Assert.Equal(knownSecondTilePixel, firstPixelOfSecondTile);
        Assert.NotEqual(firstPixel, firstPixelOfSecondTile);
    }
}
