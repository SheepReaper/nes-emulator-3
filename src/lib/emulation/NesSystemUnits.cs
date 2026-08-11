using System;

using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes;

/// <summary>
/// Holds components and bus connections for NesSystem.
/// </summary>
internal sealed class NesSystemUnits
{
    internal InterruptLines Interrupts { get; } = new();
    internal CartridgeSlot CartridgeSlot { get; } = new();
    internal CartridgeFactory CartridgeFactory { get; }
    internal NesDebugger Debugger { get; }
    internal Cpu.Cpu Cpu { get; }
    internal Ppu Ppu { get; }
    internal Apu Apu { get; }
    internal CpuBus CpuBus { get; }
    internal PpuBus PpuBus { get; }

    internal NesSystemUnits(NesSystem nes, NesVideoStandard videoStandard, NesTiming timing, Action<ulong> onFrameCompleted)
    {
        CartridgeFactory = new CartridgeFactory(Interrupts);
        Cpu = new Cpu.Cpu(Interrupts);
        Ppu = new Ppu(Interrupts, videoStandard, timing);
        Apu = new Apu(Interrupts, timing.ApuRegion, timing.MasterClockHz, timing.CpuDivisor);

        PpuBus = new PpuBus(CartridgeSlot);
        Ppu.ConnectBus(PpuBus);

        CpuBus = new CpuBus(Ppu, Apu, CartridgeSlot);
        CpuBus.ConnectCpu(Cpu);
        Apu.ConnectDmcDma(CpuBus.RequestDmcDma, CpuBus.AbortDmcDma);
        Cpu.ConnectBus(CpuBus);
        Ppu.FrameCompleted += onFrameCompleted;
        Debugger = new NesDebugger(nes);
    }
}
