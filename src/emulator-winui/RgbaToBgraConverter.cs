namespace EmuSheep;

using System.Runtime.InteropServices;

public static class RgbaToBgraConverter
{
    /// <summary>
    /// Converts complete RGBA8888 pixels to BGRA8888 pixels without allocating.
    /// </summary>
    /// <param name="source">The source RGBA8888 pixels.</param>
    /// <param name="destination">The destination buffer, which must be the same size as <paramref name="source"/>.</param>
    public static void Convert(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length != destination.Length || source.Length % 4 != 0)
        {
            throw new ArgumentException("Source and destination must contain the same number of complete RGBA pixels.");
        }

        if (BitConverter.IsLittleEndian)
        {
            var sourcePixels = MemoryMarshal.Cast<byte, uint>(source);
            var destinationPixels = MemoryMarshal.Cast<byte, uint>(destination);
            for (var index = 0; index < sourcePixels.Length; index++)
            {
                var rgba = sourcePixels[index];
                destinationPixels[index] = (rgba & 0xFF00FF00u) |
                    ((rgba & 0x000000FFu) << 16) |
                    ((rgba & 0x00FF0000u) >> 16);
            }
            return;
        }

        for (var index = 0; index < source.Length; index += 4)
        {
            destination[index] = source[index + 2];
            destination[index + 1] = source[index + 1];
            destination[index + 2] = source[index];
            destination[index + 3] = source[index + 3];
        }
    }
}
