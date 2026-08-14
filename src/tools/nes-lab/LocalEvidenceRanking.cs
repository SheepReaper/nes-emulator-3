using System.Security.Cryptography;
using System.Text;

namespace Sheep.Nes.Lab;

public static class LocalEvidenceRanking
{
    public sealed record Provenance(
        bool ModelUsed, string? ModelError, string? Model, string? PromptVersion,
        double? ModelLatencyMilliseconds, bool ChangedSelection,
        int SelectedCount, IReadOnlyList<string> SelectedIds);

    public static async Task<(IReadOnlyList<ContextEvidence> Evidence, EvidenceRankingResult Ranking)> ApplyAsync(
        IReadOnlyList<ContextEvidence> evidence, bool enabled, string model, string endpoint,
        CancellationToken cancellationToken = default)
    {
        var candidates = evidence.Select((item, index) => new EvidenceCandidate(
            Id(item, index), item.Kind.ToString(), item.Content, item.Source, item.Priority)).ToArray();
        using var http = new HttpClient { BaseAddress = new Uri(endpoint), Timeout = TimeSpan.FromMinutes(5) };
        var ranking = await new OptionalEvidenceRanker(new OllamaEvidenceModel(http, model))
            .RankAsync(candidates, candidates.Length, enabled, cancellationToken).ConfigureAwait(false);
        if (!ranking.ModelUsed) return (evidence, ranking);
        var order = ranking.Selected.Select((item, index) => (item.Id, index))
            .ToDictionary(item => item.Id, item => item.index, StringComparer.Ordinal);
        var adjusted = evidence.Select((item, index) => item with
        {
            Priority = item.Priority + (order.TryGetValue(Id(item, index), out var rank)
                ? 1_000 - Math.Min(rank, 999) : 0)
        }).ToArray();
        return (adjusted, ranking);
    }

    private static string Id(ContextEvidence item, int index) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes($"{index}\0{item.Kind}\0{item.Source}\0{item.Content}")))[..24];

    public static Provenance Describe(EvidenceRankingResult result) => new(
        result.ModelUsed, result.ModelError, result.Model, result.PromptVersion,
        result.ModelLatencyMilliseconds, result.ChangedSelection,
        result.Selected.Count, result.Selected.Select(item => item.Id).ToArray());
}
