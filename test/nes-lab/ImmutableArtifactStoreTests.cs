namespace Sheep.Nes.Lab.Tests;

public sealed class ImmutableArtifactStoreTests
{
    [Fact]
    public async Task PublishAndRead_UsesContentAddressedUriAndVerifiesBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-artifacts-{Guid.NewGuid():N}");
        try
        {
            var store = new ImmutableArtifactStore(root);
            var metadata = await store.PublishTextAsync("context", "{\"answer\":42}",
                "application/json", cancellationToken: TestContext.Current.CancellationToken);

            var resource = await store.ReadAsync("context", metadata.Digest,
                TestContext.Current.CancellationToken);

            Assert.Equal($"nes-lab://artifact/context/sha256/{metadata.Digest}", resource.Uri);
            Assert.Equal("{\"answer\":42}", resource.Text);
            Assert.Equal("available", resource.Status);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Prune_PreservesPinnedContentAndReturnsGoneTombstone()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-artifacts-{Guid.NewGuid():N}");
        try
        {
            var store = new ImmutableArtifactStore(root);
            var old = await store.PublishTextAsync("log", "old", "text/plain",
                cancellationToken: TestContext.Current.CancellationToken);
            var pinned = await store.PublishTextAsync("log", "pinned", "text/plain", true,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(1, await store.PruneAsync(DateTimeOffset.UtcNow.AddMinutes(1),
                TestContext.Current.CancellationToken));
            Assert.Equal("gone", (await store.ReadAsync("log", old.Digest,
                TestContext.Current.CancellationToken)).Status);
            Assert.Equal("available", (await store.ReadAsync("log", pinned.Digest,
                TestContext.Current.CancellationToken)).Status);
        }
        finally { Directory.Delete(root, true); }
    }
}
