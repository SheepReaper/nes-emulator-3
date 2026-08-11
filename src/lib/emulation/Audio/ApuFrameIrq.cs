namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Handles frame IRQ generation, pulse counter, and delayed clearing.
/// </summary>
internal sealed class ApuFrameIrq(InterruptLines interrupts)
{
    private int _pulseRemaining;
    private bool _clearPending;

    internal bool Inhibit
    {
        get => interrupts.ApuFrameIrqInhibited;
        set => interrupts.ApuFrameIrqInhibited = value;
    }

    internal void Clock()
    {
        if (_pulseRemaining > 0)
        {
            _pulseRemaining--;
            if (_pulseRemaining > 0)
            {
                interrupts.ApuFrameIrq = true;
                _clearPending = false;
            }
            else if (Inhibit)
            {
                interrupts.ApuFrameIrq = false;
            }
        }
    }

    internal void Trigger()
    {
        interrupts.ApuFrameIrq = true;
        _pulseRemaining = Inhibit ? 2 : 3;
    }

    internal void HandleClearOnEvenClock(ulong cpuClock)
    {
        if (_clearPending)
        {
            interrupts.ApuFrameIrq = false;
            _clearPending = false;
        }
    }

    internal void RequestClear(ulong cpuClock)
    {
        _clearPending = true;
    }

    internal void Reset()
    {
        _pulseRemaining = 0;
        _clearPending = false;
        Inhibit = false;
        interrupts.ApuFrameIrq = false;
    }
}
