namespace SR.Emulation.Nes;

public sealed class PalTiming() : NesTiming(
    masterClockHz: 26601712,
    cpuDivisor: 16,
    ppuDivisor: 5,
    scanlinesPerFrame: 312,
    dotsPerScanline: 341,
    apuRegion: ApuRegion.Pal);

