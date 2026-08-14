namespace Sheep.Nes.Lab.Tests;

public sealed class DiagnoseCommandParserTests
{
    [Fact]
    public void ExactlyOneRunSelectionIsRequired()
    {
        Assert.Equal("invalid_selection", DiagnoseCommandParser.Parse(["diagnose"]).Error?.Code);
        Assert.Equal("invalid_selection", DiagnoseCommandParser.Parse(
            ["diagnose", "--case", "case", "--run", "latest"]).Error?.Code);
    }

    [Fact]
    public void Persist_IsExplicitForExistingRunDiagnosis()
    {
        var invocation = Assert.IsType<DiagnoseInvocation>(DiagnoseCommandParser.Parse(
            ["diagnose", "--run", "abc", "--persist"]).Invocation);
        Assert.True(invocation.Persist);
    }

    [Fact]
    public void CaseDefaultsToSixteenKilobytePacket()
    {
        var invocation = Assert.IsType<DiagnoseInvocation>(
            DiagnoseCommandParser.Parse(["diagnose", "--case", "Implicit DMA Abort"]).Invocation);
        Assert.Equal(16_000, invocation.BudgetBytes);
    }

    [Fact]
    public void BudgetTokens_AreParsedWhenExplicitlyProvided()
    {
        var invocation = Assert.IsType<DiagnoseInvocation>(DiagnoseCommandParser.Parse([
            "diagnose", "--case", "Implicit DMA Abort", "--budget-tokens", "2048"]).Invocation);
        Assert.Equal(2048, invocation.BudgetTokens);
    }
}
