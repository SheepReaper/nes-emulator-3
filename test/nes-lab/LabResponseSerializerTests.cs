using System.Text;
using System.Text.Json;
using Sheep.Nes.Lab;

namespace Sheep.Nes.Lab.Tests;

public sealed class LabResponseSerializerTests
{
    [Fact]
    public void SerializeVerification_ReportsCompactEvidenceReductionWithoutSelfReferentialEnvelopeSize()
    {
        var result = new VerificationBatchResult(true, [], 10_000);

        var payload = LabResponseSerializer.SerializeVerification(VerificationScope.Cpu, result);

        using var document = JsonDocument.Parse(payload);
        Assert.True(document.RootElement.GetProperty("compactEvidenceBytes").GetInt32() < 10_000);
        Assert.Equal("reduced", document.RootElement.GetProperty("evidenceSizeOutcome").GetString());
        Assert.True(document.RootElement.GetProperty("evidenceSizePercent").GetDouble() > 90);
        Assert.False(document.RootElement.TryGetProperty("reducedOutputBytes", out _));
        Assert.False(document.RootElement.TryGetProperty("reductionPercent", out _));
        Assert.False(document.RootElement.TryGetProperty("responseOutputBytes", out _));
    }

    [Fact]
    public void SerializeVerification_IncludesCacheHitCount()
    {
        var result = new VerificationBatchResult(true, [], 0, CacheHits: 2);

        var payload = LabResponseSerializer.SerializeVerification(VerificationScope.All, result);

        using var document = JsonDocument.Parse(payload);
        Assert.Equal(2, document.RootElement.GetProperty("cacheHits").GetInt32());
    }

    [Fact]
    public void SerializeVerification_ReportsCompactEvidenceExpansionForSmallRawOutput()
    {
        var result = new VerificationBatchResult(true, [], 1);

        var payload = LabResponseSerializer.SerializeVerification(VerificationScope.Cpu, result);

        using var document = JsonDocument.Parse(payload);
        var evidenceBytes = document.RootElement.GetProperty("compactEvidenceBytes").GetInt32();
        Assert.True(evidenceBytes > 1);
        Assert.Equal("expanded", document.RootElement.GetProperty("evidenceSizeOutcome").GetString());
        Assert.Equal(evidenceBytes - 1,
            document.RootElement.GetProperty("evidenceSizeDifferenceBytes").GetInt32());
        Assert.True(document.RootElement.GetProperty("evidenceSizePercent").GetDouble() > 0);
    }

    [Fact]
    public void SerializeVerification_KeepsFullFailureDiagnosticOutOfResponse()
    {
        var longDiagnostic = "Important boundary failure. " + new string('x', 2_000);
        var result = new VerificationBatchResult(false,
        [
            new VerificationResult(
                VerificationScope.Conformance, false, VerificationOutcome.TestFailure, 2, 10,
                new VerificationSummary(1, 1, 0, 0),
                [new VerificationIssue("Example.Failure", longDiagnostic)],
                "artifact.log", 10_000)
        ], 10_000);

        var payload = LabResponseSerializer.SerializeVerification(VerificationScope.Conformance, result);

        Assert.DoesNotContain(new string('x', 500), payload, StringComparison.Ordinal);
        Assert.Contains("Important boundary failure", payload, StringComparison.Ordinal);
        Assert.Contains("artifact.log", payload, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(payload);
        Assert.Equal("reduced", document.RootElement.GetProperty("evidenceSizeOutcome").GetString());
        Assert.True(document.RootElement.GetProperty("evidenceSizePercent").GetDouble() > 90);
    }

    [Fact]
    public void SerializeVerification_IncludesFailureTraceArtifact()
    {
        var result = new VerificationBatchResult(false,
        [
            new VerificationResult(
                VerificationScope.Conformance, false, VerificationOutcome.TestFailure, 2, 10,
                new VerificationSummary(1, 1, 0, 0), [], "run.log", 10_000,
                TraceArtifactPath: "trace.json")
        ], 10_000);

        var payload = LabResponseSerializer.SerializeVerification(VerificationScope.Conformance, result);

        Assert.Contains("trace.json", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeVerification_SeparatesExecutionBaselineRegressionAndExitPolicy()
    {
        var result = new VerificationBatchResult(false,
            [new VerificationResult(VerificationScope.Conformance, false, VerificationOutcome.TestFailure,
                2, 10, new VerificationSummary(243, 3, 239, 1), [], "run.log", 100)], 100);
        var baseline = new ConformanceBaselineComparison(1, true, 239, 1, 239, 1,
            true, false, false, []);

        using var document = JsonDocument.Parse(LabResponseSerializer.SerializeVerification(
            VerificationScope.Conformance, result, baseline, VerificationExitPolicy.BaselineAware));
        var root = document.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.False(root.GetProperty("executionPassed").GetBoolean());
        Assert.True(root.GetProperty("matchesAcceptedBaseline").GetBoolean());
        Assert.False(root.GetProperty("hasRegressions").GetBoolean());
        Assert.Equal("acceptedBaseline", root.GetProperty("verificationStatus").GetString());
        Assert.Equal("baselineAware", root.GetProperty("exitPolicy").GetString());
    }

    [Fact]
    public void SerializeVerification_CompactsBaselineFailureDiagnostics()
    {
        var longDiagnostic = "Expected marker. " + new string('x', 2_000);
        var result = new VerificationBatchResult(false,
            [new VerificationResult(VerificationScope.Conformance, false, VerificationOutcome.TestFailure,
                2, 10, new VerificationSummary(1, 1, 0, 0), [], "run.log", 3_000)], 3_000);
        var baseline = new ConformanceBaselineComparison(1, true, 0, 0, 0, 0,
            true, false, false,
            [new ConformanceBaselineCase("known", "Known case", BaselineCaseStatus.ExpectedKnownFailure,
                "evidence://known", longDiagnostic)]);

        var payload = LabResponseSerializer.SerializeVerification(
            VerificationScope.Conformance, result, baseline);

        Assert.Contains("Expected marker", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 500), payload, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(payload);
        Assert.Equal("reduced", document.RootElement.GetProperty("evidenceSizeOutcome").GetString());
        Assert.True(document.RootElement.GetProperty("evidenceSizePercent").GetDouble() > 0);
    }
}
