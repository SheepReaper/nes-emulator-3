namespace Sheep.Nes.Lab.Tests;

public sealed class McpResponseLimitsTests
{
    [Theory]
    [InlineData(32 * 1024)]
    [InlineData(64 * 1024)]
    [InlineData(128 * 1024)]
    public void EnsureResponseIsBounded_AcceptsExactBoundaryAndRejectsNextByte(int limit)
    {
        McpResponseLimits.EnsureResponseIsBounded(new string('x', limit), limit);
        Assert.Throws<InvalidDataException>(() =>
            McpResponseLimits.EnsureResponseIsBounded(new string('x', limit + 1), limit));
    }

    [Fact]
    public void RoutineLimits_DoNotUseEmergencyCeiling()
    {
        Assert.Equal(32 * 1024, McpResponseLimits.MaximumDiscoveryBytes);
        Assert.Equal(64 * 1024, McpResponseLimits.MaximumInspectionBytes);
        Assert.Equal(64 * 1024, McpResponseLimits.MaximumResourceBytes);
        Assert.Equal(128 * 1024, McpResponseLimits.MaximumContextBytes);
        Assert.True(McpResponseLimits.MaximumContextBytes < McpResponseLimits.EmergencyCeilingBytes);
    }
}
