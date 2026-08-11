using System.Reflection;

namespace Sheep.Emulation.Nes.Tests;

/// <summary>
/// Rom building and A12 signal generation helpers for Mapper 4 (MMC3) tests.
/// </summary>
internal static class Mapper4TestHelper
{
    internal static Cartridge CreateCartridge(bool fourScreen = false, InterruptLines? interrupts = null) =>
        new CartridgeFactory(interrupts).Create(CreateRom(fourScreen));

    internal static byte[] CreateRom(bool fourScreen = false)
    {
        const int prgBanks16K = 4;
        const int chrBanks8K = 2;
        var rom = new byte[16 + prgBanks16K * 0x4000 + chrBanks8K * 0x2000];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = prgBanks16K;
        rom[5] = chrBanks8K;
        rom[6] = (byte)(0x40 | (fourScreen ? 0x08 : 0));

        for (var bank = 0; bank < 8; bank++)
        {
            Array.Fill(rom, (byte)bank, 16 + bank * 0x2000, 0x2000);
        }

        var chrStart = 16 + prgBanks16K * 0x4000;
        for (var bank = 0; bank < 16; bank++)
        {
            Array.Fill(rom, (byte)bank, chrStart + bank * 0x0400, 0x0400);
        }
        return rom;
    }

    internal static void WriteBank(Cartridge cartridge, byte register, byte bank)
    {
        cartridge.CpuWrite(0x8000, register);
        cartridge.CpuWrite(0x8001, bank);
    }

    internal static byte[] ReadChrSlots(Cartridge cartridge) =>
        Enumerable.Range(0, 8).Select(slot => cartridge.PpuRead((ushort)(slot * 0x0400))).ToArray();

    internal static void ClockA12(Cartridge cartridge, ulong lowCycle, ulong highCycle)
    {
        ClockA12Low(cartridge, lowCycle);
        ClockCpuFilter(cartridge, 4);
        NotifyA12High(cartridge, highCycle);
    }

    internal static void ClockA12Low(Cartridge cartridge, ulong cycle)
    {
        var notify = typeof(Cartridge).GetMethod("NotifyPpuAddress", BindingFlags.Instance | BindingFlags.NonPublic)!;
        notify.Invoke(cartridge, [ushort.MinValue, cycle]);
    }

    internal static void NotifyA12High(Cartridge cartridge, ulong cycle)
    {
        var notify = typeof(Cartridge).GetMethod("NotifyPpuAddress", BindingFlags.Instance | BindingFlags.NonPublic)!;
        notify.Invoke(cartridge, [(ushort)0x1000, cycle]);
    }

    internal static void ClockCpuFilter(Cartridge cartridge, int clocks)
    {
        var notify = typeof(Cartridge).GetMethod("NotifyCpuClock", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (var clock = 0; clock < clocks; clock++)
        {
            notify.Invoke(cartridge, null);
        }
    }
}
