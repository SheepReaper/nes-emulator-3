namespace Sheep.Nes.Lab.Tests;

public sealed class GatewayBenchmarkTests
{
    [Fact]
    public void Corpus_UsesVersionedRepositoryTasks()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nes-emulator-3.slnx")))
            directory = directory.Parent;
        var result = GatewayBenchmark.LoadCorpus(directory?.FullName ?? throw new DirectoryNotFoundException());

        Assert.Equal(1, result.Version);
        Assert.Equal(8, result.Tasks.Count);
        Assert.All(result.Tasks, item => Assert.NotEmpty(item.RequiredPaths));
        Assert.Equal(64, result.Digest.Length);
    }

    [Fact]
    public void CorpusV2_UsesStructuredPacketEvidenceAndQualityThresholds()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nes-emulator-3.slnx")))
            directory = directory.Parent;
        var corpus = GatewayBenchmark.LoadCorpus(directory!.FullName, 2);

        Assert.Equal(2, corpus.Version);
        Assert.All(corpus.Tasks, task =>
        {
            var requiredEvidence = Assert.IsType<GatewayBenchmarkEvidence[]>(task.RequiredEvidence);
            Assert.NotEmpty(requiredEvidence);
            Assert.All(requiredEvidence, required =>
            {
                Assert.False(string.IsNullOrWhiteSpace(required.Path));
                Assert.NotEmpty(required.Markers);
            });
            Assert.Equal(.40, task.MinimumPrecisionAt5);
            Assert.Equal(.50, task.MinimumReciprocalRank);
        });
    }

    [Fact]
    public void QualityGate_RejectsRecallWithoutUsefulRankingOrPacketPresence()
    {
        var weak = new GatewayBenchmarkCase("weak", 2048, 1, 0, .03, [], [], 0, ["required"],
            100, 100, 100, 100, 1, true, true, "verify");

        Assert.False(GatewayBenchmark.PassesQualityGate(weak, .40, .50));
    }

    [Fact]
    public void CorpusV3_CertifiesRepositoryWideWorkflowsWithoutWeakeningV2()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nes-emulator-3.slnx")))
            directory = directory.Parent;
        var v2 = GatewayBenchmark.LoadCorpus(directory!.FullName, 2);
        var v3 = GatewayBenchmark.LoadCorpus(directory.FullName, 3);

        Assert.Equal(3, v3.Version);
        Assert.True(v3.Tasks.Count > v2.Tasks.Count);
        Assert.All(v2.Tasks, old => Assert.Contains(v3.Tasks, current => current.Name == old.Name));
        Assert.Contains(v3.Tasks, task => task.Name == "mapper-mmc3");
        Assert.Contains(v3.Tasks, task => task.Name == "winui-audio");
        Assert.Contains(v3.Tasks, task => task.Name == "build-namespace-migration");
    }

    [Fact]
    public void QualityGate_RejectsPacketWhoseRequiredEvidenceIsReportedMissing()
    {
        var weak = new GatewayBenchmarkCase("weak", 2048, 1, .5, 1, [], [], 1, [],
            100, 100, 100, 100, 1, true, true, "verify", RequiredEvidenceComplete: false);

        Assert.False(GatewayBenchmark.PassesQualityGate(weak, .40, .50));
    }
}
