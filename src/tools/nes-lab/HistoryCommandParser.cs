namespace Sheep.Nes.Lab;

public sealed record HistoryInvocation(
    string Operation, VerificationScope? Scope, string? CaseName, string? Query, int MaximumResults);
public sealed record HistoryParseResult(HistoryInvocation? Invocation, LabError? Error);

public static class HistoryCommandParser
{
    public static HistoryParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count > 0 && args[0].Equals("history", StringComparison.OrdinalIgnoreCase))
            args = args.Skip(1).ToArray();
        if (args.Count == 1 && args[0].Equals("metrics", StringComparison.OrdinalIgnoreCase))
            return new(new HistoryInvocation("metrics", null, null, null, 0), null);
        if (args.Count < 2 || args[0] is not ("runs" or "failures"))
            return Failure("invalid_history_command", "Expected history runs latest, history failures latest/search, or history metrics (legacy aliases: runs, failures, metrics).");
        var operation = $"{args[0]}-{args[1]}";
        if (operation is not ("runs-latest" or "failures-latest" or "failures-search"))
            return Failure("invalid_history_command", $"Unknown history operation '{operation}'.");
        string? caseName = null, query = null;
        VerificationScope? scope = null;
        var maximum = 32;
        for (var index = 2; index < args.Count; index++)
        {
            var option = args[index];
            if (++index >= args.Count) return Failure("missing_value", $"{option} requires a value.");
            var value = args[index];
            switch (option)
            {
                case "--case": caseName = value; break;
                case "--query": query = value; break;
                case "--max" when int.TryParse(value, out maximum) && maximum > 0: break;
                case "--scope" when Enum.TryParse<VerificationScope>(value.Replace("-", ""), true, out var parsed): scope = parsed; break;
                default: return Failure("invalid_option", $"Invalid history option '{option}'.");
            }
        }
        if (operation == "failures-search" && string.IsNullOrWhiteSpace(query))
            return Failure("missing_query", "failures search requires --query.");
        return new(new HistoryInvocation(operation, scope, caseName, query, maximum), null);
    }

    private static HistoryParseResult Failure(string code, string message) => new(null, new LabError(code, message));
}
