using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class Mapper1Tests
{
    [Fact]
    public void Factory_CreatesMmc1WithSwitchableFirstAndFixedLastPrgBanks()
    {
        var cartridge = Mapper1TestHelper.CreateCartridge();
        Assert.IsType<Mmc1Cart>(cartridge);
        Assert.Equal(0, cartridge.CpuRead(0x8000));
        Assert.Equal(7, cartridge.CpuRead(0xC000));
    }

    [Fact]
    public void SerialRegister_CommitsOnlyOnFifthWriteUsingFinalWriteAddress()
    {
        var cartridge = Mapper1TestHelper.CreateCartridge();
        for (var bit = 0; bit < 4; bit++)
        {
            cartridge.CpuWrite(0x8000, (byte)((3 >> bit) & 1));
        }
        Assert.Equal(0, cartridge.CpuRead(0x8000));

        cartridge.CpuWrite(0xE000, 0);
        Assert.Equal(3, cartridge.CpuRead(0x8000));
    }

    [Fact]
    public void PrgBanking_SupportsBothSixteenKilobyteModesAndThirtyTwoKilobyteMode()
    {
        var cartridge = Mapper1TestHelper.CreateCartridge();
        Mapper1TestHelper.SerialWrite(cartridge, 0xE000, 3);
        Assert.Equal(3, cartridge.CpuRead(0x8000));
        Assert.Equal(7, cartridge.CpuRead(0xC000));

        Mapper1TestHelper.SerialWrite(cartridge, 0x8000, 0x08);
        Assert.Equal(0, cartridge.CpuRead(0x8000));
        Assert.Equal(3, cartridge.CpuRead(0xC000));

        Mapper1TestHelper.SerialWrite(cartridge, 0x8000, 0x00);
        Assert.Equal(2, cartridge.CpuRead(0x8000));
        Assert.Equal(3, cartridge.CpuRead(0xC000));
    }

    [Fact]
    public void ChrBanking_SupportsSeparateFourKilobyteAndCombinedEightKilobyteModes()
    {
        var cartridge = Mapper1TestHelper.CreateCartridge(chrBanks8K: 4);
        Mapper1TestHelper.SerialWrite(cartridge, 0x8000, 0x10);
        Mapper1TestHelper.SerialWrite(cartridge, 0xA000, 2);
        Mapper1TestHelper.SerialWrite(cartridge, 0xC000, 5);

        Assert.Equal(2, cartridge.PpuRead(0x0000));
        Assert.Equal(5, cartridge.PpuRead(0x1000));

        Mapper1TestHelper.SerialWrite(cartridge, 0x8000, 0x00);
        Mapper1TestHelper.SerialWrite(cartridge, 0xA000, 3);
        Assert.Equal(2, cartridge.PpuRead(0x0000));
        Assert.Equal(3, cartridge.PpuRead(0x1000));
    }

    [Fact]
    public void ChrRamWrites_FollowTheSelectedFourKilobyteBank()
    {
        var cartridge = Mapper1TestHelper.CreateCartridge(chrBanks8K: 0);
        Mapper1TestHelper.SerialWrite(cartridge, 0x8000, 0x10);
        Mapper1TestHelper.SerialWrite(cartridge, 0xA000, 1);
        cartridge.PpuWrite(0x0000, 0xA5);

        Mapper1TestHelper.SerialWrite(cartridge, 0xA000, 0);
        Assert.Equal(0, cartridge.PpuRead(0x0000));

        Mapper1TestHelper.SerialWrite(cartridge, 0xC000, 1);
        Assert.Equal(0xA5, cartridge.PpuRead(0x1000));
    }

    [Theory]
    [InlineData(0, NametableMirroring.SingleScreenLower)]
    [InlineData(1, NametableMirroring.SingleScreenUpper)]
    [InlineData(2, NametableMirroring.Vertical)]
    [InlineData(3, NametableMirroring.Horizontal)]
    public void ControlRegister_SelectsEveryMirroringMode(byte value, NametableMirroring expected)
    {
        var cartridge = Mapper1TestHelper.CreateCartridge();
        Mapper1TestHelper.SerialWrite(cartridge, 0x8000, value);
        Assert.Equal(expected, cartridge.NametableMirroring);
    }

    [Fact]
    public void FourScreenMirroring_IsNotOverriddenByControlRegister()
    {
        var rom = Mapper1TestHelper.CreateRom();
        rom[6] |= 0x08;
        var cartridge = new CartridgeFactory().Create(rom);
        Mapper1TestHelper.SerialWrite(cartridge, 0x8000, 0);
        Assert.Equal(NametableMirroring.FourScreen, cartridge.NametableMirroring);
    }
}
