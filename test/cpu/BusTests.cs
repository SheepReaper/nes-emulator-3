using System;
using System.Reflection;

using Xunit;

namespace SR.Emulation.Nes.Tests;

public sealed class BusTests
{
    [Fact]
    public void CpuBus_InternalRamMirrorsEvery0800Bytes()
    {
        var (bus, _, _, _) = CreateCpuBus();

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
        var (bus, _, ppu, _) = CreateCpuBus();

        bus.Write(0x3FFB, 0x20); // Mirror of OAMADDR ($2003)
        bus.Write(0x3FFC, 0x42); // Mirror of OAMDATA ($2004)
        bus.Write(0x3FFB, 0x20);

        Assert.Equal(0x42, ppu.Read(0x2004));
        Assert.Equal(0x42, bus.Read(0x3FFC));
    }

    [Fact]
    public void CpuBus_ApuAndIoRangesAreMappedWithoutThrowing()
    {
        var (bus, _, _, _) = CreateCpuBus();

        for (ushort address = 0x4000; address <= 0x4013; address++) bus.Write(address, 0x55);
        bus.Write(0x4015, 0x55);
        bus.Write(0x4016, 0x55);
        bus.Write(0x4017, 0x55);

        Assert.Equal(0, bus.Read(0x4015));
        Assert.Equal(0, bus.Read(0x4016));
        Assert.Equal(0, bus.Read(0x4017));
    }

    [Theory]
    [InlineData(0x4020)]
    [InlineData(0x5FFF)]
    [InlineData(0x6000)]
    [InlineData(0x7FFF)]
    [InlineData(0x8000)]
    [InlineData(0xFFFF)]
    public void CpuBus_CartridgeOwnsEntire4020ThroughFfffRange(ushort address)
    {
        var (bus, cartridge, _, _) = CreateCpuBus();

        Assert.Equal((byte)address, bus.Read(address));
        Assert.Equal(address, cartridge.LastCpuReadAddress);

        bus.Write(address, 0xA5);
        Assert.Equal(address, cartridge.LastCpuWriteAddress);
        Assert.Equal(0xA5, cartridge.LastCpuWriteValue);
    }

    [Theory]
    [InlineData(0x4018)]
    [InlineData(0x401F)]
    public void CpuBus_DisabledTestRangeDoesNotReachCartridge(ushort address)
    {
        var (bus, cartridge, _, _) = CreateCpuBus();

        Assert.Equal(0, bus.Read(address));
        bus.Write(address, 0xA5);

        Assert.Null(cartridge.LastCpuReadAddress);
        Assert.Null(cartridge.LastCpuWriteAddress);
    }

    [Fact]
    public void CpuBus_OamDmaCopiesPageAndStallsCpu()
    {
        var (bus, _, ppu, cpu) = CreateCpuBus();
        for (var i = 0; i < 256; i++) bus.Write((ushort)(0x0200 + i), (byte)i);
        ppu.Write(0x2003, 0x80);

        bus.Write(0x4014, 0x02);

        Assert.Equal(513, GetPrivateField<int>(cpu, "_cycles"));
        ppu.Write(0x2003, 0x80);
        Assert.Equal(0x00, ppu.Read(0x2004));
        ppu.Write(0x2003, 0x7F);
        Assert.Equal(0xFF, ppu.Read(0x2004));
    }

    [Fact]
    public void CpuBus_OamDmaAddsAlignmentCycleOnOddCpuCycle()
    {
        var (bus, _, _, cpu) = CreateCpuBus();
        SetPrivateField(cpu, "_masterClock", 1UL);

        bus.Write(0x4014, 0x00);

        Assert.Equal(514, GetPrivateField<int>(cpu, "_cycles"));
    }

    [Fact]
    public void CpuBus_ControllerPortsLatchAndShiftBothControllers()
    {
        var (bus, _, _, _) = CreateCpuBus();
        bus.SetControllerState(0, 0b0101_0101);
        bus.SetControllerState(1, 0b1010_1010);

        bus.Write(0x4016, 1);
        bus.Write(0x4016, 0);

        byte[] controller1 = new byte[8];
        byte[] controller2 = new byte[8];
        for (var i = 0; i < 8; i++)
        {
            controller1[i] = bus.Read(0x4016);
            controller2[i] = bus.Read(0x4017);
        }

        Assert.Equal(new byte[] { 1, 0, 1, 0, 1, 0, 1, 0 }, controller1);
        Assert.Equal(new byte[] { 0, 1, 0, 1, 0, 1, 0, 1 }, controller2);
        Assert.Equal(1, bus.Read(0x4016));
        Assert.Equal(1, bus.Read(0x4017));
    }

    [Fact]
    public void PpuBus_PatternTablesRouteToCartridgeAndFourteenBitMirror()
    {
        var cartridge = new RecordingCartridge(NametableMirroring.Vertical);
        var bus = CreatePpuBus(cartridge);

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
        var bus = CreatePpuBus(new RecordingCartridge(mirroring));

        bus.Write(source, 0x42);

        Assert.Equal(0x42, bus.Read(mirror));
    }

    [Fact]
    public void PpuBus_FourScreenMirroringKeepsAllNametablesDistinct()
    {
        var bus = CreatePpuBus(new RecordingCartridge(NametableMirroring.FourScreen));

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
        var bus = CreatePpuBus(new RecordingCartridge(NametableMirroring.FourScreen));

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
        var bus = CreatePpuBus(new RecordingCartridge(NametableMirroring.Vertical));

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
        rom[0] = 0x4E; rom[1] = 0x45; rom[2] = 0x53; rom[3] = 0x1A;
        rom[4] = 1;
        rom[5] = 1;
        rom[6] = flags6;

        var cartridge = new CartridgeFactory().Create(rom);

        Assert.Equal(expected, cartridge.NametableMirroring);
    }

    private static (CpuBus Bus, RecordingCartridge Cartridge, Ppu Ppu, Cpu Cpu) CreateCpuBus()
    {
        var interrupts = new InterruptLines();
        var cpu = new Cpu(interrupts);
        var ppu = new Ppu(interrupts);
        var apu = new Apu(interrupts);
        var cartridge = new RecordingCartridge(NametableMirroring.Vertical);
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        var ppuBus = new PpuBus(slot);
        ppu.ConnectBus(ppuBus);
        var bus = new CpuBus(cpu, ppu, apu, slot);
        cpu.ConnectBus(bus);
        return (bus, cartridge, ppu, cpu);
    }

    private static PpuBus CreatePpuBus(RecordingCartridge cartridge)
    {
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        return new PpuBus(slot);
    }

    private static T GetPrivateField<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;

    private static void SetPrivateField<T>(object instance, string name, T value) =>
        instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(instance, value);

    private sealed class RecordingCartridge(NametableMirroring mirroring)
        : Cartridge(new byte[0x8000], new byte[0x2000], mirroring)
    {
        public ushort? LastCpuReadAddress { get; private set; }
        public ushort? LastCpuWriteAddress { get; private set; }
        public byte LastCpuWriteValue { get; private set; }
        public ushort? LastPpuWriteAddress { get; private set; }
        public byte LastPpuWriteValue { get; private set; }

        public override byte CpuRead(ushort address)
        {
            LastCpuReadAddress = address;
            return (byte)address;
        }

        public override void CpuWrite(ushort address, byte value)
        {
            LastCpuWriteAddress = address;
            LastCpuWriteValue = value;
        }

        public override byte PpuRead(ushort address) => (byte)address;

        public override void PpuWrite(ushort address, byte value)
        {
            LastPpuWriteAddress = address;
            LastPpuWriteValue = value;
        }
    }
}
