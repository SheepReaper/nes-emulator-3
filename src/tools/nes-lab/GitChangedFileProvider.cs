namespace Sheep.Nes.Lab;

public sealed class GitChangedFileProvider(ICommandExecutor executor, string repositoryRoot)
{
    public async Task<IReadOnlyList<string>> GetChangedFilesAsync(
        CancellationToken cancellationToken, string? baseRevision = null)
    {
        string committed = "";
        if (!string.IsNullOrWhiteSpace(baseRevision))
        {
            var mergeBase = (await RunGitAsync(
                ["merge-base", "HEAD", baseRevision], cancellationToken).ConfigureAwait(false)).Trim();
            if (mergeBase.Length == 0)
                throw new InvalidOperationException($"Git did not resolve a merge base for '{baseRevision}'.");
            committed = await RunGitAsync(
                ["diff", "--name-only", "--relative", $"{mergeBase}..HEAD"],
                cancellationToken).ConfigureAwait(false);
        }
        var tracked = await RunGitAsync(
            ["diff", "--name-only", "--relative", "HEAD"],
            cancellationToken).ConfigureAwait(false);
        var untracked = await RunGitAsync(
            ["ls-files", "--others", "--exclude-standard"],
            cancellationToken).ConfigureAwait(false);

        List<string> files = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        AddFiles(committed, files, seen, includeAnyPath: true);
        AddFiles(tracked, files, seen, includeAnyPath: true);
        AddFiles(untracked, files, seen, includeAnyPath: false);
        return files;
    }

    private async Task<string> RunGitAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var command = new VerificationCommand(VerificationScope.LabTests, "git", arguments);
        var execution = await executor.ExecuteAsync(command, repositoryRoot, cancellationToken)
            .ConfigureAwait(false);
        if (execution.ExitCode != 0)
        {
            var diagnostic = string.IsNullOrWhiteSpace(execution.StandardError)
                ? execution.StandardOutput.Trim()
                : execution.StandardError.Trim();
            throw new InvalidOperationException(
                $"Git changed-file discovery failed with exit code {execution.ExitCode}: {diagnostic}");
        }

        return execution.StandardOutput;
    }

    private static void AddFiles(
        string output,
        List<string> files,
        HashSet<string> seen,
        bool includeAnyPath)
    {
        foreach (var file in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = file.Trim().Replace('\\', '/');
            if (normalized.Length > 0 &&
                (includeAnyPath || IsRepositoryCandidate(normalized)) &&
                seen.Add(normalized))
                files.Add(normalized);
        }
    }

    private static bool IsRepositoryCandidate(string path) =>
        path.StartsWith("src/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("test/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("tasks/", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("AGENTS.md", StringComparison.OrdinalIgnoreCase) ||
        path.Equals(".gitignore", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("global.json", StringComparison.OrdinalIgnoreCase);
}
