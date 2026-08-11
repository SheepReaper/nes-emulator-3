using BenchmarkDotNet.Attributes;

using Sheep.Emulation.Nes.Audio;
using Sheep.Emulation.Nes.Buses;

namespace Sheep.Emulation.Nes.Benchmarks;

[MemoryDiagnoser]
public class ApuBenchmarks
{
    private Apu _apu = null!;
    private InterruptLines _interrupts = null!;
    private float[] _sampleBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _interrupts = new InterruptLines();
        _apu = new Apu(_interrupts);
        _sampleBuffer = new float[1024];

        // Enable channels via $4015
        _apu.Write(0x4015, 0x0F);

        // Configure Pulse 1
        _apu.Write(0x4000, 0xBF); // 50% duty, constant volume 15
        _apu.Write(0x4002, 0xFD); // Period low
        _apu.Write(0x4003, 0x08); // Length counter & period high

        // Configure Noise
        _apu.Write(0x400C, 0x3F); // Constant volume 15
        _apu.Write(0x400E, 0x04); // Period 4
        _apu.Write(0x400F, 0x08); // Length counter
    }

    [Benchmark(OperationsPerInvoke = 29780)]
    public void ClockOneFullFrameApuCycles()
    {
        // 29780 master / PPU cycles corresponds to ~9927 APU / CPU cycles
        for (int i = 0; i < 9927; i++)
        {
            _apu.Clock();
        }

        _apu.ReadAudioSamples(_sampleBuffer);
    }
}
