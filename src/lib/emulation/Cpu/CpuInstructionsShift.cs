namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionsShift
{
    internal static void AslAcc(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.A = CpuAluOperations.Asl(s, s.A); }); s.Cycles = 2; }
    internal static void AslZp(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ZP(cpu, s), v => CpuAluOperations.Asl(s, v)); s.Cycles = 5; }
    internal static void AslZpx(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ZPX(cpu, s), v => CpuAluOperations.Asl(s, v)); s.Cycles = 6; }
    internal static void AslAbs(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ABS(cpu, s), v => CpuAluOperations.Asl(s, v)); s.Cycles = 6; }
    internal static void AslAbsx(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ABSX_Write(cpu, s), v => CpuAluOperations.Asl(s, v)); s.Cycles = 7; }

    internal static void LsrAcc(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.A = CpuAluOperations.Lsr(s, s.A); }); s.Cycles = 2; }
    internal static void LsrZp(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ZP(cpu, s), v => CpuAluOperations.Lsr(s, v)); s.Cycles = 5; }
    internal static void LsrZpx(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ZPX(cpu, s), v => CpuAluOperations.Lsr(s, v)); s.Cycles = 6; }
    internal static void LsrAbs(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ABS(cpu, s), v => CpuAluOperations.Lsr(s, v)); s.Cycles = 6; }
    internal static void LsrAbsx(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ABSX_Write(cpu, s), v => CpuAluOperations.Lsr(s, v)); s.Cycles = 7; }

    internal static void RolAcc(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.A = CpuAluOperations.Rol(s, s.A); }); s.Cycles = 2; }
    internal static void RolZp(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ZP(cpu, s), v => CpuAluOperations.Rol(s, v)); s.Cycles = 5; }
    internal static void RolZpx(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ZPX(cpu, s), v => CpuAluOperations.Rol(s, v)); s.Cycles = 6; }
    internal static void RolAbs(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ABS(cpu, s), v => CpuAluOperations.Rol(s, v)); s.Cycles = 6; }
    internal static void RolAbsx(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ABSX_Write(cpu, s), v => CpuAluOperations.Rol(s, v)); s.Cycles = 7; }

    internal static void RorAcc(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.A = CpuAluOperations.Ror(s, s.A); }); s.Cycles = 2; }
    internal static void RorZp(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ZP(cpu, s), v => CpuAluOperations.Ror(s, v)); s.Cycles = 5; }
    internal static void RorZpx(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ZPX(cpu, s), v => CpuAluOperations.Ror(s, v)); s.Cycles = 6; }
    internal static void RorAbs(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ABS(cpu, s), v => CpuAluOperations.Ror(s, v)); s.Cycles = 6; }
    internal static void RorAbsx(Cpu cpu, CpuState s) { CpuReadModifyWrite.BeginRmw(s, CpuAddressingModes.Addr_ABSX_Write(cpu, s), v => CpuAluOperations.Ror(s, v)); s.Cycles = 7; }

    internal static void IncZp(Cpu cpu, CpuState s) { CpuReadModifyWrite.Inc(s, CpuAddressingModes.Addr_ZP(cpu, s)); s.Cycles = 5; }
    internal static void IncZpx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Inc(s, CpuAddressingModes.Addr_ZPX(cpu, s)); s.Cycles = 6; }
    internal static void IncAbs(Cpu cpu, CpuState s) { CpuReadModifyWrite.Inc(s, CpuAddressingModes.Addr_ABS(cpu, s)); s.Cycles = 6; }
    internal static void IncAbsx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Inc(s, CpuAddressingModes.Addr_ABSX_Write(cpu, s)); s.Cycles = 7; }

    internal static void DecZp(Cpu cpu, CpuState s) { CpuReadModifyWrite.Dec(s, CpuAddressingModes.Addr_ZP(cpu, s)); s.Cycles = 5; }
    internal static void DecZpx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Dec(s, CpuAddressingModes.Addr_ZPX(cpu, s)); s.Cycles = 6; }
    internal static void DecAbs(Cpu cpu, CpuState s) { CpuReadModifyWrite.Dec(s, CpuAddressingModes.Addr_ABS(cpu, s)); s.Cycles = 6; }
    internal static void DecAbsx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Dec(s, CpuAddressingModes.Addr_ABSX_Write(cpu, s)); s.Cycles = 7; }

    internal static void Inx(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.X++; s.SetZeroAndNegativeFlags(s.X); }); s.Cycles = 2; }
    internal static void Iny(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.Y++; s.SetZeroAndNegativeFlags(s.Y); }); s.Cycles = 2; }
    internal static void Dex(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.X--; s.SetZeroAndNegativeFlags(s.X); }); s.Cycles = 2; }
    internal static void Dey(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.Y--; s.SetZeroAndNegativeFlags(s.Y); }); s.Cycles = 2; }
}
