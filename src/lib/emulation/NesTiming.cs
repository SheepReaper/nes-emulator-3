namespace SR.Emulation.Nes;

public class NesTiming(
    int masterClockHz,
    int cpuDivisor,
    int ppuDivisor,
    int scanlinesPerFrame,
    int dotsPerScanline,
    ApuRegion apuRegion)
{
    public int MasterClockHz => masterClockHz;
    public int CpuDivisor => cpuDivisor;
    public int PpuDivisor => ppuDivisor;
    public int ScanlinesPerFrame => scanlinesPerFrame;
    public int DotsPerScanline => dotsPerScanline;
    public ApuRegion ApuRegion => apuRegion;
}

