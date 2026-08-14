namespace Sheep.Nes.Lab.Tests;

public sealed class RetrievalProfileClassificationTests
{
    [Fact]
    public void Classify_PrefersExplicitLabLanguageAndNamedPaths()
    {
        var root = FindRepositoryRoot();
        var profiles = RetrievalProfiles.Load(root);

        var result = RetrievalProfiles.Classify(profiles,
            "Audit NES Lab retrieval in src/tools/nes-lab/ContextPacketBuilder.cs");

        Assert.Equal("lab", result.PrimarySubsystem);
        Assert.True(result.Confident);
        Assert.Contains(result.Reasons, reason => reason.StartsWith("explicit-path:", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nes-emulator-3.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
