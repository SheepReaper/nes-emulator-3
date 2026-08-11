namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>
/// Scanline counter, A12 filter, and IRQ generation for MMC3 mapper.
/// </summary>
internal sealed class Mmc3IrqTracker(InterruptLines interrupts)
{
    private byte _latch;
    private byte _counter;
    private bool _reload;
    private bool _enabled;
    private bool _a12High;
    private int _a12LowCpuClocks;

    internal void SetLatch(byte value) => _latch = value;

    internal void Reload()
    {
        _counter = 0;
        _reload = true;
    }

    internal void Disable()
    {
        _enabled = false;
        interrupts.MapperIrq = false;
    }

    internal void Enable() => _enabled = true;

    internal void NotifyPpuAddress(ushort address)
    {
        var a12High = (address & 0x1000) != 0;
        if (!a12High)
        {
            if (_a12High)
            {
                _a12LowCpuClocks = 0;
            }
            _a12High = false;
            return;
        }

        if (!_a12High && _a12LowCpuClocks >= 4)
        {
            ClockCounter();
        }
        _a12High = true;
    }

    internal void NotifyCpuClock()
    {
        if (!_a12High && _a12LowCpuClocks < 4)
        {
            _a12LowCpuClocks++;
        }
    }

    private void ClockCounter()
    {
        if (_counter == 0 || _reload)
        {
            _counter = _latch;
            _reload = false;
        }
        else
        {
            _counter--;
        }

        if (_counter == 0 && _enabled)
        {
            interrupts.MapperIrq = true;
        }
    }

    internal void Reset()
    {
        _latch = 0;
        _counter = 0;
        _reload = false;
        _enabled = false;
        _a12High = false;
        _a12LowCpuClocks = 0;
        interrupts.MapperIrq = false;
    }
}
