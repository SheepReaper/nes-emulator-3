namespace Sheep.Emulation.Nes.Cpu;

internal sealed class CpuOamDmaState
{
    public bool Pending;
    public bool Active;
    public byte Page;
    public int Index;
    public int StartupCycles;
    public bool ReadPhase;
    public bool Realign;
    public byte Latch;

    public void Trigger(byte page)
    {
        Page = page;
        Pending = true;
    }

    public bool ClockCycle(CpuBus bus, Ppu ppu)
    {
        if (StartupCycles > 0)
        {
            StartupCycles--;
            if (bus.HasCpuReadAddress)
            {
                _ = bus.Read(bus.LastCpuReadAddress);
            }
            return true;
        }

        if (ReadPhase)
        {
            Latch = bus.ReadOamDmaSource((ushort)((Page << 8) | Index));
            ReadPhase = false;
        }
        else
        {
            ppu.DmaWriteByte(Latch);
            Index++;
            ReadPhase = true;
            if (Index == 256)
            {
                Active = false;
            }
        }
        return true;
    }
}
