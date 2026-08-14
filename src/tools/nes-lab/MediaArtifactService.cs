namespace Sheep.Nes.Lab;

public sealed record FrameArtifactComparison(FrameComparisonResult Comparison, string LeftUri,
    string RightUri, string? HeatmapUri, ulong? PpuDot = null, ulong? CpuClock = null,
    string? CaptureId = null);
public sealed record AudioArtifactAnalysis(AudioAnalysisResult Analysis, string Uri,
    ulong? StartCpuClock = null, ulong? EndCpuClock = null, string? CaptureId = null);
public sealed record AudioArtifactComparison(AudioComparisonResult Comparison, string LeftUri,
    string RightUri, ulong? StartCpuClock = null, ulong? EndCpuClock = null,
    string? CaptureId = null);

public sealed class MediaArtifactService(string repositoryRoot)
{
    private readonly ImmutableArtifactStore store = new(Path.Combine(Path.GetFullPath(repositoryRoot),
        ".artifacts", "nes-lab"));

    public async Task<FrameArtifactComparison> CompareFramesAsync(string leftUri, string rightUri,
        bool heatmap = false, IReadOnlyList<FrameRegion>? regions = null,
        CancellationToken cancellationToken = default)
    {
        var left = await ReadBytesAsync(leftUri, "frame", cancellationToken);
        var right = await ReadBytesAsync(rightUri, "frame", cancellationToken);
        var comparison = MediaArtifactAnalyzer.CompareFrames(left, right, regions, createHeatmap: heatmap);
        string? heatmapUri = null;
        if (comparison.HeatmapRgba is not null)
        {
            var metadata = await store.PublishBytesAsync("frame-diff", comparison.HeatmapRgba,
                "application/vnd.nes-lab.rgba8888", reproductionCommand:
                $"nes-lab media frame compare --left {leftUri} --right {rightUri} --heatmap",
                cancellationToken: cancellationToken);
            heatmapUri = ImmutableArtifactStore.Uri("frame-diff", metadata.Digest);
        }
        return new(comparison with { HeatmapRgba = null }, leftUri, rightUri, heatmapUri);
    }

    public async Task<AudioArtifactAnalysis> AnalyzeAudioAsync(string uri, int sampleRate = 48_000,
        int windowSize = 1024, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadBytesAsync(uri, "audio", cancellationToken);
        return new(MediaArtifactAnalyzer.AnalyzeAudio(MediaArtifactAnalyzer.DecodeFloat32(bytes),
            sampleRate, windowSize), uri);
    }

    public async Task<AudioArtifactComparison> CompareAudioAsync(string leftUri, string rightUri,
        AudioComparisonTolerance? tolerance = null, int sampleRate = 48_000,
        CancellationToken cancellationToken = default)
    {
        var left = MediaArtifactAnalyzer.DecodeFloat32(await ReadBytesAsync(leftUri, "audio", cancellationToken));
        var right = MediaArtifactAnalyzer.DecodeFloat32(await ReadBytesAsync(rightUri, "audio", cancellationToken));
        return new(MediaArtifactAnalyzer.CompareAudio(left, right, sampleRate, tolerance), leftUri, rightUri);
    }

    private async Task<byte[]> ReadBytesAsync(string uri, string expectedKind, CancellationToken token)
    {
        if (!ImmutableArtifactStore.TryParseUri(uri, out var kind, out _) || kind != expectedKind)
            throw new ArgumentException($"An immutable {expectedKind} artifact URI is required.");
        var path = await store.ResolveVerifiedDataPathAsync(uri, token);
        return await File.ReadAllBytesAsync(path, token);
    }
}
