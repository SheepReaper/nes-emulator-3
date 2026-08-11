namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionsLogic
{
    internal static void LdaImm(Cpu cpu, CpuState s) { cpu.LoadA(CpuAddressingModes.Addr_IMM(s)); s.Cycles = 2; }
    internal static void LdaZp(Cpu cpu, CpuState s) { cpu.LoadA(CpuAddressingModes.Addr_ZP(cpu, s)); s.Cycles = 3; }
    internal static void LdaZpx(Cpu cpu, CpuState s) { cpu.LoadA(CpuAddressingModes.Addr_ZPX(cpu, s)); s.Cycles = 4; }
    internal static void LdaAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(val => { s.A = val; s.SetZeroAndNegativeFlags(s.A); });
    internal static void LdaAbsx(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSX(cpu, s); cpu.LoadA(a); }
    internal static void LdaAbsy(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSY(cpu, s); cpu.LoadA(a); }
    internal static void LdaIndx(Cpu cpu, CpuState s) { cpu.LoadA(CpuAddressingModes.Addr_INDX(cpu, s)); s.Cycles = 6; }
    internal static void LdaIndy(Cpu cpu, CpuState s) { s.Cycles = 5; var a = CpuAddressingModes.Addr_INDY(cpu, s); cpu.LoadA(a); }

    internal static void LdxImm(Cpu cpu, CpuState s) { cpu.LoadX(CpuAddressingModes.Addr_IMM(s)); s.Cycles = 2; }
    internal static void LdxZp(Cpu cpu, CpuState s) { cpu.LoadX(CpuAddressingModes.Addr_ZP(cpu, s)); s.Cycles = 3; }
    internal static void LdxZpy(Cpu cpu, CpuState s) { cpu.LoadX(CpuAddressingModes.Addr_ZPY(cpu, s)); s.Cycles = 4; }
    internal static void LdxAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(val => { s.X = val; s.SetZeroAndNegativeFlags(s.X); });
    internal static void LdxAbsy(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSY(cpu, s); cpu.LoadX(a); }

    internal static void LdyImm(Cpu cpu, CpuState s) { cpu.LoadY(CpuAddressingModes.Addr_IMM(s)); s.Cycles = 2; }
    internal static void LdyZp(Cpu cpu, CpuState s) { cpu.LoadY(CpuAddressingModes.Addr_ZP(cpu, s)); s.Cycles = 3; }
    internal static void LdyZpx(Cpu cpu, CpuState s) { cpu.LoadY(CpuAddressingModes.Addr_ZPX(cpu, s)); s.Cycles = 4; }
    internal static void LdyAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(val => { s.Y = val; s.SetZeroAndNegativeFlags(s.Y); });
    internal static void LdyAbsx(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSX(cpu, s); cpu.LoadY(a); }

    internal static void AndImm(Cpu cpu, CpuState s) { cpu.ReadAccumulator(CpuAddressingModes.Addr_IMM(s), v => (byte)(s.A & v)); s.Cycles = 2; }
    internal static void AndZp(Cpu cpu, CpuState s) { cpu.ReadAccumulator(CpuAddressingModes.Addr_ZP(cpu, s), v => (byte)(s.A & v)); s.Cycles = 3; }
    internal static void AndZpx(Cpu cpu, CpuState s) { cpu.ReadAccumulator(CpuAddressingModes.Addr_ZPX(cpu, s), v => (byte)(s.A & v)); s.Cycles = 4; }
    internal static void AndAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(v => { s.A &= v; s.SetZeroAndNegativeFlags(s.A); });
    internal static void AndAbsx(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSX(cpu, s); cpu.ReadAccumulator(a, v => (byte)(s.A & v)); }
    internal static void AndAbsy(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSY(cpu, s); cpu.ReadAccumulator(a, v => (byte)(s.A & v)); }
    internal static void AndIndx(Cpu cpu, CpuState s) { cpu.ReadAccumulator(CpuAddressingModes.Addr_INDX(cpu, s), v => (byte)(s.A & v)); s.Cycles = 6; }
    internal static void AndIndy(Cpu cpu, CpuState s) { s.Cycles = 5; var a = CpuAddressingModes.Addr_INDY(cpu, s); cpu.ReadAccumulator(a, v => (byte)(s.A & v)); }

    internal static void EorImm(Cpu cpu, CpuState s) { cpu.ReadAccumulator(CpuAddressingModes.Addr_IMM(s), v => (byte)(s.A ^ v)); s.Cycles = 2; }
    internal static void EorZp(Cpu cpu, CpuState s) { cpu.ReadAccumulator(CpuAddressingModes.Addr_ZP(cpu, s), v => (byte)(s.A ^ v)); s.Cycles = 3; }
    internal static void EorZpx(Cpu cpu, CpuState s) { cpu.ReadAccumulator(CpuAddressingModes.Addr_ZPX(cpu, s), v => (byte)(s.A ^ v)); s.Cycles = 4; }
    internal static void EorAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(v => { s.A ^= v; s.SetZeroAndNegativeFlags(s.A); });
    internal static void EorAbsx(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSX(cpu, s); cpu.ReadAccumulator(a, v => (byte)(s.A ^ v)); }
    internal static void EorAbsy(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSY(cpu, s); cpu.ReadAccumulator(a, v => (byte)(s.A ^ v)); }
    internal static void EorIndx(Cpu cpu, CpuState s) { cpu.ReadAccumulator(CpuAddressingModes.Addr_INDX(cpu, s), v => (byte)(s.A ^ v)); s.Cycles = 6; }
    internal static void EorIndy(Cpu cpu, CpuState s) { s.Cycles = 5; var a = CpuAddressingModes.Addr_INDY(cpu, s); cpu.ReadAccumulator(a, v => (byte)(s.A ^ v)); }

    internal static void OraImm(Cpu cpu, CpuState s) { cpu.ReadAccumulator(CpuAddressingModes.Addr_IMM(s), v => (byte)(s.A | v)); s.Cycles = 2; }
    internal static void OraZp(Cpu cpu, CpuState s) { cpu.ReadAccumulator(CpuAddressingModes.Addr_ZP(cpu, s), v => (byte)(s.A | v)); s.Cycles = 3; }
    internal static void OraZpx(Cpu cpu, CpuState s) { cpu.ReadAccumulator(CpuAddressingModes.Addr_ZPX(cpu, s), v => (byte)(s.A | v)); s.Cycles = 4; }
    internal static void OraAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(v => { s.A |= v; s.SetZeroAndNegativeFlags(s.A); });
    internal static void OraAbsx(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSX(cpu, s); cpu.ReadAccumulator(a, v => (byte)(s.A | v)); }
    internal static void OraAbsy(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSY(cpu, s); cpu.ReadAccumulator(a, v => (byte)(s.A | v)); }
    internal static void OraIndx(Cpu cpu, CpuState s) { cpu.ReadAccumulator(CpuAddressingModes.Addr_INDX(cpu, s), v => (byte)(s.A | v)); s.Cycles = 6; }
    internal static void OraIndy(Cpu cpu, CpuState s) { s.Cycles = 5; var a = CpuAddressingModes.Addr_INDY(cpu, s); cpu.ReadAccumulator(a, v => (byte)(s.A | v)); }
}
