using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed record LocalSynthesisResult(bool ModelUsed, string? Model, string PromptVersion,
    IReadOnlyList<string> Statements, IReadOnlyList<string> CitationIds, double? LatencyMilliseconds,
    string? FallbackReason);

public static class LocalEvidenceSynthesis
{
    public const string PromptVersion = "citation-synthesis-v1";
    public static async Task<LocalSynthesisResult> RunAsync(IReadOnlyList<ContextEvidence> evidence,
        bool enabled, string model, string endpoint, CancellationToken cancellationToken = default)
    {
        if (!enabled) return new(false, null, PromptVersion, [], [], null, "disabled");
        var cited = evidence.Where(item => item.EvidenceId is not null).Select(item => new
            { id = item.EvidenceId, item.Kind, item.Source, item.Content }).ToArray();
        var schema = new { type = "object", properties = new { statements = new { type = "array", items = new { type = "string" } },
            citationIds = new { type = "array", items = new { type = "string" }, uniqueItems = true } },
            required = new[] { "statements", "citationIds" } };
        var watch = Stopwatch.StartNew();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(endpoint), Timeout = TimeSpan.FromMinutes(5) };
            var request = new { model, stream = false, think = false, format = schema,
                messages = new[] { new { role = "user", content = "Draft a concise diagnosis. Every statement must be supported by one or more supplied evidence IDs. Return no uncited facts.\n" + JsonSerializer.Serialize(cited, LabResponseSerializer.Options) } } };
            using var response = await http.PostAsJsonAsync("api/chat", request, LabResponseSerializer.Options, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var envelope = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            using var payload = JsonDocument.Parse(envelope.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "{}");
            var statements = payload.RootElement.GetProperty("statements").EnumerateArray().Select(item => item.GetString() ?? "").Where(item => item.Length > 0).ToArray();
            var ids = payload.RootElement.GetProperty("citationIds").EnumerateArray().Select(item => item.GetString() ?? "").ToArray();
            var known = cited.Select(item => item.id!).ToHashSet(StringComparer.Ordinal);
            if (statements.Length == 0 || ids.Length == 0 || ids.Any(id => !known.Contains(id)))
                throw new InvalidDataException("Local synthesis returned missing or unknown evidence citations.");
            watch.Stop(); return new(true, model, PromptVersion, statements, ids, watch.Elapsed.TotalMilliseconds, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { watch.Stop(); return new(false, model, PromptVersion, [], [], watch.Elapsed.TotalMilliseconds, exception.Message); }
    }
}
