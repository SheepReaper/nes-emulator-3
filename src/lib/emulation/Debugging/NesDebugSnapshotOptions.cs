namespace Sheep.Emulation.Nes.Debugging;

public sealed class NesDebugSnapshotOptions
{
    public NesDebugSnapshotSections Sections { get; set; } = NesDebugSnapshotSections.Core;
    public NesMemoryRegion MemoryRegions { get; set; } = NesMemoryRegion.None;
    public ushort? DisassemblyStartAddress { get; set; }
    public int DisassemblyInstructionCount { get; set; } = 32;
    public int PatternPalette { get; set; }
}