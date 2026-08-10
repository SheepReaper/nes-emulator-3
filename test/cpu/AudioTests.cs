using Xunit;

namespace SR.Emulation.Nes.Tests;

public sealed class AudioTests
{
    [Fact]
    public void PulseDutyTimerProducesDocumentedEightStepSequence()
    {
        var pulse = new ApuPulse(true) { Enabled = true };
        pulse.WriteControl(0x1F); // duty 0, constant volume 15
        pulse.WriteTimerLow(8);
        pulse.WriteTimerHigh(0);
        pulse.CommitDeferredWrites(false);

        var output = new byte[8];
        for (var step = 0; step < output.Length; step++)
        {
            for (var cycle = 0; cycle < 9; cycle++) pulse.ClockTimer();
            output[step] = pulse.Output;
        }

        Assert.Equal([15, 0, 0, 0, 0, 0, 0, 0], output);
    }

    [Fact]
    public void TriangleRetainsItsDacLevelWhenCountersSilenceTheSequencer()
    {
        var triangle = new ApuTriangle { Enabled = true };
        triangle.WriteControl(0x81);
        triangle.WriteTimerLow(2);
        triangle.WriteTimerHigh(0);
        triangle.CommitDeferredWrites(false);
        triangle.ClockLinear();
        for (var cycle = 0; cycle < 3; cycle++) triangle.ClockTimer();
        var level = triangle.Output;

        triangle.Enabled = false;
        triangle.Length = 0;
        for (var cycle = 0; cycle < 20; cycle++) triangle.ClockTimer();

        Assert.Equal(level, triangle.Output);
    }

    [Fact]
    public void StatusReportsLengthCountersAndDisablingAChannelClearsItsLength()
    {
        var apu = new Apu(new InterruptLines());
        apu.Write(0x4015, 0x01);
        apu.Write(0x4003, 0x00);
        apu.Clock();

        Assert.Equal(0x01, apu.Read(0x4015) & 0x01);

        apu.Write(0x4015, 0x00);
        Assert.Equal(0, apu.Read(0x4015) & 0x01);
    }

    [Fact]
    public void FourStepSequencerRaisesFrameIrqAndStatusReadClearsOnlyThatFlag()
    {
        var interrupts = new InterruptLines();
        var apu = new Apu(interrupts);

        for (var cycle = 0; cycle < 29_829; cycle++) apu.Clock();

        Assert.True(interrupts.ApuFrameIrq);
        Assert.Equal(0x40, apu.Read(0x4015) & 0x40);
        Assert.False(interrupts.ApuFrameIrq);
    }

    [Fact]
    public void DmcReaderWrapsFromFfffTo8000AndRaisesTerminalIrq()
    {
        var interrupts = new InterruptLines();
        var apu = new Apu(interrupts);
        var addresses = new List<ushort>();
        var dmaRequests = 0;
        apu.ConnectDmcDma((address, completed) =>
        {
            addresses.Add(address);
            dmaRequests++;
            completed(0xAA);
        });
        apu.Write(0x4010, 0x8F); // IRQ enabled, fastest NTSC rate
        apu.Write(0x4012, 0xFF); // $FFC0
        apu.Write(0x4013, 0x04); // 65 bytes, crossing $FFFF
        apu.Write(0x4015, 0x10);

        for (var cycle = 0; cycle < 40_000 && !interrupts.ApuDmcIrq; cycle++) apu.Clock();

        Assert.Contains((ushort)0xFFFF, addresses);
        Assert.Contains((ushort)0x8000, addresses);
        Assert.Equal(65, dmaRequests);
        Assert.True(interrupts.ApuDmcIrq);
        Assert.Equal(0x80, apu.Read(0x4015) & 0x80);
        Assert.True(interrupts.ApuDmcIrq); // Status reads do not acknowledge DMC IRQ.
        apu.Write(0x4015, 0);
        Assert.False(interrupts.ApuDmcIrq);
    }

    [Theory]
    [InlineData(15, 15, 0, 0, 0, 0.2584831057)]
    [InlineData(0, 0, 15, 15, 127, 0.7415162451)]
    public void NonlinearMixerMatchesDocumentedFormula(
        byte pulse1, byte pulse2, byte triangle, byte noise, byte dmc, double expected)
    {
        Assert.Equal(expected, Apu.Mix(pulse1, pulse2, triangle, noise, dmc), 8);
    }

    [Fact]
    public void NesProducesBoundedFortyEightKilohertzMonoSamplesFromCpuClocks()
    {
        var nes = new Nes();

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
        var nes = new Nes();
        Assert.Equal(48_000, Nes.AudioSampleRate);
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
}
