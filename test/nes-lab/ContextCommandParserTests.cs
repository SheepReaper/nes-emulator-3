namespace Sheep.Nes.Lab.Tests;

public sealed class ContextCommandParserTests
{
    [Fact]
    public void Build_ParsesSymbolAndBudget()
    {
        var result = ContextCommandParser.Parse([
            "context", "build", "--symbol", "Cpu", "--budget", "4096", "--budget-tokens", "1024"]);

        var invocation = Assert.IsType<ContextBuildInvocation>(result.Invocation);
        Assert.Equal(4096, invocation.BudgetBytes);
        Assert.Equal(1024, invocation.BudgetTokens);
        Assert.Equal("Cpu", invocation.Symbol);
    }

    [Fact]
    public void Build_ParsesStableIdWithoutSymbol()
    {
        var result = ContextCommandParser.Parse([
            "context", "build", "--id", "symbol-123", "--budget", "2048"]);

        var invocation = Assert.IsType<ContextBuildInvocation>(result.Invocation);
        Assert.Equal("symbol-123", invocation.SymbolId);
        Assert.Null(invocation.Symbol);
    }

    [Fact]
    public void Build_ParsesSymbolDisambiguationFilters()
    {
        var result = ContextCommandParser.Parse([
            "context", "build", "--symbol", "Cpu", "--qualified", "Sheep.Emulation.Nes.Cpu",
            "--kind", "NamedType", "--project", "Sheep.Emulation.Nes",
            "--namespace", "Sheep.Emulation.Nes", "--file", "Cpu.cs"]);

        var invocation = Assert.IsType<ContextBuildInvocation>(result.Invocation);
        Assert.Equal("Sheep.Emulation.Nes.Cpu", invocation.ExactQualifiedName);
        Assert.Equal("NamedType", invocation.Kind);
        Assert.Equal("Sheep.Emulation.Nes", invocation.Project);
        Assert.Equal("Sheep.Emulation.Nes", invocation.Namespace);
        Assert.Equal("Cpu.cs", invocation.FilePath);
    }

    [Fact]
    public void Build_RejectsSymbolAndStableIdTogether()
    {
        var result = ContextCommandParser.Parse([
            "context", "build", "--symbol", "Cpu", "--id", "symbol-123"]);

        Assert.Null(result.Invocation);
        Assert.Equal("conflicting_context_selectors", result.Error?.Code);
    }

    [Theory]
    [InlineData("--changed", null)]
    [InlineData("--subsystem", "ppu")]
    [InlineData("--task", "fix sprite priority")]
    [InlineData("--run", "latest")]
    public void Build_ParsesGeneralContextSelector(string option, string? value)
    {
        List<string> arguments = ["context", "build", option];
        if (value is not null) arguments.Add(value);

        var result = ContextCommandParser.Parse(arguments);

        var invocation = Assert.IsType<ContextBuildInvocation>(result.Invocation);
        Assert.True(invocation.Changed || invocation.Subsystem is not null ||
            invocation.Task is not null || invocation.RunId is not null);
    }

    [Fact]
    public void Build_ParsesChangedBaseRevision()
    {
        var result = ContextCommandParser.Parse([
            "context", "build", "--changed", "--base", "origin/main"]);

        var invocation = Assert.IsType<ContextBuildInvocation>(result.Invocation);
        Assert.True(invocation.Changed);
        Assert.Equal("origin/main", invocation.BaseRevision);
    }
}
