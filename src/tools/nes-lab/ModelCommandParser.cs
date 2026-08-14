namespace Sheep.Nes.Lab;

public interface IModelInvocation;
public sealed record ModelBenchmarkInvocation(string Model, string Endpoint, string FixturePath) : IModelInvocation;
public sealed record ModelRankInvocation(
    string Model, string Endpoint, string InputPath, int MaximumResults, bool UseLocalModel) : IModelInvocation;
public sealed record ModelParseResult(IModelInvocation? Invocation, LabError? Error);

public static class ModelCommandParser
{
    public static ModelParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count < 2 || !args[0].Equals("model", StringComparison.OrdinalIgnoreCase) ||
            args[1] is not ("benchmark" or "rank"))
            return Failure("invalid_command", "Expected 'model benchmark' or 'model rank'.");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 2; index < args.Count; index++)
        {
            var option = args[index];
            if (!option.StartsWith("--", StringComparison.Ordinal) || ++index >= args.Count)
                return Failure("missing_value", $"{option} requires a value.");
            values[option] = args[index];
        }
        var model = values.GetValueOrDefault("--model") ?? "nes-lab:devstral-24b";
        var endpoint = values.GetValueOrDefault("--endpoint") ?? "http://localhost:11434/";
        if (args[1] == "benchmark")
            return new(new ModelBenchmarkInvocation(model, endpoint,
                values.GetValueOrDefault("--fixture") ??
                Path.Combine("src", "tools", "nes-lab", "ollama", "benchmark.json")), null);

        if (!values.TryGetValue("--input", out var inputPath))
            return Failure("missing_option", "model rank requires --input <path>.");
        if (!int.TryParse(values.GetValueOrDefault("--max") ?? "8", out var maximumResults) || maximumResults <= 0)
            return Failure("invalid_value", "--max must be a positive integer.");
        var useLocalModel = bool.TryParse(values.GetValueOrDefault("--local") ?? "false", out var local) && local;
        return new(new ModelRankInvocation(model, endpoint, inputPath, maximumResults, useLocalModel), null);
    }

    private static ModelParseResult Failure(string code, string message) => new(null, new LabError(code, message));
}
