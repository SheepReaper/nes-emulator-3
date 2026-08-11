namespace Sheep.Emulation.Nes.Cpu;

public record struct ProcessorStatus
{
    public byte Value;

    private const byte C_MASK = 1 << 0;
    private const byte Z_MASK = 1 << 1;
    private const byte I_MASK = 1 << 2;
    private const byte D_MASK = 1 << 3;
    private const byte B_MASK = 1 << 4;
    private const byte U_MASK = 1 << 5; // Unused
    private const byte V_MASK = 1 << 6;
    private const byte N_MASK = 1 << 7;

    public bool Carry { readonly get => (Value & C_MASK) != 0; set => Value = (byte)(value ? Value | C_MASK : Value & ~C_MASK); }
    public bool Zero { readonly get => (Value & Z_MASK) != 0; set => Value = (byte)(value ? Value | Z_MASK : Value & ~Z_MASK); }
    public bool InterruptDisable { readonly get => (Value & I_MASK) != 0; set => Value = (byte)(value ? Value | I_MASK : Value & ~I_MASK); }
    public bool Decimal { readonly get => (Value & D_MASK) != 0; set => Value = (byte)(value ? Value | D_MASK : Value & ~D_MASK); }
    public bool Break { readonly get => (Value & B_MASK) != 0; set => Value = (byte)(value ? Value | B_MASK : Value & ~B_MASK); }
    public bool Overflow { readonly get => (Value & V_MASK) != 0; set => Value = (byte)(value ? Value | V_MASK : Value & ~V_MASK); }
    public bool Negative { readonly get => (Value & N_MASK) != 0; set => Value = (byte)(value ? Value | N_MASK : Value & ~N_MASK); }

    public ProcessorStatus()
    {
        // Ensure unused bit 5 is always set
        Value = U_MASK;
    }
}