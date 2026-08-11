namespace Sheep.Emulation.Nes.Video;

internal sealed class FrameSlot
{
    public byte[] Pixels { get; } = new byte[Ppu.FrameBufferSize];
    public int LeaseCount { get; set; }
}