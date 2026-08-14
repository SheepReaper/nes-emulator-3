namespace Sheep.Nes.Lab.Tests;

public sealed class ModelCommandParserTests
{
    [Fact]
    public void Benchmark_HasLocalSafeDefaults()
    {
        var invocation = Assert.IsType<ModelBenchmarkInvocation>(
            ModelCommandParser.Parse(["model", "benchmark"]).Invocation);

        Assert.Equal("nes-lab:devstral-24b", invocation.Model);
        Assert.Equal("http://localhost:11434/", invocation.Endpoint);
    }

    [Fact]
    public void Rank_DefaultsToDeterministicOfflineMode()
    {
        var invocation = Assert.IsType<ModelRankInvocation>(
            ModelCommandParser.Parse(["model", "rank", "--input", "evidence.json"]).Invocation);

        Assert.False(invocation.UseLocalModel);
        Assert.Equal(8, invocation.MaximumResults);
    }

    [Fact]
    public void Rank_CanExplicitlyEnableLocalModel()
    {
        var invocation = Assert.IsType<ModelRankInvocation>(ModelCommandParser.Parse(
            ["model", "rank", "--input", "evidence.json", "--max", "3", "--local", "true"]).Invocation);

        Assert.True(invocation.UseLocalModel);
        Assert.Equal(3, invocation.MaximumResults);
    }
}
