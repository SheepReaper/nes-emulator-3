namespace Sheep.Nes.Lab.Tests;

public sealed class HostDiagnosticsTests
{
    [Fact]
    public async Task ImportAndCompare_PublishesImmutableBoundedBundles()
    {
        var root = Path.Combine(Path.GetTempPath(), "nes-lab-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var service = new HostDiagnosticsService(root);
            var left = await service.ImportAsync("""{"schemaVersion":1,"applicationVersion":"1","emulatorVersion":"1","romSha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","mapper":4,"audio":{"graphStatus":"running","underruns":1},"video":{"presentedFrames":10}}""", TestContext.Current.CancellationToken);
            var right = await service.ImportAsync("""{"schemaVersion":1,"applicationVersion":"1","emulatorVersion":"1","romSha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","mapper":4,"audio":{"graphStatus":"running","underruns":3},"video":{"presentedFrames":11}}""", TestContext.Current.CancellationToken);

            var comparison = await service.CompareAsync(left.ResourceUri!, right.ResourceUri!, TestContext.Current.CancellationToken);

            Assert.Equal(2, comparison.AudioUnderrunDelta);
            Assert.Contains("audio.underruns", comparison.ChangedFields);
            Assert.StartsWith("nes-lab://artifact/host-diagnostics/sha256/", left.ResourceUri);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Import_RejectsInvalidRomDigest()
    {
        var root = Path.Combine(Path.GetTempPath(), "nes-lab-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => new HostDiagnosticsService(root)
                .ImportAsync("""{"schemaVersion":1,"romSha256":"bad"}""", TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
