namespace Sheep.Nes.Lab;

public sealed record ContextBuildInvocation(
    string? Symbol, string? SymbolId, int BudgetBytes, string SolutionPath,
    string? ExactQualifiedName = null, string? Kind = null, string? Project = null,
    string? Namespace = null, string? FilePath = null,
    bool Changed = false, string? BaseRevision = null, string? Subsystem = null,
    string? Task = null, string? RunId = null,
    bool RankLocal = false, bool SynthesizeLocal = false, string Model = "nes-lab:devstral-24b", string Endpoint = "http://localhost:11434/",
    int? BudgetTokens = null, string? HandoffUri = null);
public sealed record ContextParseResult(ContextBuildInvocation? Invocation, LabError? Error);

public static class ContextCommandParser
{
    public static ContextParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count < 2 || args[0] != "context" || args[1] != "build")
            return Failure("invalid_command", "Expected 'context build'.");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var changed = false;
        for (var index = 2; index < args.Count; index++)
        {
            var option = args[index];
            if (option.Equals("--changed", StringComparison.OrdinalIgnoreCase))
            {
                changed = true;
                continue;
            }
            if (!option.StartsWith("--", StringComparison.Ordinal) || ++index >= args.Count)
                return Failure("missing_value", $"{option} requires a value.");
            values[option] = args[index];
        }
        var symbol = values.GetValueOrDefault("--symbol");
        var symbolId = values.GetValueOrDefault("--id");
        var subsystem = values.GetValueOrDefault("--subsystem");
        var task = values.GetValueOrDefault("--task");
        var runId = values.GetValueOrDefault("--run");
        var handoffUri = values.GetValueOrDefault("--handoff");
        var selectorCount = new[] { symbol, symbolId, subsystem, task, runId, handoffUri }
            .Count(value => !string.IsNullOrWhiteSpace(value)) + (changed ? 1 : 0);
        if (selectorCount != 1)
            return Failure(selectorCount == 0 ? "missing_context_selector" : "conflicting_context_selectors",
                "context build requires exactly one primary selector.");
        string[] supportedSubsystems = ["cpu", "ppu", "apu", "dma", "mapper", "winui", "conformance", "lab"];
        if (subsystem is not null && !supportedSubsystems.Contains(subsystem, StringComparer.OrdinalIgnoreCase))
            return Failure("invalid_subsystem", $"Unsupported subsystem '{subsystem}'.");
        var baseRevision = values.GetValueOrDefault("--base");
        if (baseRevision is not null && !changed)
            return Failure("base_requires_changed", "--base is valid only with --changed.");
        var budget = values.TryGetValue("--budget", out var text) && int.TryParse(text, out var parsed)
            ? parsed : 12_000;
        if (budget < 128) return Failure("invalid_budget", "--budget must be at least 128 bytes.");
        if (budget > McpResponseLimits.MaximumContextBytes)
            return Failure("invalid_budget", $"--budget cannot exceed {McpResponseLimits.MaximumContextBytes} bytes.");
        int? budgetTokens = null;
        if (values.TryGetValue("--budget-tokens", out var tokenText))
        {
            if (!int.TryParse(tokenText, out var tokenValue))
                return Failure("invalid_budget_tokens", "--budget-tokens must be an integer.");
            budgetTokens = tokenValue;
        }
        if (budgetTokens is not null && budgetTokens < 32)
            return Failure("invalid_budget_tokens", "--budget-tokens must be at least 32.");
        var rankLocal = values.GetValueOrDefault("--rank")?.Equals("local", StringComparison.OrdinalIgnoreCase) == true;
        var synthesizeLocal = values.GetValueOrDefault("--synthesize")?.Equals("local", StringComparison.OrdinalIgnoreCase) == true;
        return new(new ContextBuildInvocation(symbol, symbolId, budget,
            values.GetValueOrDefault("--solution") ?? "nes-emulator-3.slnx",
            values.GetValueOrDefault("--qualified"), values.GetValueOrDefault("--kind"),
            values.GetValueOrDefault("--project"), values.GetValueOrDefault("--namespace"),
            values.GetValueOrDefault("--file"), changed, baseRevision, subsystem, task, runId, rankLocal, synthesizeLocal,
            values.GetValueOrDefault("--model") ?? "nes-lab:devstral-24b",
            values.GetValueOrDefault("--endpoint") ?? "http://localhost:11434/",
            budgetTokens, handoffUri), null);
    }

    private static ContextParseResult Failure(string code, string message) => new(null, new LabError(code, message));
}
