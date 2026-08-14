using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sheep.Nes.Lab;

public sealed record HostAudioDiagnostics(string? GraphStatus = null, string? DeviceId = null,
    int? QuantumSamples = null, long? RequiredSamples = null, long? SubmittedSamples = null,
    long? Underruns = null, string? Exception = null);
public sealed record HostVideoDiagnostics(int? WindowWidth = null, int? WindowHeight = null,
    string? ScalingMode = null, long? DispatcherBacklog = null, ulong? PresentedFrames = null);
public sealed record HostDiagnosticBundle(int SchemaVersion, string? ApplicationVersion = null,
    string? EmulatorVersion = null, string? RomSha256 = null, int? Mapper = null,
    string? VideoStandard = null, HostAudioDiagnostics? Audio = null, HostVideoDiagnostics? Video = null,
    IReadOnlyList<string>? ArtifactUris = null, IReadOnlyList<string>? RunIds = null,
    string? ScreenshotUri = null, string? ResourceUri = null);
public sealed record HostDiagnosticComparison(string LeftUri, string RightUri,
    IReadOnlyList<string> ChangedFields, long? AudioUnderrunDelta,
    long? PresentedFrameDelta);

public sealed partial class HostDiagnosticsService(string repositoryRoot)
{
    private const int MaximumBundleBytes = 256 * 1024;
    private readonly ImmutableArtifactStore artifacts = new(Path.Combine(Path.GetFullPath(repositoryRoot), ".artifacts", "nes-lab"));

    [GeneratedRegex("^[a-fA-F0-9]{64}$")]
    private static partial Regex DigestPattern();

    public async Task<HostDiagnosticBundle> ImportAsync(string json, CancellationToken cancellationToken = default)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumBundleBytes)
            throw new ArgumentException("Host diagnostic bundle exceeds 256 KiB.");
        var bundle = JsonSerializer.Deserialize<HostDiagnosticBundle>(json, LabResponseSerializer.Options)
            ?? throw new ArgumentException("Host diagnostic bundle is empty.");
        if (bundle.SchemaVersion != 1) throw new ArgumentException("Unsupported host diagnostic schema.");
        if (bundle.RomSha256 is { } digest && !DigestPattern().IsMatch(digest))
            throw new ArgumentException("ROM checksum must be a SHA-256 digest.");
        foreach (var uri in (bundle.ArtifactUris ?? []).Append(bundle.ScreenshotUri).Where(uri => uri is not null))
            if (!ImmutableArtifactStore.TryParseUri(uri!, out _, out _))
                throw new ArgumentException($"Host diagnostic artifact URI is not immutable: {uri}");
        var canonical = JsonSerializer.Serialize(bundle with { ResourceUri = null }, LabResponseSerializer.Options);
        var metadata = await artifacts.PublishTextAsync("host-diagnostics", canonical,
            "application/vnd.nes-lab.host-diagnostics+json", pinned: true,
            reproductionCommand: "nes-lab host diagnostics import --json <bundle>", cancellationToken: cancellationToken);
        return bundle with { ResourceUri = ImmutableArtifactStore.Uri("host-diagnostics", metadata.Digest) };
    }

    public async Task<HostDiagnosticBundle> ShowAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (!ImmutableArtifactStore.TryParseUri(uri, out var kind, out var digest) || kind != "host-diagnostics")
            throw new ArgumentException("An immutable host-diagnostics URI is required.");
        var resource = await artifacts.ReadAsync(kind, digest, cancellationToken);
        return (JsonSerializer.Deserialize<HostDiagnosticBundle>(resource.Text!, LabResponseSerializer.Options)
            ?? throw new InvalidDataException("Host diagnostic bundle is invalid.")) with { ResourceUri = uri };
    }

    public async Task<HostDiagnosticComparison> CompareAsync(string leftUri, string rightUri,
        CancellationToken cancellationToken = default)
    {
        var left = await ShowAsync(leftUri, cancellationToken); var right = await ShowAsync(rightUri, cancellationToken);
        List<string> changed = [];
        Add(changed, "romSha256", left.RomSha256, right.RomSha256);
        Add(changed, "mapper", left.Mapper, right.Mapper);
        Add(changed, "audio.graphStatus", left.Audio?.GraphStatus, right.Audio?.GraphStatus);
        Add(changed, "audio.deviceId", left.Audio?.DeviceId, right.Audio?.DeviceId);
        Add(changed, "audio.underruns", left.Audio?.Underruns, right.Audio?.Underruns);
        Add(changed, "video.presentedFrames", left.Video?.PresentedFrames, right.Video?.PresentedFrames);
        return new(leftUri, rightUri, changed,
            Difference(left.Audio?.Underruns, right.Audio?.Underruns),
            Difference(left.Video?.PresentedFrames, right.Video?.PresentedFrames));
    }

    private static void Add<T>(List<string> changed, string field, T left, T right)
    { if (!EqualityComparer<T>.Default.Equals(left, right)) changed.Add(field); }
    private static long? Difference(long? left, long? right) => left.HasValue && right.HasValue ? right - left : null;
    private static long? Difference(ulong? left, ulong? right) => left.HasValue && right.HasValue
        ? checked((long)right.Value - (long)left.Value) : null;
}
