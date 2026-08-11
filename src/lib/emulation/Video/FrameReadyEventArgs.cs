using System;

namespace Sheep.Emulation.Nes.Video;

public sealed class FrameReadyEventArgs(ulong frameNumber, NesVideoStandard videoStandard) : EventArgs
{
    public ulong FrameNumber { get; } = frameNumber;
    public NesVideoStandard VideoStandard { get; } = videoStandard;
}