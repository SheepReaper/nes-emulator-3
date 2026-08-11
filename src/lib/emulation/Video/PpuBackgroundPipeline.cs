namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Shift registers and pipeline latches for PPU background rendering.
/// </summary>
internal sealed class PpuBackgroundPipeline
{
    private ushort _patternLowShift;
    private ushort _patternHighShift;
    private ushort _attributeLowShift;
    private ushort _attributeHighShift;
    private byte _nextTileId;
    private byte _nextTileAttribute;
    private byte _nextTileLow;
    private byte _nextTileHigh;

    internal byte NextTileId { get => _nextTileId; set => _nextTileId = value; }
    internal byte NextTileAttribute { get => _nextTileAttribute; set => _nextTileAttribute = value; }
    internal byte NextTileLow { get => _nextTileLow; set => _nextTileLow = value; }
    internal byte NextTileHigh { get => _nextTileHigh; set => _nextTileHigh = value; }

    internal void Reset()
    {
        _patternLowShift = 0;
        _patternHighShift = 0;
        _attributeLowShift = 0;
        _attributeHighShift = 0;
        _nextTileId = 0;
        _nextTileAttribute = 0;
        _nextTileLow = 0;
        _nextTileHigh = 0;
    }

    internal void Shift()
    {
        _patternLowShift <<= 1;
        _patternHighShift <<= 1;
        _attributeLowShift <<= 1;
        _attributeHighShift <<= 1;
    }

    internal void Load()
    {
        _patternLowShift = (ushort)((_patternLowShift & 0xFF00) | _nextTileLow);
        _patternHighShift = (ushort)((_patternHighShift & 0xFF00) | _nextTileHigh);
        _attributeLowShift = (ushort)((_attributeLowShift & 0xFF00) |
            ((_nextTileAttribute & 0x01) != 0 ? 0xFF : 0x00));
        _attributeHighShift = (ushort)((_attributeHighShift & 0xFF00) |
            ((_nextTileAttribute & 0x02) != 0 ? 0xFF : 0x00));
    }

    internal (int Pixel, int Palette) SamplePixel(byte fineXScroll)
    {
        var mux = (ushort)(0x8000 >> fineXScroll);
        var pixel = ((_patternHighShift & mux) != 0 ? 2 : 0) |
                    ((_patternLowShift & mux) != 0 ? 1 : 0);
        var palette = ((_attributeHighShift & mux) != 0 ? 2 : 0) |
                      ((_attributeLowShift & mux) != 0 ? 1 : 0);
        return (pixel, palette);
    }
}
