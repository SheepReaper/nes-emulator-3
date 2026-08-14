namespace Sheep.Nes.Lab.Tests;

public sealed class OptionalEvidenceRankerTests
{
    private static readonly EvidenceCandidate[] Candidates =
    [
        new("fact", "confirmedFact", "Documented DMA priority.", "NESdev DMA", 100),
        new("trace", "traceWindow", "First divergence at clock 42.", "trace.json", 90),
        new("guess", "hypothesis", "Maybe timing.", "note.md", 10)
    ];

    [Fact]
    public async Task Rank_DisabledUsesDeterministicOrderWithoutCallingModel()
    {
        var model = new StubModel(() => throw new InvalidOperationException("must not run"));

        var result = await new OptionalEvidenceRanker(model).RankAsync(
            Candidates, 2, useLocalModel: false, TestContext.Current.CancellationToken);

        Assert.False(result.ModelUsed);
        Assert.Equal(["fact", "trace"], result.Selected.Select(item => item.Id));
        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public async Task Rank_ValidatesUnknownDuplicatesAndMaximumWhilePreservingCandidates()
    {
        var model = new StubModel(() => new LocalModelSelection(
            ["trace", "missing", "trace", "fact", "guess"], "Trace is direct evidence."));

        var result = await new OptionalEvidenceRanker(model).RankAsync(
            Candidates, 2, useLocalModel: true, TestContext.Current.CancellationToken);

        Assert.True(result.ModelUsed);
        Assert.Equal(["trace", "fact"], result.Selected.Select(item => item.Id));
        Assert.Equal("trace.json", result.Selected[0].Source);
    }

    [Fact]
    public async Task Rank_ModelFailureFallsBackWithoutMakingModelRequired()
    {
        var model = new StubModel(() => throw new HttpRequestException("offline"));

        var result = await new OptionalEvidenceRanker(model).RankAsync(
            Candidates, 2, useLocalModel: true, TestContext.Current.CancellationToken);

        Assert.False(result.ModelUsed);
        Assert.Equal(["fact", "trace"], result.Selected.Select(item => item.Id));
        Assert.Contains("offline", result.ModelError, StringComparison.Ordinal);
    }

    private sealed class StubModel(Func<LocalModelSelection> result) : ILocalEvidenceModel
    {
        public int Calls { get; private set; }

        public Task<LocalModelSelection> SelectAsync(
            IReadOnlyList<EvidenceCandidate> candidates,
            int maximumResults,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result());
        }
    }
}
