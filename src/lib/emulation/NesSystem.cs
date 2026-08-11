using System;
using System.Diagnostics;
using System.Threading;

using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes;

/// <summary>
/// Emulated physical NES console coordinating CPU, PPU, APU, and buses.
/// </summary>
public sealed class NesSystem
{
    public const int FrameWidth = Ppu.FrameWidth;
    public const int FrameHeight = Ppu.FrameHeight;
    public const int BytesPerPixel = 4;
    public const int FrameBufferSize = Ppu.FrameBufferSize;
    public const int AudioSampleRate = ApuMixer.SampleRate;

    private readonly NesSystemUnits _u;
    private int _cpuClockAccumulator;
    private ulong? _pendingFrameNumber;
    private bool _isPaused;
    private ulong _cpuClockCounter;

    public NesSystem(NesVideoStandard videoStandard = NesVideoStandard.Ntsc)
        : this(NesHardwareProfile.ForVideoStandard(videoStandard)) { }

    public NesSystem(NesHardwareProfile profile)
    {
        HardwareProfile = profile ?? throw new ArgumentNullException(nameof(profile));
        VideoStandard = profile.VideoStandard;
        Timing = profile.Timing;
        _u = new NesSystemUnits(this, VideoStandard, Timing, frame => _pendingFrameNumber = frame);
        Reset();
    }

    public NesVideoStandard VideoStandard { get; }
    public NesHardwareProfile HardwareProfile { get; }
    public NesTiming Timing { get; }
    public INesDebugger Debugger => _u.Debugger;
    public event EventHandler<FrameReadyEventArgs>? FrameReady;

    public NesAudioFilterMode AudioFilterMode
    {
        get => NesAudioBridge.GetFilterMode(this, _u.Apu);
        set => NesAudioBridge.SetFilterMode(this, _u.Apu, value);
    }

    public int BufferedAudioSampleCount => _u.Apu.BufferedSampleCount;
    public int ReadAudioSamples(Span<float> dest) => NesAudioBridge.ReadSamples(_u.Apu, dest);
    public NesAudioReadResult ReadAudio(Span<float> dest) => NesAudioBridge.ReadAudio(_u.Apu, dest);
    public void DiscardAudioSamples() => NesAudioBridge.DiscardSamples(_u.Apu);

    public void LoadRom(byte[] romData) => NesResetController.LoadRom(this, _u, romData);
    public void Reset() => NesResetController.Reset(this, _u);
    public void SetControllerState(int port, NesControllerButton keys) { lock (SyncRoot) _u.CpuBus.SetControllerState(port, (byte)keys); }
    public bool TryCopyFrame(Span<byte> dest, out ulong frame) => NesVideoOutput.TryCopyFrame(_u.Ppu, dest, out frame);
    public NesVideoFrame? TryAcquireFrame() => NesVideoOutput.TryAcquireFrame(_u.Ppu, VideoStandard);

    public void Clock() => NesExecutionEngine.Clock(this, _u, frame => FrameReady?.Invoke(this, frame));
    public NesRunResult RunForPpuDots(int count) => NesExecutionEngine.RunForPpuDots(this, count);
    public NesRunResult RunUntilFrame() => NesExecutionEngine.RunUntilFrame(this);

    internal NesRunResult RunBatchInternal(int dots, bool frame) => NesBatchRunner.RunBatch(
        this, _u.Ppu, _u.Apu, _u.Debugger, Timing.CpuDivisor, Timing.PpuDivisor,
        ref _cpuClockAccumulator, ClockCpuCore, dots, frame, f => FrameReady?.Invoke(this, f));

    internal void ClockCoreInternal() =>
        NesClockDriver.ClockCore(_u.Ppu, ref _cpuClockAccumulator, Timing.PpuDivisor, Timing.CpuDivisor, ClockCpuCore);

    private void ClockCpuCore() => NesClockDriver.ClockCpuCore(
        ref _cpuClockAccumulator, Timing.CpuDivisor, _u.Debugger, _u.Apu, _u.Cpu,
        _u.CpuBus, _u.CartridgeSlot, ref _cpuClockCounter);

    internal bool ExecuteDotLocked(bool checkBreakpoints, out FrameReadyEventArgs? frameReady) =>
        NesClockDriver.ExecuteDotLocked(this, _u.Debugger, checkBreakpoints, out frameReady);

    internal bool WillClockCpuLocked => _cpuClockAccumulator + Timing.PpuDivisor >= Timing.CpuDivisor;

    internal void ResetTimingCountersInternal()
    {
        _cpuClockCounter = 0;
        _cpuClockAccumulator = 0;
        _pendingFrameNumber = null;
    }

    internal FrameReadyEventArgs? TakePendingFrameInternal()
    {
        if (!_pendingFrameNumber.HasValue) return null;
        var result = new FrameReadyEventArgs(_pendingFrameNumber.Value, VideoStandard);
        _pendingFrameNumber = null;
        return result;
    }

    internal void RaiseFrameReady(FrameReadyEventArgs frame) => FrameReady?.Invoke(this, frame);

    internal object SyncRoot { get; } = new();
    internal bool IsPausedLocked
    {
        get { AssertLockHeld(); return _isPaused; }
        set { AssertLockHeld(); _isPaused = value; }
    }
    internal void SetPausedLocked(bool value) => IsPausedLocked = value;

    internal ulong CpuClockCounter => _cpuClockCounter;
    internal int SchedulerClockAccumulator => _cpuClockAccumulator;
    internal InterruptLines Interrupts => _u.Interrupts;
    internal Cartridge? Cartridge => _u.CartridgeSlot.Cartridge;
    internal Cpu.Cpu Cpu => _u.Cpu;
    internal Ppu Ppu => _u.Ppu;
    internal Apu Apu => _u.Apu;
    internal CpuBus CpuBus => _u.CpuBus;
    internal PpuBus PpuBus => _u.PpuBus;
    internal ulong CurrentFrameNumber => _u.Ppu.FrameNumber;

    [Conditional("DEBUG")]
    private void AssertLockHeld() => Debug.Assert(Monitor.IsEntered(SyncRoot));
}
