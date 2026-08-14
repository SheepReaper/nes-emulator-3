using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sheep.Nes.Lab;

public enum ReferenceAvailability { Available, Unavailable, ChangedUpstream, InvalidDigest }
public sealed record ReferenceClaim(string Summary, string Section);
public sealed record ReferenceEntry(string Id, string Title, string CanonicalUrl, string FetchUrl, string Format,
    string Authority, string[] Topics, string[] Aliases, string ExpectedSha256, string UpstreamRevision,
    string LicenseStatus, ReferenceClaim[] Claims);
public sealed record ReferenceManifest(int Version, ReferenceEntry[] Entries);
public sealed record ReferenceStatus(string Id, ReferenceAvailability Availability, string ExpectedSha256,
    string? ActualSha256, string? ResourceUri = null, string? Detail = null);
public sealed record ReferenceDocument(ReferenceEntry Entry, string Content, string Sha256, string? ResourceUri);

public sealed class ReferenceCorpusStore(string manifestPath, string cacheRoot)
{
    private readonly ReferenceManifest manifest = JsonSerializer.Deserialize<ReferenceManifest>(
        File.ReadAllText(manifestPath), LabResponseSerializer.Options) ?? throw new InvalidDataException("Reference manifest is invalid.");

    public async Task<IReadOnlyList<ReferenceStatus>> SyncAsync(HttpClient http, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(cacheRoot); List<ReferenceStatus> statuses = [];
        foreach (var entry in manifest.Entries)
        {
            try
            {
                var raw = await http.GetStringAsync(entry.FetchUrl, cancellationToken);
                var (content, revision) = entry.Format == "mediawiki" ? ParseMediaWiki(raw) : (raw.Replace("\r\n", "\n"), entry.UpstreamRevision);
                var digest = Hash(content);
                if (!digest.Equals(entry.ExpectedSha256, StringComparison.OrdinalIgnoreCase) ||
                    !revision.Equals(entry.UpstreamRevision, StringComparison.OrdinalIgnoreCase))
                { statuses.Add(new(entry.Id, ReferenceAvailability.ChangedUpstream, entry.ExpectedSha256, digest, Detail: $"Expected revision {entry.UpstreamRevision}; received {revision}.")); continue; }
                var path = CachePath(entry); await File.WriteAllTextAsync(path, content, cancellationToken);
                var artifact = await new ImmutableArtifactStore(Path.GetDirectoryName(cacheRoot)!).PublishTextAsync(
                    "reference", content, "text/plain", reproductionCommand: $"nes-lab references sync --id {entry.Id}", cancellationToken: cancellationToken);
                statuses.Add(new(entry.Id, ReferenceAvailability.Available, entry.ExpectedSha256, digest,
                    ImmutableArtifactStore.Uri("reference", artifact.Digest)));
            }
            catch (JsonException exception) { statuses.Add(new(entry.Id, ReferenceAvailability.InvalidDigest, entry.ExpectedSha256, null, Detail: exception.Message)); }
            catch (HttpRequestException exception) { statuses.Add(new(entry.Id, ReferenceAvailability.Unavailable, entry.ExpectedSha256, null, Detail: exception.Message)); }
        }
        return statuses;
    }

    public IReadOnlyList<ReferenceStatus> Status() => manifest.Entries.Select(entry =>
    {
        var path = CachePath(entry); if (!File.Exists(path)) return new ReferenceStatus(entry.Id, ReferenceAvailability.Unavailable, entry.ExpectedSha256, null);
        var digest = Hash(File.ReadAllText(path)); return new ReferenceStatus(entry.Id,
            digest.Equals(entry.ExpectedSha256, StringComparison.OrdinalIgnoreCase) ? ReferenceAvailability.Available : ReferenceAvailability.InvalidDigest,
            entry.ExpectedSha256, digest);
    }).ToArray();

    public ReferenceDocument Show(string id)
    {
        var entry = manifest.Entries.SingleOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? throw new KeyNotFoundException(id);
        var path = CachePath(entry); if (!File.Exists(path)) throw new FileNotFoundException($"Reference '{id}' is unavailable; run references sync.", path);
        var content = File.ReadAllText(path); var digest = Hash(content);
        if (!digest.Equals(entry.ExpectedSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Reference '{id}' has an invalid digest.");
        return new(entry, content, digest, ImmutableArtifactStore.Uri("reference", digest));
    }

    public IReadOnlyList<ReferenceDocument> Search(string query, int maximumResults) => manifest.Entries
        .Where(entry => ($"{entry.Title} {string.Join(' ', entry.Topics)} {string.Join(' ', entry.Aliases)} {string.Join(' ', entry.Claims.Select(c => c.Summary))}")
            .Contains(query, StringComparison.OrdinalIgnoreCase) || query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(term => entry.Topics.Concat(entry.Aliases).Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase))))
        .Where(entry => File.Exists(CachePath(entry))).Take(maximumResults).Select(entry => Show(entry.Id)).ToArray();

    public IReadOnlyList<ReferenceEntry> FindEntries(string query, int maximumResults) => manifest.Entries
        .Select(entry => (Entry: entry, Score: query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Count(term => entry.Topics.Concat(entry.Aliases).Append(entry.Title).Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase)))))
        .Where(item => item.Score > 0).OrderByDescending(item => item.Score).Take(maximumResults).Select(item => item.Entry).ToArray();

    public IReadOnlyList<ReferenceEntry> Entries => manifest.Entries;
    private string CachePath(ReferenceEntry entry) => Path.Combine(cacheRoot, entry.Id + ".txt");
    private static (string Content, string Revision) ParseMediaWiki(string json) { using var document = JsonDocument.Parse(json); var parse = document.RootElement.GetProperty("parse"); return (parse.GetProperty("wikitext").GetString() ?? "", parse.GetProperty("revid").GetInt32().ToString()); }
    private static string Hash(string content) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
