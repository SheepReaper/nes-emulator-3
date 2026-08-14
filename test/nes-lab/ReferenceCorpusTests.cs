using System.Net;
using System.Net.Http;

namespace Sheep.Nes.Lab.Tests;

public sealed class ReferenceCorpusTests
{
    [Fact]
    public async Task SyncSearchAndShow_VerifyDigestAndRemainOfflineAfterSync()
    {
        var root = Directory.CreateTempSubdirectory("nes-lab-reference-");
        try
        {
            var content = "DMA halt retry occurs on a CPU read cycle.";
            var digest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(content)));
            var manifest = Path.Combine(root.FullName, "references.json");
            await File.WriteAllTextAsync(manifest, $$"""
{"version":1,"entries":[{"id":"dma","title":"DMA","canonicalUrl":"https://example.test/dma","fetchUrl":"https://example.test/dma","format":"raw","authority":"test","topics":["dma"],"aliases":["halt"],"expectedSha256":"{{digest}}","upstreamRevision":"1","licenseStatus":"cache-only","claims":[{"summary":"DMA retries halt on reads.","section":"Halt"}]}]}
""", TestContext.Current.CancellationToken);
            using var http = new HttpClient(new StubHandler(content));
            var store = new ReferenceCorpusStore(manifest, Path.Combine(root.FullName, "cache"));

            var sync = await store.SyncAsync(http, TestContext.Current.CancellationToken);
            Assert.Equal(ReferenceAvailability.Available, Assert.Single(sync).Availability);
            Assert.Single(store.Search("halt", 5));
            Assert.Equal(content, store.Show("dma").Content);
        }
        finally { Directory.Delete(root.FullName, true); }
    }

    [Fact]
    public async Task Sync_RejectsChangedUpstreamWithoutReplacingCachedEvidence()
    {
        var root = Directory.CreateTempSubdirectory("nes-lab-reference-changed-");
        try
        {
            var manifest = Path.Combine(root.FullName, "references.json");
            await File.WriteAllTextAsync(manifest, """
{"version":1,"entries":[{"id":"dma","title":"DMA","canonicalUrl":"https://example.test/dma","fetchUrl":"https://example.test/dma","format":"raw","authority":"test","topics":[],"aliases":[],"expectedSha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","upstreamRevision":"1","licenseStatus":"cache-only","claims":[]}]}
""", TestContext.Current.CancellationToken);
            using var http = new HttpClient(new StubHandler("changed"));
            var store = new ReferenceCorpusStore(manifest, Path.Combine(root.FullName, "cache"));
            Assert.Equal(ReferenceAvailability.ChangedUpstream,
                Assert.Single(await store.SyncAsync(http, TestContext.Current.CancellationToken)).Availability);
            Assert.Equal(ReferenceAvailability.Unavailable, store.Status().Single().Availability);
        }
        finally { Directory.Delete(root.FullName, true); }
    }

    private sealed class StubHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });
    }
}
