namespace Sheep.Nes.Lab.Tests;

public sealed class MemoryCommandParserTests
{
    [Fact]
    public void ParseAdd_RequiresTypedContentAndProvenance()
    {
        var result = MemoryCommandParser.Parse([
            "memory", "add", "--kind", "confirmed-fact", "--title", "Fact",
            "--body", "Evidence", "--source", "source.s", "--source-hash", "abc",
            "--line", "12", "--commit", "deadbeef"]);

        var invocation = Assert.IsType<MemoryAddInvocation>(result.Invocation);
        Assert.Equal(EngineeringMemoryKind.ConfirmedFact, invocation.Kind);
        Assert.Equal(12, invocation.LineNumber);
    }

    [Fact]
    public void ParseSearch_MapsKindLimitAndRejectedPolicy()
    {
        var result = MemoryCommandParser.Parse([
            "memory", "search", "--query", "DMC DMA", "--kind", "hypothesis",
            "--max", "5", "--include-rejected"]);

        var invocation = Assert.IsType<MemorySearchInvocation>(result.Invocation);
        Assert.Equal(EngineeringMemoryKind.Hypothesis, invocation.Kind);
        Assert.Equal(5, invocation.MaximumResults);
        Assert.True(invocation.IncludeRejectedHypotheses);
    }

    [Fact]
    public void ParseAddWithoutProvenance_ReturnsStructuredError()
    {
        var result = MemoryCommandParser.Parse([
            "memory", "add", "--kind", "observation", "--title", "x", "--body", "y"]);

        Assert.Equal("missing_provenance", result.Error?.Code);
    }
}
