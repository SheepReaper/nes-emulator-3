using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Sheep.Nes.Lab;

public enum GitChangeCategory { Committed, Staged, Unstaged, Untracked, Added, Deleted, Renamed }
public sealed record GitDiffHunk(string? OldPath, string? NewPath, string Header, int OldStart, int OldCount,
    int NewStart, int NewCount, GitChangeCategory Category, string Content, string? BaseRevision,
    string? MergeBase, string ContentHash);

public sealed class GitDiffHunkProvider(ICommandExecutor executor, string repositoryRoot)
{
    public async Task<IReadOnlyList<GitDiffHunk>> GetHunksAsync(CancellationToken cancellationToken,
        string? baseRevision = null)
    {
        List<GitDiffHunk> hunks = [];
        string? mergeBase = null;
        if (!string.IsNullOrWhiteSpace(baseRevision))
        {
            mergeBase = (await Git(["merge-base", "HEAD", baseRevision], cancellationToken)).Trim();
            hunks.AddRange(Parse(await Git(["diff", "--no-color", "--find-renames", $"{mergeBase}..HEAD"], cancellationToken),
                GitChangeCategory.Committed, baseRevision, mergeBase));
        }
        hunks.AddRange(Parse(await Git(["diff", "--cached", "--no-color", "--find-renames"], cancellationToken),
            GitChangeCategory.Staged, baseRevision, mergeBase));
        hunks.AddRange(Parse(await Git(["diff", "--no-color", "--find-renames"], cancellationToken),
            GitChangeCategory.Unstaged, baseRevision, mergeBase));
        var untracked = await Git(["ls-files", "--others", "--exclude-standard"], cancellationToken);
        foreach (var relative in untracked.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = relative.Replace('\\', '/');
            var full = Path.GetFullPath(Path.Combine(repositoryRoot, normalized));
            if (!File.Exists(full)) continue;
            var content = await File.ReadAllTextAsync(full, cancellationToken);
            hunks.Add(Create(null, normalized, $"@@ -0,0 +1,{CountLines(content)} @@", 0, 0, 1,
                CountLines(content), GitChangeCategory.Untracked, PrefixAdded(content), baseRevision, mergeBase));
        }
        return hunks;
    }

    public static IReadOnlyList<GitDiffHunk> Parse(string diff, GitChangeCategory category,
        string? baseRevision = null, string? mergeBase = null)
    {
        List<GitDiffHunk> result = [];
        string? oldPath = null, newPath = null;
        var effectiveCategory = category;
        var lines = diff.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("diff --git ", StringComparison.Ordinal))
            {
                var match = Regex.Match(lines[i], "^diff --git a/(.+) b/(.+)$");
                oldPath = match.Success ? match.Groups[1].Value : null;
                newPath = match.Success ? match.Groups[2].Value : null;
                effectiveCategory = category;
            }
            else if (lines[i].StartsWith("rename from ", StringComparison.Ordinal))
            { oldPath = lines[i][12..]; effectiveCategory = GitChangeCategory.Renamed; }
            else if (lines[i].StartsWith("rename to ", StringComparison.Ordinal)) newPath = lines[i][10..];
            else if (lines[i].StartsWith("deleted file mode", StringComparison.Ordinal))
            { effectiveCategory = GitChangeCategory.Deleted; newPath = null; }
            else if (lines[i].StartsWith("new file mode", StringComparison.Ordinal))
            { effectiveCategory = GitChangeCategory.Added; oldPath = null; }
            else if (lines[i].StartsWith("@@ ", StringComparison.Ordinal))
            {
                var header = lines[i];
                var match = Regex.Match(header, "^@@ -(\\d+)(?:,(\\d+))? \\+(\\d+)(?:,(\\d+))? @@");
                if (!match.Success) continue;
                var body = new StringBuilder(header).Append('\n');
                while (i + 1 < lines.Length && !lines[i + 1].StartsWith("@@ ", StringComparison.Ordinal) &&
                    !lines[i + 1].StartsWith("diff --git ", StringComparison.Ordinal)) body.Append(lines[++i]).Append('\n');
                result.Add(Create(oldPath, newPath, header, Number(match, 1), Count(match, 2),
                    Number(match, 3), Count(match, 4), effectiveCategory, body.ToString(), baseRevision, mergeBase));
            }
        }
        return result;
    }

    private async Task<string> Git(string[] arguments, CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync(new VerificationCommand(VerificationScope.LabTests, "git", arguments),
            repositoryRoot, cancellationToken);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.StandardError);
        return result.StandardOutput;
    }
    private static GitDiffHunk Create(string? oldPath, string? newPath, string header, int oldStart, int oldCount,
        int newStart, int newCount, GitChangeCategory category, string content, string? baseRevision, string? mergeBase) =>
        new(oldPath, newPath, header, oldStart, oldCount, newStart, newCount, category, content, baseRevision,
            mergeBase, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
    private static int Number(Match match, int group) => int.Parse(match.Groups[group].Value);
    private static int Count(Match match, int group) => match.Groups[group].Success ? int.Parse(match.Groups[group].Value) : 1;
    private static int CountLines(string text) => text.Length == 0 ? 0 : text.Count(character => character == '\n') + 1;
    private static string PrefixAdded(string text) => string.Join('\n', text.Replace("\r\n", "\n").Split('\n').Select(line => "+" + line));
}
