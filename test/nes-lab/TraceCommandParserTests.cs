namespace Sheep.Nes.Lab.Tests;

public sealed class TraceCommandParserTests
{
    [Fact]
    public void ParseQuery_MapsAllFilters()
    {
        var result = TraceCommandParser.Parse([
            "trace", "query", "--artifact", "trace.json", "--address", "0x4000",
            "--end", "$4017", "--actor", "dmcDma", "--interrupt-edges",
            "--dma-overlap", "--instruction-boundaries", "--max", "12"]);

        var invocation = Assert.IsType<TraceQueryInvocation>(result.Invocation);
        Assert.Equal("trace.json", invocation.ArtifactPath);
        Assert.Equal((ushort)0x4000, invocation.Query.AddressStart);
        Assert.Equal((ushort)0x4017, invocation.Query.AddressEnd);
        Assert.Equal(12, invocation.Query.MaximumRecords);
        Assert.True(invocation.Query.InterruptEdgesOnly);
    }

    [Fact]
    public void ParseDiff_MapsPathsAndContext()
    {
        var result = TraceCommandParser.Parse([
            "trace", "diff", "--expected", "a.json", "--actual", "b.json", "--context", "5"]);

        Assert.Equal(new TraceDiffInvocation("a.json", "b.json", 5), result.Invocation);
    }

    [Fact]
    public void ParseCapture_UsesBoundedDefaultAndAcceptsExplicitTimeout()
    {
        var defaultResult = TraceCommandParser.Parse(["trace", "capture", "--case", "case"]);
        var explicitResult = TraceCommandParser.Parse(
            ["trace", "capture", "--timeout-seconds", "45", "--case", "case"]);

        Assert.Equal(new TraceCaptureInvocation("case", 30), defaultResult.Invocation);
        Assert.Equal(new TraceCaptureInvocation("case", 45), explicitResult.Invocation);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("301")]
    public void ParseCapture_RejectsUnsafeTimeout(string seconds)
    {
        var result = TraceCommandParser.Parse(
            ["trace", "capture", "--case", "case", "--timeout-seconds", seconds]);

        Assert.Null(result.Invocation);
        Assert.Equal("invalid_timeout", result.Error?.Code);
    }

    [Theory]
    [InlineData("trace", "query")]
    [InlineData("trace", "diff", "--expected", "a")]
    [InlineData("trace", "wat")]
    public void Parse_InvalidInvocationReturnsStructuredError(params string[] arguments)
    {
        var result = TraceCommandParser.Parse(arguments);

        Assert.Null(result.Invocation);
        Assert.NotNull(result.Error);
    }
}
