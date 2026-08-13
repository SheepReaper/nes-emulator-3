namespace EmuSheep;

internal sealed class NesSessionRunner
{
    private readonly Lock _stateGate = new();
    private Task? _runTask;
    private bool _disposed;

    public bool IsRunning
    {
        get
        {
            lock (_stateGate)
            {
                return _runTask is { IsCompleted: false };
            }
        }
    }

    public void Start(Func<CancellationToken, Task> runFactory, CancellationToken cancellationToken)
    {
        lock (_stateGate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NesSessionRunner));
            }
            if (_runTask != null)
            {
                throw new InvalidOperationException("The emulation session has already been started.");
            }

            _runTask = Task.Run(() => runFactory(cancellationToken));
        }
    }

    public async Task StopAsync(CancellationTokenSource cancellation)
    {
        Task? runTask;
        lock (_stateGate)
        {
            cancellation.Cancel();
            runTask = _runTask;
        }

        if (runTask != null)
        {
            await runTask.ConfigureAwait(false);
        }
    }

    public void MarkDisposed()
    {
        lock (_stateGate)
        {
            _disposed = true;
        }
    }
}
