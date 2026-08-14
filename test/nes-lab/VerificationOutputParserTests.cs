using Sheep.Nes.Lab;

namespace Sheep.Nes.Lab.Tests;

public sealed class VerificationOutputParserTests
{
    [Fact]
    public void Parse_MicrosoftTestingPlatformSummary_ReturnsCounts()
    {
        const string output = """
            Test run summary: Passed!
              total: 681
              failed: 0
              succeeded: 681
              skipped: 0
              duration: 2s 106ms
            """;

        var summary = VerificationOutputParser.Parse(output);

        Assert.Equal(681, summary.Total);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(681, summary.Succeeded);
        Assert.Equal(0, summary.Skipped);
    }

    [Fact]
    public void Parse_BuildOutput_LeavesTestCountsAbsent()
    {
        var summary = VerificationOutputParser.Parse("Build succeeded.\n    0 Warning(s)\n    0 Error(s)");

        Assert.Null(summary.Total);
        Assert.Null(summary.Failed);
    }

    [Fact]
    public void ParseFailures_MicrosoftTestingPlatformFailure_ReturnsNameAndDiagnostic()
    {
        const string output = """
            failed Sheep.Emulation.Nes.Tests.CpuTests.AddsWithCarry (2ms)
              from tests.dll
              Expected: 2
              Actual:   3
                at Sheep.Emulation.Nes.Tests.CpuTests.AddsWithCarry() in CpuTests.cs:42
            Test run summary: Failed!
            """;

        var failures = VerificationOutputParser.ParseFailures(output);

        var failure = Assert.Single(failures);
        Assert.Equal("Sheep.Emulation.Nes.Tests.CpuTests.AddsWithCarry", failure.Name);
        Assert.Equal("Expected: 2", failure.Diagnostic);
    }
}
