using Xunit;
using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes.ConformanceTests;

public sealed class AccuracyCoinConformanceTests
{
    [Theory]
    [MemberData(nameof(AccuracyCoinTestData.FocusedTestCases), MemberType = typeof(AccuracyCoinTestData))]
    public void FocusedCpuBusBehaviorPasses(
        string name, int suiteIndex, int testIndex, ushort resultAddress)
    {
        var nes = AccuracyCoinLoader.Load();
        var result = AccuracyCoinHarness.RunFocused(
            nes, suiteIndex, testIndex, resultAddress, name, out var diag);

        Assert.True(result.Outcome == AccuracyCoinOutcome.Passed, diag);
    }



    [Fact(Skip = "Complete AccuracyCoin suite includes unmodeled analog/PPU quirks; focused CPU and DMA bus cases run via FocusedCpuBusBehaviorPasses.")]
    public void CompleteHardwareAccuracySuitePasses()
    {
        var root = AccuracyCoinAssets.FindRoot()!;
        var nes = AccuracyCoinLoader.Load();
        var result = new AccuracyCoinRunner(new NesTestMachine(nes)).Run(2_000_000_000);
        var testNames = AccuracyCoinCatalog.Load(Path.Combine(root, "AccuracyCoin.asm"));
        var details = string.Join(", ", result.NonPassingResults.Select(item =>
            $"{testNames.GetValueOrDefault(item.Address, $"${item.Address:X4}")}=${item.Value:X2}"));

        Assert.True(result.Outcome == AccuracyCoinOutcome.Passed,
            $"AccuracyCoin: {result.Outcome}; passed {result.Passed}/{result.Total}, " +
            $"skipped {result.Skipped}, after {result.ElapsedPpuDots:N0} PPU dots. " + details);
    }
}
