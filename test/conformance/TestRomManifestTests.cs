using Xunit;
using Sheep.Nes.Lab;

namespace Sheep.Emulation.Nes.ConformanceTests;

public sealed class TestRomManifestTests
{
    [Fact]
    public void Load_ReadsPinnedDefinitionsAndVerifiesTheirChecksums()
    {
        var manifest = TestRomManifest.Load(Path.Combine(AppContext.BaseDirectory, "test-roms.json"));

        Assert.Equal("95d8f621ae55cee0d09b91519a8989ae0e64753b", manifest.UpstreamCommit);
        Assert.Equal(95, manifest.Tests.Length);
        Assert.All(manifest.Tests, test =>
        {
            Assert.NotEmpty(test.Suite);
            Assert.NotEmpty(test.Name);
            Assert.Equal(64, test.Sha256.Length);
            Assert.True(test.MaximumPpuDots > 0);
            Assert.NotEmpty(test.Protocols);
            Assert.True(Enum.TryParse<NesVideoStandard>(test.VideoStandard, true, out _));
        });
    }

    [Fact]
    public void VerifyChecksum_RejectsModifiedAssets()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [1, 2, 3]);
            var definition = new TestRomDefinition("suite", "rom", "rom.nes", new string('0', 64), 1,
                "Ntsc", [new(RomProtocolKind.Blargg6000)]);

            Assert.Throws<InvalidDataException>(() => TestRomManifest.VerifyChecksum(definition, path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
