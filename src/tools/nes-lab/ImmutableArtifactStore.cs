using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sheep.Nes.Lab;

public sealed record ImmutableArtifactMetadata(
    int SchemaVersion, string Kind, string Digest, string MimeType, long ByteCount,
    DateTimeOffset CreatedAtUtc, bool Pinned, DateTimeOffset? DeletedAtUtc = null,
    string? ReproductionCommand = null);

public sealed record ImmutableArtifactResource(
    ImmutableArtifactMetadata Metadata, string Uri, string? Text, string? Base64,
    string Status = "available");

public sealed class ImmutableArtifactStore(string artifactRoot)
{
    private static readonly Regex KindPattern = new("^[a-z][a-z0-9-]{0,31}$", RegexOptions.Compiled);
    private static readonly Regex DigestPattern = new("^[a-f0-9]{64}$", RegexOptions.Compiled);
    private readonly string root = Path.GetFullPath(Path.Combine(artifactRoot, "objects"));

    public async Task<ImmutableArtifactMetadata> PublishFileAsync(
        string kind, string path, string mimeType, bool pinned = false,
        string? reproductionCommand = null, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var digest = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        stream.Position = 0;
        return await PublishAsync(kind, digest, stream, mimeType, pinned, reproductionCommand, cancellationToken);
    }

    public async Task<ImmutableArtifactMetadata> PublishTextAsync(
        string kind, string text, string mimeType, bool pinned = false,
        string? reproductionCommand = null, CancellationToken cancellationToken = default)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text), writable: false);
        var digest = Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
        return await PublishAsync(kind, digest, stream, mimeType, pinned, reproductionCommand, cancellationToken);
    }

    public async Task<ImmutableArtifactMetadata> PublishBytesAsync(
        string kind, ReadOnlyMemory<byte> bytes, string mimeType, bool pinned = false,
        string? reproductionCommand = null, CancellationToken cancellationToken = default)
    {
        await using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        var digest = Convert.ToHexStringLower(SHA256.HashData(bytes.Span));
        return await PublishAsync(kind, digest, stream, mimeType, pinned, reproductionCommand, cancellationToken);
    }

    public async Task<ImmutableArtifactResource> ReadAsync(
        string kind, string digest, CancellationToken cancellationToken = default)
    {
        Validate(kind, digest);
        var metadataPath = MetadataPath(kind, digest);
        if (!File.Exists(metadataPath)) throw new KeyNotFoundException($"Artifact {kind}/{digest} is not indexed.");
        var metadata = JsonSerializer.Deserialize<ImmutableArtifactMetadata>(
            await File.ReadAllTextAsync(metadataPath, cancellationToken), LabResponseSerializer.Options)
            ?? throw new InvalidDataException("Artifact metadata is invalid.");
        var uri = Uri(kind, digest);
        if (metadata.DeletedAtUtc is not null)
            return new ImmutableArtifactResource(metadata, uri, null, null, "gone");
        var dataPath = DataPath(kind, digest);
        var info = new FileInfo(dataPath);
        if (!info.Exists) throw new InvalidDataException("Artifact bytes are missing without a tombstone.");
        if (info.Length > TraceArtifactReader.MaximumArtifactBytes)
            throw new InvalidDataException("Artifact exceeds the MCP retrieval size limit.");
        var bytes = await File.ReadAllBytesAsync(dataPath, cancellationToken);
        var actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!actual.Equals(digest, StringComparison.Ordinal))
            throw new InvalidDataException("Artifact content digest does not match its immutable URI.");
        var textual = metadata.MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
            metadata.MimeType.Contains("json", StringComparison.OrdinalIgnoreCase);
        return new ImmutableArtifactResource(metadata, uri,
            textual ? Encoding.UTF8.GetString(bytes) : null,
            textual ? null : Convert.ToBase64String(bytes));
    }

    public async Task SetPinnedAsync(string kind, string digest, bool pinned,
        CancellationToken cancellationToken = default)
    {
        var resource = await ReadAsync(kind, digest, cancellationToken);
        await WriteMetadataAsync(resource.Metadata with { Pinned = pinned }, cancellationToken);
    }

    public async Task<int> PruneAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(root)) return 0;
        var count = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*.meta.json", SearchOption.AllDirectories))
        {
            var metadata = JsonSerializer.Deserialize<ImmutableArtifactMetadata>(
                await File.ReadAllTextAsync(path, cancellationToken), LabResponseSerializer.Options);
            if (metadata is null || metadata.Pinned || metadata.DeletedAtUtc is not null || metadata.CreatedAtUtc >= olderThan)
                continue;
            File.Delete(DataPath(metadata.Kind, metadata.Digest));
            await WriteMetadataAsync(metadata with { DeletedAtUtc = DateTimeOffset.UtcNow }, cancellationToken);
            count++;
        }
        return count;
    }

    public IReadOnlyList<ImmutableArtifactMetadata> List(int maximum = 64)
    {
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, "*.meta.json", SearchOption.AllDirectories)
            .Select(path => JsonSerializer.Deserialize<ImmutableArtifactMetadata>(
                File.ReadAllText(path), LabResponseSerializer.Options))
            .OfType<ImmutableArtifactMetadata>()
            .Where(item => item.Pinned || item.DeletedAtUtc is null)
            .OrderByDescending(item => item.Pinned).ThenByDescending(item => item.CreatedAtUtc)
            .Take(maximum).ToArray();
    }

    public static string Uri(string kind, string digest) => $"nes-lab://artifact/{kind}/sha256/{digest}";

    public static bool TryParseUri(string value, out string kind, out string digest)
    {
        kind = digest = "";
        if (!System.Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "nes-lab" ||
            uri.Host != "artifact") return false;
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length != 3 || segments[1] != "sha256") return false;
        kind = segments[0];
        digest = segments[2].ToLowerInvariant();
        return KindPattern.IsMatch(kind) && DigestPattern.IsMatch(digest);
    }

    public async Task<string> ResolveVerifiedDataPathAsync(string uri,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseUri(uri, out var kind, out var digest))
            throw new ArgumentException("A valid immutable artifact URI is required.", nameof(uri));
        var resource = await ReadAsync(kind, digest, cancellationToken).ConfigureAwait(false);
        if (resource.Status != "available") throw new FileNotFoundException($"Artifact '{uri}' is gone.");
        return DataPath(kind, digest);
    }

    private async Task<ImmutableArtifactMetadata> PublishAsync(
        string kind, string digest, Stream source, string mimeType, bool pinned,
        string? reproductionCommand, CancellationToken cancellationToken)
    {
        Validate(kind, digest);
        var directory = Path.Combine(root, kind, digest[..2]);
        Directory.CreateDirectory(directory);
        var dataPath = DataPath(kind, digest);
        if (!File.Exists(dataPath))
        {
            await using var target = new FileStream(dataPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                16 * 1024, FileOptions.Asynchronous);
            await source.CopyToAsync(target, cancellationToken);
        }
        var metadata = new ImmutableArtifactMetadata(1, kind, digest, mimeType,
            new FileInfo(dataPath).Length, DateTimeOffset.UtcNow, pinned, null, reproductionCommand);
        if (File.Exists(MetadataPath(kind, digest)))
        {
            var existing = JsonSerializer.Deserialize<ImmutableArtifactMetadata>(
                await File.ReadAllTextAsync(MetadataPath(kind, digest), cancellationToken), LabResponseSerializer.Options);
            if (existing is not null) metadata = existing with { Pinned = existing.Pinned || pinned };
        }
        await WriteMetadataAsync(metadata, cancellationToken);
        return metadata;
    }

    private async Task WriteMetadataAsync(ImmutableArtifactMetadata metadata, CancellationToken cancellationToken)
    {
        var path = MetadataPath(metadata.Kind, metadata.Digest);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path,
            JsonSerializer.Serialize(metadata, LabResponseSerializer.Options), cancellationToken);
    }

    private string DataPath(string kind, string digest) => Path.Combine(root, kind, digest[..2], digest + ".data");
    private string MetadataPath(string kind, string digest) => Path.Combine(root, kind, digest[..2], digest + ".meta.json");
    private static void Validate(string kind, string digest)
    {
        if (!KindPattern.IsMatch(kind)) throw new ArgumentException("Invalid artifact kind.", nameof(kind));
        if (!DigestPattern.IsMatch(digest)) throw new ArgumentException("Invalid SHA-256 digest.", nameof(digest));
    }
}
