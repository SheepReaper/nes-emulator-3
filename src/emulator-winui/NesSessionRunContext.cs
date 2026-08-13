namespace EmuSheep;

internal sealed class NesSessionRunContext
{
    private readonly Func<NesAudioPlayer?> _getAudioPlayer;
    private readonly Func<double> _getSpeedMultiplier;
    private readonly Func<CancellationToken, Task> _waitForAudioDemand;
    private readonly Action<NesVideoFrame?> _publishFrame;
    private readonly Action _onFrameAvailable;
    private readonly Action<double> _onFrameRateAvailable;
    private readonly Action<Exception> _onFaulted;

    public NesSessionRunContext(
        Func<NesAudioPlayer?> getAudioPlayer,
        Func<double> getSpeedMultiplier,
        Func<CancellationToken, Task> waitForAudioDemand,
        Action<NesVideoFrame?> publishFrame,
        Action onFrameAvailable,
        Action<double> onFrameRateAvailable,
        Action<Exception> onFaulted)
    {
        _getAudioPlayer = getAudioPlayer;
        _getSpeedMultiplier = getSpeedMultiplier;
        _waitForAudioDemand = waitForAudioDemand;
        _publishFrame = publishFrame;
        _onFrameAvailable = onFrameAvailable;
        _onFrameRateAvailable = onFrameRateAvailable;
        _onFaulted = onFaulted;
    }

    public NesAudioPlayer? GetAudioPlayer() => _getAudioPlayer();
    public double GetSpeedMultiplier() => _getSpeedMultiplier();
    public Task WaitForAudioDemandAsync(CancellationToken ct) => _waitForAudioDemand(ct);
    public void PublishFrame(NesVideoFrame? frame) => _publishFrame(frame);
    public void OnFrameAvailable() => _onFrameAvailable();
    public void OnFrameRateAvailable(double fps) => _onFrameRateAvailable(fps);
    public void OnFaulted(Exception ex) => _onFaulted(ex);
}
