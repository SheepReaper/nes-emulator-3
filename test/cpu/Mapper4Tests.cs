using System.Reflection;
using Xunit;

namespace SR.Emulation.Nes.Tests;

public sealed class Mapper4Tests
{
    [Fact]
    public void SixteenKilobytePrg_IsSupportedForHardwareTestCartridges()
    {
        var cartridge = new Mmc3Cart(
            new byte[0x4000], new byte[0x2000], NametableMirroring.Vertical, false, new InterruptLines());

        Assert.Equal(0, cartridge.CpuRead(0x8000));
        Assert.Equal(0, cartridge.CpuRead(0xE000));
    }

    [Fact]
    public void Factory_CreatesMapper4AndMapsFixedPrgBanks()
    {
        var cartridge = CreateCartridge();

        Assert.IsType<Mmc3Cart>(cartridge);
        Assert.Equal(0, cartridge.CpuRead(0x8000));
        Assert.Equal(0, cartridge.CpuRead(0xA000));
        Assert.Equal(6, cartridge.CpuRead(0xC000));
        Assert.Equal(7, cartridge.CpuRead(0xE000));
    }

    [Fact]
    public void PrgBankRegisters_MapSwitchableBanksInBothModes()
    {
        var cartridge = CreateCartridge();
        WriteBank(cartridge, 6, 3);
        WriteBank(cartridge, 7, 4);

        Assert.Equal(3, cartridge.CpuRead(0x8000));
        Assert.Equal(4, cartridge.CpuRead(0xA000));
        Assert.Equal(6, cartridge.CpuRead(0xC000));

        cartridge.CpuWrite(0x8000, 0x46);

        Assert.Equal(6, cartridge.CpuRead(0x8000));
        Assert.Equal(4, cartridge.CpuRead(0xA000));
        Assert.Equal(3, cartridge.CpuRead(0xC000));
    }

    [Fact]
    public void ChrBankRegisters_MapTwoAndOneKilobyteBanksInBothModes()
    {
        var cartridge = CreateCartridge();
        WriteBank(cartridge, 0, 3);
        WriteBank(cartridge, 1, 6);
        WriteBank(cartridge, 2, 8);
        WriteBank(cartridge, 3, 9);
        WriteBank(cartridge, 4, 10);
        WriteBank(cartridge, 5, 11);

        Assert.Equal(new byte[] { 2, 3, 6, 7, 8, 9, 10, 11 }, ReadChrSlots(cartridge));

        cartridge.CpuWrite(0x8000, 0x80);

        Assert.Equal(new byte[] { 8, 9, 10, 11, 2, 3, 6, 7 }, ReadChrSlots(cartridge));
    }

    [Fact]
    public void MirroringRegister_ChangesMirroringUnlessFourScreenIsHardwired()
    {
        var cartridge = CreateCartridge();
        cartridge.CpuWrite(0xA000, 0);
        Assert.Equal(NametableMirroring.Vertical, cartridge.NametableMirroring);
        cartridge.CpuWrite(0xA000, 1);
        Assert.Equal(NametableMirroring.Horizontal, cartridge.NametableMirroring);

        var fourScreen = CreateCartridge(fourScreen: true);
        fourScreen.CpuWrite(0xA000, 1);
        Assert.Equal(NametableMirroring.FourScreen, fourScreen.NametableMirroring);
    }

    [Fact]
    public void PrgRamProtect_ControlsReadsAndWrites()
    {
        var cartridge = CreateCartridge();
        cartridge.CpuWrite(0x6000, 0x12);
        Assert.Equal(0x12, cartridge.CpuRead(0x6000));

        cartridge.CpuWrite(0xA001, 0xC0);
        cartridge.CpuWrite(0x6000, 0x34);
        Assert.Equal(0x12, cartridge.CpuRead(0x6000));

        cartridge.CpuWrite(0xA001, 0x00);
        Assert.Equal(0, cartridge.CpuRead(0x6000));
    }

    [Fact]
    public void IrqCounter_ClocksOnFilteredA12RisingEdgesAndCanBeAcknowledged()
    {
        var interrupts = new InterruptLines();
        var cartridge = CreateCartridge(interrupts: interrupts);
        cartridge.CpuWrite(0xC000, 2);
        cartridge.CpuWrite(0xC001, 0);
        cartridge.CpuWrite(0xE001, 0);

        ClockA12(cartridge, 0, 8);
        ClockA12(cartridge, 9, 17);
        Assert.False(interrupts.Irq);
        ClockA12(cartridge, 18, 26);
        Assert.True(interrupts.Irq);

        cartridge.CpuWrite(0xE000, 0);
        Assert.False(interrupts.Irq);
    }

    [Fact]
    public void IrqCounter_IgnoresA12RiseBeforeThreeCompleteLowCpuClocks()
    {
        var interrupts = new InterruptLines();
        var cartridge = CreateCartridge(interrupts: interrupts);
        cartridge.CpuWrite(0xC000, 0);
        cartridge.CpuWrite(0xC001, 0);
        cartridge.CpuWrite(0xE001, 0);

        ClockA12Low(cartridge, 0);
        ClockCpuFilter(cartridge, 3);
        NotifyA12High(cartridge, 7);

        Assert.False(interrupts.Irq);
    }

    [Fact]
    public void ScheduledPpuFetches_ClockMapperIrqCounter()
    {
        var interrupts = new InterruptLines();
        var cartridge = CreateCartridge(interrupts: interrupts);
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
            if (dot % 3 == 2) ClockCpuFilter(cartridge, 1);
        }

        Assert.True(interrupts.Irq);
    }

    [Fact]
    public void EmptySpriteSlots_StillFetchFromTheSelectedSpritePatternTableAndClockIrq()
    {
        var interrupts = new InterruptLines();
        var cartridge = CreateCartridge(interrupts: interrupts);
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        var ppu = new Ppu(interrupts);
        ppu.ConnectBus(new PpuBus(slot));
        ppu.Reset();
        ppu.Write(0x2003, 0);
        for (var index = 0; index < 256; index++) ppu.Write(0x2004, 0xFF);
        cartridge.CpuWrite(0xC000, 1);
        cartridge.CpuWrite(0xC001, 0);
        cartridge.CpuWrite(0xE001, 0);
        ppu.Write(0x2000, 0x08); // Background at $0000, sprites at $1000.
        ppu.Write(0x2001, 0x18);

        for (var dot = 0; dot < 341 * 3 && !interrupts.Irq; dot++)
        {
            ppu.Clock();
            if (dot % 3 == 2) ClockCpuFilter(cartridge, 1);
        }

        Assert.True(interrupts.Irq);
    }

    [Fact]
    public void SpritePatternTableA12RiseClocksIrqOnPpuDot260()
    {
        var interrupts = new InterruptLines();
        var cartridge = CreateCartridge(interrupts: interrupts);
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
            if (dot % 3 == 2) ClockCpuFilter(cartridge, 1);
        }
        Assert.False(interrupts.Irq);

        ppu.Clock();

        Assert.True(interrupts.Irq);
    }

    [Fact]
    public void BackgroundPatternTableA12RiseUsesTheAddressPresentationDot()
    {
        var interrupts = new InterruptLines();
        var cartridge = CreateCartridge(interrupts: interrupts);
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
            if (dot % 3 == 2) ClockCpuFilter(cartridge, 1);
        }
        Assert.False(interrupts.Irq);

        ppu.Clock();

        Assert.True(interrupts.Irq);
    }

    [Fact]
    public void PpuPeeks_DoNotClockMapperIrqCounter()
    {
        var interrupts = new InterruptLines();
        var cartridge = CreateCartridge(interrupts: interrupts);
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        var bus = new PpuBus(slot);
        cartridge.CpuWrite(0xC000, 0);
        cartridge.CpuWrite(0xC001, 0);
        cartridge.CpuWrite(0xE001, 0);
        ClockA12Low(cartridge, 0);

        _ = typeof(PpuBus).GetMethod("Peek", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(bus, [(ushort)0x1000]);

        Assert.False(interrupts.Irq);
    }

    [Fact]
    public void PpuDataIncrement_CanClockA12From0fffTo1000()
    {
        var interrupts = new InterruptLines();
        var cartridge = CreateCartridge(interrupts: interrupts);
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        var ppu = new Ppu(interrupts);
        ppu.ConnectBus(new PpuBus(slot));
        cartridge.CpuWrite(0xC000, 0);
        cartridge.CpuWrite(0xC001, 0);
        cartridge.CpuWrite(0xE001, 0);
        ppu.Write(0x2006, 0x0F);
        ppu.Write(0x2006, 0xFF);
        for (var dot = 0; dot < 8; dot++) ppu.Clock();
        ClockCpuFilter(cartridge, 4);

        _ = ppu.Read(0x2007);

        Assert.True(interrupts.Irq);
    }

    [Fact]
    public void Factory_SkipsTrainerBeforeMapper4PrgRom()
    {
        var rom = CreateRom();
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
        var nes = new Nes();
        nes.LoadRom(CreateRom());
        nes.Debugger.Pause();

        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.CartridgeRam, 3, new byte[] { 0x5A });
        var ram = new byte[4];
        nes.Debugger.CopyMemoryRegion(NesMemoryRegion.CartridgeRam, 0, ram);

        Assert.Equal(0x2000, nes.Debugger.GetMemoryRegionSize(NesMemoryRegion.CartridgeRam));
        Assert.Equal(0x5A, ram[3]);
    }

    private static Cartridge CreateCartridge(bool fourScreen = false, InterruptLines? interrupts = null)
    {
        return new CartridgeFactory(interrupts).Create(CreateRom(fourScreen));
    }

    private static byte[] CreateRom(bool fourScreen = false)
    {
        const int prgBanks16K = 4;
        const int chrBanks8K = 2;
        var rom = new byte[16 + prgBanks16K * 0x4000 + chrBanks8K * 0x2000];
        rom[0] = (byte)'N'; rom[1] = (byte)'E'; rom[2] = (byte)'S'; rom[3] = 0x1A;
        rom[4] = prgBanks16K;
        rom[5] = chrBanks8K;
        rom[6] = (byte)(0x40 | (fourScreen ? 0x08 : 0));
        for (var bank = 0; bank < 8; bank++)
            Array.Fill(rom, (byte)bank, 16 + bank * 0x2000, 0x2000);
        var chrStart = 16 + prgBanks16K * 0x4000;
        for (var bank = 0; bank < 16; bank++)
            Array.Fill(rom, (byte)bank, chrStart + bank * 0x0400, 0x0400);
        return rom;
    }

    private static void WriteBank(Cartridge cartridge, byte register, byte bank)
    {
        cartridge.CpuWrite(0x8000, register);
        cartridge.CpuWrite(0x8001, bank);
    }

    private static byte[] ReadChrSlots(Cartridge cartridge) =>
        Enumerable.Range(0, 8).Select(slot => cartridge.PpuRead((ushort)(slot * 0x0400))).ToArray();

    private static void ClockA12(Cartridge cartridge, ulong lowCycle, ulong highCycle)
    {
        ClockA12Low(cartridge, lowCycle);
        ClockCpuFilter(cartridge, 4);
        NotifyA12High(cartridge, highCycle);
    }

    private static void ClockA12Low(Cartridge cartridge, ulong cycle)
    {
        var notify = typeof(Cartridge).GetMethod("NotifyPpuAddress", BindingFlags.Instance | BindingFlags.NonPublic)!;
        notify.Invoke(cartridge, [ushort.MinValue, cycle]);
    }

    private static void NotifyA12High(Cartridge cartridge, ulong cycle)
    {
        var notify = typeof(Cartridge).GetMethod("NotifyPpuAddress", BindingFlags.Instance | BindingFlags.NonPublic)!;
        notify.Invoke(cartridge, [(ushort)0x1000, cycle]);
    }

    private static void ClockCpuFilter(Cartridge cartridge, int clocks)
    {
        var notify = typeof(Cartridge).GetMethod("NotifyCpuClock", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (var clock = 0; clock < clocks; clock++) notify.Invoke(cartridge, null);
    }
}
