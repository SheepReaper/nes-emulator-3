using System.Text.RegularExpressions;

namespace Sheep.Nes.Lab;

public sealed record AccuracyCoinCaseSource(
    string CaseName, string RoutineSymbol, string ResultSymbol, int MenuLine,
    string SourcePath, string SourceSha256, int RoutineLine, string RoutineBody,
    int? EncodedResult, int? ErrorCode, bool? Passed,
    IReadOnlyList<AssemblyReferenceDefinition>? ReferencedDefinitions = null);
public sealed record AssemblyReferenceDefinition(string Symbol, string Kind, int Line, string Body);

public static partial class AccuracyCoinSourceResolver
{
    public static AccuracyCoinCaseSource? Resolve(string repositoryRoot, string caseName, int? encodedResult = null)
    {
        var path = FindSource(repositoryRoot);
        if (path is null) return null;
        var lines = File.ReadAllLines(path);
        var table = TablePattern(caseName);
        var menuIndex = Array.FindIndex(lines, line => table.IsMatch(line));
        if (menuIndex < 0) return null;
        var match = table.Match(lines[menuIndex]);
        var result = match.Groups[1].Value;
        var routine = match.Groups[2].Value;
        var routineIndex = Array.FindIndex(lines, line =>
            line.Trim().Equals(routine + ":", StringComparison.OrdinalIgnoreCase));
        if (routineIndex < 0) return null;
        var end = routineIndex + 1;
        while (end < lines.Length)
        {
            var label = TopLevelLabel().Match(lines[end]);
            if (label.Success && label.Groups[1].Value.StartsWith("TEST_", StringComparison.OrdinalIgnoreCase) &&
                !label.Groups[1].Value.StartsWith(routine + "_", StringComparison.OrdinalIgnoreCase)) break;
            end++;
        }
        var body = string.Join('\n', lines.Skip(routineIndex).Take(end - routineIndex));
        var definitions = ReferencedDefinitions(lines, body);
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        return new AccuracyCoinCaseSource(caseName, routine, result, menuIndex + 1,
            Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'), hash,
            routineIndex + 1, body, encodedResult,
            encodedResult is null ? null : encodedResult.Value >> 2,
            encodedResult is null ? null : (encodedResult.Value & 1) != 0, definitions);
    }

    private static IReadOnlyList<AssemblyReferenceDefinition> ReferencedDefinitions(string[] lines, string body)
    {
        var identifiers = Regex.Matches(body, "\\b[A-Za-z_][A-Za-z0-9_]*\\b")
            .Select(match => match.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<AssemblyReferenceDefinition> result = [];
        for (var index = 0; index < lines.Length && result.Count < 64; index++)
        {
            var constant = Regex.Match(lines[index], "^\\s*([A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*(.+)$");
            if (constant.Success && identifiers.Contains(constant.Groups[1].Value))
                result.Add(new(constant.Groups[1].Value, "constant", index + 1, lines[index].Trim()));
            var macro = Regex.Match(lines[index], "^\\s*([A-Za-z_][A-Za-z0-9_]*)\\s+\\.macro\\b",
                RegexOptions.IgnoreCase);
            if (!macro.Success || !identifiers.Contains(macro.Groups[1].Value)) continue;
            var end = index + 1;
            while (end < lines.Length && !lines[end].Contains(".endm", StringComparison.OrdinalIgnoreCase)) end++;
            end = Math.Min(lines.Length - 1, end);
            result.Add(new(macro.Groups[1].Value, "macro", index + 1,
                string.Join('\n', lines.Skip(index).Take(end - index + 1))));
        }
        return result;
    }

    private static Regex TablePattern(string caseName) => new(
        $"^\\s*table\\s+\"{Regex.Escape(caseName)}\"\\s*,\\s*\\$[0-9A-Fa-f]+\\s*,\\s*([A-Za-z0-9_]+)\\s*,\\s*([A-Za-z0-9_]+)",
        RegexOptions.IgnoreCase);

    [GeneratedRegex("^([A-Za-z_][A-Za-z0-9_]*):")]
    private static partial Regex TopLevelLabel();

    private static string? FindSource(string start)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(start)); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "test-roms", "accuracy-coin", "AccuracyCoin.asm");
            if (File.Exists(path)) return path;
        }
        return null;
    }
}
