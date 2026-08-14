using System.Globalization;

namespace Sheep.Nes.Lab;

public abstract record TraceInvocation;
public sealed record TraceQueryInvocation(string ArtifactPath, TraceQuery Query) : TraceInvocation;
public sealed record TraceDiffInvocation(
    string ExpectedArtifactPath, string ActualArtifactPath, int ContextRecords) : TraceInvocation;
public sealed record TraceCaptureInvocation(string CaseName, int TimeoutSeconds = 30) : TraceInvocation;
public sealed record TraceParseResult(TraceInvocation? Invocation, LabError? Error);

public static class TraceCommandParser
{
    public static TraceParseResult Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2 || !arguments[0].Equals("trace", StringComparison.OrdinalIgnoreCase))
            return Failure("invalid_command", "Expected 'trace query' or 'trace diff'.");
        return arguments[1].ToLowerInvariant() switch
        {
            "query" => ParseQuery(arguments),
            "diff" => ParseDiff(arguments),
            "capture" => ParseCapture(arguments),
            _ => Failure("invalid_trace_command", $"Unknown trace command '{arguments[1]}'.")
        };
    }

    private static TraceParseResult ParseCapture(IReadOnlyList<string> args)
    {
        string? caseName = null;
        var timeoutSeconds = 30;
        for (var i = 2; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--case": if (!Value(args, ref i, out caseName)) return Missing(args[i - 1]); break;
                case "--timeout-seconds":
                    if (!Integer(args, ref i, out timeoutSeconds)) return InvalidNumber(args[i - 1]);
                    break;
                default: return Failure("invalid_option", $"Unknown option '{args[i]}'.");
            }
        }
        if (string.IsNullOrWhiteSpace(caseName)) return Failure("missing_case", "--case is required.");
        if (timeoutSeconds is < 1 or > 300)
            return Failure("invalid_timeout", "--timeout-seconds must be between 1 and 300.");
        return new(new TraceCaptureInvocation(caseName, timeoutSeconds), null);
    }

    private static TraceParseResult ParseQuery(IReadOnlyList<string> args)
    {
        string? path = null, actor = null;
        ushort? start = null, end = null;
        var edges = false; var overlap = false; var boundaries = false; var maximum = 64;
        for (var i = 2; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--artifact" or "--artifact-uri": if (!Value(args, ref i, out path)) return Missing(args[i - 1]); break;
                case "--actor": if (!Value(args, ref i, out actor)) return Missing(args[i - 1]); break;
                case "--address": if (!Number(args, ref i, out start)) return InvalidNumber(args[i - 1]); break;
                case "--end": if (!Number(args, ref i, out end)) return InvalidNumber(args[i - 1]); break;
                case "--max": if (!Integer(args, ref i, out maximum)) return InvalidNumber(args[i - 1]); break;
                case "--interrupt-edges": edges = true; break;
                case "--dma-overlap": overlap = true; break;
                case "--instruction-boundaries": boundaries = true; break;
                default: return Failure("invalid_option", $"Unknown option '{args[i]}'.");
            }
        }
        if (string.IsNullOrWhiteSpace(path)) return Failure("missing_artifact", "--artifact or --artifact-uri is required.");
        return new TraceParseResult(new TraceQueryInvocation(path,
            new TraceQuery(start, end, actor, edges, overlap, boundaries, maximum)), null);
    }

    private static TraceParseResult ParseDiff(IReadOnlyList<string> args)
    {
        string? expected = null, actual = null; var context = 3;
        for (var i = 2; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--expected": if (!Value(args, ref i, out expected)) return Missing(args[i - 1]); break;
                case "--actual": if (!Value(args, ref i, out actual)) return Missing(args[i - 1]); break;
                case "--context": if (!Integer(args, ref i, out context)) return InvalidNumber(args[i - 1]); break;
                default: return Failure("invalid_option", $"Unknown option '{args[i]}'.");
            }
        }
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
            return Failure("missing_artifact", "--expected and --actual are required.");
        return new TraceParseResult(new TraceDiffInvocation(expected, actual, context), null);
    }

    private static bool Value(IReadOnlyList<string> args, ref int i, out string? value)
    { value = ++i < args.Count ? args[i] : null; return value is not null; }
    private static bool Integer(IReadOnlyList<string> args, ref int i, out int value)
    {
        value = 0;
        return ++i < args.Count &&
            int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
    private static bool Number(IReadOnlyList<string> args, ref int i, out ushort? value)
    {
        value = null;
        if (++i >= args.Count) return false;
        var text = args[i];
        var hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || text.StartsWith('$');
        if (hex) text = text.StartsWith('$') ? text[1..] : text[2..];
        if (!ushort.TryParse(text, hex ? NumberStyles.HexNumber : NumberStyles.None,
                CultureInfo.InvariantCulture, out var parsed)) return false;
        value = parsed; return true;
    }
    private static TraceParseResult Missing(string option) => Failure("missing_value", $"{option} requires a value.");
    private static TraceParseResult InvalidNumber(string option) => Failure("invalid_number", $"{option} requires a valid number.");
    private static TraceParseResult Failure(string code, string message) => new(null, new LabError(code, message));
}
