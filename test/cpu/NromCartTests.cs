using Xunit;

namespace SR.Emulation.Nes.Tests;

public sealed class NromCartTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Factory_UsesUnifiedNromCartForSupportedPrgSizes(byte prgBanks)
    {
        var cartridge = new CartridgeFactory().Create(CreateRom(prgBanks));

        Assert.IsType<NromCart>(cartridge);
    }

    [Fact]
    public void SixteenKilobytePrg_IsMirroredAcrossCpuRomSpace()
    {
        var rom = CreateRom(1);
        Array.Fill(rom, (byte)0x11, 16, 0x2000);
        Array.Fill(rom, (byte)0x22, 16 + 0x2000, 0x2000);
        var cartridge = new CartridgeFactory().Create(rom);

        Assert.Equal(0x11, cartridge.CpuRead(0x8000));
        Assert.Equal(0x22, cartridge.CpuRead(0xA000));
        Assert.Equal(0x11, cartridge.CpuRead(0xC000));
        Assert.Equal(0x22, cartridge.CpuRead(0xE000));
    }

    [Fact]
    public void ThirtyTwoKilobytePrg_MapsDirectlyAcrossCpuRomSpace()
    {
        var rom = CreateRom(2);
        for (var bank = 0; bank < 4; bank++)
            Array.Fill(rom, (byte)bank, 16 + bank * 0x2000, 0x2000);
        var cartridge = new CartridgeFactory().Create(rom);

        Assert.Equal(0, cartridge.CpuRead(0x8000));
        Assert.Equal(1, cartridge.CpuRead(0xA000));
        Assert.Equal(2, cartridge.CpuRead(0xC000));
        Assert.Equal(3, cartridge.CpuRead(0xE000));
    }

    [Fact]
    public void CartridgeRam_IsReadableAndWritableAt6000Through7fff()
    {
        var cartridge = new CartridgeFactory().Create(CreateRom(2));

        cartridge.CpuWrite(0x6000, 0x12);
        cartridge.CpuWrite(0x7FFF, 0x34);

        Assert.Equal(0x12, cartridge.CpuRead(0x6000));
        Assert.Equal(0x34, cartridge.CpuRead(0x7FFF));
    }

    private static byte[] CreateRom(byte prgBanks)
    {
        var rom = new byte[16 + prgBanks * 0x4000 + 0x2000];
        rom[0] = (byte)'N'; rom[1] = (byte)'E'; rom[2] = (byte)'S'; rom[3] = 0x1A;
        rom[4] = prgBanks;
        rom[5] = 1;
        return rom;
    }
}
