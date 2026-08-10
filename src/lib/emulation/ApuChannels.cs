using System;

namespace SR.Emulation.Nes;

internal static class ApuTables
{
    internal static readonly byte[] Length =
    [10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
     12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30];

    internal static readonly ushort[] NoiseNtsc =
    [4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068];
}

internal sealed class ApuEnvelope
{
    private byte _divider;
    private byte _decay;
    private bool _start;

    internal bool Loop { get; set; }
    internal bool Constant { get; set; }
    internal byte Period { get; set; }
    internal byte Output => Constant ? Period : _decay;

    internal void Restart() => _start = true;

    internal void Clock()
    {
        if (_start)
        {
            _start = false;
            _decay = 15;
            _divider = Period;
        }
        else if (_divider > 0) _divider--;
        else
        {
            _divider = Period;
            if (_decay > 0) _decay--;
            else if (Loop) _decay = 15;
        }
    }

    internal void Reset()
    {
        _divider = _decay = 0;
        _start = false;
    }
}

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
    private ushort _timerPeriod;
    private ushort _timer;
    private byte _sequence;
    private byte _duty;
    private bool _sweepEnabled;
    private byte _sweepPeriod;
    private bool _sweepNegate;
    private byte _sweepShift;
    private byte _sweepDivider;
    private bool _sweepReload;
    private bool? _pendingLengthHalt;
    private byte? _pendingLengthLoad;
    private bool _lengthWasNonzeroWhenClocked;

    internal bool Enabled { get; set; }
    internal byte Length { get; set; }
    internal ushort TimerPeriod => _timerPeriod;
    internal byte Output => !Enabled || Length == 0 || _timerPeriod < 8 || SweepTarget > 0x7FF || Duty[_duty][_sequence] == 0
        ? (byte)0 : _envelope.Output;

    private int SweepTarget => _timerPeriod + (_sweepNegate
        ? -((int)(_timerPeriod >> _sweepShift)) - (first ? 1 : 0)
        : (_timerPeriod >> _sweepShift));

    internal void WriteControl(byte value)
    {
        _duty = (byte)(value >> 6);
        _pendingLengthHalt = (value & 0x20) != 0;
        _envelope.Constant = (value & 0x10) != 0;
        _envelope.Period = (byte)(value & 0x0F);
    }

    internal void WriteSweep(byte value)
    {
        _sweepEnabled = (value & 0x80) != 0;
        _sweepPeriod = (byte)((value >> 4) & 7);
        _sweepNegate = (value & 8) != 0;
        _sweepShift = (byte)(value & 7);
        _sweepReload = true;
    }

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

    internal void ClockSweep()
    {
        if (_sweepDivider == 0 && _sweepEnabled && _sweepShift > 0 && SweepTarget <= 0x7FF && _timerPeriod >= 8)
            _timerPeriod = (ushort)SweepTarget;
        if (_sweepDivider == 0 || _sweepReload)
        {
            _sweepDivider = _sweepPeriod;
            _sweepReload = false;
        }
        else _sweepDivider--;
    }

    internal void Reset()
    {
        Enabled = false;
        Length = _sequence = _duty = _sweepPeriod = _sweepShift = _sweepDivider = 0;
        _timerPeriod = _timer = 0;
        _sweepEnabled = _sweepNegate = _sweepReload = false;
        _pendingLengthHalt = null;
        _pendingLengthLoad = null;
        _lengthWasNonzeroWhenClocked = false;
        _envelope.Reset();
    }
}

internal sealed class ApuTriangle
{
    private static readonly byte[] Sequence =
    [15,14,13,12,11,10,9,8,7,6,5,4,3,2,1,0,0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15];
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

internal sealed class ApuNoise
{
    private readonly ApuEnvelope _envelope = new();
    private ushort _timerPeriod = 4;
    private ushort _timer;
    private ushort _shift = 1;
    private bool _mode;
    private bool? _pendingLengthHalt;
    private byte? _pendingLengthLoad;
    private bool _lengthWasNonzeroWhenClocked;

    internal bool Enabled { get; set; }
    internal byte Length { get; set; }
    internal ushort TimerPeriod => _timerPeriod;
    internal byte Output => !Enabled || Length == 0 || (_shift & 1) != 0 ? (byte)0 : _envelope.Output;
    internal void WriteControl(byte value)
    {
        _pendingLengthHalt = (value & 0x20) != 0;
        _envelope.Constant = (value & 0x10) != 0;
        _envelope.Period = (byte)(value & 0x0F);
    }
    internal void WritePeriod(byte value)
    {
        _mode = (value & 0x80) != 0;
        _timerPeriod = ApuTables.NoiseNtsc[value & 0x0F];
    }
    internal void WriteLength(byte value)
    {
        if (Enabled) _pendingLengthLoad = ApuTables.Length[value >> 3];
        _envelope.Restart();
    }
    internal void ClockTimer()
    {
        if (_timer == 0)
        {
            _timer = _timerPeriod;
            var tap = _mode ? 6 : 1;
            var feedback = (ushort)((_shift & 1) ^ ((_shift >> tap) & 1));
            _shift = (ushort)((_shift >> 1) | (feedback << 14));
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
    internal void Reset()
    {
        Enabled = false;
        Length = 0;
        _timerPeriod = 4;
        _timer = 0;
        _shift = 1;
        _mode = false;
        _pendingLengthHalt = null;
        _pendingLengthLoad = null;
        _lengthWasNonzeroWhenClocked = false;
        _envelope.Reset();
    }
}
