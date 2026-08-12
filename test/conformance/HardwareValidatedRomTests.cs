using Xunit;
using Sheep.Nes.Lab;

namespace Sheep.Emulation.Nes.ConformanceTests;

public sealed class HardwareValidatedRomTests
{
    public static TheoryData<string, string, string, string, long, string> Cases
    {
        get
        {
            var manifest = TestRomManifest.Load(Path.Combine(AppContext.BaseDirectory, "test-roms.json"));
            var suiteFilter = Environment.GetEnvironmentVariable("NES_CONFORMANCE_SUITE");
            var cases = new TheoryData<string, string, string, string, long, string>();
            foreach (var test in manifest.Tests)
            {
                if (!string.IsNullOrWhiteSpace(suiteFilter) &&
                    !test.Suite.Equals(suiteFilter, StringComparison.OrdinalIgnoreCase) &&
                    !test.Name.Contains(suiteFilter, StringComparison.OrdinalIgnoreCase)) continue;
                cases.Add(test.Suite, test.Name, test.Path, test.Sha256, test.MaximumPpuDots, test.VideoStandard);
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Rom_PassesOnTheEmulatedMachine(
        string suite, string name, string relativePath, string sha256, long maximumPpuDots, string videoStandard)
    {
        var assetRoot = TestRomAssets.FindRoot();
        if (assetRoot is null)
            Assert.Skip("Conformance ROMs are not installed. Run test/conformance/Install-TestRoms.ps1 first.");

        var definition = TestRomManifest.Load(Path.Combine(AppContext.BaseDirectory, "test-roms.json")).Tests
            .Single(test => test.Suite == suite && test.Name == name);
        var romPath = Path.Combine(assetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(romPath), $"Missing test ROM: {romPath}");
        TestRomManifest.VerifyChecksum(definition, romPath);

        Assert.True(Enum.TryParse<NesVideoStandard>(definition.VideoStandard, true, out var standard),
            $"Unsupported video standard '{definition.VideoStandard}' in the test ROM manifest.");
        var nes = new NesSystem(standard);
        nes.LoadRom(File.ReadAllBytes(romPath));
        var traceCapture = ConformanceTraceCapture.FromEnvironment(
            nes, Path.GetFileName(romPath), definition.Sha256, definition.VideoStandard,
            definition.Suite, definition.Name);
        traceCapture?.Start();
        traceCapture?.MarkCheckpoint("test-entry", "entry", "manifest runner");
        var legacy = definition.Protocols.FirstOrDefault(item => item.Kind == RomProtocolKind.LegacyResultAddress);
        var successPc = definition.Protocols.FirstOrDefault(item => item.Kind == RomProtocolKind.SuccessProgramCounter);
        var textProtocols = definition.Protocols.Where(item => item.Kind == RomProtocolKind.TextConsole).ToArray();
        var textConsoleSuccessMarkers = textProtocols.SelectMany(item => item.SuccessMarkers ?? []).Distinct().ToArray();
        var result = new NesTestRomRunner(new NesTestMachine(nes), chunkSize: 25_000,
            legacyResultAddress: legacy?.Address,
            legacyMinimumPpuDots: legacy?.MinimumPpuDots ?? 0,
            successProgramCounter: successPc?.Address,
            detectTextConsoleResult: textProtocols.Length > 0,
            textConsoleSuccessMarkers: textConsoleSuccessMarkers.Length == 0 ? null : textConsoleSuccessMarkers).Run(definition.MaximumPpuDots);
        traceCapture?.MarkCheckpoint(
            result.Outcome == NesTestOutcome.Passed ? "terminal-state" : "first-unexpected-status",
            result.Outcome == NesTestOutcome.Passed ? "terminal" : "assertion",
            "ROM terminal protocol", result.ResetCount);
        traceCapture?.Complete(result.Outcome == NesTestOutcome.Passed, result.ResetCount);
        Assert.True(
            result.Outcome == NesTestOutcome.Passed,
            $"{definition.Suite}/{definition.Name}: {result.Outcome}, code {result.Code?.ToString() ?? "n/a"}, " +
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
