namespace Sheep.Nes.Lab.Tests;

public sealed class AssemblySourceIndexTests
{
    [Fact]
    public void Build_IndexesLabelsProceduresConstantsAndDefinesWithProvenance()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-source-{Guid.NewGuid():N}");
        var suiteRoot = Path.Combine(root, "suite", "source");
        Directory.CreateDirectory(suiteRoot);
        try
        {
            File.WriteAllText(Path.Combine(suiteRoot, "test.s"), """
                reset:
                .proc run_case
                result_code = $0A
                .define STATUS_ADDR $6000
                    lda #result_code
                set_test 3,"NOP timing is wrong"
                .endproc
                """);
            var entry = Entry("suite/rom_singles/case.nes");

            var index = AssemblySourceIndex.Build(entry, root);

            Assert.Single(index.Documents);
            Assert.Equal(64, index.Documents[0].Sha256.Length);
            Assert.Equal(4, index.Symbols.Count);
            var result = index.FindSymbol("RESULT_CODE");
            Assert.Equal(3, Assert.Single(result).LineNumber);
            Assert.Equal("$0A", result[0].Value);
            Assert.Equal(Path.GetFullPath(Path.Combine(suiteRoot, "test.s")), result[0].SourcePath);
            var encoding = Assert.Single(index.ResultEncodings);
            Assert.Equal(3, encoding.Code);
            Assert.Equal("NOP timing is wrong", encoding.Message);
            Assert.Equal(6, encoding.LineNumber);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SearchText_ReturnsBoundedCaseInsensitiveSourceExcerpts()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "suite"));
        try
        {
            File.WriteAllText(Path.Combine(root, "suite", "case.asm"),
                "first DMA boundary\nsecond dma boundary\nthird DMA boundary\n");
            var index = AssemblySourceIndex.Build(Entry("suite/case.nes"), root);

            var matches = index.SearchText("dma boundary", maximumResults: 2);

            Assert.Equal(2, matches.Count);
            Assert.Equal([1, 2], matches.Select(match => match.LineNumber));
            Assert.All(matches, match => Assert.Equal(64, match.SourceSha256.Length));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Build_UsesFirstManifestPathSegmentAsSuiteRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "renamed-upstream", "source"));
        try
        {
            File.WriteAllText(Path.Combine(root, "renamed-upstream", "source", "case.s"), "case_label:\n");

            var index = AssemblySourceIndex.Build(
                Entry("renamed-upstream/rom_singles/case.nes"), root);

            Assert.Equal("case_label", Assert.Single(index.Symbols).Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static RomCatalogEntry Entry(string path) => new(
        "suite", "case", path, "hash", null, 1, "Ntsc", null, null,
        RomAvailability.NotChecked, null, [],
        new RomCatalogProvenance("manifest", "hash", "commit"));
}
