namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Mask and color emphasis / display flags for PPU ($2001).
/// </summary>
internal sealed class PpuMaskSettings
{
    internal bool RenderingEnabled { get; set; }
    internal bool ShowBackground { get; set; }
    internal bool ShowSprites { get; set; }
    internal bool ShowBackgroundLeft { get; set; }
    internal bool ShowSpritesLeft { get; set; }
    internal bool Grayscale { get; set; }
    internal int PaletteEmphasisOffset { get; set; }

    internal void Reset()
    {
        RenderingEnabled = false;
        ShowBackground = false;
        ShowSprites = false;
        ShowBackgroundLeft = false;
        ShowSpritesLeft = false;
        Grayscale = false;
        PaletteEmphasisOffset = 0;
    }

    internal void UpdateFromMask(byte value)
    {
        RenderingEnabled = (value & 0x18) != 0;
        ShowBackground = (value & 0x08) != 0;
        ShowSprites = (value & 0x10) != 0;
        ShowBackgroundLeft = (value & 0x02) != 0;
        ShowSpritesLeft = (value & 0x04) != 0;
        Grayscale = (value & 0x01) != 0;
        PaletteEmphasisOffset = ((value >> 5) & 0x07) * 64;
    }
}
