using System;

namespace Sheep.Emulation.Nes.Audio;

internal sealed class AudioSampleBuffer(int capacity)
{
    private readonly float[] _samples = new float[capacity];
    private readonly object _sync = new();
    private int _readIndex;
    private int _count;

    public int Count
    {
        get { lock (_sync) return _count; }
    }

    public void Write(float sample)
    {
        Span<float> samples = stackalloc float[1] { sample };
        Write(samples);
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        lock (_sync)
        {
            if (samples.Length >= _samples.Length)
            {
                samples[^_samples.Length..].CopyTo(_samples);
                _readIndex = 0;
                _count = _samples.Length;
                return;
            }

            var overflow = Math.Max(0, _count + samples.Length - _samples.Length);
            _readIndex = (_readIndex + overflow) % _samples.Length;
            _count -= overflow;

            var writeIndex = (_readIndex + _count) % _samples.Length;
            var first = Math.Min(samples.Length, _samples.Length - writeIndex);
            samples[..first].CopyTo(_samples.AsSpan(writeIndex));
            samples[first..].CopyTo(_samples);
            _count += samples.Length;
        }
    }

    public int Read(Span<float> destination) => Read(destination, out _);

    public int Read(Span<float> destination, out int samplesRemaining)
    {
        lock (_sync)
        {
            var length = Math.Min(destination.Length, _count);
            var first = Math.Min(length, _samples.Length - _readIndex);
            _samples.AsSpan(_readIndex, first).CopyTo(destination);
            _samples.AsSpan(0, length - first).CopyTo(destination[first..]);
            _readIndex = (_readIndex + length) % _samples.Length;
            _count -= length;
            samplesRemaining = _count;
            return length;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _readIndex = 0;
            _count = 0;
        }
    }
}