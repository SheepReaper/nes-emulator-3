using Xunit;

namespace SR.Emulation.Nes.Tests;

public sealed class Mapper1Tests
{
    [Fact]
    public void Factory_CreatesMmc1WithSwitchableFirstAndFixedLastPrgBanks()
    {
        var cartridge = CreateCartridge();

        Assert.IsType<Mmc1Cart>(cartridge);
        Assert.Equal(0, cartridge.CpuRead(0x8000));
        Assert.Equal(7, cartridge.CpuRead(0xC000));
    }

    [Fact]
    public void SerialRegister_CommitsOnlyOnFifthWriteUsingFinalWriteAddress()
    {
        var cartridge = CreateCartridge();
        for (var bit = 0; bit < 4; bit++) cartridge.CpuWrite(0x8000, (byte)((3 >> bit) & 1));
        Assert.Equal(0, cartridge.CpuRead(0x8000));

        cartridge.CpuWrite(0xE000, 0);

        Assert.Equal(3, cartridge.CpuRead(0x8000));
    }

    [Fact]
    public void PrgBanking_SupportsBothSixteenKilobyteModesAndThirtyTwoKilobyteMode()
    {
        var cartridge = CreateCartridge();
        SerialWrite(cartridge, 0xE000, 3);
        Assert.Equal(3, cartridge.CpuRead(0x8000));
        Assert.Equal(7, cartridge.CpuRead(0xC000));

        SerialWrite(cartridge, 0x8000, 0x08);
        Assert.Equal(0, cartridge.CpuRead(0x8000));
        Assert.Equal(3, cartridge.CpuRead(0xC000));

        SerialWrite(cartridge, 0x8000, 0x00);
        Assert.Equal(2, cartridge.CpuRead(0x8000));
        Assert.Equal(3, cartridge.CpuRead(0xC000));
    }

    [Fact]
    public void ChrBanking_SupportsSeparateFourKilobyteAndCombinedEightKilobyteModes()
    {
        var cartridge = CreateCartridge(chrBanks8K: 4);
        SerialWrite(cartridge, 0x8000, 0x10);
        SerialWrite(cartridge, 0xA000, 2);
        SerialWrite(cartridge, 0xC000, 5);

        Assert.Equal(2, cartridge.PpuRead(0x0000));
        Assert.Equal(5, cartridge.PpuRead(0x1000));

        SerialWrite(cartridge, 0x8000, 0x00);
        SerialWrite(cartridge, 0xA000, 3);
        Assert.Equal(2, cartridge.PpuRead(0x0000));
        Assert.Equal(3, cartridge.PpuRead(0x1000));
    }

    [Fact]
    public void ChrRamWrites_FollowTheSelectedFourKilobyteBank()
    {
        var cartridge = CreateCartridge(chrBanks8K: 0);
        SerialWrite(cartridge, 0x8000, 0x10);
        SerialWrite(cartridge, 0xA000, 1);
        cartridge.PpuWrite(0x0000, 0xA5);

        SerialWrite(cartridge, 0xA000, 0);
        Assert.Equal(0, cartridge.PpuRead(0x0000));

        SerialWrite(cartridge, 0xC000, 1);
        Assert.Equal(0xA5, cartridge.PpuRead(0x1000));
    }

    [Theory]
    [InlineData(0, NametableMirroring.SingleScreenLower)]
    [InlineData(1, NametableMirroring.SingleScreenUpper)]
    [InlineData(2, NametableMirroring.Vertical)]
    [InlineData(3, NametableMirroring.Horizontal)]
    public void ControlRegister_SelectsEveryMirroringMode(byte value, NametableMirroring expected)
    {
        var cartridge = CreateCartridge();

        SerialWrite(cartridge, 0x8000, value);

        Assert.Equal(expected, cartridge.NametableMirroring);
    }

    [Fact]
    public void FourScreenMirroring_IsNotOverriddenByControlRegister()
    {
        var rom = CreateRom();
        rom[6] |= 0x08;
        var cartridge = new CartridgeFactory().Create(rom);

        SerialWrite(cartridge, 0x8000, 0);

        Assert.Equal(NametableMirroring.FourScreen, cartridge.NametableMirroring);
    }

    [Fact]
    public void ResetWrite_ClearsPartialSerialValueAndForcesFixedLastPrgMode()
    {
        var cartridge = CreateCartridge();
        SerialWrite(cartridge, 0x8000, 0x0A);
        SerialWrite(cartridge, 0xE000, 3);
        cartridge.CpuWrite(0x8000, 1);
        cartridge.CpuWrite(0x8000, 1);

        cartridge.CpuWrite(0xA000, 0x80);
        SerialWrite(cartridge, 0xE000, 4);

        Assert.Equal(NametableMirroring.Vertical, cartridge.NametableMirroring);
        Assert.Equal(4, cartridge.CpuRead(0x8000));
        Assert.Equal(7, cartridge.CpuRead(0xC000));
    }

    [Fact]
    public void PrgRamDisable_BlocksMappedReadsAndWritesButPreservesStoredData()
    {
        var cartridge = CreateCartridge();
        cartridge.CpuWrite(0x6000, 0x42);
        SerialWrite(cartridge, 0xE000, 0x10);
        cartridge.CpuWrite(0x6000, 0x99);
        Assert.Equal(0, cartridge.CpuRead(0x6000));

        SerialWrite(cartridge, 0xE000, 0x00);

        Assert.Equal(0x42, cartridge.CpuRead(0x6000));
    }

    [Fact]
    public void SuromOuterBankBit_SelectsUpperPrgRomHalf()
    {
        var cartridge = CreateCartridge(prgBanks16K: 32, chrBanks8K: 0);
        SerialWrite(cartridge, 0xA000, 0x10);
        SerialWrite(cartridge, 0xE000, 1);

        Assert.Equal(17, cartridge.CpuRead(0x8000));
        Assert.Equal(31, cartridge.CpuRead(0xC000));
    }

    [Fact]
    public void Debugger_ExposesMapper1CartridgeRam()
    {
        var nes = new Nes();
        nes.LoadRom(CreateRom());
        nes.Debugger.Pause();

        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.CartridgeRam, 7, new byte[] { 0xA5 });
        var data = new byte[8];
        nes.Debugger.CopyMemoryRegion(NesMemoryRegion.CartridgeRam, 0, data);

        Assert.Equal(0x2000, nes.Debugger.GetMemoryRegionSize(NesMemoryRegion.CartridgeRam));
        Assert.Equal(0xA5, data[7]);
    }

    private static Cartridge CreateCartridge(byte prgBanks16K = 8, byte chrBanks8K = 2) =>
        new CartridgeFactory().Create(CreateRom(prgBanks16K, chrBanks8K));

    private static byte[] CreateRom(byte prgBanks16K = 8, byte chrBanks8K = 2)
    {
        var rom = new byte[16 + prgBanks16K * 0x4000 + chrBanks8K * 0x2000];
        rom[0] = (byte)'N'; rom[1] = (byte)'E'; rom[2] = (byte)'S'; rom[3] = 0x1A;
        rom[4] = prgBanks16K;
        rom[5] = chrBanks8K;
        rom[6] = 0x10;
        for (var bank = 0; bank < prgBanks16K; bank++)
            Array.Fill(rom, (byte)bank, 16 + bank * 0x4000, 0x4000);
        var chrStart = 16 + prgBanks16K * 0x4000;
        for (var bank = 0; bank < chrBanks8K * 2; bank++)
            Array.Fill(rom, (byte)bank, chrStart + bank * 0x1000, 0x1000);
        return rom;
    }

    private static void SerialWrite(Cartridge cartridge, ushort address, byte value)
    {
        for (var bit = 0; bit < 5; bit++)
            cartridge.CpuWrite(address, (byte)((value >> bit) & 1));
    }
}
