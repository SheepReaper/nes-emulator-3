using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes.ConformanceTests;

internal interface INesTestMachine
{
    void RunForPpuDots(int count);
    void Reset();
    void SetControllerState(int controller, NesControllerButton buttons);
    byte PeekCpuMemory(ushort address);
    byte PeekPpuMemory(ushort address);
    ushort ProgramCounter { get; }
    void WriteCpuMemory(ushort address, byte value);
    void SetCpuRegisters(CpuRegisterValues registers);
}

internal enum NesTestOutcome
{
    Passed,
    Failed,
    TimedOut
}

internal sealed record NesTestRunResult(
    NesTestOutcome Outcome,
    byte? Code,
    string Output,
    long ElapsedPpuDots,
    int ResetCount);

internal sealed class NesTestMachine(Sheep.Emulation.Nes.NesSystem nes) : INesTestMachine
{
    public void RunForPpuDots(int count) => nes.RunForPpuDots(count);
    public void Reset() => nes.Reset();
    public void SetControllerState(int controller, NesControllerButton buttons) =>
        nes.SetControllerState(controller, buttons);
    public byte PeekCpuMemory(ushort address) => nes.Debugger.PeekCpuMemory(address);
    public byte PeekPpuMemory(ushort address) => nes.Debugger.PeekPpuMemory(address);
    public ushort ProgramCounter => nes.Debugger.ProgramCounter;
    public void WriteCpuMemory(ushort address, byte value)
    {
        var wasRunning = nes.Debugger.ExecutionState == NesExecutionState.Running;
        if (wasRunning) nes.Debugger.Pause();
        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.CpuRam, address, new[] { value });
        if (wasRunning) nes.Debugger.Resume();
    }

    public void SetCpuRegisters(CpuRegisterValues registers)
    {
        var wasRunning = nes.Debugger.ExecutionState == NesExecutionState.Running;
        if (wasRunning) nes.Debugger.Pause();
        nes.Debugger.SetCpuRegisters(registers);
        if (wasRunning) nes.Debugger.Resume();
    }
}
