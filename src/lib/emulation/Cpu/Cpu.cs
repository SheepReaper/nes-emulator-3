using System;
using System.Diagnostics;
using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes.Cpu;

public sealed class Cpu(InterruptLines interrupts) : IBusMaster
{
    private readonly InterruptLines _interrupts = interrupts;
    private readonly CpuState _s = new();
    private readonly Action?[] _instructionDispatch = new Action?[256];
    private IBus? _bus;

    public void ConnectBus(IBus bus) => _bus = bus;

    internal CpuState State => _s;
    internal ushort PendingBusAddress => CpuDmaBusInspector.GetPendingBusAddress(_s);
    internal bool CanHaltForDma => !CpuDmaBusInspector.IsCurrentCycleWrite(_s);
    internal ushort? DmaReadAddress => CpuDmaBusInspector.GetDmaReadAddress(_s);
    internal ushort ProgramCounter => _s.ProgramCounter;
    internal bool IsInstructionBoundary => _s.Cycles == 0;

    public void Reset()
    {
        var lo = Read(0xFFFC);
        var hi = Read(0xFFFD);
        _s.ProgramCounter = (ushort)((hi << 8) | lo);
        _s.Reset();
        _ = _interrupts.ConsumeNmiEdge();
    }

    internal void NotifyDmaHalt() => _s.DmaHaltOccurred = true;

    public void Clock(ulong masterClock = 0) =>
        CpuClockDriver.Clock(this, _s, _interrupts, _instructionDispatch, masterClock);

    public ulong Step()
    {
        var startCycles = _s.TotalCyclesExecuted;
        do
        {
            Clock();
        } while (_s.Cycles > 0);
        return _s.TotalCyclesExecuted - startCycles;
    }

    public void Stall(int cycles) => _s.Cycles += cycles;
    public bool IsOnOddCycle() => (_s.MasterClock % 2) != 0;

    public byte Read(ushort address)
    {
        Debug.Assert(_bus != null, "CPU bus is not connected.");
        return _bus.Read(address);
    }

    public void Write(ushort address, byte value)
    {
        Debug.Assert(_bus != null, "CPU bus is not connected.");
        _bus.Write(address, value);
    }

    internal void CompleteIoRead(ushort address, Action action)
    {
        _s.PendingIoReadAddress = address;
        _s.EndOfInstructionAction = action;
    }

    internal void CompleteMemoryRead(ushort address, Action<byte> consume) =>
        CompleteIoRead(address, () => consume(Read(address)));

    internal void CompleteWrite(Action action)
    {
        _s.EndOfInstructionAction = action;
        _s.EndOfInstructionIsWrite = true;
    }

    internal void LoadA(ushort address) => CompleteMemoryRead(address, val => { _s.A = val; _s.SetZeroAndNegativeFlags(_s.A); });
    internal void LoadX(ushort address) => CompleteMemoryRead(address, val => { _s.X = val; _s.SetZeroAndNegativeFlags(_s.X); });
    internal void LoadY(ushort address) => CompleteMemoryRead(address, val => { _s.Y = val; _s.SetZeroAndNegativeFlags(_s.Y); });
    internal void ReadAccumulator(ushort address, Func<byte, byte> op) => CompleteMemoryRead(address, val => { _s.A = op(val); _s.SetZeroAndNegativeFlags(_s.A); });
    internal void StartAbsoluteRead(Action<byte> consumer) { _s.AbsoluteReadConsumer = consumer; _s.Cycles = 4; }

    internal CpuDebugState CaptureDebugState() => _s.CaptureDebugState();
    internal void SetRegisters(CpuRegisterValues r) => _s.SetRegisters(r);
}
