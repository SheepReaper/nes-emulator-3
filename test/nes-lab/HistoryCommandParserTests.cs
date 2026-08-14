namespace Sheep.Nes.Lab.Tests;

public sealed class HistoryCommandParserTests
{
    [Theory]
    [InlineData("runs", "latest", "runs-latest")]
    [InlineData("failures", "latest", "failures-latest")]
    public void LatestCommandsParse(string group, string verb, string expected)
    {
        var invocation = Assert.IsType<HistoryInvocation>(HistoryCommandParser.Parse([group, verb]).Invocation);
        Assert.Equal(expected, invocation.Operation);
    }

    [Fact]
    public void FailureSearchRequiresQuery()
    {
        Assert.Equal("missing_query", HistoryCommandParser.Parse(["failures", "search"]).Error?.Code);
    }

    [Theory]
    [InlineData("history", "runs", "latest", "runs-latest")]
    [InlineData("history", "failures", "latest", "failures-latest")]
    [InlineData("history", "metrics", null, "metrics")]
    public void SemanticHistoryAliasParses(string history, string group, string? verb, string expected)
    {
        var args = verb is null ? new[] { history, group } : new[] { history, group, verb };
        var invocation = Assert.IsType<HistoryInvocation>(HistoryCommandParser.Parse(args).Invocation);
        Assert.Equal(expected, invocation.Operation);
    }
}
