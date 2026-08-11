namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionsFlow
{
    internal static void Branch(Cpu cpu, CpuState s, InterruptLines interrupts, bool condition)
    {
        bool irqOnOpcodeFetch = interrupts.Irq && !s.P.InterruptDisable;
        sbyte offset = (sbyte)cpu.Read(s.ProgramCounter++);
        if (condition)
        {
            ushort oldPc = s.ProgramCounter;
            ushort targetAddress = (ushort)(s.ProgramCounter + offset);
            _ = cpu.Read(oldPc);

            s.Cycles = 3;
            s.IrqPending = irqOnOpcodeFetch;
            s.BranchTakenNoPageCross = true;

            if ((oldPc & 0xFF00) != (targetAddress & 0xFF00))
            {
                _ = cpu.Read((ushort)((oldPc & 0xFF00) | (targetAddress & 0x00FF)));
                s.Cycles++;
                s.BranchTakenNoPageCross = false;
            }

            s.ProgramCounter = targetAddress;
        }
        else
        {
            s.Cycles = 2;
        }
    }

    internal static void JmpAbs(Cpu cpu, CpuState s)
    {
        s.ProgramCounter = CpuAddressingModes.Addr_ABS(cpu, s);
        s.Cycles = 3;
    }

    internal static void JmpInd(Cpu cpu, CpuState s)
    {
        ushort pointer = CpuAddressingModes.Addr_ABS(cpu, s);
        s.ProgramCounter = CpuStackOperations.ReadWordBug(cpu, pointer);
        s.Cycles = 5;
    }

    internal static void JsrAbs(CpuState s)
    {
        s.JsrInProgress = true;
        s.Cycles = 6;
    }

    internal static void RtsImp(CpuState s)
    {
        s.RtsInProgress = true;
        s.Cycles = 6;
    }

    internal static void PhaImp(Cpu cpu, CpuState s)
    {
        s.Cycles = 3;
        s.PenultimateInstructionAction = () => _ = cpu.Read(s.ProgramCounter);
        cpu.CompleteWrite(() => CpuStackOperations.Push(cpu, s, s.A));
    }

    internal static void PlaImp(CpuState s)
    {
        s.StackPullInProgress = true;
        s.Cycles = 4;
    }

    internal static void PhpImp(Cpu cpu, CpuState s)
    {
        s.Cycles = 3;
        s.PenultimateInstructionAction = () => _ = cpu.Read(s.ProgramCounter);
        cpu.CompleteWrite(() => CpuStackOperations.Push(cpu, s, (byte)(s.P.Value | 0b00110000)));
    }

    internal static void PlpImp(CpuState s)
    {
        s.StackPullInProgress = true;
        s.Cycles = 4;
    }

    internal static void BrkImp(Cpu cpu, CpuState s)
    {
        _ = cpu.Read(s.ProgramCounter++);
        CpuStackOperations.Push(cpu, s, (byte)(s.ProgramCounter >> 8));
        CpuStackOperations.Push(cpu, s, (byte)(s.ProgramCounter & 0xFF));
        CpuStackOperations.Push(cpu, s, (byte)(s.P.Value | 0b00110000));
        s.P.InterruptDisable = true;
        s.ProgramCounter = CpuStackOperations.ReadWord(cpu, 0xFFFE);
        s.Cycles = 7;
        s.BrkInProgress = true;
    }

    internal static void RtiImp(Cpu cpu, CpuState s)
    {
        _ = cpu.Read(s.ProgramCounter);
        s.P.Value = CpuStackOperations.Pull(cpu, s);
        s.P.Value = (byte)((s.P.Value & ~0b00010000) | 0b00100000);
        s.ProgramCounter = (ushort)(CpuStackOperations.Pull(cpu, s) | (CpuStackOperations.Pull(cpu, s) << 8));
        s.Cycles = 6;
    }
}
