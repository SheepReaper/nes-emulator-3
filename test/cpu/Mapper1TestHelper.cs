namespace Sheep.Emulation.Nes.Tests;

/// <summary>
/// Helper for generating test MMC1 ROMs and executing 5-bit serial writes.
/// </summary>
internal static class Mapper1TestHelper
{
    internal static Cartridge CreateCartridge(byte prgBanks16K = 8, byte chrBanks8K = 2) =>
        new CartridgeFactory().Create(CreateRom(prgBanks16K, chrBanks8K));

    internal static byte[] CreateRom(byte prgBanks16K = 8, byte chrBanks8K = 2)
    {
        var rom = new byte[16 + prgBanks16K * 0x4000 + chrBanks8K * 0x2000];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = prgBanks16K;
        rom[5] = chrBanks8K;
        rom[6] = 0x10;

        for (var bank = 0; bank < prgBanks16K; bank++)
        {
            Array.Fill(rom, (byte)bank, 16 + bank * 0x4000, 0x4000);
        }

        var chrStart = 16 + prgBanks16K * 0x4000;
        for (var bank = 0; bank < chrBanks8K * 2; bank++)
        {
            Array.Fill(rom, (byte)bank, chrStart + bank * 0x1000, 0x1000);
        }
        return rom;
    }

    internal static void SerialWrite(Cartridge cartridge, ushort address, byte value)
    {
        for (var bit = 0; bit < 5; bit++)
        {
            cartridge.CpuWrite(address, (byte)((value >> bit) & 1));
        }
    }
}
