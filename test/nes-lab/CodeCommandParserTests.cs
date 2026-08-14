namespace Sheep.Nes.Lab.Tests;

public sealed class CodeCommandParserTests
{
    [Fact]
    public void Find_UsesRepositorySolutionByDefault()
    {
        var result = CodeCommandParser.Parse(["code", "find", "--symbol", "Cpu", "--max", "4"]);

        var invocation = Assert.IsType<CodeFindInvocation>(result.Invocation);
        Assert.Equal("nes-emulator-3.slnx", invocation.SolutionPath);
        Assert.Equal(4, invocation.MaximumResults);
    }

    [Fact]
    public void Relations_RequireStableSymbolId()
    {
        var result = CodeCommandParser.Parse(["code", "refs"]);

        Assert.Equal("missing_symbol_id", result.Error?.Code);
    }

    [Fact]
    public void Benchmark_ParsesIterationCount()
    {
        var result = CodeCommandParser.Parse([
            "code", "benchmark", "--symbol", "Cpu", "--iterations", "7"]);

        Assert.Equal(7, Assert.IsType<CodeBenchmarkInvocation>(result.Invocation).Iterations);
    }
}
