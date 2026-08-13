using Sheep.Emulation.Nes;

namespace EmuSheep;

internal sealed class NesSessionFrameBuffer
{
    private readonly Lock _frameGate = new();
    private NesVideoFrame? _latestFrame;

    public bool TryCopyLatestFrame(Span<byte> destination, out ulong frameNumber)
    {
        if (destination.Length < NesSystem.FrameBufferSize)
        {
            throw new ArgumentException($"The destination must contain at least {NesSystem.FrameBufferSize} bytes.", nameof(destination));
        }

        lock (_frameGate)
        {
            if (_latestFrame == null)
            {
                frameNumber = 0;
                return false;
            }

            _latestFrame.Pixels.Span.CopyTo(destination);
            frameNumber = _latestFrame.FrameNumber;
            return true;
        }
    }

    public void PublishFrame(NesVideoFrame? nextFrame)
    {
        NesVideoFrame? previousFrame;
        lock (_frameGate)
        {
            previousFrame = _latestFrame;
            _latestFrame = nextFrame;
        }
        previousFrame?.Dispose();
    }

    public void Clear()
    {
        lock (_frameGate)
        {
            _latestFrame?.Dispose();
            _latestFrame = null;
        }
    }
}
