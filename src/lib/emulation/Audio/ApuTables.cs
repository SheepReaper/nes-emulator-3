namespace Sheep.Emulation.Nes.Audio;

internal static class ApuTables
{
    internal static readonly byte[] Length =
    [10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
     12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30];

    internal static readonly ushort[] NoiseNtsc =
    [4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068];

    internal static readonly ushort[] NoisePal =
    [4, 8, 14, 30, 60, 88, 118, 148, 188, 236, 354, 472, 708, 944, 1890, 3778];

    internal static readonly ushort[] DmcNtsc =
    [428, 380, 340, 320, 286, 254, 226, 214, 190, 160, 142, 128, 106, 84, 72, 54];

    internal static readonly ushort[] DmcPal =
    [398, 354, 316, 298, 276, 236, 210, 198, 176, 148, 132, 118, 98, 78, 66, 50];
}
