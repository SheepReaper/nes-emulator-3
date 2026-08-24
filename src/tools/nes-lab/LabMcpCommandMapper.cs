using System.Globalization;
using System.Text.Json;

namespace Sheep.Nes.Lab;

public static class LabMcpCommandMapper
{
    public static IReadOnlyList<string> Map(
        string capability, string operation, JsonElement arguments, string? repositoryRoot = null)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("MCP arguments must be a JSON object.", nameof(arguments));
        return (capability.ToLowerInvariant(), operation.ToLowerInvariant()) switch
        {
            ("context", "build") => Context(arguments),
            ("code", "find") => CodeFind(arguments),
            ("code", "index") => ["code", "index"],
            ("code", "refs" or "callers" or "tests") =>
                ["code", operation.ToLowerInvariant(), "--id", Required(arguments, "symbolId")],
            ("memory", "search") => Memory(arguments),
            ("memory", "show") => ["memory", "show", "--id", Integer(arguments, "id", 0)],
            ("memory", "stale") => ["memory", "stale"],
            ("memory", "validate") => ["memory", "validate"],
            ("memory", "proposals-list") => ["memory", "proposals", "list"],
            ("memory", "proposals-show" or "proposals-accept" or "proposals-reject") =>
                ["memory", "proposals", operation[10..], Integer(arguments, "id", 0)],
            ("feedback", "show" or "metrics") => ["feedback", operation],
            ("feedback", "search") => ["feedback", "search", Required(arguments, "query")],
            ("feedback", "record") => Feedback(arguments),
            ("references", "status" or "sync") => ["references", operation],
            ("references", "search") => ["references", "search", Required(arguments, "query")],
            ("references", "show") => ["references", "show", Required(arguments, "id")],
            ("experiment", "run") => ["experiment", "run", "--scenario",
                Confined(Required(arguments, "scenarioPath"), repositoryRoot), "--mcp-safe"],
            ("experiment", "run-inline") => ["experiment", "run", "--inline",
                RequiredJson(arguments, "scenario"), "--mcp-safe"],
            ("experiment", "compare") => ["experiment", "compare", "--left",
                Required(arguments, "leftUri"), "--right", Required(arguments, "rightUri")],
            ("media", "frame-compare") => MediaFrameCompare(arguments),
            ("media", "audio-analyze") => MediaAudioAnalyze(arguments),
            ("media", "audio-compare") => MediaAudioCompare(arguments),
            ("investigate", "task") => Investigate(arguments, "task"),
            ("investigate", "run") => Investigate(arguments, "runId"),
            ("session", "close") => SessionClose(arguments),
            ("session", "show") => ["session", "show", "--uri", Required(arguments, "uri")],
            ("host", "diagnostics-import") => ["host", "diagnostics", "import", "--json", RequiredJson(arguments, "bundle")],
            ("host", "diagnostics-show") => ["host", "diagnostics", "show", "--uri", Required(arguments, "uri")],
            ("host", "diagnostics-compare") => ["host", "diagnostics", "compare", "--left",
                Required(arguments, "leftUri"), "--right", Required(arguments, "rightUri")],
            ("build", "diagnose") => ["build", "diagnose", "--run", Optional(arguments, "runId") ?? "latest"],
            ("rom", "list" or "show" or "source" or "diagnose") => Rom(operation, arguments),
            ("trace", "query") => TraceQuery(arguments, repositoryRoot),
            ("trace", "diff") => TraceDiff(arguments, repositoryRoot),
            ("trace", "capture") => ["trace", "capture", "--case", Required(arguments, "caseName"),
                "--timeout-seconds", Integer(arguments, "timeoutSeconds", 30)],
            ("verify", "run") => Verify(arguments),
            ("baseline", "show") => ["baseline", "show"],
            ("baseline", "update") => BaselineUpdate(arguments),
            ("history", "latest") => HistoryLatest(arguments),
            ("history", "search") => ["failures", "search", "--query", Required(arguments, "query"),
                "--max", Integer(arguments, "maximumResults", 32)],
            ("history", "metrics") => ["metrics"],
            ("diagnose", "run") => ["diagnose", "--case", Required(arguments, "caseName"),
                "--budget", Integer(arguments, "budgetBytes", 16_000), "--persist"],
            ("diagnose", "inspect") => ["diagnose", "--run", Optional(arguments, "runId") ?? "latest",
                "--budget", Integer(arguments, "budgetBytes", 16_000)],
            ("artifacts", "list") => ["artifacts", "list"],
            ("artifacts", "describe") => ["artifacts", "describe", "--uri", Required(arguments, "uri")],
            ("artifacts", "text") => ["artifacts", "text", "--uri", Required(arguments, "uri"),
                "--start-line", Integer(arguments, "startLine", 1), "--max-lines", Integer(arguments, "maximumLines", 200)],
            ("artifacts", "pin" or "unpin") => ["artifacts", operation.ToLowerInvariant(),
                "--uri", Required(arguments, "uri")],
            ("artifacts", "prune") => ["artifacts", "prune", "--older-than",
                Optional(arguments, "olderThan") ?? "30d"],
            _ => throw new KeyNotFoundException(
                $"Unknown nes-lab operation '{capability}.{operation}'. Call nes_lab_discover first.")
        };
    }

    private static IReadOnlyList<string> Context(JsonElement value)
    {
        List<string> result = ["context", "build"];
        var id = Optional(value, "symbolId");
        if (id is not null) Add(result, "--id", id);
        else if (Optional(value, "symbol") is { } symbol) Add(result, "--symbol", symbol);
        else if (Boolean(value, "changed")) result.Add("--changed");
        else if (Optional(value, "subsystem") is { } subsystem) Add(result, "--subsystem", subsystem);
        else if (Optional(value, "task") is { } task) Add(result, "--task", task);
        else if (Optional(value, "runId") is { } runId) Add(result, "--run", runId);
        else if (Optional(value, "handoffUri") is { } handoffUri) Add(result, "--handoff", handoffUri);
        else throw new ArgumentException("Context build requires a primary selector.");
        Add(result, "--base", Optional(value, "baseRevision"));
        Add(result, "--qualified", Optional(value, "exactQualifiedName"));
        Add(result, "--kind", Optional(value, "kind"));
        Add(result, "--project", Optional(value, "project"));
        Add(result, "--namespace", Optional(value, "namespace"));
        Add(result, "--file", Optional(value, "filePath"));
        Add(result, "--budget", Integer(value, "budgetBytes", 12_000));
        if (Optional(value, "synthesize") is { } synthesize) Add(result, "--synthesize", synthesize);
        return result;
    }

    private static IReadOnlyList<string> CodeFind(JsonElement value)
    {
        List<string> result = ["code", "find", "--symbol", Required(value, "symbol")];
        Add(result, "--max", OptionalInteger(value, "maximumResults"));
        Add(result, "--qualified", Optional(value, "exactQualifiedName"));
        Add(result, "--kind", Optional(value, "kind"));
        Add(result, "--project", Optional(value, "project"));
        Add(result, "--namespace", Optional(value, "namespace"));
        Add(result, "--file", Optional(value, "filePath"));
        return result;
    }

    private static IReadOnlyList<string> Memory(JsonElement value)
    {
        List<string> result = ["memory", "search", "--query", Required(value, "query")];
        Add(result, "--kind", Optional(value, "kind"));
        Add(result, "--max", OptionalInteger(value, "maximumResults"));
        return result;
    }

    private static IReadOnlyList<string> Feedback(JsonElement value)
    {
        List<string> result = ["feedback", "record", "--packet", Required(value, "packetId")];
        Add(result, "--useful", StringArray(value, "usefulEvidenceIds"));
        Add(result, "--not-useful", StringArray(value, "notUsefulEvidenceIds"));
        Add(result, "--outcome", Optional(value, "outcome"));
        Add(result, "--run", Optional(value, "runId"));
        Add(result, "--model", Optional(value, "model"));
        Add(result, "--provider", Optional(value, "provider"));
        Add(result, "--cloud-input-tokens", OptionalInteger(value, "cloudInputTokens"));
        Add(result, "--cloud-output-tokens", OptionalInteger(value, "cloudOutputTokens"));
        Add(result, "--iterations", OptionalInteger(value, "diagnosticIterations"));
        Add(result, "--elapsed-ms", OptionalNumber(value, "elapsedMilliseconds"));
        Add(result, "--verification", Optional(value, "verificationResult"));
        Add(result, "--accepted-proposals", NumberArray(value, "acceptedProposalIds"));
        Add(result, "--accepted-fixes", NumberArray(value, "acceptedFixIds"));
        Add(result, "--tokens-avoided", OptionalInteger(value, "tokensAvoided"));
        if (value.TryGetProperty("telemetry", out var telemetry) && telemetry.ValueKind == JsonValueKind.Object)
        { result.Add("--telemetry"); result.Add(telemetry.GetRawText()); }
        return result;
    }

    private static IReadOnlyList<string> MediaFrameCompare(JsonElement value)
    {
        List<string> result = ["media", "frame", "compare", "--left", Required(value, "leftUri"),
            "--right", Required(value, "rightUri")];
        Flag(result, value, "heatmap", "--heatmap");
        return result;
    }

    private static IReadOnlyList<string> MediaAudioAnalyze(JsonElement value)
    {
        List<string> result = ["media", "audio", "analyze", "--uri", Required(value, "uri")];
        Add(result, "--sample-rate", OptionalInteger(value, "sampleRate"));
        Add(result, "--window", OptionalInteger(value, "windowSize"));
        return result;
    }

    private static IReadOnlyList<string> MediaAudioCompare(JsonElement value)
    {
        List<string> result = ["media", "audio", "compare", "--left", Required(value, "leftUri"),
            "--right", Required(value, "rightUri")];
        Add(result, "--sample-rate", OptionalInteger(value, "sampleRate"));
        Add(result, "--sample-tolerance", OptionalNumber(value, "sampleTolerance"));
        Add(result, "--timing-tolerance", OptionalInteger(value, "timingToleranceSamples"));
        Add(result, "--rms-tolerance", OptionalNumber(value, "rmsTolerance"));
        return result;
    }

    private static IReadOnlyList<string> Investigate(JsonElement value, string selector)
    {
        List<string> result = ["investigate", selector == "task" ? "--task" : "--run",
            Required(value, selector), "--agent", "local", "--budget", Integer(value, "budgetBytes", 16_000),
            "--max-steps", Integer(value, "maximumSteps", 8)];
        Add(result, "--model", Optional(value, "model"));
        Add(result, "--endpoint", Optional(value, "endpoint"));
        return result;
    }

    private static IReadOnlyList<string> SessionClose(JsonElement value)
    {
        List<string> result = ["session", "close", "--task", Required(value, "task")];
        Add(result, "--run", Optional(value, "runId"));
        Add(result, "--packet", Optional(value, "packetUri"));
        Add(result, "--next", Optional(value, "recommendedNextCommand"));
        if (value.TryGetProperty("telemetry", out var telemetry) && telemetry.ValueKind == JsonValueKind.Object)
        { result.Add("--telemetry"); result.Add(telemetry.GetRawText()); }
        return result;
    }

    private static IReadOnlyList<string> Rom(string operation, JsonElement value)
    {
        List<string> result = ["rom", operation.ToLowerInvariant()];
        Add(result, "--suite", Optional(value, "suite"));
        if (operation is not "list") Add(result, "--name", Required(value, "name"));
        Add(result, "--symbol", Optional(value, "symbol"));
        Add(result, "--text", Optional(value, "text"));
        Add(result, "--code", OptionalInteger(value, "code"));
        Add(result, "--max", OptionalInteger(value, "maximumResults"));
        return result;
    }

    private static IReadOnlyList<string> TraceQuery(JsonElement value, string? repositoryRoot)
    {
        var uri = Optional(value, "artifactUri");
        var path = uri is null ? Confined(Required(value, "artifactPath"), repositoryRoot) : uri;
        List<string> result = ["trace", "query", uri is null ? "--artifact" : "--artifact-uri", path];
        Add(result, "--actor", Optional(value, "actor"));
        Add(result, "--address", Optional(value, "address"));
        Add(result, "--end", Optional(value, "endAddress"));
        Add(result, "--max", OptionalInteger(value, "maximumResults"));
        Flag(result, value, "interruptEdges", "--interrupt-edges");
        Flag(result, value, "dmaOverlap", "--dma-overlap");
        Flag(result, value, "instructionBoundaries", "--instruction-boundaries");
        return result;
    }

    private static IReadOnlyList<string> TraceDiff(JsonElement value, string? repositoryRoot) =>
        ["trace", "diff", "--expected", Confined(Required(value, "expectedArtifactPath"), repositoryRoot),
            "--actual", Confined(Required(value, "actualArtifactPath"), repositoryRoot),
            "--context", Integer(value, "contextRecords", 3)];

    private static string Confined(string path, string? repositoryRoot) => repositoryRoot is null
        ? path : new McpPathPolicy(repositoryRoot).ResolveInspectionPath(path);

    private static IReadOnlyList<string> Verify(JsonElement value)
    {
        List<string> result = ["verify"];
        if (Boolean(value, "changed")) result.Add("--changed");
        else
        {
            result.Add("--scope");
            result.Add(Optional(value, "scope") ?? "all");
        }
        Add(result, "--case", Optional(value, "caseName"));
        Flag(result, value, "continueOnFailure", "--continue-on-failure");
        Flag(result, value, "traceOnFailure", "--trace-on-failure");
        Flag(result, value, "traceAlways", "--trace-always");
        Flag(result, value, "planOnly", "--plan-only");
        Flag(result, value, "baselineAwareExitCode", "--baseline-aware-exit-code");
        return result;
    }

    private static IReadOnlyList<string> BaselineUpdate(JsonElement value)
    {
        List<string> result = ["baseline", "update", "--run", Optional(value, "runId") ?? "latest"];
        Flag(result, value, "apply", "--apply");
        return result;
    }

    private static IReadOnlyList<string> HistoryLatest(JsonElement value)
    {
        List<string> result = [Boolean(value, "failuresOnly") ? "failures" : "runs", "latest"];
        Add(result, "--scope", Optional(value, "scope"));
        Add(result, "--case", Optional(value, "caseName"));
        return result;
    }

    private static string Required(JsonElement value, string name) =>
        Optional(value, name) is { Length: > 0 } result ? result :
        throw new ArgumentException($"MCP operation requires '{name}'.", name);

    private static string RequiredJson(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Object
            ? property.GetRawText() : throw new ArgumentException($"MCP operation requires object '{name}'.", name);

    private static string? Optional(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() : null;

    private static string? OptionalInteger(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.TryGetInt32(out var number)
            ? number.ToString(CultureInfo.InvariantCulture) : null;

    private static string? OptionalNumber(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.TryGetDouble(out var number)
            ? number.ToString(CultureInfo.InvariantCulture) : null;

    private static string Integer(JsonElement value, string name, int fallback) =>
        OptionalInteger(value, name) ?? fallback.ToString(CultureInfo.InvariantCulture);

    private static bool Boolean(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;

    private static string? StringArray(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Array
            ? string.Join(',', property.EnumerateArray().Select(item => item.GetString())) : null;
    private static string? NumberArray(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Array
            ? string.Join(',', property.EnumerateArray().Select(item => item.GetInt64().ToString(CultureInfo.InvariantCulture))) : null;

    private static void Add(List<string> arguments, string option, string? value)
    {
        if (value is null) return;
        arguments.Add(option);
        arguments.Add(value);
    }

    private static void Flag(List<string> arguments, JsonElement value, string property, string option)
    {
        if (Boolean(value, property)) arguments.Add(option);
    }
}
