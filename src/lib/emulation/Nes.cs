using System;

using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;

public sealed class Nes
{
    public const int FrameWidth = Ppu.FrameWidth;
    public const int FrameHeight = Ppu.FrameHeight;
    public const int BytesPerPixel = 4;
    public const int FrameBufferSize = Ppu.FrameBufferSize;

    private readonly InterruptLines _interrupts = new();
    private readonly CartridgeSlot _cartridgeSlot = new();
    private readonly CartridgeFactory _cartridgeFactory;

    private readonly CpuBus _cpuBus;
    private readonly PpuBus _ppuBus;
    private readonly Cpu _cpu;
    private readonly Ppu _ppu;
    private readonly Apu _apu;
    private readonly object _sync = new();
    private readonly NesDebugger _debugger;

    private ulong _cpuClockCounter;
    private int _cpuClockAccumulator;
    private ulong? _pendingFrameNumber;
    private bool _isPaused;
    private double _requestedSpeedMultiplier = 1.0;

    public Nes(NesVideoStandard videoStandard = NesVideoStandard.Ntsc)
    {
        VideoStandard = videoStandard;
        Timing = videoStandard switch
        {
            NesVideoStandard.Ntsc => new NtscTiming(),
            NesVideoStandard.Pal => new PalTiming(),
            _ => throw new ArgumentOutOfRangeException(nameof(videoStandard))
        };
        _cartridgeFactory = new CartridgeFactory(_interrupts);
        _cpu = new Cpu(_interrupts);
        _ppu = new Ppu(_interrupts, videoStandard, Timing);
        _apu = new Apu(_interrupts, Timing.ApuRegion);

        _ppuBus = new PpuBus(_cartridgeSlot);
        _ppu.ConnectBus(_ppuBus);

        _cpuBus = new CpuBus(_cpu, _ppu, _apu, _cartridgeSlot);
        _cpu.ConnectBus(_cpuBus);
        _ppu.FrameCompleted += OnFrameCompleted;
        _debugger = new NesDebugger(this);
        Debugger = _debugger;
        Reset();
    }

    public NesVideoStandard VideoStandard { get; }
    public NesTiming Timing { get; }
    public INesDebugger Debugger { get; }
    public event EventHandler<FrameReadyEventArgs>? FrameReady;

    public double RequestedSpeedMultiplier
    {
        get { lock (_sync) return _requestedSpeedMultiplier; }
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "The speed multiplier must be positive and finite.");
            lock (_sync) _requestedSpeedMultiplier = value;
        }
    }

    public void LoadRom(byte[] romData)
    {
        var cartridge = _cartridgeFactory.Create(romData);
        lock (_sync)
        {
            _cartridgeSlot.Insert(cartridge);
            ResetUnsafe();
        }
    }

    public void Reset()
    {
        lock (_sync) ResetUnsafe();
    }

    private void ResetUnsafe()
    {
        _debugger.ResetTransientStateUnsafe();
        try
        {
            _cartridgeSlot.Cartridge?.Reset();
            _cpu.Reset();
            _ppu.Reset();
            _apu.Reset();
        }
        finally { _debugger.FinishResetUnsafe(); }
        _cpuClockCounter = 0;
        _cpuClockAccumulator = 0;
        _pendingFrameNumber = null;
        _isPaused = false;
    }

    public bool TryCopyFrame(Span<byte> destination, out ulong frameNumber)
    {
        if (destination.Length < FrameBufferSize)
        {
            throw new ArgumentException($"The destination must contain at least {FrameBufferSize} bytes.", nameof(destination));
        }
        lock (_sync) return _ppu.TryCopyFrame(destination, out frameNumber);
    }

    public void Clock()
    {
        FrameReadyEventArgs? frameReady;
        lock (_sync)
        {
            if (_isPaused) return;
            _ = ExecuteDotUnsafe(true, out frameReady);
        }
        if (frameReady != null) FrameReady?.Invoke(this, frameReady);
        _debugger.DispatchPendingEvents();
    }

    public NesRunResult RunForPpuDots(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return RunBatch(count, false);
    }

    public NesRunResult RunUntilFrame() => RunBatch(int.MaxValue, true);

    private NesRunResult RunBatch(int maximumDots, bool untilFrame)
    {
        var framesToRaise = new System.Collections.Generic.List<FrameReadyEventArgs>();
        NesRunResult result;
        lock (_sync)
        {
            if (_isPaused) return new NesRunResult(0, 0, 0, NesRunStopReason.Paused);
            var startingCpu = _cpuClockCounter;
            var startingFrame = _ppu.FrameNumber;
            var dots = 0;
            var stopReason = NesRunStopReason.Completed;
            while (dots < maximumDots)
            {
                if (!ExecuteDotUnsafe(true, out var frame))
                {
                    stopReason = NesRunStopReason.Breakpoint;
                    break;
                }
                dots++;
                if (frame != null) framesToRaise.Add(frame);
                if (_isPaused)
                {
                    stopReason = NesRunStopReason.Breakpoint;
                    break;
                }
                if (untilFrame && _ppu.FrameNumber != startingFrame) break;
            }
            result = new NesRunResult(dots, _cpuClockCounter - startingCpu,
                _ppu.FrameNumber - startingFrame, stopReason);
        }
        foreach (var frame in framesToRaise) FrameReady?.Invoke(this, frame);
        _debugger.DispatchPendingEvents();
        return result;
    }

    private void ClockCore()
    {
        _ppu.Clock();

        _cpuClockAccumulator += Timing.PpuDivisor;
        if (_cpuClockAccumulator >= Timing.CpuDivisor)
        {
            _cpuClockAccumulator -= Timing.CpuDivisor;
            _cpu.Clock(_cpuClockCounter++);
        }

    }

    internal bool ExecuteDotUnsafe(bool checkBreakpoints, out FrameReadyEventArgs? frameReady)
    {
        frameReady = null;
        if (checkBreakpoints && _debugger.TryBreakBeforeCpuClockUnsafe()) return false;
        ClockCore();
        frameReady = TakePendingFrame();
        if (checkBreakpoints) _debugger.CompleteDotUnsafe();
        return true;
    }

    internal bool WillClockCpuUnsafe
    {
        get
        {
            return _cpuClockAccumulator + Timing.PpuDivisor >= Timing.CpuDivisor;
        }
    }

    internal void RaiseFrameReady(FrameReadyEventArgs frame) => FrameReady?.Invoke(this, frame);

    private void OnFrameCompleted(ulong frameNumber) => _pendingFrameNumber = frameNumber;

    private FrameReadyEventArgs? TakePendingFrame()
    {
        if (!_pendingFrameNumber.HasValue) return null;
        var result = new FrameReadyEventArgs(_pendingFrameNumber.Value, VideoStandard);
        _pendingFrameNumber = null;
        return result;
    }

    internal object SyncRoot => _sync;
    internal bool IsPausedUnsafe => _isPaused;
    internal void SetPausedUnsafe(bool value) => _isPaused = value;
    internal Cpu Cpu => _cpu;
    internal Ppu Ppu => _ppu;
    internal Apu Apu => _apu;
    internal CpuBus CpuBus => _cpuBus;
    internal PpuBus PpuBus => _ppuBus;
    internal Cartridge? Cartridge => _cartridgeSlot.Cartridge;
    internal ulong CpuClockCounter => _cpuClockCounter;
    internal ulong CurrentFrameNumber => _ppu.FrameNumber;
    internal double RequestedSpeedMultiplierUnsafe => _requestedSpeedMultiplier;
}
