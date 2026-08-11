using System;
using System.Collections.Generic;
using System.Linq;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Memory region reading, writing, and snapshot capture for debugger.
/// </summary>
internal static class NesMemoryInspector
{
    internal static int GetSize(NesSystem nes, NesMemoryRegion region) => region switch
    {
        NesMemoryRegion.CpuRam => nes.CpuBus.RamSize,
        NesMemoryRegion.PpuVram => nes.PpuBus.VramSize,
        NesMemoryRegion.PaletteRam => nes.PpuBus.PaletteRamSize,
        NesMemoryRegion.Oam => 0x100,
        NesMemoryRegion.PrgRom => nes.Cartridge?.PrgRomSize ?? 0,
        NesMemoryRegion.Chr => nes.Cartridge?.ChrSize ?? 0,
        NesMemoryRegion.CartridgeRam => nes.Cartridge?.CartridgeRamSize ?? 0,
        _ => throw new ArgumentOutOfRangeException(nameof(region))
    };

    internal static void Copy(NesSystem nes, NesMemoryRegion region, int offset, Span<byte> destination)
    {
        if (destination.Length == 0)
        {
            return;
        }
        switch (region)
        {
            case NesMemoryRegion.CpuRam: nes.CpuBus.CopyRam(offset, destination); break;
            case NesMemoryRegion.PpuVram: nes.PpuBus.CopyVram(offset, destination); break;
            case NesMemoryRegion.PaletteRam: nes.PpuBus.CopyPaletteRam(offset, destination); break;
            case NesMemoryRegion.Oam: nes.Ppu.CopyOam(offset, destination); break;
            case NesMemoryRegion.PrgRom: nes.Cartridge!.CopyPrgRom(offset, destination); break;
            case NesMemoryRegion.Chr: nes.Cartridge!.CopyChr(offset, destination); break;
            case NesMemoryRegion.CartridgeRam: nes.Cartridge!.CopyCartridgeRam(offset, destination); break;
            default: throw new ArgumentOutOfRangeException(nameof(region));
        }
    }

    internal static void Write(NesSystem nes, NesMemoryRegion region, int offset, ReadOnlySpan<byte> source)
    {
        switch (region)
        {
            case NesMemoryRegion.CpuRam: nes.CpuBus.WriteRam(offset, source); break;
            case NesMemoryRegion.PpuVram: nes.PpuBus.WriteVram(offset, source); break;
            case NesMemoryRegion.PaletteRam: nes.PpuBus.WritePaletteRam(offset, source); break;
            case NesMemoryRegion.Oam: nes.Ppu.WriteOam(offset, source); break;
            case NesMemoryRegion.Chr:
                if (nes.Cartridge == null || !nes.Cartridge.IsChrWritable)
                {
                    throw new InvalidOperationException("The loaded cartridge contains read-only CHR ROM.");
                }
                nes.Cartridge.WriteChr(offset, source);
                break;
            case NesMemoryRegion.PrgRom:
                throw new InvalidOperationException("PRG ROM is read-only.");
            case NesMemoryRegion.CartridgeRam:
                if (nes.Cartridge == null || nes.Cartridge.CartridgeRamSize == 0)
                {
                    throw new InvalidOperationException("The current cartridge has no writable cartridge RAM.");
                }
                nes.Cartridge.WriteCartridgeRam(offset, source);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(region));
        }
    }

    internal static bool IsWritable(NesSystem nes, NesMemoryRegion region) => region switch
    {
        NesMemoryRegion.CpuRam or NesMemoryRegion.PpuVram or NesMemoryRegion.PaletteRam or NesMemoryRegion.Oam => true,
        NesMemoryRegion.Chr => nes.Cartridge?.IsChrWritable == true,
        NesMemoryRegion.CartridgeRam => GetSize(nes, region) > 0,
        _ => false
    };

    internal static IReadOnlyList<MemoryRegionSnapshot> CaptureSnapshots(NesSystem nes, NesMemoryRegion requested)
    {
        var result = new List<MemoryRegionSnapshot>();
        foreach (var region in Enum.GetValues(typeof(NesMemoryRegion)).Cast<NesMemoryRegion>())
        {
            if (region is NesMemoryRegion.None or NesMemoryRegion.All || !requested.HasFlag(region))
            {
                continue;
            }
            var size = GetSize(nes, region);
            if (size == 0)
            {
                continue;
            }
            var data = new byte[size];
            Copy(nes, region, 0, data);
            result.Add(new MemoryRegionSnapshot(region, data, IsWritable(nes, region)));
        }
        return result.AsReadOnly();
    }
}
