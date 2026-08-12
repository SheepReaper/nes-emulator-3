using System.Security.Cryptography;

using Xunit;

namespace Sheep.Emulation.Nes.ConformanceTests;

public sealed class MapperConformanceRomTests
{
    [Fact]
    public void InstalledMapperRoms_AreAcceptedByTheCartridgeFactory()
    {
        var root = TestRomAssets.FindRoot();
        if (root is null)
        {
            Assert.Skip("Conformance ROMs are not installed. Run test/conformance/Install-TestRoms.ps1 first.");
        }
        CartridgeFactoryConformanceHelper.VerifyInstalledRoms(root);
    }

    [Theory]
    [MemberData(nameof(HolyMapperelTestData.Cases), MemberType = typeof(HolyMapperelTestData))]
    public void HolyMapperel_ReportsWorkingPrgAndChrBanking(string fileName, string sha256)
    {
        var root = HolyMapperelTestHelper.FindHolyMapperelRoot();
        if (root is null)
        {
            Assert.Skip("Holy Mapperel ROMs are not installed. Run test/conformance/Install-TestRoms.ps1 first.");
        }
        var path = Path.Combine(root, fileName);
        Assert.True(File.Exists(path), $"Missing test ROM: {path}");
        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());

        var nes = new NesSystem(NesVideoStandard.Ntsc);
        nes.LoadRom(File.ReadAllBytes(path));
        var screen = "";
        for (var elapsed = 0; elapsed < 60_000_000; elapsed += 100_000)
        {
            nes.RunForPpuDots(100_000);
            screen = HolyMapperelTestHelper.ReadSmallFontScreen(nes);
            if (screen.Contains("DETAILED TEST RESULT: 0000", StringComparison.Ordinal))
            {
                break;
            }
        }

        var pc = nes.Debugger.ProgramCounter;
        Assert.True(screen.Contains("DETAILED TEST RESULT: 0000", StringComparison.Ordinal), $"PC=${pc:X4}\n{screen}");
        Assert.DoesNotContain("PROBLEM", screen);
    }

    [Fact]
    public void Vrc2aRom_ExercisesMultipleChrBanks()
    {
        var root = TestRomAssets.FindRoot();
        if (root is null)
        {
            Assert.Skip("Conformance ROMs are not installed. Run test/conformance/Install-TestRoms.ps1 first.");
        }
        var path = Path.Combine(root, "m22chrbankingtest", "0-127.nes");
        const string expectedHash = "4d380f263bc5c7571e8607811e3947c3939d5f58fc80aee6bc1bc49bf29ce05c";
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());

        var nes = new NesSystem(NesVideoStandard.Ntsc);
        nes.LoadRom(File.ReadAllBytes(path));
        var observed = new HashSet<byte>();
        for (var frame = 0; frame < 160; frame++)
        {
            nes.RunForPpuDots(341 * 262);
            observed.Add(nes.Debugger.PeekPpuMemory(0x05A2));
        }

        Assert.True(observed.Count >= 6, $"Expected at least six CHR banks, observed {observed.Count} distinct values.");
    }

    [Fact]
    public void Mmc5Rom_ReportsTheSelectedOneKilobyteChrBanks()
    {
        var root = TestRomAssets.FindRoot();
        if (root is null)
        {
            Assert.Skip("Conformance ROMs are not installed. Run test/conformance/Install-TestRoms.ps1 first.");
        }
        var path = Path.Combine(root, "mmc5test_v2", "mmc5test.nes");
        const string expectedHash = "f18f60a27cae9c00b51782caa3b77cf96a11e1c45e4323a9815474728e5b2980";
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());

        var nes = new NesSystem(NesVideoStandard.Ntsc);
        nes.LoadRom(File.ReadAllBytes(path));
        nes.RunForPpuDots(341 * 262 * 12);

        byte[] expected = [8, 9, 10, 11, 8, 9, 10, 11];
        var actual = new byte[expected.Length];
        for (var index = 0; index < actual.Length; index++)
        {
            actual[index] = nes.Debugger.PeekPpuMemory((ushort)(0x224F + index));
        }
        Assert.Equal(expected, actual);
    }
}
