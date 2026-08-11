namespace Sheep.Emulation.Nes.Audio;

internal sealed class ApuNoise(ApuRegion? region = null)
{
    private readonly ushort[] _periods = region == ApuRegion.Pal ? ApuTables.NoisePal : ApuTables.NoiseNtsc;
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
    internal bool LengthWasNonzeroWhenClocked => _lengthWasNonzeroWhenClocked;
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
        _timerPeriod = _periods[value & 0x0F];
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
