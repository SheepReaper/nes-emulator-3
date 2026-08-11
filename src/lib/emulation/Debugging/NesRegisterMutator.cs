using System;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Direct register read/write mutators for debugger.
/// </summary>
internal static class NesRegisterMutator
{
    internal static void WritePpuRegister(NesSystem nes, ushort register, byte value)
    {
        if (register is < 0x2000 or > 0x2007)
        {
            throw new ArgumentOutOfRangeException(nameof(register));
        }
        nes.Ppu.Write(register, value);
    }

    internal static void WriteApuRegister(NesSystem nes, ushort register, byte value)
    {
        if (!(register is >= 0x4000 and <= 0x4013 or 0x4015 or 0x4017))
        {
            throw new ArgumentOutOfRangeException(nameof(register));
        }
        nes.Apu.Write(register, value);
    }
}
