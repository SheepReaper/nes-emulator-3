using System;

namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// OAM sprite memory storage and DMA access.
/// </summary>
internal sealed class PpuOam
{
    private readonly byte[] _oam = new byte[256];
    internal byte Address { get; set; }

    internal byte this[int index] => _oam[index];

    internal void Reset()
    {
        Array.Clear(_oam, 0, _oam.Length);
        Address = 0;
    }

    internal byte Read()
    {
        var value = _oam[Address];
        if ((Address & 0x03) == 2)
        {
            value &= 0xE3;
        }
        return value;
    }

    internal void Write(byte value) => _oam[Address++] = value;

    internal void DmaTransfer(ReadOnlySpan<byte> data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            _oam[(byte)(Address + i)] = data[i];
        }
    }

    internal void DmaWriteByte(byte value) => _oam[Address++] = value;

    internal void Copy(int offset, Span<byte> destination) =>
        _oam.AsSpan(offset, destination.Length).CopyTo(destination);

    internal void WriteSpan(int offset, ReadOnlySpan<byte> source) =>
        source.CopyTo(_oam.AsSpan(offset, source.Length));
}
