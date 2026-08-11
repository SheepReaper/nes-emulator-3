namespace Sheep.Emulation.Nes.Cartridges;

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

    internal byte CpuReadOrOpenBus(ushort address, byte openBus) =>
        Cartridge is { } cartridge && cartridge.CpuReadDrivesDataBus(address)
            ? cartridge.CpuRead(address)
            : openBus;

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

    public byte CpuPeek(ushort address) => Cartridge?.CpuPeek(address) ?? 0;
    public byte PpuPeek(ushort address) => Cartridge?.PpuPeek(address) ?? 0;
    internal void NotifyPpuAddress(ushort address, ulong ppuCycle) =>
        Cartridge?.NotifyPpuAddress(address, ppuCycle);
    internal void NotifyCpuClock() => Cartridge?.NotifyCpuClock();
}
