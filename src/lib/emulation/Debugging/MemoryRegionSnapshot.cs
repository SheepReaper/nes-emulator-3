using System;
namespace Sheep.Emulation.Nes.Debugging;

public sealed class MemoryRegionSnapshot(NesMemoryRegion region, ReadOnlyMemory<byte> data, bool isWritable)
{
    public NesMemoryRegion Region { get; } = region;
    public ReadOnlyMemory<byte> Data { get; } = data;
    public bool IsWritable { get; } = isWritable;
}