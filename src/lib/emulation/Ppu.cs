using System;
using System.Diagnostics;

using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;

public sealed class Ppu(
    InterruptLines interrupts,
    NesVideoStandard videoStandard = NesVideoStandard.Ntsc,
    NesTiming? timing = null) : IBusMaster, IBusDevice
{
    internal const int FrameWidth = 256;
    internal const int FrameHeight = 240;
    internal const int FrameBufferSize = FrameWidth * FrameHeight * 4;

    private readonly byte[] _oam = new byte[256];
    private readonly byte[] _renderFrame = new byte[FrameBufferSize];
    private readonly byte[] _publishedFrame = new byte[FrameBufferSize];
    private readonly object _frameLock = new();
    private readonly NesTiming _timing = timing ?? (videoStandard == NesVideoStandard.Pal ? new PalTiming() : new NtscTiming());
    private readonly SpriteRenderData[] _sprites = new SpriteRenderData[8];

    private IBus? _bus;
    private PpuCtrl _ppuCtrl;
    private PpuMask _ppuMask;
    private PpuStatus _ppuStatus;
    private byte _oamAddress;
    private byte _ioLatch;
    private byte _dataBuffer;

    // Loopy scrolling registers: current address (v), temporary address (t), fine X (x), write latch (w).
    private ushort _vramAddress;
    private ushort _tempVramAddress;
    private byte _fineXScroll;
    private bool _writeToggle;

    private ushort _backgroundPatternLowShift;
    private ushort _backgroundPatternHighShift;
    private ushort _backgroundAttributeLowShift;
    private ushort _backgroundAttributeHighShift;
    private byte _nextTileId;
    private byte _nextTileAttribute;
    private byte _nextTileLow;
    private byte _nextTileHigh;

    private int _spriteCount;
    private int _scanline;
    private int _cycle;
    private bool _oddFrame;
    private bool _suppressVblank;
    private bool _hasPublishedFrame;

    internal event Action<ulong>? FrameCompleted;

    public void ConnectBus(IBus bus) => _bus = bus;

    public void Reset()
    {
        if (_bus is PpuBus ppuBus) ppuBus.ResetCycle();
        Array.Clear(_oam, 0, _oam.Length);
        Array.Clear(_renderFrame, 0, _renderFrame.Length);
        Array.Clear(_publishedFrame, 0, _publishedFrame.Length);
        for (var i = 3; i < FrameBufferSize; i += 4)
        {
            _renderFrame[i] = 0xFF;
            _publishedFrame[i] = 0xFF;
        }

        _ppuCtrl.Value = 0;
        _ppuMask.Value = 0;
        _ppuStatus.Value = 0;
        _oamAddress = 0;
        _ioLatch = 0;
        _dataBuffer = 0;
        _vramAddress = 0;
        _tempVramAddress = 0;
        _fineXScroll = 0;
        _writeToggle = false;
        _backgroundPatternLowShift = 0;
        _backgroundPatternHighShift = 0;
        _backgroundAttributeLowShift = 0;
        _backgroundAttributeHighShift = 0;
        _nextTileId = 0;
        _nextTileAttribute = 0;
        _nextTileLow = 0;
        _nextTileHigh = 0;
        _spriteCount = 0;
        _scanline = 0;
        _cycle = 0;
        _oddFrame = false;
        _suppressVblank = false;
        FrameNumber = 0;
        _hasPublishedFrame = false;
        interrupts.Nmi = false;
        interrupts.DelayNmiOneInstruction = false;
    }

    public byte Read(ushort address)
    {
        var register = (ushort)(0x2000 | (address & 0x0007));
        return register switch
        {
            0x2002 => ReadStatus(),
            0x2004 => ReadOamData(),
            0x2007 => ReadData(),
            _ => _ioLatch
        };
    }

    public void Write(ushort address, byte value)
    {
        var register = (ushort)(0x2000 | (address & 0x0007));
        _ioLatch = value;
        switch (register)
        {
            case 0x2000:
                var wasNmiEnabled = _ppuCtrl.VBlankNmiEnable;
                _ppuCtrl.Value = value;
                _tempVramAddress = (ushort)((_tempVramAddress & 0xF3FF) | ((value & 0x03) << 10));
                if (!wasNmiEnabled && _ppuCtrl.VBlankNmiEnable && _ppuStatus.VBlank)
                {
                    interrupts.Nmi = true;
                    // CPU writes are applied at the beginning of the instruction in this functional core.
                    // A physical STA changes PPUCTRL on its final cycle, after the interrupt poll, so the
                    // instruction following the STA completes before this immediate NMI is recognized.
                    interrupts.DelayNmiOneInstruction = true;
                }
                if (!_ppuCtrl.VBlankNmiEnable) interrupts.Nmi = false;
                break;
            case 0x2001:
                _ppuMask.Value = value;
                break;
            case 0x2003:
                _oamAddress = value;
                break;
            case 0x2004:
                _oam[_oamAddress++] = value;
                break;
            case 0x2005:
                WriteScroll(value);
                break;
            case 0x2006:
                WriteAddress(value);
                break;
            case 0x2007:
                WriteData(value);
                break;
        }
    }

    public void DmaTransfer(ReadOnlySpan<byte> data)
    {
        for (var i = 0; i < data.Length; i++) _oam[(byte)(_oamAddress + i)] = data[i];
    }

    public void Clock()
    {
        // Timing and fetch phases: https://www.nesdev.org/wiki/PPU_rendering
        Debug.Assert(_bus != null, "PPU bus is not connected.");
        if (_bus is PpuBus ppuBus) ppuBus.AdvanceCycle();
        var preRenderScanline = _timing.ScanlinesPerFrame - 1;
        var renderingEnabled = IsRenderingEnabled;

        if (_scanline == preRenderScanline && _cycle == 1)
        {
            _ppuStatus.VBlank = false;
            _ppuStatus.Sprite0Hit = false;
            _ppuStatus.SpriteOverflow = false;
            interrupts.Nmi = false;
            _suppressVblank = false;
        }

        if (_scanline == 241 && _cycle == 1)
        {
            if (!_suppressVblank)
            {
                _ppuStatus.VBlank = true;
                if (_ppuCtrl.VBlankNmiEnable) interrupts.Nmi = true;
            }
            _suppressVblank = false;
        }

        if ((_scanline < FrameHeight || _scanline == preRenderScanline) && renderingEnabled)
        {
            ClockBackgroundPipeline(preRenderScanline);
            if (_cycle is >= 257 and <= 320) ClockSpriteFetches();
        }

        if (_scanline < FrameHeight && _cycle is >= 1 and <= 256) RenderPixel(_cycle - 1, _scanline);

        var completedFrame = _scanline == 239 && _cycle == _timing.DotsPerScanline - 1 ? PublishFrame() : (ulong?)null;
        AdvanceTiming(preRenderScanline, renderingEnabled);
        if (completedFrame.HasValue) FrameCompleted?.Invoke(completedFrame.Value);
    }

    private void AdvanceTiming(int preRenderScanline, bool renderingEnabled)
    {
        // NTSC omits the final pre-render dot on odd frames while rendering.
        if (videoStandard == NesVideoStandard.Ntsc && _oddFrame && renderingEnabled &&
            _scanline == preRenderScanline && _cycle == _timing.DotsPerScanline - 2)
        {
            _cycle = 0;
            _scanline = 0;
            _oddFrame = false;
            return;
        }

        _cycle++;
        if (_cycle < _timing.DotsPerScanline) return;
        _cycle = 0;
        _scanline++;
        if (_scanline < _timing.ScanlinesPerFrame) return;
        _scanline = 0;
        _oddFrame = !_oddFrame;
    }

    internal bool TryCopyFrame(Span<byte> destination, out ulong frameNumber)
    {
        lock (_frameLock)
        {
            frameNumber = FrameNumber;
            if (!_hasPublishedFrame) return false;
            _publishedFrame.AsSpan().CopyTo(destination);
            return true;
        }
    }

    internal PpuDebugState CaptureDebugState() => new(
        _ppuCtrl.Value, _ppuMask.Value, _ppuStatus.Value, _oamAddress,
        _vramAddress, _tempVramAddress, _fineXScroll, _writeToggle, _dataBuffer,
        _scanline, _cycle, FrameNumber, _oddFrame, _spriteCount);
    internal ulong FrameNumber { get; private set; }

    internal byte PeekRegister(ushort address)
    {
        var register = (ushort)(0x2000 | (address & 0x0007));
        return register switch
        {
            0x2002 => (byte)((_ppuStatus.Value & 0xE0) | (_ioLatch & 0x1F)),
            0x2004 => _oam[_oamAddress],
            0x2007 => _bus is PpuBus ppuBus
                ? ppuBus.Peek((ushort)(_vramAddress & 0x3FFF))
                : _bus!.Read((ushort)(_vramAddress & 0x3FFF)),
            _ => _ioLatch
        };
    }

    internal void CopyOam(int offset, Span<byte> destination) =>
        _oam.AsSpan(offset, destination.Length).CopyTo(destination);

    internal void WriteOam(int offset, ReadOnlySpan<byte> source) =>
        source.CopyTo(_oam.AsSpan(offset, source.Length));

    private bool IsRenderingEnabled => _ppuMask.ShowBackground || _ppuMask.ShowSprites;

    private void ClockBackgroundPipeline(int preRenderScanline)
    {
        if ((_cycle is >= 2 and <= 257) || (_cycle is >= 321 and <= 337))
        {
            ShiftBackgroundRegisters();
            switch ((_cycle - 1) & 0x07)
            {
                case 0:
                    LoadBackgroundRegisters();
                    _nextTileId = _bus!.Read((ushort)(0x2000 | (_vramAddress & 0x0FFF)));
                    break;
                case 2:
                    var attribute = _bus!.Read((ushort)(0x23C0 | (_vramAddress & 0x0C00) |
                        ((_vramAddress >> 4) & 0x38) | ((_vramAddress >> 2) & 0x07)));
                    var shift = (int)(((_vramAddress >> 4) & 0x04) | (_vramAddress & 0x02));
                    _nextTileAttribute = (byte)((attribute >> shift) & 0x03);
                    break;
                case 4:
                    _nextTileLow = _bus!.Read(GetBackgroundPatternAddress(0));
                    break;
                case 6:
                    _nextTileHigh = _bus!.Read(GetBackgroundPatternAddress(8));
                    break;
                case 7:
                    IncrementHorizontal();
                    break;
            }
        }

        if (_cycle == 256) IncrementVertical();
        if (_cycle == 257)
        {
            LoadBackgroundRegisters();
            CopyHorizontalBits();
            EvaluateSpritesForNextScanline();
        }
        if (_scanline == preRenderScanline && _cycle is >= 280 and <= 304) CopyVerticalBits();
        if (_cycle == _timing.DotsPerScanline - 3 || _cycle == _timing.DotsPerScanline - 1)
            _nextTileId = _bus!.Read((ushort)(0x2000 | (_vramAddress & 0x0FFF)));
    }

    private ushort GetBackgroundPatternAddress(int planeOffset)
    {
        var table = _ppuCtrl.BackgroundPatternTableAddress ? 0x1000 : 0;
        var fineY = (_vramAddress >> 12) & 0x07;
        return (ushort)(table + (_nextTileId * 16) + fineY + planeOffset);
    }

    private void LoadBackgroundRegisters()
    {
        _backgroundPatternLowShift = (ushort)((_backgroundPatternLowShift & 0xFF00) | _nextTileLow);
        _backgroundPatternHighShift = (ushort)((_backgroundPatternHighShift & 0xFF00) | _nextTileHigh);
        _backgroundAttributeLowShift = (ushort)((_backgroundAttributeLowShift & 0xFF00) |
            ((_nextTileAttribute & 0x01) != 0 ? 0xFF : 0x00));
        _backgroundAttributeHighShift = (ushort)((_backgroundAttributeHighShift & 0xFF00) |
            ((_nextTileAttribute & 0x02) != 0 ? 0xFF : 0x00));
    }

    private void ShiftBackgroundRegisters()
    {
        _backgroundPatternLowShift <<= 1;
        _backgroundPatternHighShift <<= 1;
        _backgroundAttributeLowShift <<= 1;
        _backgroundAttributeHighShift <<= 1;
    }

    private void RenderPixel(int x, int y)
    {
        if (videoStandard == NesVideoStandard.Pal && (y == 0 || x < 2 || x >= FrameWidth - 2))
        {
            var borderOffset = ((y * FrameWidth) + x) * 4;
            _renderFrame[borderOffset] = 0;
            _renderFrame[borderOffset + 1] = 0;
            _renderFrame[borderOffset + 2] = 0;
            _renderFrame[borderOffset + 3] = 0xFF;
            return;
        }

        var backgroundPixel = 0;
        var backgroundPalette = 0;
        if (_ppuMask.ShowBackground && (x >= 8 || _ppuMask.ShowBackgroundLeft))
        {
            var mux = (ushort)(0x8000 >> _fineXScroll);
            backgroundPixel = ((_backgroundPatternHighShift & mux) != 0 ? 2 : 0) |
                              ((_backgroundPatternLowShift & mux) != 0 ? 1 : 0);
            backgroundPalette = ((_backgroundAttributeHighShift & mux) != 0 ? 2 : 0) |
                                ((_backgroundAttributeLowShift & mux) != 0 ? 1 : 0);
        }

        var spritePixel = 0;
        var spritePalette = 0;
        var spriteBehindBackground = false;
        var spriteZero = false;
        if (_ppuMask.ShowSprites && (x >= 8 || _ppuMask.ShowSpritesLeft))
        {
            for (var i = 0; i < _spriteCount; i++)
            {
                ref readonly var sprite = ref _sprites[i];
                var offset = x - sprite.X;
                if ((uint)offset >= 8) continue;
                var bit = (sprite.Attributes & 0x40) != 0 ? offset : 7 - offset;
                var pixel = (((sprite.PatternHigh >> bit) & 1) << 1) | ((sprite.PatternLow >> bit) & 1);
                if (pixel == 0) continue;
                spritePixel = pixel;
                spritePalette = sprite.Attributes & 0x03;
                spriteBehindBackground = (sprite.Attributes & 0x20) != 0;
                spriteZero = sprite.IsSpriteZero;
                break;
            }
        }

        int paletteAddress;
        if (backgroundPixel == 0 && spritePixel == 0)
        {
            paletteAddress = !IsRenderingEnabled && (_vramAddress & 0x3F00) == 0x3F00
                ? _vramAddress & 0x3FFF
                : 0x3F00;
        }
        else if (backgroundPixel == 0)
        {
            paletteAddress = 0x3F10 + (spritePalette * 4) + spritePixel;
        }
        else if (spritePixel == 0)
        {
            paletteAddress = 0x3F00 + (backgroundPalette * 4) + backgroundPixel;
        }
        else
        {
            if (spriteZero && x < 255) _ppuStatus.Sprite0Hit = true;
            paletteAddress = spriteBehindBackground
                ? 0x3F00 + (backgroundPalette * 4) + backgroundPixel
                : 0x3F10 + (spritePalette * 4) + spritePixel;
        }

        // Palette RAM is internal to the PPU. Pixel composition must not expose a synthetic
        // palette read on the external PPU address bus, where mappers observe A12 transitions.
        var color = (_bus is PpuBus ppuBus
            ? ppuBus.Peek((ushort)paletteAddress)
            : _bus!.Read((ushort)paletteAddress)) & 0x3F;
        if (_ppuMask.Grayscale) color &= 0x30;
        WriteRgbaPixel(x, y, color);
    }

    private void WriteRgbaPixel(int x, int y, int color)
    {
        NesPalette.GetColor(videoStandard, color, _ppuMask.Value, out var red, out var green, out var blue);
        var offset = ((y * FrameWidth) + x) * 4;
        _renderFrame[offset] = red;
        _renderFrame[offset + 1] = green;
        _renderFrame[offset + 2] = blue;
        _renderFrame[offset + 3] = 0xFF;
    }

    private void EvaluateSpritesForNextScanline()
    {
        _spriteCount = 0;
        var nextScanline = _scanline == _timing.ScanlinesPerFrame - 1 ? 0 : _scanline + 1;
        if (nextScanline >= FrameHeight) return;
        var height = _ppuCtrl.SpriteSize ? 16 : 8;
        var inRangeCount = 0;
        for (var spriteIndex = 0; spriteIndex < 64; spriteIndex++)
        {
            var address = spriteIndex * 4;
            var top = _oam[address] + 1;
            var row = nextScanline - top;
            if (row < 0 || row >= height) continue;
            inRangeCount++;
            if (_spriteCount >= 8) continue;

            var tile = _oam[address + 1];
            var attributes = _oam[address + 2];
            if ((attributes & 0x80) != 0) row = height - 1 - row;
            _sprites[_spriteCount++] = new SpriteRenderData(
                _oam[address + 3], attributes, GetSpritePatternAddress(tile, row), spriteIndex == 0);
        }
        if (inRangeCount > 8) _ppuStatus.SpriteOverflow = true;
    }

    private void ClockSpriteFetches()
    {
        var slot = (_cycle - 257) / 8;
        var phase = (_cycle - 257) & 0x07;
        if (slot >= _spriteCount)
        {
            if (phase is 4 or 6)
            {
                var dummyPatternAddress = GetSpritePatternAddress(0xFF, 0);
                _ = _bus!.Read((ushort)(dummyPatternAddress + (phase == 6 ? 8 : 0)));
            }
            return;
        }

        if (phase == 4) _sprites[slot].PatternLow = _bus!.Read(_sprites[slot].PatternAddress);
        if (phase == 6) _sprites[slot].PatternHigh = _bus!.Read((ushort)(_sprites[slot].PatternAddress + 8));
    }

    private ushort GetSpritePatternAddress(byte tile, int row)
    {
        if (!_ppuCtrl.SpriteSize)
        {
            var table = _ppuCtrl.SpritePatternTableAddress ? 0x1000 : 0;
            return (ushort)(table + (tile * 16) + row);
        }

        var tableAddress = (tile & 0x01) * 0x1000;
        var tileIndex = tile & 0xFE;
        if (row >= 8)
        {
            tileIndex++;
            row -= 8;
        }
        return (ushort)(tableAddress + (tileIndex * 16) + row);
    }

    private void IncrementHorizontal()
    {
        if ((_vramAddress & 0x001F) == 31)
        {
            _vramAddress &= 0xFFE0;
            _vramAddress ^= 0x0400;
        }
        else _vramAddress++;
    }

    private void IncrementVertical()
    {
        if ((_vramAddress & 0x7000) != 0x7000)
        {
            _vramAddress += 0x1000;
            return;
        }

        _vramAddress &= 0x8FFF;
        var coarseY = (_vramAddress & 0x03E0) >> 5;
        if (coarseY == 29)
        {
            coarseY = 0;
            _vramAddress ^= 0x0800;
        }
        else if (coarseY == 31) coarseY = 0;
        else coarseY++;
        _vramAddress = (ushort)((_vramAddress & 0xFC1F) | (coarseY << 5));
    }

    private void CopyHorizontalBits() =>
        _vramAddress = (ushort)((_vramAddress & 0xFBE0) | (_tempVramAddress & 0x041F));

    private void CopyVerticalBits() =>
        _vramAddress = (ushort)((_vramAddress & 0x841F) | (_tempVramAddress & 0x7BE0));

    private byte ReadStatus()
    {
        // VBlank race behavior: https://www.nesdev.org/wiki/PPU_frame_timing#VBL_Flag_Timing
        var value = (byte)((_ppuStatus.Value & 0xE0) | (_ioLatch & 0x1F));
        if (_scanline == 241 && _cycle <= 1) _suppressVblank = true;
        _ppuStatus.VBlank = false;
        interrupts.Nmi = false;
        interrupts.DelayNmiOneInstruction = false;
        _writeToggle = false;
        _ioLatch = value;
        return value;
    }

    private byte ReadOamData()
    {
        _ioLatch = _oam[_oamAddress];
        return _ioLatch;
    }

    private void WriteScroll(byte value)
    {
        if (!_writeToggle)
        {
            _fineXScroll = (byte)(value & 0x07);
            _tempVramAddress = (ushort)((_tempVramAddress & 0xFFE0) | (value >> 3));
        }
        else
        {
            _tempVramAddress = (ushort)((_tempVramAddress & 0x8FFF) | ((value & 0x07) << 12));
            _tempVramAddress = (ushort)((_tempVramAddress & 0xFC1F) | ((value & 0xF8) << 2));
        }
        _writeToggle = !_writeToggle;
    }

    private void WriteAddress(byte value)
    {
        if (!_writeToggle)
        {
            _tempVramAddress = (ushort)((_tempVramAddress & 0x00FF) | ((value & 0x3F) << 8));
        }
        else
        {
            _tempVramAddress = (ushort)((_tempVramAddress & 0xFF00) | value);
            _vramAddress = _tempVramAddress;
            if (_bus is PpuBus ppuBus) ppuBus.NotifyPpuAddress((ushort)(_vramAddress & 0x3FFF));
        }
        _writeToggle = !_writeToggle;
    }

    private byte ReadData()
    {
        var address = (ushort)(_vramAddress & 0x3FFF);
        var busValue = _bus!.Read(address);
        byte value;
        if (address >= 0x3F00)
        {
            value = busValue;
            _dataBuffer = _bus.Read((ushort)(address - 0x1000));
        }
        else
        {
            value = _dataBuffer;
            _dataBuffer = busValue;
        }
        IncrementDataAddress();
        _ioLatch = value;
        return value;
    }

    private void WriteData(byte value)
    {
        _bus!.Write((ushort)(_vramAddress & 0x3FFF), value);
        IncrementDataAddress();
    }

    private void IncrementDataAddress()
    {
        _vramAddress = (ushort)((_vramAddress + (_ppuCtrl.VramIncrement ? 32 : 1)) & 0x7FFF);
        if (_bus is PpuBus ppuBus) ppuBus.NotifyPpuAddress((ushort)(_vramAddress & 0x3FFF));
    }

    private ulong PublishFrame()
    {
        lock (_frameLock)
        {
            Buffer.BlockCopy(_renderFrame, 0, _publishedFrame, 0, FrameBufferSize);
            _hasPublishedFrame = true;
            return ++FrameNumber;
        }
    }

    private struct SpriteRenderData(
        byte x, byte attributes, ushort patternAddress, bool isSpriteZero)
    {
        public byte X { get; } = x;
        public byte Attributes { get; } = attributes;
        public ushort PatternAddress { get; } = patternAddress;
        public byte PatternLow { get; set; }
        public byte PatternHigh { get; set; }
        public bool IsSpriteZero { get; } = isSpriteZero;
    }
}
