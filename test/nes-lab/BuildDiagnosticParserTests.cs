namespace Sheep.Nes.Lab.Tests;

public sealed class BuildDiagnosticParserTests
{
    [Fact]
    public void Parse_ProducesStructuredDiagnosticsAndGroupsCascades()
    {
        const string output = """
src/App.cs(12,5): error CS0246: The type 'Missing' could not be found [src/App.csproj]
src/App.cs(12,5): error CS0246: The type 'Missing' could not be found [src/App.csproj]
src/App.csproj : error MSB4019: imported project was not found
""";

        var result = BuildDiagnosticParser.Parse(output);

        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal("CS0246", result.EarliestActionable!.Code);
        Assert.Equal(2, result.Diagnostics[0].Occurrences);
        Assert.Equal(12, result.Diagnostics[0].Line);
    }

    [Fact]
    public void Parse_RecognizesLockedPublishedAssembly()
    {
        const string output = "error MSB3021: Unable to copy file. The process cannot access the file because it is being used by another process.";

        var diagnostic = Assert.Single(BuildDiagnosticParser.Parse(output).Diagnostics);

        Assert.Equal(BuildDiagnosticCategory.LockedFile, diagnostic.Category);
    }
}
