using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sheep.Nes.Lab;

public static class GeneralContextEvidenceCollector
{
    public static async Task<IReadOnlyList<ContextEvidence>> CollectAsync(
        RoslynSymbolIndex index, string repositoryRoot, ContextBuildInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(repositoryRoot);
        if (invocation.Changed)
        {
            var hunks = await new GitDiffHunkProvider(new ProcessCommandExecutor(), root)
                .GetHunksAsync(cancellationToken, invocation.BaseRevision).ConfigureAwait(false);
            return await WithGuidanceAsync(root, await FromHunksAsync(index, root, hunks, cancellationToken), cancellationToken);
        }
        if (invocation.Subsystem is not null)
        {
            var profiles = RetrievalProfiles.Load(root);
            return await WithGuidanceAsync(root, await FromDeclarationsAsync(index, root,
                index.FindDeclarationsByPaths(profiles.Subsystems[invocation.Subsystem], 24), cancellationToken)
                .ConfigureAwait(false), cancellationToken);
        }
        if (invocation.Task is not null)
            return await WithGuidanceAsync(root, await FromTaskAsync(index, root, invocation.Task, cancellationToken).ConfigureAwait(false), cancellationToken);
        if (invocation.RunId is not null)
            return await WithGuidanceAsync(root, FromRun(root, invocation.RunId), cancellationToken);
        if (invocation.HandoffUri is not null)
        {
            var handoff = await new SessionHandoffService(root).ShowAsync(invocation.HandoffUri, cancellationToken);
            var json = JsonSerializer.Serialize(handoff, LabResponseSerializer.Options);
            return [new ContextEvidence(ContextEvidenceKind.Reference, 250, json, invocation.HandoffUri,
                Hash(json), Required: true, RequiredMarkers: ["recommendedNextCommand"]),
                .. await GuidanceEvidenceCollector.CollectAsync(root,
                    handoff.GitHunks.Select(hunk => hunk.NewPath ?? hunk.OldPath ?? ""), cancellationToken)];
        }
        throw new InvalidOperationException("The invocation is not a general context selector.");
    }

    private static async Task<IReadOnlyList<ContextEvidence>> FromPathsAsync(
        RoslynSymbolIndex index, string root, IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        List<ContextEvidence> evidence = [];
        var declarations = index.FindDeclarationsByPaths(paths, 24);
        evidence.AddRange(await DeclarationEvidenceAsync(index, root, declarations, cancellationToken));
        foreach (var relative in paths.Take(16))
        {
            var full = Path.GetFullPath(Path.Combine(root, relative));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(full)) continue;
            var content = await File.ReadAllTextAsync(full, cancellationToken);
            if (content.Length > 4_000) content = content[..4_000] + "\n…[file excerpt truncated]";
            evidence.Add(new ContextEvidence(ContextEvidenceKind.GitDiff, 85, content,
                relative.Replace('\\', '/'), Hash(content)));
        }
        return evidence;
    }

    private static async Task<IReadOnlyList<ContextEvidence>> FromHunksAsync(RoslynSymbolIndex index, string root,
        IReadOnlyList<GitDiffHunk> hunks, CancellationToken cancellationToken)
    {
        List<ContextEvidence> evidence = hunks.Select(hunk => new ContextEvidence(ContextEvidenceKind.GitDiff, 110,
            hunk.Content, hunk.NewPath ?? hunk.OldPath ?? "git://unknown", hunk.ContentHash, hunk.NewStart,
            110, [$"change:{hunk.Category}", $"hunk:{hunk.Header}"])).ToList();
        var paths = hunks.Select(hunk => hunk.NewPath ?? hunk.OldPath).Where(path => path is not null).Cast<string>().Distinct().ToArray();
        evidence.AddRange(await DeclarationEvidenceAsync(index, root,
            index.FindDeclarationsByPaths(paths, 24), cancellationToken));
        return evidence;
    }

    private static async Task<IReadOnlyList<ContextEvidence>> FromDeclarationsAsync(
        RoslynSymbolIndex index, string root, IReadOnlyList<RoslynSymbolDeclaration> declarations,
        CancellationToken cancellationToken) =>
        await DeclarationEvidenceAsync(index, root, declarations, cancellationToken);

    private static async Task<IReadOnlyList<ContextEvidence>> FromTaskAsync(
        RoslynSymbolIndex index, string root, string task, CancellationToken cancellationToken)
    {
        var retrieved = await HybridTaskRetriever.RetrieveAsync(index, root, task, cancellationToken);
        List<ContextEvidence> evidence = [new(ContextEvidenceKind.Reference, 100, task, "task://request")];
        evidence.AddRange(retrieved.Evidence);
        return EvidenceSpine.Apply(task, evidence, retrieved.Classification);
    }

    private static IReadOnlyList<ContextEvidence> FromRun(string root, string runId)
    {
        var database = Path.Combine(root, ".artifacts", "nes-lab", "index.sqlite");
        if (!File.Exists(database))
            throw new FileNotFoundException("NES Lab run history is unavailable.", database);
        using var history = new RunHistoryStore(database, root);
        var entry = runId.Equals("latest", StringComparison.OrdinalIgnoreCase)
            ? history.Latest() ?? throw new KeyNotFoundException("NES Lab run history is empty.")
            : history.Get(runId);
        var json = JsonSerializer.Serialize(entry, LabResponseSerializer.Options);
        List<ContextEvidence> evidence = [new(ContextEvidenceKind.VerificationFailure, 100, json,
            entry.ResourceUri ?? $"run://{entry.Id}")];
        if (File.Exists(entry.ArtifactPath))
        {
            var text = File.ReadAllText(entry.ArtifactPath);
            if (text.Length > 8_000) text = text[^8_000..];
            evidence.Add(new ContextEvidence(ContextEvidenceKind.Reference, 80, text,
                entry.LogResourceUri ?? entry.ArtifactPath, Hash(text)));
        }
        return evidence;
    }

    private static async Task<IReadOnlyList<ContextEvidence>> DeclarationEvidenceAsync(
        RoslynSymbolIndex index, string root, IEnumerable<RoslynSymbolDeclaration> declarations,
        CancellationToken cancellationToken)
    {
        List<ContextEvidence> evidence = [];
        foreach (var declaration in declarations.Take(24))
        {
            var source = await index.GetDeclarationSourceAsync(declaration.Id, 4_000, cancellationToken);
            evidence.Add(new ContextEvidence(ContextEvidenceKind.Declaration, 90, source.Content,
                Path.GetRelativePath(root, source.FilePath), Hash(source.Content), source.LineNumber));
        }
        return evidence;
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task<IReadOnlyList<ContextEvidence>> WithGuidanceAsync(string root,
        IReadOnlyList<ContextEvidence> evidence, CancellationToken cancellationToken) =>
        [.. evidence, .. await GuidanceEvidenceCollector.CollectAsync(root, evidence.Select(item => item.Source), cancellationToken)];
}
