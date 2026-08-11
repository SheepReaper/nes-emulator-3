using System.Collections.Generic;
namespace Sheep.Emulation.Nes.Debugging;

public sealed class NesDebugSnapshot(
    CpuDebugState? cpu,
    PpuDebugState? ppu,
    ApuDebugState? apu,
    NesTimingDebugState? timing,
    IReadOnlyList<MemoryRegionSnapshot>? memory,
    IReadOnlyList<DisassembledInstruction>? disassembly,
    IReadOnlyList<PatternTableSnapshot>? patternTables)
{
    public CpuDebugState? Cpu { get; } = cpu;
    public PpuDebugState? Ppu { get; } = ppu;
    public ApuDebugState? Apu { get; } = apu;
    public NesTimingDebugState? Timing { get; } = timing;
    public IReadOnlyList<MemoryRegionSnapshot>? Memory { get; } = memory;
    public IReadOnlyList<DisassembledInstruction>? Disassembly { get; } = disassembly;
    public IReadOnlyList<PatternTableSnapshot>? PatternTables { get; } = patternTables;
}