using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class Mapper5Tests
{
    [Fact]
    public void Factory_CreatesMmc5WithLastEightKilobytePrgBankAtE000()
    {
        var cartridge = CreateCartridge();

        Assert.IsType<Mmc5Cart>(cartridge);
        Assert.Equal(7, cartridge.CpuRead(0xE000));
    }

    [Fact]
    public void PrgMode3_MapsFourIndependentEightKilobyteRomBanks()
    {
        var cartridge = CreateCartridge();

        cartridge.CpuWrite(0x5100, 3);
        cartridge.CpuWrite(0x5114, 0x81);
        cartridge.CpuWrite(0x5115, 0x82);
        cartridge.CpuWrite(0x5116, 0x83);
        cartridge.CpuWrite(0x5117, 0x84);

        Assert.Equal(1, cartridge.CpuRead(0x8000));
        Assert.Equal(2, cartridge.CpuRead(0xA000));
        Assert.Equal(3, cartridge.CpuRead(0xC000));
        Assert.Equal(4, cartridge.CpuRead(0xE000));
    }

    [Fact]
    public void ChrMode3_MapsOneKilobyteBanks()
    {
        var cartridge = CreateCartridge();

        cartridge.CpuWrite(0x5101, 3);
        cartridge.CpuWrite(0x5120, 3);
        cartridge.CpuWrite(0x5127, 9);

        Assert.Equal(3, cartridge.PpuRead(0x0000));
        Assert.Equal(9, cartridge.PpuRead(0x1C00));
    }

    [Fact]
    public void ExRamAndFillModeSupplySelectedNametables()
    {
        var cartridge = CreateCartridge();
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        var bus = new PpuBus(slot);

        cartridge.CpuWrite(0x5104, 0);
        cartridge.CpuWrite(0x5C00, 0xA5);
        cartridge.CpuWrite(0x5105, 0xE4);
        cartridge.CpuWrite(0x5106, 0x33);
        cartridge.CpuWrite(0x5107, 2);

        Assert.Equal(0xA5, bus.Read(0x2800));
        Assert.Equal(0x33, bus.Read(0x2C00));
        Assert.Equal(0xAA, bus.Read(0x2FC0));
    }

    [Fact]
    public void MultiplierReturnsImmediateSixteenBitProduct()
    {
        var cartridge = CreateCartridge();

        cartridge.CpuWrite(0x5205, 200);
        cartridge.CpuWrite(0x5206, 3);

        Assert.Equal(0x58, cartridge.CpuRead(0x5205));
        Assert.Equal(0x02, cartridge.CpuRead(0x5206));
    }

    private static Cartridge CreateCartridge()
    {
        const int prgBanks8K = 8;
        const int chrBanks1K = 16;
        var rom = new byte[16 + prgBanks8K * 0x2000 + chrBanks1K * 0x0400];
        rom[0] = (byte)'N'; rom[1] = (byte)'E'; rom[2] = (byte)'S'; rom[3] = 0x1A;
        rom[4] = prgBanks8K / 2;
        rom[5] = chrBanks1K / 8;
        rom[6] = 0x50;
        for (var bank = 0; bank < prgBanks8K; bank++)
            Array.Fill(rom, (byte)bank, 16 + bank * 0x2000, 0x2000);
        var chrStart = 16 + prgBanks8K * 0x2000;
        for (var bank = 0; bank < chrBanks1K; bank++)
            Array.Fill(rom, (byte)bank, chrStart + bank * 0x0400, 0x0400);
        return new CartridgeFactory().Create(rom);
    }
}
