namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Output level and sample shift logic for APU DMC channel.
/// </summary>
internal sealed class ApuDmcOutput
{
    private byte _shift;
    private byte _bitsRemaining = 8;
    private bool _silence = true;

    internal byte Level { get; private set; }
    internal bool Silence => _silence;

    internal void SetLevel(byte value)
    {
        Level = (byte)(value & 0x7F);
    }

    internal void ClockLevel()
    {
        if (_silence)
        {
            return;
        }

        if ((_shift & 1) != 0)
        {
            if (Level <= 125)
            {
                Level += 2;
            }
        }
        else if (Level >= 2)
        {
            Level -= 2;
        }
    }

    internal void ClockShift(ref byte? sampleBuffer)
    {
        _shift >>= 1;
        _bitsRemaining--;
        if (_bitsRemaining > 0)
        {
            return;
        }

        _bitsRemaining = 8;
        _silence = !sampleBuffer.HasValue;
        if (sampleBuffer.HasValue)
        {
            _shift = sampleBuffer.Value;
            sampleBuffer = null;
        }
    }

    internal void Reset()
    {
        _shift = 0;
        _bitsRemaining = 8;
        _silence = true;
        Level = 0;
    }
}
