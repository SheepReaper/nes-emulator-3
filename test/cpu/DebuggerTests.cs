using Sheep.Emulation.Nes.Debugging;

using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class DebuggerTests
{
    [Fact]
    public void DebuggerCanPauseAndResumeTheWholeMachine()
    {
        var nes = new NesSystem();
        var debugger = nes.Debugger;

        Assert.Equal(NesExecutionState.Running, debugger.ExecutionState);
        debugger.Pause();
        Assert.True(debugger.IsPaused);
        var paused = debugger.CaptureSnapshot();
        nes.Clock();
        var stillPaused = debugger.CaptureSnapshot();
        Assert.Equal(paused.Timing!.CpuClocks, stillPaused.Timing!.CpuClocks);
        Assert.Equal(paused.Ppu!.Dot, stillPaused.Ppu!.Dot);

        debugger.Resume();
        nes.Clock();
        Assert.Equal(NesExecutionState.Running, debugger.ExecutionState);
        Assert.NotEqual(stillPaused.Ppu.Dot, debugger.CaptureSnapshot().Ppu!.Dot);
    }

    [Fact]
    public void DefaultSnapshotContainsCopiedCoreState()
    {
        var nes = new NesSystem(NesVideoStandard.Pal);
        var snapshot = nes.Debugger.CaptureSnapshot();

        Assert.NotNull(snapshot.Cpu);
        Assert.Equal(0xFD, snapshot.Cpu.StackPointer);
        Assert.Equal(0x24, snapshot.Cpu.Status);
        Assert.True(snapshot.Cpu.IsInstructionBoundary is false);
        Assert.NotNull(snapshot.Ppu);
        Assert.Equal(0, snapshot.Ppu.Scanline);
        Assert.Equal(0, snapshot.Ppu.Dot);
        Assert.NotNull(snapshot.Apu);
        Assert.True(snapshot.Apu.IsImplemented);
        Assert.Equal(0x18, snapshot.Apu.Registers.Length);
        Assert.NotNull(snapshot.Timing);
        Assert.Equal(NesVideoStandard.Pal, snapshot.Timing.VideoStandard);
    }

    [Fact]
    public void ProgramCounterCanBeInspectedWithoutCapturingASnapshot()
    {
        var nes = new NesSystem();
        nes.Debugger.Pause();
        nes.Debugger.SetCpuRegisters(new CpuRegisterValues(0, 0, 0, 0xFD, 0x8123, 0x24));
        Assert.Equal(0x8123, nes.Debugger.ProgramCounter);
    }

    [Fact]
    public void SnapshotSectionsCanExcludeSubsystems()
    {
        var nes = new NesSystem();
        var snapshot = nes.Debugger.CaptureSnapshot(new NesDebugSnapshotOptions
        {
            Sections = NesDebugSnapshotSections.Cpu
        });

        Assert.NotNull(snapshot.Cpu);
        Assert.Null(snapshot.Ppu);
        Assert.Null(snapshot.Apu);
        Assert.Null(snapshot.Timing);
    }

    [Fact]
    public void CpuPeekDoesNotApplyPpuStatusReadSideEffects()
    {
        var nes = new NesSystem();
        for (var i = 0; i < (241 * 341) + 2; i++)
        {
            nes.Clock();
        }
        nes.Debugger.Pause();

        Assert.NotEqual(0, nes.Debugger.PeekCpuMemory(0x2002) & 0x80);
        Assert.NotEqual(0, nes.Debugger.PeekCpuMemory(0x2002) & 0x80);
        Assert.NotEqual(0, nes.Debugger.CaptureSnapshot().Ppu!.Status & 0x80);
    }

    [Fact]
    public void PausedRegisterAndDeviceEditsAreReflectedInSnapshots()
    {
        var nes = new NesSystem();
        nes.Debugger.Pause();
        nes.Debugger.SetCpuRegisters(new CpuRegisterValues(1, 2, 3, 4, 0x8123, 0));
        nes.Debugger.WritePpuRegister(0x2000, 0x84);
        nes.Debugger.WriteApuRegister(0x4002, 0x66);

        var snapshot = nes.Debugger.CaptureSnapshot();
        Assert.Equal(1, snapshot.Cpu!.Accumulator);
        Assert.Equal(0x8123, snapshot.Cpu.ProgramCounter);
        Assert.Equal(0x20, snapshot.Cpu.Status);
        Assert.Equal(0x84, snapshot.Ppu!.Control);
        Assert.Equal(0x66, snapshot.Apu!.Registers.Span[2]);
    }
}
