namespace Sheep.Nes.Lab;

public abstract record CodeInvocation(string SolutionPath);
public sealed record CodeFindInvocation(string Symbol, int MaximumResults, string SolutionPath,
    string? ExactQualifiedName = null, string? Kind = null, string? Project = null,
    string? Namespace = null, string? FilePath = null)
    : CodeInvocation(SolutionPath);
public sealed record CodeBenchmarkInvocation(string Symbol, int Iterations, string SolutionPath)
    : CodeInvocation(SolutionPath);
public sealed record CodeRelationsInvocation(string Operation, string SymbolId, string SolutionPath)
    : CodeInvocation(SolutionPath);
public sealed record CodeParseResult(CodeInvocation? Invocation, LabError? Error);

public static class CodeCommandParser
{
    public static CodeParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count < 2 || !args[0].Equals("code", StringComparison.OrdinalIgnoreCase))
            return Failure("invalid_command", "Expected 'code find|refs|callers|tests'.");

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 2; index < args.Count; index++)
        {
            var option = args[index];
            if (!option.StartsWith("--", StringComparison.Ordinal) || ++index >= args.Count)
                return Failure("missing_value", $"{option} requires a value.");
            values[option] = args[index];
        }

        var solution = values.GetValueOrDefault("--solution") ?? "nes-emulator-3.slnx";
        var operation = args[1].ToLowerInvariant();
        if (operation is "find" or "benchmark")
        {
            if (!values.TryGetValue("--symbol", out var symbol) || string.IsNullOrWhiteSpace(symbol))
                return Failure("missing_symbol", $"code {operation} requires --symbol.");
            if (operation == "benchmark")
            {
                var iterations = values.TryGetValue("--iterations", out var iterationsText) &&
                    int.TryParse(iterationsText, out var parsedIterations) ? parsedIterations : 5;
                return iterations > 0
                    ? new(new CodeBenchmarkInvocation(symbol, iterations, solution), null)
                    : Failure("invalid_iterations", "--iterations must be positive.");
            }
            var maximum = values.TryGetValue("--max", out var text) && int.TryParse(text, out var parsed)
                ? parsed : 64;
            if (maximum <= 0)
                return Failure("invalid_maximum", "--max must be positive.");
            return new(new CodeFindInvocation(symbol, maximum, solution,
                values.GetValueOrDefault("--qualified"), values.GetValueOrDefault("--kind"),
                values.GetValueOrDefault("--project"), values.GetValueOrDefault("--namespace"),
                values.GetValueOrDefault("--file")), null);
        }

        if (operation is not ("refs" or "callers" or "tests"))
            return Failure("invalid_code_command", $"Unknown code command '{args[1]}'.");
        if (!values.TryGetValue("--id", out var id) || string.IsNullOrWhiteSpace(id))
            return Failure("missing_symbol_id", $"code {operation} requires --id.");
        return new(new CodeRelationsInvocation(operation, id, solution), null);
    }

    private static CodeParseResult Failure(string code, string message) =>
        new(null, new LabError(code, message));
}
