namespace Sheep.Nes.Lab;

public sealed record LabCapabilitySummary(string Name, string Summary);
public sealed record LabOperationDescription(
    string Name,
    string Summary,
    IReadOnlyList<string> Parameters);
public sealed record LabCapabilityDescription(
    string Name,
    string Summary,
    string Provenance,
    IReadOnlyList<LabOperationDescription> Operations);

public static class LabCapabilityCatalog
{
    private static readonly LabCapabilityDescription[] Capabilities =
    [
        Capability("code", "Find C# declarations, references, callers, and affected tests.",
            "Every result cites its project, file, and line.",
            Operation("find", "Find declarations with exact disambiguation filters.", "symbol", "maximumResults", "exactQualifiedName", "kind", "project", "namespace", "filePath"),
            Operation("refs", "Find references by stable symbol ID.", "symbolId"),
            Operation("callers", "Find callers by stable symbol ID.", "symbolId"),
            Operation("tests", "Find affected tests by stable symbol ID.", "symbolId"),
            Operation("index", "Persist a source-hash-invalidated Roslyn declaration cache.")),
        Capability("context", "Build deterministic evidence packets under a byte ceiling.",
            "Every evidence item retains a source path and optional hash/line.",
            Operation("build", "Pack symbol, changed-file, subsystem, task, run, or handoff evidence.", "symbol", "symbolId", "exactQualifiedName", "kind", "project", "namespace", "filePath", "changed", "baseRevision", "subsystem", "task", "runId", "handoffUri", "budgetBytes", "synthesize")),
        Capability("memory", "Search provenance-first engineering knowledge.",
            "Entries retain typed status and mandatory source provenance.",
            Operation("search", "Search current confirmed facts, observations, decisions, and hypotheses.", "query", "kind", "maximumResults"),
            Operation("show", "Read one immutable memory revision.", "id"),
            Operation("stale", "List revisions whose provenance no longer validates."),
            Operation("validate", "Recompute provenance hashes and stale labels."),
            Operation("proposals-list", "List immutable pending, accepted, and rejected proposals."),
            Operation("proposals-show", "Read one proposal.", "id"),
            Operation("proposals-accept", "Explicitly promote a cited proposal to authoritative memory.", "id"),
            Operation("proposals-reject", "Reject a proposal while retaining it for audit.", "id")),
        Capability("feedback", "Record explicit usefulness labels for immutable packet evidence.",
            "Feedback is append-only, bounded, and cannot override required benchmark evidence.",
            Operation("record", "Label evidence from an immutable packet and optionally attach measured outcome telemetry.", "packetId", "usefulEvidenceIds", "notUsefulEvidenceIds", "outcome", "runId", "model", "provider", "cloudInputTokens", "cloudOutputTokens", "diagnosticIterations", "elapsedMilliseconds", "verificationResult", "acceptedProposalIds", "acceptedFixIds", "tokensAvoided", "telemetry"),
            Operation("show", "List recorded feedback."),
            Operation("search", "Search feedback provenance.", "query"),
            Operation("metrics", "Read ranking weights and explicitly reported outcome measurements; unavailable host telemetry remains labeled Unavailable.")),
        Capability("references", "Search and synchronize versioned authoritative NES documentation.",
            "Cached source is checksum-verified and every claim cites its canonical upstream source.",
            Operation("status", "Report offline cache availability and integrity."),
            Operation("search", "Search synchronized reference content.", "query"),
            Operation("show", "Read one synchronized reference.", "id"),
            Operation("sync", "Explicitly fetch and verify the pinned reference corpus.")),
        Capability("experiment", "Run and compare deterministic headless NES scenarios.",
            "Scenarios use emulated clocks only; outputs are immutable content-addressed artifacts.",
            Operation("run", "Run a schema-validated committed scenario.", "scenarioPath"),
            Operation("run-inline", "Run a bounded inline scenario and publish its canonical immutable form.", "scenario"),
            Operation("compare", "Compare two immutable experiment results by capture identity.", "leftUri", "rightUri")),
        Capability("media", "Analyze immutable frame and Float32 audio evidence semantically.",
            "Responses contain bounded numeric evidence; full media and heatmaps remain immutable artifacts.",
            Operation("frame-compare", "Compare RGBA frames by pixels, scanlines, bounds, and optional heatmap.", "leftUri", "rightUri", "heatmap"),
            Operation("audio-analyze", "Measure Float32 PCM level, health, windows, and spectral bands.", "uri", "sampleRate", "windowSize"),
            Operation("audio-compare", "Find the first sample difference under explicit tolerances.", "leftUri", "rightUri", "sampleRate", "sampleTolerance", "timingToleranceSamples", "rmsTolerance")),
        Capability("investigate", "Run a bounded read-only local evidence investigation.",
            "The local model can select only allowlisted inspections; all retained claims cite evidence IDs.",
            Operation("task", "Investigate a natural-language task and retain an immutable transcript.", "task", "budgetBytes", "maximumSteps", "model", "endpoint"),
            Operation("run", "Investigate an indexed run without rerunning verification.", "runId", "budgetBytes", "maximumSteps", "model", "endpoint")),
        Capability("session", "Create and reopen immutable development-session handoffs.",
            "Handoffs snapshot repository, verification, memory, Git hunk, and artifact provenance.",
            Operation("close", "Publish a compact pinned handoff for a later agent session.", "task", "runId", "packetUri", "recommendedNextCommand", "telemetry"),
            Operation("show", "Read one immutable handoff without changing local state.", "uri")),
        Capability("host", "Import and compare portable frontend diagnostic bundles.",
            "Applications explicitly export bounded diagnostics; NES Lab never attaches to arbitrary processes.",
            Operation("diagnostics-import", "Validate and publish an immutable host diagnostic bundle.", "bundle"),
            Operation("diagnostics-show", "Read one immutable host diagnostic bundle.", "uri"),
            Operation("diagnostics-compare", "Compare audio/video host counters and configuration.", "leftUri", "rightUri")),
        Capability("build", "Diagnose structured MSBuild and compiler failures from indexed runs.",
            "Diagnostics cite the immutable run/log and distinguish the earliest actionable error from cascades.",
            Operation("diagnose", "Parse an indexed build failure into structured diagnostics.", "runId")),
        Capability("rom", "Resolve conformance ROM metadata, sources, and result codes.",
            "Results cite manifest hashes, ROM checksums, upstream commits, and source lines.",
            Operation("list", "List a bounded ROM catalog projection.", "suite", "maximumResults"),
            Operation("show", "Read a pinned ROM case.", "suite", "name"),
            Operation("source", "Search indexed ROM assembly source.", "suite", "name", "symbol", "text", "maximumResults"),
            Operation("diagnose", "Explain a terminal result code from upstream source.", "suite", "name", "code")),
        Capability("trace", "Query or diff bounded CPU-clock trace artifacts.",
            "Full data remains in a versioned artifact; responses cite its path and provenance.",
            Operation("query", "Select boundaries, addresses, DMA, or interrupt records.", "artifactPath", "artifactUri", "actor", "address", "endAddress", "maximumResults", "interruptEdges", "dmaOverlap", "instructionBoundaries"),
            Operation("diff", "Locate a reference-backed semantic divergence.", "expectedArtifactPath", "actualArtifactPath", "contextRecords"),
            Operation("capture", "Run a named case and retain checkpoint-aware trace v3.", "caseName", "timeoutSeconds")),
        Capability("verify", "Run focused repository verification with reduced structured output.",
            "Complete stdout/stderr is retained in local run artifacts.",
            Operation("run", "Run a scope, changed-file selection, or named conformance case with strict or baseline-aware process exit semantics.", "scope", "caseName", "changed", "continueOnFailure", "traceOnFailure", "traceAlways", "planOnly", "baselineAwareExitCode")),
        Capability("history", "Inspect indexed verification runs, failures, and reduction metrics.",
            "Records cite retained log and trace artifacts by stable run ID.",
            Operation("latest", "Read the latest run or failure.", "failuresOnly", "scope", "caseName"),
            Operation("search", "Search indexed failure text.", "query", "maximumResults"),
            Operation("metrics", "Read aggregate run/cache/byte metrics.")),
        Capability("diagnose", "Build one deterministic implementation-ready failure packet.",
            "Packets preserve run, ROM, trace, source, test, memory, and artifact provenance.",
            Operation("run", "Execute and diagnose a named conformance case.", "caseName", "budgetBytes"),
            Operation("inspect", "Rebuild diagnosis for an existing run without persistence.", "runId", "budgetBytes")),
        Capability("artifacts", "Discover and retain immutable content-addressed evidence resources.",
            "Every listed URI embeds and verifies the artifact SHA-256 digest.",
            Operation("list", "List recent and pinned immutable resources."),
            Operation("describe", "Read verified artifact metadata without its content.", "uri"),
            Operation("text", "Read a bounded textual line window.", "uri", "startLine", "maximumLines"),
            Operation("pin", "Retain a resource indefinitely.", "uri"),
            Operation("unpin", "Return a resource to normal retention.", "uri"),
            Operation("prune", "Replace expired unpinned bytes with tombstones.", "olderThan"))
    ];

    public static IReadOnlyList<LabCapabilitySummary> List() => Capabilities
        .Select(item => new LabCapabilitySummary(item.Name, item.Summary)).ToArray();

    public static LabCapabilityDescription Describe(string name) => Capabilities.FirstOrDefault(item =>
        item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ??
        throw new KeyNotFoundException($"Unknown nes-lab capability '{name}'.");

    private static LabCapabilityDescription Capability(
        string name, string summary, string provenance, params LabOperationDescription[] operations) =>
        new(name, summary, provenance, operations);

    private static LabOperationDescription Operation(
        string name, string summary, params string[] parameters) => new(name, summary, parameters);
}
