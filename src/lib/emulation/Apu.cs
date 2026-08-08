using System;

using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;

public sealed class Apu(InterruptLines interrupts, ApuRegion? region = null) : IBusDevice
{
    private readonly InterruptLines _interrupts = interrupts;
    private readonly byte[] _registers = new byte[0x18];

    public ApuRegion Region { get; } = region ?? ApuRegion.Default;

    public byte Read(ushort address)
    {
        // Stub until channel/status emulation is implemented. Only $4015 is
        // CPU-readable; returning zero represents no active channels or IRQs.
        return 0;
    }

    public void Write(ushort address, byte value)
    {
        if (address is >= 0x4000 and <= 0x4017) _registers[address - 0x4000] = value;
    }

    internal void Reset() => Array.Clear(_registers, 0, _registers.Length);

    internal ApuDebugState CaptureDebugState() =>
        new(false, new ReadOnlyMemory<byte>((byte[])_registers.Clone()));

    internal byte Peek(ushort address) =>
        address is >= 0x4000 and <= 0x4017 ? _registers[address - 0x4000] : (byte)0;
}
