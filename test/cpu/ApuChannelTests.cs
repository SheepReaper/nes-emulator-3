using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class ApuChannelTests
{
    [Fact]
    public void PulseDutyTimerProducesDocumentedEightStepSequence()
    {
        var pulse = new ApuPulse(true) { Enabled = true };
        pulse.WriteControl(0x1F);
        pulse.WriteTimerLow(8);
        pulse.WriteTimerHigh(0);
        pulse.CommitDeferredWrites(false);

        var output = new byte[8];
        for (var step = 0; step < output.Length; step++)
        {
            for (var cycle = 0; cycle < 9; cycle++)
            {
                pulse.ClockTimer();
            }
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
        for (var cycle = 0; cycle < 3; cycle++)
        {
            triangle.ClockTimer();
        }
        var level = triangle.Output;

        triangle.Enabled = false;
        triangle.Length = 0;
        for (var cycle = 0; cycle < 20; cycle++)
        {
            triangle.ClockTimer();
        }

        Assert.Equal(level, triangle.Output);
    }

    [Theory]
    [InlineData(0x00, 398)]
    [InlineData(0x0F, 50)]
    public void PalDmcUsesPalTimerPeriods(byte control, ushort expectedPeriod)
    {
        var dmc = new ApuDmc(new InterruptLines(), ApuRegion.Pal);
        dmc.WriteControl(control);
        Assert.Equal(expectedPeriod, dmc.Period);
    }

    [Theory]
    [InlineData(0x02, 14)]
    [InlineData(0x0F, 3_778)]
    public void PalNoiseUsesPalTimerPeriods(byte period, ushort expectedPeriod)
    {
        var noise = new ApuNoise(ApuRegion.Pal);
        noise.WritePeriod(period);
        Assert.Equal(expectedPeriod, noise.TimerPeriod);
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
        apu.Write(0x4010, 0x8F);
        apu.Write(0x4012, 0xFF);
        apu.Write(0x4013, 0x04);
        apu.Write(0x4015, 0x10);

        for (var cycle = 0; cycle < 40_000 && !interrupts.ApuDmcIrq; cycle++)
        {
            apu.Clock();
        }

        Assert.Contains((ushort)0xFFFF, addresses);
        Assert.Contains((ushort)0x8000, addresses);
        Assert.Equal(65, dmaRequests);
        Assert.True(interrupts.ApuDmcIrq);
        Assert.Equal(0x80, apu.Read(0x4015) & 0x80);
        Assert.True(interrupts.ApuDmcIrq);
        apu.Write(0x4015, 0);
        Assert.False(interrupts.ApuDmcIrq);
    }

    [Theory]
    [InlineData(6, false, 0)]
    [InlineData(7, false, 2)]
    [InlineData(8, false, 3)]
    [InlineData(9, false, 0)]
    [InlineData(7, true, 0)]
    [InlineData(8, true, 0)]
    public void DmcReaderOnlyImplicitlyAbortsAtTerminalReloadBoundary(
        ushort dmcTimer,
        bool loop,
        int expectedAbortDelay)
    {
        var reader = new ApuDmcReader(new InterruptLines());
        Action<byte>? completeDma = null;
        var implicitAbortRequests = 0;
        var normalRequests = 0;
        reader.Connect((_, completed) =>
        {
            if (completed is null)
            {
                implicitAbortRequests++;
            }
            else
            {
                normalRequests++;
                completeDma = completed;
            }
        });
        reader.SetControl(irqEnabled: false, loop);
        reader.SetSampleLength(0x00);
        reader.SetEnabled(true, dmcTimer, cpuClock: 1);
        for (var cycle = 0; cycle < 3; cycle++)
        {
            reader.ClockDelays();
            reader.FillSampleBuffer();
        }

        Assert.Equal(0, implicitAbortRequests);
        Assert.Equal(1, normalRequests);
        Assert.NotNull(completeDma);
        completeDma(0xAA);
        for (var delay = 1; delay <= 3; delay++)
        {
            reader.ClockDelays();
            Assert.Equal(delay >= expectedAbortDelay && expectedAbortDelay > 0 ? 1 : 0, implicitAbortRequests);
        }
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(5, 5)]
    [InlineData(6, 5)]
    [InlineData(7, 3)]
    public void DmcReaderDelaysLoopingReloadInsidePreviousDmaWindow(
        ushort dmcTimer,
        int expectedRequestCycle)
    {
        var reader = new ApuDmcReader(new InterruptLines());
        var normalRequests = 0;
        reader.Connect((_, completed) =>
        {
            if (completed is not null)
            {
                normalRequests++;
            }
        });
        reader.SetControl(irqEnabled: false, loop: true);
        reader.SetSampleLength(0x00);
        reader.SetEnabled(true, dmcTimer, cpuClock: 1);
        for (var cycle = 1; cycle <= 5; cycle++)
        {
            reader.ClockDelays();
            reader.FillSampleBuffer();
            Assert.Equal(cycle >= expectedRequestCycle ? 1 : 0, normalRequests);
        }
    }

    [Fact]
    public void DmcReaderWaitsForEnableDelayBeforePendingReload()
    {
        var reader = new ApuDmcReader(new InterruptLines());
        Action<byte>? completeDma = null;
        var normalRequests = 0;
        reader.Connect((_, completed) =>
        {
            if (completed is not null)
            {
                normalRequests++;
                completeDma = completed;
            }
        });
        reader.SetSampleLength(0x00);
        reader.SetEnabled(true, dmcTimer: 0, cpuClock: 1);
        for (var cycle = 0; cycle < 3; cycle++)
        {
            reader.ClockDelays();
            reader.FillSampleBuffer();
        }

        Assert.NotNull(completeDma);
        completeDma(0xAA);
        reader.SetEnabled(false, dmcTimer: 0, cpuClock: 10);
        reader.SetEnabled(true, dmcTimer: 0, cpuClock: 11);
        reader.SampleBufferRef = null;

        for (var cycle = 1; cycle <= 3; cycle++)
        {
            reader.ClockDelays();
            reader.FillSampleBuffer();
            Assert.Equal(cycle == 3 ? 2 : 1, normalRequests);
        }
    }
}
