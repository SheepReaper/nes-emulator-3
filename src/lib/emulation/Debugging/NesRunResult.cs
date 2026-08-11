namespace Sheep.Emulation.Nes.Debugging;

public readonly struct NesRunResult(
    int ppuDots, ulong cpuClocks, ulong frames, NesRunStopReason stopReason)
{
    public int PpuDots { get; } = ppuDots;
    public ulong CpuClocks { get; } = cpuClocks;
    public ulong Frames { get; } = frames;
    public NesRunStopReason StopReason { get; } = stopReason;
}