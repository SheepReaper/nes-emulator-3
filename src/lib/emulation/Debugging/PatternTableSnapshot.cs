using System;
namespace Sheep.Emulation.Nes.Debugging;

public sealed class PatternTableSnapshot(int tableIndex, int paletteIndex, ReadOnlyMemory<byte> rgba)
{
    public const int Width = 128;
    public const int Height = 128;
    public int TableIndex { get; } = tableIndex;
    public int PaletteIndex { get; } = paletteIndex;
    public ReadOnlyMemory<byte> Rgba { get; } = rgba;
}