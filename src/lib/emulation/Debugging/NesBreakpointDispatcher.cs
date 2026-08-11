using System;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Breakpoint event queuing, instruction boundary checks, and dispatching.
/// </summary>
internal static class NesBreakpointDispatcher
{
    internal static bool TryBreakBeforeCpuClock(NesSystem nes, NesBreakpointManager bp)
    {
        if (bp.SuppressBreakpoints || !nes.WillClockCpuLocked || !nes.Cpu.IsInstructionBoundary)
        {
            return false;
        }

        var pc = nes.Cpu.ProgramCounter;
        var hit = bp.Find(NesDebugBreakKind.Execute, pc);
        if (hit == null)
        {
            return false;
        }

        QueueBreakpoint(nes, bp, new BreakOccurredEventArgs(hit, pc, null, pc));
        return true;
    }

    internal static void CompleteDot(NesSystem nes, NesBreakpointManager bp)
    {
        if (bp.PendingAccessBreak == null || !nes.Cpu.IsInstructionBoundary)
        {
            return;
        }

        var hit = bp.PendingAccessBreak;
        bp.PendingAccessBreak = null;
        QueueBreakpoint(nes, bp, hit);
    }

    internal static void QueueBreakpoint(NesSystem nes, NesBreakpointManager bp, BreakOccurredEventArgs hit)
    {
        var previous = nes.IsPausedLocked ? NesExecutionState.Paused : NesExecutionState.Running;
        nes.SetPausedLocked(true);
        bp.EventBreak = hit;
        if (previous != NesExecutionState.Paused)
        {
            bp.EventState = new ExecutionStateChangedEventArgs(previous, NesExecutionState.Paused, NesDebugPauseReason.Breakpoint);
        }
    }

    internal static void DispatchPendingEvents(
        NesSystem nes,
        NesBreakpointManager bp,
        Action<ExecutionStateChangedEventArgs>? onStateChanged,
        Action<BreakOccurredEventArgs>? onBreakOccurred)
    {
        ExecutionStateChangedEventArgs? state;
        BreakOccurredEventArgs? hit;
        lock (nes.SyncRoot)
        {
            state = bp.EventState;
            hit = bp.EventBreak;
            bp.EventState = null;
            bp.EventBreak = null;
        }

        if (state != null)
        {
            onStateChanged?.Invoke(state);
        }
        if (hit != null)
        {
            onBreakOccurred?.Invoke(hit);
        }
    }
}
