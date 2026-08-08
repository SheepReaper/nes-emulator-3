using Xunit;

namespace EmuSheep.Tests;

public sealed class StartupRomOptionsTests
{
    [Fact]
    public void Parse_ReturnsNullWhenRomArgumentIsAbsent() =>
        Assert.Null(StartupRomOptions.Parse(["EmuSheep.exe"]));

    [Fact]
    public void Parse_ReturnsRomPathFollowingRomOption() =>
        Assert.Equal(
            @"C:\ROM Files\nestest.nes",
            StartupRomOptions.Parse(["EmuSheep.exe", "--rom", @"C:\ROM Files\nestest.nes"]));

    [Fact]
    public void Parse_RejectsRomOptionWithoutPath() =>
        Assert.Throws<ArgumentException>(() => StartupRomOptions.Parse(["EmuSheep.exe", "--rom"]));

    [Fact]
    public void Parse_RejectsUnknownArguments() =>
        Assert.Throws<ArgumentException>(() => StartupRomOptions.Parse(["EmuSheep.exe", "--unknown"]));
}
