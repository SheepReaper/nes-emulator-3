using Sheep.Emulation.Nes;

namespace EmuSheep;

internal sealed class NesAudioPlayer : IAsyncDisposable
{
    internal static bool SimulateAvailable { get; set; }
    internal static int BufferedSamplesWhenStarted { get; private set; }
    internal static Task Started => _startedSignal.Task;
    internal static int BufferedSamples => _current?._nes.BufferedAudioSampleCount ?? 0;

    private static TaskCompletionSource _startedSignal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static NesAudioPlayer? _current;

    private readonly NesSystem _nes;
    private bool _started;
    private Action? _audioSamplesRequested;

    private NesAudioPlayer(NesSystem nes)
    {
        _nes = nes;
        _current = this;
    }

    internal static Task<NesAudioPlayer> CreateAsync(NesSystem nes) => SimulateAvailable
        ? Task.FromResult(new NesAudioPlayer(nes))
        : Task.FromException<NesAudioPlayer>(new PlatformNotSupportedException("AudioGraph is not available in unit tests."));

    internal bool IsMuted { get; set; }
    internal bool IsStarted => _started;
    internal event Action? AudioSamplesRequested
    {
        add => _audioSamplesRequested += value;
        remove => _audioSamplesRequested -= value;
    }
    internal void SetVolume(double value) { }
    internal void Start()
    {
        _started = true;
        BufferedSamplesWhenStarted = _nes.BufferedAudioSampleCount;
        _startedSignal.TrySetResult();
    }

    internal static void ConsumeSamples(int count)
    {
        var player = _current ?? throw new InvalidOperationException("No simulated audio player exists.");
        player._nes.ReadAudio(new float[count]);
        player._audioSamplesRequested?.Invoke();
    }

    internal static void ResetSimulation()
    {
        SimulateAvailable = false;
        BufferedSamplesWhenStarted = 0;
        _current = null;
        _startedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
