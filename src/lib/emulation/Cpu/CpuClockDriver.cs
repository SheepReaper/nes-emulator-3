using System;

namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuClockDriver
{
    internal static void Clock(
        Cpu cpu,
        CpuState s,
        InterruptLines interrupts,
        Action?[] dispatch,
        ulong masterClock)
    {
        s.MasterClock = masterClock;

        if (interrupts.ConsumeNmiEdge())
        {
            s.NmiPending = true;
            s.DelayPendingNmiOneInstruction = interrupts.DelayNmiOneInstruction || s.Cycles == 0;
            interrupts.DelayNmiOneInstruction = false;
        }

        if (s.Cycles == 0)
        {
            s.DmaHaltOccurred = false;
            s.BranchTakenNoPageCross = false;
            if (s.NmiPending)
            {
                if (s.DelayPendingNmiOneInstruction)
                {
                    s.DelayPendingNmiOneInstruction = false;
                }
                else
                {
                    CpuInterruptHandler.HandleNmi(cpu, s, interrupts);
                    s.Cycles--;
                    s.TotalCyclesExecuted++;
                    return;
                }
            }

            if (s.IrqPending)
            {
                CpuInterruptHandler.HandleIrq(cpu, s, interrupts);
                s.IrqPending = false;
                s.Cycles--;
                s.TotalCyclesExecuted++;
                return;
            }

            s.Opcode = cpu.Read(s.ProgramCounter++);
            var instruction = dispatch[s.Opcode] ??= CpuInstructionDecoder.Decode(s.Opcode, cpu, s, interrupts);
            instruction();
        }

        CpuMicrocodeSequencer.StepCycle(cpu, s, interrupts);
        s.Cycles--;
        s.TotalCyclesExecuted++;
    }
}
