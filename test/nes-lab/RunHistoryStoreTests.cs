namespace Sheep.Nes.Lab.Tests;

public sealed class RunHistoryStoreTests
{
    [Fact]
    public async Task Record_LatestSearchAndMetricsRetainRunIdentity()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var log = Path.Combine(root, "failure.log");
            await File.WriteAllTextAsync(log, "failure", TestContext.Current.CancellationToken);
            using var store = new RunHistoryStore(Path.Combine(root, "index.sqlite"), root);
            var command = new VerificationCommand(VerificationScope.Conformance, "dotnet", ["test"]);
            var result = new VerificationResult(VerificationScope.Conformance, false,
                VerificationOutcome.TestFailure, 1, 12, new VerificationSummary(1, 0, 1, 0),
                [new VerificationIssue("DMA case", "wrong boundary")], log, 100);

            await store.RecordAsync(command, result, TestContext.Current.CancellationToken);
            store.RecordSemantics(new VerificationBatchResult(false, [result], 100),
                new VerificationSemantics(false, true, false, false,
                    VerificationStatus.AcceptedBaseline, VerificationExitPolicy.BaselineAware));

            var latest = Assert.IsType<RunHistoryEntry>(store.Latest(failuresOnly: true));
            Assert.Equal(VerificationOutcome.TestFailure, latest.Outcome);
            Assert.Equal(VerificationStatus.AcceptedBaseline, latest.VerificationStatus);
            Assert.Equal(VerificationExitPolicy.BaselineAware, latest.ExitPolicy);
            Assert.True(latest.MatchesAcceptedBaseline);
            Assert.Single(store.SearchFailures("DMA"));
            Assert.Equal(latest, store.Get(latest.Id));
            var metrics = store.Metrics();
            Assert.Equal(1, metrics.Total);
            Assert.Equal(1, metrics.Failed);
            Assert.Equal(1, metrics.AcceptedBaseline);
            Assert.Equal(1, metrics.BaselineAwarePolicyRuns);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task RecordSemantics_DoesNotLabelPassingNonConformanceRunAsAcceptedBaseline()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var log = Path.Combine(root, "passed.log");
            await File.WriteAllTextAsync(log, "passed", TestContext.Current.CancellationToken);
            using var store = new RunHistoryStore(Path.Combine(root, "index.sqlite"), root);
            var command = new VerificationCommand(VerificationScope.LabTests, "dotnet", ["test"]);
            var result = new VerificationResult(VerificationScope.LabTests, true,
                VerificationOutcome.Passed, 0, 1, new VerificationSummary(1, 0, 1, 0), [], log, 10);
            await store.RecordAsync(command, result, TestContext.Current.CancellationToken);

            store.RecordSemantics(new VerificationBatchResult(false, [result], 10),
                new VerificationSemantics(false, true, false, false,
                    VerificationStatus.AcceptedBaseline, VerificationExitPolicy.BaselineAware));

            var latest = Assert.IsType<RunHistoryEntry>(store.Latest());
            Assert.Equal(VerificationStatus.Passed, latest.VerificationStatus);
            Assert.Null(latest.MatchesAcceptedBaseline);
            Assert.False(latest.HasRegressions);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
