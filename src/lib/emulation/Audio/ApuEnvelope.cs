namespace Sheep.Emulation.Nes.Audio;

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