using System;

namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Accumulates per-clock mixer output, decimates it to the target sample rate,
/// applies filtering, and writes samples into the <see cref="AudioSampleBuffer"/>.
/// </summary>
internal sealed class ApuAudioOutput(int masterClockHz, int cpuDivisor)
{
    private const int WriteBlockSize = 64;
    private readonly AudioSampleBuffer _buffer = new(ApuMixer.SampleRate / 5);
    private readonly ApuMixer _mixer = new();
    private readonly float[] _block = new float[WriteBlockSize];
    private int _blockCount;
    private readonly long _phaseIncrement = (long)ApuMixer.SampleRate * cpuDivisor;
    private readonly int _phaseThreshold = masterClockHz;
    private long _phase;
    private double _accumulated;
    private int _accumulatedClocks;

    internal NesAudioFilterMode FilterMode
    {
        get => _mixer.FilterMode;
        set
        {
            if (_mixer.FilterMode == value) return;
            _mixer.FilterMode = value;
            Discard();
        }
    }

    internal int BufferedSampleCount => _buffer.Count;

    internal void AccumulateAndEmit(byte pulse1, byte pulse2, byte tri, byte noise, byte dmc)
    {
        _accumulated += ApuMixer.Mix(pulse1, pulse2, tri, noise, dmc);
        _accumulatedClocks++;
        _phase += _phaseIncrement;
        if (_phase < _phaseThreshold) return;

        _phase -= _phaseThreshold;
        var avg = _accumulatedClocks == 0 ? 0.0 : _accumulated / _accumulatedClocks;
        _accumulated = 0;
        _accumulatedClocks = 0;
        _block[_blockCount++] = _mixer.ApplyFilter(avg);
        if (_blockCount == _block.Length)
        {
            _buffer.Write(_block);
            _blockCount = 0;
        }
    }

    internal int ReadSamples(Span<float> dest) => _buffer.Read(dest);

    internal NesAudioReadResult ReadAudio(Span<float> dest)
    {
        var written = _buffer.Read(dest, out var remaining);
        return new NesAudioReadResult(written, remaining, written < dest.Length);
    }

    internal void Flush()
    {
        if (_blockCount == 0) return;
        _buffer.Write(_block.AsSpan(0, _blockCount));
        _blockCount = 0;
    }

    internal void Discard()
    {
        _blockCount = 0;
        _buffer.Clear();
    }

    internal void Reset()
    {
        _blockCount = 0;
        _buffer.Clear();
        _mixer.ResetFilters();
        _phase = 0;
        _accumulated = 0;
        _accumulatedClocks = 0;
    }
}
