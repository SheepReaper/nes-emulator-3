using BenchmarkDotNet.Attributes;

namespace Sheep.Emulation.Nes.Benchmarks;

[MemoryDiagnoser]
public class FullSystemBenchmarks
{
    private NesSystem _nes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _nes = new NesSystem();

        // Construct a minimal valid NROM-128 iNES cartridge
        byte[] rom = new byte[16 + 16384 + 8192];
        // Header
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = 1; // 1x 16KB PRG
        rom[5] = 1; // 1x 8KB CHR

        // Code in PRG ($C000 offset in ROM is 16):
        // $C000: 78 (SEI)
        // $C001: D8 (CLD)
        // $C002: A9 00 (LDA #$00)
        // $C004: 8D 00 20 (STA $2000)
        // $C007: 8D 01 20 (STA $2001)
        // $C00A: 4C 0A C0 (JMP $C00A)
        int prgOffset = 16;
        rom[prgOffset + 0] = 0x78;
        rom[prgOffset + 1] = 0xD8;
        rom[prgOffset + 2] = 0xA9;
        rom[prgOffset + 3] = 0x00;
        rom[prgOffset + 4] = 0x8D;
        rom[prgOffset + 5] = 0x00;
        rom[prgOffset + 6] = 0x20;
        rom[prgOffset + 7] = 0x8D;
        rom[prgOffset + 8] = 0x01;
        rom[prgOffset + 9] = 0x20;
        rom[prgOffset + 10] = 0x4C;
        rom[prgOffset + 11] = 0x0A;
        rom[prgOffset + 12] = 0xC0;

        // Reset Vector at $FFFC (offset in 16KB PRG is 16384 - 4 = 16380)
        rom[prgOffset + 16380] = 0x00; // $C000
        rom[prgOffset + 16381] = 0xC0;

        _nes.LoadRom(rom);
    }

    [Benchmark]
    public void EmulateOneFullFrame()
    {
        _nes.RunUntilFrame();
    }
}
