using System;

namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Manages the APU frame counter sequencer for both NTSC and PAL regions.
/// </summary>
internal sealed class ApuFrameCounter(InterruptLines interrupts, ApuRegion region)
{
    private readonly ApuFrameIrq _irq = new(interrupts);
    private int _frameCycle;
    private int _pendingDelay;
    private bool _fiveStepMode;
    private byte _pendingValue;

    internal bool HalfFrameClockedThisCycle { get; private set; }
    internal int FrameCycle => _frameCycle;
    internal bool FiveStepMode => _fiveStepMode;

    internal void Reset(byte pendingValue)
    {
        _frameCycle = 0;
        _pendingDelay = 0;
        _fiveStepMode = (pendingValue & 0x80) != 0;
        _irq.Inhibit = (pendingValue & 0x40) != 0;
        _irq.Reset();
    }

    internal void WriteRegister(byte value, ulong cpuClock)
    {
        _pendingValue = value;
        _pendingDelay = (cpuClock & 1) == 0 ? 4 : 3;
        if ((value & 0x40) != 0)
        {
            interrupts.ApuFrameIrq = false;
        }
    }

    internal void Clock(ulong cpuClock, Action clockQuarter, Action clockHalf)
    {
        HalfFrameClockedThisCycle = false;
        _irq.Clock();

        if (_pendingDelay > 0 && --_pendingDelay == 0)
        {
            _fiveStepMode = (_pendingValue & 0x80) != 0;
            _irq.Inhibit = (_pendingValue & 0x40) != 0;
            if (_irq.Inhibit)
            {
                interrupts.ApuFrameIrq = false;
            }
            _frameCycle = 0;
            if (_fiveStepMode)
            {
                DoQuarterAndHalf(clockQuarter, clockHalf);
            }
        }

        _frameCycle++;
        var t = ApuFrameSequencerTable.GetTable(region, _fiveStepMode);
        if (_frameCycle == t[0] || _frameCycle == t[2])
        {
            clockQuarter();
        }
        else if (_frameCycle == t[1])
        {
            DoQuarterAndHalf(clockQuarter, clockHalf);
        }

        if (_frameCycle == t[3] && t[3] > 0)
        {
            _irq.Trigger();
        }

        if (_frameCycle == t[4])
        {
            DoQuarterAndHalf(clockQuarter, clockHalf);
            if (t[5] == t[4])
            {
                _frameCycle = 0;
            }
        }
        else if (_frameCycle == t[5] && t[5] != t[4])
        {
            _frameCycle = 0;
        }
    }

    internal void HandleIrqClearOnEvenClock(ulong cpuClock) => _irq.HandleClearOnEvenClock(cpuClock);
    internal void RequestIrqClear(ulong cpuClock) => _irq.RequestClear(cpuClock);

    private void DoQuarterAndHalf(Action clockQuarter, Action clockHalf)
    {
        HalfFrameClockedThisCycle = true;
        clockQuarter();
        clockHalf();
    }
}
