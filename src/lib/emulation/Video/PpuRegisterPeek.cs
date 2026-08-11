namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Peek helper for debugger inspecting PPU registers without side effects.
/// </summary>
internal static class PpuRegisterPeek
{
    internal static byte Peek(
        ushort address,
        PpuState state,
        PpuOam oam,
        PpuScrollRegisters scroll,
        PpuIoLatch ioLatch,
        ulong elapsedDots,
        IBus? bus,
        PpuBus? ppuBus)
    {
        return (ushort)(0x2000 | (address & 0x0007)) switch
        {
            0x2002 => (byte)((state.Status.Value & 0xE0) | (ioLatch.Read(elapsedDots) & 0x1F)),
            0x2004 => (oam.Address & 0x03) == 2 ? (byte)(oam[oam.Address] & 0xE3) : oam[oam.Address],
            0x2007 => ppuBus != null
                ? ppuBus.Peek((ushort)(scroll.VramAddress & 0x3FFF))
                : (bus?.Read((ushort)(scroll.VramAddress & 0x3FFF)) ?? 0),
            _ => ioLatch.Read(elapsedDots)
        };
    }
}
