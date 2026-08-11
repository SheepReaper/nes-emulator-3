namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Container holding PPU sub-units for rendering, timing, registers, and memory.
/// </summary>
internal sealed class PpuUnits
{
    internal readonly PpuTiming Time;
    internal readonly PpuState State;
    internal readonly PpuMaskSettings Mask = new();
    internal readonly PpuOam Oam = new();
    internal readonly PpuIoLatch IoLatch = new();
    internal readonly PpuScrollRegisters Scroll = new();
    internal readonly PpuBackgroundPipeline Bg = new();
    internal readonly PpuSpriteRenderer Sprites = new();
    internal readonly PpuFrameManager Frame = new();
    internal readonly PpuDataPort DataPort = new();

    internal PpuUnits(NesTiming nesTiming, NesVideoStandard videoStandard, InterruptLines interrupts)
    {
        Time = new PpuTiming(nesTiming, videoStandard);
        State = new PpuState(interrupts);
    }

    internal void Reset()
    {
        Time.Reset();
        State.Reset();
        Mask.Reset();
        Oam.Reset();
        IoLatch.Reset();
        Scroll.Reset();
        Bg.Reset();
        Sprites.Reset();
        Frame.Reset();
        DataPort.Reset();
    }
}
