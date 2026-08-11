namespace Sheep.Emulation.Nes.Tests;

/// <summary>
/// Helpers and mocks for PPU unit tests.
/// </summary>
internal static class PpuTestHelper
{
    internal static byte[] CreateSolidBackgroundRom(byte mask = 0x0A, byte paletteColor = 0x30)
    {
        var rom = new byte[16 + 0x4000 + 0x2000];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = 1;
        rom[5] = 1;

        var program = new byte[]
        {
            0xA9, 0x3F, 0x8D, 0x06, 0x20,
            0xA9, 0x00, 0x8D, 0x06, 0x20,
            0xA9, 0x0F, 0x8D, 0x07, 0x20,
            0xA9, paletteColor, 0x8D, 0x07, 0x20,
            0xA9, mask, 0x8D, 0x01, 0x20,
            0x4C, 0x19, 0x80
        };
        program.CopyTo(rom, 16);
        rom[16 + 0x3FFC] = 0x00;
        rom[16 + 0x3FFD] = 0x80;
        for (var row = 0; row < 8; row++)
        {
            rom[16 + 0x4000 + row] = 0xFF;
        }
        return rom;
    }

    internal static byte[] CreateSpriteRom()
    {
        var rom = new byte[16 + 0x4000 + 0x2000];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = 1;
        rom[5] = 1;
        var program = new byte[]
        {
            0xA9, 0x3F, 0x8D, 0x06, 0x20,
            0xA9, 0x11, 0x8D, 0x06, 0x20,
            0xA9, 0x16, 0x8D, 0x07, 0x20,
            0xA2, 0x00,
            0xA9, 0xFF,
            0x9D, 0x00, 0x02,
            0xE8,
            0xD0, 0xFA,
            0xA9, 29, 0x8D, 0x00, 0x02,
            0xA9, 1, 0x8D, 0x01, 0x02,
            0xA9, 0, 0x8D, 0x02, 0x02,
            0xA9, 40, 0x8D, 0x03, 0x02,
            0xA9, 2, 0x8D, 0x14, 0x40,
            0xA9, 0x14, 0x8D, 0x01, 0x20,
            0x4C, 0x39, 0x80
        };
        program.CopyTo(rom, 16);
        rom[16 + 0x3FFC] = 0x00;
        rom[16 + 0x3FFD] = 0x80;
        for (var row = 0; row < 8; row++)
        {
            rom[16 + 0x4000 + 16 + row] = 0xFF;
        }
        return rom;
    }

    internal static Ppu CreatePpu(out MemoryBus bus)
    {
        var ppu = new Ppu(new InterruptLines());
        bus = new MemoryBus();
        ppu.ConnectBus(bus);
        ppu.Reset();
        return ppu;
    }

    internal static void FillOam(Ppu ppu, byte value)
    {
        ppu.Write(0x2003, 0);
        for (var i = 0; i < 256; i++)
        {
            ppu.Write(0x2004, value);
        }
    }

    internal static void SetPpuAddress(Ppu ppu, ushort address)
    {
        _ = ppu.Read(0x2002);
        ppu.Write(0x2006, (byte)(address >> 8));
        ppu.Write(0x2006, (byte)address);
    }
}
