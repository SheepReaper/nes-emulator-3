namespace Sheep.Nes.Lab.Tests;

public sealed class HostUsageTelemetryTests
{
    [Fact]
    public void Parse_PreservesReportedUsageAndLeavesUnknownValuesUnavailable()
    {
        var telemetry = HostUsageTelemetry.Parse("""{"provider":"codex","cloudInputTokens":123,"gatewayCalls":4}""");

        Assert.Equal(123, telemetry.CloudInputTokens);
        Assert.Null(telemetry.CloudOutputTokens);
        Assert.Equal("Unavailable", telemetry.CloudOutputTokensStatus);
    }

    [Fact]
    public void Parse_RejectsNegativeUsage()
    {
        Assert.Throws<ArgumentException>(() => HostUsageTelemetry.Parse("""{"cloudInputTokens":-1}"""));
    }
}
