using Windows.Media.Audio;

namespace EmuSheep;

internal sealed class NesAudioPlayer : IAsyncDisposable
{
    private readonly NesSystem _nes;
    private AudioGraph? _graph;
    private AudioFrameInputNode? _input;
    private AudioDeviceOutputNode? _output;
    private int _started;
    private bool _disposed;
    private bool _muted;

    private NesAudioPlayer(NesSystem nes) => _nes = nes;

    internal static async Task<NesAudioPlayer> CreateAsync(NesSystem nes)
    {
        var player = new NesAudioPlayer(nes);
        await player.InitializeAsync();
        return player;
    }

    internal bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
            _input?.OutgoingGain = value ? 0 : Volume;
        }
    }

    internal double Volume { get; private set; } = 1.0;
    internal bool IsStarted => Volatile.Read(ref _started) != 0;
    internal event Action? AudioSamplesRequested;

    internal void SetVolume(double value)
    {
        Volume = Math.Clamp(value, 0, 1);
        if (_input != null && !_muted)
        {
            _input.OutgoingGain = Volume;
        }
    }

    private async Task InitializeAsync()
    {
        (_graph, _output, _input) = await NesAudioGraphFactory.CreateGraphAsync(
            Input_QuantumStarted,
            Input_AudioFrameCompleted);
    }

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }
        try
        {
            _input!.Start();
        }
        catch
        {
            Volatile.Write(ref _started, 0);
            throw;
        }
    }

    private void Input_QuantumStarted(AudioFrameInputNode sender, FrameInputNodeQuantumStartedEventArgs args)
    {
        if (args.RequiredSamples <= 0 || _disposed)
        {
            return;
        }

        NesAudioFrameProvider.ProcessQuantum(sender, args.RequiredSamples, _nes, AudioSamplesRequested);
    }

    private static void Input_AudioFrameCompleted(
        AudioFrameInputNode sender,
        AudioFrameCompletedEventArgs args) => args.Frame.Dispose();

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }
        _disposed = true;
        if (_input != null)
        {
            _input.QuantumStarted -= Input_QuantumStarted;
            _input.AudioFrameCompleted -= Input_AudioFrameCompleted;
        }
        _graph?.Stop();
        _input?.Dispose();
        _output?.Dispose();
        _graph?.Dispose();
        return ValueTask.CompletedTask;
    }
}
