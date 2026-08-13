using Sheep.Emulation.Nes;

namespace EmuSheep;

internal sealed class NesSessionAudioState : IAsyncDisposable
{
    private readonly NesSystem _nes;
    private NesAudioPlayer? _player;
    private bool _muted;
    private double _volume = 1;

    public NesSessionAudioState(NesSystem nes) => _nes = nes;

    public bool HasAudio => _player != null;

    public async Task InitializeAsync(
        Action onAudioSamplesRequested,
        Action<Exception> onAudioUnavailable)
    {
        try
        {
            var player = await NesAudioPlayer.CreateAsync(_nes);
            player.AudioSamplesRequested += onAudioSamplesRequested;
            player.IsMuted = _muted;
            player.SetVolume(_volume);
            _player = player;
        }
        catch (Exception exception)
        {
            _player = null;
            onAudioUnavailable(exception);
        }
    }

    public void SetMuted(bool muted)
    {
        _muted = muted;
        _player?.IsMuted = muted;
    }

    public void SetVolume(double volume)
    {
        _volume = Math.Clamp(volume, 0, 1);
        _player?.SetVolume(_volume);
    }

    public NesAudioPlayer? GetPlayer() => Volatile.Read(ref _player);

    public async ValueTask DisposeAsync()
    {
        var player = _player;
        if (player != null)
        {
            _player = null;
            await player.DisposeAsync().ConfigureAwait(false);
        }
    }
}
