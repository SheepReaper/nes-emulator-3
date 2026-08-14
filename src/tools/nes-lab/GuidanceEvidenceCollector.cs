using System.Security.Cryptography;

namespace Sheep.Nes.Lab;

public static class GuidanceEvidenceCollector
{
    public static async Task<IReadOnlyList<ContextEvidence>> CollectAsync(string repositoryRoot,
        IEnumerable<string> evidencePaths, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(repositoryRoot);
        HashSet<string> guidance = new(StringComparer.OrdinalIgnoreCase);
        var rootFile = Path.Combine(root, "AGENTS.md");
        if (File.Exists(rootFile)) guidance.Add(rootFile);
        foreach (var evidencePath in evidencePaths)
        {
            var full = Path.IsPathRooted(evidencePath) ? Path.GetFullPath(evidencePath) : Path.GetFullPath(Path.Combine(root, evidencePath));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            var directory = File.Exists(full) ? Path.GetDirectoryName(full) : Path.GetDirectoryName(full);
            while (directory is not null && directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                var candidate = Path.Combine(directory, "AGENTS.md"); if (File.Exists(candidate)) guidance.Add(candidate);
                if (directory.Equals(root, StringComparison.OrdinalIgnoreCase)) break; directory = Path.GetDirectoryName(directory);
            }
        }
        List<ContextEvidence> result = [];
        foreach (var path in guidance.Order(StringComparer.OrdinalIgnoreCase))
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            if (content.Length > 1_200) content = content[..1_200] + "\n…[guidance truncated]";
            result.Add(new(ContextEvidenceKind.Guidance, 25, content, Path.GetRelativePath(root, path).Replace('\\', '/'),
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))));
        }
        return result;
    }
}
