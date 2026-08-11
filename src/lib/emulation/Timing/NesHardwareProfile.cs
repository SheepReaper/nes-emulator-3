using System;

namespace Sheep.Emulation.Nes.Timing;

/// <summary>Describes a supported pairing of physical NES CPU/APU and PPU revisions.</summary>
public sealed class NesHardwareProfile
{
    private NesHardwareProfile(
        NesCpuModel cpuModel,
        NesPpuModel ppuModel,
        NesVideoStandard videoStandard,
        NesTiming timing)
    {
        CpuModel = cpuModel;
        PpuModel = ppuModel;
        VideoStandard = videoStandard;
        Timing = timing;
    }

    /// <summary>The NTSC consumer NES hardware pairing used by later front-loading consoles.</summary>
    public static NesHardwareProfile Rp2A03G_Rp2C02G { get; } = new(
        NesCpuModel.Rp2A03G, NesPpuModel.Rp2C02G, NesVideoStandard.Ntsc, new NtscTiming());

    /// <summary>The licensed PAL consumer NES hardware pairing.</summary>
    public static NesHardwareProfile Rp2A07_Rp2C07 { get; } = new(
        NesCpuModel.Rp2A07, NesPpuModel.Rp2C07, NesVideoStandard.Pal, new PalTiming());

    /// <summary>Gets the physical CPU/APU revision.</summary>
    public NesCpuModel CpuModel { get; }

    /// <summary>Gets the physical PPU revision.</summary>
    public NesPpuModel PpuModel { get; }

    /// <summary>Gets the television standard produced by the hardware pairing.</summary>
    public NesVideoStandard VideoStandard { get; }

    /// <summary>Gets the shared master-clock timing for the hardware pairing.</summary>
    public NesTiming Timing { get; }

    internal static NesHardwareProfile ForVideoStandard(NesVideoStandard videoStandard) => videoStandard switch
    {
        NesVideoStandard.Ntsc => Rp2A03G_Rp2C02G,
        NesVideoStandard.Pal => Rp2A07_Rp2C07,
        _ => throw new ArgumentOutOfRangeException(nameof(videoStandard))
    };
}
