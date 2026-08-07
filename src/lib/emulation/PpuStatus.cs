using System.Runtime.InteropServices;

namespace SR.Emulation.Nes;

[StructLayout(LayoutKind.Explicit)]
public record struct PpuStatus
{
    [FieldOffset(0)]
    public byte Value;

    [FieldOffset(5)]
    public bool SpriteOverflow;

    [FieldOffset(6)]
    public bool Sprite0Hit;

    [FieldOffset(7)]
    public bool VBlank;
}
