using System;
using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes;

/// <summary>
/// Clock driver coordinating PPU and CPU clock advancement and batch execution.
/// </summary>
internal static class NesClockDriver
{
    internal static void ClockCore(
        Ppu ppu,
        ref int cpuAccumulator,
        int ppuDivisor,
        int cpuDivisor,
        Action clockCpu)
    {
        ppu.Clock();
        cpuAccumulator += ppuDivisor;
        if (cpuAccumulator >= cpuDivisor)
        {
            clockCpu();
        }
    }

    internal static void ClockCpuCore(
        ref int cpuAccumulator,
        int cpuDivisor,
        NesDebugger debugger,
        Audio.Apu apu,
        Cpu.Cpu cpu,
        Cpu.CpuBus cpuBus,
        Cartridges.CartridgeSlot slot,
        ref ulong cpuClockCounter)
    {
        cpuAccumulator -= cpuDivisor;
        debugger.BeginCpuClockTraceLocked();
        var actor = NesCpuClockActor.Cpu;
        apu.Clock();
        if (!cpu.CanHaltForDma)
        {
            cpuBus.NotifyWriteCycle();
            cpu.Clock(cpuClockCounter);
        }
        else if (!cpuBus.ClockDma(cpuClockCounter, cpu.DmaReadAddress, out actor))
        {
            cpu.Clock(cpuClockCounter);
        }
        else
        {
            cpu.NotifyDmaHalt();
        }

        slot.NotifyCpuClock();
        debugger.CompleteCpuClockTraceLocked(actor);
        cpuClockCounter++;
    }

    internal static bool ExecuteDotLocked(
        NesSystem nes,
        NesDebugger debugger,
        bool checkBreakpoints,
        out FrameReadyEventArgs? frameReady)
    {
        frameReady = null;
        if (checkBreakpoints && debugger.TryBreakBeforeCpuClockLocked())
        {
            return false;
        }

        nes.ClockCoreInternal();
        frameReady = nes.TakePendingFrameInternal();
        if (checkBreakpoints)
        {
            debugger.CompleteDotLocked();
        }
        return true;
    }
}
