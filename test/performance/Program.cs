using System.Diagnostics;
using Sheep.Emulation.Nes;

RunScenario("Rendering disabled", enableRendering: false);
RunScenario("Rendering enabled", enableRendering: true);

static void RunScenario(string name, bool enableRendering)
{
    const int measuredDots = 10_000_000;
    var nes = new NesSystem();
    nes.LoadRom(CreateLoopRom(enableRendering));
    nes.RunForPpuDots(1_000_000);

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var stopwatch = Stopwatch.StartNew();
    var result = nes.RunForPpuDots(measuredDots);
    stopwatch.Stop();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

    Console.WriteLine(name);
    Console.WriteLine($"PPU dots: {result.PpuDots:N0}");
    Console.WriteLine($"Elapsed: {stopwatch.Elapsed.TotalMilliseconds:N2} ms");
    Console.WriteLine($"Throughput: {result.PpuDots / stopwatch.Elapsed.TotalSeconds:N0} PPU dots/s");
    Console.WriteLine($"Allocation: {allocated:N0} bytes");
}

static byte[] CreateLoopRom(bool enableRendering)
{
    var rom = new byte[16 + 16_384 + 8_192];
    rom[0] = (byte)'N'; rom[1] = (byte)'E'; rom[2] = (byte)'S'; rom[3] = 0x1A;
    rom[4] = 1;
    rom[5] = 1;
    Array.Fill(rom, (byte)0xEA, 16, 16_384);
    var program = rom.AsSpan(16);
    if (enableRendering)
    {
        program[0] = 0xA9; // LDA #$18
        program[1] = 0x18;
        program[2] = 0x8D; // STA $2001
        program[3] = 0x01;
        program[4] = 0x20;
        program[5] = 0x4C; // JMP $8005
        program[6] = 0x05;
        program[7] = 0x80;
    }
    else
    {
        program[0] = 0x4C; // JMP $8000
        program[1] = 0x00;
        program[2] = 0x80;
    }
    rom[16 + 0x3FFA] = 0x00;
    rom[16 + 0x3FFB] = 0x80;
    rom[16 + 0x3FFC] = 0x00;
    rom[16 + 0x3FFD] = 0x80;
    rom[16 + 0x3FFE] = 0x00;
    rom[16 + 0x3FFF] = 0x80;
    return rom;
}
