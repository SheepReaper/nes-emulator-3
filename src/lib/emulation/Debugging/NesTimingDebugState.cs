namespace Sheep.Emulation.Nes.Debugging;

public sealed class NesTimingDebugState(
    NesVideoStandard videoStandard, NesExecutionState executionState, ulong cpuClocks,
    ulong frameNumber)
{
    public NesVideoStandard VideoStandard { get; } = videoStandard;
    public NesExecutionState ExecutionState { get; } = executionState;
    public ulong CpuClocks { get; } = cpuClocks;
    public ulong FrameNumber { get; } = frameNumber;
}