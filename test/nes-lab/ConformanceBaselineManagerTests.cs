namespace Sheep.Nes.Lab.Tests;

public sealed class ConformanceBaselineManagerTests
{
    private static readonly AcceptedConformanceBaseline Baseline = new(1, 239, 1,
    [
        new("fixed", "Fixed Case", ["error=02"], "nes-lab://fixed"),
        new("remaining", "Remaining Case", ["error=15"], "nes-lab://remaining")
    ]);

    [Fact]
    public void Parser_DefaultsToPreviewOfLatestRun()
    {
        var parsed = BaselineCommandParser.Parse(["baseline", "update"]);

        Assert.Null(parsed.Error);
        Assert.Equal(new BaselineUpdateInvocation("latest", false), parsed.Invocation);
    }

    [Fact]
    public void Propose_RemovesResolvedFailuresAndUsesObservedCounts()
    {
        var log = """
            failed Remaining Case (1s)
              error=15
            Test run summary:
              total: 242
              failed: 1
              succeeded: 240
              skipped: 1
            """;

        var proposal = ConformanceBaselineManager.Propose(Baseline, FullRun(), log);

        Assert.True(proposal.Changed);
        Assert.False(proposal.Applied);
        Assert.Equal(240, proposal.Proposed.ExpectedPassed);
        Assert.Single(proposal.Proposed.KnownFailures);
        Assert.Equal(["fixed"], proposal.ResolvedFailureIds);
    }

    [Fact]
    public void Propose_RejectsNewFailures()
    {
        var log = """
            failed New Case (1s)
              error=99
            Test run summary:
              total: 242
              failed: 1
              succeeded: 240
              skipped: 1
            """;

        Assert.Throws<InvalidOperationException>(() =>
            ConformanceBaselineManager.Propose(Baseline, FullRun(), log));
    }

    [Fact]
    public void Propose_RejectsChangedKnownFailureDiagnostics()
    {
        var log = """
            failed Remaining Case (1s)
              error=16
            Test run summary:
              total: 242
              failed: 1
              succeeded: 240
              skipped: 1
            """;

        Assert.Throws<InvalidOperationException>(() =>
            ConformanceBaselineManager.Propose(Baseline, FullRun(), log));
    }

    [Fact]
    public void Propose_RejectsIncompleteSummaries()
    {
        const string log = "total: 242\nsucceeded: 242\nskipped: 0";

        Assert.Throws<InvalidDataException>(() =>
            ConformanceBaselineManager.Propose(Baseline, FullRun(), log));
    }

    [Fact]
    public void Propose_RejectsFocusedRuns()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ConformanceBaselineManager.Propose(Baseline, FullRun() with { CaseName = "Fixed Case" }, ""));
    }

    private static RunHistoryEntry FullRun() => new("run", DateTimeOffset.UtcNow,
        VerificationScope.Conformance, null, VerificationOutcome.TestFailure, false, false, 1, 1,
        "log.txt", null, "fingerprint", "head", null);
}
