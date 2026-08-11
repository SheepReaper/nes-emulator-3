namespace Sheep.Emulation.Nes.Tests;

/// <summary>
/// Rom generation and register helpers for discrete mapper unit tests.
/// </summary>
internal static class DiscreteMapperTestHelper
{
    internal static Cartridge CreateCartridge(byte mapper, byte prgBanks16K, byte chrBanks8K) =>
        new CartridgeFactory().Create(CreateRom(mapper, prgBanks16K, chrBanks8K));

    internal static byte[] CreateRom(byte mapper, byte prgBanks16K, byte chrBanks8K)
    {
        var rom = new byte[16 + prgBanks16K * 0x4000 + chrBanks8K * 0x2000];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = prgBanks16K;
        rom[5] = chrBanks8K;
        rom[6] = (byte)(mapper << 4);
        rom[7] = (byte)(mapper & 0xF0);

        for (var bank = 0; bank < prgBanks16K; bank++)
        {
            Array.Fill(rom, (byte)bank, 16 + bank * 0x4000, 0x4000);
        }
        rom[16 + 0x0FFF] = 0xFF;

        var chrStart = 16 + prgBanks16K * 0x4000;
        for (var bank = 0; bank < chrBanks8K; bank++)
        {
            Array.Fill(rom, (byte)bank, chrStart + bank * 0x2000, 0x2000);
        }

        return rom;
    }

    internal static void WriteAction53Register(Cartridge cartridge, byte register, byte value)
    {
        cartridge.CpuWrite(0x5000, register);
        cartridge.CpuWrite(0x8000, value);
    }
}
