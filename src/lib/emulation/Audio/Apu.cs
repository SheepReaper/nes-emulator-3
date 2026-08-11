using System;

using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes.Audio;

public sealed class Apu(
    InterruptLines interrupts,
    ApuRegion? region = null,
    int masterClockHz = 21_477_272,
    int cpuDivisor = 12) : IBusDevice
{
    private readonly byte[] _registers = new byte[0x18];
    private readonly ApuChannels _channels = new(interrupts, region);
    private readonly ApuFrameCounter _frame = new(interrupts, region ?? ApuRegion.Default);
    private readonly ApuAudioOutput _audio = new(masterClockHz, cpuDivisor);
    private ulong _cpuClock;
    private bool _poweredOn;

    public ApuRegion Region { get; } = region ?? ApuRegion.Default;
    internal ulong CpuClock => _cpuClock;
    internal int BufferedSampleCount => _audio.BufferedSampleCount;
    internal NesAudioFilterMode FilterMode
    {
        get => _audio.FilterMode;
        set => _audio.FilterMode = value;
    }

    public byte Read(ushort address) => address == 0x4015
        ? ApuRegisterRouter.ReadStatus(true, _cpuClock, _channels, _frame, interrupts)
        : (byte)0;

    public void Write(ushort address, byte value)
    {
        if (address is < 0x4000 or > 0x4017) return;
        _registers[address - 0x4000] = value;
        ApuRegisterRouter.Write(address, value, _cpuClock, _channels, _frame, interrupts);
    }

    internal void Clock()
    {
        _frame.Clock(_cpuClock, _channels.ClockQuarterFrame, _channels.ClockHalfFrame);
        _channels.CommitDeferredWrites(_frame.HalfFrameClockedThisCycle);
        _channels.ClockTimers(_cpuClock);
        if ((_cpuClock & 1) != 0)
        {
            _frame.HandleIrqClearOnEvenClock(_cpuClock);
        }
        _audio.AccumulateAndEmit(
            _channels.Pulse1.Output,
            _channels.Pulse2.Output,
            _channels.Triangle.Output,
            _channels.Noise.Output,
            _channels.Dmc.Output);
        _cpuClock++;
    }

    internal int ReadAudioSamples(Span<float> dest) => _audio.ReadSamples(dest);
    internal NesAudioReadResult ReadAudio(Span<float> dest) => _audio.ReadAudio(dest);
    internal void FlushAudioSamples() => _audio.Flush();
    internal void DiscardAudioSamples() => _audio.Discard();

    internal void Reset()
    {
        if (!_poweredOn)
        {
            Array.Clear(_registers, 0, _registers.Length);
            _channels.Reset();
            _poweredOn = true;
        }
        else
        {
            ApuStatusRegister.Write(0, _cpuClock, _channels, interrupts);
        }
        _registers[0x15] = 0;
        _cpuClock = 0;
        _frame.Reset(_registers[0x17]);
        _audio.Reset();
        interrupts.ApuDmcIrq = false;
    }

    internal void ConnectDmcDma(Action<ushort, Action<byte>?> requestDma, Action? abortDma = null) =>
        _channels.Dmc.Connect(requestDma, abortDma);

    internal ApuDebugState CaptureDebugState() =>
        new(true, new ReadOnlyMemory<byte>((byte[])_registers.Clone()), _frame.FrameCycle,
            _frame.FiveStepMode, interrupts.ApuFrameIrq, interrupts.ApuDmcIrq,
            _channels.Pulse1.Length, _channels.Pulse2.Length, _channels.Triangle.Length, _channels.Noise.Length,
            _channels.Pulse1.Output, _channels.Pulse2.Output, _channels.Triangle.Output, _channels.Noise.Output, _channels.Dmc.Output,
            _channels.Dmc.CurrentAddress, _channels.Dmc.BytesRemaining);

    internal byte Peek(ushort address) =>
        address == 0x4015 ? ApuRegisterRouter.ReadStatus(false, _cpuClock, _channels, _frame, interrupts) :
        address is >= 0x4000 and <= 0x4017 ? _registers[address - 0x4000] : (byte)0;
}
