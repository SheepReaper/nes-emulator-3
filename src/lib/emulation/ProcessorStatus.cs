using System.Runtime.InteropServices;

namespace SR.Emulation.Nes;

[StructLayout(LayoutKind.Explicit)]
public record struct ProcessorStatus
{
    [FieldOffset(0)]
    public byte Value;

    [FieldOffset(0)]
    public bool Carry; // C
    [FieldOffset(1)]
    public bool Zero; // Z
    [FieldOffset(2)]
    public bool InterruptDisable; // I
    [FieldOffset(3)]
    public bool Decimal; // D
    [FieldOffset(4)]
    public bool Break; // B
    [FieldOffset(6)]
    public bool Overflow; // V
    [FieldOffset(7)]
    public bool Negative; // N
}