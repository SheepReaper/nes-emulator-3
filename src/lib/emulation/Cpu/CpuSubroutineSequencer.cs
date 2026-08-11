namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuSubroutineSequencer
{
    internal static void StepJsr(Cpu cpu, CpuState s)
    {
        if (s.Cycles == 6) s.JsrTargetLow = cpu.Read(s.ProgramCounter++);
        if (s.Cycles == 5) _ = cpu.Read((ushort)(0x0100 | s.SP));
        if (s.Cycles == 4) s.JsrReturnAddress = s.ProgramCounter;
        if (s.Cycles == 3) CpuStackOperations.Push(cpu, s, (byte)(s.JsrReturnAddress >> 8));
        if (s.Cycles == 2) CpuStackOperations.Push(cpu, s, (byte)(s.JsrReturnAddress & 0xFF));
        if (s.Cycles == 1)
        {
            var targetHigh = cpu.Read(s.ProgramCounter);
            s.ProgramCounter = (ushort)((targetHigh << 8) | s.JsrTargetLow);
            s.JsrInProgress = false;
        }
    }

    internal static void StepRts(Cpu cpu, CpuState s)
    {
        if (s.Cycles == 5) _ = cpu.Read(s.ProgramCounter);
        if (s.Cycles == 4) _ = cpu.Read((ushort)(0x0100 | s.SP));
        if (s.Cycles == 3) s.RtsTargetLow = cpu.Read((ushort)(0x0100 | ++s.SP));
        if (s.Cycles == 2)
        {
            var targetHigh = cpu.Read((ushort)(0x0100 | ++s.SP));
            s.ProgramCounter = (ushort)((targetHigh << 8) | s.RtsTargetLow);
        }
        if (s.Cycles == 1)
        {
            _ = cpu.Read(s.ProgramCounter);
            s.ProgramCounter++;
            s.RtsInProgress = false;
        }
    }

    internal static void StepStackPull(Cpu cpu, CpuState s)
    {
        if (s.Cycles == 3) _ = cpu.Read(s.ProgramCounter);
        if (s.Cycles == 2) _ = cpu.Read((ushort)(0x0100 | s.SP));
        if (s.Cycles == 1)
        {
            var value = CpuStackOperations.Pull(cpu, s);
            if (s.Opcode == 0x68)
            {
                s.A = value;
                s.SetZeroAndNegativeFlags(s.A);
            }
            else
            {
                s.P.Value = (byte)((value & ~0b00010000) | 0b00100000);
            }
            s.StackPullInProgress = false;
        }
    }
}
