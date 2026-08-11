using System;
using System.Threading;
namespace Sheep.Emulation.Nes.Video;

/// <summary>Provides immutable, zero-copy access to a published video frame.</summary>
public sealed class NesVideoFrame : IDisposable
{
    private Action? _release;

    internal NesVideoFrame(ReadOnlyMemory<byte> pixels, ulong frameNumber, NesVideoStandard videoStandard, Action release)
    {
        Pixels = pixels;
        FrameNumber = frameNumber;
        VideoStandard = videoStandard;
        _release = release;
    }

    /// <summary>Gets the immutable RGBA pixels for the lifetime of this lease.</summary>
    public ReadOnlyMemory<byte> Pixels { get; }

    /// <summary>Gets the monotonically increasing emulated frame number.</summary>
    public ulong FrameNumber { get; }

    /// <summary>Gets the television standard used to generate this frame.</summary>
    public NesVideoStandard VideoStandard { get; }

    /// <summary>Gets the pixel layout.</summary>
    public NesPixelFormat PixelFormat => NesPixelFormat.Rgba8888;

    /// <summary>Releases the backing frame slot for reuse by the renderer.</summary>
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}