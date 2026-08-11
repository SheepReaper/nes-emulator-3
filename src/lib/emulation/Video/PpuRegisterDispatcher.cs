using System;

namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Handles CPU writes to PPU registers ($2000-$2007).
/// </summary>
internal static class PpuRegisterDispatcher
{
    internal static void Write(
        ushort register,
        byte value,
        PpuState state,
        PpuScrollRegisters scroll,
        PpuOam oam,
        PpuMaskSettings mask,
        PpuBus? ppuBus,
        Action<byte> writeData)
    {
        switch (register)
        {
            case 0x2000:
                state.WriteControl(value);
                scroll.WriteControl(value);
                break;
            case 0x2001:
                state.Mask.Value = value;
                mask.UpdateFromMask(value);
                break;
            case 0x2003:
                oam.Address = value;
                break;
            case 0x2004:
                oam.Write(value);
                break;
            case 0x2005:
                scroll.WriteScroll(value);
                break;
            case 0x2006:
                if (scroll.WriteAddress(value))
                {
                    ppuBus?.NotifyPpuAddress((ushort)(scroll.VramAddress & 0x3FFF));
                }
                break;
            case 0x2007:
                writeData(value);
                break;
        }
    }
}
