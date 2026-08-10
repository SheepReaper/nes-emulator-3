using SR.Emulation.Nes;

namespace EmuSheep;

internal sealed class NesAudioPlayer : IAsyncDisposable
{
    internal static Task<NesAudioPlayer> CreateAsync(Nes nes) =>
        Task.FromException<NesAudioPlayer>(new PlatformNotSupportedException("AudioGraph is not available in unit tests."));

    internal bool IsMuted { get; set; }
    internal void SetVolume(double value) { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
