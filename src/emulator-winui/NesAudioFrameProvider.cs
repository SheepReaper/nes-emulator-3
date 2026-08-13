using Windows.Media;
using Windows.Media.Audio;

namespace EmuSheep;

internal static class NesAudioFrameProvider
{
    internal static void ProcessQuantum(
        AudioFrameInputNode sender,
        int requiredSamples,
        NesSystem nes,
        Action? audioSamplesRequested)
    {
        var frame = new AudioFrame((uint)(requiredSamples * sizeof(float)));
        try
        {
            AudioFrameBuffer.WithSpan<float, NesSystem>(
                frame,
                AudioBufferAccessMode.Write,
                requiredSamples,
                nes,
                static (output, n) =>
                {
                    output.Clear();
                    n.ReadAudio(output);
                });

            sender.AddFrame(frame);
        }
        catch
        {
            frame.Dispose();
            throw;
        }

        audioSamplesRequested?.Invoke();
    }
}
