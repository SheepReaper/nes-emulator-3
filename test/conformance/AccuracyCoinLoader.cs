using System.Security.Cryptography;

using Xunit;

namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>
/// ROM loader and validation helper for AccuracyCoin conformance tests.
/// </summary>
internal static class AccuracyCoinLoader
{
    internal const string RomSha256 = "448df0e3e6aed4d36972d79d63715c0fccbe89bd435ef3a2a97fbfb70184cc96";

    internal static NesSystem Load()
    {
        var root = AccuracyCoinAssets.FindRoot();
        if (root is null)
        {
            Assert.Skip("AccuracyCoin is not installed. Run test/conformance/Install-TestRoms.ps1 first.");
        }

        var romPath = Path.Combine(root, "AccuracyCoin.nes");
        Assert.True(File.Exists(romPath), $"Missing AccuracyCoin ROM: {romPath}");
        var rom = File.ReadAllBytes(romPath);
        Assert.Equal(RomSha256, Convert.ToHexStringLower(SHA256.HashData(rom)));

        var nes = new NesSystem(NesHardwareProfile.Rp2A03G_Rp2C02G);
        nes.LoadRom(rom);
        return nes;
    }
}
