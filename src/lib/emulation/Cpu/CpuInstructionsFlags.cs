namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionsFlags
{
    internal static void Clc(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => s.P.Carry = false); s.Cycles = 2; }
    internal static void Sec(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => s.P.Carry = true); s.Cycles = 2; }
    internal static void Cli(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => s.P.InterruptDisable = false); s.Cycles = 2; }
    internal static void Sei(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => s.P.InterruptDisable = true); s.Cycles = 2; }
    internal static void Clv(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => s.P.Overflow = false); s.Cycles = 2; }
    internal static void Cld(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => s.P.Decimal = false); s.Cycles = 2; }
    internal static void Sed(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => s.P.Decimal = true); s.Cycles = 2; }

    internal static void NopImm(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_IMM(s), _ => { }); s.Cycles = 2; }
    internal static void NopZp(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_ZP(cpu, s), _ => { }); s.Cycles = 3; }
    internal static void NopZpx(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_ZPX(cpu, s), _ => { }); s.Cycles = 4; }
    internal static void NopAbsx(Cpu cpu, CpuState s)
    {
        s.Cycles = 4;
        var address = CpuAddressingModes.Addr_ABSX(cpu, s);
        cpu.CompleteIoRead(address, () => _ = cpu.Read(address));
    }
}
