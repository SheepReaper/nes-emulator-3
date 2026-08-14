namespace Sheep.Nes.Lab.Tests;

public sealed class RomDiagnosisBuilderTests
{
    [Fact]
    public void Diagnose_ResolvesCodeToExactSourceEvidence()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-diagnose-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "suite", "source"));
        try
        {
            File.WriteAllText(Path.Combine(root, "suite", "source", "case.s"),
                "set_test 3,\"NOP timing is wrong\"\n");
            var entry = new RomCatalogEntry(
                "suite", "case", "suite/case.nes", "romhash", null, 100, "Ntsc",
                null, null, RomAvailability.NotChecked, null,
                [new RomProtocolDescriptor(RomProtocolKind.Blargg6000)],
                new RomCatalogProvenance("manifest.json", "manifesthash", "commit"));
            var catalog = new RomCatalog("commit", "manifesthash", "manifest.json", [entry]);

            var diagnosis = RomDiagnosisBuilder.Diagnose(catalog, root, "suite", "case", 3);

            var meaning = Assert.Single(diagnosis.Meanings);
            Assert.Equal("NOP timing is wrong", meaning.Message);
            Assert.Equal(1, meaning.LineNumber);
            Assert.Equal("manifesthash", diagnosis.ManifestProvenance.ManifestSha256);
            Assert.Equal("romhash", diagnosis.ExpectedRomSha256);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
