namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Container and sequencer for the five APU audio channels.
/// </summary>
internal sealed class ApuChannels(InterruptLines interrupts, ApuRegion? region)
{
    internal readonly ApuPulse Pulse1 = new(true);
    internal readonly ApuPulse Pulse2 = new(false);
    internal readonly ApuTriangle Triangle = new();
    internal readonly ApuNoise Noise = new(region);
    internal readonly ApuDmc Dmc = new(interrupts, region);

    internal void ClockQuarterFrame()
    {
        Pulse1.ClockEnvelope();
        Pulse2.ClockEnvelope();
        Noise.ClockEnvelope();
        Triangle.ClockLinear();
    }

    internal void ClockHalfFrame()
    {
        Pulse1.ClockLength();
        Pulse2.ClockLength();
        Triangle.ClockLength();
        Noise.ClockLength();
        Pulse1.ClockSweep();
        Pulse2.ClockSweep();
    }

    internal void ClockTimers(ulong cpuClock)
    {
        Triangle.ClockTimer();
        Dmc.Clock();
        if ((cpuClock & 1) == 0)
        {
            Pulse1.ClockTimer();
            Pulse2.ClockTimer();
            Noise.ClockTimer();
        }
    }

    internal void CommitDeferredWrites(bool halfFrameClocked)
    {
        Pulse1.CommitDeferredWrites(halfFrameClocked);
        Pulse2.CommitDeferredWrites(halfFrameClocked);
        Triangle.CommitDeferredWrites(halfFrameClocked);
        Noise.CommitDeferredWrites(halfFrameClocked);
    }

    internal void Reset()
    {
        Pulse1.Reset();
        Pulse2.Reset();
        Triangle.Reset();
        Noise.Reset();
        Dmc.Reset();
    }
}
