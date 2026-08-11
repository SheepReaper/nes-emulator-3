namespace Sheep.Emulation.Nes.Video;

internal struct SpriteRenderData(
    byte x, byte attributes, ushort patternAddress, bool isSpriteZero)
{
    public byte X { get; } = x;
    public byte Attributes { get; } = attributes;
    public ushort PatternAddress { get; } = patternAddress;
    public byte PatternLow { get; set; }
    public byte PatternHigh { get; set; }
    public bool IsSpriteZero { get; } = isSpriteZero;
}