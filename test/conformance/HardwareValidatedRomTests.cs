using Xunit;

namespace SR.Emulation.Nes.ConformanceTests;

public sealed class HardwareValidatedRomTests
{
    public static TheoryData<string, string, string, string, long> Cases
    {
        get
        {
            var manifest = TestRomManifest.Load(Path.Combine(AppContext.BaseDirectory, "test-roms.json"));
            var suiteFilter = Environment.GetEnvironmentVariable("NES_CONFORMANCE_SUITE");
            var cases = new TheoryData<string, string, string, string, long>();
            foreach (var test in manifest.Tests)
            {
                if (!string.IsNullOrWhiteSpace(suiteFilter) &&
                    !test.Suite.Equals(suiteFilter, StringComparison.OrdinalIgnoreCase)) continue;
                cases.Add(test.Suite, test.Name, test.Path, test.Sha256, test.MaximumPpuDots);
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Rom_PassesOnTheEmulatedMachine(
        string suite, string name, string relativePath, string sha256, long maximumPpuDots)
    {
        if (suite == "sprdma_and_dmc_dma")
            Assert.Skip("Known gap: end-of-OAM DMC overlap needs CPU per-cycle write-phase visibility.");
        if (suite == "dmc_dma_during_read4")
            Assert.Skip("Supplemental observation ROM: validates printed CRC/output variants and has no machine terminal result.");

        var assetRoot = TestRomAssets.FindRoot();
        if (assetRoot is null)
            Assert.Skip("Conformance ROMs are not installed. Run test/conformance/Install-TestRoms.ps1 first.");

        var definition = new TestRomDefinition(suite, name, relativePath, sha256, maximumPpuDots);
        var romPath = Path.Combine(assetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(romPath), $"Missing test ROM: {romPath}");
        TestRomManifest.VerifyChecksum(definition, romPath);

        var nes = new Nes(NesVideoStandard.Ntsc);
        nes.LoadRom(File.ReadAllBytes(romPath));
        var legacyResultAddress = suite == "blargg_apu_2005.07.30" ? (ushort?)0x00F0 : null;
        ushort? successProgramCounter = suite == "dmc_tests" ? name switch
        {
            "buffer_retained" => 0xE149,
            "latency" => 0xE162,
            "status_irq" => 0xE154,
            "status" => 0xE14E,
            _ => null
        } : null;
        var result = new NesTestRomRunner(new NesTestMachine(nes), chunkSize: 25_000,
            legacyResultAddress: legacyResultAddress,
            legacyMinimumPpuDots: legacyResultAddress.HasValue ? 3_000_000 : 0,
            successProgramCounter: successProgramCounter).Run(maximumPpuDots);
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
