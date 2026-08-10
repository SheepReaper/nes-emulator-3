using System;

namespace SR.Emulation.Nes;

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
        lock (_sync)
        {
            if (_count == _samples.Length)
            {
                _readIndex = (_readIndex + 1) % _samples.Length;
                _count--;
            }

            _samples[(_readIndex + _count) % _samples.Length] = sample;
            _count++;
        }
    }

    public int Read(Span<float> destination)
    {
        lock (_sync)
        {
            var length = Math.Min(destination.Length, _count);
            var first = Math.Min(length, _samples.Length - _readIndex);
            _samples.AsSpan(_readIndex, first).CopyTo(destination);
            _samples.AsSpan(0, length - first).CopyTo(destination[first..]);
            _readIndex = (_readIndex + length) % _samples.Length;
            _count -= length;
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
