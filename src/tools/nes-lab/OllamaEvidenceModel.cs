using System.Net.Http.Json;
using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed class OllamaEvidenceModel(HttpClient httpClient, string modelName) : ILocalEvidenceModel
{
    public const string CurrentPromptVersion = "evidence-ranker-v2";
    public string ModelName => modelName;
    public string PromptVersion => CurrentPromptVersion;

    public async Task<LocalModelSelection> SelectAsync(
        IReadOnlyList<EvidenceCandidate> candidates,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                selectedIds = new
                {
                    type = "array",
                    items = new { type = "string" },
                    maxItems = maximumResults,
                    uniqueItems = true
                },
                summary = new { type = "string" }
            },
            required = new[] { "selectedIds", "summary" }
        };
        var evidence = JsonSerializer.Serialize(candidates, LabResponseSerializer.Options);
        var prompt = $"""
            Select at most {maximumResults} evidence items that best support the current diagnosis.
            Return selectedIds strongest-first and a concise summary grounded only in selected items.
            Rank direct run-specific evidence that identifies a divergence or cause (such as a trace
            window) before general reference facts. A generic observation that merely restates a symptom
            is not failure-localizing and ranks below an applicable confirmed fact. Rank confirmed facts
            before ordinary observations or hypotheses. Use priority to order evidence of otherwise
            comparable diagnostic specificity.
            Include each selected item's source identifier verbatim in the summary.
            Do not return unknown IDs. Evidence JSON follows:
            {evidence}
            """;
        var request = new
        {
            model = modelName,
            stream = false,
            think = false,
            keep_alive = "10m",
            format = schema,
            messages = new[] { new { role = "user", content = prompt } }
        };
        using var response = await httpClient.PostAsJsonAsync(
            "api/chat", request, LabResponseSerializer.Options, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var envelope = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var content = envelope.RootElement.GetProperty("message").GetProperty("content").GetString() ??
            throw new InvalidDataException("Ollama returned an empty message.");
        using var selection = JsonDocument.Parse(content);
        var ids = selection.RootElement.GetProperty("selectedIds").EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
        var summary = selection.RootElement.GetProperty("summary").GetString() ?? string.Empty;
        return new LocalModelSelection(ids, summary);
    }
}
