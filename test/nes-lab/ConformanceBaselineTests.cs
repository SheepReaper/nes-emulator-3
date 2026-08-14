namespace Sheep.Nes.Lab.Tests;

public sealed class ConformanceBaselineTests
{
    [Fact]
    public void Compare_DistinguishesKnownNewResolvedAndChangedFailures()
    {
        var baseline = new AcceptedConformanceBaseline(1, 239, 1, [
            new("known", "Known case", ["error=02"], "evidence://known"),
            new("resolved", "Resolved case", ["error=01"], "evidence://resolved"),
            new("changed", "Changed case", ["error=03"], "evidence://changed")
        ]);
        VerificationIssue[] failures = [
            new("Known case test", "error=02"),
            new("Changed case test", "error=04"),
            new("New case test", "error=05")
        ];

        var result = ConformanceBaselineComparer.Compare(baseline, failures);

        Assert.Contains(result.Cases, item => item.Id == "known" && item.Status == BaselineCaseStatus.ExpectedKnownFailure);
        Assert.Contains(result.Cases, item => item.Id == "resolved" && item.Status == BaselineCaseStatus.ResolvedKnownFailure);
        Assert.Contains(result.Cases, item => item.Id == "changed" && item.Status == BaselineCaseStatus.ChangedFailureDiagnostic);
        Assert.Contains(result.Cases, item => item.Status == BaselineCaseStatus.NewFailure);
    }

    [Fact]
    public void Compare_RequiresExpectedPassAndSkipCounts()
    {
        var baseline = new AcceptedConformanceBaseline(1, 10, 1,
            [new("known", "Known case", ["error=02"], "evidence://known")]);
        var failures = new[] { new VerificationIssue("Known case test", "error=02") };

        var matching = ConformanceBaselineComparer.Compare(baseline, failures,
            new VerificationSummary(12, 1, 10, 1));
        var drifted = ConformanceBaselineComparer.Compare(baseline, failures,
            new VerificationSummary(13, 1, 11, 1));

        Assert.True(matching.MatchesAcceptedBaseline);
        Assert.False(matching.HasRegressions);
        Assert.False(drifted.MatchesAcceptedBaseline);
        Assert.True(drifted.HasRegressions);
        Assert.False(drifted.CountsMatch);
    }

    [Fact]
    public void Semantics_BaselineAwarePolicyAcceptsKnownAndResolvedFailuresOnly()
    {
        var failedBatch = new VerificationBatchResult(false, [Result(VerificationOutcome.TestFailure)], 1);
        var accepted = new ConformanceBaselineComparison(1, true, 10, 1, 10, 1, true,
            false, false, []);
        var improved = accepted with { MatchesAcceptedBaseline = false, HasResolvedBaselineCases = true };
        var regressed = accepted with { MatchesAcceptedBaseline = false, HasRegressions = true };

        Assert.False(VerificationSemantics.Evaluate(failedBatch, accepted, VerificationExitPolicy.Strict).ExitSucceeded);
        Assert.True(VerificationSemantics.Evaluate(failedBatch, accepted, VerificationExitPolicy.BaselineAware).ExitSucceeded);
        Assert.Equal(VerificationStatus.ImprovedBaseline,
            VerificationSemantics.Evaluate(failedBatch, improved, VerificationExitPolicy.BaselineAware).VerificationStatus);
        Assert.False(VerificationSemantics.Evaluate(failedBatch, regressed, VerificationExitPolicy.BaselineAware).ExitSucceeded);
        Assert.Equal(VerificationStatus.InfrastructureFailure,
            VerificationSemantics.Evaluate(new(false, [Result(VerificationOutcome.InfrastructureFailure)], 1),
                accepted, VerificationExitPolicy.BaselineAware).VerificationStatus);

        static VerificationResult Result(VerificationOutcome outcome) => new(VerificationScope.Conformance,
            false, outcome, 1, 1, new VerificationSummary(1, 1, 0, 0), [], "run.log", 1);
    }

    [Fact]
    public void Compare_TreatsResolvedFailurePassCountAsImprovement()
    {
        var baseline = new AcceptedConformanceBaseline(1, 10, 1,
            [new("resolved", "Resolved case", ["error=01"], "evidence://resolved")]);

        var result = ConformanceBaselineComparer.Compare(baseline, [],
            new VerificationSummary(12, 0, 11, 1));

        Assert.True(result.CountsMatch);
        Assert.False(result.HasRegressions);
        Assert.True(result.HasResolvedBaselineCases);
        Assert.False(result.MatchesAcceptedBaseline);
    }

    [Fact]
    public void Compare_FocusedKnownFailure_DoesNotEvaluateUnselectedBaselineCasesOrFullSuiteCounts()
    {
        var baseline = new AcceptedConformanceBaseline(1, 239, 1, [
            new("explicit", "Explicit DMA Abort", ["error=02"], "evidence://explicit"),
            new("implicit", "Implicit DMA Abort", ["error=02"], "evidence://implicit"),
            new("dmc", "Delta Modulation Channel", ["error=15"], "evidence://dmc")
        ]);
        VerificationIssue[] failures = [new("Focused test: Explicit DMA Abort", "error=02")];

        var result = ConformanceBaselineComparer.Compare(baseline, failures,
            new VerificationSummary(1, 1, 0, 0), "Explicit DMA Abort");

        Assert.True(result.MatchesAcceptedBaseline);
        Assert.False(result.HasRegressions);
        Assert.False(result.HasResolvedBaselineCases);
        Assert.True(result.CountsMatch);
        Assert.Equal(0, result.ExpectedPassed);
        Assert.Equal(0, result.ExpectedSkipped);
        Assert.Equal("Explicit DMA Abort", result.EvaluatedCaseName);
        Assert.Contains(result.Cases, item => item.Id == "explicit" &&
            item.Status == BaselineCaseStatus.ExpectedKnownFailure);
        Assert.Equal(2, result.Cases.Count(item => item.Status == BaselineCaseStatus.NotEvaluated));
        Assert.DoesNotContain(result.Cases, item => item.Status == BaselineCaseStatus.ResolvedKnownFailure);
    }

    [Fact]
    public void Compare_FocusedKnownFailurePass_IsAnImprovementWithoutARegression()
    {
        var baseline = new AcceptedConformanceBaseline(1, 239, 1,
            [new("explicit", "Explicit DMA Abort", ["error=02"], "evidence://explicit")]);

        var result = ConformanceBaselineComparer.Compare(baseline, [],
            new VerificationSummary(1, 0, 1, 0), "Explicit DMA Abort");

        Assert.False(result.MatchesAcceptedBaseline);
        Assert.False(result.HasRegressions);
        Assert.True(result.HasResolvedBaselineCases);
        Assert.True(result.CountsMatch);
        Assert.Contains(result.Cases, item => item.Status == BaselineCaseStatus.ResolvedKnownFailure);
    }

    [Fact]
    public void Compare_FocusedOrdinaryCase_UsesSinglePassingCaseContract()
    {
        var baseline = new AcceptedConformanceBaseline(1, 239, 1,
            [new("explicit", "Explicit DMA Abort", ["error=02"], "evidence://explicit")]);

        var passing = ConformanceBaselineComparer.Compare(baseline, [],
            new VerificationSummary(1, 0, 1, 0), "Ordinary manifest case");
        var failing = ConformanceBaselineComparer.Compare(baseline,
            [new VerificationIssue("Ordinary manifest case", "unexpected")],
            new VerificationSummary(1, 1, 0, 0), "Ordinary manifest case");

        Assert.True(passing.MatchesAcceptedBaseline);
        Assert.False(passing.HasRegressions);
        Assert.Equal(1, passing.ExpectedPassed);
        Assert.True(failing.HasRegressions);
        Assert.Contains(failing.Cases, item => item.Status == BaselineCaseStatus.NewFailure);
    }
}
