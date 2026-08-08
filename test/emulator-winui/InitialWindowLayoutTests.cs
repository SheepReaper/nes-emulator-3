using EmuSheep;
using Xunit;

namespace EmuSheep.Tests;

public sealed class InitialWindowLayoutTests
{
    [Fact]
    public void Calculate_UsesSeventyPercentOfWorkAreaHeight_AndCentersWindow()
    {
        var layout = InitialWindowLayout.Calculate(1920, 1080);

        Assert.Equal(756, layout.Height);
        Assert.Equal((1920 - layout.Width) / 2, layout.X);
        Assert.Equal((1080 - layout.Height) / 2, layout.Y);
    }

    [Fact]
    public void Calculate_SizesWidthForTheNesDisplayAndSurroundingChrome()
    {
        var layout = InitialWindowLayout.Calculate(1920, 1080);

        Assert.Equal(652, layout.Width);
    }

    [Fact]
    public void Calculate_NeverExceedsASmallWorkArea()
    {
        var layout = InitialWindowLayout.Calculate(480, 360);

        Assert.InRange(layout.Width, 1, 480);
        Assert.InRange(layout.Height, 1, 360);
        Assert.True(layout.X >= 0);
        Assert.True(layout.Y >= 0);
    }
}
