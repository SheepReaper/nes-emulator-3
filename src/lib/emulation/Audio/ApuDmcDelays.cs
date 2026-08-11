namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Manages DMC DMA enable, disable, and load delays.
/// </summary>
internal sealed class ApuDmcDelays
{
    private int _loadDmaDelay;
    private int _disableDelay;
    private int _enableDelay;

    internal bool EnableDelayActive => _enableDelay > 0;
    internal bool LoadDmaDelayActive => _loadDmaDelay > 0;
    internal void CancelLoadDmaDelay() => _loadDmaDelay = 0;
    internal void ExtendLoadDmaDelay(int cycles) => _loadDmaDelay += cycles;

    internal void SetEnabled(bool enabled, bool hasBytes, bool hasSample, ulong cpuClock)
    {
        if (!enabled)
        {
            _enableDelay = 0;
            if (hasBytes && _disableDelay == 0)
            {
                _disableDelay = (cpuClock & 1) == 0 ? 4 : 3;
            }
        }
        else
        {
            _disableDelay = 0;
            if (!hasBytes)
            {
                if (!hasSample)
                {
                    _loadDmaDelay = (cpuClock & 1) == 0 ? 4 : 3;
                }
                else
                {
                    _enableDelay = (cpuClock & 1) == 0 ? 4 : 3;
                }
            }
        }
    }

    internal bool ClockDisableDelay()
    {
        return _disableDelay > 0 && --_disableDelay == 0;
    }

    internal bool ClockEnableDelay()
    {
        return _enableDelay > 0 && --_enableDelay == 0;
    }

    internal bool ShouldWaitLoadDma()
    {
        return _loadDmaDelay > 0 && --_loadDmaDelay > 0;
    }

    internal void Reset()
    {
        _loadDmaDelay = 0;
        _disableDelay = 0;
        _enableDelay = 0;
    }
}
