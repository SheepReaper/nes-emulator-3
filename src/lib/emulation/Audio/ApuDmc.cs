using System;

namespace Sheep.Emulation.Nes.Audio;

internal sealed class ApuDmc(InterruptLines interrupts, ApuRegion? region = null)
{
    private readonly ushort[] _periods = region == ApuRegion.Pal ? ApuTables.DmcPal : ApuTables.DmcNtsc;
    private readonly ApuDmcOutput _output = new();
    private readonly ApuDmcReader _reader = new(interrupts);
    private ushort _period = region == ApuRegion.Pal ? (ushort)398 : (ushort)428;
    private ushort _timer;

    internal byte Output => _output.Level;
    internal bool Enabled => _reader.Enabled;
    internal ushort BytesRemaining => _reader.BytesRemaining;
    internal ushort CurrentAddress => _reader.CurrentAddress;
    internal ushort Period => _period;

    internal void Connect(Action<ushort, Action<byte>?> requestDma, Action? abortDma = null) =>
        _reader.Connect(requestDma, abortDma);

    internal void WriteControl(byte value)
    {
        _reader.SetControl((value & 0x80) != 0, (value & 0x40) != 0);
        _period = _periods[value & 0x0F];
    }

    internal void WriteOutput(byte value) => _output.SetLevel(value);
    internal void WriteAddress(byte value) => _reader.SetSampleAddress(value);
    internal void WriteLength(byte value) => _reader.SetSampleLength(value);
    internal void SetEnabled(bool enabled, ulong cpuClock) => _reader.SetEnabled(enabled, _timer, cpuClock);

    internal void Clock()
    {
        _reader.ClockDelays();
        if (_timer > 0)
        {
            _timer--;
        }
        else
        {
            _timer = (ushort)(_period - 1);
            _output.ClockLevel();
            _output.ClockShift(ref _reader.SampleBufferRef);
        }
        _reader.FillSampleBuffer();
    }

    internal void Reset()
    {
        _period = _periods[0];
        _timer = 0;
        _output.Reset();
        _reader.Reset();
        interrupts.ApuDmcIrq = false;
    }
}