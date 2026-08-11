using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>
/// 8 KB Cartridge RAM buffer with enable and write-protect flags.
/// </summary>
internal sealed class CartridgeRam8K
{
    private readonly byte[] _ram = new byte[0x2000];

    internal int Size => _ram.Length;
    internal bool Enabled { get; set; } = true;
    internal bool WriteProtected { get; set; }

    internal byte Read(ushort address) => Enabled ? _ram[address - 0x6000] : (byte)0;

    internal void Write(ushort address, byte value)
    {
        if (Enabled && !WriteProtected)
        {
            _ram[address - 0x6000] = value;
        }
    }

    internal void CopyTo(int offset, Span<byte> destination) =>
        _ram.AsSpan(offset, destination.Length).CopyTo(destination);

    internal void WriteFrom(int offset, ReadOnlySpan<byte> source) =>
        source.CopyTo(_ram.AsSpan(offset, source.Length));

    internal void Reset()
    {
        Enabled = true;
        WriteProtected = false;
    }
}
