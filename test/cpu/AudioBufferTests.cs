using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class AudioBufferTests
{
    [Theory]
    [InlineData(15, 15, 0, 0, 0, 0.2584831057)]
    [InlineData(0, 0, 15, 15, 127, 0.7415162451)]
    public void NonlinearMixerMatchesDocumentedFormula(
        byte pulse1, byte pulse2, byte triangle, byte noise, byte dmc, double expected)
    {
        Assert.Equal(expected, ApuMixer.Mix(pulse1, pulse2, triangle, noise, dmc), 8);
    }

    [Fact]
    public void NesProducesBoundedFortyEightKilohertzMonoSamplesFromCpuClocks()
    {
        var nes = new NesSystem();
        nes.RunForPpuDots(3 * 17_898);

        var samples = new float[481];
        var count = nes.ReadAudioSamples(samples);
        Assert.InRange(count, 479, 481);
        Assert.All(samples.AsSpan(0, count).ToArray(), sample =>
        {
            Assert.True(float.IsFinite(sample));
            Assert.InRange(sample, -1.0f, 1.0f);
        });
    }

    [Fact]
    public void AudioQueueCanBeDiscardedAndFilterChangesDiscardHistory()
    {
        var nes = new NesSystem();
        Assert.Equal(48_000, NesSystem.AudioSampleRate);
        Assert.Equal(NesAudioFilterMode.Nes, nes.AudioFilterMode);

        nes.RunForPpuDots(20_000);
        Assert.True(nes.BufferedAudioSampleCount > 0);

        nes.DiscardAudioSamples();
        Assert.Equal(0, nes.BufferedAudioSampleCount);

        nes.RunForPpuDots(20_000);
        nes.AudioFilterMode = NesAudioFilterMode.Raw;
        Assert.Equal(0, nes.BufferedAudioSampleCount);
    }

    [Fact]
    public void AudioReadReportsWrittenRemainingAndUnderrunState()
    {
        var nes = new NesSystem();
        nes.RunForPpuDots(20_000);
        var before = nes.BufferedAudioSampleCount;

        var result = nes.ReadAudio(new float[before - 1]);
        Assert.Equal(before - 1, result.SamplesWritten);
        Assert.Equal(1, result.SamplesRemaining);
        Assert.False(result.Underrun);

        result = nes.ReadAudio(new float[2]);
        Assert.Equal(1, result.SamplesWritten);
        Assert.Equal(0, result.SamplesRemaining);
        Assert.True(result.Underrun);
    }

    [Fact]
    public void AudioQueueDropsOldestSamplesWhenProducerOutrunsConsumer()
    {
        var buffer = new AudioSampleBuffer(3);
        buffer.Write(1);
        buffer.Write(2);
        buffer.Write(3);
        buffer.Write(4);

        var destination = new float[3];
        Assert.Equal(3, buffer.Read(destination));
        Assert.Equal([2, 3, 4], destination);
    }

    [Fact]
    public void AudioQueueWritesBlocksAndPreservesNewestSamplesAcrossWraparound()
    {
        var buffer = new AudioSampleBuffer(5);
        buffer.Write([1, 2, 3]);

        var discarded = new float[2];
        Assert.Equal(2, buffer.Read(discarded));
        buffer.Write([4, 5, 6, 7, 8]);

        var destination = new float[5];
        Assert.Equal(5, buffer.Read(destination));
        Assert.Equal([4, 5, 6, 7, 8], destination);
    }
}
