using System;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Step and run dispatcher for NesDebugger.
/// </summary>
internal static class NesStepController
{
    internal static NesRunResult StepPpuDot(NesSystem nes, NesBreakpointManager bp) =>
        StepUntil(nes, bp, (_, _, dots) => dots >= 1);

    internal static NesRunResult StepCpuCycle(NesSystem nes, NesBreakpointManager bp)
    {
        ulong target;
        lock (nes.SyncRoot) target = nes.CpuClockCounter + 1;
        return StepUntil(nes, bp, (cpu, _, _) => cpu >= target);
    }

    internal static NesRunResult StepInstruction(NesSystem nes, NesBreakpointManager bp)
    {
        ulong start;
        lock (nes.SyncRoot) start = nes.CpuClockCounter;
        return StepUntil(nes, bp, (cpu, _, _) => cpu > start && nes.Cpu.IsInstructionBoundary);
    }

    internal static NesRunResult StepFrame(NesSystem nes, NesBreakpointManager bp)
    {
        ulong target;
        lock (nes.SyncRoot) target = nes.CurrentFrameNumber + 1;
        return StepUntil(nes, bp, (_, frame, _) => frame >= target);
    }

    private static NesRunResult StepUntil(NesSystem nes, NesBreakpointManager bp, Func<ulong, ulong, int, bool> completed)
    {
        lock (nes.SyncRoot)
        {
            return !nes.IsPausedLocked
                ? throw new InvalidOperationException("The NES must be paused for this operation.")
                : NesExecutionStepper.StepUntil(nes, bp, completed);
        }
    }
}
