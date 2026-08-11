using BenchmarkDotNet.Attributes;

using Sheep.Emulation.Nes.Buses;

namespace Sheep.Emulation.Nes.Benchmarks;

[MemoryDiagnoser]
public class CpuBenchmarks
{
    private Cpu.Cpu _cpu = null!;
    private TestBus _bus = null!;
    private InterruptLines _interrupts = null!;

    [GlobalSetup]
    public void Setup()
    {
        _interrupts = new InterruptLines();
        _cpu = new Cpu.Cpu(_interrupts);
        _bus = new TestBus();
        _cpu.ConnectBus(_bus);

        // Preload a tight instruction loop into test memory:
        // $8000: CLC
        // $8001: LDA #$01
        // $8003: ADC #$02
        // $8005: STA $00
        // $8007: JMP $8000
        _bus.Memory[0x8000] = 0x18; // CLC
        _bus.Memory[0x8001] = 0xA9; // LDA #$01
        _bus.Memory[0x8002] = 0x01;
        _bus.Memory[0x8003] = 0x69; // ADC #$02
        _bus.Memory[0x8004] = 0x02;
        _bus.Memory[0x8005] = 0x85; // STA $00
        _bus.Memory[0x8006] = 0x00;
        _bus.Memory[0x8007] = 0x4C; // JMP $8000
        _bus.Memory[0x8008] = 0x00;
        _bus.Memory[0x8009] = 0x80;

        // Reset vector
        _bus.Memory[0xFFFC] = 0x00;
        _bus.Memory[0xFFFD] = 0x80;

        _cpu.Reset();
    }

    [Benchmark(OperationsPerInvoke = 1000)]
    public void ExecuteInstructions1000()
    {
        for (int i = 0; i < 1000; i++)
        {
            _cpu.Step();
        }
    }

    private sealed class TestBus : IBus
    {
        public readonly byte[] Memory = new byte[0x10000];

        public byte Read(ushort address) => Memory[address];
        public void Write(ushort address, byte value) => Memory[address] = value;
        public byte Peek(ushort address) => Memory[address];
    }
}
