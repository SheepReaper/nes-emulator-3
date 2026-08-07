using System;
using System.Diagnostics;

using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;

public sealed class Ppu(InterruptLines interrupts) : IBusMaster, IBusDevice
{
    private readonly byte[] _oam = new byte[256]; // Object Attribute Memory for 64 sprites
    private byte _oamAddress;

    private PpuCtrl _ppuCtrl;
    private PpuMask _ppuMask;
    private IBus? _bus;
    private PpuStatus _ppuStatus;

    // Internal PPU registers for VRAM addressing and scrolling
    private ushort _vramAddress;      // Current VRAM address (v)
    private ushort _tempVramAddress;  // Temporary VRAM address (t)
    private byte _fineXScroll;        // Fine X scroll (x)
    private bool _writeToggle;        // Write latch (w)
    private byte _dataBuffer;         // Internal read buffer for PPUDATA

    // PPU timing state
    private int _scanline;
    private int _cycle;

    public void ConnectBus(IBus bus)
    {
        _bus = bus;
    }

    public byte Read(ushort address)
    {
        // The 8 PPU registers are mirrored from $2008 to $3FFF.
        var register = (ushort)(address & 0x2007);
        return register switch
        {
            0x2002 => ReadStatus(),
            0x2004 => _oam[_oamAddress], // Read from OAMDATA
            0x2007 => ReadData(), // Read from PPUDATA
            _ => 0,
        };
    }

    public void Write(ushort address, byte value)
    {
        // The 8 PPU registers are mirrored from $2008 to $3FFF.
        var register = (ushort)(address & 0x2007);

        Action write = register switch
        {
            0x2000 => () => // PPUCTRL
            {
                _ppuCtrl.Value = value;
                // Update nametable bits in t from PPUCTRL
                _tempVramAddress = (ushort)((_tempVramAddress & 0xF3FF) | ((value & 0x03) << 10));
            }
            ,
            0x2001 => () => _ppuMask.Value = value, // Set PPUMASK
            0x2003 => () => _oamAddress = value, // Set OAMADDR
            0x2004 => () => _oam[_oamAddress++] = value, // Write to OAMDATA and increment address
            0x2005 => () => // PPUSCROLL
            {
                if (!_writeToggle)
                {
                    // First write: coarse X scroll and fine X scroll
                    _fineXScroll = (byte)(value & 0x07);
                    _tempVramAddress = (ushort)((_tempVramAddress & 0xFFE0) | (value >> 3)); // Coarse X
                }
                else
                {
                    // Second write: coarse Y scroll and fine Y scroll
                    _tempVramAddress = (ushort)((_tempVramAddress & 0x8FFF) | ((value & 0x07) << 12)); // Fine Y
                    _tempVramAddress = (ushort)((_tempVramAddress & 0xFC1F) | ((value & 0xF8) << 2)); // Coarse Y
                }
                _writeToggle = !_writeToggle;
            },
            0x2006 => () => // PPUADDR
            {
                if (!_writeToggle)
                {
                    // First write: high byte of VRAM address
                    _tempVramAddress = (ushort)((_tempVramAddress & 0x00FF) | ((value & 0x3F) << 8));
                }
                else
                {
                    // Second write: low byte of VRAM address
                    _tempVramAddress = (ushort)((_tempVramAddress & 0xFF00) | value);
                    _vramAddress = _tempVramAddress; // Transfer t to v
                }
                _writeToggle = !_writeToggle;
            },
            0x2007 => () => WriteData(value), // PPUDATA
            _ => () => { }
        };

        write();
    }

    public void DmaTransfer(ReadOnlySpan<byte> data)
    {
        data.CopyTo(_oam);
        // OAMDMA resets the OAM address register
        _oamAddress = 0;
    }

    public void Clock()
    {
        // The PPU renders 262 scanlines per frame.
        // Each scanline has 341 PPU clock cycles.

        // Scanlines 0-239 are the visible scanlines where pixel data is fetched.
        // Scanline 240 is a post-render scanline (idle).
        // Scanlines 241-260 are the VBlank period.
        // Scanline 261 is the pre-render scanline.

        if (_scanline == 241 && _cycle == 1)
        {
            // Set VBlank flag at the start of the VBlank period
            _ppuStatus.VBlank = true;
            if (_ppuCtrl.VBlankNmiEnable)
            {
                interrupts.Nmi = true;
            }
        }

        if (_scanline == 261 && _cycle == 1)
        {
            // Clear VBlank, Sprite 0 Hit, and Sprite Overflow flags
            _ppuStatus.VBlank = false;
            _ppuStatus.Sprite0Hit = false;
            _ppuStatus.SpriteOverflow = false;
        }

        // Advance cycle and scanline counters
        _cycle++;
        if (_cycle > 340)
        {
            _cycle = 0;
            _scanline++;
            if (_scanline > 261)
            {
                _scanline = 0;
            }
        }
    }

    private byte ReadStatus()
    {
        Debug.Assert(_bus != null, "PPU bus is not connected.");
        // Reading the status register has side effects:
        // 1. It clears the VBlank flag.
        // 2. It resets the address latch used by PPUADDR and PPUSCROLL
        var status = (byte)(_ppuStatus.Value & 0xE0); // Mask out the lower 5 bits
        _ppuStatus.VBlank = false; // Clear the VBlank flag
        _writeToggle = false; // Reset the address latch
        return status;
    }

    private byte ReadData()
    {
        Debug.Assert(_bus != null, "PPU bus is not connected.");
        var data = _dataBuffer;
        // The read buffer is immediately updated with the value from VRAM
        _dataBuffer = _bus.Read(_vramAddress);

        // For reads from palette RAM, the data is not buffered, so we return it directly.
        if ((_vramAddress & 0x3F00) == 0x3F00) // Palette RAM
        {
            data = _dataBuffer;
        }

        // Increment VRAM address after the read
        _vramAddress += (ushort)(_ppuCtrl.VramIncrement ? 32 : 1); // Increment by 1 or 32 based on PPUCTRL
        return data;
    }

    private void WriteData(byte value)
    {
        Debug.Assert(_bus != null, "PPU bus is not connected.");
        _bus.Write(_vramAddress, value);
        // Increment VRAM address after the write
        _vramAddress += (ushort)(_ppuCtrl.VramIncrement ? 32 : 1); // Increment by 1 or 32 based on PPUCTRL
    }
}
