namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Emulates PPU open bus / IO latch and capacitance decay over PPU dots.
/// </summary>
internal sealed class PpuIoLatch
{
    private byte _ioLatch;
    private readonly ulong[] _expiry = new ulong[8];

    internal byte Read(ulong elapsedPpuDots)
    {
        for (var bit = 0; bit < _expiry.Length; bit++)
        {
            if (_expiry[bit] != 0 && elapsedPpuDots >= _expiry[bit])
            {
                _ioLatch = (byte)(_ioLatch & ~(1 << bit));
                _expiry[bit] = 0;
            }
        }
        return _ioLatch;
    }

    internal void Drive(byte value, ulong elapsedPpuDots, ulong decayDots, byte mask = 0xFF)
    {
        var current = Read(elapsedPpuDots);
        _ioLatch = (byte)((current & ~mask) | (value & mask));
        for (var bit = 0; bit < _expiry.Length; bit++)
        {
            if ((mask & (1 << bit)) != 0)
            {
                _expiry[bit] = elapsedPpuDots + decayDots;
            }
        }
    }

    internal void Reset()
    {
        _ioLatch = 0;
        for (var i = 0; i < _expiry.Length; i++)
        {
            _expiry[i] = 0;
        }
    }
}
