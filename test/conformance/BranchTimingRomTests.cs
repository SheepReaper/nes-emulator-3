using Xunit;

namespace Sheep.Emulation.Nes.ConformanceTests;

public sealed class BranchTimingRomTests
{
    private const long MaximumPpuDots = 25_000_000;

    public static TheoryData<string, string> Cases => new()
    {
        { "1.Branch_Basics.nes", "7b69e3044eaeb86317147a900d1f4a467b666f59d375ec1ba6658233f23786cd" },
        { "2.Backward_Branch.nes", "f7966e9b86b04b4adb987439a442e926e9cfe6bb71436dd5dd56f41f9eb029a4" },
        { "3.Forward_Branch.nes", "d0fbc6b1899bc948172c45f37a981eb0dade212d7e807cc56efedae93d6f9c9b" }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void BranchTimingMatchesHardwareValidatedRom(string fileName, string sha256)
    {
        var assetRoot = TestRomAssets.FindRoot();
        if (assetRoot is null)
            Assert.Skip("Conformance ROMs are not installed. Run test/conformance/Install-TestRoms.ps1 first.");

        var relativePath = $"branch_timing_tests/{fileName}";
        var definition = new TestRomDefinition("branch_timing_tests", fileName, relativePath, sha256, MaximumPpuDots);
        var romPath = Path.Combine(assetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(romPath), $"Missing test ROM: {romPath}");
        TestRomManifest.VerifyChecksum(definition, romPath);

        var nes = new NesSystem(NesVideoStandard.Ntsc);
        nes.LoadRom(File.ReadAllBytes(romPath));
        var terminal = LegacyRomTerminal.WaitForSelfJump(nes, MaximumPpuDots);
        var result = nes.Debugger.PeekCpuMemory(0x00F8);

        Assert.True(
            result == 1,
            $"branch_timing_tests/{fileName} failed with result code {result} at terminal PC " +
            $"${terminal.ProgramCounter:X4} after {terminal.ElapsedPpuDots:N0} PPU dots. " +
            "See the pinned upstream readme for the code-specific timing boundary.");
    }
}

internal readonly record struct LegacyRomTerminal(ushort ProgramCounter, long ElapsedPpuDots)
{
    internal static LegacyRomTerminal WaitForSelfJump(NesSystem nes, long maximumPpuDots, int chunkSize = 25_000)
    {
        ushort? previousPc = null;
        var stableSamples = 0;
        long elapsed = 0;
        while (elapsed < maximumPpuDots)
        {
            var dots = (int)Math.Min(chunkSize, maximumPpuDots - elapsed);
            nes.RunForPpuDots(dots);
            elapsed += dots;
            var pc = nes.Debugger.ProgramCounter;
            stableSamples = pc == previousPc ? stableSamples + 1 : 0;
            previousPc = pc;
            if (stableSamples < 20) continue;

            var opcode = nes.Debugger.PeekCpuMemory(pc);
            var target = (ushort)(nes.Debugger.PeekCpuMemory((ushort)(pc + 1)) |
                                  nes.Debugger.PeekCpuMemory((ushort)(pc + 2)) << 8);
            if (opcode == 0x4C && target == pc)
                return new LegacyRomTerminal(pc, elapsed);
        }

        throw new TimeoutException($"Legacy test ROM did not terminate after {maximumPpuDots:N0} PPU dots.");
    }
}
