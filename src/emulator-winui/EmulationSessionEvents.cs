namespace EmuSheep;

internal sealed class EmulationFaultedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

internal sealed class FrameRateAvailableEventArgs(double framesPerSecond) : EventArgs
{
    public double FramesPerSecond { get; } = framesPerSecond;
}
