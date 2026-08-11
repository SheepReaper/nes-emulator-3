using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes.Video;

public sealed class Ppu : IBusMaster, IBusDevice
{
    internal const int FrameWidth = 256;
    internal const int FrameHeight = 240;
    internal const int FrameBufferSize = FrameWidth * FrameHeight * 4;

    private readonly InterruptLines _interrupts;
    private readonly uint[] _colorLookup;
    private readonly bool _isPal;
    private readonly ulong _ioLatchDecayDots;
    private readonly PpuUnits _u;

    private IBus? _bus;
    private PpuBus? _ppuBus;
    private ulong _elapsedPpuDots;

    internal event Action<ulong>? FrameCompleted;

    public Ppu(InterruptLines interrupts, NesVideoStandard videoStandard = NesVideoStandard.Ntsc, NesTiming? timing = null)
    {
        _interrupts = interrupts;
        var nesTiming = timing ?? (videoStandard == NesVideoStandard.Pal ? new PalTiming() : new NtscTiming());
        _colorLookup = NesPalette.GetLookup(videoStandard);
        _isPal = videoStandard == NesVideoStandard.Pal;
        _ioLatchDecayDots = (ulong)nesTiming.MasterClockHz / ((ulong)nesTiming.PpuDivisor * 2);
        _u = new PpuUnits(nesTiming, videoStandard, interrupts);
    }

    public void ConnectBus(IBus bus)
    {
        _bus = bus;
        _ppuBus = bus as PpuBus;
    }

    public void Reset()
    {
        _ppuBus?.ResetCycle();
        _u.Reset();
        _elapsedPpuDots = 0;
    }

    public byte Read(ushort address) => (ushort)(0x2000 | (address & 0x0007)) switch
    {
        0x2002 => PpuRegisterReader.ReadStatus(_u.State, _u.IoLatch, _u.Scroll, _u.Time, _elapsedPpuDots, _ioLatchDecayDots),
        0x2004 => PpuRegisterReader.ReadOamData(_u.Oam, _u.IoLatch, _elapsedPpuDots, _ioLatchDecayDots),
        0x2007 => _u.DataPort.Read(_bus!, _u.Scroll, _u.IoLatch, _elapsedPpuDots, _ioLatchDecayDots, _u.State.Ctrl.VramIncrement, _ppuBus, IsRenderingActive, _u.Mask.Grayscale),
        _ => _u.IoLatch.Read(_elapsedPpuDots)
    };

    public void Write(ushort address, byte value)
    {
        _u.IoLatch.Drive(value, _elapsedPpuDots, _ioLatchDecayDots);
        PpuRegisterDispatcher.Write(
            (ushort)(0x2000 | (address & 0x0007)), value, _u.State, _u.Scroll, _u.Oam, _u.Mask, _ppuBus,
            v => _u.DataPort.Write(_bus!, v, _u.Scroll, _u.State.Ctrl.VramIncrement, _ppuBus, IsRenderingActive));
    }

    private bool IsRenderingActive => _u.Mask.RenderingEnabled && (_u.Time.Scanline < FrameHeight || _u.Time.Scanline == _u.Time.PreRenderScanline);

    public void DmaTransfer(ReadOnlySpan<byte> data) => _u.Oam.DmaTransfer(data);
    internal void DmaWriteByte(byte value) => _u.Oam.DmaWriteByte(value);
    public void Clock() => ClockDots(1);

    internal void ClockDots(int count)
    {
        Debug.Assert(_bus != null, "PPU bus is not connected.");
        while (count-- > 0)
        {
            var completed = PpuDotClockDriver.ClockSingleDot(
                ref _elapsedPpuDots, _u.Time, _u.State, _u.Mask, _u.Scroll, _u.Bg, _u.Sprites, _u.Oam,
                _u.Frame, _ppuBus, _interrupts, _isPal, _colorLookup, ReadBus, PeekPalette);
            if (completed.HasValue) FrameCompleted?.Invoke(completed.Value);
        }
    }

    internal bool TryClockBatch(int count) => PpuBatchClock.TryClockBatch(
        count, _u.Mask.RenderingEnabled, _u.State, _u.Time, _ppuBus, _interrupts,
        ref _elapsedPpuDots, _u.Mask.Grayscale, _u.Mask.PaletteEmphasisOffset, _colorLookup,
        _u.Frame.RenderFrame, _u.Scroll, PeekPalette);

    internal bool TryCopyFrame(Span<byte> destination, out ulong frameNumber) =>
        _u.Frame.TryCopyFrame(destination, out frameNumber);

    internal NesVideoFrame? TryAcquireFrame(NesVideoStandard videoStandard) =>
        _u.Frame.TryAcquireFrame(videoStandard);

    internal PpuDebugState CaptureDebugState() => new(
        _u.State.Ctrl.Value, _u.State.Mask.Value, _u.State.Status.Value, _u.Oam.Address,
        _u.Scroll.VramAddress, _u.Scroll.TempVramAddress, _u.Scroll.FineXScroll, _u.Scroll.WriteToggle, _u.DataPort.DataBuffer,
        _u.Time.Scanline, _u.Time.Cycle, _u.Frame.FrameNumber, _u.Time.OddFrame, _u.Sprites.SpriteCount);

    internal ulong FrameNumber => _u.Frame.FrameNumber;
    internal PpuPhase Phase => _u.Time.Phase;
    internal bool RenderingEnabled => _u.Mask.RenderingEnabled;

    internal byte PeekRegister(ushort address) =>
        PpuRegisterPeek.Peek(address, _u.State, _u.Oam, _u.Scroll, _u.IoLatch, _elapsedPpuDots, _bus, _ppuBus);

    internal void CopyOam(int offset, Span<byte> destination) => _u.Oam.Copy(offset, destination);
    internal void WriteOam(int offset, ReadOnlySpan<byte> source) => _u.Oam.WriteSpan(offset, source);

    private byte PeekPalette(ushort address) => _ppuBus != null ? _ppuBus.PeekPalette(address) : _bus!.Read(address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte ReadBus(ushort address) => _ppuBus != null ? _ppuBus.Read(address) : _bus!.Read(address);
}