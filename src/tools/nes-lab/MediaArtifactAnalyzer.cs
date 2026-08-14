using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Sheep.Nes.Lab;

public sealed record FrameDifferenceBounds(int Left, int Top, int Right, int Bottom);
public sealed record FramePixelDifference(int X, int Y, byte[] Left, byte[] Right);
public sealed record FrameScanlineDifference(int Y, int ChangedPixels, string LeftSha256, string RightSha256);
public sealed record FrameRegion(string Id, int X, int Y, int Width, int Height);
public sealed record FrameRegionHash(string Id, string LeftSha256, string RightSha256, bool Equal);
public sealed record FrameComparisonResult(bool Equal, int ChangedPixelCount, double ChangedPixelPercentage,
    FrameDifferenceBounds? Bounds, FramePixelDifference? FirstDifference,
    IReadOnlyList<FrameScanlineDifference> Scanlines, IReadOnlyList<FrameRegionHash> Regions,
    int PaletteIndexDifferences, bool PaletteComparable, byte[]? HeatmapRgba);

public sealed record AudioWindowSummary(int StartSample, int SampleCount, double Rms, float Peak);
public sealed record AudioSpectralSummary(double Low, double Mid, double High);
public sealed record AudioAnalysisResult(int SampleCount, int SampleRate, double DurationSeconds,
    float Minimum, float Maximum, float Peak, double Rms, double Mean, int SilentSampleCount,
    int ClippedSampleCount, int NaNCount, int InfinityCount, bool IsSilent,
    IReadOnlyList<AudioWindowSummary> Windows, AudioSpectralSummary Spectrum);
public sealed record AudioComparisonTolerance(float Sample = 0, int TimingSamples = 0, double Rms = 0);
public sealed record AudioComparisonResult(bool Equal, int? FirstDifferingSample, float? LeftSample,
    float? RightSample, int SampleCountDifference, double RmsDifference,
    AudioComparisonTolerance Tolerance, AudioAnalysisResult Left, AudioAnalysisResult Right);

public static class MediaArtifactAnalyzer
{
    public const int FrameWidth = 256;
    public const int FrameHeight = 240;
    public const int FrameBytes = FrameWidth * FrameHeight * 4;

    public static FrameComparisonResult CompareFrames(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right,
        IReadOnlyList<FrameRegion>? regions = null, IReadOnlyDictionary<uint, int>? palette = null,
        bool createHeatmap = false)
    {
        ValidateFrame(left); ValidateFrame(right);
        var heatmap = createHeatmap ? new byte[FrameBytes] : null;
        List<FrameScanlineDifference> scanlines = [];
        FramePixelDifference? first = null;
        var changed = 0; var paletteDifferences = 0; var paletteComparable = palette is not null;
        var minX = FrameWidth; var minY = FrameHeight; var maxX = -1; var maxY = -1;
        for (var y = 0; y < FrameHeight; y++)
        {
            var rowStart = y * FrameWidth * 4;
            var rowChanged = 0;
            for (var x = 0; x < FrameWidth; x++)
            {
                var offset = rowStart + x * 4;
                var a = left.Slice(offset, 4); var b = right.Slice(offset, 4);
                if (a.SequenceEqual(b)) continue;
                changed++; rowChanged++;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                first ??= new(x, y, a.ToArray(), b.ToArray());
                if (heatmap is not null)
                { heatmap[offset] = 255; heatmap[offset + 3] = 255; }
                if (palette is not null)
                {
                    var leftColor = BinaryPrimitives.ReadUInt32BigEndian(a);
                    var rightColor = BinaryPrimitives.ReadUInt32BigEndian(b);
                    if (!palette.TryGetValue(leftColor, out var leftIndex) || !palette.TryGetValue(rightColor, out var rightIndex))
                        paletteComparable = false;
                    else if (leftIndex != rightIndex) paletteDifferences++;
                }
            }
            if (rowChanged > 0)
                scanlines.Add(new(y, rowChanged, Hash(left.Slice(rowStart, FrameWidth * 4)),
                    Hash(right.Slice(rowStart, FrameWidth * 4))));
        }
        List<FrameRegionHash> regionHashes = [];
        foreach (var region in regions ?? []) regionHashes.Add(HashRegion(left, right, region));
        return new(changed == 0, changed, changed * 100d / (FrameWidth * FrameHeight),
            changed == 0 ? null : new(minX, minY, maxX, maxY), first, scanlines, regionHashes,
            paletteDifferences, paletteComparable, heatmap);
    }

    public static AudioAnalysisResult AnalyzeAudio(ReadOnlySpan<float> samples, int sampleRate = 48_000,
        int windowSize = 1024, float silenceThreshold = 1e-5f)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(windowSize, 1);
        var min = float.PositiveInfinity; var max = float.NegativeInfinity; var peak = 0f;
        double sum = 0, squares = 0; var finite = 0; var silent = 0; var clipped = 0; var nan = 0; var infinity = 0;
        foreach (var sample in samples)
        {
            if (float.IsNaN(sample)) { nan++; continue; }
            if (float.IsInfinity(sample)) { infinity++; continue; }
            finite++; min = Math.Min(min, sample); max = Math.Max(max, sample);
            peak = Math.Max(peak, Math.Abs(sample)); sum += sample; squares += sample * sample;
            if (Math.Abs(sample) <= silenceThreshold) silent++;
            if (Math.Abs(sample) > 1f) clipped++;
        }
        List<AudioWindowSummary> windows = [];
        for (var start = 0; start < samples.Length; start += windowSize)
        {
            var window = samples.Slice(start, Math.Min(windowSize, samples.Length - start));
            var values = window.ToArray().Where(float.IsFinite).ToArray();
            windows.Add(new(start, window.Length,
                values.Length == 0 ? 0 : Math.Sqrt(values.Sum(value => value * value) / values.Length),
                values.Length == 0 ? 0 : values.Max(value => Math.Abs(value))));
        }
        if (finite == 0) min = max = 0;
        return new(samples.Length, sampleRate, samples.Length / (double)sampleRate, min, max, peak,
            finite == 0 ? 0 : Math.Sqrt(squares / finite), finite == 0 ? 0 : sum / finite,
            silent, clipped, nan, infinity, finite == 0 || silent == finite, windows,
            Spectrum(samples, sampleRate));
    }

    public static AudioComparisonResult CompareAudio(ReadOnlySpan<float> left, ReadOnlySpan<float> right,
        int sampleRate = 48_000, AudioComparisonTolerance? tolerance = null)
    {
        tolerance ??= new();
        var leftAnalysis = AnalyzeAudio(left, sampleRate);
        var rightAnalysis = AnalyzeAudio(right, sampleRate);
        int? first = null; float? a = null, b = null;
        var common = Math.Min(left.Length, right.Length);
        for (var index = 0; index < common; index++)
            if (!Equivalent(left[index], right[index], tolerance.Sample))
            { first = index; a = left[index]; b = right[index]; break; }
        if (first is null && left.Length != right.Length) first = common;
        var countDifference = Math.Abs(left.Length - right.Length);
        var rmsDifference = Math.Abs(leftAnalysis.Rms - rightAnalysis.Rms);
        var equal = first is null || (first.Value < tolerance.TimingSamples &&
            countDifference <= tolerance.TimingSamples && rmsDifference <= tolerance.Rms);
        return new(equal, first, a, b, countDifference, rmsDifference, tolerance, leftAnalysis, rightAnalysis);
    }

    public static float[] DecodeFloat32(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % sizeof(float) != 0) throw new InvalidDataException("Float32 PCM byte count must be divisible by four.");
        var samples = new float[bytes.Length / sizeof(float)];
        for (var index = 0; index < samples.Length; index++)
            samples[index] = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(index * 4, 4));
        return samples;
    }

    private static AudioSpectralSummary Spectrum(ReadOnlySpan<float> samples, int sampleRate)
    {
        var length = Math.Min(samples.Length, 2048);
        if (length == 0) return new(0, 0, 0);
        double low = 0, mid = 0, high = 0;
        var bins = Math.Min(128, length / 2);
        for (var bin = 1; bin <= bins; bin++)
        {
            double real = 0, imaginary = 0;
            for (var n = 0; n < length; n++)
            {
                var value = float.IsFinite(samples[n]) ? samples[n] : 0;
                var angle = -2 * Math.PI * bin * n / length;
                real += value * Math.Cos(angle); imaginary += value * Math.Sin(angle);
            }
            var energy = (real * real + imaginary * imaginary) / (length * length);
            var frequency = bin * sampleRate / (double)length;
            if (frequency < 250) low += energy; else if (frequency < 4000) mid += energy; else high += energy;
        }
        return new(low, mid, high);
    }

    private static FrameRegionHash HashRegion(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, FrameRegion region)
    {
        if (region.X < 0 || region.Y < 0 || region.Width < 1 || region.Height < 1 ||
            region.X + region.Width > FrameWidth || region.Y + region.Height > FrameHeight)
            throw new ArgumentOutOfRangeException(nameof(region));
        using var leftHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var rightHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var y = region.Y; y < region.Y + region.Height; y++)
        {
            var offset = ((y * FrameWidth) + region.X) * 4;
            leftHash.AppendData(left.Slice(offset, region.Width * 4));
            rightHash.AppendData(right.Slice(offset, region.Width * 4));
        }
        var a = Convert.ToHexStringLower(leftHash.GetHashAndReset());
        var b = Convert.ToHexStringLower(rightHash.GetHashAndReset());
        return new(region.Id, a, b, a == b);
    }

    private static bool Equivalent(float left, float right, float tolerance) =>
        float.IsNaN(left) && float.IsNaN(right) || left.Equals(right) ||
        float.IsFinite(left) && float.IsFinite(right) && Math.Abs(left - right) <= tolerance;
    private static void ValidateFrame(ReadOnlySpan<byte> frame)
    { if (frame.Length != FrameBytes) throw new InvalidDataException($"RGBA frame must contain exactly {FrameBytes} bytes."); }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
