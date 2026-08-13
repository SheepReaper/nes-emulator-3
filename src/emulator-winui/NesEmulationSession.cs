using Sheep.Emulation.Nes;

namespace EmuSheep;

internal sealed class NesEmulationSession : IAsyncDisposable
{
    private readonly NesSystem _nes;
    private readonly NesSessionAudioState _audio;
    private readonly NesAudioDemandCoordinator _audioDemand = new();
    private readonly NesSessionRunner _runner = new();
    private readonly NesSessionFrameBuffer _frames = new();
    private readonly CancellationTokenSource _cancellation = new();

    private double _speedMultiplier = 1;

    public NesEmulationSession(byte[] romData)
    {
        ArgumentNullException.ThrowIfNull(romData);
        _nes = new NesSystem(NesVideoStandard.Ntsc);
        _nes.LoadRom(romData);
        _audio = new NesSessionAudioState(_nes);
    }

    public event EventHandler? FrameAvailable;
    public event EventHandler<FrameRateAvailableEventArgs>? FrameRateAvailable;
    public event EventHandler<EmulationFaultedEventArgs>? Faulted;
    public event EventHandler<EmulationFaultedEventArgs>? AudioUnavailable;

    public bool HasAudio => _audio.HasAudio;
    public bool IsRunning => _runner.IsRunning;

    public Task InitializeAudioAsync() => _audio.InitializeAsync(
        _audioDemand.SignalDemand,
        ex => AudioUnavailable?.Invoke(this, new EmulationFaultedEventArgs(ex)));

    public void SetMuted(bool muted) => _audio.SetMuted(muted);
    public void SetVolume(double volume) => _audio.SetVolume(volume);
    public void SetFilterMode(NesAudioFilterMode mode) => _nes.AudioFilterMode = mode;
    public void SetControllerState(NesControllerButton buttons) => _nes.SetControllerState(0, buttons);

    public void SetSpeedMultiplier(double speedMultiplier)
    {
        if (!double.IsFinite(speedMultiplier) || speedMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier), "The speed multiplier must be positive and finite.");
        }
        Volatile.Write(ref _speedMultiplier, speedMultiplier);
    }

    public void Start()
    {
        var context = new NesSessionRunContext(
            _audio.GetPlayer,
            () => Volatile.Read(ref _speedMultiplier),
            _audioDemand.WaitForDemandAsync,
            _frames.PublishFrame,
            () => FrameAvailable?.Invoke(this, EventArgs.Empty),
            fps => FrameRateAvailable?.Invoke(this, new FrameRateAvailableEventArgs(fps)),
            ex => Faulted?.Invoke(this, new EmulationFaultedEventArgs(ex)));

        _runner.Start(ct => NesEmulationLoop.RunAsync(_nes, context, ct), _cancellation.Token);
    }

    public bool TryCopyLatestFrame(Span<byte> destination, out ulong frameNumber) =>
        _frames.TryCopyLatestFrame(destination, out frameNumber);

    public Task StopAsync() => _runner.StopAsync(_cancellation);

    public async ValueTask DisposeAsync()
    {
        _runner.MarkDisposed();
        await StopAsync().ConfigureAwait(false);
        await _audio.DisposeAsync().ConfigureAwait(false);
        _frames.Clear();
        _audioDemand.Dispose();
        _cancellation.Dispose();
    }
}
