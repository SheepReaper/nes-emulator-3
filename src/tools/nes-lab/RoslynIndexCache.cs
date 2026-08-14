using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace Sheep.Nes.Lab;

internal sealed record RoslynIndexCacheDocument(
    int SchemaVersion, string Fingerprint, IReadOnlyList<RoslynSymbolDeclaration> Declarations);

internal static class RoslynIndexCache
{
    internal static async Task<IReadOnlyList<RoslynSymbolDeclaration>?> TryReadAsync(
        Solution solution, string solutionPath, CancellationToken cancellationToken)
    {
        var path = CachePath(solutionPath);
        if (!File.Exists(path)) return null;
        var document = JsonSerializer.Deserialize<RoslynIndexCacheDocument>(
            await File.ReadAllTextAsync(path, cancellationToken), LabResponseSerializer.Options);
        if (document is null || document.SchemaVersion != 1 ||
            document.Fingerprint != await FingerprintAsync(solution, cancellationToken).ConfigureAwait(false)) return null;
        return document.Declarations;
    }

    internal static async Task WriteAsync(Solution solution, string solutionPath,
        IReadOnlyList<RoslynSymbolDeclaration> declarations, CancellationToken cancellationToken)
    {
        var path = CachePath(solutionPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var document = new RoslynIndexCacheDocument(1,
            await FingerprintAsync(solution, cancellationToken).ConfigureAwait(false), declarations);
        await File.WriteAllTextAsync(path,
            JsonSerializer.Serialize(document, LabResponseSerializer.Options), cancellationToken);
    }

    private static string CachePath(string solutionPath)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(solutionPath))!;
        return Path.Combine(root, ".artifacts", "nes-lab", "roslyn", "index.json");
    }

    private static async Task<string> FingerprintAsync(Solution solution, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var document in solution.Projects.SelectMany(project => project.Documents)
            .Where(document => document.FilePath is not null).OrderBy(document => document.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            hash.AppendData(Encoding.UTF8.GetBytes(document.FilePath!));
            hash.AppendData([0]);
            hash.AppendData(Encoding.UTF8.GetBytes(text.ToString()));
            hash.AppendData([0]);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
