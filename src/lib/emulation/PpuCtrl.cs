using System.Runtime.InteropServices;

namespace SR.Emulation.Nes;

[StructLayout(LayoutKind.Explicit)]
public record struct PpuCtrl
{
    [FieldOffset(0)]
    public byte Value;

    [FieldOffset(0)]
    private readonly byte _nametableAddress; // NN
    [FieldOffset(2)]
    public readonly bool VramIncrement; // I
    [FieldOffset(3)]
    public readonly bool SpritePatternTableAddress; // S
    [FieldOffset(4)]
    public readonly bool BackgroundPatternTableAddress; // B
    [FieldOffset(5)]
    public readonly bool SpriteSize; // H
    [FieldOffset(6)]
    public readonly bool PpuMasterSlaveSelect; // P
    [FieldOffset(7)]
    public readonly bool VBlankNmiEnable; // V

    public readonly ushort BaseNametableAddress => (ushort)(0x2000 + (_nametableAddress & 0x3) * 0x400);
}