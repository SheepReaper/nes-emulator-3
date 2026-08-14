using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sheep.Nes.Lab;

public enum ContextEvidenceKind
{
    Guidance,
    Declaration,
    Reference,
    AffectedTest,
    VerificationFailure,
    TraceWindow,
    RomSource,
    EngineeringMemory,
    GitDiff
}

public sealed record ContextEvidence(
    ContextEvidenceKind Kind,
    int Priority,
    string Content,
    string Source,
    string? SourceHash = null,
    int? LineNumber = null,
    double? RetrievalScore = null,
    IReadOnlyList<string>? ScoreReasons = null,
    string? EvidenceId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Required = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? RequiredMarkers = null);

public enum RequiredEvidenceStatus { Complete, Excerpted, Missing, Unavailable }

public sealed record RequiredEvidenceAccounting(
    string EvidenceId, string Source, RequiredEvidenceStatus Status,
    IReadOnlyList<string> MissingMarkers);

public sealed record ContextPacketDensity(
    int CompleteItems, int ExcerptedItems, int TruncatedItems,
    int DuplicateGuidanceItems, int RequiredIncluded, double UsefulEvidencePerKiB);

public sealed record ContextPacketArtifact(
    string Content,
    int BudgetBytes,
    int UsedBytes,
    int IncludedEvidenceCount,
    int OmittedEvidenceCount,
    bool Truncated,
    IReadOnlyList<ContextCategoryAccounting>? Categories = null,
    string? PacketId = null,
    int BudgetTokens = 0,
    int UsedTokens = 0,
    IReadOnlyList<RequiredEvidenceAccounting>? RequiredEvidence = null,
    ContextPacketDensity? Density = null,
    IReadOnlyList<EvidenceSpineAccounting>? EvidenceSpine = null,
    int InputEvidenceCount = 0,
    int UniqueEvidenceCount = 0,
    int DuplicateEvidenceCount = 0);

public sealed record ContextCategoryAccounting(
    string Category, int Available, int Included, int Truncated, int Omitted);

public static class ContextPacketBuilder
{
    private const string TruncationMarker = "\n…[truncated]";

    public static ContextPacketArtifact Build(IEnumerable<ContextEvidence> evidence, int budgetBytes, int? budgetTokens = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentOutOfRangeException.ThrowIfLessThan(budgetBytes, 128);
        var effectiveBudgetTokens = budgetTokens ?? EstimateTokenBudget(budgetBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(effectiveBudgetTokens, 32);
        var input = evidence.Select(Validate).Select(WithStableId).ToArray();
        var unique = Deduplicate(input);
        var ordered = unique
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.Source, StringComparer.Ordinal)
            .ThenBy(item => item.LineNumber)
            .ToArray();
        List<ContextEvidence> included = [];
        HashSet<ContextEvidence> selected = [];
        HashSet<string> protectedEvidenceIds = new(StringComparer.Ordinal);
        var truncated = false;

        var requiredCandidates = ordered.Where(item => item.Required).ToArray();
        foreach (var candidate in requiredCandidates)
        {
            var contentBudget = Math.Max(160, (budgetBytes - 512) / Math.Max(1, requiredCandidates.Length));
            var bounded = BoundRequiredAtSemanticBoundary(candidate, contentBudget);
            if (!Fits(included.Append(bounded), ordered.Length - included.Count - 1,
                    budgetBytes, effectiveBudgetTokens))
                bounded = ShortenRequiredToFit(candidate, included, ordered.Length - included.Count - 1,
                    budgetBytes, effectiveBudgetTokens);
            if (bounded is null) continue;
            included.Add(bounded);
            selected.Add(candidate);
            protectedEvidenceIds.Add(candidate.EvidenceId!);
            truncated |= bounded.Content != candidate.Content;
        }

        var mandatoryGuidance = ordered
            .Where(item => item.Kind == ContextEvidenceKind.Guidance)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Source, StringComparer.Ordinal)
            .ThenBy(item => item.LineNumber)
            .FirstOrDefault();

        if (mandatoryGuidance is not null)
        {
            var guidanceBudget = Math.Max(96, Math.Min(256, budgetBytes / 2));
            var guidanceCandidate = BoundAtSemanticBoundary(mandatoryGuidance, guidanceBudget);
            if (!Fits(included.Append(guidanceCandidate), ordered.Length - included.Count - 1, budgetBytes, effectiveBudgetTokens))
                guidanceCandidate = ShortenToFit(guidanceCandidate, included, ordered.Length - included.Count - 1, budgetBytes, effectiveBudgetTokens) ?? guidanceCandidate;

            if (Fits(included.Append(guidanceCandidate), ordered.Length - included.Count - 1,
                    budgetBytes, effectiveBudgetTokens))
            {
                included.Add(guidanceCandidate);
                selected.Add(mandatoryGuidance);
                truncated |= guidanceCandidate.Content != mandatoryGuidance.Content;
            }
        }

        if (budgetBytes >= 4_096)
        {
            var maximumProtected = budgetBytes >= 16_384 ? 4 : 2;
            var protectedCandidates = ordered.Where(item =>
                !selected.Contains(item) && item.Priority >= 200 &&
                (item.Kind is ContextEvidenceKind.Declaration or ContextEvidenceKind.AffectedTest))
                .Take(maximumProtected).ToArray();
            foreach (var candidate in protectedCandidates)
            {
                var contentBudget = Math.Max(256, budgetBytes / Math.Max(4, protectedCandidates.Length + 2));
                var bounded = BoundAtSemanticBoundary(candidate, contentBudget);
                if (!Fits(included.Append(bounded), ordered.Length - included.Count - 1,
                        budgetBytes, effectiveBudgetTokens))
                    bounded = ShortenToFit(bounded, included, ordered.Length - included.Count - 1,
                        budgetBytes, effectiveBudgetTokens);
                if (bounded is null) continue;
                included.Add(bounded);
                selected.Add(candidate);
                protectedEvidenceIds.Add(candidate.EvidenceId!);
                truncated |= bounded.Content != candidate.Content;
            }
        }

        foreach (var (category, share) in CategoryShares)
        {
            if (category == "guidance") continue;
            var candidate = ordered.FirstOrDefault(item => Category(item.Kind) == category && !selected.Contains(item));
            if (candidate is null) continue;
            var contentBudget = Math.Max(32, (int)(budgetBytes * share) - 160);
            var bounded = BoundAtSemanticBoundary(candidate, contentBudget);
            if (!Fits(included.Append(bounded), ordered.Length - included.Count - 1, budgetBytes, effectiveBudgetTokens))
                bounded = ShortenToFit(bounded, included, ordered.Length - included.Count - 1, budgetBytes, effectiveBudgetTokens);
            if (bounded is null) continue;
            included.Add(bounded);
            selected.Add(candidate);
            truncated |= bounded.Content != candidate.Content;
        }

        for (var index = 0; index < ordered.Length; index++)
        {
            var maximumItems = Math.Clamp(budgetBytes / 1_200, 5, 12);
            if (included.Count >= maximumItems) break;
            var candidate = ordered[index];
            if (selected.Contains(candidate)) continue;
            if (Fits(included.Append(candidate), ordered.Length - index - 1, budgetBytes, effectiveBudgetTokens))
            {
                included.Add(candidate);
                continue;
            }

            var shortened = ShortenToFit(candidate, included, ordered.Length - index - 1, budgetBytes, effectiveBudgetTokens);
            if (shortened is not null)
            {
                included.Add(shortened);
                truncated = true;
                continue;
            }

            var replaceable = included
                .Where(item => (mandatoryGuidance is null || !string.Equals(item.EvidenceId, mandatoryGuidance.EvidenceId, StringComparison.Ordinal)) &&
                    !protectedEvidenceIds.Contains(item.EvidenceId!))
                .OrderBy(item => item.Priority)
                .ThenBy(item => item.Source, StringComparer.Ordinal)
                .FirstOrDefault();
            if (replaceable is not null)
            {
                included.Remove(replaceable);
                var replacement = ShortenToFit(candidate, included, ordered.Length - index - 1, budgetBytes, effectiveBudgetTokens);
                if (replacement is not null)
                {
                    included.Add(replacement);
                    truncated |= replacement.Content != candidate.Content;
                }
                else included.Add(replaceable);
            }
        }

        var omitted = ordered.Length - included.Count;
        var content = Serialize(included, omitted);
        var mandatoryGuidanceId = mandatoryGuidance?.EvidenceId;
        while ((Encoding.UTF8.GetByteCount(content) > budgetBytes || EstimateTokenCount(content) > effectiveBudgetTokens) && included.Count > 0)
        {
            var removable = included
                .Where(item => (mandatoryGuidanceId is null || !string.Equals(item.EvidenceId, mandatoryGuidanceId, StringComparison.Ordinal)) &&
                    !protectedEvidenceIds.Contains(item.EvidenceId!))
                .OrderBy(item => item.Priority)
                .ThenBy(item => item.Source, StringComparer.Ordinal)
                .LastOrDefault();
            if (removable is null)
            {
                var guidance = included.SingleOrDefault(item => string.Equals(item.EvidenceId, mandatoryGuidanceId, StringComparison.Ordinal));
                if (guidance is null) break;
                var guidanceIndex = included.IndexOf(guidance);
                var trimmed = guidance.Content.Length > 64
                    ? guidance.Content[..64] + TruncationMarker
                    : guidance.Content;
                included[guidanceIndex] = guidance with { Content = trimmed };
                truncated = true;
                content = Serialize(included, omitted);
                break;
            }

            included.Remove(removable);
            omitted++;
            content = Serialize(included, omitted);
        }
        var accounting = CategoryShares.Select(item =>
        {
            var available = ordered.Count(evidence => Category(evidence.Kind) == item.Category);
            var includedInCategory = included.Where(evidence => Category(evidence.Kind) == item.Category).ToArray();
            var truncatedInCategory = includedInCategory.Count(evidence =>
                evidence.Content.EndsWith(TruncationMarker, StringComparison.Ordinal));
            return new ContextCategoryAccounting(item.Category, available, includedInCategory.Length,
                truncatedInCategory, available - includedInCategory.Length);
        }).ToArray();
        var packetId = "packet-" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(content)));
        var usedTokens = EstimateTokenCount(content);
        var requiredAccounting = requiredCandidates.Select(candidate =>
        {
            var retained = included.FirstOrDefault(item => string.Equals(item.EvidenceId, candidate.EvidenceId,
                StringComparison.Ordinal));
            var missingMarkers = (candidate.RequiredMarkers ?? []).Where(marker =>
                retained is null || !retained.Content.Contains(marker, StringComparison.OrdinalIgnoreCase)).ToArray();
            var status = retained is null || missingMarkers.Length > 0
                ? RequiredEvidenceStatus.Missing
                : retained.Content == candidate.Content
                    ? RequiredEvidenceStatus.Complete
                    : RequiredEvidenceStatus.Excerpted;
            return new RequiredEvidenceAccounting(candidate.EvidenceId!, candidate.Source, status, missingMarkers);
        }).ToArray();
        var truncatedItems = included.Count(item => item.Content.EndsWith(TruncationMarker, StringComparison.Ordinal));
        var duplicateGuidance = included.Where(item => item.Kind == ContextEvidenceKind.Guidance)
            .GroupBy(item => item.SourceHash ?? item.Content, StringComparer.Ordinal).Sum(group => Math.Max(0, group.Count() - 1));
        var usefulCount = included.Count(item => item.Kind != ContextEvidenceKind.Guidance);
        var density = new ContextPacketDensity(
            included.Count - truncatedItems, truncatedItems, truncatedItems, duplicateGuidance,
            requiredAccounting.Count(item => item.Status is RequiredEvidenceStatus.Complete or RequiredEvidenceStatus.Excerpted),
            usefulCount / Math.Max(1d, Encoding.UTF8.GetByteCount(content) / 1024d));
        var spine = ordered.SelectMany(item => (item.ScoreReasons ?? [])
                .Where(reason => reason.StartsWith("spine:", StringComparison.Ordinal))
                .Select(reason => (Item: item, Category: reason["spine:".Length..])))
            .GroupBy(item => item.Category, StringComparer.Ordinal)
            .Select(group =>
            {
                var candidate = group.First().Item;
                var retained = included.FirstOrDefault(item => item.EvidenceId == candidate.EvidenceId);
                var declared = candidate.ScoreReasons?.FirstOrDefault(reason =>
                    reason.StartsWith("spine-status:", StringComparison.Ordinal));
                var status = declared is not null
                    ? Enum.Parse<EvidenceSpineStatus>(declared["spine-status:".Length..])
                    : retained is null ? EvidenceSpineStatus.BudgetExcluded
                    : retained.Content == candidate.Content ? EvidenceSpineStatus.Complete
                    : EvidenceSpineStatus.Excerpted;
                return new EvidenceSpineAccounting(group.Key, status, candidate.EvidenceId,
                    status == EvidenceSpineStatus.BudgetExcluded ? "Evidence existed but did not fit the packet budget."
                    : status == EvidenceSpineStatus.Unavailable ? "No focused evidence was found."
                    : candidate.Source);
            }).OrderBy(item => item.Category, StringComparer.Ordinal).ToArray();
        return new ContextPacketArtifact(content, budgetBytes, Encoding.UTF8.GetByteCount(content),
            included.Count, omitted, truncated, accounting, packetId, effectiveBudgetTokens, usedTokens,
            requiredAccounting, density, spine, input.Length, unique.Count, input.Length - unique.Count);
    }

    private static IReadOnlyList<ContextEvidence> Deduplicate(IReadOnlyList<ContextEvidence> input)
    {
        List<ContextEvidence> result = [];
        foreach (var group in input.GroupBy(item => item.EvidenceId!, StringComparer.Ordinal))
        {
            var first = group.First();
            if (group.Any(item => item.Kind != first.Kind || item.Content != first.Content ||
                item.Source != first.Source || item.SourceHash != first.SourceHash ||
                item.LineNumber != first.LineNumber))
                throw new InvalidDataException($"Evidence ID '{group.Key}' identifies conflicting content or provenance.");

            var scoreReasons = group.SelectMany(item => item.ScoreReasons ?? [])
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var requiredMarkers = group.SelectMany(item => item.RequiredMarkers ?? [])
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            result.Add(first with
            {
                Priority = group.Max(item => item.Priority),
                RetrievalScore = group.Max(item => item.RetrievalScore),
                ScoreReasons = scoreReasons.Length == 0 ? null : scoreReasons,
                Required = group.Any(item => item.Required),
                RequiredMarkers = requiredMarkers.Length == 0 ? null : requiredMarkers
            });
        }
        return result;
    }

    private static readonly (string Category, double Share)[] CategoryShares =
    [
        ("failureTrace", .35),
        ("implementation", .40),
        ("tests", .15),
        ("guidance", .05),
        ("supporting", .05)
    ];

    private static string Category(ContextEvidenceKind kind) => kind switch
    {
        ContextEvidenceKind.VerificationFailure or ContextEvidenceKind.TraceWindow => "failureTrace",
        ContextEvidenceKind.Declaration => "implementation",
        ContextEvidenceKind.AffectedTest => "tests",
        ContextEvidenceKind.Guidance => "guidance",
        _ => "supporting"
    };

    private static ContextEvidence BoundAtSemanticBoundary(ContextEvidence candidate, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(candidate.Content) <= maximumBytes) return candidate;
        var maximumCharacters = Math.Min(candidate.Content.Length, maximumBytes);
        while (maximumCharacters > 0 && Encoding.UTF8.GetByteCount(candidate.Content[..maximumCharacters]) > maximumBytes)
            maximumCharacters--;
        var prefix = candidate.Content[..maximumCharacters];
        var boundary = Math.Max(prefix.LastIndexOf('\n'), Math.Max(prefix.LastIndexOf('}'), prefix.LastIndexOf(';')));
        if (boundary >= 16) prefix = prefix[..(boundary + 1)];
        return candidate with { Content = prefix + TruncationMarker };
    }

    private static ContextEvidence BoundRequiredAtSemanticBoundary(ContextEvidence candidate, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(candidate.Content) <= maximumBytes) return candidate;
        var markers = candidate.RequiredMarkers ?? [];
        if (markers.Count == 0) return BoundAtSemanticBoundary(candidate, maximumBytes);
        var lines = candidate.Content.Replace("\r\n", "\n").Split('\n');
        List<string> selected = [$"Evidence file: {Path.GetFileName(candidate.Source)}"];
        foreach (var marker in markers)
        {
            var index = Array.FindIndex(lines, line => line.Contains(marker, StringComparison.OrdinalIgnoreCase));
            if (index < 0) continue;
            selected.AddRange(lines.Skip(Math.Max(0, index - 1)).Take(3));
        }
        var focused = candidate with { Content = string.Join('\n', selected.Distinct()) + TruncationMarker };
        return Encoding.UTF8.GetByteCount(focused.Content) <= maximumBytes
            ? focused : BoundAtSemanticBoundary(focused, maximumBytes);
    }

    private static ContextEvidence? ShortenRequiredToFit(ContextEvidence candidate,
        IReadOnlyList<ContextEvidence> included, int omittedAfterCandidate, int budget, int tokenBudget)
    {
        var focused = BoundRequiredAtSemanticBoundary(candidate, Math.Max(96, budget / 3));
        if (Fits(included.Append(focused), omittedAfterCandidate, budget, tokenBudget)) return focused;
        return ShortenToFit(focused, included, omittedAfterCandidate, budget, tokenBudget);
    }

    private static ContextEvidence Validate(ContextEvidence item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Content);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Source);
        return item;
    }

    private static ContextEvidence WithStableId(ContextEvidence item)
    {
        if (!string.IsNullOrWhiteSpace(item.EvidenceId)) return item;
        var identity = $"{item.Kind}\n{item.Source}\n{item.SourceHash}\n{item.LineNumber}\n{item.Content}";
        return item with { EvidenceId = "evidence-" + Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(identity))) };
    }

    private static ContextEvidence? ShortenToFit(
        ContextEvidence candidate,
        IReadOnlyList<ContextEvidence> included,
        int omittedAfterCandidate,
        int budget,
        int tokenBudget)
    {
        var low = 0;
        var high = candidate.Content.Length;
        ContextEvidence? best = null;
        while (low <= high)
        {
            var length = low + ((high - low) / 2);
            var shortened = candidate with
            {
                Content = candidate.Content[..length] + TruncationMarker
            };
            if (Fits(included.Append(shortened), omittedAfterCandidate, budget, tokenBudget))
            {
                best = shortened;
                low = length + 1;
            }
            else high = length - 1;
        }
        return best;
    }

    private static bool Fits(IEnumerable<ContextEvidence> evidence, int omitted, int budget, int tokenBudget)
    {
        var serialized = Serialize(evidence, omitted);
        return Encoding.UTF8.GetByteCount(serialized) <= budget && EstimateTokenCount(serialized) <= tokenBudget;
    }

    private static int EstimateTokenBudget(int budgetBytes) => Math.Max(32, budgetBytes / 4);

    private static int EstimateTokenCount(string content) =>
        Math.Max(1, (int)Math.Ceiling(Encoding.UTF8.GetByteCount(content) / 4d));

    private static string Serialize(IEnumerable<ContextEvidence> evidence, int omitted) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            evidence,
            omittedEvidenceCount = omitted
        }, LabResponseSerializer.Options);
}
