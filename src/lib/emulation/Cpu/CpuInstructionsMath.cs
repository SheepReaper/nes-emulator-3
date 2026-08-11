namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionsMath
{
    internal static void AdcImm(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_IMM(s), v => CpuAluOperations.Adc(s, v)); s.Cycles = 2; }
    internal static void AdcZp(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_ZP(cpu, s), v => CpuAluOperations.Adc(s, v)); s.Cycles = 3; }
    internal static void AdcZpx(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_ZPX(cpu, s), v => CpuAluOperations.Adc(s, v)); s.Cycles = 4; }
    internal static void AdcAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(v => CpuAluOperations.Adc(s, v));
    internal static void AdcAbsx(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSX(cpu, s); cpu.CompleteMemoryRead(a, v => CpuAluOperations.Adc(s, v)); }
    internal static void AdcAbsy(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSY(cpu, s); cpu.CompleteMemoryRead(a, v => CpuAluOperations.Adc(s, v)); }
    internal static void AdcIndx(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_INDX(cpu, s), v => CpuAluOperations.Adc(s, v)); s.Cycles = 6; }
    internal static void AdcIndy(Cpu cpu, CpuState s) { s.Cycles = 5; var a = CpuAddressingModes.Addr_INDY(cpu, s); cpu.CompleteMemoryRead(a, v => CpuAluOperations.Adc(s, v)); }

    internal static void SbcImm(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_IMM(s), v => CpuAluOperations.Adc(s, (byte)~v)); s.Cycles = 2; }
    internal static void SbcZp(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_ZP(cpu, s), v => CpuAluOperations.Adc(s, (byte)~v)); s.Cycles = 3; }
    internal static void SbcZpx(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_ZPX(cpu, s), v => CpuAluOperations.Adc(s, (byte)~v)); s.Cycles = 4; }
    internal static void SbcAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(v => CpuAluOperations.Adc(s, (byte)~v));
    internal static void SbcAbsx(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSX(cpu, s); cpu.CompleteMemoryRead(a, v => CpuAluOperations.Adc(s, (byte)~v)); }
    internal static void SbcAbsy(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSY(cpu, s); cpu.CompleteMemoryRead(a, v => CpuAluOperations.Adc(s, (byte)~v)); }
    internal static void SbcIndx(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_INDX(cpu, s), v => CpuAluOperations.Adc(s, (byte)~v)); s.Cycles = 6; }
    internal static void SbcIndy(Cpu cpu, CpuState s) { s.Cycles = 5; var a = CpuAddressingModes.Addr_INDY(cpu, s); cpu.CompleteMemoryRead(a, v => CpuAluOperations.Adc(s, (byte)~v)); }

    internal static void CmpImm(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_IMM(s), v => CpuAluOperations.Compare(s, s.A, v)); s.Cycles = 2; }
    internal static void CmpZp(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_ZP(cpu, s), v => CpuAluOperations.Compare(s, s.A, v)); s.Cycles = 3; }
    internal static void CmpZpx(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_ZPX(cpu, s), v => CpuAluOperations.Compare(s, s.A, v)); s.Cycles = 4; }
    internal static void CmpAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(v => CpuAluOperations.Compare(s, s.A, v));
    internal static void CmpAbsx(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSX(cpu, s); cpu.CompleteMemoryRead(a, v => CpuAluOperations.Compare(s, s.A, v)); }
    internal static void CmpAbsy(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSY(cpu, s); cpu.CompleteMemoryRead(a, v => CpuAluOperations.Compare(s, s.A, v)); }
    internal static void CmpIndx(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_INDX(cpu, s), v => CpuAluOperations.Compare(s, s.A, v)); s.Cycles = 6; }
    internal static void CmpIndy(Cpu cpu, CpuState s) { s.Cycles = 5; var a = CpuAddressingModes.Addr_INDY(cpu, s); cpu.CompleteMemoryRead(a, v => CpuAluOperations.Compare(s, s.A, v)); }

    internal static void CpxImm(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_IMM(s), v => CpuAluOperations.Compare(s, s.X, v)); s.Cycles = 2; }
    internal static void CpxZp(Cpu cpu, CpuState s) { CpuAluOperations.Compare(s, s.X, cpu.Read(CpuAddressingModes.Addr_ZP(cpu, s))); s.Cycles = 3; }
    internal static void CpxAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(v => CpuAluOperations.Compare(s, s.X, v));

    internal static void CpyImm(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_IMM(s), v => CpuAluOperations.Compare(s, s.Y, v)); s.Cycles = 2; }
    internal static void CpyZp(Cpu cpu, CpuState s) { CpuAluOperations.Compare(s, s.Y, cpu.Read(CpuAddressingModes.Addr_ZP(cpu, s))); s.Cycles = 3; }
    internal static void CpyAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(v => CpuAluOperations.Compare(s, s.Y, v));

    internal static void BitZp(Cpu cpu, CpuState s) { CpuAluOperations.Bit(s, cpu.Read(CpuAddressingModes.Addr_ZP(cpu, s))); s.Cycles = 3; }
    internal static void BitAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(v => CpuAluOperations.Bit(s, v));
}
