namespace Sheep.Nes.Lab.Tests;

public sealed class AccuracyCoinSourceResolverTests
{
    [Theory]
    [InlineData("Delta Modulation Channel", "TEST_DeltaModulationChannel")]
    [InlineData("Implicit DMA Abort", "TEST_ImplicitDMAAbort")]
    public void Resolve_FollowsMenuEntryToTestRoutine(string caseName, string routine)
    {
        var source = Assert.IsType<AccuracyCoinCaseSource>(
            AccuracyCoinSourceResolver.Resolve(Directory.GetCurrentDirectory(), caseName, 0x0A));

        Assert.Equal(routine, source.RoutineSymbol);
        Assert.StartsWith(routine + ":", source.RoutineBody);
        Assert.DoesNotContain($"table \"{caseName}\"", source.RoutineBody);
        Assert.Equal(2, source.ErrorCode);
        Assert.False(source.Passed);
    }
}
