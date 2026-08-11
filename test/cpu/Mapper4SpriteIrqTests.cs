using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class Mapper4SpriteIrqTests
{
    [Fact]
    public void EmptySpriteSlots_StillFetchFromTheSelectedSpritePatternTableAndClockIrq()
    {
        var interrupts = new InterruptLines();
        var cartridge = Mapper4TestHelper.CreateCartridge(interrupts: interrupts);
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        var ppu = new Ppu(interrupts);
        ppu.ConnectBus(new PpuBus(slot));
        ppu.Reset();
        ppu.Write(0x2003, 0);
        for (var index = 0; index < 256; index++)
        {
            ppu.Write(0x2004, 0xFF);
        }
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

    [Fact]
    public void SpritePatternTableA12RiseClocksIrqOnPpuDot260()
    {
        var interrupts = new InterruptLines();
        var cartridge = Mapper4TestHelper.CreateCartridge(interrupts: interrupts);
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        var ppu = new Ppu(interrupts);
        ppu.ConnectBus(new PpuBus(slot));
        ppu.Reset();
        cartridge.CpuWrite(0xC000, 0);
        cartridge.CpuWrite(0xC001, 0);
        cartridge.CpuWrite(0xE001, 0);
        ppu.Write(0x2000, 0x08);
        ppu.Write(0x2001, 0x18);

        for (var dot = 0; dot < 260; dot++)
        {
            ppu.Clock();
            if (dot % 3 == 2)
            {
                Mapper4TestHelper.ClockCpuFilter(cartridge, 1);
            }
        }
        Assert.False(interrupts.Irq);

        ppu.Clock();
        Assert.True(interrupts.Irq);
    }
}
