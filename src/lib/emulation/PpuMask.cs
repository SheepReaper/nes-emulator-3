using System.Runtime.InteropServices;

namespace SR.Emulation.Nes;

[StructLayout(LayoutKind.Explicit)]
public record struct PpuMask
{
    [FieldOffset(0)]
    public byte Value;

    [FieldOffset(0)]
    public readonly bool Grayscale; // g
    [FieldOffset(1)]
    public readonly bool ShowBackgroundLeft; // m
    [FieldOffset(2)]
    public readonly bool ShowSpritesLeft; // M
    [FieldOffset(3)]
    public readonly bool ShowBackground; // b
    [FieldOffset(4)]
    public readonly bool ShowSprites; // s
    [FieldOffset(5)]
    public readonly bool EmphasizeRed; // R
    [FieldOffset(6)]
    public readonly bool EmphasizeGreen; // G
    [FieldOffset(7)]
    public readonly bool EmphasizeBlue; // B
}