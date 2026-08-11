using System.Collections.Generic;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Snapshot assembler combining CPU, PPU, APU, timing, and memory debug state.
/// </summary>
internal static class NesSnapshotBuilder
{
    internal static NesDebugSnapshot Capture(
        NesSystem nes,
        NesExecutionState executionState,
        NesDebugSnapshotOptions? options)
    {
        options ??= new NesDebugSnapshotOptions();
        var sections = options.Sections;

        var cpu = sections.HasFlag(NesDebugSnapshotSections.Cpu) ? nes.Cpu.CaptureDebugState() : null;
        var ppu = sections.HasFlag(NesDebugSnapshotSections.Ppu) ? nes.Ppu.CaptureDebugState() : null;
        var apu = sections.HasFlag(NesDebugSnapshotSections.Apu) ? nes.Apu.CaptureDebugState() : null;
        var timing = sections.HasFlag(NesDebugSnapshotSections.Timing)
            ? new NesTimingDebugState(nes.VideoStandard, executionState, nes.CpuClockCounter, nes.CurrentFrameNumber)
            : null;
        var memory = sections.HasFlag(NesDebugSnapshotSections.Memory)
            ? NesMemoryInspector.CaptureSnapshots(nes, options.MemoryRegions)
            : null;
        var disassembly = sections.HasFlag(NesDebugSnapshotSections.Disassembly)
            ? NesDisassembler.Disassemble(nes, options.DisassemblyStartAddress ?? nes.Cpu.CaptureDebugState().ProgramCounter, options.DisassemblyInstructionCount)
            : null;

        IReadOnlyList<PatternTableSnapshot>? patterns = null;
        if (sections.HasFlag(NesDebugSnapshotSections.PatternTables))
        {
            patterns = new List<PatternTableSnapshot>
            {
                NesPatternTableInspector.Capture(nes, 0, options.PatternPalette),
                NesPatternTableInspector.Capture(nes, 1, options.PatternPalette)
            }.AsReadOnly();
        }

        return new NesDebugSnapshot(cpu, ppu, apu, timing, memory, disassembly, patterns);
    }
}
