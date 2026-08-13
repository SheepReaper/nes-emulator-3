namespace EmuSheep.Tests;

internal static class NesEmulationTestHelper
{
    internal static byte[] CreateMapperZeroRom()
    {
        var rom = new byte[16 + 16 * 1024 + 8 * 1024];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = 1;
        rom[5] = 1;
        Array.Fill(rom, (byte)0xEA, 16, 16 * 1024);
        rom[16 + 0x3FFA] = 0x00;
        rom[16 + 0x3FFB] = 0x80;
        rom[16 + 0x3FFC] = 0x00;
        rom[16 + 0x3FFD] = 0x80;
        rom[16 + 0x3FFE] = 0x00;
        rom[16 + 0x3FFF] = 0x80;
        return rom;
    }
}
