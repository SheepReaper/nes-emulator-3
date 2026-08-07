using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;

public sealed class Apu(InterruptLines interrupts) : IBusDevice
{
    public byte Read(ushort address)
    {
        throw new System.NotImplementedException();
    }

    public void Write(ushort address, byte value)
    {
        throw new System.NotImplementedException();
    }
}
