namespace Sheep.Emulation.Nes.Video;

public record struct PpuCtrl
{
    public byte Value;
    public readonly bool VramIncrement => (Value & 0x04) != 0;
    public readonly bool SpritePatternTableAddress => (Value & 0x08) != 0;
    public readonly bool BackgroundPatternTableAddress => (Value & 0x10) != 0;
    public readonly bool SpriteSize => (Value & 0x20) != 0;
    public readonly bool PpuMasterSlaveSelect => (Value & 0x40) != 0;
    public readonly bool VBlankNmiEnable => (Value & 0x80) != 0;
    public readonly ushort BaseNametableAddress => (ushort)(0x2000 + (Value & 0x03) * 0x0400);
}