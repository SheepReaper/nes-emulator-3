using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed record AcceptedConformanceFailure(string Id, string NameContains,
    string[] DiagnosticContains, string EvidenceUri);
public sealed record AcceptedConformanceBaseline(int Version, int ExpectedPassed, int ExpectedSkipped,
    AcceptedConformanceFailure[] KnownFailures);
public enum BaselineCaseStatus
{
    ExpectedKnownFailure, NewFailure, ResolvedKnownFailure, ChangedFailureDiagnostic, NotEvaluated
}
public sealed record ConformanceBaselineCase(string Id, string Name, BaselineCaseStatus Status,
    string? EvidenceUri, string? Diagnostic);
public sealed record ConformanceBaselineComparison(int BaselineVersion, bool MatchesAcceptedBaseline,
    int ExpectedPassed, int ExpectedSkipped, int? ActualPassed, int? ActualSkipped,
    bool CountsMatch, bool HasRegressions, bool HasResolvedBaselineCases,
    IReadOnlyList<ConformanceBaselineCase> Cases, string? EvaluatedCaseName = null);

public static class ConformanceBaselineComparer
{
    public static AcceptedConformanceBaseline Load(string path) =>
        JsonSerializer.Deserialize<AcceptedConformanceBaseline>(File.ReadAllText(path), LabResponseSerializer.Options)
        ?? throw new InvalidDataException("Accepted conformance baseline is invalid.");

    public static ConformanceBaselineComparison Compare(AcceptedConformanceBaseline baseline,
        IReadOnlyList<VerificationIssue> failures, VerificationSummary? summary = null,
        string? selectedCaseName = null)
    {
        List<ConformanceBaselineCase> cases = [];
        HashSet<VerificationIssue> matched = [];
        var selectedKnownFailureCount = selectedCaseName is null
            ? baseline.KnownFailures.Length
            : baseline.KnownFailures.Count(expected => NamesMatch(selectedCaseName, expected.NameContains));
        foreach (var expected in baseline.KnownFailures)
        {
            if (selectedCaseName is not null && !NamesMatch(selectedCaseName, expected.NameContains))
            {
                cases.Add(new(expected.Id, expected.NameContains, BaselineCaseStatus.NotEvaluated,
                    expected.EvidenceUri, null));
                continue;
            }

            var actual = failures.FirstOrDefault(failure => failure.Name?.Contains(expected.NameContains,
                StringComparison.OrdinalIgnoreCase) == true);
            if (actual is null)
            {
                cases.Add(new(expected.Id, expected.NameContains, BaselineCaseStatus.ResolvedKnownFailure,
                    expected.EvidenceUri, null));
                continue;
            }
            matched.Add(actual);
            var diagnosticMatches = expected.DiagnosticContains.All(marker =>
                actual.Diagnostic.Contains(marker, StringComparison.OrdinalIgnoreCase));
            cases.Add(new(expected.Id, actual.Name ?? expected.NameContains,
                diagnosticMatches ? BaselineCaseStatus.ExpectedKnownFailure : BaselineCaseStatus.ChangedFailureDiagnostic,
                expected.EvidenceUri, actual.Diagnostic));
        }
        foreach (var failure in failures.Where(item => !matched.Contains(item)))
            cases.Add(new("new-" + cases.Count, failure.Name ?? "unknown", BaselineCaseStatus.NewFailure,
                null, failure.Diagnostic));
        var actualPassed = summary?.Succeeded;
        var actualSkipped = summary?.Skipped;
        var resolvedCount = cases.Count(item => item.Status == BaselineCaseStatus.ResolvedKnownFailure);
        var expectedPassed = selectedCaseName is null
            ? baseline.ExpectedPassed
            : selectedKnownFailureCount == 0 ? 1 : 0;
        var expectedSkipped = selectedCaseName is null ? baseline.ExpectedSkipped : 0;
        var countsMatch = actualPassed is null || actualSkipped is null ||
            (actualPassed == expectedPassed + resolvedCount && actualSkipped == expectedSkipped);
        var hasRegressions = !countsMatch || cases.Any(item => item.Status is
            BaselineCaseStatus.NewFailure or BaselineCaseStatus.ChangedFailureDiagnostic);
        var hasResolved = resolvedCount > 0;
        var matches = countsMatch && !hasRegressions && !hasResolved &&
            cases.Where(item => item.Status != BaselineCaseStatus.NotEvaluated)
                .All(item => item.Status == BaselineCaseStatus.ExpectedKnownFailure);
        return new(baseline.Version, matches, expectedPassed, expectedSkipped,
            actualPassed, actualSkipped, countsMatch, hasRegressions, hasResolved, cases, selectedCaseName);
    }

    private static bool NamesMatch(string selectedCaseName, string expectedName) =>
        selectedCaseName.Contains(expectedName, StringComparison.OrdinalIgnoreCase) ||
        expectedName.Contains(selectedCaseName, StringComparison.OrdinalIgnoreCase);
}

public sealed record VerificationSemantics(bool ExecutionPassed, bool? MatchesAcceptedBaseline,
    bool HasRegressions, bool HasResolvedBaselineCases, VerificationStatus VerificationStatus,
    VerificationExitPolicy ExitPolicy)
{
    public bool ExitSucceeded => ExitPolicy == VerificationExitPolicy.Strict
        ? ExecutionPassed
        : !HasRegressions && VerificationStatus != VerificationStatus.InfrastructureFailure;

    public static VerificationSemantics Evaluate(VerificationBatchResult result,
        ConformanceBaselineComparison? baseline, VerificationExitPolicy exitPolicy)
    {
        var infrastructureFailure = result.Results.Any(item => item.Outcome is
            VerificationOutcome.InfrastructureFailure or VerificationOutcome.Cancelled);
        var hasRegressions = infrastructureFailure || baseline?.HasRegressions == true ||
            (!result.Success && baseline is null);
        var resolved = baseline?.HasResolvedBaselineCases == true;
        var status = infrastructureFailure ? VerificationStatus.InfrastructureFailure
            : hasRegressions ? VerificationStatus.Regression
            : result.Success && baseline is null ? VerificationStatus.Passed
            : resolved ? VerificationStatus.ImprovedBaseline
            : baseline?.MatchesAcceptedBaseline == true ? VerificationStatus.AcceptedBaseline
            : result.Success ? VerificationStatus.Passed
            : VerificationStatus.Regression;
        return new(result.Success, baseline?.MatchesAcceptedBaseline, hasRegressions, resolved, status, exitPolicy);
    }
}
