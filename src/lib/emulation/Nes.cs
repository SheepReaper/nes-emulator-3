using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;

public sealed class Nes
{
    private readonly InterruptLines _interrupts = new();
    private readonly CartridgeSlot _cartridgeSlot = new();
    private readonly CartridgeFactory _cartridgeFactory = new();

    private readonly IBus _cpuBus;
    private readonly IBus _ppuBus;
    private readonly Cpu _cpu;
    private readonly Ppu _ppu;

    private ulong _systemClockCounter;

    public Nes()
    {
        _cpu = new Cpu(_interrupts);
        _ppu = new Ppu(_interrupts);
        Apu apu = new(_interrupts);

        _ppuBus = new PpuBus(_cartridgeSlot);
        _ppu.ConnectBus(_ppuBus);

        _cpuBus = new CpuBus(_cpu, _ppu, apu, _cartridgeSlot);
        _cpu.ConnectBus(_cpuBus);
    }

    public void LoadRom(byte[] romData)
    {
        var cartridge = _cartridgeFactory.Create(romData);
        _cartridgeSlot.Insert(cartridge);
        Reset();
    }

    public void Reset()
    {
        _cpu.Reset();
        // TODO: Reset other components like PPU and APU
    }

    public void Clock()
    {
        // The PPU clock runs 3x faster than the CPU clock.
        _ppu.Clock();

        // The CPU clock runs every 3 PPU clocks.
        if (_systemClockCounter % 3 == 0)
        {
            _cpu.Clock(_systemClockCounter);
        }

        _systemClockCounter++;
    }
}
