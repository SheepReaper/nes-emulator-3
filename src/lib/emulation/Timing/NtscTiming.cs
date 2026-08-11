namespace Sheep.Emulation.Nes.Timing;

public sealed class NtscTiming() : NesTiming(
    masterClockHz: 21477272,
    cpuDivisor: 12,
    ppuDivisor: 4,
    scanlinesPerFrame: 262,
    dotsPerScanline: 341,
    apuRegion: ApuRegion.Ntsc);