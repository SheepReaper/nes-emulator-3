using BenchmarkDotNet.Attributes;
using Sheep.Emulation.Nes.Buses;
using Sheep.Emulation.Nes.Cartridges;
using Sheep.Emulation.Nes.Video;

namespace Sheep.Emulation.Nes.Benchmarks;

[MemoryDiagnoser]
public class PpuBenchmarks
{
    private Ppu _ppu = null!;
    private PpuBus _ppuBus = null!;
    private CartridgeSlot _cartridgeSlot = null!;
    private InterruptLines _interrupts = null!;

    [GlobalSetup]
    public void Setup()
    {
        _interrupts = new InterruptLines();
        _cartridgeSlot = new CartridgeSlot();
        _ppuBus = new PpuBus(_cartridgeSlot);
        _ppu = new Ppu(_interrupts);
        _ppu.ConnectBus(_ppuBus);

        // Turn on background and sprite rendering via PPUMASK ($2001)
        _ppu.Write(0x2001, 0x1E);
    }

    [Benchmark(OperationsPerInvoke = 29780)]
    public void ClockOneFullFrameDots()
    {
        // One NTSC frame is ~29780.5 PPU dots (341 dots * 261 + 340.5)
        for (int i = 0; i < 29780; i++)
        {
            _ppu.Clock();
        }
    }
}
