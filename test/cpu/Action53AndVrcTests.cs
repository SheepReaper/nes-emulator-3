using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class Action53AndVrcTests
{
    [Fact]
    public void Factory_Action53WithChrRomReportsMapperSpecificError()
    {
        var rom = DiscreteMapperTestHelper.CreateRom(mapper: 28, prgBanks16K: 4, chrBanks8K: 1);
        var exception = Assert.Throws<NotSupportedException>(() => new CartridgeFactory().Create(rom));
        Assert.Contains("Action 53 requires CHR-RAM", exception.Message);
    }

    [Fact]
    public void Mapper34_Nina001SwitchesPrgAndTwoFourKilobyteChrBanks()
    {
        var rom = DiscreteMapperTestHelper.CreateRom(mapper: 34, prgBanks16K: 4, chrBanks8K: 4);
        var chrStart = 16 + 4 * 0x4000;
        for (var bank = 0; bank < 8; bank++)
        {
            Array.Fill(rom, (byte)bank, chrStart + bank * 0x1000, 0x1000);
        }
        var cartridge = new CartridgeFactory().Create(rom);

        cartridge.CpuWrite(0x7FFD, 1);
        cartridge.CpuWrite(0x7FFE, 3);
        cartridge.CpuWrite(0x7FFF, 6);

        Assert.IsType<Nina001Cart>(cartridge);
        Assert.Equal(1, cartridge.CpuRead(0x7FFD));
        Assert.Equal(2, cartridge.CpuRead(0x8000));
        Assert.Equal(3, cartridge.CpuRead(0xC000));
        Assert.Equal(3, cartridge.PpuRead(0x0000));
        Assert.Equal(6, cartridge.PpuRead(0x1000));
    }

    [Fact]
    public void Mapper22_SwitchesPrgAndOneKilobyteChrBanksUsingVrc2aAddressWiring()
    {
        var rom = DiscreteMapperTestHelper.CreateRom(mapper: 22, prgBanks16K: 4, chrBanks8K: 16);
        var chrStart = 16 + 4 * 0x4000;
        for (var bank = 0; bank < 128; bank++)
        {
            Array.Fill(rom, (byte)bank, chrStart + bank * 0x400, 0x400);
        }
        var cartridge = new CartridgeFactory().Create(rom);

        cartridge.CpuWrite(0x8000, 2);
        cartridge.CpuWrite(0xB000, 6);
        cartridge.CpuWrite(0xB002, 0);
        cartridge.CpuWrite(0xB001, 8);
        cartridge.CpuWrite(0xB003, 0);

        Assert.Equal(1, cartridge.CpuRead(0x8000));
        Assert.Equal(3, cartridge.CpuRead(0xC000));
        Assert.Equal(3, cartridge.PpuRead(0x0000));
        Assert.Equal(4, cartridge.PpuRead(0x0400));
    }

    [Fact]
    public void Mapper28_CombinesInnerAndOuterPrgBanksAndSwitchesChrRam()
    {
        var cartridge = DiscreteMapperTestHelper.CreateCartridge(mapper: 28, prgBanks16K: 32, chrBanks8K: 0);
        Assert.Equal(31, cartridge.CpuRead(0xC000));

        DiscreteMapperTestHelper.WriteAction53Register(cartridge, 0x80, 0x2F);
        DiscreteMapperTestHelper.WriteAction53Register(cartridge, 0x81, 2);
        DiscreteMapperTestHelper.WriteAction53Register(cartridge, 0x01, 1);
        cartridge.PpuWrite(0x0000, 0xA5);
        DiscreteMapperTestHelper.WriteAction53Register(cartridge, 0x00, 2);

        Assert.Equal(1, cartridge.CpuRead(0x8000));
        Assert.Equal(5, cartridge.CpuRead(0xC000));
        Assert.Equal(NametableMirroring.Horizontal, cartridge.NametableMirroring);
        Assert.Equal(0, cartridge.PpuRead(0x0000));
        DiscreteMapperTestHelper.WriteAction53Register(cartridge, 0x00, 0);
        Assert.Equal(0xA5, cartridge.PpuRead(0x0000));
    }
}
