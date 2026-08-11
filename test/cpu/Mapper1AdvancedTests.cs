using Sheep.Emulation.Nes.Debugging;
using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class Mapper1AdvancedTests
{
    [Fact]
    public void ResetWrite_ClearsPartialSerialValueAndForcesFixedLastPrgMode()
    {
        var cartridge = Mapper1TestHelper.CreateCartridge();
        Mapper1TestHelper.SerialWrite(cartridge, 0x8000, 0x0A);
        Mapper1TestHelper.SerialWrite(cartridge, 0xE000, 3);
        cartridge.CpuWrite(0x8000, 1);
        cartridge.CpuWrite(0x8000, 1);

        cartridge.CpuWrite(0xA000, 0x80);
        Mapper1TestHelper.SerialWrite(cartridge, 0xE000, 4);

        Assert.Equal(NametableMirroring.Vertical, cartridge.NametableMirroring);
        Assert.Equal(4, cartridge.CpuRead(0x8000));
        Assert.Equal(7, cartridge.CpuRead(0xC000));
    }

    [Fact]
    public void PrgRamDisable_BlocksMappedReadsAndWritesButPreservesStoredData()
    {
        var cartridge = Mapper1TestHelper.CreateCartridge();
        cartridge.CpuWrite(0x6000, 0x42);
        Mapper1TestHelper.SerialWrite(cartridge, 0xE000, 0x10);
        cartridge.CpuWrite(0x6000, 0x99);
        Assert.Equal(0, cartridge.CpuRead(0x6000));

        Mapper1TestHelper.SerialWrite(cartridge, 0xE000, 0x00);
        Assert.Equal(0x42, cartridge.CpuRead(0x6000));
    }

    [Fact]
    public void SuromOuterBankBit_SelectsUpperPrgRomHalf()
    {
        var cartridge = Mapper1TestHelper.CreateCartridge(prgBanks16K: 32, chrBanks8K: 0);
        Mapper1TestHelper.SerialWrite(cartridge, 0xA000, 0x10);
        Mapper1TestHelper.SerialWrite(cartridge, 0xE000, 1);

        Assert.Equal(17, cartridge.CpuRead(0x8000));
        Assert.Equal(31, cartridge.CpuRead(0xC000));
    }

    [Fact]
    public void Debugger_ExposesMapper1CartridgeRam()
    {
        var nes = new NesSystem();
        nes.LoadRom(Mapper1TestHelper.CreateRom());
        nes.Debugger.Pause();

        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.CartridgeRam, 7, [0xA5]);
        var data = new byte[8];
        nes.Debugger.CopyMemoryRegion(NesMemoryRegion.CartridgeRam, 0, data);

        Assert.Equal(0x2000, nes.Debugger.GetMemoryRegionSize(NesMemoryRegion.CartridgeRam));
        Assert.Equal(0xA5, data[7]);
    }
}
