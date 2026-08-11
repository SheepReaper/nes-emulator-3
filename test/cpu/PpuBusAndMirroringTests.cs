using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class PpuBusAndMirroringTests
{
    [Fact]
    public void PpuBus_PatternTablesRouteToCartridgeAndFourteenBitMirror()
    {
        var cartridge = new RecordingCartridge(NametableMirroring.Vertical);
        var bus = BusTestHelper.CreatePpuBus(cartridge);

        Assert.Equal(0x34, bus.Read(0x1234));
        Assert.Equal(0x34, bus.Read(0x5234));
        bus.Write(0x5ABC, 0x55);

        Assert.Equal((ushort)0x1ABC, cartridge.LastPpuWriteAddress);
        Assert.Equal(0x55, cartridge.LastPpuWriteValue);
    }

    [Theory]
    [InlineData(NametableMirroring.Vertical, 0x2000, 0x2800)]
    [InlineData(NametableMirroring.Vertical, 0x2400, 0x2C00)]
    [InlineData(NametableMirroring.Horizontal, 0x2000, 0x2400)]
    [InlineData(NametableMirroring.Horizontal, 0x2800, 0x2C00)]
    [InlineData(NametableMirroring.SingleScreenLower, 0x2000, 0x2C00)]
    [InlineData(NametableMirroring.SingleScreenUpper, 0x2400, 0x2800)]
    public void PpuBus_NametableMirroringMapsToSelectedPhysicalTable(
        NametableMirroring mirroring, ushort source, ushort mirror)
    {
        var bus = BusTestHelper.CreatePpuBus(new RecordingCartridge(mirroring));
        bus.Write(source, 0x42);
        Assert.Equal(0x42, bus.Read(mirror));
    }

    [Fact]
    public void PpuBus_FourScreenMirroringKeepsAllNametablesDistinct()
    {
        var bus = BusTestHelper.CreatePpuBus(new RecordingCartridge(NametableMirroring.FourScreen));
        bus.Write(0x2000, 0x10);
        bus.Write(0x2400, 0x20);
        bus.Write(0x2800, 0x30);
        bus.Write(0x2C00, 0x40);

        Assert.Equal(0x10, bus.Read(0x2000));
        Assert.Equal(0x20, bus.Read(0x2400));
        Assert.Equal(0x30, bus.Read(0x2800));
        Assert.Equal(0x40, bus.Read(0x2C00));
    }

    [Fact]
    public void PpuBus_WithoutCartridgeUsesHorizontalNametableMirroring()
    {
        var bus = new PpuBus(new CartridgeSlot());
        bus.Write(0x2000, 0x10);
        bus.Write(0x2800, 0x20);

        Assert.Equal(0x10, bus.Read(0x2400));
        Assert.Equal(0x20, bus.Read(0x2C00));
        Assert.NotEqual(bus.Read(0x2000), bus.Read(0x2800));
    }

    [Theory]
    [InlineData(0x2000, 0x3000)]
    [InlineData(0x2EFF, 0x3EFF)]
    public void PpuBus_3000Through3effMirrors2000Through2eff(ushort source, ushort mirror)
    {
        var bus = BusTestHelper.CreatePpuBus(new RecordingCartridge(NametableMirroring.FourScreen));
        bus.Write(source, 0x42);
        Assert.Equal(0x42, bus.Read(mirror));
    }

    [Theory]
    [InlineData(0x3F00, 0x3F20)]
    [InlineData(0x3F1F, 0x3FFF)]
    [InlineData(0x3F00, 0x3F10)]
    [InlineData(0x3F04, 0x3F14)]
    [InlineData(0x3F08, 0x3F18)]
    [InlineData(0x3F0C, 0x3F1C)]
    public void PpuBus_PaletteRamAppliesGeneralAndBackgroundMirrors(ushort source, ushort mirror)
    {
        var bus = BusTestHelper.CreatePpuBus(new RecordingCartridge(NametableMirroring.Vertical));
        bus.Write(source, 0x42);
        Assert.Equal(0x42, bus.Read(mirror));
    }

    [Theory]
    [InlineData(0x00, NametableMirroring.Horizontal)]
    [InlineData(0x01, NametableMirroring.Vertical)]
    [InlineData(0x08, NametableMirroring.FourScreen)]
    [InlineData(0x09, NametableMirroring.FourScreen)]
    public void CartridgeFactory_UsesInesNametableMirroringFlags(byte flags6, NametableMirroring expected)
    {
        var rom = new byte[16 + 0x4000 + 0x2000];
        rom[0] = 0x4E;
        rom[1] = 0x45;
        rom[2] = 0x53;
        rom[3] = 0x1A;
        rom[4] = 1;
        rom[5] = 1;
        rom[6] = flags6;

        var cartridge = new CartridgeFactory().Create(rom);
        Assert.Equal(expected, cartridge.NametableMirroring);
    }
}
