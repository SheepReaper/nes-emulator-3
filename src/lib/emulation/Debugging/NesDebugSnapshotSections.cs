using System;
namespace Sheep.Emulation.Nes.Debugging;

[Flags]
public enum NesDebugSnapshotSections
{
    None = 0,
    Cpu = 1 << 0,
    Ppu = 1 << 1,
    Apu = 1 << 2,
    Timing = 1 << 3,
    Memory = 1 << 4,
    Disassembly = 1 << 5,
    PatternTables = 1 << 6,
    Core = Cpu | Ppu | Apu | Timing,
    All = Core | Memory | Disassembly | PatternTables
}