using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed record RetrievalProfileDocument(int Version,
    IReadOnlyDictionary<string, string[]> Aliases,
    IReadOnlyDictionary<string, string[]> Subsystems,
    IReadOnlyDictionary<string, string[]> Routes,
    IReadOnlyDictionary<string, string[]>? Seeds = null);
public sealed record SubsystemClassification(string? PrimarySubsystem, bool Confident,
    IReadOnlyDictionary<string, double> Scores, IReadOnlyList<string> Reasons);

public static class RetrievalProfiles
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        { "the", "and", "for", "with", "from", "instead", "into", "wrong", "number", "remain", "selected", "but" };
    public static RetrievalProfileDocument Load(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, "src", "tools", "nes-lab", "retrieval-profiles.v1.json");
        return JsonSerializer.Deserialize<RetrievalProfileDocument>(File.ReadAllText(path),
            LabResponseSerializer.Options) ?? throw new InvalidDataException("Retrieval profiles are invalid.");
    }

    public static IReadOnlyList<string> Expand(RetrievalProfileDocument profiles, string task)
    {
        HashSet<string> terms = new(StringComparer.OrdinalIgnoreCase);
        foreach (var term in Tokenize(task)) terms.Add(term);
        foreach (var (phrase, aliases) in profiles.Aliases)
            if (task.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                foreach (var alias in aliases.SelectMany(Tokenize)) terms.Add(alias);
        return terms.Take(48).ToArray();
    }

    public static IReadOnlyList<string> MatchSubsystemPaths(RetrievalProfileDocument profiles, string task)
    {
        var terms = Expand(profiles, task);
        return profiles.Subsystems
            .Where(profile => terms.Contains(profile.Key, StringComparer.OrdinalIgnoreCase) ||
                profiles.Routes.TryGetValue(profile.Key, out var routeTerms) &&
                    routeTerms.Any(term => terms.Contains(term, StringComparer.OrdinalIgnoreCase)) ||
                profile.Value.Any(path => terms.Any(term => path.Contains(term, StringComparison.OrdinalIgnoreCase))))
            .SelectMany(profile => profile.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static SubsystemClassification Classify(RetrievalProfileDocument profiles, string task)
    {
        var terms = Expand(profiles, task);
        Dictionary<string, double> scores = profiles.Subsystems.Keys.ToDictionary(key => key, _ => 0d,
            StringComparer.OrdinalIgnoreCase);
        List<string> reasons = [];
        foreach (var subsystem in profiles.Subsystems.Keys)
        {
            if (task.Contains(subsystem, StringComparison.OrdinalIgnoreCase)) scores[subsystem] += 10;
            if (profiles.Routes.TryGetValue(subsystem, out var routes))
                scores[subsystem] += routes.Count(route => terms.Contains(route, StringComparer.OrdinalIgnoreCase)) * 2;
            foreach (var path in profiles.Subsystems[subsystem])
            {
                if (!task.Contains(path, StringComparison.OrdinalIgnoreCase)) continue;
                scores[subsystem] += 20; reasons.Add($"explicit-path:{path}");
            }
        }
        var namedPaths = System.Text.RegularExpressions.Regex.Matches(task,
            @"(?:src|test)[/\\][A-Za-z0-9_./\\-]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Select(match => match.Value.Replace('\\', '/')).ToArray();
        foreach (var named in namedPaths)
        foreach (var subsystem in profiles.Subsystems)
            if (subsystem.Value.Any(path => named.Contains(path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)))
            { scores[subsystem.Key] += 30; reasons.Add($"explicit-path:{named}"); }
        var ordered = scores.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal).ToArray();
        var primary = ordered[0].Value > 0 ? ordered[0].Key : null;
        var confident = primary is not null && ordered[0].Value >= 6 &&
            (ordered.Length == 1 || ordered[0].Value >= ordered[1].Value * 1.5);
        if (primary is not null) reasons.Add($"primary:{primary}:{ordered[0].Value:0.#}");
        return new(primary, confident, scores, reasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    public static IReadOnlyList<string> MatchSeedPaths(RetrievalProfileDocument profiles, string task) =>
        (profiles.Seeds ?? new Dictionary<string, string[]>())
            .Where(item => task.Contains(item.Key, StringComparison.OrdinalIgnoreCase))
            .SelectMany(item => item.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IEnumerable<string> Tokenize(string text) => text
        .Replace("$", "", StringComparison.Ordinal).Replace("/", " ", StringComparison.Ordinal)
        .Split([' ', '\t', '\r', '\n', '.', ',', ':', ';', '(', ')', '-', '_'],
            StringSplitOptions.RemoveEmptyEntries)
        .Select(term => term.Trim()).Where(term => term.Length >= 3 && !StopWords.Contains(term));
}
