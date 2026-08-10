using System;

using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;

public sealed class Apu(
    InterruptLines interrupts,
    ApuRegion? region = null,
    int masterClockHz = 21_477_272,
    int cpuDivisor = 12) : IBusDevice
{
    internal const int SampleRate = 48_000;
    private const int AudioBufferCapacity = SampleRate / 5;
    private readonly InterruptLines _interrupts = interrupts;
    private readonly byte[] _registers = new byte[0x18];
    private readonly ApuPulse _pulse1 = new(true);
    private readonly ApuPulse _pulse2 = new(false);
    private readonly ApuTriangle _triangle = new();
    private readonly ApuNoise _noise = new();
    private readonly ApuDmc _dmc = new(interrupts);
    private readonly AudioSampleBuffer _audio = new(AudioBufferCapacity);
    private readonly long _samplePhaseIncrement = (long)SampleRate * cpuDivisor;
    private readonly int _samplePhaseThreshold = masterClockHz;
    private long _samplePhase;
    private double _integratedOutput;
    private int _integratedClocks;
    private NesAudioFilterMode _filterMode = NesAudioFilterMode.Nes;
    private double _highPass90Input;
    private double _highPass90Output;
    private double _highPass440Input;
    private double _highPass440Output;
    private double _lowPassOutput;
    private ulong _cpuClock;
    private int _frameCycle;
    private bool _fiveStepMode;
    private bool _frameIrqInhibit;
    private int _pendingFrameCounterDelay;
    private byte _pendingFrameCounterValue;
    private int _frameIrqPulseRemaining;
    private bool _poweredOn;
    private bool _halfFrameClockedThisCycle;
    private Func<double>? _cartridgeAudio;

    public ApuRegion Region { get; } = region ?? ApuRegion.Default;

    internal NesAudioFilterMode FilterMode
    {
        get => _filterMode;
        set
        {
            if (!Enum.IsDefined(typeof(NesAudioFilterMode), value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_filterMode == value) return;
            _filterMode = value;
            ResetAudioOutput();
        }
    }

    internal int BufferedSampleCount => _audio.Count;

    public byte Read(ushort address)
    {
        if (address != 0x4015) return 0;
        return ReadStatus(clearFrameIrq: true);
    }

    private byte ReadStatus(bool clearFrameIrq)
    {
        byte result = 0;
        if (_pulse1.Length > 0) result |= 0x01;
        if (_pulse2.Length > 0) result |= 0x02;
        if (_triangle.Length > 0) result |= 0x04;
        if (_noise.Length > 0) result |= 0x08;
        if (_dmc.Enabled) result |= 0x10;
        if (_interrupts.ApuFrameIrq) result |= 0x40;
        if (_interrupts.ApuDmcIrq) result |= 0x80;
        if (clearFrameIrq) _interrupts.ApuFrameIrq = false;
        return result;
    }

    public void Write(ushort address, byte value)
    {
        if (address is < 0x4000 or > 0x4017) return;
        _registers[address - 0x4000] = value;
        switch (address)
        {
            case 0x4000: _pulse1.WriteControl(value); break;
            case 0x4001: _pulse1.WriteSweep(value); break;
            case 0x4002: _pulse1.WriteTimerLow(value); break;
            case 0x4003: _pulse1.WriteTimerHigh(value); break;
            case 0x4004: _pulse2.WriteControl(value); break;
            case 0x4005: _pulse2.WriteSweep(value); break;
            case 0x4006: _pulse2.WriteTimerLow(value); break;
            case 0x4007: _pulse2.WriteTimerHigh(value); break;
            case 0x4008: _triangle.WriteControl(value); break;
            case 0x400A: _triangle.WriteTimerLow(value); break;
            case 0x400B: _triangle.WriteTimerHigh(value); break;
            case 0x400C: _noise.WriteControl(value); break;
            case 0x400E: _noise.WritePeriod(value); break;
            case 0x400F: _noise.WriteLength(value); break;
            case 0x4010: _dmc.WriteControl(value); break;
            case 0x4011: _dmc.WriteOutput(value); break;
            case 0x4012: _dmc.WriteAddress(value); break;
            case 0x4013: _dmc.WriteLength(value); break;
            case 0x4015: WriteStatus(value); break;
            case 0x4017:
                _pendingFrameCounterValue = value;
                _pendingFrameCounterDelay = (_cpuClock & 1) == 0 ? 3 : 4;
                if ((value & 0x40) != 0) _interrupts.ApuFrameIrq = false;
                break;
        }
    }

    internal void Clock()
    {
        _cpuClock++;
        _halfFrameClockedThisCycle = false;
        if (_frameIrqPulseRemaining > 0)
        {
            if (!_frameIrqInhibit) _interrupts.ApuFrameIrq = true;
            _frameIrqPulseRemaining--;
        }
        ClockFrameCounter();
        _pulse1.CommitDeferredWrites(_halfFrameClockedThisCycle);
        _pulse2.CommitDeferredWrites(_halfFrameClockedThisCycle);
        _triangle.CommitDeferredWrites(_halfFrameClockedThisCycle);
        _noise.CommitDeferredWrites(_halfFrameClockedThisCycle);
        _triangle.ClockTimer();
        _dmc.Clock();
        if ((_cpuClock & 1) == 0)
        {
            _pulse1.ClockTimer();
            _pulse2.ClockTimer();
            _noise.ClockTimer();
        }

        var output = Mix(_pulse1.Output, _pulse2.Output, _triangle.Output, _noise.Output, _dmc.Output) +
            (_cartridgeAudio?.Invoke() ?? 0);
        output = Math.Max(-1, Math.Min(1, output));
        _integratedOutput += output;
        _integratedClocks++;
        _samplePhase += _samplePhaseIncrement;
        if (_samplePhase < _samplePhaseThreshold) return;

        _samplePhase -= _samplePhaseThreshold;
        var sample = _integratedClocks == 0 ? 0.0 : _integratedOutput / _integratedClocks;
        _integratedOutput = 0;
        _integratedClocks = 0;
        _audio.Write((float)ApplyFilter(sample));
    }

    internal int ReadAudioSamples(Span<float> destination) => _audio.Read(destination);
    internal void DiscardAudioSamples() => _audio.Clear();

    internal void Reset()
    {
        if (!_poweredOn)
        {
            Array.Clear(_registers, 0, _registers.Length);
            _pulse1.Reset();
            _pulse2.Reset();
            _triangle.Reset();
            _noise.Reset();
            _dmc.Reset();
            _poweredOn = true;
        }
        else
        {
            // Reset behaves like a $4015 clear and a rewrite of the last
            // $4017 value. Other channel registers (notably triangle halt)
            // retain their state.
            WriteStatus(0);
            _pendingFrameCounterValue = _registers[0x17];
        }
        _registers[0x15] = 0;
        _cpuClock = 0;
        // Reset begins CPU execution seven clocks later. Hardware behaves as
        // though the frame-counter write preceded the first opcode by roughly
        // ten clocks, leaving three sequencer clocks already elapsed here.
        _frameCycle = 3;
        _fiveStepMode = (_pendingFrameCounterValue & 0x80) != 0;
        _frameIrqInhibit = (_pendingFrameCounterValue & 0x40) != 0;
        _pendingFrameCounterDelay = 0;
        _frameIrqPulseRemaining = 0;
        _interrupts.ApuFrameIrq = false;
        _interrupts.ApuDmcIrq = false;
        _samplePhase = 0;
        _integratedOutput = 0;
        _integratedClocks = 0;
        ResetAudioOutput();
    }

    private void ResetAudioOutput()
    {
        _audio.Clear();
        _highPass90Input = _highPass90Output = 0;
        _highPass440Input = _highPass440Output = 0;
        _lowPassOutput = 0;
    }

    private double ApplyFilter(double input)
    {
        if (_filterMode == NesAudioFilterMode.Raw) return input;
        var highPass90 = HighPass(input, 90, ref _highPass90Input, ref _highPass90Output);
        var highPass440 = HighPass(highPass90, 440, ref _highPass440Input, ref _highPass440Output);
        var lowPassCoefficient = 1.0 - Math.Exp(-2.0 * Math.PI * 14_000 / SampleRate);
        _lowPassOutput += lowPassCoefficient * (highPass440 - _lowPassOutput);
        return _lowPassOutput;
    }

    private static double HighPass(double input, double frequency, ref double previousInput, ref double previousOutput)
    {
        var coefficient = Math.Exp(-2.0 * Math.PI * frequency / SampleRate);
        var output = coefficient * (previousOutput + input - previousInput);
        previousInput = input;
        previousOutput = output;
        return output;
    }

    internal static double Mix(byte pulse1, byte pulse2, byte triangle, byte noise, byte dmc)
    {
        var pulseSum = pulse1 + pulse2;
        var pulse = pulseSum == 0 ? 0 : 95.88 / (8128.0 / pulseSum + 100.0);
        var tndInput = triangle / 8227.0 + noise / 12241.0 + dmc / 22638.0;
        var tnd = tndInput == 0 ? 0 : 159.79 / (1.0 / tndInput + 100.0);
        return pulse + tnd;
    }

    private void WriteStatus(byte value)
    {
        _pulse1.Enabled = (value & 0x01) != 0;
        _pulse2.Enabled = (value & 0x02) != 0;
        _triangle.Enabled = (value & 0x04) != 0;
        _noise.Enabled = (value & 0x08) != 0;
        _dmc.SetEnabled((value & 0x10) != 0);
        if (!_pulse1.Enabled) _pulse1.Length = 0;
        if (!_pulse2.Enabled) _pulse2.Length = 0;
        if (!_triangle.Enabled) _triangle.Length = 0;
        if (!_noise.Enabled) _noise.Length = 0;
        _interrupts.ApuDmcIrq = false;
    }

    internal void ConnectDmcDma(Action<ushort, Action<byte>> requestDma) => _dmc.Connect(requestDma);
    internal void ConnectCartridgeAudio(Func<double> audioOutput) => _cartridgeAudio = audioOutput;

    private void ClockFrameCounter()
    {
        if (_pendingFrameCounterDelay > 0 && --_pendingFrameCounterDelay == 0)
        {
            _fiveStepMode = (_pendingFrameCounterValue & 0x80) != 0;
            _frameIrqInhibit = (_pendingFrameCounterValue & 0x40) != 0;
            if (_frameIrqInhibit) _interrupts.ApuFrameIrq = false;
            _frameCycle = 0;
            if (_fiveStepMode) ClockQuarterAndHalfFrame();
        }

        _frameCycle++;
        if (_fiveStepMode)
        {
            switch (_frameCycle)
            {
                case 7458: ClockQuarterFrame(); break;
                case 14914: ClockQuarterAndHalfFrame(); break;
                case 22372: ClockQuarterFrame(); break;
                case 37282:
                    ClockQuarterAndHalfFrame();
                    _frameCycle = 0;
                    break;
            }
        }
        else
        {
            switch (_frameCycle)
            {
                case 7458: ClockQuarterFrame(); break;
                case 14914: ClockQuarterAndHalfFrame(); break;
                case 22372: ClockQuarterFrame(); break;
                case 29829:
                    if (!_frameIrqInhibit)
                    {
                        _interrupts.ApuFrameIrq = true;
                        _frameIrqPulseRemaining = 2;
                    }
                    break;
                case 29830:
                    ClockQuarterAndHalfFrame();
                    _frameCycle = 0;
                    break;
            }
        }
    }

    private void ClockQuarterFrame()
    {
        _pulse1.ClockEnvelope();
        _pulse2.ClockEnvelope();
        _noise.ClockEnvelope();
        _triangle.ClockLinear();
    }

    private void ClockQuarterAndHalfFrame()
    {
        _halfFrameClockedThisCycle = true;
        ClockQuarterFrame();
        _pulse1.ClockLength();
        _pulse2.ClockLength();
        _triangle.ClockLength();
        _noise.ClockLength();
        _pulse1.ClockSweep();
        _pulse2.ClockSweep();
    }

    internal ApuDebugState CaptureDebugState() =>
        new(true, new ReadOnlyMemory<byte>((byte[])_registers.Clone()), _frameCycle,
            _fiveStepMode, _interrupts.ApuFrameIrq, _interrupts.ApuDmcIrq,
            _pulse1.Length, _pulse2.Length, _triangle.Length, _noise.Length,
            _pulse1.Output, _pulse2.Output, _triangle.Output, _noise.Output, _dmc.Output,
            _dmc.CurrentAddress, _dmc.BytesRemaining);

    internal byte Peek(ushort address) =>
        address == 0x4015 ? ReadStatus(clearFrameIrq: false) :
        address is >= 0x4000 and <= 0x4017 ? _registers[address - 0x4000] : (byte)0;
}
