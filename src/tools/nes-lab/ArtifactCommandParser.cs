namespace Sheep.Nes.Lab;

public sealed record ArtifactInvocation(
    string Operation, string? Kind = null, string? Digest = null, TimeSpan? OlderThan = null,
    int StartLine = 1, int MaximumLines = 200);
public sealed record ArtifactParseResult(ArtifactInvocation? Invocation, LabError? Error);

public static class ArtifactCommandParser
{
    public static ArtifactParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count < 2 || args[0] != "artifacts") return Failure("invalid_command", "Expected artifacts pin, unpin, or prune.");
        var operation = args[1].ToLowerInvariant();
        if (operation == "list" && args.Count == 2)
            return new(new ArtifactInvocation(operation), null);
        if (operation is "describe" or "text")
        {
            string? uri = null; var start = 1; var maximum = 200;
            for (var index = 2; index < args.Count; index += 2)
            {
                if (index + 1 >= args.Count) return Failure("missing_value", $"{args[index]} requires a value.");
                if (args[index] == "--uri") uri = args[index + 1];
                else if (args[index] == "--start-line" && int.TryParse(args[index + 1], out var parsedStart) && parsedStart > 0) start = parsedStart;
                else if (args[index] == "--max-lines" && int.TryParse(args[index + 1], out var parsedMax) && parsedMax is > 0 and <= 1000) maximum = parsedMax;
                else return Failure("invalid_option", $"Invalid {args[index]} value.");
            }
            if (uri is null || !ImmutableArtifactStore.TryParseUri(uri, out var kind, out var digest))
                return Failure("invalid_uri", $"artifacts {operation} requires an immutable --uri.");
            return new(new ArtifactInvocation(operation, kind, digest, StartLine: start, MaximumLines: maximum), null);
        }
        if (operation is "pin" or "unpin")
        {
            if (args.Count != 4 || args[2] != "--uri" ||
                !ImmutableArtifactStore.TryParseUri(args[3], out var kind, out var digest))
                return Failure("invalid_uri", "pin and unpin require an immutable --uri.");
            return new(new ArtifactInvocation(operation, kind, digest), null);
        }
        if (operation == "prune")
        {
            var age = TimeSpan.FromDays(30);
            if (args.Count == 4 && args[2] == "--older-than" && TryAge(args[3], out var parsed)) age = parsed;
            else if (args.Count != 2) return Failure("invalid_age", "Use --older-than <Nd|Nh>.");
            return new(new ArtifactInvocation(operation, OlderThan: age), null);
        }
        return Failure("invalid_operation", $"Unknown artifact operation '{operation}'.");
    }

    private static bool TryAge(string value, out TimeSpan age)
    {
        age = default;
        if (value.Length < 2 || !double.TryParse(value[..^1], out var number) || number <= 0) return false;
        age = char.ToLowerInvariant(value[^1]) switch
        {
            'd' => TimeSpan.FromDays(number),
            'h' => TimeSpan.FromHours(number),
            _ => default
        };
        return age > TimeSpan.Zero;
    }

    private static ArtifactParseResult Failure(string code, string message) => new(null, new LabError(code, message));
}
