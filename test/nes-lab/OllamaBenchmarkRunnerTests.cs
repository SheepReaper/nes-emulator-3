namespace Sheep.Nes.Lab.Tests;

public sealed class OllamaBenchmarkRunnerTests
{
    [Fact]
    public async Task RunAsync_ScoresIdsAndRequiredSummaryTerms()
    {
        var fixture = new OllamaBenchmarkFixture(1,
        [
            new OllamaBenchmarkCase("passing",
                [new EvidenceCandidate("a", "fact", "content", "source-a", 1)],
                1, ["a"], ["source-a"]),
            new OllamaBenchmarkCase("failing",
                [new EvidenceCandidate("b", "fact", "content", "source-b", 1)],
                1, ["b"], ["source-b"])
        ]);
        var model = new SequenceModel(
            new LocalModelSelection(["a"], "Supported by source-a."),
            new LocalModelSelection(["wrong"], "No provenance."));

        var result = await OllamaBenchmarkRunner.RunAsync(
            model, "fixture-model", fixture, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Passed);
        Assert.Equal(2, result.Total);
        Assert.True(result.Cases[0].Passed);
        Assert.False(result.Cases[1].Passed);
        Assert.Equal(2, result.Cases[1].Failures.Count);
    }

    private sealed class SequenceModel(params LocalModelSelection[] selections) : ILocalEvidenceModel
    {
        private int index;

        public Task<LocalModelSelection> SelectAsync(IReadOnlyList<EvidenceCandidate> candidates,
            int maximumResults, CancellationToken cancellationToken) =>
            Task.FromResult(selections[index++]);
    }
}
