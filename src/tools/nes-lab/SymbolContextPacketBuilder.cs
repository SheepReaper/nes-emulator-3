using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Sheep.Nes.Lab;

public static class SymbolContextPacketBuilder
{
    public sealed class AmbiguousSymbolException(
        string query, IReadOnlyList<RoslynSymbolDeclaration> candidates)
        : InvalidOperationException($"Symbol '{query}' is ambiguous ({candidates.Count} matches).")
    {
        public IReadOnlyList<RoslynSymbolDeclaration> Candidates { get; } = candidates;
    }

    public static async Task<ContextPacketArtifact> BuildAsync(
        RoslynSymbolIndex index,
        string repositoryRoot,
        string symbolName,
        int budgetBytes,
        CancellationToken cancellationToken = default)
        => ContextPacketBuilder.Build(await CollectAsync(
            index, repositoryRoot, symbolName, cancellationToken).ConfigureAwait(false), budgetBytes);

    public static async Task<IReadOnlyList<ContextEvidence>> CollectAsync(
        RoslynSymbolIndex index,
        string repositoryRoot,
        string symbolName,
        CancellationToken cancellationToken = default)
        => await CollectAsync(index, repositoryRoot, new RoslynSymbolQuery(symbolName), cancellationToken)
            .ConfigureAwait(false);

    public static Task<IReadOnlyList<ContextEvidence>> CollectByIdAsync(
        RoslynSymbolIndex index, string repositoryRoot, string symbolId,
        CancellationToken cancellationToken = default)
    {
        var declaration = index.FindDeclarationById(symbolId);
        return CollectDeclarationsAsync(index, repositoryRoot, [declaration], cancellationToken);
    }

    public static async Task<IReadOnlyList<ContextEvidence>> CollectAsync(
        RoslynSymbolIndex index,
        string repositoryRoot,
        RoslynSymbolQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(index);
        var root = Path.GetFullPath(repositoryRoot);
        var declarations = index.FindDeclarations(query with { MaximumResults = 16 });
        if (declarations.Count == 0)
            throw new KeyNotFoundException($"No symbol matched '{query.Name}'.");
        if (declarations.Count > 1)
            throw new AmbiguousSymbolException(query.Name, declarations);
        return await CollectDeclarationsAsync(index, repositoryRoot, declarations, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ContextEvidence>> CollectDeclarationsAsync(
        RoslynSymbolIndex index, string repositoryRoot,
        IReadOnlyList<RoslynSymbolDeclaration> declarations,
        CancellationToken cancellationToken)
    {
        List<ContextEvidence> evidence = [];
        var root = Path.GetFullPath(repositoryRoot);
        foreach (var guidance in FindGuidance(root, declarations.Select(item => item.FilePath)))
        {
            var content = GuidanceDigest(await File.ReadAllTextAsync(guidance, cancellationToken));
            evidence.Add(new ContextEvidence(ContextEvidenceKind.Guidance, 20, content,
                Path.GetRelativePath(root, guidance), Hash(content)));
        }

        foreach (var declaration in declarations)
        {
            var source = await index.GetDeclarationSourceAsync(declaration.Id, 6_000, cancellationToken);
            evidence.Add(new ContextEvidence(ContextEvidenceKind.Declaration, 100,
                source.Content, Relative(root, source.FilePath), Hash(source.Content), source.LineNumber));
            var references = await index.FindReferencesAsync(declaration.Id, cancellationToken);
            foreach (var reference in references
                .GroupBy(item => (item.FilePath, item.ContainingSymbol)).Select(group => group.First()))
            {
                var kind = IsTest(reference) ? ContextEvidenceKind.AffectedTest : ContextEvidenceKind.Reference;
                var excerpt = await index.GetContainingSourceAsync(reference,
                    kind == ContextEvidenceKind.AffectedTest ? 5_000 : 3_000, cancellationToken);
                evidence.Add(new ContextEvidence(kind, kind == ContextEvidenceKind.AffectedTest ? 90 : 80,
                    excerpt.Content, Relative(root, excerpt.FilePath), Hash(excerpt.Content), excerpt.LineNumber));
            }
        }
        return evidence;
    }

    private static IEnumerable<string> FindGuidance(string root, IEnumerable<string> files)
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        var rootGuidance = Path.Combine(root, "AGENTS.md");
        if (File.Exists(rootGuidance)) paths.Add(rootGuidance);
        foreach (var file in files)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(file));
            while (directory is not null && directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                var candidate = Path.Combine(directory, "AGENTS.md");
                if (File.Exists(candidate)) paths.Add(candidate);
                if (directory.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
                directory = Path.GetDirectoryName(directory);
            }
        }
        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsTest(RoslynSymbolReference reference) =>
        reference.ProjectName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
        reference.FilePath.Replace('\\', '/').Contains("/test/", StringComparison.OrdinalIgnoreCase);

    private static string Relative(string root, string path) => Path.IsPathRooted(path)
        ? Path.GetRelativePath(root, path)
        : path;

    private static string GuidanceDigest(string content)
    {
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).Where(line => line.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        var digest = string.Join('\n', lines);
        return digest.Length <= 1_200 ? digest : digest[..1_200] + "\n…[guidance truncated]";
    }

    private static string Hash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
