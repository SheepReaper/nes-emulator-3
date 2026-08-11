namespace Sheep.Emulation.Nes.Video;

internal static class NesPalette
{
    private const int ColorsPerEmphasis = 64;
    private const int ComponentsPerColor = 3;
    // Palette model and 2C07 reference output: https://www.nesdev.org/wiki/PPU_programmer_reference#Palettes
    // Commonly used 2C02 RGB approximation. RGB output from an analog NES has no single canonical palette.
    private static readonly byte[] Ntsc =
    [
        84,84,84, 0,30,116, 8,16,144, 48,0,136, 68,0,100, 92,0,48, 84,4,0, 60,24,0,
        32,42,0, 8,58,0, 0,64,0, 0,60,0, 0,50,60, 0,0,0, 0,0,0, 0,0,0,
        152,150,152, 8,76,196, 48,50,236, 92,30,228, 136,20,176, 160,20,100, 152,34,32, 120,60,0,
        84,90,0, 40,114,0, 8,124,0, 0,118,40, 0,102,120, 0,0,0, 0,0,0, 0,0,0,
        236,238,236, 76,154,236, 120,124,236, 176,98,236, 228,84,236, 236,88,180, 236,106,100, 212,136,32,
        160,170,0, 116,196,0, 76,208,32, 56,204,108, 56,180,204, 60,60,60, 0,0,0, 0,0,0,
        236,238,236, 168,204,236, 188,188,236, 212,178,236, 236,174,236, 236,174,212, 236,180,176, 228,196,144,
        204,210,120, 180,222,120, 168,226,144, 152,226,180, 160,214,228, 160,162,160, 0,0,0, 0,0,0
    ];

    // NESdev's Pally-generated 2C07 palette; PAL emphasis swaps the red/green control bits below.
    private static readonly byte[] Pal =
    [
        98,98,98, 0,34,99, 13,16,125, 43,2,125, 68,0,99, 83,0,54, 83,5,2, 68,21,0,
        43,39,0, 13,54,0, 0,62,0, 0,61,2, 0,51,54, 0,0,0, 0,0,0, 0,0,0,
        171,171,171, 18,81,168, 52,56,203, 92,36,203, 126,25,168, 146,27,107, 146,41,36, 126,63,0,
        92,87,0, 52,107,0, 18,118,0, 0,116,36, 0,103,107, 0,0,0, 0,0,0, 0,0,0,
        255,255,255, 98,161,250, 133,137,255, 172,117,255, 207,106,250, 227,108,188, 227,121,117, 207,144,55,
        172,168,20, 133,188,20, 98,199,55, 78,197,117, 78,183,188, 78,78,78, 0,0,0, 0,0,0,
        255,255,255, 196,221,255, 209,211,255, 225,203,255, 239,199,255, 246,200,231, 246,205,203, 239,214,179,
        225,223,166, 209,231,166, 196,235,179, 188,235,203, 188,229,231, 184,184,184, 0,0,0, 0,0,0
    ];

    private static readonly uint[] NtscLookup = BuildLookup(Ntsc, swapRedAndGreenEmphasis: false);
    private static readonly uint[] PalLookup = BuildLookup(Pal, swapRedAndGreenEmphasis: true);

    public static void GetColor(
        NesVideoStandard standard, int color, byte mask, out byte red, out byte green, out byte blue)
    {
        var rgba = GetRgba(standard, color, mask);
        red = (byte)rgba;
        green = (byte)(rgba >> 8);
        blue = (byte)(rgba >> 16);
    }

    internal static uint GetRgba(NesVideoStandard standard, int color, byte mask)
    {
        var emphasis = (mask >> 5) & 0x07;
        var offset = (emphasis * ColorsPerEmphasis) + (color & 0x3F);
        var lookup = standard == NesVideoStandard.Pal ? PalLookup : NtscLookup;
        return lookup[offset];
    }

    internal static uint[] GetLookup(NesVideoStandard standard) =>
        standard == NesVideoStandard.Pal ? PalLookup : NtscLookup;

    private static uint[] BuildLookup(byte[] palette, bool swapRedAndGreenEmphasis)
    {
        var result = new uint[8 * ColorsPerEmphasis];
        for (var maskEmphasis = 0; maskEmphasis < 8; maskEmphasis++)
        {
            var emphasis = swapRedAndGreenEmphasis
                ? ((maskEmphasis & 1) << 1) | ((maskEmphasis & 2) >> 1) | (maskEmphasis & 4)
                : maskEmphasis;
            for (var color = 0; color < ColorsPerEmphasis; color++)
            {
                var source = color * ComponentsPerColor;
                var red = (int)palette[source];
                var green = (int)palette[source + 1];
                var blue = (int)palette[source + 2];
                if ((emphasis & 1) != 0) { green = green * 3 / 4; blue = blue * 3 / 4; }
                if ((emphasis & 2) != 0) { red = red * 3 / 4; blue = blue * 3 / 4; }
                if ((emphasis & 4) != 0) { red = red * 3 / 4; green = green * 3 / 4; }

                var destination = (maskEmphasis * ColorsPerEmphasis) + color;
                result[destination] = (uint)(red | (green << 8) | (blue << 16) | unchecked((int)0xFF000000));
            }
        }
        return result;
    }
}