namespace Sheep.Nes.Lab;

public sealed class McpPathPolicy
{
    private readonly string repositoryRoot;
    private readonly string artifactRoot;

    public McpPathPolicy(string repositoryRoot)
    {
        this.repositoryRoot = WithSeparator(Path.GetFullPath(repositoryRoot));
        artifactRoot = WithSeparator(Path.GetFullPath(Path.Combine(repositoryRoot, ".artifacts", "nes-lab")));
    }

    public string ResolveInspectionPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path, repositoryRoot);
        if (!IsWithin(full, repositoryRoot) && !IsWithin(full, artifactRoot))
            throw new UnauthorizedAccessException("MCP paths must remain within the repository or nes-lab artifact root.");
        if ((File.Exists(full) || Directory.Exists(full)) &&
            File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint))
            throw new UnauthorizedAccessException("MCP paths may not target repository reparse points.");
        RejectReparseEscape(full);
        return full;
    }

    private void RejectReparseEscape(string path)
    {
        var current = new FileInfo(path).Directory;
        while (current is not null && IsWithin(current.FullName, repositoryRoot))
        {
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new UnauthorizedAccessException("MCP paths may not traverse repository reparse points.");
            current = current.Parent;
        }
    }

    private static bool IsWithin(string path, string root) =>
        Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
        Path.GetFullPath(path).Equals(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    private static string WithSeparator(string path) => path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
}
