using Xunit;

namespace SR.Emulation.Nes.Tests;

public sealed class DebuggerTests
{
    [Fact]
    public void DebuggerCanPauseAndResumeTheWholeMachine()
    {
        var nes = new Nes();
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
    public void RequestedSpeedMultiplierIsValidatedAndPreserved()
    {
        var nes = new Nes();
        nes.RequestedSpeedMultiplier = 2.5;
        Assert.Equal(2.5, nes.RequestedSpeedMultiplier);
        Assert.Throws<ArgumentOutOfRangeException>(() => nes.RequestedSpeedMultiplier = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => nes.RequestedSpeedMultiplier = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => nes.RequestedSpeedMultiplier = double.PositiveInfinity);
    }

    [Fact]
    public void DefaultSnapshotContainsCopiedCoreState()
    {
        var nes = new Nes(NesVideoStandard.Pal);
        var snapshot = nes.Debugger.CaptureSnapshot();

        Assert.NotNull(snapshot.Cpu);
        Assert.Equal(0xFD, snapshot.Cpu.StackPointer);
        Assert.Equal(0x24, snapshot.Cpu.Status);
        Assert.True(snapshot.Cpu.IsInstructionBoundary is false); // Reset begins with seven remaining cycles.
        Assert.NotNull(snapshot.Ppu);
        Assert.Equal(0, snapshot.Ppu.Scanline);
        Assert.Equal(0, snapshot.Ppu.Dot);
        Assert.NotNull(snapshot.Apu);
        Assert.False(snapshot.Apu.IsImplemented);
        Assert.Equal(0x18, snapshot.Apu.Registers.Length);
        Assert.NotNull(snapshot.Timing);
        Assert.Equal(NesVideoStandard.Pal, snapshot.Timing.VideoStandard);
    }

    [Fact]
    public void SnapshotSectionsCanExcludeSubsystems()
    {
        var nes = new Nes();
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
    public void PhysicalMemoryRegionsCanBeCopiedAndOnlyWritableRegionsCanBeEditedWhilePaused()
    {
        var nes = new Nes();
        nes.LoadRom(CreateRom(chrBanks: 1));
        var debugger = nes.Debugger;

        Assert.Equal(0x0800, debugger.GetMemoryRegionSize(NesMemoryRegion.CpuRam));
        Assert.Equal(0x1000, debugger.GetMemoryRegionSize(NesMemoryRegion.PpuVram));
        Assert.Equal(0x20, debugger.GetMemoryRegionSize(NesMemoryRegion.PaletteRam));
        Assert.Equal(0x100, debugger.GetMemoryRegionSize(NesMemoryRegion.Oam));
        Assert.Equal(0x4000, debugger.GetMemoryRegionSize(NesMemoryRegion.PrgRom));
        Assert.Equal(0x2000, debugger.GetMemoryRegionSize(NesMemoryRegion.Chr));
        Assert.Throws<InvalidOperationException>(() =>
            debugger.WriteMemoryRegion(NesMemoryRegion.CpuRam, 0, new byte[] { 0x42 }));

        debugger.Pause();
        debugger.WriteMemoryRegion(NesMemoryRegion.CpuRam, 0x12, new byte[] { 0x42, 0x43 });
        var copied = new byte[2];
        debugger.CopyMemoryRegion(NesMemoryRegion.CpuRam, 0x12, copied);
        Assert.Equal(new byte[] { 0x42, 0x43 }, copied);
        Assert.Throws<InvalidOperationException>(() =>
            debugger.WriteMemoryRegion(NesMemoryRegion.PrgRom, 0, new byte[] { 1 }));
        Assert.Throws<InvalidOperationException>(() =>
            debugger.WriteMemoryRegion(NesMemoryRegion.Chr, 0, new byte[] { 1 }));
    }

    [Fact]
    public void ChrRamIsAllocatedAndWritableWhenRomHasNoChrBanks()
    {
        var nes = new Nes();
        nes.LoadRom(CreateRom(chrBanks: 0));
        nes.Debugger.Pause();

        Assert.Equal(0x2000, nes.Debugger.GetMemoryRegionSize(NesMemoryRegion.Chr));
        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.Chr, 7, new byte[] { 0xA5 });
        Assert.Equal(0xA5, nes.Debugger.PeekPpuMemory(7));
    }

    [Fact]
    public void CpuPeekDoesNotApplyPpuStatusReadSideEffects()
    {
        var nes = new Nes();
        for (var i = 0; i < (241 * 341) + 2; i++) nes.Clock();
        nes.Debugger.Pause();

        Assert.NotEqual(0, nes.Debugger.PeekCpuMemory(0x2002) & 0x80);
        Assert.NotEqual(0, nes.Debugger.PeekCpuMemory(0x2002) & 0x80);
        Assert.NotEqual(0, nes.Debugger.CaptureSnapshot().Ppu!.Status & 0x80);
    }

    [Fact]
    public void PausedRegisterAndDeviceEditsAreReflectedInSnapshots()
    {
        var nes = new Nes();
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

    [Fact]
    public void MemorySnapshotCopiesOnlyRequestedRegions()
    {
        var nes = new Nes();
        nes.Debugger.Pause();
        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.CpuRam, 0, new byte[] { 0x31 });
        var snapshot = nes.Debugger.CaptureSnapshot(new NesDebugSnapshotOptions
        {
            Sections = NesDebugSnapshotSections.Memory,
            MemoryRegions = NesMemoryRegion.CpuRam | NesMemoryRegion.PaletteRam
        });

        Assert.Equal(2, snapshot.Memory!.Count);
        Assert.Contains(snapshot.Memory, x => x.Region == NesMemoryRegion.CpuRam && x.Data.Span[0] == 0x31);
        Assert.DoesNotContain(snapshot.Memory, x => x.Region == NesMemoryRegion.Oam);
    }

    [Fact]
    public void DisassemblyReturnsStructuredOperandsAndUnsupportedDataBytes()
    {
        var rom = CreateRom(chrBanks: 1);
        new byte[] { 0xA9, 0x42, 0x8D, 0x00, 0x20, 0xD0, 0xFC, 0x6C, 0xFF, 0x10, 0x02 }
            .CopyTo(rom, 16);
        var nes = new Nes();
        nes.LoadRom(rom);

        var lines = nes.Debugger.Disassemble(0x8000, 5);

        Assert.Equal("LDA", lines[0].Mnemonic);
        Assert.Equal("#$42", lines[0].Operand);
        Assert.Equal(CpuAddressingMode.Immediate, lines[0].AddressingMode);
        Assert.True(lines[0].IsCurrent);
        Assert.Equal("$2000", lines[1].Operand);
        Assert.Equal("$8003", lines[2].Operand);
        Assert.Equal("($10FF)", lines[3].Operand);
        Assert.Equal(".db", lines[4].Mnemonic);
        Assert.Equal("$02", lines[4].Operand);
    }

    [Fact]
    public void DisassemblyWrapsMappedCpuAddressesWithoutSideEffects()
    {
        var nes = new Nes();
        var rom = CreateRom(chrBanks: 1);
        rom[16 + 0x3FFF] = 0xA9;
        nes.LoadRom(rom);
        nes.Debugger.Pause();
        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.CpuRam, 0, new byte[] { 0x7F });

        var lines = nes.Debugger.Disassemble(0xFFFF, 1);

        Assert.Equal(2, lines[0].Length);
        Assert.Equal(0x7F, lines[0].Bytes.Span[1]);
    }

    [Fact]
    public void PatternTableIsDecodedAsRgbaWithTheSelectedPalette()
    {
        var rom = CreateRom(chrBanks: 1);
        for (var row = 0; row < 8; row++) rom[16 + 0x4000 + row] = 0xFF;
        var nes = new Nes();
        nes.LoadRom(rom);
        nes.Debugger.Pause();
        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.PaletteRam, 1, new byte[] { 0x30 });

        var pattern = nes.Debugger.CapturePatternTable(0, 0);

        Assert.Equal(0, pattern.TableIndex);
        Assert.Equal(PatternTableSnapshot.Width * PatternTableSnapshot.Height * 4, pattern.Rgba.Length);
        Assert.Equal(new byte[] { 236, 238, 236, 255 }, pattern.Rgba.Span[..4].ToArray());
    }

    [Fact]
    public void SnapshotCanIncludeDisassemblyAndBothPatternTables()
    {
        var nes = new Nes();
        nes.LoadRom(CreateRom(chrBanks: 1));
        var snapshot = nes.Debugger.CaptureSnapshot(new NesDebugSnapshotOptions
        {
            Sections = NesDebugSnapshotSections.Disassembly | NesDebugSnapshotSections.PatternTables,
            DisassemblyInstructionCount = 3,
            PatternPalette = 0
        });

        Assert.Equal(3, snapshot.Disassembly!.Count);
        Assert.Equal(2, snapshot.PatternTables!.Count);
    }

    [Fact]
    public void PausedSteppingAdvancesTheRequestedMachineBoundary()
    {
        var rom = CreateRom(chrBanks: 1);
        rom[16] = 0xEA;
        rom[17] = 0xEA;
        var nes = new Nes();
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
    public void RunningBatchesReportWorkAndStopWhenPaused()
    {
        var nes = new Nes();
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
    public void ExecuteBreakpointStopsBeforeOpcodeFetch()
    {
        var rom = CreateRom(chrBanks: 1);
        rom[16] = 0xEA;
        var nes = new Nes();
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
    [InlineData(NesDebugBreakKind.CpuRead, 0xAD, 0x10, 0x00)] // LDA $0010
    [InlineData(NesDebugBreakKind.CpuWrite, 0x8D, 0x10, 0x00)] // STA $0010
    public void MemoryWatchpointPausesAtTheNextInstructionBoundary(
        NesDebugBreakKind kind, byte opcode, byte low, byte high)
    {
        var rom = CreateRom(chrBanks: 1);
        rom[16] = opcode; rom[17] = low; rom[18] = high; rom[19] = 0xEA;
        var nes = new Nes();
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
        var nes = new Nes();
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
    public void StepFrameAndRunUntilFrameStopAtFramePublication()
    {
        var nes = new Nes();
        nes.Debugger.Pause();
        var stepped = nes.Debugger.StepFrame();
        Assert.Equal(1UL, stepped.Frames);
        Assert.True(nes.Debugger.IsPaused);

        nes.Debugger.Resume();
        var run = nes.RunUntilFrame();
        Assert.Equal(1UL, run.Frames);
        Assert.Equal(NesRunStopReason.Completed, run.StopReason);
    }

    [Fact]
    public void DebugPeeksDoNotTriggerWatchpoints()
    {
        var nes = new Nes();
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
        var nes = new Nes();
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

    [Fact]
    public void MemorySnapshotsRemainUnchangedAfterEmulatorMemoryIsEdited()
    {
        var nes = new Nes();
        nes.Debugger.Pause();
        var options = new NesDebugSnapshotOptions
        {
            Sections = NesDebugSnapshotSections.Memory,
            MemoryRegions = NesMemoryRegion.CpuRam
        };
        var before = nes.Debugger.CaptureSnapshot(options);
        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.CpuRam, 0, new byte[] { 0xEE });

        Assert.Equal(0, before.Memory![0].Data.Span[0]);
        Assert.Equal(0xEE, nes.Debugger.CaptureSnapshot(options).Memory![0].Data.Span[0]);
    }

    private static byte[] CreateRom(byte chrBanks)
    {
        var rom = new byte[16 + 0x4000 + (chrBanks * 0x2000)];
        rom[0] = (byte)'N'; rom[1] = (byte)'E'; rom[2] = (byte)'S'; rom[3] = 0x1A;
        rom[4] = 1;
        rom[5] = chrBanks;
        rom[16 + 0x3FFC] = 0x00;
        rom[16 + 0x3FFD] = 0x80;
        return rom;
    }
}
