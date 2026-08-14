using System.Net;
using System.Text;

namespace Sheep.Nes.Lab.Tests;

public sealed class OllamaEvidenceModelTests
{
    [Fact]
    public async Task SelectAsync_UsesStructuredNonStreamingLocalChatRequest()
    {
        string? requestJson = null;
        var handler = new StubHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"message":{"content":"{\"selectedIds\":[\"trace\"],\"summary\":\"Direct trace.\"}"}}
                    """, Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var model = new OllamaEvidenceModel(http, "nes-lab:test");

        var result = await model.SelectAsync([
            new EvidenceCandidate("trace", "traceWindow", "clock 42", "trace.json", 90)
        ], 1, TestContext.Current.CancellationToken);

        Assert.Equal(["trace"], result.SelectedIds);
        Assert.Equal("Direct trace.", result.Summary);
        Assert.Contains("\"model\":\"nes-lab:test\"", requestJson, StringComparison.Ordinal);
        Assert.Contains("\"stream\":false", requestJson, StringComparison.Ordinal);
        Assert.Contains("\"maxItems\":1", requestJson, StringComparison.Ordinal);
        Assert.Contains("trace.json", requestJson, StringComparison.Ordinal);
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => response(request);
    }
}
