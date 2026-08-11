namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInterruptHandler
{
    internal static void HandleNmi(Cpu cpu, CpuState s, InterruptLines interrupts)
    {
        CpuStackOperations.Push(cpu, s, (byte)(s.ProgramCounter >> 8));
        CpuStackOperations.Push(cpu, s, (byte)(s.ProgramCounter & 0xFF));
        CpuStackOperations.Push(cpu, s, (byte)((s.P.Value | 0b00100000) & ~0b00010000));
        s.P.InterruptDisable = true;
        s.NmiPending = false;
        interrupts.Nmi = false;
        s.ProgramCounter = CpuStackOperations.ReadWord(cpu, 0xFFFA);
        s.Cycles = 7;
    }

    internal static void HandleIrq(Cpu cpu, CpuState s, InterruptLines interrupts)
    {
        CpuStackOperations.Push(cpu, s, (byte)(s.ProgramCounter >> 8));
        CpuStackOperations.Push(cpu, s, (byte)(s.ProgramCounter & 0xFF));
        CpuStackOperations.Push(cpu, s, (byte)((s.P.Value | 0b00100000) & ~0b00010000));
        s.P.InterruptDisable = true;
        ushort vector = s.NmiPending ? (ushort)0xFFFA : (ushort)0xFFFE;
        if (s.NmiPending)
        {
            s.NmiPending = false;
            interrupts.Nmi = false;
        }
        s.ProgramCounter = CpuStackOperations.ReadWord(cpu, vector);
        s.Cycles = 7;
        s.IrqInProgress = true;
    }
}
