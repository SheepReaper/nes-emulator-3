using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed record SessionHandoff(int SchemaVersion, string Task, DateTimeOffset CreatedAtUtc,
    RepositoryProvenance Repository, RunHistoryEntry? Run, IReadOnlyList<GitDiffHunk> GitHunks,
    IReadOnlyList<EngineeringMemoryEntry> AcceptedMemory,
    IReadOnlyList<EngineeringMemoryEntry> RejectedHypotheses,
    IReadOnlyList<EngineeringMemoryEntry> StaleMemory,
    IReadOnlyList<string> ArtifactUris, string? PacketUri, string RecommendedNextCommand,
    string? ResourceUri = null, IReadOnlyList<EvidenceFeedback>? Feedback = null,
    HostUsageTelemetry? Telemetry = null);

public static class SessionHandoffBuilder
{
    public static SessionHandoff Build(string task, RepositoryProvenance provenance, RunHistoryEntry? run,
        IReadOnlyList<GitDiffHunk> hunks, IReadOnlyList<EngineeringMemoryEntry> memory,
        IReadOnlyList<string> artifactUris, string? packetUri, string nextCommand,
        IReadOnlyList<EvidenceFeedback>? feedback = null, HostUsageTelemetry? telemetry = null)
    {
        var current = memory.Where(item => !item.IsStale && item.Kind != EngineeringMemoryKind.RejectedHypothesis).ToArray();
        var rejected = memory.Where(item => !item.IsStale && item.Kind == EngineeringMemoryKind.RejectedHypothesis).ToArray();
        var stale = memory.Where(item => item.IsStale).ToArray();
        return new(1, task, DateTimeOffset.UtcNow, provenance, run, hunks, current, rejected, stale,
            artifactUris.Distinct(StringComparer.Ordinal).ToArray(), packetUri, nextCommand,
            Feedback: feedback ?? [], Telemetry: telemetry);
    }
}

public sealed class SessionHandoffService(string repositoryRoot)
{
    private readonly string root = Path.GetFullPath(repositoryRoot);
    private readonly ImmutableArtifactStore artifacts = new(Path.Combine(Path.GetFullPath(repositoryRoot), ".artifacts", "nes-lab"));

    public async Task<SessionHandoff> CloseAsync(string task, string runId = "latest", string? packetUri = null,
        string recommendedNextCommand = "nes-lab verify --changed", HostUsageTelemetry? telemetry = null,
        CancellationToken cancellationToken = default)
    {
        RunHistoryEntry? run = null;
        var historyPath = Path.Combine(root, ".artifacts", "nes-lab", "index.sqlite");
        if (File.Exists(historyPath))
        {
            using var history = new RunHistoryStore(historyPath, root);
            run = runId.Equals("latest", StringComparison.OrdinalIgnoreCase) ? history.Latest() : history.Get(runId);
        }
        IReadOnlyList<EngineeringMemoryEntry> memory = [];
        var memoryPath = Path.Combine(root, ".artifacts", "nes-lab", "knowledge.db");
        if (File.Exists(memoryPath)) { using var store = new EngineeringMemoryStore(memoryPath); memory = store.All(); }
        IReadOnlyList<EvidenceFeedback> feedback = [];
        if (File.Exists(historyPath))
        {
            using var learning = new GatewayLearningStore(historyPath);
            feedback = learning.AllFeedback()
                .Where(item => packetUri is null || packetUri.EndsWith(item.PacketId["packet-".Length..], StringComparison.Ordinal))
                .Take(32).ToArray();
        }
        var hunks = await new GitDiffHunkProvider(new ProcessCommandExecutor(), root).GetHunksAsync(cancellationToken);
        var recentEvidence = artifacts.List(64)
            .Where(item => item.DeletedAtUtc is null && item.Kind is "context" or "experiment" or "frame" or "audio" or "reference" or "investigation")
            .OrderByDescending(item => item.CreatedAtUtc).Take(24)
            .Select(item => ImmutableArtifactStore.Uri(item.Kind, item.Digest));
        var uris = new[] { run?.ResourceUri, run?.LogResourceUri, run?.TraceResourceUri, packetUri }
            .OfType<string>().Concat(recentEvidence).ToArray();
        var handoff = SessionHandoffBuilder.Build(task, RepositoryProvenance.Capture(root), run, hunks,
            memory, uris, packetUri, recommendedNextCommand, feedback, telemetry);
        var json = JsonSerializer.Serialize(handoff, LabResponseSerializer.Options);
        var metadata = await artifacts.PublishTextAsync("handoff", json,
            "application/vnd.nes-lab.session-handoff+json", pinned: true,
            reproductionCommand: $"nes-lab session close --task \"{task.Replace("\"", "'", StringComparison.Ordinal)}\" --run {runId}",
            cancellationToken: cancellationToken);
        return handoff with { ResourceUri = ImmutableArtifactStore.Uri("handoff", metadata.Digest) };
    }

    public async Task<SessionHandoff> ShowAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (!ImmutableArtifactStore.TryParseUri(uri, out var kind, out var digest) || kind != "handoff")
            throw new ArgumentException("An immutable handoff URI is required.");
        var resource = await artifacts.ReadAsync(kind, digest, cancellationToken);
        return JsonSerializer.Deserialize<SessionHandoff>(resource.Text!, LabResponseSerializer.Options)
            ?? throw new InvalidDataException("Session handoff is invalid.");
    }
}
