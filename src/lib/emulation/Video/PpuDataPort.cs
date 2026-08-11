namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Handles $2007 read and write operations and VRAM auto-incrementing.
/// </summary>
internal sealed class PpuDataPort
{
    private byte _dataBuffer;
    private byte _previousDataBuffer;
    private ulong _lastReadDot;

    internal byte DataBuffer => _dataBuffer;

    internal void Reset()
    {
        _dataBuffer = 0;
        _previousDataBuffer = 0;
        _lastReadDot = 0;
    }

    internal byte Read(
        IBus bus,
        PpuScrollRegisters scroll,
        PpuIoLatch ioLatch,
        ulong elapsedDots,
        ulong decayDots,
        bool vramIncrement32,
        PpuBus? ppuBus,
        bool isRenderingActive = false,
        bool grayscale = false)
    {
        var address = (ushort)(scroll.VramAddress & 0x3FFF);
        var busValue = bus.Read(address);
        byte value;
        if (address >= 0x3F00)
        {
            var paletteMask = grayscale ? 0x30 : 0x3F;
            value = (byte)((busValue & paletteMask) | (ioLatch.Read(elapsedDots) & 0xC0));
            _dataBuffer = bus.Read((ushort)(address - 0x1000));
            _previousDataBuffer = _dataBuffer;
            ioLatch.Drive(value, elapsedDots, decayDots, (byte)paletteMask);
        }
        else
        {
            if (elapsedDots > 0 && elapsedDots - _lastReadDot <= 4)
            {
                value = _previousDataBuffer;
            }
            else
            {
                value = _dataBuffer;
            }
            _previousDataBuffer = _dataBuffer;
            _dataBuffer = busValue;
            ioLatch.Drive(value, elapsedDots, decayDots);
        }
        _lastReadDot = elapsedDots;
        IncrementDataAddress(scroll, vramIncrement32, ppuBus, isRenderingActive);
        return value;
    }

    internal void Write(
        IBus bus,
        byte value,
        PpuScrollRegisters scroll,
        bool vramIncrement32,
        PpuBus? ppuBus,
        bool isRenderingActive = false)
    {
        bus.Write((ushort)(scroll.VramAddress & 0x3FFF), value);
        IncrementDataAddress(scroll, vramIncrement32, ppuBus, isRenderingActive);
    }

    private static void IncrementDataAddress(
        PpuScrollRegisters scroll,
        bool vramIncrement32,
        PpuBus? ppuBus,
        bool isRenderingActive)
    {
        if (isRenderingActive)
        {
            scroll.IncrementHorizontal();
            scroll.IncrementVertical();
        }
        else
        {
            scroll.VramAddress = (ushort)((scroll.VramAddress + (vramIncrement32 ? 32 : 1)) & 0x7FFF);
        }
        ppuBus?.NotifyPpuAddress((ushort)(scroll.VramAddress & 0x3FFF));
    }
}
