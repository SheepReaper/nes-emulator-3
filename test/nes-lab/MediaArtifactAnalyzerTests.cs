using System.Buffers.Binary;

namespace Sheep.Nes.Lab.Tests;

public sealed class MediaArtifactAnalyzerTests
{
    [Fact]
    public void CompareFrames_ReportsPixelBoundsScanlineAndDeterministicHeatmap()
    {
        var left = OpaqueFrame();
        var right = OpaqueFrame();
        SetPixel(right, 3, 4, 10, 20, 30);
        SetPixel(right, 8, 9, 40, 50, 60);

        var result = MediaArtifactAnalyzer.CompareFrames(left, right, createHeatmap: true);

        Assert.False(result.Equal);
        Assert.Equal(2, result.ChangedPixelCount);
        Assert.Equal(new FrameDifferenceBounds(3, 4, 8, 9), result.Bounds);
        Assert.Equal(3, result.FirstDifference!.X);
        Assert.Contains(result.Scanlines, line => line.Y == 4 && line.ChangedPixels == 1);
        Assert.Equal(left.Length, result.HeatmapRgba!.Length);
        Assert.Equal([255, 0, 0, 255], result.HeatmapRgba.AsSpan(((4 * 256) + 3) * 4, 4).ToArray());
    }

    [Fact]
    public void AnalyzeAudio_ReportsSignalHealthAndWindowSummaries()
    {
        float[] samples = [0f, .5f, -1f, 1.2f, float.NaN, float.PositiveInfinity];

        var result = MediaArtifactAnalyzer.AnalyzeAudio(samples, 48_000, windowSize: 2);

        Assert.Equal(6, result.SampleCount);
        Assert.Equal(1, result.NaNCount);
        Assert.Equal(1, result.InfinityCount);
        Assert.Equal(1, result.ClippedSampleCount);
        Assert.Equal(3, result.Windows.Count);
        Assert.False(result.IsSilent);
    }

    [Fact]
    public void CompareAudio_FindsFirstDifferenceAndHonorsTolerance()
    {
        float[] left = [0f, .25f, .5f];
        float[] right = [0f, .2501f, .7f];

        var exact = MediaArtifactAnalyzer.CompareAudio(left, right, 48_000);
        var tolerant = MediaArtifactAnalyzer.CompareAudio(left, right, 48_000,
            new AudioComparisonTolerance(.001f, 1));

        Assert.Equal(1, exact.FirstDifferingSample);
        Assert.Equal(2, tolerant.FirstDifferingSample);
        Assert.False(tolerant.Equal);
    }

    [Fact]
    public void DecodeFloat32_RejectsMisalignedBytes()
    {
        Assert.Throws<InvalidDataException>(() => MediaArtifactAnalyzer.DecodeFloat32([1, 2, 3]));
        var bytes = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(bytes, .25f);
        Assert.Equal(.25f, Assert.Single(MediaArtifactAnalyzer.DecodeFloat32(bytes)));
    }

    private static byte[] OpaqueFrame()
    {
        var frame = new byte[256 * 240 * 4];
        for (var index = 3; index < frame.Length; index += 4) frame[index] = 255;
        return frame;
    }

    private static void SetPixel(byte[] frame, int x, int y, byte r, byte g, byte b)
    {
        var offset = ((y * 256) + x) * 4;
        frame[offset] = r; frame[offset + 1] = g; frame[offset + 2] = b; frame[offset + 3] = 255;
    }
}
