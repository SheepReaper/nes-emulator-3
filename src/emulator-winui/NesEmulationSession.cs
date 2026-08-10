using System.Diagnostics;
using SR.Emulation.Nes;

namespace EmuSheep;

internal sealed class NesEmulationSession : IAsyncDisposable
{
    private const double NtscFramesPerSecond = 60.0988;
    private const double MaximumRecoverableLagInFrames = 4;

    private readonly Nes _nes;
    private readonly byte[] _latestFrame = new byte[Nes.FrameBufferSize];
    private readonly object _frameGate = new();
    private readonly object _stateGate = new();
    private readonly CancellationTokenSource _cancellation = new();

    private Task? _runTask;
    private ulong _latestFrameNumber;
    private bool _hasFrame;
    private bool _disposed;

    public NesEmulationSession(byte[] romData)
    {
        ArgumentNullException.ThrowIfNull(romData);
        _nes = new Nes(NesVideoStandard.Ntsc);
        _nes.LoadRom(romData);
    }

    public event EventHandler? FrameAvailable;
    public event EventHandler<EmulationFaultedEventArgs>? Faulted;

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

    public void Start()
    {
        lock (_stateGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_runTask != null)
            {
                throw new InvalidOperationException("The emulation session has already been started.");
            }

            _runTask = Task.Run(() => RunAsync(_cancellation.Token));
        }
    }

    public bool TryCopyLatestFrame(Span<byte> destination, out ulong frameNumber)
    {
        if (destination.Length < Nes.FrameBufferSize)
        {
            throw new ArgumentException($"The destination must contain at least {Nes.FrameBufferSize} bytes.", nameof(destination));
        }

        lock (_frameGate)
        {
            if (!_hasFrame)
            {
                frameNumber = 0;
                return false;
            }

            _latestFrame.CopyTo(destination);
            frameNumber = _latestFrameNumber;
            return true;
        }
    }

    public void SetControllerState(NesControllerButton buttons) =>
        _nes.SetControllerState(0, buttons);

    public async Task StopAsync()
    {
        Task? runTask;
        lock (_stateGate)
        {
            _cancellation.Cancel();
            runTask = _runTask;
        }

        if (runTask != null)
        {
            await runTask.ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_stateGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        await StopAsync().ConfigureAwait(false);
        _cancellation.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var frameDurationTicks = Stopwatch.Frequency / NtscFramesPerSecond;
        double nextFrameDeadline = stopwatch.ElapsedTicks;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = _nes.RunUntilFrame();
                if (result.Frames == 0)
                {
                    throw new InvalidOperationException($"Emulation stopped before completing a frame ({result.StopReason}).");
                }

                lock (_frameGate)
                {
                    _hasFrame = _nes.TryCopyFrame(_latestFrame, out _latestFrameNumber);
                }

                cancellationToken.ThrowIfCancellationRequested();
                FrameAvailable?.Invoke(this, EventArgs.Empty);

                nextFrameDeadline += frameDurationTicks;
                var remainingTicks = nextFrameDeadline - stopwatch.ElapsedTicks;
                if (remainingTicks > 0)
                {
                    var delay = TimeSpan.FromSeconds(remainingTicks / Stopwatch.Frequency);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                else if (-remainingTicks > frameDurationTicks * MaximumRecoverableLagInFrames)
                {
                    nextFrameDeadline = stopwatch.ElapsedTicks;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Faulted?.Invoke(this, new EmulationFaultedEventArgs(exception));
        }
    }
}

internal sealed class EmulationFaultedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
