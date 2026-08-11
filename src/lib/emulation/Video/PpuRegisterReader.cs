namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Status and OAM data register read handlers for PPU ($2002 / $2004).
/// </summary>
internal static class PpuRegisterReader
{
    internal static byte ReadStatus(
        PpuState state,
        PpuIoLatch ioLatch,
        PpuScrollRegisters scroll,
        PpuTiming time,
        ulong elapsedDots,
        ulong decayDots)
    {
        var value = state.ReadStatus(ioLatch.Read(elapsedDots));
        if (time.Scanline == 241 && time.Cycle == 1)
        {
            state.SuppressVblank = true;
        }
        scroll.WriteToggle = false;
        ioLatch.Drive(value, elapsedDots, decayDots, 0xE0);
        return value;
    }

    internal static byte ReadOamData(
        PpuOam oam,
        PpuIoLatch ioLatch,
        ulong elapsedDots,
        ulong decayDots)
    {
        var value = oam.Read();
        ioLatch.Drive(value, elapsedDots, decayDots);
        return value;
    }
}
