namespace Sheep.Emulation.Nes.Buses;

public interface IBus
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
}