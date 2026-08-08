namespace EmuSheep;

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

        for (var index = 0; index < source.Length; index += 4)
        {
            var red = source[index];
            var green = source[index + 1];
            var blue = source[index + 2];
            var alpha = source[index + 3];

            destination[index] = blue;
            destination[index + 1] = green;
            destination[index + 2] = red;
            destination[index + 3] = alpha;
        }
    }
}
