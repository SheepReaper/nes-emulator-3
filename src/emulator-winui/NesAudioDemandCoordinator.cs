namespace EmuSheep;

internal sealed class NesAudioDemandCoordinator : IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);
    private int _pending;

    internal void SignalDemand()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 0)
        {
            _signal.Release();
        }
    }

    internal async Task WaitForDemandAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _pending, 0);
    }

    public void Dispose() => _signal.Dispose();
}
