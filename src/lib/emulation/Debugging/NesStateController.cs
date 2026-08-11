using System;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Execution state transitions and debugger event firing.
/// </summary>
internal static class NesStateController
{
    internal static void SetExecutionState(
        NesSystem nes,
        bool paused,
        NesDebugPauseReason reason,
        Action<ExecutionStateChangedEventArgs>? handler)
    {
        NesExecutionState previous;
        NesExecutionState current;
        lock (nes.SyncRoot)
        {
            previous = nes.IsPausedLocked ? NesExecutionState.Paused : NesExecutionState.Running;
            nes.SetPausedLocked(paused);
            current = paused ? NesExecutionState.Paused : NesExecutionState.Running;
        }
        if (previous != current)
        {
            handler?.Invoke(new ExecutionStateChangedEventArgs(previous, current, reason));
        }
    }
}
