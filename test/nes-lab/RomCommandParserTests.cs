namespace Sheep.Nes.Lab.Tests;

public sealed class RomCommandParserTests
{
    [Fact]
    public void ParseShow_RequiresNameAndMapsOverrides()
    {
        var result = RomCommandParser.Parse([
            "rom", "show", "--suite", "apu_test", "--name", "1-len_ctr",
            "--manifest", "manifest.json", "--assets", "roms"]);

        Assert.Equal(new RomShowInvocation(
            "apu_test", "1-len_ctr", "manifest.json", "roms"), result.Invocation);
    }

    [Fact]
    public void ParseList_MapsOptionalSuite()
    {
        var result = RomCommandParser.Parse(["rom", "list", "--suite", "dmc_tests"]);

        Assert.Equal("dmc_tests", Assert.IsType<RomListInvocation>(result.Invocation).Suite);
    }

    [Fact]
    public void ParseList_PreservesMaximumResults()
    {
        var result = RomCommandParser.Parse(["rom", "list", "--max", "2"]);

        Assert.Equal(2, Assert.IsType<RomListInvocation>(result.Invocation).MaximumResults);
    }

    [Fact]
    public void ParseList_DefaultsToConservativeMaximum()
    {
        var result = RomCommandParser.Parse(["rom", "list"]);

        Assert.Equal(32, Assert.IsType<RomListInvocation>(result.Invocation).MaximumResults);
    }

    [Fact]
    public void ParseShowWithoutName_ReturnsStructuredError()
    {
        var result = RomCommandParser.Parse(["rom", "show"]);

        Assert.Equal("missing_rom_name", result.Error?.Code);
    }

    [Fact]
    public void ParseSource_MapsSymbolQueryAndLimit()
    {
        var result = RomCommandParser.Parse([
            "rom", "source", "--suite", "instr_timing", "--name", "1-instr_timing",
            "--symbol", "run_tests", "--max", "12"]);

        var invocation = Assert.IsType<RomSourceInvocation>(result.Invocation);
        Assert.Equal("run_tests", invocation.Symbol);
        Assert.Equal(12, invocation.MaximumResults);
    }

    [Fact]
    public void ParseDiagnose_MapsTerminalCode()
    {
        var result = RomCommandParser.Parse([
            "rom", "diagnose", "--suite", "instr_timing", "--name", "1-instr_timing",
            "--code", "3"]);

        Assert.Equal(3, Assert.IsType<RomDiagnoseInvocation>(result.Invocation).Code);
    }
}
