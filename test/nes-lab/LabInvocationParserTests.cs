using Sheep.Nes.Lab;

namespace Sheep.Nes.Lab.Tests;

public sealed class LabInvocationParserTests
{
    [Fact]
    public void Parse_NamedConformanceCase()
    {
        var result = LabInvocationParser.Parse([
            "verify", "--scope", "conformance", "--case", "Implicit DMA Abort"]);

        Assert.Equal("Implicit DMA Abort", result.Invocation?.CaseName);
    }

    [Fact]
    public void Parse_CaseOutsideConformanceFails()
    {
        var result = LabInvocationParser.Parse(["verify", "--scope", "cpu", "--case", "case"]);

        Assert.Equal("invalid_case_scope", result.Error?.Code);
    }

    [Fact]
    public void Parse_TraceOnFailureRequiresNamedConformanceCase()
    {
        var valid = LabInvocationParser.Parse([
            "verify", "--scope", "conformance", "--case", "Implicit DMA Abort",
            "--trace-on-failure"]);
        var invalid = LabInvocationParser.Parse([
            "verify", "--scope", "conformance", "--trace-on-failure"]);

        Assert.True(valid.Invocation?.TraceOnFailure);
        Assert.Equal("trace_requires_case", invalid.Error?.Code);
    }

    [Fact]
    public void Parse_TraceAlwaysRequiresNamedConformanceCase()
    {
        var valid = LabInvocationParser.Parse([
            "verify", "--scope", "conformance", "--case", "Instruction Timing",
            "--trace-always"]);

        Assert.True(valid.Invocation?.TraceAlways);
    }
    [Fact]
    public void Parse_VerifyCpuPlan_ReturnsTypedInvocation()
    {
        var result = LabInvocationParser.Parse(["verify", "--scope", "cpu", "--plan-only"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(VerificationScope.Cpu, result.Invocation!.Scope);
        Assert.True(result.Invocation.PlanOnly);
        Assert.Equal(LabOutputFormat.Json, result.Invocation.Format);
    }

    [Fact]
    public void Parse_UnknownScope_ReturnsStructuredError()
    {
        var result = LabInvocationParser.Parse(["verify", "--scope", "sound"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_scope", result.Error!.Code);
        Assert.Contains("sound", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ppu", VerificationScope.Ppu)]
    [InlineData("apu", VerificationScope.Apu)]
    [InlineData("dma", VerificationScope.Dma)]
    [InlineData("bus", VerificationScope.Bus)]
    [InlineData("mapper", VerificationScope.Mapper)]
    [InlineData("cartridge", VerificationScope.Cartridge)]
    [InlineData("debugger", VerificationScope.Debugger)]
    [InlineData("winui-video", VerificationScope.WinUiVideo)]
    [InlineData("winui-audio", VerificationScope.WinUiAudio)]
    public void Parse_LogicalScope_ReturnsTypedInvocation(string value, VerificationScope expected)
    {
        var result = LabInvocationParser.Parse(["verify", "--scope", value]);
        Assert.Equal(expected, result.Invocation?.Scope);
    }

    [Fact]
    public void Parse_MissingCommand_ReturnsUsageError()
    {
        var result = LabInvocationParser.Parse([]);

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_command", result.Error!.Code);
    }

    [Fact]
    public void Parse_ContinueOnFailure_EnablesExplicitBatchPolicy()
    {
        var result = LabInvocationParser.Parse(["verify", "--continue-on-failure"]);

        Assert.True(result.IsSuccess);
        Assert.True(result.Invocation!.ContinueOnFailure);
    }

    [Fact]
    public void Parse_Changed_SelectsRepositoryChanges()
    {
        var result = LabInvocationParser.Parse(["verify", "--changed"]);

        Assert.True(result.IsSuccess);
        Assert.True(result.Invocation!.Changed);
    }

    [Fact]
    public void Parse_BaselineAwareExitCode_SelectsOptInPolicy()
    {
        var result = LabInvocationParser.Parse(["verify", "--scope", "conformance",
            "--baseline-aware-exit-code"]);

        Assert.Equal(VerificationExitPolicy.BaselineAware, result.Invocation?.ExitPolicy);
    }

    [Fact]
    public void Parse_ChangedAndScope_ReturnsConflict()
    {
        var result = LabInvocationParser.Parse(["verify", "--changed", "--scope", "cpu"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("conflicting_selection", result.Error!.Code);
    }
}
