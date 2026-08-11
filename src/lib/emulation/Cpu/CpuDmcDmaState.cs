using System;

namespace Sheep.Emulation.Nes.Cpu;

internal sealed class CpuDmcDmaState
{
    public bool Pending;
    public bool HaltLatched;
    public int Cycles;
    public ushort Address;
    public Action<byte>? Completed;
    public bool ControllerReadClocked;

    public void Request(ushort address, Action<byte>? completed)
    {
        if (Pending)
        {
            return;
        }
        Pending = true;
        HaltLatched = false;
        Cycles = completed is null ? 1 : 0;
        Address = address;
        Completed = completed;
        ControllerReadClocked = false;
    }

    public void Abort()
    {
        if (Completed is null)
        {
            Pending = false;
            HaltLatched = false;
            Cycles = 0;
            Completed = null;
            ControllerReadClocked = false;
        }
        else if (Cycles is 1)
        {
            Completed = null;
            ControllerReadClocked = false;
        }
        else if (Cycles == 0 && HaltLatched)
        {
            Cycles = 1;
            Completed = null;
            ControllerReadClocked = false;
        }
        else
        {
            Pending = false;
            Completed = null;
            ControllerReadClocked = false;
        }
    }
}
