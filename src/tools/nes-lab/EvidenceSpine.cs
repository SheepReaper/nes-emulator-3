namespace Sheep.Nes.Lab;

public enum EvidenceSpineStatus { Complete, Excerpted, Missing, Unavailable, Ambiguous, BudgetExcluded }

public sealed record EvidenceSpineAccounting(string Category, EvidenceSpineStatus Status,
    string? EvidenceId, string Detail);

public static class EvidenceSpine
{
    private static readonly (string Category, ContextEvidenceKind Kind)[] Categories =
    [
        ("implementation", ContextEvidenceKind.Declaration),
        ("test", ContextEvidenceKind.AffectedTest),
        ("contract", ContextEvidenceKind.Reference)
    ];

    public static IReadOnlyList<ContextEvidence> Apply(string task, IReadOnlyList<ContextEvidence> evidence,
        SubsystemClassification? classification)
    {
        var original = evidence.ToArray();
        List<ContextEvidence> result = [.. original];
        foreach (var (category, kind) in Categories)
        {
            var candidate = original.Where(item => item.Kind == kind &&
                    (category != "contract" || !item.Source.Equals("task://request", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(item => item.RetrievalScore ?? item.Priority).FirstOrDefault();
            if (candidate is null)
            {
                result.Add(new ContextEvidence(ContextEvidenceKind.Reference, 1_500,
                    $"{category}: no focused evidence was found for this task.", $"spine://{category}",
                    ScoreReasons: [$"spine:{category}", "spine-status:Unavailable"], Required: true));
                continue;
            }
            var compactContent = Compact(candidate.Content);
            Replace(result, candidate, candidate with
            {
                Content = compactContent, Priority = Math.Max(candidate.Priority, 1_500), Required = true,
                ScoreReasons = [$"spine:{category}"]
            });
        }

        var scope = LogicalVerificationScopes.ForTask(task, classification?.PrimarySubsystem);
        result.Add(new ContextEvidence(ContextEvidenceKind.Reference, 1_500,
            $"Smallest verification: verify --scope {scope}", "spine://verification",
            ScoreReasons: ["spine:verification"], Required: true,
            RequiredMarkers: [$"verify --scope {scope}"]));
        return result;
    }

    private static void Replace(List<ContextEvidence> items, ContextEvidence oldItem, ContextEvidence replacement)
    {
        var index = items.IndexOf(oldItem);
        items[index] = replacement;
    }

    private static string Compact(string content)
    {
        if (content.Length <= 180) return content;
        var boundary = content.LastIndexOfAny(['\n', ';', '}'], 179);
        return content[..(boundary >= 32 ? boundary + 1 : 180)] + "\n…[spine excerpt]";
    }
}

public static class LogicalVerificationScopes
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
        { "cpu", "ppu", "apu", "dma", "bus", "mapper", "cartridge", "debugger", "winui-video", "winui-audio", "conformance", "lab" };

    public static string ForSubsystem(string? subsystem) => subsystem?.ToLowerInvariant() switch
    {
        "lab" => "lab-tests",
        { } value when Supported.Contains(value) => value,
        _ => "all"
    };

    public static string ForTask(string task, string? subsystem)
    {
        if (subsystem?.Equals("winui", StringComparison.OrdinalIgnoreCase) == true)
            return task.Contains("audio", StringComparison.OrdinalIgnoreCase) ||
                   task.Contains("audiograph", StringComparison.OrdinalIgnoreCase) ||
                   task.Contains("sound", StringComparison.OrdinalIgnoreCase)
                ? "winui-audio" : "winui-video";
        return ForSubsystem(subsystem);
    }
}
