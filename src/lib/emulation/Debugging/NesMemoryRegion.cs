using System;
namespace Sheep.Emulation.Nes.Debugging;

[Flags]
public enum NesMemoryRegion
{
    None = 0,
    CpuRam = 1 << 0,
    PpuVram = 1 << 1,
    PaletteRam = 1 << 2,
    Oam = 1 << 3,
    PrgRom = 1 << 4,
    Chr = 1 << 5,
    CartridgeRam = 1 << 6,
    All = CpuRam | PpuVram | PaletteRam | Oam | PrgRom | Chr | CartridgeRam
}