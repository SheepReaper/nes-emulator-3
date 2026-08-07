namespace SR.Emulation.Nes.Abtractions;

public interface IBus
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
}
