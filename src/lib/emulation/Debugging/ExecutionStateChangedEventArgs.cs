using System;
namespace Sheep.Emulation.Nes.Debugging;

public sealed class ExecutionStateChangedEventArgs(
    NesExecutionState previous, NesExecutionState current, NesDebugPauseReason reason) : EventArgs
{
    public NesExecutionState Previous { get; } = previous;
    public NesExecutionState Current { get; } = current;
    public NesDebugPauseReason Reason { get; } = reason;
}