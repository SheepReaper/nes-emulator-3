namespace Sheep.Nes.Lab;

public abstract record MemoryInvocation(string? DatabasePath);
public sealed record MemoryAddInvocation(
    EngineeringMemoryKind Kind,
    string Title,
    string Body,
    string SourceKind,
    string Source,
    string SourceHash,
    int? LineNumber,
    string? Commit,
    string? DatabasePath = null) : MemoryInvocation(DatabasePath);
public sealed record MemorySearchInvocation(
    string Query,
    EngineeringMemoryKind? Kind,
    int MaximumResults,
    bool IncludeRejectedHypotheses,
    string? DatabasePath = null) : MemoryInvocation(DatabasePath);
public sealed record MemoryShowInvocation(long Id, string? DatabasePath = null) : MemoryInvocation(DatabasePath);
public sealed record MemorySupersedeInvocation(long Id, MemoryAddInvocation Replacement,
    string? DatabasePath = null) : MemoryInvocation(DatabasePath);
public sealed record MemoryValidateInvocation(string RepositoryRoot,
    string? DatabasePath = null) : MemoryInvocation(DatabasePath);
public sealed record MemoryStaleInvocation(string? DatabasePath = null) : MemoryInvocation(DatabasePath);
public sealed record MemoryTransferInvocation(string Operation, string Path,
    string? DatabasePath = null) : MemoryInvocation(DatabasePath);
public sealed record MemoryParseResult(MemoryInvocation? Invocation, LabError? Error);

public static class MemoryCommandParser
{
    public static MemoryParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count < 2 || !args[0].Equals("memory", StringComparison.OrdinalIgnoreCase))
            return Failure("invalid_command", "Expected 'memory add' or 'memory search'.");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var includeRejected = false;
        for (var index = 2; index < args.Count; index++)
        {
            if (args[index] == "--include-rejected")
            {
                includeRejected = true;
                continue;
            }
            var option = args[index];
            if (!option.StartsWith("--", StringComparison.Ordinal) || ++index >= args.Count)
                return Failure("missing_value", $"{option} requires a value.");
            values[option] = args[index];
        }

        return args[1].ToLowerInvariant() switch
        {
            "add" => ParseAdd(values),
            "search" => ParseSearch(values, includeRejected),
            "show" => ParseId(values, id => new MemoryShowInvocation(id, values.GetValueOrDefault("--database"))),
            "supersede" => ParseSupersede(values),
            "validate" => new(new MemoryValidateInvocation(
                values.GetValueOrDefault("--root") ?? Directory.GetCurrentDirectory(),
                values.GetValueOrDefault("--database")), null),
            "stale" => new(new MemoryStaleInvocation(values.GetValueOrDefault("--database")), null),
            "export" or "import" when Required(values, "--path", out var path) =>
                new(new MemoryTransferInvocation(args[1].ToLowerInvariant(), path,
                    values.GetValueOrDefault("--database")), null),
            _ => Failure("invalid_memory_command", $"Unknown memory command '{args[1]}'.")
        };
    }

    private static MemoryParseResult ParseSupersede(IReadOnlyDictionary<string, string> values)
    {
        var replacement = ParseAdd(values);
        if (replacement.Invocation is not MemoryAddInvocation add) return replacement;
        return ParseId(values, id => new MemorySupersedeInvocation(id, add,
            values.GetValueOrDefault("--database")));
    }

    private static MemoryParseResult ParseId(
        IReadOnlyDictionary<string, string> values, Func<long, MemoryInvocation> factory) =>
        values.TryGetValue("--id", out var text) && long.TryParse(text, out var id) && id > 0
            ? new MemoryParseResult(factory(id), null)
            : Failure("invalid_memory_id", "A positive --id is required.");

    private static MemoryParseResult ParseAdd(IReadOnlyDictionary<string, string> values)
    {
        if (!TryKind(values.GetValueOrDefault("--kind"), out var kind))
            return Failure("invalid_memory_kind", "memory add requires a valid --kind.");
        if (!Required(values, "--title", out var title) || !Required(values, "--body", out var body))
            return Failure("missing_content", "memory add requires --title and --body.");
        if (!Required(values, "--source", out var source) ||
            !Required(values, "--source-hash", out var sourceHash))
            return Failure("missing_provenance", "memory add requires --source and --source-hash.");
        int? line = values.TryGetValue("--line", out var lineText) && int.TryParse(lineText, out var parsedLine)
            ? parsedLine : null;
        return new MemoryParseResult(new MemoryAddInvocation(
            kind, title, body, values.GetValueOrDefault("--source-kind") ?? "source",
            source, sourceHash, line, values.GetValueOrDefault("--commit"),
            values.GetValueOrDefault("--database")), null);
    }

    private static MemoryParseResult ParseSearch(
        IReadOnlyDictionary<string, string> values,
        bool includeRejected)
    {
        if (!Required(values, "--query", out var query))
            return Failure("missing_query", "memory search requires --query.");
        EngineeringMemoryKind? kind = null;
        if (values.TryGetValue("--kind", out var kindText))
        {
            if (!TryKind(kindText, out var parsedKind))
                return Failure("invalid_memory_kind", $"Unknown memory kind '{kindText}'.");
            kind = parsedKind;
        }
        var maximum = values.TryGetValue("--max", out var maxText) && int.TryParse(maxText, out var parsedMax)
            ? parsedMax : 32;
        return new MemoryParseResult(new MemorySearchInvocation(
            query, kind, maximum, includeRejected, values.GetValueOrDefault("--database")), null);
    }

    private static bool Required(
        IReadOnlyDictionary<string, string> values, string option, out string value) =>
        values.TryGetValue(option, out value!) && !string.IsNullOrWhiteSpace(value);

    private static bool TryKind(string? text, out EngineeringMemoryKind kind)
    {
        var normalized = text?.Replace("-", "", StringComparison.Ordinal);
        return Enum.TryParse(normalized, true, out kind);
    }

    private static MemoryParseResult Failure(string code, string message) =>
        new(null, new LabError(code, message));
}
