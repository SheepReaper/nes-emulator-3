using Sheep.Emulation.Nes.Debugging;

using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class DebuggerBreakpointTests
{
    [Fact]
    public void ExecuteBreakpointStopsBeforeOpcodeFetch()
    {
        var rom = DebuggerTestHelper.CreateRom(chrBanks: 1);
        rom[16] = 0xEA;
        var nes = new NesSystem();
        nes.LoadRom(rom);
        var breakpoint = nes.Debugger.AddBreakpoint(NesDebugBreakKind.Execute, 0x8000);
        BreakOccurredEventArgs? hit = null;
        nes.Debugger.BreakOccurred += (_, args) => hit = args;

        var result = nes.RunForPpuDots(100);

        Assert.Equal(NesRunStopReason.Breakpoint, result.StopReason);
        Assert.True(nes.Debugger.IsPaused);
        Assert.Equal(breakpoint.Id, hit!.Breakpoint.Id);
        Assert.Equal(0x8000, hit.Address);
        Assert.Equal(0x8000, nes.Debugger.CaptureSnapshot().Cpu!.ProgramCounter);
    }

    [Theory]
    [InlineData(NesDebugBreakKind.CpuRead, 0xAD, 0x10, 0x00)]
    [InlineData(NesDebugBreakKind.CpuWrite, 0x8D, 0x10, 0x00)]
    public void MemoryWatchpointPausesAtTheNextInstructionBoundary(
        NesDebugBreakKind kind, byte opcode, byte low, byte high)
    {
        var rom = DebuggerTestHelper.CreateRom(chrBanks: 1);
        rom[16] = opcode;
        rom[17] = low;
        rom[18] = high;
        rom[19] = 0xEA;
        var nes = new NesSystem();
        nes.LoadRom(rom);
        nes.Debugger.AddBreakpoint(kind, 0x0010);
        BreakOccurredEventArgs? hit = null;
        nes.Debugger.BreakOccurred += (_, args) => hit = args;

        var result = nes.RunForPpuDots(200);

        Assert.Equal(NesRunStopReason.Breakpoint, result.StopReason);
        Assert.Equal(0x0010, hit!.Address);
        Assert.Equal(0x8003, nes.Debugger.CaptureSnapshot().Cpu!.ProgramCounter);
        Assert.True(nes.Debugger.CaptureSnapshot().Cpu!.IsInstructionBoundary);
    }

    [Fact]
    public void BreakpointsCanBeDisabledRemovedAndCleared()
    {
        var nes = new NesSystem();
        var first = nes.Debugger.AddBreakpoint(NesDebugBreakKind.Execute, 0x8000, 0x8002);
        var second = nes.Debugger.AddBreakpoint(NesDebugBreakKind.CpuRead, 0x10);
        Assert.True(nes.Debugger.SetBreakpointEnabled(first.Id, false));
        Assert.False(nes.Debugger.GetBreakpoints()[0].IsEnabled);
        Assert.True(nes.Debugger.RemoveBreakpoint(second.Id));
        Assert.Single(nes.Debugger.GetBreakpoints());
        nes.Debugger.ClearBreakpoints();
        Assert.Empty(nes.Debugger.GetBreakpoints());
    }

    [Fact]
    public void DebugPeeksDoNotTriggerWatchpoints()
    {
        var nes = new NesSystem();
        nes.Debugger.AddBreakpoint(NesDebugBreakKind.CpuRead, 0x2002);
        var breaks = 0;
        nes.Debugger.BreakOccurred += (_, _) => breaks++;

        _ = nes.Debugger.PeekCpuMemory(0x2002);
        _ = nes.Debugger.PeekPpuMemory(0);

        Assert.Equal(0, breaks);
        Assert.False(nes.Debugger.IsPaused);
    }

    [Fact]
    public void ControlEventsAreRaisedOnlyWhenExecutionStateChanges()
    {
        var nes = new NesSystem();
        var events = new List<ExecutionStateChangedEventArgs>();
        nes.Debugger.ExecutionStateChanged += (_, args) => events.Add(args);

        nes.Debugger.Pause();
        nes.Debugger.Pause();
        nes.Debugger.Resume();

        Assert.Equal(2, events.Count);
        Assert.Equal(NesExecutionState.Running, events[0].Previous);
        Assert.Equal(NesExecutionState.Paused, events[0].Current);
        Assert.Equal(NesExecutionState.Running, events[1].Current);
    }
}
