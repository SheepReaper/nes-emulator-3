namespace Sheep.Nes.Lab;

public sealed record EvidenceCandidate(
    string Id,
    string Kind,
    string Content,
    string Source,
    int Priority);

public sealed record LocalModelSelection(
    IReadOnlyList<string> SelectedIds,
    string Summary);

public sealed record EvidenceRankingResult(
    IReadOnlyList<EvidenceCandidate> Selected,
    string? Summary,
    bool ModelUsed,
    string? ModelError,
    string? Model,
    string? PromptVersion,
    double? ModelLatencyMilliseconds,
    bool ChangedSelection);

public interface ILocalEvidenceModel
{
    string ModelName => GetType().Name;
    string PromptVersion => "unknown";

    Task<LocalModelSelection> SelectAsync(
        IReadOnlyList<EvidenceCandidate> candidates,
        int maximumResults,
        CancellationToken cancellationToken);
}

public sealed class OptionalEvidenceRanker(ILocalEvidenceModel model)
{
    public async Task<EvidenceRankingResult> RankAsync(
        IReadOnlyList<EvidenceCandidate> candidates,
        int maximumResults,
        bool useLocalModel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        var deterministic = candidates
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Take(maximumResults)
            .ToArray();
        if (!useLocalModel)
            return new EvidenceRankingResult(deterministic, null, false, null, null, null, null, false);

        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var selection = await model.SelectAsync(candidates, maximumResults, cancellationToken)
                .ConfigureAwait(false);
            watch.Stop();
            var byId = candidates.ToDictionary(item => item.Id, StringComparer.Ordinal);
            HashSet<string> seen = new(StringComparer.Ordinal);
            var selected = selection.SelectedIds
                .Where(id => seen.Add(id) && byId.ContainsKey(id))
                .Take(maximumResults)
                .Select(id => byId[id])
                .ToArray();
            if (selected.Length == 0)
                return new EvidenceRankingResult(deterministic, null, false,
                    "The local model selected no valid evidence IDs.", model.ModelName,
                    model.PromptVersion, watch.Elapsed.TotalMilliseconds, false);
            return new EvidenceRankingResult(selected, selection.Summary, true, null,
                model.ModelName, model.PromptVersion, watch.Elapsed.TotalMilliseconds,
                !selected.Select(item => item.Id).SequenceEqual(deterministic.Select(item => item.Id)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new EvidenceRankingResult(deterministic, null, false, exception.Message,
                model.ModelName, model.PromptVersion, null, false);
        }
    }
}
