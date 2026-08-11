namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Step timing tables for the APU frame counter sequencer across regions and modes.
/// </summary>
internal static class ApuFrameSequencerTable
{
    public static readonly int[] NtscFour = [7457, 14914, 22372, 29829, 29830, 29830];
    public static readonly int[] NtscFive = [7457, 14914, 22372, -1, 37282, 37282];
    public static readonly int[] PalFour = [8314, 16628, 24940, 33253, 33254, 33254];
    public static readonly int[] PalFive = [8314, 16628, 24940, -1, 41566, 41566];

    internal static int[] GetTable(ApuRegion region, bool fiveStep)
    {
        return region == ApuRegion.Pal ? fiveStep ? PalFive : PalFour : fiveStep ? NtscFive : NtscFour;
    }
}
