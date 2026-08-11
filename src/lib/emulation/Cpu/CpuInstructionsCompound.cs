namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionsCompound
{
    internal static void SloZp(Cpu cpu, CpuState s) { CpuReadModifyWrite.Slo(s, CpuAddressingModes.Addr_ZP(cpu, s)); s.Cycles = 5; }
    internal static void RlaZp(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rla(s, CpuAddressingModes.Addr_ZP(cpu, s)); s.Cycles = 5; }
    internal static void SreZp(Cpu cpu, CpuState s) { CpuReadModifyWrite.Sre(s, CpuAddressingModes.Addr_ZP(cpu, s)); s.Cycles = 5; }
    internal static void RraZp(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rra(s, CpuAddressingModes.Addr_ZP(cpu, s)); s.Cycles = 5; }
    internal static void AaxZp(Cpu cpu, CpuState s) { cpu.Write(CpuAddressingModes.Addr_ZP(cpu, s), (byte)(s.A & s.X)); s.Cycles = 3; }
    internal static void LaxZp(Cpu cpu, CpuState s) { s.A = s.X = cpu.Read(CpuAddressingModes.Addr_ZP(cpu, s)); s.SetZeroAndNegativeFlags(s.A); s.Cycles = 3; }
    internal static void DcpZp(Cpu cpu, CpuState s) { CpuReadModifyWrite.Dcp(s, CpuAddressingModes.Addr_ZP(cpu, s)); s.Cycles = 5; }
    internal static void IscZp(Cpu cpu, CpuState s) { CpuReadModifyWrite.Isc(s, CpuAddressingModes.Addr_ZP(cpu, s)); s.Cycles = 5; }

    internal static void SloZpx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Slo(s, CpuAddressingModes.Addr_ZPX(cpu, s)); s.Cycles = 6; }
    internal static void RlaZpx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rla(s, CpuAddressingModes.Addr_ZPX(cpu, s)); s.Cycles = 6; }
    internal static void SreZpx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Sre(s, CpuAddressingModes.Addr_ZPX(cpu, s)); s.Cycles = 6; }
    internal static void RraZpx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rra(s, CpuAddressingModes.Addr_ZPX(cpu, s)); s.Cycles = 6; }
    internal static void AaxZpy(Cpu cpu, CpuState s) { cpu.Write(CpuAddressingModes.Addr_ZPY(cpu, s), (byte)(s.A & s.X)); s.Cycles = 4; }
    internal static void LaxZpy(Cpu cpu, CpuState s) { s.A = s.X = cpu.Read(CpuAddressingModes.Addr_ZPY(cpu, s)); s.SetZeroAndNegativeFlags(s.A); s.Cycles = 4; }
    internal static void DcpZpx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Dcp(s, CpuAddressingModes.Addr_ZPX(cpu, s)); s.Cycles = 6; }
    internal static void IscZpx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Isc(s, CpuAddressingModes.Addr_ZPX(cpu, s)); s.Cycles = 6; }

    internal static void SloAbs(Cpu cpu, CpuState s) { CpuReadModifyWrite.Slo(s, CpuAddressingModes.Addr_ABS(cpu, s)); s.Cycles = 6; }
    internal static void RlaAbs(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rla(s, CpuAddressingModes.Addr_ABS(cpu, s)); s.Cycles = 6; }
    internal static void SreAbs(Cpu cpu, CpuState s) { CpuReadModifyWrite.Sre(s, CpuAddressingModes.Addr_ABS(cpu, s)); s.Cycles = 6; }
    internal static void RraAbs(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rra(s, CpuAddressingModes.Addr_ABS(cpu, s)); s.Cycles = 6; }
    internal static void AaxAbs(CpuState s) { s.AbsoluteStoreRegister = 4; s.Cycles = 4; }
    internal static void LaxAbs(Cpu cpu, CpuState s) => cpu.StartAbsoluteRead(val => { s.A = s.X = val; s.SetZeroAndNegativeFlags(s.A); });
    internal static void DcpAbs(Cpu cpu, CpuState s) { CpuReadModifyWrite.Dcp(s, CpuAddressingModes.Addr_ABS(cpu, s)); s.Cycles = 6; }
    internal static void IscAbs(Cpu cpu, CpuState s) { CpuReadModifyWrite.Isc(s, CpuAddressingModes.Addr_ABS(cpu, s)); s.Cycles = 6; }

    internal static void SloAbsx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Slo(s, CpuAddressingModes.Addr_ABSX_Write(cpu, s)); s.Cycles = 7; }
    internal static void RlaAbsx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rla(s, CpuAddressingModes.Addr_ABSX_Write(cpu, s)); s.Cycles = 7; }
    internal static void SreAbsx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Sre(s, CpuAddressingModes.Addr_ABSX_Write(cpu, s)); s.Cycles = 7; }
    internal static void RraAbsx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rra(s, CpuAddressingModes.Addr_ABSX_Write(cpu, s)); s.Cycles = 7; }
    internal static void DcpAbsx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Dcp(s, CpuAddressingModes.Addr_ABSX_Write(cpu, s)); s.Cycles = 7; }
    internal static void IscAbsx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Isc(s, CpuAddressingModes.Addr_ABSX_Write(cpu, s)); s.Cycles = 7; }

    internal static void SloAbsy(Cpu cpu, CpuState s) { CpuReadModifyWrite.Slo(s, CpuAddressingModes.Addr_ABSY_Write(cpu, s)); s.Cycles = 7; }
    internal static void RlaAbsy(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rla(s, CpuAddressingModes.Addr_ABSY_Write(cpu, s)); s.Cycles = 7; }
    internal static void SreAbsy(Cpu cpu, CpuState s) { CpuReadModifyWrite.Sre(s, CpuAddressingModes.Addr_ABSY_Write(cpu, s)); s.Cycles = 7; }
    internal static void RraAbsy(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rra(s, CpuAddressingModes.Addr_ABSY_Write(cpu, s)); s.Cycles = 7; }
    internal static void DcpAbsy(Cpu cpu, CpuState s) { CpuReadModifyWrite.Dcp(s, CpuAddressingModes.Addr_ABSY_Write(cpu, s)); s.Cycles = 7; }
    internal static void IscAbsy(Cpu cpu, CpuState s) { CpuReadModifyWrite.Isc(s, CpuAddressingModes.Addr_ABSY_Write(cpu, s)); s.Cycles = 7; }
    internal static void LaxAbsy(Cpu cpu, CpuState s) { s.Cycles = 4; var a = CpuAddressingModes.Addr_ABSY(cpu, s); cpu.CompleteMemoryRead(a, v => { s.A = s.X = v; s.SetZeroAndNegativeFlags(s.A); }); }

    internal static void SloIndx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Slo(s, CpuAddressingModes.Addr_INDX(cpu, s)); s.Cycles = 8; }
    internal static void RlaIndx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rla(s, CpuAddressingModes.Addr_INDX(cpu, s)); s.Cycles = 8; }
    internal static void SreIndx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Sre(s, CpuAddressingModes.Addr_INDX(cpu, s)); s.Cycles = 8; }
    internal static void RraIndx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rra(s, CpuAddressingModes.Addr_INDX(cpu, s)); s.Cycles = 8; }
    internal static void AaxIndx(Cpu cpu, CpuState s) { cpu.Write(CpuAddressingModes.Addr_INDX(cpu, s), (byte)(s.A & s.X)); s.Cycles = 6; }
    internal static void LaxIndx(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(CpuAddressingModes.Addr_INDX(cpu, s), v => { s.A = s.X = v; s.SetZeroAndNegativeFlags(s.A); }); s.Cycles = 6; }
    internal static void DcpIndx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Dcp(s, CpuAddressingModes.Addr_INDX(cpu, s)); s.Cycles = 8; }
    internal static void IscIndx(Cpu cpu, CpuState s) { CpuReadModifyWrite.Isc(s, CpuAddressingModes.Addr_INDX(cpu, s)); s.Cycles = 8; }

    internal static void SloIndy(Cpu cpu, CpuState s) { CpuReadModifyWrite.Slo(s, CpuAddressingModes.Addr_INDY(cpu, s, false)); s.Cycles = 8; }
    internal static void RlaIndy(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rla(s, CpuAddressingModes.Addr_INDY(cpu, s, false)); s.Cycles = 8; }
    internal static void SreIndy(Cpu cpu, CpuState s) { CpuReadModifyWrite.Sre(s, CpuAddressingModes.Addr_INDY(cpu, s, false)); s.Cycles = 8; }
    internal static void RraIndy(Cpu cpu, CpuState s) { CpuReadModifyWrite.Rra(s, CpuAddressingModes.Addr_INDY(cpu, s, false)); s.Cycles = 8; }
    internal static void LaxIndy(Cpu cpu, CpuState s) { s.Cycles = 5; var a = CpuAddressingModes.Addr_INDY(cpu, s); cpu.CompleteMemoryRead(a, v => { s.A = s.X = v; s.SetZeroAndNegativeFlags(s.A); }); }
    internal static void DcpIndy(Cpu cpu, CpuState s) { CpuReadModifyWrite.Dcp(s, CpuAddressingModes.Addr_INDY(cpu, s, false)); s.Cycles = 8; }
    internal static void IscIndy(Cpu cpu, CpuState s) { CpuReadModifyWrite.Isc(s, CpuAddressingModes.Addr_INDY(cpu, s, false)); s.Cycles = 8; }
}
