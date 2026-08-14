namespace Sheep.Nes.Lab.Tests;

public sealed class RepositoryTextIndexTests
{
    [Fact]
    public void BuildAndSearch_ReturnsFocusedSourceAndInvalidatesChangedContent()
    {
        var root = Directory.CreateTempSubdirectory("nes-lab-fts-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, "src"));
            var source = Path.Combine(root.FullName, "src", "Dma.cs");
            File.WriteAllText(source, "sealed class Dma { void RetryHaltOnReadCycle() {} }");
            var database = Path.Combine(root.FullName, "index.sqlite");

            var first = RepositoryTextIndex.Build(root.FullName, database);
            Assert.Single(RepositoryTextIndex.Search(database, "halt read cycle", 5));
            File.WriteAllText(source, "sealed class Dma { void Nothing() {} }");
            var second = RepositoryTextIndex.Build(root.FullName, database);

            Assert.NotEqual(first.SourceFingerprint, second.SourceFingerprint);
            Assert.Empty(RepositoryTextIndex.Search(database, "halt read cycle", 5));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root.FullName, true);
        }
    }

    [Fact]
    public void Search_MissingDatabaseIsReadOnlyAndReturnsEmpty()
    {
        var root = Directory.CreateTempSubdirectory("nes-lab-fts-missing-");
        try
        {
            var path = Path.Combine(root.FullName, "missing.sqlite");
            Assert.Empty(RepositoryTextIndex.Search(path, "dma", 5));
            Assert.False(File.Exists(path));
        }
        finally { Directory.Delete(root.FullName, true); }
    }
}
