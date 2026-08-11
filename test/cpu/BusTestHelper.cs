using System.Reflection;

using NesCpu = Sheep.Emulation.Nes.Cpu.Cpu;

namespace Sheep.Emulation.Nes.Tests;

/// <summary>
/// Helper for instantiating interconnected CPU and PPU bus configurations for testing.
/// </summary>
internal static class BusTestHelper
{
    internal static (CpuBus Bus, RecordingCartridge Cartridge, Ppu Ppu, NesCpu Cpu) CreateCpuBus()
    {
        var interrupts = new InterruptLines();
        var cpu = new NesCpu(interrupts);
        var ppu = new Ppu(interrupts);
        var apu = new Apu(interrupts);
        var cartridge = new RecordingCartridge(NametableMirroring.Vertical);
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        var ppuBus = new PpuBus(slot);
        ppu.ConnectBus(ppuBus);
        var bus = new CpuBus(ppu, apu, slot);
        cpu.ConnectBus(bus);
        return (bus, cartridge, ppu, cpu);
    }

    internal static PpuBus CreatePpuBus(RecordingCartridge cartridge)
    {
        var slot = new CartridgeSlot();
        slot.Insert(cartridge);
        return new PpuBus(slot);
    }

    internal static T GetPrivateField<T>(object instance, string name)
    {
        return instance is NesCpu cpu
            ? name switch
            {
                "_a" => (T)(object)cpu.State.A,
                "_x" => (T)(object)cpu.State.X,
                "_y" => (T)(object)cpu.State.Y,
                "_sp" => (T)(object)cpu.State.SP,
                _ => (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!
        }
            : (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
    }

    internal static void SetPrivateField<T>(object instance, string name, T value)
    {
        if (instance is NesCpu cpu)
        {
            switch (name)
            {
                case "<ProgramCounter>k__BackingField":
                case "_programCounter":
                    cpu.State.ProgramCounter = (ushort)(object)value!;
                    return;
                case "_a":
                    cpu.State.A = (byte)(object)value!;
                    return;
                case "_x":
                    cpu.State.X = (byte)(object)value!;
                    return;
                case "_y":
                    cpu.State.Y = (byte)(object)value!;
                    return;
                case "_sp":
                    cpu.State.SP = (byte)(object)value!;
                    return;
            }
        }
        instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(instance, value);
    }
}
