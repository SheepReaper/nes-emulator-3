namespace Sheep.Emulation.Nes.Audio;

internal sealed class ApuPulse(bool first)
{
    private static readonly byte[][] Duty =
    [
        [0, 1, 0, 0, 0, 0, 0, 0],
        [0, 1, 1, 0, 0, 0, 0, 0],
        [0, 1, 1, 1, 1, 0, 0, 0],
        [1, 0, 0, 1, 1, 1, 1, 1]
    ];

    private readonly ApuEnvelope _envelope = new();
    private readonly ApuPulseSweep _sweep = new(first);
    private ushort _timerPeriod;
    private ushort _timer;
    private byte _sequence;
    private byte _duty;
    private bool? _pendingLengthHalt;
    private byte? _pendingLengthLoad;
    private bool _lengthWasNonzeroWhenClocked;

    internal bool Enabled { get; set; }
    internal byte Length { get; set; }
    internal bool LengthWasNonzeroWhenClocked => _lengthWasNonzeroWhenClocked;
    internal ushort TimerPeriod => _timerPeriod;
    internal byte Output => !Enabled || Length == 0 || _timerPeriod < 8 || _sweep.Target(_timerPeriod) > 0x7FF || Duty[_duty][_sequence] == 0
        ? (byte)0 : _envelope.Output;

    internal void WriteControl(byte value)
    {
        _duty = (byte)(value >> 6);
        _pendingLengthHalt = (value & 0x20) != 0;
        _envelope.Constant = (value & 0x10) != 0;
        _envelope.Period = (byte)(value & 0x0F);
    }

    internal void WriteSweep(byte value) => _sweep.Write(value);
    internal void WriteTimerLow(byte value) => _timerPeriod = (ushort)((_timerPeriod & 0x700) | value);
    internal void WriteTimerHigh(byte value)
    {
        _timerPeriod = (ushort)((_timerPeriod & 0xFF) | ((value & 7) << 8));
        if (Enabled) _pendingLengthLoad = ApuTables.Length[value >> 3];
        _sequence = 0;
        _envelope.Restart();
    }

    internal void ClockTimer()
    {
        if (_timer == 0)
        {
            _timer = _timerPeriod;
            _sequence = (byte)((_sequence + 1) & 7);
        }
        else _timer--;
    }

    internal void ClockEnvelope() => _envelope.Clock();
    internal void ClockLength()
    {
        _lengthWasNonzeroWhenClocked = Length > 0;
        if (!_envelope.Loop && Length > 0) Length--;
    }

    internal void CommitDeferredWrites(bool halfFrameClocked)
    {
        if (_pendingLengthLoad.HasValue)
        {
            if (!halfFrameClocked || !_lengthWasNonzeroWhenClocked) Length = _pendingLengthLoad.Value;
            _pendingLengthLoad = null;
        }
        if (_pendingLengthHalt.HasValue)
        {
            _envelope.Loop = _pendingLengthHalt.Value;
            _pendingLengthHalt = null;
        }
        _lengthWasNonzeroWhenClocked = false;
    }

    internal void ClockSweep() => _timerPeriod = _sweep.Clock(_timerPeriod);

    internal void Reset()
    {
        Enabled = false;
        Length = _sequence = _duty = 0;
        _timerPeriod = _timer = 0;
        _pendingLengthHalt = null;
        _pendingLengthLoad = null;
        _lengthWasNonzeroWhenClocked = false;
        _sweep.Reset();
        _envelope.Reset();
    }
}