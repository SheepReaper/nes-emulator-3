using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class PpuRegistersAndMemoryTests
{
    [Fact]
    public void StatusReadReturnsOpenBusBitsAndResetsTheSharedWriteLatch()
    {
        var ppu = PpuTestHelper.CreatePpu(out var bus);
        ppu.Write(0x2000, 0x1F);

        Assert.Equal(0x1F, ppu.Read(0x2002));

        ppu.Write(0x2005, 0x2B);
        _ = ppu.Read(0x2002);
        ppu.Write(0x2006, 0x21);
        ppu.Write(0x2006, 0x05);
        ppu.Write(0x2007, 0x77);
        Assert.Equal(0x77, bus.Memory[0x2105]);
    }

    [Fact]
    public void PpuOpenBusDecaysAfterOneSecondOfPpuDots()
    {
        var ppu = PpuTestHelper.CreatePpu(out _);
        ppu.Write(0x2002, 0xFF);
        ppu.ClockDots(5_369_319);
        Assert.Equal(0, ppu.Read(0x2000));
    }

    [Fact]
    public void PpuDataImplementsBufferedAndImmediatePaletteReads()
    {
        var ppu = PpuTestHelper.CreatePpu(out var bus);
        bus.Memory[0x2000] = 0xAB;
        PpuTestHelper.SetPpuAddress(ppu, 0x2000);
        Assert.Equal(0, ppu.Read(0x2007));
        Assert.Equal(0xAB, ppu.Read(0x2007));

        bus.Memory[0x3F00] = 0xCD;
        bus.Memory[0x2F00] = 0xEF;
        PpuTestHelper.SetPpuAddress(ppu, 0x3F00);
        Assert.Equal(0x0D, ppu.Read(0x2007));
        PpuTestHelper.SetPpuAddress(ppu, 0x2000);
        Assert.Equal(0xEF, ppu.Read(0x2007));
    }

    [Fact]
    public void PpuDataHonorsThirtyTwoByteIncrementMode()
    {
        var ppu = PpuTestHelper.CreatePpu(out var bus);
        ppu.Write(0x2000, 0x04);
        PpuTestHelper.SetPpuAddress(ppu, 0x2100);
        ppu.Write(0x2007, 0x11);
        ppu.Write(0x2007, 0x22);

        Assert.Equal(0x11, bus.Memory[0x2100]);
        Assert.Equal(0x22, bus.Memory[0x2120]);
    }
}
