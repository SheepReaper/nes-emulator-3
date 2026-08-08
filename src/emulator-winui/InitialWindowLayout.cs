namespace EmuSheep;

internal readonly record struct InitialWindowBounds(int X, int Y, int Width, int Height);

internal static class InitialWindowLayout
{
    private const double WorkAreaHeightFraction = 0.70;
    private const int MinimumWindowWidth = 560;
    private const int MinimumWindowHeight = 520;
    private const int VerticalChromeHeight = 190;
    private const int HorizontalPadding = 48;

    internal static InitialWindowBounds Calculate(int workAreaWidth, int workAreaHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workAreaWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workAreaHeight);

        var height = Math.Min(
            workAreaHeight,
            Math.Max(MinimumWindowHeight, (int)Math.Round(workAreaHeight * WorkAreaHeightFraction)));
        var displayHeight = Math.Max(1, height - VerticalChromeHeight);
        var contentWidth = (int)Math.Round(displayHeight * 16d / 15d) + HorizontalPadding;
        var width = Math.Min(workAreaWidth, Math.Max(MinimumWindowWidth, contentWidth));

        return new InitialWindowBounds(
            (workAreaWidth - width) / 2,
            (workAreaHeight - height) / 2,
            width,
            height);
    }
}
