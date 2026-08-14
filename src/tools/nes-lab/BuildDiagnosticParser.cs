using System.Text.RegularExpressions;

namespace Sheep.Nes.Lab;

public enum BuildDiagnosticCategory { Compiler, Project, Package, TargetFramework, GeneratedSource, LockedFile, Unknown }

public sealed record BuildDiagnostic(string? Project, string? File, int? Line, int? Column,
    string Code, string Message, BuildDiagnosticCategory Category, int Occurrences);

public sealed record BuildDiagnosticReport(IReadOnlyList<BuildDiagnostic> Diagnostics,
    BuildDiagnostic? EarliestActionable, int TotalDiagnostics = 0, bool Truncated = false);

public static partial class BuildDiagnosticParser
{
    [GeneratedRegex(@"^(?:(?<file>.+?)(?:\((?<line>\d+)(?:,(?<column>\d+))?\))?\s*:\s*)?error\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.*?)(?:\s+\[(?<project>[^\]]+)\])?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DiagnosticPattern();

    public static BuildDiagnosticReport Parse(string output)
    {
        var parsed = output.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => DiagnosticPattern().Match(line.Trim())).Where(match => match.Success)
            .Select(match => Create(match)).ToArray();
        var grouped = parsed.GroupBy(item => new { item.Project, item.File, item.Line, item.Column, item.Code, item.Message, item.Category })
            .Select(group => new BuildDiagnostic(group.Key.Project, group.Key.File, group.Key.Line, group.Key.Column,
                group.Key.Code, group.Key.Message, group.Key.Category, group.Count())).ToArray();
        var actionable = grouped.FirstOrDefault(item => item.Category != BuildDiagnosticCategory.GeneratedSource)
            ?? grouped.FirstOrDefault();
        return new(grouped.Take(64).ToArray(), actionable, grouped.Length, grouped.Length > 64);
    }

    private static BuildDiagnostic Create(Match match)
    {
        var file = Value(match, "file"); var project = Value(match, "project");
        var code = match.Groups["code"].Value.ToUpperInvariant();
        var message = match.Groups["message"].Value.Trim();
        var category = message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
            ? BuildDiagnosticCategory.LockedFile
            : file?.Contains("obj", StringComparison.OrdinalIgnoreCase) == true || file?.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) == true
                ? BuildDiagnosticCategory.GeneratedSource
                : code.StartsWith("CS", StringComparison.Ordinal) ? BuildDiagnosticCategory.Compiler
                : code.StartsWith("NU", StringComparison.Ordinal) ? BuildDiagnosticCategory.Package
                : message.Contains("target framework", StringComparison.OrdinalIgnoreCase) ? BuildDiagnosticCategory.TargetFramework
                : code.StartsWith("MSB", StringComparison.Ordinal) ? BuildDiagnosticCategory.Project
                : BuildDiagnosticCategory.Unknown;
        return new(project, file, Number(match, "line"), Number(match, "column"), code, message, category, 1);
    }

    private static string? Value(Match match, string group) => match.Groups[group].Success
        ? match.Groups[group].Value.Trim() : null;
    private static int? Number(Match match, string group) => int.TryParse(Value(match, group), out var value) ? value : null;
}
