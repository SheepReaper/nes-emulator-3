using System;

namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Frame copy and lease services for NesSystem.
/// </summary>
internal static class NesVideoOutput
{
    internal static bool TryCopyFrame(Ppu ppu, Span<byte> destination, out ulong frameNumber)
    {
        return destination.Length < NesSystem.FrameBufferSize
            ? throw new ArgumentException(
                $"The destination must contain at least {NesSystem.FrameBufferSize} bytes.",
                nameof(destination))
            : ppu.TryCopyFrame(destination, out frameNumber);
    }

    internal static NesVideoFrame? TryAcquireFrame(Ppu ppu, NesVideoStandard standard) =>
        ppu.TryAcquireFrame(standard);
}
