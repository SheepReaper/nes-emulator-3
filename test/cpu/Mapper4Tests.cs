using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class Mapper4Tests
{
    [Fact]
    public void SixteenKilobytePrg_IsSupportedForHardwareTestCartridges()
    {
        var cartridge = new Mmc3Cart(
            new byte[0x4000], new byte[0x2000], NametableMirroring.Vertical, false, new InterruptLines());

        Assert.Equal(0, cartridge.CpuRead(0x8000));
        Assert.Equal(0, cartridge.CpuRead(0xE000));
    }

    [Fact]
    public void Factory_CreatesMapper4AndMapsFixedPrgBanks()
    {
        var cartridge = Mapper4TestHelper.CreateCartridge();
        Assert.IsType<Mmc3Cart>(cartridge);
        Assert.Equal(0, cartridge.CpuRead(0x8000));
        Assert.Equal(0, cartridge.CpuRead(0xA000));
        Assert.Equal(6, cartridge.CpuRead(0xC000));
        Assert.Equal(7, cartridge.CpuRead(0xE000));
    }

    [Fact]
    public void PrgBankRegisters_MapSwitchableBanksInBothModes()
    {
        var cartridge = Mapper4TestHelper.CreateCartridge();
        Mapper4TestHelper.WriteBank(cartridge, 6, 3);
        Mapper4TestHelper.WriteBank(cartridge, 7, 4);

        Assert.Equal(3, cartridge.CpuRead(0x8000));
        Assert.Equal(4, cartridge.CpuRead(0xA000));
        Assert.Equal(6, cartridge.CpuRead(0xC000));

        cartridge.CpuWrite(0x8000, 0x46);

        Assert.Equal(6, cartridge.CpuRead(0x8000));
        Assert.Equal(4, cartridge.CpuRead(0xA000));
        Assert.Equal(3, cartridge.CpuRead(0xC000));
    }

    [Fact]
    public void ChrBankRegisters_MapTwoAndOneKilobyteBanksInBothModes()
    {
        var cartridge = Mapper4TestHelper.CreateCartridge();
        Mapper4TestHelper.WriteBank(cartridge, 0, 3);
        Mapper4TestHelper.WriteBank(cartridge, 1, 6);
        Mapper4TestHelper.WriteBank(cartridge, 2, 8);
        Mapper4TestHelper.WriteBank(cartridge, 3, 9);
        Mapper4TestHelper.WriteBank(cartridge, 4, 10);
        Mapper4TestHelper.WriteBank(cartridge, 5, 11);

        Assert.Equal(new byte[] { 2, 3, 6, 7, 8, 9, 10, 11 }, Mapper4TestHelper.ReadChrSlots(cartridge));

        cartridge.CpuWrite(0x8000, 0x80);

        Assert.Equal(new byte[] { 8, 9, 10, 11, 2, 3, 6, 7 }, Mapper4TestHelper.ReadChrSlots(cartridge));
    }

    [Fact]
    public void MirroringRegister_ChangesMirroringUnlessFourScreenIsHardwired()
    {
        var cartridge = Mapper4TestHelper.CreateCartridge();
        cartridge.CpuWrite(0xA000, 0);
        Assert.Equal(NametableMirroring.Vertical, cartridge.NametableMirroring);
        cartridge.CpuWrite(0xA000, 1);
        Assert.Equal(NametableMirroring.Horizontal, cartridge.NametableMirroring);

        var fourScreen = Mapper4TestHelper.CreateCartridge(fourScreen: true);
        fourScreen.CpuWrite(0xA000, 1);
        Assert.Equal(NametableMirroring.FourScreen, fourScreen.NametableMirroring);
    }

    [Fact]
    public void PrgRamProtect_ControlsReadsAndWrites()
    {
        var cartridge = Mapper4TestHelper.CreateCartridge();
        cartridge.CpuWrite(0x6000, 0x12);
        Assert.Equal(0x12, cartridge.CpuRead(0x6000));

        cartridge.CpuWrite(0xA001, 0xC0);
        cartridge.CpuWrite(0x6000, 0x34);
        Assert.Equal(0x12, cartridge.CpuRead(0x6000));

        cartridge.CpuWrite(0xA001, 0x00);
        Assert.Equal(0, cartridge.CpuRead(0x6000));
    }
}
