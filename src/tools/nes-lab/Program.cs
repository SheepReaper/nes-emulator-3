using System.Text.Json;

using Sheep.Nes.Lab;
if (args.Length == 0)
{
    Console.WriteLine(LabHelp.For([]));
    return 0;
}

if (args[0] is "--help" or "-h" or "help")
{
    Console.WriteLine(LabHelp.For(args.Skip(1).ToArray()));
    return 0;
}

var helpIndex = Array.FindIndex(args, 1, argument => argument is "--help" or "-h");
if (helpIndex >= 0)
{
    Console.WriteLine(LabHelp.For(args.Take(helpIndex).ToArray()));
    return 0;
}

if (args.Length > 0 && args[0].Equals("mcp", StringComparison.OrdinalIgnoreCase))
{
    await NesLabMcpServer.RunAsync();
    return 0;
}

var options = LabResponseSerializer.Options;

if (args.Length > 0 && args[0].Equals("baseline", StringComparison.OrdinalIgnoreCase))
{
    var baselineParsed = BaselineCommandParser.Parse(args);
    if (baselineParsed.Error is not null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = false,
            error = baselineParsed.Error }, options));
        return 2;
    }
    try
    {
        var root = Directory.GetCurrentDirectory();
        var baselinePath = Path.Combine(root, "src", "tools", "nes-lab", "accepted-conformance-baseline.v1.json");
        var baseline = ConformanceBaselineComparer.Load(baselinePath);
        if (baselineParsed.Operation == "show")
        {
            Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true,
                operation = "baseline-show", result = baseline }, options));
            return 0;
        }

        var historyPath = Path.Combine(root, ".artifacts", "nes-lab", "index.sqlite");
        if (!File.Exists(historyPath)) throw new KeyNotFoundException("NES Lab run history is unavailable.");
        using var history = new RunHistoryStore(historyPath, root);
        var requestedRun = baselineParsed.Invocation!;
        var run = requestedRun.RunId.Equals("latest", StringComparison.OrdinalIgnoreCase)
            ? history.LatestFull(VerificationScope.Conformance) ??
                throw new KeyNotFoundException("No complete conformance run exists in NES Lab history.")
            : history.Get(requestedRun.RunId);
        var current = RepositoryProvenance.Capture(root);
        if (!string.Equals(run.SourceRevision, current.Head, StringComparison.Ordinal) ||
            !string.Equals(run.WorkingTreeDigest, current.WorkingTreeDigest, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Run '{run.Id}' is stale for the current repository state. Run a complete conformance verification first.");
        if (!File.Exists(run.ArtifactPath))
            throw new FileNotFoundException("The selected run log is no longer available.", run.ArtifactPath);
        var proposal = ConformanceBaselineManager.Propose(baseline, run, File.ReadAllText(run.ArtifactPath));
        if (requestedRun.Apply) proposal = ConformanceBaselineManager.Apply(proposal, baselinePath);
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true,
            operation = "baseline-update", result = proposal }, options));
        return 0;
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = false,
            error = new LabError("baseline_update_rejected", exception.Message) }, options));
        return 3;
    }
}

var historyArgs = args.Length > 0 && args[0].Equals("history", StringComparison.OrdinalIgnoreCase)
    ? args.Skip(1).ToArray() : args;

if (args.Length >= 2 && args[0].Equals("telemetry", StringComparison.OrdinalIgnoreCase) &&
    args[1].Equals("normalize", StringComparison.OrdinalIgnoreCase))
{
    var jsonIndex = Array.IndexOf(args, "--json"); var pathIndex = Array.IndexOf(args, "--path");
    var telemetryJson = jsonIndex >= 0 && jsonIndex + 1 < args.Length ? args[jsonIndex + 1]
        : pathIndex >= 0 && pathIndex + 1 < args.Length ? File.ReadAllText(args[pathIndex + 1])
        : await Console.In.ReadToEndAsync();
    var telemetry = HostUsageTelemetry.Parse(telemetryJson);
    Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true,
        operation = "telemetry-normalize", result = telemetry }, options));
    return 0;
}

if (args.Length >= 3 && args[0].Equals("host", StringComparison.OrdinalIgnoreCase) &&
    args[1].Equals("diagnostics", StringComparison.OrdinalIgnoreCase))
{
    static string? HostValue(string[] values, string option)
    { var index = Array.IndexOf(values, option); return index >= 0 && index + 1 < values.Length ? values[index + 1] : null; }
    var host = new HostDiagnosticsService(Directory.GetCurrentDirectory());
    object hostResult = args[2].ToLowerInvariant() switch
    {
        "import" => await host.ImportAsync(HostValue(args, "--json") ??
            File.ReadAllText(HostValue(args, "--path") ?? throw new ArgumentException("host diagnostics import requires --json or --path."))),
        "show" => await host.ShowAsync(HostValue(args, "--uri") ?? throw new ArgumentException("host diagnostics show requires --uri.")),
        "compare" => await host.CompareAsync(
            HostValue(args, "--left") ?? throw new ArgumentException("host diagnostics compare requires --left."),
            HostValue(args, "--right") ?? throw new ArgumentException("host diagnostics compare requires --right.")),
        _ => throw new ArgumentException($"Unknown host diagnostics operation '{args[2]}'.")
    };
    Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true,
        operation = "host-diagnostics-" + args[2], result = hostResult }, options));
    return 0;
}

if (args.Length >= 2 && args[0].Equals("build", StringComparison.OrdinalIgnoreCase) &&
    args[1].Equals("diagnose", StringComparison.OrdinalIgnoreCase))
{
    var runIndex = Array.IndexOf(args, "--run");
    var runId = runIndex >= 0 && runIndex + 1 < args.Length ? args[runIndex + 1] : "latest";
    var root = Directory.GetCurrentDirectory();
    var historyPath = Path.Combine(root, ".artifacts", "nes-lab", "index.sqlite");
    if (!File.Exists(historyPath)) throw new KeyNotFoundException("NES Lab run history is unavailable.");
    using var history = new RunHistoryStore(historyPath, root);
    var run = runId.Equals("latest", StringComparison.OrdinalIgnoreCase)
        ? history.Latest(failuresOnly: true) ?? throw new KeyNotFoundException("No failed run exists.")
        : history.Get(runId);
    var report = BuildDiagnosticParser.Parse(File.ReadAllText(run.ArtifactPath));
    Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true,
        operation = "build-diagnose", runId = run.Id, run.ResourceUri, run.LogResourceUri,
        report, reproductionCommand = $"nes-lab verify --scope {run.Scope.ToString().ToLowerInvariant()}" }, options));
    return 0;
}

if (args.Length >= 2 && args[0].Equals("session", StringComparison.OrdinalIgnoreCase))
{
    static string? SessionValue(string[] values, string option)
    { var index = Array.IndexOf(values, option); return index >= 0 && index + 1 < values.Length ? values[index + 1] : null; }
    var service = new SessionHandoffService(Directory.GetCurrentDirectory());
    object sessionResult = args[1].ToLowerInvariant() switch
    {
        "close" => await service.CloseAsync(SessionValue(args, "--task") ?? "NES Lab development session",
            SessionValue(args, "--run") ?? "latest", SessionValue(args, "--packet"),
            SessionValue(args, "--next") ?? "nes-lab verify --changed",
            SessionValue(args, "--telemetry") is { } telemetry ? HostUsageTelemetry.ParseArgument(telemetry) : null),
        "show" => await service.ShowAsync(SessionValue(args, "--uri") ??
            throw new ArgumentException("session show requires --uri.")),
        _ => throw new ArgumentException($"Unknown session operation '{args[1]}'.")
    };
    Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true,
        operation = "session-" + args[1], result = sessionResult }, options));
    return 0;
}

if (args.Length > 0 && args[0].Equals("investigate", StringComparison.OrdinalIgnoreCase))
{
    static string? InvestigationValue(string[] values, string option)
    { var index = Array.IndexOf(values, option); return index >= 0 && index + 1 < values.Length ? values[index + 1] : null; }
    var task = InvestigationValue(args, "--task");
    var runId = InvestigationValue(args, "--run");
    if ((task is null) == (runId is null)) throw new ArgumentException("Investigate requires exactly one of --task or --run.");
    if (!string.Equals(InvestigationValue(args, "--agent"), "local", StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("Investigate currently requires --agent local.");
    var budget = int.TryParse(InvestigationValue(args, "--budget"), out var parsedBudget) ? parsedBudget : 16_000;
    var maximumSteps = int.TryParse(InvestigationValue(args, "--max-steps"), out var parsedSteps) ? parsedSteps : 8;
    var root = Directory.GetCurrentDirectory();
    await using var symbolIndex = await RoslynSymbolIndex.OpenAsync(Path.Combine(root, "nes-emulator-3.slnx"));
    var investigationContext = new ContextBuildInvocation(null, null, budget, Path.Combine(root, "nes-emulator-3.slnx"),
        Task: task, RunId: runId);
    var initialEvidence = await GeneralContextEvidenceCollector.CollectAsync(symbolIndex, root, investigationContext);
    using var investigationHttp = new HttpClient { BaseAddress = new Uri(InvestigationValue(args, "--endpoint") ??
        "http://localhost:11434/"), Timeout = TimeSpan.FromMinutes(3) };
    var investigator = new LocalInvestigator(root,
        new OllamaInvestigationModel(investigationHttp, InvestigationValue(args, "--model") ?? "nes-lab:devstral-24b"),
        new LabInvestigationDispatcher(root));
    var investigation = await investigator.InvestigateAsync(task ?? $"Diagnose indexed run {runId}",
        initialEvidence, budget, maximumSteps);
    Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true,
        operation = "investigate", result = investigation }, options));
    return 0;
}

if (args.Length >= 3 && args[0].Equals("media", StringComparison.OrdinalIgnoreCase))
{
    static string MediaValue(string[] values, string option)
    {
        var index = Array.IndexOf(values, option);
        return index >= 0 && index + 1 < values.Length ? values[index + 1]
            : throw new ArgumentException($"{option} is required.");
    }
    static double MediaNumber(string[] values, string option, double fallback)
    {
        var index = Array.IndexOf(values, option);
        return index >= 0 && index + 1 < values.Length && double.TryParse(values[index + 1],
            System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : fallback;
    }
    var service = new MediaArtifactService(Directory.GetCurrentDirectory());
    object mediaResult = (args[1].ToLowerInvariant(), args[2].ToLowerInvariant()) switch
    {
        ("frame", "compare") => await service.CompareFramesAsync(MediaValue(args, "--left"),
            MediaValue(args, "--right"), args.Contains("--heatmap", StringComparer.OrdinalIgnoreCase)),
        ("audio", "analyze") => await service.AnalyzeAudioAsync(MediaValue(args, "--uri"),
            (int)MediaNumber(args, "--sample-rate", 48_000), (int)MediaNumber(args, "--window", 1024)),
        ("audio", "compare") => await service.CompareAudioAsync(MediaValue(args, "--left"),
            MediaValue(args, "--right"), new AudioComparisonTolerance(
                (float)MediaNumber(args, "--sample-tolerance", 0),
                (int)MediaNumber(args, "--timing-tolerance", 0), MediaNumber(args, "--rms-tolerance", 0)),
            (int)MediaNumber(args, "--sample-rate", 48_000)),
        _ => throw new ArgumentException($"Unknown media operation '{args[1]} {args[2]}'.")
    };
    Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true,
        operation = $"media-{args[1]}-{args[2]}", result = mediaResult }, options));
    return 0;
}

if (args.Length >= 2 && args[0].Equals("experiment", StringComparison.OrdinalIgnoreCase))
{
    static string RequiredValue(string[] values, string option)
    {
        var index = Array.IndexOf(values, option);
        return index >= 0 && index + 1 < values.Length ? values[index + 1]
            : throw new ArgumentException($"{option} is required.");
    }
    var service = new NesExperimentService(Directory.GetCurrentDirectory());
    object experimentResult = args[1].ToLowerInvariant() switch
    {
        "run" when args.Contains("--inline", StringComparer.OrdinalIgnoreCase) =>
            await service.RunInlineAsync(RequiredValue(args, "--inline"),
                args.Contains("--mcp-safe", StringComparer.OrdinalIgnoreCase)),
        "run" when args.Contains("--scenario-uri", StringComparer.OrdinalIgnoreCase) =>
            await service.RunArtifactAsync(RequiredValue(args, "--scenario-uri"),
                args.Contains("--mcp-safe", StringComparer.OrdinalIgnoreCase)),
        "run" => await service.RunAsync(RequiredValue(args, "--scenario"),
                args.Contains("--mcp-safe", StringComparer.OrdinalIgnoreCase)),
        "compare" => await service.CompareAsync(RequiredValue(args, "--left"), RequiredValue(args, "--right")),
        _ => throw new ArgumentException($"Unknown experiment operation '{args[1]}'.")
    };
    Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true,
        operation = "experiment-" + args[1], result = experimentResult }, options));
    return 0;
}

if (args.Length >= 2 && args[0].Equals("references", StringComparison.OrdinalIgnoreCase))
{
    var root = Directory.GetCurrentDirectory();
    var store = new ReferenceCorpusStore(Path.Combine(root, "src", "tools", "nes-lab", "reference-corpus.v1.json"),
        Path.Combine(root, ".artifacts", "nes-lab", "references"));
    object referenceResult;
    switch (args[1].ToLowerInvariant())
    {
        case "sync":
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Sheep-Nes-Lab/1.0 (local reference cache; contact: repository owner)");
                referenceResult = await store.SyncAsync(http);
            }
            break;
        case "status": referenceResult = store.Status(); break;
        case "show": referenceResult = store.Show(args.Length > 2 ? args[2] : throw new ArgumentException("references show requires an ID.")); break;
        case "search": referenceResult = store.Search(args.Length > 2 ? args[2] : throw new ArgumentException("references search requires a query."), 16); break;
        default: throw new ArgumentException($"Unknown references operation '{args[1]}'.");
    }
    Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true, operation = "references-" + args[1], result = referenceResult }, options));
    return 0;
}

if (args.Length >= 2 && args[0].Equals("feedback", StringComparison.OrdinalIgnoreCase))
{
    var database = Path.Combine(Directory.GetCurrentDirectory(), ".artifacts", "nes-lab", "index.sqlite");
    if (args[1] is "show" or "search" or "metrics" && !File.Exists(database))
    { Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true, result = Array.Empty<object>() }, options)); return 0; }
    using var learning = new GatewayLearningStore(database);
    object feedbackResult;
    if (args[1] == "record")
    {
        static string? Value(string[] values, string name) { var i = Array.IndexOf(values, name); return i >= 0 && i + 1 < values.Length ? values[i + 1] : null; }
        static string[] Ids(string? value) => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        static long? LongValue(string[] values, string name) => long.TryParse(Value(values, name), out var parsed) ? parsed : null;
        static int? IntValue(string[] values, string name) => int.TryParse(Value(values, name), out var parsed) ? parsed : null;
        static double? DoubleValue(string[] values, string name) => double.TryParse(Value(values, name), System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        static long[] LongIds(string? value) => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(long.Parse).ToArray() ?? [];
        var hostTelemetry = Value(args, "--telemetry") is { } reported ? HostUsageTelemetry.ParseArgument(reported) : null;
        var telemetry = new EvidenceOutcomeTelemetry(Value(args, "--model") ?? hostTelemetry?.Model,
            Value(args, "--provider") ?? hostTelemetry?.Provider,
            LongValue(args, "--cloud-input-tokens") ?? hostTelemetry?.CloudInputTokens,
            LongValue(args, "--cloud-output-tokens") ?? hostTelemetry?.CloudOutputTokens,
            IntValue(args, "--iterations"), DoubleValue(args, "--elapsed-ms"), Value(args, "--verification"),
            LongIds(Value(args, "--accepted-proposals")), LongIds(Value(args, "--accepted-fixes")),
            LongValue(args, "--tokens-avoided"), hostTelemetry?.CachedTokens, hostTelemetry?.GatewayCalls,
            hostTelemetry?.TimeToUsefulEvidenceMilliseconds, hostTelemetry?.TimeToDiagnosisMilliseconds,
            hostTelemetry?.TimeToPassingVerificationMilliseconds, hostTelemetry?.DirectSourceReads);
        feedbackResult = learning.Record(Value(args, "--packet") ?? throw new ArgumentException("--packet is required."),
            Ids(Value(args, "--useful")), Ids(Value(args, "--not-useful")), Value(args, "--outcome"), Value(args, "--run"), telemetry);
    }
    else if (args[1] == "show") feedbackResult = learning.AllFeedback();
    else if (args[1] == "search")
    { var query = args.Length > 2 ? args[2] : ""; feedbackResult = learning.AllFeedback().Where(item => JsonSerializer.Serialize(item).Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray(); }
    else if (args[1] == "metrics") feedbackResult = new { records = learning.AllFeedback().Count,
        evidenceWeights = learning.EvidenceWeights(), outcomes = learning.Metrics() };
    else throw new ArgumentException($"Unknown feedback operation '{args[1]}'.");
    Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true, operation = "feedback-" + args[1], result = feedbackResult }, options));
    return 0;
}

if (args.Length >= 3 && args[0].Equals("memory", StringComparison.OrdinalIgnoreCase) &&
    args[1].Equals("proposals", StringComparison.OrdinalIgnoreCase))
{
    var root = Directory.GetCurrentDirectory();
    var database = Path.Combine(root, ".artifacts", "nes-lab", "index.sqlite");
    if (args[2] is "list" or "show" && !File.Exists(database))
    { Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true, result = Array.Empty<object>() }, options)); return 0; }
    using var learning = new GatewayLearningStore(database);
    object proposalResult = args[2] switch
    {
        "list" => learning.Proposals(),
        "show" => learning.GetProposal(long.Parse(args[3])),
        "reject" => learning.Reject(long.Parse(args[3])),
        "accept" => AcceptProposal(learning, long.Parse(args[3]), root),
        _ => throw new ArgumentException($"Unknown proposal operation '{args[2]}'.")
    };
    Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true, operation = "memory-proposals-" + args[2], result = proposalResult }, options));
    return 0;
}

static MemoryProposal AcceptProposal(GatewayLearningStore learning, long id, string root)
{
    using var memory = new EngineeringMemoryStore(Path.Combine(root, ".artifacts", "nes-lab", "knowledge.db"));
    return learning.Accept(id, memory);
}

if (args.Length >= 2 && args[0].Equals("gateway", StringComparison.OrdinalIgnoreCase) &&
    args[1].Equals("benchmark", StringComparison.OrdinalIgnoreCase))
{
    int? corpus = null;
    var agentLocal = false;
    for (var index = 2; index < args.Length; index++)
        if (args[index] == "--corpus" && index + 1 < args.Length && int.TryParse(args[++index], out var value)) corpus = value;
        else if (args[index] == "--agent" && index + 1 < args.Length) agentLocal = args[++index].Equals("local", StringComparison.OrdinalIgnoreCase);
        else { Console.Error.WriteLine($"Unknown gateway benchmark argument: {args[index]}"); return 2; }
    var gatewayBenchmark = await GatewayBenchmark.RunAsync(Directory.GetCurrentDirectory(), corpus, agentLocal);
    var benchmarkJson = JsonSerializer.Serialize(gatewayBenchmark, options);
    var benchmarkArtifact = await new ImmutableArtifactStore(Path.Combine(Directory.GetCurrentDirectory(),
        ".artifacts", "nes-lab")).PublishTextAsync("context", benchmarkJson,
        "application/vnd.nes-lab.gateway-benchmark+json", pinned: true,
        reproductionCommand: $"nes-lab gateway benchmark --corpus {gatewayBenchmark.CorpusVersion}" +
            (agentLocal ? " --agent local" : ""));
    Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1,
        success = gatewayBenchmark.Passed == gatewayBenchmark.Total,
        operation = "gateway-benchmark", result = gatewayBenchmark,
        resourceUri = ImmutableArtifactStore.Uri("context", benchmarkArtifact.Digest) }, options));
    return gatewayBenchmark.Passed == gatewayBenchmark.Total ? 0 : 1;
}

if (args.Length > 0 && args[0].Equals("setup", StringComparison.OrdinalIgnoreCase))
{
    var setupParsed = SetupCommandParser.Parse(args);
    if (setupParsed.Invocation is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = false, error = setupParsed.Error }, options));
        return 2;
    }
    try
    {
        var root = Directory.GetCurrentDirectory();
        var setupResult = setupParsed.Invocation switch
        {
            McpSetupInvocation mcp => await McpRegistration.ExecuteAsync(mcp, root),
            ModelSetupInvocation model => await ModelSetup.ExecuteAsync(model, root),
            _ => throw new InvalidOperationException("Unsupported setup operation.")
        };
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true,
            operation = "setup", result = setupResult }, options));
        return 0;
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = false,
            error = new LabError("setup_failed", exception.Message) }, options));
        return 3;
    }
}

if (args.Length > 0 && args[0].Equals("artifacts", StringComparison.OrdinalIgnoreCase))
{
    var artifactParsed = ArtifactCommandParser.Parse(args);
    if (artifactParsed.Invocation is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = false, error = artifactParsed.Error }, options));
        return 2;
    }
    try
    {
        var store = new ImmutableArtifactStore(Path.Combine(Directory.GetCurrentDirectory(), ".artifacts", "nes-lab"));
        var artifactInvocation = artifactParsed.Invocation;
        object artifactResult;
        if (artifactInvocation.Operation == "list")
            artifactResult = store.List().Select(item => new { item.Kind, item.Digest, item.MimeType,
                item.ByteCount, item.CreatedAtUtc, item.Pinned, item.DeletedAtUtc,
                uri = ImmutableArtifactStore.Uri(item.Kind, item.Digest) }).ToArray();
        else if (artifactInvocation.Operation == "describe")
        {
            var resource = await store.ReadAsync(artifactInvocation.Kind!, artifactInvocation.Digest!);
            artifactResult = new { resource.Metadata, resource.Uri, resource.Status };
        }
        else if (artifactInvocation.Operation == "text")
        {
            var resource = await store.ReadAsync(artifactInvocation.Kind!, artifactInvocation.Digest!);
            if (resource.Text is null) throw new InvalidDataException("Artifact is not textual.");
            var lines = resource.Text.Split('\n');
            var selected = lines.Skip(artifactInvocation.StartLine - 1).Take(artifactInvocation.MaximumLines).ToArray();
            artifactResult = new { resource.Uri, totalLines = lines.Length,
                startLine = artifactInvocation.StartLine, returnedLines = selected.Length,
                truncated = artifactInvocation.StartLine - 1 + selected.Length < lines.Length,
                text = string.Join('\n', selected) };
        }
        else if (artifactInvocation.Operation == "prune")
            artifactResult = new { pruned = await store.PruneAsync(DateTimeOffset.UtcNow - artifactInvocation.OlderThan!.Value) };
        else
        {
            await store.SetPinnedAsync(artifactInvocation.Kind!, artifactInvocation.Digest!, artifactInvocation.Operation == "pin");
            artifactResult = new { uri = ImmutableArtifactStore.Uri(artifactInvocation.Kind!, artifactInvocation.Digest!),
                pinned = artifactInvocation.Operation == "pin" };
        }
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true,
            operation = "artifacts-" + artifactInvocation.Operation, result = artifactResult }, options));
        return 0;
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = false,
            error = new LabError("artifact_operation_failed", exception.Message) }, options));
        return 3;
    }
}

if (args.Length > 0 && args[0].Equals("diagnose", StringComparison.OrdinalIgnoreCase))
{
    var diagnosisParsed = DiagnoseCommandParser.Parse(args);
    if (diagnosisParsed.Invocation is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = false, error = diagnosisParsed.Error }, options));
        return 2;
    }
    try
    {
        var root = Directory.GetCurrentDirectory();
        var diagnosisInvocation = diagnosisParsed.Invocation;
        if (diagnosisInvocation.CaseName is { } caseName)
            _ = await new LabCliBridge(root).ExecuteAsync(
                ["verify", "--scope", "conformance", "--case", caseName, "--trace-on-failure"],
                CancellationToken.None);
        var historyPath = Path.Combine(root, ".artifacts", "nes-lab", "index.sqlite");
        if (diagnosisInvocation.CaseName is null && !File.Exists(historyPath))
            throw new KeyNotFoundException("No nes-lab run history exists.");
        using var history = new RunHistoryStore(historyPath, root);
        var run = diagnosisInvocation.RunId switch
        {
            null => history.Latest(caseName: diagnosisInvocation.CaseName, failuresOnly: true),
            "latest" => history.Latest(failuresOnly: true),
            { } id => history.Get(id)
        } ?? throw new KeyNotFoundException("No matching failed nes-lab run was found.");
        var diagnosis = await DiagnosisService.BuildAsync(run, root, diagnosisInvocation.BudgetBytes,
            diagnosisInvocation.CaseName is not null || diagnosisInvocation.Persist,
            diagnosisInvocation.RankLocal, diagnosisInvocation.Model, diagnosisInvocation.Endpoint);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = true, operation = "diagnose", result = diagnosis
        }, options));
        return 0;
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false, error = new LabError("diagnosis_failed", exception.Message)
        }, options));
        return 3;
    }
}

if (historyArgs.Length > 0 && historyArgs[0] is "runs" or "failures" or "metrics")
{
    var parsedHistory = HistoryCommandParser.Parse(historyArgs);
    if (parsedHistory.Invocation is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = false, error = parsedHistory.Error }, options));
        return 2;
    }
    try
    {
        var root = Directory.GetCurrentDirectory();
        var historyPath = Path.Combine(root, ".artifacts", "nes-lab", "index.sqlite");
        if (!File.Exists(historyPath))
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                schemaVersion = 1, success = true, operation = parsedHistory.Invocation.Operation,
                result = parsedHistory.Invocation.Operation == "metrics"
                    ? (object)new RunMetrics(0, 0, 0, 0, 0, 0, 0, 0)
                    : Array.Empty<RunHistoryEntry>()
            }, options));
            return 0;
        }
        using var history = new RunHistoryStore(historyPath, root);
        var historyInvocation = parsedHistory.Invocation;
        object? historyResult = historyInvocation.Operation switch
        {
            "metrics" => history.Metrics(),
            "runs-latest" => history.Latest(historyInvocation.Scope, historyInvocation.CaseName),
            "failures-latest" => history.Latest(historyInvocation.Scope, historyInvocation.CaseName, true),
            "failures-search" => history.SearchFailures(historyInvocation.Query!, historyInvocation.MaximumResults),
            _ => throw new InvalidOperationException("Unsupported history operation.")
        };
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = true, operation = historyInvocation.Operation, result = historyResult
        }, options));
        return 0;
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false, error = new LabError("history_operation_failed", exception.Message)
        }, options));
        return 3;
    }
}

if (args.Length > 0 && args[0].Equals("model", StringComparison.OrdinalIgnoreCase))
{
    var modelParsed = ModelCommandParser.Parse(args);
    if (modelParsed.Invocation is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false, error = modelParsed.Error
        }, options));
        return 2;
    }
    try
    {
        var modelInvocation = modelParsed.Invocation;
        if (modelInvocation is ModelRankInvocation rankInvocation)
        {
            var candidates = JsonSerializer.Deserialize<EvidenceCandidate[]>(
                await File.ReadAllTextAsync(rankInvocation.InputPath), options) ?? [];
            using var rankHttp = new HttpClient
            {
                BaseAddress = new Uri(rankInvocation.Endpoint), Timeout = TimeSpan.FromMinutes(5)
            };
            var ranking = await new OptionalEvidenceRanker(
                new OllamaEvidenceModel(rankHttp, rankInvocation.Model)).RankAsync(
                candidates, rankInvocation.MaximumResults, rankInvocation.UseLocalModel);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                schemaVersion = 1, success = true, operation = "model-rank", result = ranking
            }, options));
            return 0;
        }
        var benchmarkInvocation = (ModelBenchmarkInvocation)modelInvocation;
        using var http = new HttpClient { BaseAddress = new Uri(benchmarkInvocation.Endpoint), Timeout = TimeSpan.FromMinutes(5) };
        var fixture = OllamaBenchmarkRunner.Load(benchmarkInvocation.FixturePath);
        var benchmarkResult = await OllamaBenchmarkRunner.RunAsync(
            new OllamaEvidenceModel(http, benchmarkInvocation.Model), benchmarkInvocation.Model, fixture);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = true, operation = "model-benchmark", result = benchmarkResult
        }, options));
        return benchmarkResult.Passed == benchmarkResult.Total ? 0 : 1;
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false,
            error = new LabError("model_operation_failed", exception.Message)
        }, options));
        return 3;
    }
}

if (args.Length > 0 && args[0].Equals("context", StringComparison.OrdinalIgnoreCase))
{
    var contextParsed = ContextCommandParser.Parse(args);
    if (contextParsed.Invocation is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false, error = contextParsed.Error
        }, options));
        return 2;
    }
    try
    {
        var contextInvocation = contextParsed.Invocation;
        await using var index = await RoslynSymbolIndex.OpenAsync(contextInvocation.SolutionPath);
        var evidence = contextInvocation.SymbolId is not null
            ? await SymbolContextPacketBuilder.CollectByIdAsync(index,
                Directory.GetCurrentDirectory(), contextInvocation.SymbolId)
            : contextInvocation.Symbol is not null
              ? await SymbolContextPacketBuilder.CollectAsync(index, Directory.GetCurrentDirectory(),
                new RoslynSymbolQuery(contextInvocation.Symbol!, contextInvocation.ExactQualifiedName,
                    contextInvocation.Kind, contextInvocation.Project, contextInvocation.Namespace,
                    contextInvocation.FilePath, 16))
              : await GeneralContextEvidenceCollector.CollectAsync(index,
                    Directory.GetCurrentDirectory(), contextInvocation);
        var ranked = await LocalEvidenceRanking.ApplyAsync(evidence, contextInvocation.RankLocal,
            contextInvocation.Model, contextInvocation.Endpoint);
        var packet = ContextPacketBuilder.Build(ranked.Evidence, contextInvocation.BudgetBytes, contextInvocation.BudgetTokens);
        var synthesis = await LocalEvidenceSynthesis.RunAsync(ranked.Evidence, contextInvocation.SynthesizeLocal,
            contextInvocation.Model, contextInvocation.Endpoint);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = true, operation = "context-build",
            packet.PacketId, packet.BudgetBytes, packet.UsedBytes, packet.IncludedEvidenceCount,
            packet.OmittedEvidenceCount, packet.Truncated, packet.Categories,
            packet.RequiredEvidence, packet.Density, packet.EvidenceSpine, packet.InputEvidenceCount,
            packet.UniqueEvidenceCount, packet.DuplicateEvidenceCount,
            packet = JsonDocument.Parse(packet.Content).RootElement
            , ranking = LocalEvidenceRanking.Describe(ranked.Ranking), synthesis
        }, options));
        return 0;
    }
    catch (SymbolContextPacketBuilder.AmbiguousSymbolException exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false,
            error = new LabError("ambiguous_symbol", exception.Message),
            candidates = exception.Candidates
        }, options));
        return 2;
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false,
            error = new LabError("context_operation_failed", exception.Message)
        }, options));
        return 3;
    }
}

if (args.Length >= 2 && args[0].Equals("code", StringComparison.OrdinalIgnoreCase) &&
    args[1].Equals("index", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var solution = args.Length == 4 && args[2] == "--solution" ? args[3] : "nes-emulator-3.slnx";
        await using var index = await RoslynSymbolIndex.OpenAsync(solution, persistCache: true);
        var textIndex = RepositoryTextIndex.Build(Directory.GetCurrentDirectory(), Path.Combine(
            Directory.GetCurrentDirectory(), ".artifacts", "nes-lab", "retrieval.sqlite"));
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = true,
            operation = "code-index", result = new { index.DeclarationCount, solution, textIndex } }, options));
        return 0;
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, success = false,
            error = new LabError("code_index_failed", exception.Message) }, options));
        return 3;
    }
}

if (args.Length > 0 && args[0].Equals("code", StringComparison.OrdinalIgnoreCase))
{
    var codeParsed = CodeCommandParser.Parse(args);
    if (codeParsed.Invocation is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false, error = codeParsed.Error
        }, options));
        return 2;
    }
    try
    {
        var initialization = System.Diagnostics.Stopwatch.StartNew();
        await using var index = await RoslynSymbolIndex.OpenAsync(
            codeParsed.Invocation.SolutionPath, CancellationToken.None);
        initialization.Stop();
        object codeResult = codeParsed.Invocation switch
        {
            CodeFindInvocation find => index.FindDeclarations(new RoslynSymbolQuery(
                find.Symbol, find.ExactQualifiedName, find.Kind, find.Project,
                find.Namespace, find.FilePath, find.MaximumResults)),
            CodeBenchmarkInvocation benchmark => await BenchmarkCodeAsync(
                index, benchmark, initialization.Elapsed.TotalMilliseconds),
            CodeRelationsInvocation { Operation: "refs" } relations =>
                await index.FindReferencesAsync(relations.SymbolId),
            CodeRelationsInvocation { Operation: "callers" } relations =>
                await index.FindCallersAsync(relations.SymbolId),
            CodeRelationsInvocation { Operation: "tests" } relations =>
                await index.FindAffectedTestsAsync(relations.SymbolId),
            _ => throw new InvalidOperationException("Unsupported code operation.")
        };
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = true,
            operation = $"code-{args[1].ToLowerInvariant()}", result = codeResult
        }, options));
        return 0;
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false,
            error = new LabError("code_operation_failed", exception.Message)
        }, options));
        return 3;
    }
}

static async Task<object> BenchmarkCodeAsync(
    RoslynSymbolIndex index,
    CodeBenchmarkInvocation invocation,
    double initializationMilliseconds)
{
    var declarations = index.FindDeclarations(invocation.Symbol, 8);
    var declaration = declarations.FirstOrDefault() ?? throw new KeyNotFoundException(
        $"No symbol matched '{invocation.Symbol}'.");
    _ = await index.FindReferencesAsync(declaration.Id);
    List<double> findTimes = [];
    List<double> referenceTimes = [];
    IReadOnlyList<RoslynSymbolReference> references = [];
    for (var iteration = 0; iteration < invocation.Iterations; iteration++)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        declarations = index.FindDeclarations(invocation.Symbol, 8);
        watch.Stop();
        findTimes.Add(watch.Elapsed.TotalMilliseconds);
        watch.Restart();
        references = await index.FindReferencesAsync(declaration.Id);
        watch.Stop();
        referenceTimes.Add(watch.Elapsed.TotalMilliseconds);
    }
    var payload = JsonSerializer.Serialize(new { declarations, references }, LabResponseSerializer.Options);
    return new
    {
        invocation.Iterations,
        initializationMilliseconds,
        averageFindMilliseconds = findTimes.Average(),
        averageReferencesMilliseconds = referenceTimes.Average(),
        payloadUtf8Bytes = System.Text.Encoding.UTF8.GetByteCount(payload),
        declarationCount = declarations.Count,
        referenceCount = references.Count
    };
}

if (args.Length > 0 && args[0].Equals("memory", StringComparison.OrdinalIgnoreCase))
{
    var memoryParsed = MemoryCommandParser.Parse(args);
    if (memoryParsed.Invocation is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false, error = memoryParsed.Error
        }, options));
        return 2;
    }
    try
    {
        var databasePath = memoryParsed.Invocation.DatabasePath ?? Path.Combine(
            Directory.GetCurrentDirectory(), ".artifacts", "nes-lab", "knowledge.db");
        object memoryResult;
        if (memoryParsed.Invocation is MemorySearchInvocation or MemoryShowInvocation or MemoryStaleInvocation && !File.Exists(databasePath))
            memoryResult = Array.Empty<EngineeringMemoryEntry>();
        else
        {
            using var store = new EngineeringMemoryStore(databasePath);
            memoryResult = memoryParsed.Invocation switch
            {
                MemoryAddInvocation add => AddMemory(store, add),
                MemorySearchInvocation search => store.Search(
                    search.Query, search.Kind, search.MaximumResults,
                    search.IncludeRejectedHypotheses),
                MemoryShowInvocation show => store.Get(show.Id),
                MemorySupersedeInvocation supersede => SupersedeMemory(store, supersede),
                MemoryValidateInvocation validate => store.Validate(validate.RepositoryRoot),
                MemoryStaleInvocation => store.Stale(),
                MemoryTransferInvocation transfer when transfer.Operation == "export" => ExportMemory(store, transfer.Path),
                MemoryTransferInvocation transfer when transfer.Operation == "import" => ImportMemory(store, transfer.Path),
                _ => throw new InvalidOperationException("Unsupported memory operation.")
            };
            if (memoryResult is EngineeringMemoryEntry memoryEntry &&
                memoryParsed.Invocation is MemoryAddInvocation or MemorySupersedeInvocation)
            {
                var artifactRoot = Path.Combine(Directory.GetCurrentDirectory(), ".artifacts", "nes-lab");
                var artifactStore = new ImmutableArtifactStore(artifactRoot);
                var snapshot = JsonSerializer.Serialize(memoryEntry, LabResponseSerializer.Options);
                var metadata = await artifactStore.PublishTextAsync("memory", snapshot,
                    "application/vnd.nes-lab.memory+json", pinned: true,
                    reproductionCommand: $"nes-lab memory show {memoryEntry.Id}");
                memoryResult = new
                {
                    entry = memoryEntry,
                    resourceUri = ImmutableArtifactStore.Uri("memory", metadata.Digest)
                };
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = true,
            operation = "memory-" + memoryParsed.Invocation.GetType().Name
                .Replace("Memory", "").Replace("Invocation", "").ToLowerInvariant(),
            databasePath = Path.GetFullPath(databasePath),
            result = memoryResult
        }, options));
        return 0;
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false,
            error = new LabError("memory_operation_failed", exception.Message)
        }, options));
        return 3;
    }
}

static object SupersedeMemory(EngineeringMemoryStore store, MemorySupersedeInvocation invocation)
{
    var replacement = (EngineeringMemoryEntry)AddMemoryEntry(invocation.Replacement);
    var id = store.Supersede(invocation.Id, replacement);
    return store.Get(id);
}

static object AddMemoryEntry(MemoryAddInvocation invocation) => new EngineeringMemoryEntry(
    0, invocation.Kind, invocation.Title, invocation.Body,
    [new EngineeringProvenance(invocation.SourceKind, invocation.Source, invocation.SourceHash,
        invocation.LineNumber, invocation.Commit)], DateTimeOffset.UtcNow);

static object ExportMemory(EngineeringMemoryStore store, string path)
{
    var fullPath = Path.GetFullPath(path);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    File.WriteAllText(fullPath, JsonSerializer.Serialize(store.All(), LabResponseSerializer.Options));
    return new { path = fullPath, count = store.All().Count };
}

static object ImportMemory(EngineeringMemoryStore store, string path)
{
    var entries = JsonSerializer.Deserialize<EngineeringMemoryEntry[]>(
        File.ReadAllText(path), LabResponseSerializer.Options) ?? [];
    var ids = entries.Select(entry => store.Add(entry with { Id = 0 })).ToArray();
    return new { count = ids.Length, ids };
}

static object AddMemory(EngineeringMemoryStore store, MemoryAddInvocation invocation)
{
    var entry = (EngineeringMemoryEntry)AddMemoryEntry(invocation);
    var id = store.Add(entry);
    return entry with { Id = id };
}

if (args.Length > 0 && args[0].Equals("rom", StringComparison.OrdinalIgnoreCase))
{
    var romParsed = RomCommandParser.Parse(args);
    if (romParsed.Invocation is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false, error = romParsed.Error
        }, options));
        return 2;
    }
    try
    {
        var root = Directory.GetCurrentDirectory();
        var manifest = romParsed.Invocation.ManifestPath ??
            Path.Combine(root, "test", "conformance", "test-roms.json");
        var assets = romParsed.Invocation.AssetRoot;
        if (assets is null)
        {
            var candidate = Path.Combine(root, "test-roms", "nes-test-roms");
            if (Directory.Exists(candidate)) assets = candidate;
        }
        var catalog = RomCatalog.Load(manifest, assets);
        object romResult = romParsed.Invocation switch
        {
            RomShowInvocation show => catalog.Find(show.Suite, show.Name),
            RomSourceInvocation source => QuerySource(catalog, source, assets),
            RomDiagnoseInvocation diagnose when assets is not null => RomDiagnosisBuilder.Diagnose(
                catalog, assets, diagnose.Suite, diagnose.Name, diagnose.Code),
            RomDiagnoseInvocation => throw new DirectoryNotFoundException(
                "The ROM asset root is required for diagnosis."),
            RomListInvocation list => ListRoms(catalog, list),
            _ => throw new InvalidOperationException("Unsupported ROM operation.")
        };
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = true,
            operation = romParsed.Invocation switch
            {
                RomShowInvocation => "rom-show",
                RomSourceInvocation => "rom-source",
                RomDiagnoseInvocation => "rom-diagnose",
                _ => "rom-list"
            },
            upstreamCommit = catalog.UpstreamCommit,
            manifestSha256 = catalog.ManifestSha256,
            result = romResult
        }, options));
        return 0;
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, success = false,
            error = new LabError("rom_operation_failed", exception.Message)
        }, options));
        return 3;
    }
}

static object ListRoms(RomCatalog catalog, RomListInvocation invocation)
{
    var filtered = catalog.Entries.Where(entry => invocation.Suite is null ||
        entry.Suite.Equals(invocation.Suite, StringComparison.OrdinalIgnoreCase)).ToArray();
    var entries = filtered.Take(invocation.MaximumResults).Select(entry => new
    {
        entry.Suite, entry.Name, entry.Availability, entry.VideoStandard, entry.MaximumPpuDots
    }).ToArray();
    return new
    {
        total = filtered.Length,
        returned = entries.Length,
        truncated = entries.Length < filtered.Length,
        entries
    };
}

static object QuerySource(RomCatalog catalog, RomSourceInvocation invocation, string? assets)
{
    if (assets is null)
        throw new DirectoryNotFoundException("The ROM asset root is required for source queries.");
    var entry = catalog.Find(invocation.Suite, invocation.Name);
    var index = AssemblySourceIndex.Build(entry, assets);
    return new
    {
        entry.Suite,
        entry.Name,
        index.SourceRoot,
        documents = index.Documents.Count,
        resultEncodings = index.ResultEncodings,
        results = invocation.Symbol is not null
            ? (object)index.FindSymbol(invocation.Symbol, invocation.MaximumResults)
            : index.SearchText(invocation.Text!, invocation.MaximumResults)
    };
}

if (args.Length > 0 && args[0].Equals("trace", StringComparison.OrdinalIgnoreCase))
{
    var traceParsed = TraceCommandParser.Parse(args);
    if (traceParsed.Invocation is null)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            success = false,
            error = traceParsed.Error
        }, options));
        return 2;
    }

    try
    {
        var reader = new TraceArtifactReader();
        object traceResult = traceParsed.Invocation switch
        {
            TraceCaptureInvocation capture => await CaptureTraceAsync(capture),
            TraceQueryInvocation query => TraceQueryEngine.Query(
                await reader.ReadAsync(await ResolveTracePathAsync(query.ArtifactPath)), query.Query),
            TraceDiffInvocation diff => TraceDiffEngine.Diff(
                await reader.ReadAsync(diff.ExpectedArtifactPath),
                await reader.ReadAsync(diff.ActualArtifactPath),
                diff.ContextRecords),
            _ => throw new InvalidOperationException("Unsupported trace operation.")
        };
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            success = true,
            operation = traceParsed.Invocation switch
            {
                TraceQueryInvocation => "trace-query",
                TraceDiffInvocation => "trace-diff",
                _ => "trace-capture"
            },
            result = traceResult
        }, options));
        return traceResult is TraceDiffResult { Equal: false } ? 1 : 0;
    }
    catch (Exception exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            success = false,
            error = new LabError(exception is TimeoutException ? "trace_capture_timeout" : "trace_operation_failed",
                exception.Message)
        }, options));
        return 3;
    }
}

static async Task<JsonElement> CaptureTraceAsync(TraceCaptureInvocation capture)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(capture.TimeoutSeconds));
    try
    {
        return await new LabCliBridge(Directory.GetCurrentDirectory())
            .ExecuteAsync(["verify", "--scope", "conformance", "--case", capture.CaseName,
                "--trace-always"], timeout.Token);
    }
    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
    {
        throw new TimeoutException(
            $"Trace capture for case '{capture.CaseName}' exceeded {capture.TimeoutSeconds} seconds. " +
            "The child process tree was terminated. Increase --timeout-seconds only when the case is expected to run longer.");
    }
}

static async Task<string> ResolveTracePathAsync(string pathOrUri)
{
    if (!ImmutableArtifactStore.TryParseUri(pathOrUri, out _, out _)) return pathOrUri;
    return await new ImmutableArtifactStore(Path.Combine(Directory.GetCurrentDirectory(), ".artifacts", "nes-lab"))
        .ResolveVerifiedDataPathAsync(pathOrUri);
}

var parsed = LabInvocationParser.Parse(args);
if (!parsed.IsSuccess)
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        success = false,
        error = parsed.Error
    }, options));
    return 2;
}

var invocation = parsed.Invocation!;
var repositoryRoot = Directory.GetCurrentDirectory();
IReadOnlyList<string> changedFiles = [];
ChangedFileScopeSelector.Selection? scopeSelection = null;
IReadOnlyList<VerificationCommand> commands;
IReadOnlyList<VerificationCommand> escalationCommands = [];
try
{
    if (invocation.Changed)
    {
        changedFiles = await new GitChangedFileProvider(new ProcessCommandExecutor(), repositoryRoot)
            .GetChangedFilesAsync(CancellationToken.None);
        scopeSelection = ChangedFileScopeSelector.SelectWithExplanation(changedFiles);
        commands = VerificationCommandCatalog.Create(scopeSelection.Selected, invocation.NoRestore);
        escalationCommands = VerificationCommandCatalog.Create(scopeSelection.Omitted, invocation.NoRestore);
    }
    else
    {
        if (invocation.CaseName is null)
        {
            commands = VerificationCommandCatalog.Create(invocation.Scope, invocation.NoRestore);
        }
        else
        {
            IReadOnlyDictionary<string, string>? environment = null;
            if (invocation.TraceOnFailure || invocation.TraceAlways)
            {
                var tracePath = Path.Combine(
                    repositoryRoot, ".artifacts", "nes-lab", "traces",
                    $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json");
                var commit = await new TraceProvenance(
                    new ProcessCommandExecutor(), repositoryRoot)
                    .GetSourceCommitAsync(CancellationToken.None);
                environment = new Dictionary<string, string>
                {
                    ["NES_LAB_TRACE_PATH"] = tracePath,
                    ["NES_LAB_TRACE_CASE"] = invocation.CaseName,
                    ["NES_LAB_SOURCE_COMMIT"] = commit,
                    ["NES_LAB_TRACE_CAPACITY"] = TraceArtifact.DefaultMaximumRecords.ToString(),
                    ["NES_LAB_TRACE_MODE"] = invocation.TraceAlways ? "always" : "failure"
                };
            }
            commands = [VerificationCommandCatalog.CreateNamedConformance(
                invocation.CaseName, invocation.NoRestore, environment)];
        }
    }
}
catch (Exception exception)
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        success = false,
        error = new LabError("change_discovery_failed", exception.Message)
    }, options));
    return 4;
}

if (invocation.PlanOnly)
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        success = true,
        operation = "verify-plan",
        invocation.Scope,
        changedFiles,
        scopeSelection,
        commands
    }, options));
    return 0;
}

using var runHistory = new RunHistoryStore(
    Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "index.sqlite"), repositoryRoot);
var runner = new VerificationRunner(
    new ProcessCommandExecutor(),
    Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "runs"),
    new FileVerificationCache(
        repositoryRoot,
        Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "cache")),
    runHistory,
    progress => Console.Error.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        stream = "verification-progress",
        scope = progress.Scope,
        phase = progress.Phase,
        elapsedMilliseconds = progress.ElapsedMilliseconds
    }, options)));
var result = invocation.Changed
    ? await runner.RunWithEscalationAsync(commands, escalationCommands, CancellationToken.None,
        invocation.ContinueOnFailure)
    : await runner.RunAsync(commands, CancellationToken.None, invocation.ContinueOnFailure);
ConformanceBaselineComparison? baselineComparison = null;
var conformanceResult = result.Results.FirstOrDefault(item => item.Scope == VerificationScope.Conformance);
if (conformanceResult is not null)
{
    var baseline = ConformanceBaselineComparer.Load(Path.Combine(repositoryRoot, "src", "tools", "nes-lab",
        "accepted-conformance-baseline.v1.json"));
    baselineComparison = ConformanceBaselineComparer.Compare(baseline, conformanceResult.Failures,
        conformanceResult.Summary, invocation.CaseName);
}
var semantics = VerificationSemantics.Evaluate(result, baselineComparison, invocation.ExitPolicy);
runHistory.RecordSemantics(result, semantics);
var payload = LabResponseSerializer.SerializeVerification(invocation.Scope, result, baselineComparison,
    invocation.ExitPolicy);
Console.WriteLine(payload);
return semantics.ExitSucceeded ? 0 : 1;
