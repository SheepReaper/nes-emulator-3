using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sheep.Nes.Lab;

public static class LabResponseSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string SerializeVerification(VerificationScope scope, VerificationBatchResult result,
        ConformanceBaselineComparison? conformanceBaseline = null,
        VerificationExitPolicy exitPolicy = VerificationExitPolicy.Strict)
    {
        var semantics = VerificationSemantics.Evaluate(result, conformanceBaseline, exitPolicy);
        var compactBaseline = ToCompactBaseline(conformanceBaseline);
        var compactResults = result.Results.Select(ToCompactResult).ToArray();
        var compactEvidenceBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(
            new VerificationEvidencePayload(compactResults, compactBaseline), Options));
        var evidenceSize = VerificationOutputSize.Create(result.RawOutputBytes, compactEvidenceBytes);
        return JsonSerializer.Serialize(new VerificationResponse(
            2,
            result.Success,
            semantics.ExecutionPassed,
            semantics.MatchesAcceptedBaseline,
            semantics.HasRegressions,
            semantics.HasResolvedBaselineCases,
            semantics.VerificationStatus,
            semantics.ExitPolicy,
            "verify",
            scope,
            result.RawOutputBytes,
            compactEvidenceBytes,
            evidenceSize.Outcome,
            evidenceSize.DifferenceBytes,
            evidenceSize.Percent,
            result.CacheHits,
            compactResults,
            compactBaseline), Options);
    }

    private static ConformanceBaselineComparison? ToCompactBaseline(
        ConformanceBaselineComparison? baseline) => baseline is null
        ? null
        : baseline with
        {
            Cases = baseline.Cases.Select(item => item with
            {
                Diagnostic = Abbreviate(item.Diagnostic, 240)
            }).ToArray()
        };

    private static CompactVerificationResult ToCompactResult(VerificationResult result) => new(
        result.Scope,
        result.Success,
        result.Outcome,
        result.ExitCode,
        result.DurationMilliseconds,
        result.Summary,
        result.Failures.Select(issue => new CompactVerificationIssue(
            Abbreviate(issue.Name, 180),
            Abbreviate(issue.Diagnostic, 240))).ToArray(),
        result.ArtifactPath,
        result.RawOutputBytes,
        result.Cached,
        result.TraceArtifactPath,
        result.BuildDiagnostics);

    private static string? Abbreviate(string? value, int maximumLength)
    {
        if (value is null || value.Length <= maximumLength)
            return value;
        return value[..(maximumLength - 1)] + "…";
    }

    private sealed record VerificationResponse(
        int SchemaVersion,
        bool Success,
        bool ExecutionPassed,
        bool? MatchesAcceptedBaseline,
        bool HasRegressions,
        bool HasResolvedBaselineCases,
        VerificationStatus VerificationStatus,
        VerificationExitPolicy ExitPolicy,
        string Operation,
        VerificationScope Scope,
        int RawOutputBytes,
        int CompactEvidenceBytes,
        VerificationOutputSizeOutcome EvidenceSizeOutcome,
        int EvidenceSizeDifferenceBytes,
        double? EvidenceSizePercent,
        int CacheHits,
        IReadOnlyList<CompactVerificationResult> Results,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ConformanceBaselineComparison? ConformanceBaseline);

    private sealed record VerificationEvidencePayload(
        IReadOnlyList<CompactVerificationResult> Results,
        ConformanceBaselineComparison? ConformanceBaseline);

    private sealed record CompactVerificationResult(
        VerificationScope Scope,
        bool Success,
        VerificationOutcome Outcome,
        int ExitCode,
        long DurationMilliseconds,
        VerificationSummary Summary,
        IReadOnlyList<CompactVerificationIssue> Failures,
        string ArtifactPath,
        int RawOutputBytes,
        bool Cached,
        string? TraceArtifactPath,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BuildDiagnosticReport? BuildDiagnostics);

    private sealed record CompactVerificationIssue(string? Name, string? Diagnostic);

    private sealed record VerificationOutputSize(
        int ComparedBytes,
        VerificationOutputSizeOutcome Outcome,
        int DifferenceBytes,
        double? Percent)
    {
        internal static VerificationOutputSize Create(int rawBytes, int responseBytes)
        {
            var difference = Math.Abs(responseBytes - rawBytes);
            var outcome = responseBytes < rawBytes
                ? VerificationOutputSizeOutcome.Reduced
                : responseBytes > rawBytes
                    ? VerificationOutputSizeOutcome.Expanded
                    : VerificationOutputSizeOutcome.Unchanged;
            var percent = rawBytes == 0
                ? (double?)null
                : Math.Round((double)difference / rawBytes * 100, 1);
            return new(responseBytes, outcome, difference, percent);
        }
    }

    private enum VerificationOutputSizeOutcome { Reduced, Expanded, Unchanged }
}
