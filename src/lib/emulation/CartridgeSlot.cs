namespace SR.Emulation.Nes;

public sealed class CartridgeSlot
{
    public Cartridge? Cartridge { get; private set; }

    public void Insert(Cartridge cartridge)
    {
        Cartridge = cartridge;
    }

    public void Eject()
    {
        Cartridge = null;
    }

    public byte CpuRead(ushort address)
    {
        return Cartridge?.CpuRead(address) ?? 0;
    }

    public void CpuWrite(ushort address, byte value)
    {
        Cartridge?.CpuWrite(address, value);
    }

    public byte PpuRead(ushort address)
    {
        return Cartridge?.PpuRead(address) ?? 0;
    }

    public void PpuWrite(ushort address, byte value)
    {
        Cartridge?.PpuWrite(address, value);
    }
}
