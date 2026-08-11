using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class DiscreteMapperTests
{
    [Fact]
    public void Mapper2_SwitchesLowerPrgBankAndFixesLastBankAtC000()
    {
        var cartridge = DiscreteMapperTestHelper.CreateCartridge(mapper: 2, prgBanks16K: 8, chrBanks8K: 0);
        cartridge.CpuWrite(0x8FFF, 3);
        Assert.Equal(3, cartridge.CpuRead(0x8000));
        Assert.Equal(7, cartridge.CpuRead(0xC000));
    }

    [Fact]
    public void Mapper3_SwitchesEightKilobyteChrBank()
    {
        var cartridge = DiscreteMapperTestHelper.CreateCartridge(mapper: 3, prgBanks16K: 2, chrBanks8K: 4);
        cartridge.CpuWrite(0x8FFF, 2);
        Assert.Equal(2, cartridge.PpuRead(0x0000));
        Assert.Equal(2, cartridge.PpuRead(0x1FFF));
    }

    [Fact]
    public void Mapper3_ProvidesTheLegacyEightKilobyteCpuRamWindow()
    {
        var cart = new CnromCart(new byte[0x8000], new byte[0x2000], NametableMirroring.Horizontal);
        cart.CpuWrite(0x6000, 0x5A);
        cart.CpuWrite(0x7FFF, 0xA5);
        Assert.Equal(0x5A, cart.CpuRead(0x6000));
        Assert.Equal(0xA5, cart.CpuRead(0x7FFF));
    }

    [Theory]
    [InlineData(0x02, NametableMirroring.SingleScreenLower)]
    [InlineData(0x12, NametableMirroring.SingleScreenUpper)]
    public void Mapper7_SwitchesThirtyTwoKilobytePrgAndOneScreenMirroring(
        byte value,
        NametableMirroring expectedMirroring)
    {
        var cartridge = DiscreteMapperTestHelper.CreateCartridge(mapper: 7, prgBanks16K: 8, chrBanks8K: 0);
        cartridge.CpuWrite(0x8FFF, value);
        Assert.Equal(4, cartridge.CpuRead(0x8000));
        Assert.Equal(5, cartridge.CpuRead(0xC000));
        Assert.Equal(expectedMirroring, cartridge.NametableMirroring);
    }

    [Fact]
    public void Mapper11_SwitchesThirtyTwoKilobytePrgAndEightKilobyteChr()
    {
        var cartridge = DiscreteMapperTestHelper.CreateCartridge(mapper: 11, prgBanks16K: 8, chrBanks8K: 8);
        cartridge.CpuWrite(0x8FFF, 0x32);
        Assert.Equal(4, cartridge.CpuRead(0x8000));
        Assert.Equal(5, cartridge.CpuRead(0xC000));
        Assert.Equal(3, cartridge.PpuRead(0x0000));
    }

    [Fact]
    public void Mapper34_BnromSwitchesThirtyTwoKilobytePrgBank()
    {
        var cartridge = DiscreteMapperTestHelper.CreateCartridge(mapper: 34, prgBanks16K: 8, chrBanks8K: 0);
        cartridge.CpuWrite(0x8FFF, 2);
        Assert.Equal(4, cartridge.CpuRead(0x8000));
        Assert.Equal(5, cartridge.CpuRead(0xC000));
    }

    [Fact]
    public void Mapper34_BnromAcceptsEightKilobytesOfReadOnlyChrRom()
    {
        var cartridge = DiscreteMapperTestHelper.CreateCartridge(mapper: 34, prgBanks16K: 4, chrBanks8K: 1);
        var original = cartridge.PpuRead(0x0123);
        cartridge.PpuWrite(0x0123, (byte)(original ^ 0xFF));
        Assert.IsType<BnromCart>(cartridge);
        Assert.False(cartridge.IsChrWritable);
        Assert.Equal(original, cartridge.PpuRead(0x0123));
    }

    [Theory]
    [InlineData(3, "CNROM requires CHR-ROM")]
    [InlineData(7, "AxROM requires CHR-RAM")]
    [InlineData(11, "Color Dreams requires CHR-ROM")]
    [InlineData(22, "VRC2a requires CHR-ROM")]
    public void Factory_KnownMapperWithUnsupportedChrConfigurationReportsMapperSpecificError(
        byte mapper,
        string expectedMessage)
    {
        var rom = DiscreteMapperTestHelper.CreateRom(
            mapper, prgBanks16K: mapper == 7 ? (byte)2 : (byte)4, chrBanks8K: mapper == 7 ? (byte)1 : (byte)0);
        var exception = Assert.Throws<NotSupportedException>(() => new CartridgeFactory().Create(rom));
        Assert.Contains(expectedMessage, exception.Message);
    }
}
