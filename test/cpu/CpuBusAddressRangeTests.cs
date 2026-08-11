using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuBusAddressRangeTests
{
    [Theory]
    [InlineData(0x6000)]
    [InlineData(0x7FFF)]
    [InlineData(0x8000)]
    [InlineData(0xFFFF)]
    public void CpuBus_CartridgeOwnsEntire4020ThroughFfffRange(ushort address)
    {
        var (bus, cartridge, _, _) = BusTestHelper.CreateCpuBus();
        Assert.Equal((byte)address, bus.Read(address));
        Assert.Equal(address, cartridge.LastCpuReadAddress);

        bus.Write(address, 0xA5);
        Assert.Equal(address, cartridge.LastCpuWriteAddress);
        Assert.Equal(0xA5, cartridge.LastCpuWriteValue);
    }

    [Fact]
    public void CpuBus_UnmappedCartridgeExpansionRangeReturnsTheExistingDataBusValue()
    {
        var (bus, cartridge, _, _) = BusTestHelper.CreateCpuBus();
        bus.Write(0x4000, 0x5A);

        Assert.Equal(0x5A, bus.Read(0x5000));
        Assert.Null(cartridge.LastCpuReadAddress);
    }

    [Theory]
    [InlineData(0x4018)]
    [InlineData(0x401F)]
    public void CpuBus_DisabledTestRangeDoesNotReachCartridge(ushort address)
    {
        var (bus, cartridge, _, _) = BusTestHelper.CreateCpuBus();
        Assert.Equal(0, bus.Read(address));
        bus.Write(address, 0xA5);

        Assert.Null(cartridge.LastCpuReadAddress);
        Assert.Null(cartridge.LastCpuWriteAddress);
    }
}
