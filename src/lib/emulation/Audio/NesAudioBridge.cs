using System;

namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Audio buffer and filter delegates for NesSystem.
/// </summary>
internal static class NesAudioBridge
{
    internal static NesAudioFilterMode GetFilterMode(NesSystem nes, Apu apu)
    {
        lock (nes.SyncRoot)
        {
            return apu.FilterMode;
        }
    }

    internal static void SetFilterMode(NesSystem nes, Apu apu, NesAudioFilterMode mode)
    {
        lock (nes.SyncRoot)
        {
            apu.FilterMode = mode;
        }
    }

    internal static int ReadSamples(Apu apu, Span<float> destination) =>
        apu.ReadAudioSamples(destination);

    internal static NesAudioReadResult ReadAudio(Apu apu, Span<float> destination) =>
        apu.ReadAudio(destination);

    internal static void DiscardSamples(Apu apu) =>
        apu.DiscardAudioSamples();
}
