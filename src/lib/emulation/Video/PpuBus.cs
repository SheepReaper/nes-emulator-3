using System;

namespace Sheep.Emulation.Nes.Video;

public sealed class PpuBus(CartridgeSlot cartridgeSlot) : IBus
{
    private readonly byte[] _vRam = new byte[0x1000];
    private readonly byte[] _paletteRam = new byte[0x20];
    private ulong _ppuCycle;

    public byte Read(ushort address)
    {
        address &= 0x3FFF;
        ObservePpuAddress(address);

        return address switch
        {
            <= 0x1FFF => cartridgeSlot.PpuRead(address),
            <= 0x3EFF => ReadNametable(address),
            _ => _paletteRam[PpuBusAddressMapper.GetPaletteRamAddress(address)]
        };
    }

    public void Write(ushort address, byte value)
    {
        address &= 0x3FFF;
        ObservePpuAddress(address);

        switch (address)
        {
            case <= 0x1FFF:
                cartridgeSlot.PpuWrite(address, value);
                break;
            case <= 0x3EFF:
                if (cartridgeSlot.Cartridge?.TryWriteNametable(address, value, _vRam) != true)
                {
                    var mirroring = cartridgeSlot.Cartridge?.NametableMirroring ?? NametableMirroring.Horizontal;
                    _vRam[PpuBusAddressMapper.GetVramAddress(address, mirroring)] = value;
                }
                break;
            default:
                _paletteRam[PpuBusAddressMapper.GetPaletteRamAddress(address)] = value;
                break;
        }
    }

    internal int VramSize => _vRam.Length;
    internal int PaletteRamSize => _paletteRam.Length;
    internal void CopyVram(int offset, Span<byte> destination) => _vRam.AsSpan(offset, destination.Length).CopyTo(destination);
    internal void CopyPaletteRam(int offset, Span<byte> destination) => _paletteRam.AsSpan(offset, destination.Length).CopyTo(destination);
    internal void WriteVram(int offset, ReadOnlySpan<byte> source) => source.CopyTo(_vRam.AsSpan(offset, source.Length));
    internal void WritePaletteRam(int offset, ReadOnlySpan<byte> source) => source.CopyTo(_paletteRam.AsSpan(offset, source.Length));

    internal byte Peek(ushort address)
    {
        address &= 0x3FFF;
        return address switch
        {
            <= 0x1FFF => cartridgeSlot.PpuPeek(address),
            <= 0x3EFF => ReadNametable(address),
            _ => _paletteRam[PpuBusAddressMapper.GetPaletteRamAddress(address)]
        };
    }

    internal byte PeekPalette(ushort address) => _paletteRam[PpuBusAddressMapper.GetPaletteRamAddress(address)];

    internal void AdvanceCycle() => _ppuCycle++;
    internal void AdvanceCycles(int count) => _ppuCycle += (uint)count;
    internal void ResetCycle() => _ppuCycle = 0;

    internal void NotifyPpuAddress(ushort address)
    {
        address &= 0x3FFF;
        if (address >= 0x3F00)
        {
            address -= 0x1000;
        }
        cartridgeSlot.NotifyPpuAddress(address, _ppuCycle);
    }

    private void ObservePpuAddress(ushort address)
    {
        if (address <= 0x2FFF)
        {
            cartridgeSlot.NotifyPpuAddress(address, _ppuCycle);
        }
        else if (address <= 0x3EFF)
        {
            cartridgeSlot.NotifyPpuAddress((ushort)(address - 0x1000), _ppuCycle);
        }
    }

    private byte ReadNametable(ushort address)
    {
        if (cartridgeSlot.Cartridge?.TryReadNametable(address, _vRam, out var value) == true)
        {
            return value;
        }
        var mirroring = cartridgeSlot.Cartridge?.NametableMirroring ?? NametableMirroring.Horizontal;
        return _vRam[PpuBusAddressMapper.GetVramAddress(address, mirroring)];
    }
}