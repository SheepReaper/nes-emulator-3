namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Manages DMC sample start address, length, and current play position.
/// </summary>
internal sealed class ApuDmcAddress
{
    private ushort _sampleAddress = 0xC000;
    private ushort _sampleLength = 1;

    internal ushort Current { get; private set; }
    internal ushort Remaining { get; private set; }
    internal ushort InitialLength => _sampleLength;
    internal bool HasBytes => Remaining > 0;

    internal void SetAddress(byte value) => _sampleAddress = (ushort)(0xC000 | (value << 6));
    internal void SetLength(byte value) => _sampleLength = (ushort)((value << 4) | 1);

    internal void Restart()
    {
        Current = _sampleAddress;
        Remaining = _sampleLength;
    }

    internal void ClearRemaining()
    {
        Remaining = 0;
    }

    internal bool Advance()
    {
        Current = Current == 0xFFFF ? (ushort)0x8000 : (ushort)(Current + 1);
        Remaining--;
        return Remaining == 0;
    }

    internal void Reset()
    {
        _sampleAddress = 0xC000;
        _sampleLength = 1;
        Current = 0;
        Remaining = 0;
    }
}
