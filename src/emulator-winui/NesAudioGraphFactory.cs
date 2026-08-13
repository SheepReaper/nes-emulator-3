using Windows.Foundation;
using Windows.Media.Audio;
using Windows.Media.Render;

namespace EmuSheep;

internal static class NesAudioGraphFactory
{
    internal static async Task<(AudioGraph Graph, AudioDeviceOutputNode Output, AudioFrameInputNode Input)> CreateGraphAsync(
        TypedEventHandler<AudioFrameInputNode, FrameInputNodeQuantumStartedEventArgs> quantumStartedHandler,
        TypedEventHandler<AudioFrameInputNode, AudioFrameCompletedEventArgs> frameCompletedHandler)
    {
        var settings = new AudioGraphSettings(AudioRenderCategory.Media)
        {
            QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency
        };
        var graphResult = await AudioGraph.CreateAsync(settings);
        if (graphResult.Status != AudioGraphCreationStatus.Success)
        {
            throw new InvalidOperationException(
                $"AudioGraph creation failed: {graphResult.Status} ({graphResult.ExtendedError?.Message}).");
        }

        var graph = graphResult.Graph;
        var outputResult = await graph.CreateDeviceOutputNodeAsync();
        if (outputResult.Status != AudioDeviceNodeCreationStatus.Success)
        {
            throw new InvalidOperationException(
                $"Audio output creation failed: {outputResult.Status} ({outputResult.ExtendedError?.Message}).");
        }

        var output = outputResult.DeviceOutputNode;
        var encoding = graph.EncodingProperties;
        encoding.ChannelCount = 1;
        if (encoding.SampleRate != NesSystem.AudioSampleRate)
        {
            throw new InvalidOperationException(
                $"The selected audio device uses {encoding.SampleRate} Hz, but the emulator produces {NesSystem.AudioSampleRate} Hz audio.");
        }

        var input = graph.CreateFrameInputNode(encoding);
        input.AddOutgoingConnection(output);
        input.QuantumStarted += quantumStartedHandler;
        input.AudioFrameCompleted += frameCompletedHandler;
        input.Stop();
        graph.Start();
        return (graph, output, input);
    }
}
