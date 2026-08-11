namespace Sheep.Emulation.Nes.Video;

public record struct PpuMask
{
    public byte Value;
    public readonly bool Grayscale => (Value & 0x01) != 0;
    public readonly bool ShowBackgroundLeft => (Value & 0x02) != 0;
    public readonly bool ShowSpritesLeft => (Value & 0x04) != 0;
    public readonly bool ShowBackground => (Value & 0x08) != 0;
    public readonly bool ShowSprites => (Value & 0x10) != 0;
    public readonly bool EmphasizeRed => (Value & 0x20) != 0;
    public readonly bool EmphasizeGreen => (Value & 0x40) != 0;
    public readonly bool EmphasizeBlue => (Value & 0x80) != 0;
}