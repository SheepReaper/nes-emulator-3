namespace Sheep.Emulation.Nes.Buses;

public interface IBusDevice
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
}