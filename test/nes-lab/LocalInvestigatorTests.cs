using System.Text.Json;

namespace Sheep.Nes.Lab.Tests;

public sealed class LocalInvestigatorTests
{
    [Fact]
    public async Task Investigate_ExecutesOnlyAllowlistedQueriesAndRetainsCitedClaims()
    {
        using var temporary = new TemporaryDirectory();
        var initial = new ContextEvidence(ContextEvidenceKind.Reference, 100, "initial", "task://request",
            EvidenceId: "evidence-initial");
        var model = new StubModel([
            new(false, "inspect code", new("code", "find", Json("{\"symbol\":\"Cpu\"}")), [], [], null, null),
            new(true, "finish", null, [new("CPU is relevant", ["evidence-query"])],
                [new("Timing remains uncertain", ["evidence-initial"])], null, "verify --scope cpu")
        ]);
        var dispatcher = new StubDispatcher();

        var result = await new LocalInvestigator(temporary.Path, model, dispatcher).InvestigateAsync(
            "cpu timing", [initial], 4096, 8, TestContext.Current.CancellationToken);

        Assert.True(result.ModelUsed);
        Assert.Single(result.Queries);
        Assert.Single(result.Observations);
        Assert.StartsWith("nes-lab://artifact/investigation/sha256/", result.TranscriptUri);
    }

    [Fact]
    public async Task Investigate_ModelFailureReturnsDeterministicPacketUnchanged()
    {
        using var temporary = new TemporaryDirectory();
        var evidence = new ContextEvidence(ContextEvidenceKind.Reference, 100, "fact", "source",
            EvidenceId: "evidence-fact");
        var model = new ThrowingModel();

        var result = await new LocalInvestigator(temporary.Path, model, new StubDispatcher())
            .InvestigateAsync("task", [evidence], 1024, 4, TestContext.Current.CancellationToken);

        Assert.False(result.ModelUsed);
        Assert.NotNull(result.FallbackReason);
        Assert.Contains("evidence-fact", result.Packet.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispatcher_RejectsStateChangingOperation()
    {
        Assert.False(LabInvestigationDispatcher.IsAllowed("experiment", "run-inline"));
        Assert.True(LabInvestigationDispatcher.IsAllowed("media", "audio-analyze"));
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();
    private sealed class StubDispatcher : IReadOnlyInvestigationDispatcher
    {
        public Task<JsonElement> ExecuteAsync(InvestigationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(JsonSerializer.SerializeToElement(new { value = "Cpu", evidenceId = "evidence-query" }));
    }
    private sealed class StubModel(IEnumerable<InvestigationModelTurn> values) : ILocalInvestigationModel
    {
        private readonly Queue<InvestigationModelTurn> turns = new(values);
        public string ModelName => "stub";
        public Task<InvestigationModelTurn> NextAsync(InvestigationModelInput input, CancellationToken token) =>
            Task.FromResult(turns.Dequeue());
    }
    private sealed class ThrowingModel : ILocalInvestigationModel
    {
        public string ModelName => "broken";
        public Task<InvestigationModelTurn> NextAsync(InvestigationModelInput input, CancellationToken token) =>
            throw new HttpRequestException("offline");
    }
    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nes-lab-investigator-" + Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
