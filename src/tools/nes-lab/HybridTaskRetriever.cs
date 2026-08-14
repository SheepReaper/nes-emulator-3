using System.Security.Cryptography;
using System.Text;

namespace Sheep.Nes.Lab;

public sealed record HybridRetrievalResult(int ProfileVersion, string Task,
    IReadOnlyList<ContextEvidence> Evidence, int CandidateCount,
    SubsystemClassification? Classification = null);

public static class HybridTaskRetriever
{
    private static readonly string[] SearchExtensions = [".cs", ".xaml", ".json", ".md", ".s", ".inc"];

    public static async Task<HybridRetrievalResult> RetrieveAsync(RoslynSymbolIndex index,
        string repositoryRoot, string task, CancellationToken cancellationToken = default)
    {
        var profiles = RetrievalProfiles.Load(repositoryRoot);
        var classification = RetrievalProfiles.Classify(profiles, task);
        var terms = RetrievalProfiles.Expand(profiles, task);
        var literalTaskTerms = task.Split([' ', '\t', '\r', '\n', '.', ',', ':', ';', '(', ')', '-', '_'],
                StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length >= 3).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var subsystemPaths = RetrievalProfiles.MatchSubsystemPaths(profiles, task);
        var seedPaths = RetrievalProfiles.MatchSeedPaths(profiles, task);
        Dictionary<string, ContextEvidence> candidates = new(StringComparer.OrdinalIgnoreCase);

        foreach (var seedPath in seedPaths)
        {
            var path = Path.GetFullPath(Path.Combine(repositoryRoot, seedPath));
            if (!path.StartsWith(Path.GetFullPath(repositoryRoot) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) continue;
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            var fileName = Path.GetFileNameWithoutExtension(path);
            content = FocusSeedContent(content, fileName, literalTaskTerms, 400);
            var isTest = seedPath.Replace('\\', '/').StartsWith("test/", StringComparison.OrdinalIgnoreCase);
            Add(candidates, new ContextEvidence(isTest ? ContextEvidenceKind.AffectedTest : ContextEvidenceKind.Declaration,
                1_000, content, Relative(repositoryRoot, path), Hash(content), 1, 1_000,
                ["task-profile-seed"]));
        }

        var referenceManifest = Path.Combine(repositoryRoot, "src", "tools", "nes-lab", "reference-corpus.v1.json");
        if (File.Exists(referenceManifest))
        {
            var references = new ReferenceCorpusStore(referenceManifest, Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "references"));
            foreach (var entry in references.FindEntries(task, 8))
            {
                var content = $"{entry.Title}\n{string.Join('\n', entry.Claims.Select(claim => $"{claim.Section}: {claim.Summary}"))}\nSource: {entry.CanonicalUrl}";
                Add(candidates, new ContextEvidence(ContextEvidenceKind.Reference, 96, content,
                    entry.CanonicalUrl, entry.ExpectedSha256, RetrievalScore: 96,
                    ScoreReasons: ["authoritative-reference", .. entry.Topics.Select(topic => $"topic:{topic}")]));
            }
        }

        var textIndex = Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "retrieval.sqlite");
        foreach (var match in RepositoryTextIndex.Search(textIndex, string.Join(' ', terms), 48))
        {
            var score = 105d + Math.Max(0, -match.Rank);
            Add(candidates, new ContextEvidence(match.Kind == "test" ? ContextEvidenceKind.AffectedTest : ContextEvidenceKind.Reference,
                (int)score, match.Excerpt, match.Path, match.SourceHash, RetrievalScore: score,
                ScoreReasons: ["sqlite-fts5", $"bm25:{match.Rank:F3}"]));
        }

        foreach (var declaration in index.FindDeclarationsByPaths(
            subsystemPaths, 48))
        {
            var source = await index.GetDeclarationSourceAsync(declaration.Id, 4_000, cancellationToken);
            var score = 34d + MatchScore(task, terms, declaration.Name, declaration.QualifiedName, source.Content);
            Add(candidates, new ContextEvidence(ContextEvidenceKind.Declaration, (int)score,
                source.Content, Relative(repositoryRoot, source.FilePath), Hash(source.Content), source.LineNumber,
                score, ["subsystem-profile"]));
        }

        foreach (var path in EnumerateSearchFiles(repositoryRoot))
        {
            var relative = Relative(repositoryRoot, path);
            if (!subsystemPaths.Any(seed => relative.Contains(seed, StringComparison.OrdinalIgnoreCase))) continue;
            var fileName = Path.GetFileNameWithoutExtension(path);
            var fileTerms = SplitIdentifier(fileName);
            var matchingTerms = terms.Where(term => fileTerms.Contains(term, StringComparer.OrdinalIgnoreCase) ||
                fileName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matchingTerms.Length == 0) continue;
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            content = FocusContent(content, fileName, matchingTerms, 260);
            var isTest = relative.StartsWith("test/", StringComparison.OrdinalIgnoreCase);
            var specificity = matchingTerms.Length / (double)Math.Max(1, fileTerms.Count);
            var score = 180d + matchingTerms.Length * 20 + specificity * 80 + (isTest ? 15 : 0);
            Add(candidates, new ContextEvidence(isTest ? ContextEvidenceKind.AffectedTest : ContextEvidenceKind.Declaration,
                (int)score, content, relative, Hash(content), 1, score,
                ["profile-seed", .. matchingTerms.Select(term => $"filename:{term}")]));
        }

        foreach (var term in terms)
        foreach (var declaration in index.FindDeclarations(term, 8))
        {
            var source = await index.GetDeclarationSourceAsync(declaration.Id, 4_000, cancellationToken);
            var score = 40d + MatchScore(task, terms, declaration.Name, declaration.QualifiedName, source.Content);
            Add(candidates, new ContextEvidence(ContextEvidenceKind.Declaration, (int)score,
                source.Content, Relative(repositoryRoot, source.FilePath), Hash(source.Content), source.LineNumber,
                score, ["roslyn-declaration", $"term:{term}"]));
        }

        foreach (var path in EnumerateSearchFiles(repositoryRoot).Take(2_500))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lines = await File.ReadAllLinesAsync(path, cancellationToken);
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var matched = terms.Where(term => lines[lineIndex].Contains(term,
                    StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (matched.Length == 0) continue;
                var start = Math.Max(0, lineIndex - 2); var end = Math.Min(lines.Length, lineIndex + 4);
                var excerpt = string.Join('\n', lines[start..end]);
                var relative = Relative(repositoryRoot, path);
                var isTest = relative.Replace('\\', '/').Contains("/test/", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(relative).Contains("Test", StringComparison.OrdinalIgnoreCase);
                var score = 20d + matched.Length * 8 + (isTest ? 8 : 0) +
                    (Path.GetFileNameWithoutExtension(path).Contains(matched[0], StringComparison.OrdinalIgnoreCase) ? 10 : 0);
                Add(candidates, new ContextEvidence(isTest ? ContextEvidenceKind.AffectedTest : ContextEvidenceKind.Reference,
                    (int)score, excerpt, relative, Hash(excerpt), start + 1, score,
                    ["repository-text", .. matched.Select(term => $"term:{term}")]));
                if (candidates.Count >= 240) break;
            }
            if (candidates.Count >= 240) break;
        }

        var database = Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "knowledge.db");
        if (File.Exists(database))
        {
            using var memory = new EngineeringMemoryStore(database);
            foreach (var term in terms.Take(8))
            foreach (var entry in memory.Search(term, maximumResults: 4))
            {
                var content = $"{entry.Title}\n{entry.Body}"; var score = 32d;
                Add(candidates, new ContextEvidence(ContextEvidenceKind.EngineeringMemory, (int)score,
                    content, $"memory://{entry.Id}", Hash(content), RetrievalScore: score,
                    ScoreReasons: ["engineering-memory", $"term:{term}"]));
            }
        }

        var feedback = GatewayLearningStore.TryReadWeights(Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "index.sqlite"));
        var scored = candidates.Values.Select(WithStableId).Select(item => ApplySubsystemScore(item, profiles, classification))
            .Select(item => feedback.TryGetValue(item.EvidenceId!, out var weight)
            ? item with { RetrievalScore = (item.RetrievalScore ?? item.Priority) + weight,
                ScoreReasons = [.. item.ScoreReasons ?? [], $"feedback:{weight:+0;-0}"] } : item);
        var ordered = scored.OrderByDescending(item => item.RetrievalScore)
            .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
            .GroupBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
            // A task packet benefits more from a second subsystem or focused test than from a
            // second lexical view of the same file. The complete declaration can still be
            // requested explicitly by symbol ID.
            .Select(group => group.First())
            .OrderByDescending(item => item.RetrievalScore)
            .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase).Take(64).ToArray();
        return new(profiles.Version, task, ordered, candidates.Count, classification);
    }

    private static void Add(IDictionary<string, ContextEvidence> candidates, ContextEvidence evidence)
    {
        var key = $"{evidence.Kind}|{evidence.Source}|{evidence.LineNumber}|{Hash(evidence.Content)}";
        if (!candidates.TryGetValue(key, out var existing) || evidence.RetrievalScore > existing.RetrievalScore)
            candidates[key] = evidence;
    }

    private static ContextEvidence ApplySubsystemScore(ContextEvidence item, RetrievalProfileDocument profiles,
        SubsystemClassification classification)
    {
        if (!classification.Confident || classification.PrimarySubsystem is null) return item;
        var normalized = item.Source.Replace('\\', '/');
        var primaryPaths = profiles.Subsystems[classification.PrimarySubsystem];
        var inPrimary = primaryPaths.Any(path => normalized.Contains(path.Replace('\\', '/'),
            StringComparison.OrdinalIgnoreCase));
        var inOther = profiles.Subsystems.Where(group => group.Key != classification.PrimarySubsystem)
            .Any(group => group.Value.Any(path => normalized.Contains(path.Replace('\\', '/'),
                StringComparison.OrdinalIgnoreCase)));
        var adjustment = inPrimary ? 100 : inOther ? -80 : 0;
        return adjustment == 0 ? item : item with
        {
            Priority = item.Priority + adjustment,
            RetrievalScore = (item.RetrievalScore ?? item.Priority) + adjustment,
            ScoreReasons = [.. item.ScoreReasons ?? [], $"subsystem:{classification.PrimarySubsystem}:{adjustment:+0;-0}"]
        };
    }

    private static double MatchScore(string task, IReadOnlyList<string> terms, params string[] values) =>
        terms.Sum(term => values.Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase)) ? 5 : 0) +
        (values.Any(value => task.Contains(value, StringComparison.OrdinalIgnoreCase)) ? 20 : 0);

    private static IEnumerable<string> EnumerateSearchFiles(string root) =>
        new[] { "src", "test", "tasks" }.SelectMany(directory =>
        {
            var path = Path.Combine(root, directory);
            return Directory.Exists(path) ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories) : [];
        }).Where(path => SearchExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) &&
            !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
            !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static ContextEvidence WithStableId(ContextEvidence item)
    {
        var identity = $"{item.Kind}\n{item.Source}\n{item.SourceHash}\n{item.LineNumber}\n{item.Content}";
        return item with { EvidenceId = "evidence-" + Hash(identity) };
    }

    private static IReadOnlyList<string> SplitIdentifier(string value) =>
        System.Text.RegularExpressions.Regex.Matches(value, "[A-Z]+(?=[A-Z][a-z]|$)|[A-Z]?[a-z]+|[0-9]+")
            .Select(match => match.Value.ToLowerInvariant()).Distinct().ToArray();

    private static string FocusContent(string content, string fileName, IReadOnlyList<string> terms, int ceiling)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var index = Array.FindIndex(lines, line => terms.Any(term =>
            line.Contains(term, StringComparison.OrdinalIgnoreCase)));
        if (index < 0) index = Array.FindIndex(lines, line => line.Contains(fileName, StringComparison.OrdinalIgnoreCase));
        if (index < 0) index = 0;
        var start = Math.Max(0, index - 3);
        var excerpt = $"Evidence file: {fileName}\n" + string.Join('\n', lines.Skip(start).Take(18));
        return excerpt.Length <= ceiling ? excerpt : excerpt[..ceiling] + "\n…[focused excerpt truncated]";
    }

    private static string FocusSeedContent(string content, string fileName,
        IReadOnlyList<string> terms, int ceiling)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var matches = lines.Select((line, index) => new
            {
                Line = line.Trim(), Index = index,
                Matches = terms.Count(term => line.Contains(term, StringComparison.OrdinalIgnoreCase))
            })
            .Where(item => item.Matches > 0 && item.Line.Length > 0)
            .OrderByDescending(item => item.Matches).ThenBy(item => item.Index)
            .Select(item => item.Line).Distinct().Take(6);
        var excerpt = $"Evidence file: {fileName}\n{string.Join('\n', matches)}";
        if (excerpt.Equals($"Evidence file: {fileName}\n", StringComparison.Ordinal))
            return FocusContent(content, fileName, terms, ceiling);
        return excerpt.Length <= ceiling ? excerpt : excerpt[..ceiling] + "\n…[focused excerpt truncated]";
    }
}
