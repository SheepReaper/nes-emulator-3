using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class BusTests
{
    [Fact]
    public void CpuBus_InternalRamMirrorsEvery0800Bytes()
    {
        var (bus, _, _, _) = BusTestHelper.CreateCpuBus();
        bus.Write(0x0000, 0x12);
        bus.Write(0x07FF, 0x34);

        Assert.Equal(0x12, bus.Read(0x0800));
        Assert.Equal(0x12, bus.Read(0x1000));
        Assert.Equal(0x12, bus.Read(0x1800));
        Assert.Equal(0x34, bus.Read(0x0FFF));
        Assert.Equal(0x34, bus.Read(0x17FF));
        Assert.Equal(0x34, bus.Read(0x1FFF));
    }

    [Fact]
    public void CpuBus_PpuRegistersMirrorEveryEightBytesThrough3fff()
    {
        var (bus, _, ppu, _) = BusTestHelper.CreateCpuBus();
        bus.Write(0x3FFB, 0x20);
        bus.Write(0x3FFC, 0x42);
        bus.Write(0x3FFB, 0x20);

        Assert.Equal(0x42, ppu.Read(0x2004));
        Assert.Equal(0x42, bus.Read(0x3FFC));
    }

    [Fact]
    public void CpuBus_ApuAndIoRangesAreMappedWithoutThrowing()
    {
        var (bus, _, _, _) = BusTestHelper.CreateCpuBus();
        for (ushort address = 0x4000; address <= 0x4013; address++)
        {
            bus.Write(address, 0x55);
        }
        bus.Write(0x4015, 0x55);
        bus.Write(0x4016, 0x55);
        bus.Write(0x4017, 0x55);

        Assert.Equal(0x10, bus.Read(0x4015));
        Assert.Equal(0x40, bus.Read(0x4016));
        Assert.Equal(0x40, bus.Read(0x4017));
    }

    [Fact]
    public void CpuBus_WriteOnlyAndControllerReadsPreserveHardwareOpenBusBits()
    {
        var (bus, _, _, _) = BusTestHelper.CreateCpuBus();
        bus.Write(0x4000, 0xA5);
        Assert.Equal(0xA5, bus.Read(0x4001));

        bus.SetControllerState(0, 0x01);
        bus.Write(0x4016, 0xE0);
        Assert.Equal(0xE1, bus.Read(0x4016));
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0xFF)]
    public void PageCrossingCpuReadCarriesPpuLatchThroughWriteOnlyApuRegister(byte latch)
    {
        var (bus, cartridge, _, cpu) = BusTestHelper.CreateCpuBus();
        cartridge.LoadCpu(0x8000, new byte[]
        {
            0xA9, latch,
            0x8D, 0x02, 0x20,
            0xA2, 0x01,
            0xBD, 0xFF, 0x3F
        });
        BusTestHelper.SetPrivateField(cpu, "<ProgramCounter>k__BackingField", (ushort)0x8000);

        for (ulong cycle = 0; cycle < 13; cycle++)
        {
            cpu.Clock(cycle);
        }

        Assert.Equal(latch, BusTestHelper.GetPrivateField<byte>(cpu, "_a"));
    }
}
