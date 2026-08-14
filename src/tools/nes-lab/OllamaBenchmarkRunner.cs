using System.Diagnostics;
using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed record OllamaBenchmarkFixture(int SchemaVersion, IReadOnlyList<OllamaBenchmarkCase> Cases);
public sealed record OllamaBenchmarkCase(
    string Name,
    IReadOnlyList<EvidenceCandidate> Candidates,
    int MaximumResults,
    IReadOnlyList<string> ExpectedSelectedIds,
    IReadOnlyList<string> RequiredSummaryTerms);
public sealed record OllamaBenchmarkCaseResult(
    string Name,
    bool Passed,
    IReadOnlyList<string> SelectedIds,
    string Summary,
    double DurationMilliseconds,
    IReadOnlyList<string> Failures);
public sealed record OllamaBenchmarkResult(
    int SchemaVersion,
    string Model,
    int Passed,
    int Total,
    double DurationMilliseconds,
    IReadOnlyList<OllamaBenchmarkCaseResult> Cases);

public static class OllamaBenchmarkRunner
{
    public static async Task<OllamaBenchmarkResult> RunAsync(
        ILocalEvidenceModel model,
        string modelName,
        OllamaBenchmarkFixture fixture,
        CancellationToken cancellationToken = default)
    {
        List<OllamaBenchmarkCaseResult> results = [];
        var totalWatch = Stopwatch.StartNew();
        foreach (var testCase in fixture.Cases)
        {
            var watch = Stopwatch.StartNew();
            var selection = await model.SelectAsync(
                testCase.Candidates, testCase.MaximumResults, cancellationToken).ConfigureAwait(false);
            watch.Stop();
            List<string> failures = [];
            if (!selection.SelectedIds.SequenceEqual(testCase.ExpectedSelectedIds))
                failures.Add($"Expected [{string.Join(",", testCase.ExpectedSelectedIds)}].");
            foreach (var term in testCase.RequiredSummaryTerms.Where(term =>
                         !selection.Summary.Contains(term, StringComparison.OrdinalIgnoreCase)))
                failures.Add($"Summary omitted '{term}'.");
            results.Add(new OllamaBenchmarkCaseResult(testCase.Name, failures.Count == 0,
                selection.SelectedIds, selection.Summary, watch.Elapsed.TotalMilliseconds, failures));
        }
        totalWatch.Stop();
        return new OllamaBenchmarkResult(fixture.SchemaVersion, modelName,
            results.Count(item => item.Passed), results.Count, totalWatch.Elapsed.TotalMilliseconds, results);
    }

    public static OllamaBenchmarkFixture Load(string path) =>
        JsonSerializer.Deserialize<OllamaBenchmarkFixture>(File.ReadAllText(path), LabResponseSerializer.Options) ??
        throw new InvalidDataException($"Invalid Ollama benchmark fixture '{path}'.");
}
