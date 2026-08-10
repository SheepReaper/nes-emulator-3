using Xunit;

namespace SR.Emulation.Nes.ConformanceTests;

public sealed class CpuTimingRomTests
{
    private const string RelativePath = "cpu_timing_test6/cpu_timing_test.nes";
    private const string Sha256 = "6ab4fe8af23b12ca0dfccfc030de3d4069bf2498e3ef20ddcf1ca75555065b85";
    private const long MaximumPpuDots = 100_000_000;

    [Fact]
    public void OfficialInstructionCycleCountsMatchHardwareValidatedRom()
    {
        var assetRoot = TestRomAssets.FindRoot();
        if (assetRoot is null)
            Assert.Skip("Conformance ROMs are not installed. Run test/conformance/Install-TestRoms.ps1 first.");

        var definition = new TestRomDefinition("cpu_timing_test6", "official", RelativePath, Sha256, MaximumPpuDots);
        var romPath = Path.Combine(assetRoot, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(romPath), $"Missing test ROM: {romPath}");
        TestRomManifest.VerifyChecksum(definition, romPath);

        var nes = new Nes(NesVideoStandard.Ntsc);
        nes.LoadRom(File.ReadAllBytes(romPath));

        var terminal = LegacyRomTerminal.WaitForSelfJump(nes, MaximumPpuDots);
        var measured = nes.Debugger.PeekCpuMemory(0x0002);
        var expected = nes.Debugger.PeekCpuMemory(0x0003);
        var testedOpcode = nes.Debugger.PeekCpuMemory(0x0013);
        var mode = nes.Debugger.PeekCpuMemory(0x0014);
        Assert.True(
            mode == 2 && testedOpcode == 0xFE,
            $"cpu_timing_test6 failed at opcode ${testedOpcode:X2} in " +
            $"{(mode == 2 ? "normal" : "page-crossing")} mode: measured {measured}, expected {expected}; " +
            $"terminal PC ${terminal.ProgramCounter:X4} after {terminal.ElapsedPpuDots:N0} PPU dots.");
    }
}
