using System.Text.Json;

namespace Sheep.Nes.Lab.Tests;

public sealed class RomCatalogTests
{
    [Fact]
    public void Load_IndexesManifestFieldsProtocolsAndProvenance()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-roms-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var manifestPath = Path.Combine(root, "test-roms.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
            {
                upstreamCommit = "abc123",
                defaultProtocols = new[] { new { kind = "Blargg6000" } },
                protocolRules = new[]
                {
                    new
                    {
                        suite = "blargg_apu_2005.07.30",
                        name = (string?)null,
                        protocols = new[]
                        {
                            new { kind = "LegacyResultAddress", address = (ushort?)0x00F0,
                                minimumPpuDots = 3_000_000L }
                        }
                    }
                },
                tests = new[]
                {
                    new
                    {
                        suite = "blargg_apu_2005.07.30", name = "01.len_ctr",
                        path = "apu/01.nes", sha256 = "abcd", maximumPpuDots = 30L,
                        videoStandard = "Ntsc", skipReason = "optional hardware",
                        knownGap = "analog behavior"
                    }
                }
            }));

            var catalog = RomCatalog.Load(manifestPath);

            Assert.Equal("abc123", catalog.UpstreamCommit);
            Assert.Equal(64, catalog.ManifestSha256.Length);
            var item = Assert.Single(catalog.Entries);
            Assert.Equal("01.len_ctr", item.Name);
            Assert.Equal("optional hardware", item.SkipReason);
            Assert.Equal("analog behavior", item.KnownGap);
            Assert.Equal(RomAvailability.NotChecked, item.Availability);
            Assert.Equal(RomProtocolKind.LegacyResultAddress, item.Protocols[0].Kind);
            Assert.Equal((ushort)0x00F0, item.Protocols[0].Address);
            Assert.Equal(RomProtocolKind.Blargg6000, item.Protocols[^1].Kind);
            Assert.Equal(Path.GetFullPath(manifestPath), item.Provenance.ManifestPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_WithAssetRootVerifiesInstalledRomChecksum()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-roms-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        try
        {
            var romPath = Path.Combine(root, "assets", "case.nes");
            File.WriteAllBytes(romPath, [1, 2, 3]);
            var digest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData([1, 2, 3]));
            var manifestPath = Path.Combine(root, "manifest.json");
            File.WriteAllText(manifestPath, $$"""
                {"upstreamCommit":"commit","tests":[{"suite":"suite","name":"case","path":"case.nes","sha256":"{{digest}}","maximumPpuDots":10}]}
                """);

            var item = Assert.Single(RomCatalog.Load(manifestPath, Path.Combine(root, "assets")).Entries);

            Assert.Equal(RomAvailability.InstalledVerified, item.Availability);
            Assert.Equal(digest, item.ActualSha256);
            Assert.Equal(Path.GetFullPath(romPath), item.RomPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Find_IsCaseInsensitiveAndRejectsAmbiguousNames()
    {
        var catalog = new RomCatalog("commit", "hash", "manifest", [
            Entry("suite-a", "same"), Entry("suite-b", "same"), Entry("suite-a", "unique")]);

        Assert.Equal("unique", catalog.Find("SUITE-A", "UNIQUE").Name);
        Assert.Throws<InvalidOperationException>(() => catalog.Find(null, "same"));
    }

    [Fact]
    public void Load_UsesOnlyManifestProtocolRules()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-protocols-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var manifest = Path.Combine(root, "test-roms.json");
            File.WriteAllText(manifest, """
                {
                  "upstreamCommit":"commit",
                  "defaultProtocols":[{"kind":"Blargg6000"}],
                  "protocolRules":[
                    {"suite":"dmc_tests","name":"latency","protocols":[{"kind":"SuccessProgramCounter","address":57698}]},
                    {"suite":"dmc_tests","protocols":[{"kind":"TextConsole","successMarkers":["OK"]}]}
                  ],
                  "tests":[{"suite":"dmc_tests","name":"latency","path":"case.nes","sha256":"hash","maximumPpuDots":10}]
                }
                """);

            var protocols = Assert.Single(RomCatalog.Load(manifest).Entries).Protocols;

            Assert.Equal([RomProtocolKind.SuccessProgramCounter, RomProtocolKind.TextConsole,
                RomProtocolKind.Blargg6000], protocols.Select(item => item.Kind));
            Assert.Equal((ushort)0xE162, protocols[0].Address);
            Assert.Equal(["OK"], protocols[1].SuccessMarkers);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static RomCatalogEntry Entry(string suite, string name) => new(
        suite, name, "path", "hash", null, 1, "Ntsc", null, null,
        RomAvailability.NotChecked, null, [new RomProtocolDescriptor(RomProtocolKind.Blargg6000)],
        new RomCatalogProvenance("manifest", "hash", "commit"));
}
