using System;

namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Manages DMC DMA fetching, sample buffer, and loop/IRQ logic.
/// </summary>
internal sealed class ApuDmcReader(InterruptLines interrupts)
{
    private readonly ApuDmcDelays _delays = new();
    private readonly ApuDmcAddress _addr = new();
    private Action<ushort, Action<byte>?>? _requestDma;
    private Action? _abortDma;
    private byte? _sampleBuffer;
    private bool _irqEnabled;
    private bool _loop;
    private bool _dmaPending;
    private bool _enabled;
    private int _abortDmaCountdown;
    private int _scheduledImplicitAbortDelay;

    internal bool Enabled => _addr.HasBytes;
    internal ushort BytesRemaining => _addr.Remaining;
    internal ushort CurrentAddress => _addr.Current;
    internal ref byte? SampleBufferRef => ref _sampleBuffer;

    internal void Connect(Action<ushort, Action<byte>?> requestDma, Action? abortDma = null)
    {
        _requestDma = requestDma;
        _abortDma = abortDma;
    }

    internal void SetControl(bool irqEnabled, bool loop)
    {
        _irqEnabled = irqEnabled;
        if (!_irqEnabled)
        {
            interrupts.ApuDmcIrq = false;
        }
        _loop = loop;
    }

    internal void SetSampleAddress(byte value) => _addr.SetAddress(value);
    internal void SetSampleLength(byte value) => _addr.SetLength(value);

    internal void SetEnabled(bool enabled, ushort dmcTimer, ulong cpuClock)
    {
        interrupts.ApuDmcIrq = false;
        var implicitAbortDelay = enabled && !_loop && !_addr.HasBytes && !_sampleBuffer.HasValue
            ? dmcTimer switch
            {
                7 => 2,
                8 => 3,
                _ => 0
            }
            : 0;
        var delayLoopingReload = enabled && _loop && !_addr.HasBytes && !_sampleBuffer.HasValue
            && dmcTimer is 5 or 6;
        _enabled = enabled;
        if (!enabled)
        {
            _abortDmaCountdown = 0;
            _scheduledImplicitAbortDelay = 0;
        }
        _delays.SetEnabled(enabled, _addr.HasBytes, _sampleBuffer.HasValue, cpuClock);
        if (delayLoopingReload)
        {
            _delays.ExtendLoadDmaDelay(2);
        }
        if (implicitAbortDelay > 0)
        {
            _scheduledImplicitAbortDelay = implicitAbortDelay;
        }
        if (enabled)
        {
            if (!_addr.HasBytes)
            {
                // test baseline
                _addr.Restart();
            }
        }
    }

    internal void ClockDelays()
    {
        if (_abortDmaCountdown > 0 && --_abortDmaCountdown == 0)
        {
            _requestDma?.Invoke(0, null);
        }
        if (_delays.ClockDisableDelay())
        {
            _addr.ClearRemaining();
            if (_dmaPending)
            {
                _dmaPending = false;
                _abortDma?.Invoke();
            }
        }
        _delays.ClockEnableDelay();
    }



    internal void FillSampleBuffer()
    {
        if (_sampleBuffer.HasValue || !_addr.HasBytes || _requestDma is null || _dmaPending
            || _delays.EnableDelayActive)
        {
            return;
        }
        if (_delays.ShouldWaitLoadDma())
        {
            return;
        }
        _dmaPending = true;
        _requestDma(_addr.Current, CompleteDma);
    }

    private void CompleteDma(byte value)
    {
        _dmaPending = false;
        _sampleBuffer = value;
        if (_scheduledImplicitAbortDelay > 0)
        {
            _abortDmaCountdown = _scheduledImplicitAbortDelay;
            _scheduledImplicitAbortDelay = 0;
        }
        if (_addr.Advance())
        {
            if (_loop)
            {
                _addr.Restart();
            }
            else if (_irqEnabled)
            {
                interrupts.ApuDmcIrq = true;
            }
        }
    }

    internal void Reset()
    {
        _addr.Reset();
        _sampleBuffer = null;
        _dmaPending = false;
        _irqEnabled = false;
        _loop = false;
        _enabled = false;
        _abortDmaCountdown = 0;
        _scheduledImplicitAbortDelay = 0;
        _delays.Reset();
    }
}
