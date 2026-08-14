namespace Sheep.Nes.Lab.Tests;

public sealed class LabHelpTests
{
    [Fact]
    public void TraceCaptureHelp_ProvidesExactDiscoverableSyntax()
    {
        var help = LabHelp.For(["trace", "capture"]);

        Assert.Contains("trace capture --case <manifest-case-name>", help, StringComparison.Ordinal);
        Assert.Contains("--timeout-seconds", help, StringComparison.Ordinal);
        Assert.Contains("2,048 CPU-clock records", help, StringComparison.Ordinal);
        Assert.Contains("Explicit DMA Abort", help, StringComparison.Ordinal);
    }

    [Fact]
    public void TraceQueryAndDiffHelp_MatchParserOptions()
    {
        var query = LabHelp.For(["trace", "query"]);
        var diff = LabHelp.For(["trace", "diff"]);

        foreach (var option in new[] { "--artifact", "--artifact-uri", "--actor", "--address", "--end",
                     "--interrupt-edges", "--dma-overlap", "--instruction-boundaries", "--max" })
            Assert.Contains(option, query, StringComparison.Ordinal);
        foreach (var option in new[] { "--expected", "--actual", "--context" })
            Assert.Contains(option, diff, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownNestedPath_FallsBackToGeneralHelp()
    {
        var help = LabHelp.For(["unknown", "operation"]);

        Assert.Contains("nes-lab <command> [options]", help, StringComparison.Ordinal);
    }
}
