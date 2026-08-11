namespace Sheep.Emulation.Nes.Audio;

internal sealed class ApuTriangle
{
    private static readonly byte[] Sequence =
    [15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
    private ushort _timerPeriod;
    private ushort _timer;
    private byte _sequence;
    private byte _linearReloadValue;
    private byte _linearCounter;
    private bool _control;
    private bool _linearReload;
    private byte? _pendingLengthLoad;
    private bool _lengthWasNonzeroWhenClocked;

    internal bool Enabled { get; set; }
    internal byte Length { get; set; }
    internal bool LengthWasNonzeroWhenClocked => _lengthWasNonzeroWhenClocked;
    internal ushort TimerPeriod => _timerPeriod;
    internal byte Output => Sequence[_sequence];

    internal void WriteControl(byte value)
    {
        _control = (value & 0x80) != 0;
        _linearReloadValue = (byte)(value & 0x7F);
    }
    internal void WriteTimerLow(byte value) => _timerPeriod = (ushort)((_timerPeriod & 0x700) | value);
    internal void WriteTimerHigh(byte value)
    {
        _timerPeriod = (ushort)((_timerPeriod & 0xFF) | ((value & 7) << 8));
        if (Enabled) _pendingLengthLoad = ApuTables.Length[value >> 3];
        _linearReload = true;
    }
    internal void ClockTimer()
    {
        if (_timer == 0)
        {
            _timer = _timerPeriod;
            if (Length > 0 && _linearCounter > 0 && _timerPeriod > 1) _sequence = (byte)((_sequence + 1) & 31);
        }
        else _timer--;
    }
    internal void ClockLinear()
    {
        if (_linearReload) _linearCounter = _linearReloadValue;
        else if (_linearCounter > 0) _linearCounter--;
        if (!_control) _linearReload = false;
    }
    internal void ClockLength()
    {
        _lengthWasNonzeroWhenClocked = Length > 0;
        if (!_control && Length > 0) Length--;
    }
    internal void CommitDeferredWrites(bool halfFrameClocked)
    {
        if (_pendingLengthLoad.HasValue)
        {
            if (!halfFrameClocked || !_lengthWasNonzeroWhenClocked) Length = _pendingLengthLoad.Value;
            _pendingLengthLoad = null;
        }
        _lengthWasNonzeroWhenClocked = false;
    }
    internal void Reset()
    {
        Enabled = false;
        Length = _sequence = _linearReloadValue = _linearCounter = 0;
        _timerPeriod = _timer = 0;
        _control = _linearReload = false;
        _pendingLengthLoad = null;
        _lengthWasNonzeroWhenClocked = false;
    }
}