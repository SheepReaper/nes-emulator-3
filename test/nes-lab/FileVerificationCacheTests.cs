using Sheep.Nes.Lab;

namespace Sheep.Nes.Lab.Tests;

public sealed class FileVerificationCacheTests
{
    [Fact]
    public async Task StoreAndRetrieveAsync_SuccessfulResult_ReturnsCachedCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-cache-{Guid.NewGuid():N}");
        try
        {
            var source = Path.Combine(root, "src", "tools", "nes-lab", "Program.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            await File.WriteAllTextAsync(source, "source", TestContext.Current.CancellationToken);
            var runLog = Path.Combine(root, "run.log");
            await File.WriteAllTextAsync(runLog, "complete output", TestContext.Current.CancellationToken);
            var command = new VerificationCommand(VerificationScope.LabTests, "dotnet", ["test"]);
            var result = CreatePassedResult(runLog);
            var cache = new FileVerificationCache(root, Path.Combine(root, ".artifacts", "cache"));

            await cache.StoreAsync(command, result, TestContext.Current.CancellationToken);
            var cached = await cache.TryGetAsync(command, TestContext.Current.CancellationToken);

            Assert.NotNull(cached);
            Assert.True(cached.Cached);
            Assert.True(File.Exists(cached.ArtifactPath));
            Assert.Equal("complete output", await File.ReadAllTextAsync(
                cached.ArtifactPath,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TryGetAsync_SourceChanged_ReturnsCacheMiss()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-cache-{Guid.NewGuid():N}");
        try
        {
            var source = Path.Combine(root, "src", "tools", "nes-lab", "Program.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            await File.WriteAllTextAsync(source, "one", TestContext.Current.CancellationToken);
            var runLog = Path.Combine(root, "run.log");
            await File.WriteAllTextAsync(runLog, "output", TestContext.Current.CancellationToken);
            var command = new VerificationCommand(VerificationScope.LabTests, "dotnet", ["test"]);
            var cache = new FileVerificationCache(root, Path.Combine(root, ".artifacts", "cache"));
            await cache.StoreAsync(command, CreatePassedResult(runLog), TestContext.Current.CancellationToken);

            await File.WriteAllTextAsync(source, "two", TestContext.Current.CancellationToken);
            var cached = await cache.TryGetAsync(command, TestContext.Current.CancellationToken);

            Assert.Null(cached);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static VerificationResult CreatePassedResult(string artifactPath) => new(
        VerificationScope.LabTests,
        true,
        VerificationOutcome.Passed,
        0,
        10,
        new VerificationSummary(1, 0, 1, 0),
        [],
        artifactPath,
        15);
}
