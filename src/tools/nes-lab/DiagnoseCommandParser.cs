namespace Sheep.Nes.Lab;

public sealed record DiagnoseInvocation(string? CaseName, string? RunId, int BudgetBytes, bool Persist = false,
    bool RankLocal = false, string Model = "nes-lab:devstral-24b", string Endpoint = "http://localhost:11434/",
    int? BudgetTokens = null);
public sealed record DiagnoseParseResult(DiagnoseInvocation? Invocation, LabError? Error);

public static class DiagnoseCommandParser
{
    public static DiagnoseParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || !args[0].Equals("diagnose", StringComparison.OrdinalIgnoreCase))
            return Failure("invalid_command", "Expected diagnose --case <name> or --run <id|latest>.");
        string? caseName = null, runId = null;
        var budget = 16_000;
        var persist = false;
        var rankLocal = false;
        var model = "nes-lab:devstral-24b";
        var endpoint = "http://localhost:11434/";
        int? budgetTokens = null;
        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index];
            if (option == "--persist") { persist = true; continue; }
            if (++index >= args.Count) return Failure("missing_value", $"{option} requires a value.");
            var value = args[index];
            switch (option)
            {
                case "--case": caseName = value; break;
                case "--run": runId = value; break;
                case "--budget" when int.TryParse(value, out budget) && budget >= 512: break;
                case "--budget-tokens" when int.TryParse(value, out var tokenBudget) && tokenBudget >= 32: budgetTokens = tokenBudget; break;
                case "--budget-tokens": return Failure("invalid_budget_tokens", "--budget-tokens must be at least 32.");
                case "--rank" when value.Equals("local", StringComparison.OrdinalIgnoreCase): rankLocal = true; break;
                case "--model": model = value; break;
                case "--endpoint": endpoint = value; break;
                default: return Failure("invalid_option", $"Invalid diagnose option '{option}'.");
            }
        }
        if ((caseName is null) == (runId is null))
            return Failure("invalid_selection", "Specify exactly one of --case or --run.");
        return new(new DiagnoseInvocation(caseName, runId, budget, persist, rankLocal, model, endpoint, budgetTokens), null);
    }

    private static DiagnoseParseResult Failure(string code, string message) => new(null, new LabError(code, message));
}
