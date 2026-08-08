using System;

namespace SR.Emulation.Nes;

public sealed class FrameReadyEventArgs(ulong frameNumber, NesVideoStandard videoStandard) : EventArgs
{
    public ulong FrameNumber { get; } = frameNumber;
    public NesVideoStandard VideoStandard { get; } = videoStandard;
}
