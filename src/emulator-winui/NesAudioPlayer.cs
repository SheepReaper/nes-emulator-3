using System.Runtime.InteropServices;
using SR.Emulation.Nes;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;

namespace EmuSheep;

internal sealed class NesAudioPlayer : IAsyncDisposable
{
    private const int Channels = 2;
    private readonly Nes _nes;
    private readonly float[] _monoScratch = new float[4096];
    private AudioGraph? _graph;
    private AudioFrameInputNode? _input;
    private AudioDeviceOutputNode? _output;
    private bool _disposed;
    private bool _muted;

    private NesAudioPlayer(Nes nes) => _nes = nes;

    internal static async Task<NesAudioPlayer> CreateAsync(Nes nes)
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
            if (_input != null) _input.OutgoingGain = value ? 0 : Volume;
        }
    }

    internal double Volume { get; private set; } = 1.0;

    internal void SetVolume(double value)
    {
        Volume = Math.Clamp(value, 0, 1);
        if (_input != null && !_muted) _input.OutgoingGain = Volume;
    }

    private async Task InitializeAsync()
    {
        var settings = new AudioGraphSettings(Windows.Media.Render.AudioRenderCategory.GameMedia)
        {
            QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency
        };
        var graphResult = await AudioGraph.CreateAsync(settings);
        if (graphResult.Status != AudioGraphCreationStatus.Success)
            throw new InvalidOperationException($"AudioGraph creation failed: {graphResult.Status}.");

        _graph = graphResult.Graph;
        var outputResult = await _graph.CreateDeviceOutputNodeAsync();
        if (outputResult.Status != AudioDeviceNodeCreationStatus.Success)
            throw new InvalidOperationException($"Audio output creation failed: {outputResult.Status}.");

        _output = outputResult.DeviceOutputNode;
        var encoding = AudioEncodingProperties.CreatePcm(Nes.AudioSampleRate, Channels, 32);
        encoding.Subtype = MediaEncodingSubtypes.Float;
        _input = _graph.CreateFrameInputNode(encoding);
        _input.AddOutgoingConnection(_output);
        _input.QuantumStarted += Input_QuantumStarted;
        _graph.Start();
    }

    private unsafe void Input_QuantumStarted(AudioFrameInputNode sender, FrameInputNodeQuantumStartedEventArgs args)
    {
        var requiredSamples = (int)args.RequiredSamples;
        if (requiredSamples <= 0 || _disposed) return;

        using var frame = new AudioFrame((uint)(requiredSamples * Channels * sizeof(float)));
        using var buffer = frame.LockBuffer(AudioBufferAccessMode.Write);
        using var reference = buffer.CreateReference();
        ((IMemoryBufferByteAccess)reference).GetBuffer(out var bytes, out var capacity);
        var output = new Span<float>(bytes, Math.Min(requiredSamples * Channels, (int)capacity / sizeof(float)));
        output.Clear();

        var outputFrame = 0;
        while (outputFrame < requiredSamples)
        {
            var requested = Math.Min(_monoScratch.Length, requiredSamples - outputFrame);
            var read = _nes.ReadAudioSamples(_monoScratch.AsSpan(0, requested));
            for (var index = 0; index < read; index++)
            {
                var sample = _monoScratch[index];
                var destination = (outputFrame + index) * Channels;
                output[destination] = sample;
                output[destination + 1] = sample;
            }
            outputFrame += requested;
            if (read < requested) break;
        }

        sender.AddFrame(frame);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        if (_input != null) _input.QuantumStarted -= Input_QuantumStarted;
        _graph?.Stop();
        _input?.Dispose();
        _output?.Dispose();
        _graph?.Dispose();
        return ValueTask.CompletedTask;
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-8659-1A5B5B7C5361")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}
