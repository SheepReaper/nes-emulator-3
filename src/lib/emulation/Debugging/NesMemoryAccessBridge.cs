using System;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Handles memory region validation and safe copy/write delegation.
/// </summary>
internal static class NesMemoryAccessBridge
{
    internal static void Copy(NesSystem nes, NesMemoryRegion region, int offset, Span<byte> destination)
    {
        ValidateSingleRegion(region);
        ValidateRange(offset, destination.Length, NesMemoryInspector.GetSize(nes, region));
        NesMemoryInspector.Copy(nes, region, offset, destination);
    }

    internal static void Write(NesSystem nes, NesMemoryRegion region, int offset, ReadOnlySpan<byte> source)
    {
        ValidateSingleRegion(region);
        ValidateRange(offset, source.Length, NesMemoryInspector.GetSize(nes, region));
        NesMemoryInspector.Write(nes, region, offset, source);
    }

    internal static void ValidateSingleRegion(NesMemoryRegion region)
    {
        var value = (int)region;
        if (value == 0 || (value & (value - 1)) != 0)
        {
            throw new ArgumentException("Specify exactly one memory region.", nameof(region));
        }
    }

    private static void ValidateRange(int offset, int length, int size)
    {
        if (offset < 0 || length < 0 || offset > size - length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "The requested range is outside the memory region.");
        }
    }
}
