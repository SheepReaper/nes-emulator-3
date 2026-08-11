using System.Reflection;

using Sheep.Emulation.Nes.Debugging;

using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class Mapper4IntegrationTests
{
    [Fact]
    public void BackgroundPatternTableA12RiseUsesTheAddressPresentationDot()
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
        ppu.Write(0x2000, 0x10);
        ppu.Write(0x2001, 0x18);

        for (var dot = 0; dot < 324; dot++)
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

    [Fact]
    public void PpuPeeks_DoNotClockMapperIrqCounter()
    {
        var interrupts = new InterruptLines();
        var cartridge = Mapper4TestHelper.CreateCartridge(interrupts: interrupts);
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        var bus = new PpuBus(slot);
        cartridge.CpuWrite(0xC000, 0);
        cartridge.CpuWrite(0xC001, 0);
        cartridge.CpuWrite(0xE001, 0);
        Mapper4TestHelper.ClockA12Low(cartridge, 0);

        _ = typeof(PpuBus).GetMethod("Peek", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(bus, [(ushort)0x1000]);

        Assert.False(interrupts.Irq);
    }

    [Fact]
    public void PpuDataIncrement_CanClockA12From0fffTo1000()
    {
        var interrupts = new InterruptLines();
        var cartridge = Mapper4TestHelper.CreateCartridge(interrupts: interrupts);
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        var ppu = new Ppu(interrupts);
        ppu.ConnectBus(new PpuBus(slot));
        cartridge.CpuWrite(0xC000, 0);
        cartridge.CpuWrite(0xC001, 0);
        cartridge.CpuWrite(0xE001, 0);
        ppu.Write(0x2006, 0x0F);
        ppu.Write(0x2006, 0xFF);
        for (var dot = 0; dot < 8; dot++)
        {
            ppu.Clock();
        }
        Mapper4TestHelper.ClockCpuFilter(cartridge, 4);

        _ = ppu.Read(0x2007);
        Assert.True(interrupts.Irq);
    }

    [Fact]
    public void Factory_SkipsTrainerBeforeMapper4PrgRom()
    {
        var rom = Mapper4TestHelper.CreateRom();
        var withTrainer = new byte[rom.Length + 512];
        Array.Copy(rom, 0, withTrainer, 0, 16);
        withTrainer[6] |= 0x04;
        Array.Fill(withTrainer, (byte)0xCC, 16, 512);
        Array.Copy(rom, 16, withTrainer, 16 + 512, rom.Length - 16);

        var cartridge = new CartridgeFactory().Create(withTrainer);
        Assert.Equal(0, cartridge.CpuRead(0x8000));
        Assert.Equal(7, cartridge.CpuRead(0xE000));
    }

    [Fact]
    public void Debugger_ExposesAndEditsMapper4CartridgeRamWhilePaused()
    {
        var nes = new NesSystem();
        nes.LoadRom(Mapper4TestHelper.CreateRom());
        nes.Debugger.Pause();

        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.CartridgeRam, 3, [0x5A]);
        var ram = new byte[4];
        nes.Debugger.CopyMemoryRegion(NesMemoryRegion.CartridgeRam, 0, ram);

        Assert.Equal(0x2000, nes.Debugger.GetMemoryRegionSize(NesMemoryRegion.CartridgeRam));
        Assert.Equal(0x5A, ram[3]);
    }
}
