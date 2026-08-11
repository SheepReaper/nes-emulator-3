namespace Sheep.Emulation.Nes.Tests;

/// <summary>
/// Helper for generating test ROMs for debugger unit tests.
/// </summary>
internal static class DebuggerTestHelper
{
    internal static byte[] CreateRom(byte chrBanks = 0)
    {
        var rom = new byte[16 + 0x4000 + (chrBanks * 0x2000)];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = 1;
        rom[5] = chrBanks;
        rom[16 + 0x3FFC] = 0x00;
        rom[16 + 0x3FFD] = 0x80;
        return rom;
    }
}
