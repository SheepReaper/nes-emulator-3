using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class Mapper4PpuAndIrqTests
{
    [Fact]
    public void IrqCounter_ClocksOnFilteredA12RisingEdgesAndCanBeAcknowledged()
    {
        var interrupts = new InterruptLines();
        var cartridge = Mapper4TestHelper.CreateCartridge(interrupts: interrupts);
        cartridge.CpuWrite(0xC000, 2);
        cartridge.CpuWrite(0xC001, 0);
        cartridge.CpuWrite(0xE001, 0);

        Mapper4TestHelper.ClockA12(cartridge, 0, 8);
        Mapper4TestHelper.ClockA12(cartridge, 9, 17);
        Assert.False(interrupts.Irq);
        Mapper4TestHelper.ClockA12(cartridge, 18, 26);
        Assert.True(interrupts.Irq);

        cartridge.CpuWrite(0xE000, 0);
        Assert.False(interrupts.Irq);
    }

    [Fact]
    public void IrqCounter_IgnoresA12RiseBeforeThreeCompleteLowCpuClocks()
    {
        var interrupts = new InterruptLines();
        var cartridge = Mapper4TestHelper.CreateCartridge(interrupts: interrupts);
        cartridge.CpuWrite(0xC000, 0);
        cartridge.CpuWrite(0xC001, 0);
        cartridge.CpuWrite(0xE001, 0);

        Mapper4TestHelper.ClockA12Low(cartridge, 0);
        Mapper4TestHelper.ClockCpuFilter(cartridge, 3);
        Mapper4TestHelper.NotifyA12High(cartridge, 7);

        Assert.False(interrupts.Irq);
    }

    [Fact]
    public void ScheduledPpuFetches_ClockMapperIrqCounter()
    {
        var interrupts = new InterruptLines();
        var cartridge = Mapper4TestHelper.CreateCartridge(interrupts: interrupts);
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        var ppu = new Ppu(interrupts);
        ppu.ConnectBus(new PpuBus(slot));
        cartridge.CpuWrite(0xC000, 1);
        cartridge.CpuWrite(0xC001, 0);
        cartridge.CpuWrite(0xE001, 0);
        ppu.Write(0x2000, 0x08);
        ppu.Write(0x2001, 0x18);

        for (var dot = 0; dot < 341 * 3 && !interrupts.Irq; dot++)
        {
            ppu.Clock();
            if (dot % 3 == 2)
            {
                Mapper4TestHelper.ClockCpuFilter(cartridge, 1);
            }
        }

        Assert.True(interrupts.Irq);
    }
}
