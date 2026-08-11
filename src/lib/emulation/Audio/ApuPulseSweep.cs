namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Encapsulates pulse sweep unit calculations and state.
/// </summary>
internal sealed class ApuPulseSweep(bool first)
{
    private bool _enabled;
    private byte _period;
    private bool _negate;
    private byte _shift;
    private byte _divider;
    private bool _reload;

    internal void Write(byte value)
    {
        _enabled = (value & 0x80) != 0;
        _period = (byte)((value >> 4) & 7);
        _negate = (value & 8) != 0;
        _shift = (byte)(value & 7);
        _reload = true;
    }

    internal int Target(ushort timerPeriod) => timerPeriod + (_negate
        ? -(timerPeriod >> _shift) - (first ? 1 : 0)
        : (timerPeriod >> _shift));

    internal ushort Clock(ushort timerPeriod)
    {
        var target = Target(timerPeriod);
        if (_divider == 0 && _enabled && _shift > 0 && target <= 0x7FF && timerPeriod >= 8)
            timerPeriod = (ushort)target;

        if (_divider == 0 || _reload)
        {
            _divider = _period;
            _reload = false;
        }
        else _divider--;

        return timerPeriod;
    }

    internal void Reset()
    {
        _enabled = _negate = _reload = false;
        _period = _shift = _divider = 0;
    }
}
