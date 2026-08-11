using System;

namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// OAM sprite evaluation, pattern fetching, and pixel composition.
/// </summary>
internal sealed class PpuSpriteRenderer
{
    private readonly SpriteRenderData[] _renderSprites = new SpriteRenderData[8];
    private readonly SpriteRenderData[] _evaluatedSprites = new SpriteRenderData[8];
    private int _renderSpriteCount;
    private int _evaluatedSpriteCount;
    private int _overflowDot = -1;

    internal int SpriteCount => _renderSpriteCount;
    internal int OverflowDot => _overflowDot;

    internal void Reset()
    {
        _renderSpriteCount = 0;
        _evaluatedSpriteCount = 0;
        _overflowDot = -1;
    }

    internal void BeginScanline()
    {
        Array.Copy(_evaluatedSprites, _renderSprites, 8);
        _renderSpriteCount = _evaluatedSpriteCount;
    }

    internal bool EvaluateSprites(
        PpuOam oam,
        int scanline,
        int scanlinesPerFrame,
        bool spriteSize16,
        bool spritePatternTableAddress)
    {
        var nextScanline = scanline == scanlinesPerFrame - 1 ? 0 : scanline + 1;
        if (nextScanline > Ppu.FrameHeight)
        {
            _evaluatedSpriteCount = 0;
            _overflowDot = -1;
            return false;
        }

        return PpuSpriteEvaluation.Evaluate(
            oam,
            nextScanline,
            spriteSize16,
            spritePatternTableAddress,
            _evaluatedSprites,
            out _evaluatedSpriteCount,
            out _overflowDot);
    }

    internal void FetchSpritePatterns(int cycle, bool spriteSize16, bool spritePatternTableAddress, Func<ushort, byte> readBus)
    {
        var slot = (cycle - 257) / 8;
        var phase = (cycle - 257) & 0x07;
        if (slot >= _evaluatedSpriteCount)
        {
            if (phase is 3 or 5)
            {
                var dummyPatternAddress = PpuSpriteAddress.GetAddress(0xFF, 0, spriteSize16, spritePatternTableAddress);
                _ = readBus((ushort)(dummyPatternAddress + (phase == 5 ? 8 : 0)));
            }
            return;
        }

        if (phase == 3)
        {
            _evaluatedSprites[slot].PatternLow = readBus(_evaluatedSprites[slot].PatternAddress);
        }
        if (phase == 5)
        {
            _evaluatedSprites[slot].PatternHigh = readBus((ushort)(_evaluatedSprites[slot].PatternAddress + 8));
        }
    }

    internal (int Pixel, int Palette, bool BehindBackground, bool IsSpriteZero) SamplePixel(int x)
    {
        for (var i = 0; i < _renderSpriteCount; i++)
        {
            ref readonly var sprite = ref _renderSprites[i];
            var offset = x - sprite.X;
            if ((uint)offset >= 8)
            {
                continue;
            }

            var bit = (sprite.Attributes & 0x40) != 0 ? offset : 7 - offset;
            var pixel = (((sprite.PatternHigh >> bit) & 1) << 1) | ((sprite.PatternLow >> bit) & 1);
            if (pixel == 0)
            {
                continue;
            }

            return (pixel, sprite.Attributes & 0x03, (sprite.Attributes & 0x20) != 0, sprite.IsSpriteZero);
        }

        return (0, 0, false, false);
    }
}
