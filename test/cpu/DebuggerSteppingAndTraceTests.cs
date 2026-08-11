using Sheep.Emulation.Nes.Debugging;

using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class DebuggerSteppingAndTraceTests
{
    [Fact]
    public void PausedSteppingAdvancesTheRequestedMachineBoundary()
    {
        var rom = DebuggerTestHelper.CreateRom(chrBanks: 1);
        rom[16] = 0xEA;
        rom[17] = 0xEA;
        var nes = new NesSystem();
        nes.LoadRom(rom);
        var debugger = nes.Debugger;
        debugger.Pause();

        var initial = debugger.CaptureSnapshot();
        Assert.Equal(1, debugger.StepPpuDot().PpuDots);
        Assert.Equal(initial.Ppu!.Dot + 1, debugger.CaptureSnapshot().Ppu!.Dot);
        var cpuBefore = debugger.CaptureSnapshot().Timing!.CpuClocks;
        debugger.StepCpuCycle();
        Assert.Equal(cpuBefore + 1, debugger.CaptureSnapshot().Timing!.CpuClocks);
        debugger.StepInstruction();
        Assert.True(debugger.CaptureSnapshot().Cpu!.IsInstructionBoundary);
        debugger.StepInstruction();
        Assert.Equal(0x8001, debugger.CaptureSnapshot().Cpu!.ProgramCounter);
        Assert.True(debugger.IsPaused);
    }

    [Fact]
    public void CpuClockTraceRetainsTheMostRecentCpuBusAccesses()
    {
        var rom = DebuggerTestHelper.CreateRom(chrBanks: 1);
        rom[16] = 0xEA;
        var nes = new NesSystem();
        nes.LoadRom(rom);

        nes.Debugger.EnableCpuClockTracing(capacity: 2);
        nes.CpuBus.DebugAccessed = (_, _, _) => { };
        var run = nes.RunForPpuDots(24);

        Assert.Equal(8UL, run.CpuClocks);
        var trace = nes.Debugger.GetCpuClockTrace();
        Assert.Equal(2, trace.Count);
        var last = trace[^1];
        Assert.Equal(7UL, last.CpuClock);
        Assert.Equal(NesCpuClockActor.Cpu, last.Actor);
        Assert.Equal(0x8001, last.PendingBusAddress);
        Assert.False(last.NmiLine);
        Assert.False(last.IrqLine);
        Assert.Contains(last.BusAccesses, access =>
            access.Kind == NesDebugBreakKind.CpuRead && access.Address == 0x8000 && access.Value == 0xEA);
    }

    [Fact]
    public void RunningBatchesReportWorkAndStopWhenPaused()
    {
        var nes = new NesSystem();
        var result = nes.RunForPpuDots(48);
        Assert.Equal(48, result.PpuDots);
        Assert.Equal(16UL, result.CpuClocks);
        Assert.Equal(NesRunStopReason.Completed, result.StopReason);

        nes.Debugger.Pause();
        result = nes.RunForPpuDots(10);
        Assert.Equal(0, result.PpuDots);
        Assert.Equal(NesRunStopReason.Paused, result.StopReason);
    }

    [Fact]
    public void StepFrameAndRunUntilFrameStopAtFramePublication()
    {
        var nes = new NesSystem();
        nes.Debugger.Pause();
        var stepped = nes.Debugger.StepFrame();
        Assert.Equal(1UL, stepped.Frames);
        Assert.True(nes.Debugger.IsPaused);

        nes.Debugger.Resume();
        var run = nes.RunUntilFrame();
        Assert.Equal(1UL, run.Frames);
        Assert.Equal(NesRunStopReason.Completed, run.StopReason);
    }
}
