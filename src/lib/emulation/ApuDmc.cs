using System;

namespace SR.Emulation.Nes;

internal sealed class ApuDmc(InterruptLines interrupts)
{
    private static readonly ushort[] PeriodsNtsc =
    [428, 380, 340, 320, 286, 254, 226, 214, 190, 160, 142, 128, 106, 84, 72, 54];
    private Action<ushort, Action<byte>>? _requestDma;
    private ushort _period = 428, _timer, _sampleAddress = 0xC000, _sampleLength = 1;
    private ushort _currentAddress, _bytesRemaining;
    private byte? _sampleBuffer;
    private byte _shift, _bitsRemaining = 8;
    private bool _silence = true, _irqEnabled, _loop;
    private bool _dmaPending;
    private int _loadDmaDelay;

    internal byte Output { get; private set; }
    internal bool Enabled => _bytesRemaining > 0;
    internal ushort BytesRemaining => _bytesRemaining;
    internal ushort CurrentAddress => _currentAddress;

    internal void Connect(Action<ushort, Action<byte>> requestDma) => _requestDma = requestDma;
    internal void WriteControl(byte value)
    {
        _irqEnabled = (value & 0x80) != 0;
        if (!_irqEnabled) interrupts.ApuDmcIrq = false;
        _loop = (value & 0x40) != 0;
        _period = PeriodsNtsc[value & 0x0F];
    }
    internal void WriteOutput(byte value) => Output = (byte)(value & 0x7F);
    internal void WriteAddress(byte value) => _sampleAddress = (ushort)(0xC000 | (value << 6));
    internal void WriteLength(byte value) => _sampleLength = (ushort)((value << 4) | 1);
    internal void SetEnabled(bool enabled)
    {
        interrupts.ApuDmcIrq = false;
        if (!enabled) _bytesRemaining = 0;
        else if (_bytesRemaining == 0)
        {
            RestartSample();
            if (!_sampleBuffer.HasValue) _loadDmaDelay = 3;
        }
    }
    internal void Clock()
    {
        FillSampleBuffer();
        if (_timer > 0) { _timer--; return; }
        _timer = (ushort)(_period - 1);
        if (!_silence)
        {
            if ((_shift & 1) != 0) { if (Output <= 125) Output += 2; }
            else if (Output >= 2) Output -= 2;
        }
        _shift >>= 1;
        if (--_bitsRemaining != 0) return;
        _bitsRemaining = 8;
        if (_sampleBuffer.HasValue)
        {
            _silence = false;
            _shift = _sampleBuffer.Value;
            _sampleBuffer = null;
        }
        else _silence = true;
    }
    private void FillSampleBuffer()
    {
        if (_sampleBuffer.HasValue || _bytesRemaining == 0 || _requestDma is null || _dmaPending) return;
        if (_loadDmaDelay > 0)
        {
            _loadDmaDelay--;
            if (_loadDmaDelay > 0) return;
        }
        _dmaPending = true;
        _requestDma(_currentAddress, CompleteDma);
    }

    private void CompleteDma(byte value)
    {
        _dmaPending = false;
        _sampleBuffer = value;
        _currentAddress = _currentAddress == 0xFFFF ? (ushort)0x8000 : (ushort)(_currentAddress + 1);
        _bytesRemaining--;
        if (_bytesRemaining != 0) return;
        if (_loop) RestartSample();
        else if (_irqEnabled) interrupts.ApuDmcIrq = true;
    }
    private void RestartSample() { _currentAddress = _sampleAddress; _bytesRemaining = _sampleLength; }
    internal void Reset()
    {
        _period = 428; _timer = 0; _sampleAddress = 0xC000; _sampleLength = 1;
        _currentAddress = _bytesRemaining = 0; _sampleBuffer = null; _shift = 0; _bitsRemaining = 8; _dmaPending = false;
        _silence = true; _irqEnabled = _loop = false; _loadDmaDelay = 0; Output = 0; interrupts.ApuDmcIrq = false;
    }
}
