namespace Sheep.Nes.Lab.Tests;

public sealed class GuidanceEvidenceCollectorTests
{
    [Fact]
    public async Task CollectAsync_IncludesRootAndNearestNestedGuidanceOnce()
    {
        var root = Directory.CreateTempSubdirectory("nes-lab-guidance-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, "src", "cpu"));
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "AGENTS.md"), "root rule",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "src", "AGENTS.md"), "source rule",
                TestContext.Current.CancellationToken);
            var result = await GuidanceEvidenceCollector.CollectAsync(root.FullName,
                ["src/cpu/Cpu.cs", "src/cpu/Other.cs"], TestContext.Current.CancellationToken);
            Assert.Equal(2, result.Count);
            Assert.All(result, evidence => Assert.Equal(ContextEvidenceKind.Guidance, evidence.Kind));
            Assert.All(result, evidence => Assert.Equal(64, evidence.SourceHash!.Length));
        }
        finally { Directory.Delete(root.FullName, true); }
    }
}
