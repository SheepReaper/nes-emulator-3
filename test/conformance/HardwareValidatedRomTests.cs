using Xunit;

namespace SR.Emulation.Nes.ConformanceTests;

public sealed class HardwareValidatedRomTests
{
    public static TheoryData<string, string, string, string, long> Cases
    {
        get
        {
            var manifest = TestRomManifest.Load(Path.Combine(AppContext.BaseDirectory, "test-roms.json"));
            var cases = new TheoryData<string, string, string, string, long>();
            foreach (var test in manifest.Tests)
                cases.Add(test.Suite, test.Name, test.Path, test.Sha256, test.MaximumPpuDots);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Rom_PassesOnTheEmulatedMachine(
        string suite, string name, string relativePath, string sha256, long maximumPpuDots)
    {
        var assetRoot = TestRomAssets.FindRoot();
        if (assetRoot is null)
            Assert.Skip("Conformance ROMs are not installed. Run test/conformance/Install-TestRoms.ps1 first.");

        var definition = new TestRomDefinition(suite, name, relativePath, sha256, maximumPpuDots);
        var romPath = Path.Combine(assetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(romPath), $"Missing test ROM: {romPath}");
        TestRomManifest.VerifyChecksum(definition, romPath);

        var nes = new Nes(NesVideoStandard.Ntsc);
        nes.LoadRom(File.ReadAllBytes(romPath));
        var result = new NesTestRomRunner(new NesTestMachine(nes), chunkSize: 25_000).Run(maximumPpuDots);

        Assert.True(
            result.Outcome == NesTestOutcome.Passed,
            $"{suite}/{name}: {result.Outcome}, code {result.Code?.ToString() ?? "n/a"}, " +
            $"after {result.ElapsedPpuDots:N0} PPU dots and {result.ResetCount} resets.\n{result.Output}");
    }
}

internal static class TestRomAssets
{
    internal static string? FindRoot()
    {
        var configured = Environment.GetEnvironmentVariable("NES_TEST_ROMS");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return Path.GetFullPath(configured);

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "test-roms", "nes-test-roms");
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }
}
