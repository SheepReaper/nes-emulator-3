namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuMicrocodeSequencer
{
    internal static void StepCycle(Cpu cpu, CpuState s, InterruptLines interrupts)
    {
        if (s.JsrInProgress) CpuSubroutineSequencer.StepJsr(cpu, s);
        if (s.RtsInProgress) CpuSubroutineSequencer.StepRts(cpu, s);
        if (s.StackPullInProgress) CpuSubroutineSequencer.StepStackPull(cpu, s);
        if (s.AbsoluteStoreRegister != 0) CpuMemorySequencer.StepAbsoluteStore(cpu, s);
        if (s.AbsoluteReadConsumer != null) CpuMemorySequencer.StepAbsoluteRead(cpu, s);
        if (s.RmwInProgress) CpuMemorySequencer.StepRmw(cpu, s);

        if (s.Cycles == 2 && s.PenultimateInstructionAction != null)
        {
            var action = s.PenultimateInstructionAction;
            s.PenultimateInstructionAction = null;
            s.PenultimateInstructionIsWrite = false;
            action();
        }

        if (s.Cycles == 1 && s.EndOfInstructionAction != null)
        {
            var action = s.EndOfInstructionAction;
            s.EndOfInstructionAction = null;
            s.EndOfInstructionIsWrite = false;
            s.PendingIoReadAddress = null;
            action();
        }

        if (s.Cycles == 3 && (s.BrkInProgress || s.IrqInProgress) && s.NmiPending && !s.DelayPendingNmiOneInstruction)
        {
            s.ProgramCounter = CpuStackOperations.ReadWord(cpu, 0xFFFA);
            s.NmiPending = false;
            interrupts.Nmi = false;
        }

        if (s.Cycles == 2)
        {
            if (!s.BranchTakenNoPageCross)
            {
                s.IrqPending = (s.IrqPending || interrupts.Irq) && !s.P.InterruptDisable;
            }
            s.BranchTakenNoPageCross = false;
        }

        if (s.Cycles == 1)
        {
            if (s.BrkInProgress || s.IrqInProgress)
            {
                s.DelayPendingNmiOneInstruction = true;
            }
            s.BrkInProgress = false;
            s.IrqInProgress = false;
        }
    }
}
