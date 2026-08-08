namespace SR.Emulation.Nes;

public record struct PpuStatus
{
    public byte Value;
    public bool SpriteOverflow { readonly get => (Value & 0x20) != 0; set => Value = Set(Value, 0x20, value); }
    public bool Sprite0Hit { readonly get => (Value & 0x40) != 0; set => Value = Set(Value, 0x40, value); }
    public bool VBlank { readonly get => (Value & 0x80) != 0; set => Value = Set(Value, 0x80, value); }

    private static byte Set(byte value, byte mask, bool enabled) =>
        enabled ? (byte)(value | mask) : (byte)(value & ~mask);
}
