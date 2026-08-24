using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed record BaselineUpdateInvocation(string RunId, bool Apply);

public sealed record BaselineUpdateProposal(
    AcceptedConformanceBaseline Current,
    AcceptedConformanceBaseline Proposed,
    string RunId,
    string? RunResourceUri,
    bool Changed,
    bool Applied,
    IReadOnlyList<string> ResolvedFailureIds,
    IReadOnlyList<string> RetainedFailureIds,
    VerificationSummary Summary);

public static class BaselineCommandParser
{
    public static (string? Operation, BaselineUpdateInvocation? Invocation, LabError? Error) Parse(
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2 || !arguments[0].Equals("baseline", StringComparison.OrdinalIgnoreCase))
            return (null, null, new("invalid_baseline_command", "Expected baseline show or baseline update."));
        if (arguments[1].Equals("show", StringComparison.OrdinalIgnoreCase))
            return arguments.Count == 2
                ? ("show", null, null)
                : (null, null, new("invalid_baseline_argument", $"Unknown baseline show argument '{arguments[2]}'."));
        if (!arguments[1].Equals("update", StringComparison.OrdinalIgnoreCase))
            return (null, null, new("invalid_baseline_operation", $"Unknown baseline operation '{arguments[1]}'."));

        var runId = "latest";
        var apply = false;
        for (var index = 2; index < arguments.Count; index++)
        {
            if (arguments[index].Equals("--apply", StringComparison.OrdinalIgnoreCase)) apply = true;
            else if (arguments[index].Equals("--run", StringComparison.OrdinalIgnoreCase) && index + 1 < arguments.Count)
                runId = arguments[++index];
            else return (null, null, new("invalid_baseline_argument",
                $"Unknown or incomplete baseline update argument '{arguments[index]}'."));
        }
        return ("update", new(runId, apply), null);
    }
}

public static class ConformanceBaselineManager
{
    public static BaselineUpdateProposal Propose(AcceptedConformanceBaseline baseline,
        RunHistoryEntry run, string log)
    {
        if (run.Scope != VerificationScope.Conformance || run.CaseName is not null)
            throw new InvalidOperationException("Accepted baselines may be updated only from a complete, unfiltered conformance run.");
        if (run.Outcome is VerificationOutcome.InfrastructureFailure or VerificationOutcome.Cancelled)
            throw new InvalidOperationException($"Run '{run.Id}' ended with {run.Outcome} and cannot update the baseline.");

        var summary = VerificationOutputParser.Parse(log);
        if (summary is not { Total: not null, Failed: not null, Succeeded: not null, Skipped: not null } ||
            summary.Total != summary.Failed + summary.Succeeded + summary.Skipped)
            throw new InvalidDataException("The selected run does not contain a complete, internally consistent test summary.");

        var failures = VerificationOutputParser.ParseFailures(log);
        if (failures.Count != summary.Failed)
            throw new InvalidDataException(
                $"The selected run reports {summary.Failed} failures but only {failures.Count} structured failure diagnostics were recovered.");
        var comparison = ConformanceBaselineComparer.Compare(baseline, failures, summary);
        if (comparison.HasRegressions)
            throw new InvalidOperationException(
                "The selected run contains a new failure, changed diagnostic, or count inconsistency. NES Lab will not accept regressions automatically.");

        var retainedIds = comparison.Cases
            .Where(item => item.Status == BaselineCaseStatus.ExpectedKnownFailure)
            .Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var retained = baseline.KnownFailures.Where(item => retainedIds.Contains(item.Id)).ToArray();
        var resolved = baseline.KnownFailures.Where(item => !retainedIds.Contains(item.Id))
            .Select(item => item.Id).ToArray();
        var proposed = baseline with
        {
            ExpectedPassed = summary.Succeeded.Value,
            ExpectedSkipped = summary.Skipped.Value,
            KnownFailures = retained
        };
        var changed = baseline.ExpectedPassed != proposed.ExpectedPassed ||
            baseline.ExpectedSkipped != proposed.ExpectedSkipped || resolved.Length != 0;
        return new(baseline, proposed, run.Id, run.ResourceUri, changed, false, resolved,
            retained.Select(item => item.Id).ToArray(), summary);
    }

    public static BaselineUpdateProposal Apply(BaselineUpdateProposal proposal, string path)
    {
        if (!proposal.Changed) return proposal with { Applied = false };
        var temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(proposal.Proposed,
                new JsonSerializerOptions(LabResponseSerializer.Options) { WriteIndented = true }) + Environment.NewLine);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        return proposal with { Applied = true };
    }
}
