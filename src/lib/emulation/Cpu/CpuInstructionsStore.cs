namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionsStore
{
    internal static void StaZp(Cpu cpu, CpuState s) { var a = CpuAddressingModes.Addr_ZP(cpu, s); s.Cycles = 3; cpu.CompleteWrite(() => cpu.Write(a, s.A)); }
    internal static void StaZpx(Cpu cpu, CpuState s) { var a = CpuAddressingModes.Addr_ZPX(cpu, s); s.Cycles = 4; cpu.CompleteWrite(() => cpu.Write(a, s.A)); }
    internal static void StaAbs(CpuState s) { s.AbsoluteStoreRegister = 1; s.Cycles = 4; }
    internal static void StaAbsx(Cpu cpu, CpuState s)
    {
        var a = CpuAddressingModes.Addr_ABSX_Write(cpu, s);
        s.PenultimateInstructionAction = () => { if (s.RmwDummyReadAddress.HasValue) { var dummy = s.RmwDummyReadAddress.Value; s.RmwDummyReadAddress = null; _ = cpu.Read(dummy); } };
        s.Cycles = 5;
        cpu.CompleteWrite(() => cpu.Write(a, s.A));
    }
    internal static void StaAbsy(Cpu cpu, CpuState s)
    {
        var a = CpuAddressingModes.Addr_ABSY_Write(cpu, s);
        s.PenultimateInstructionAction = () => { if (s.RmwDummyReadAddress.HasValue) { var dummy = s.RmwDummyReadAddress.Value; s.RmwDummyReadAddress = null; _ = cpu.Read(dummy); } };
        s.Cycles = 5;
        cpu.CompleteWrite(() => cpu.Write(a, s.A));
    }
    internal static void StaIndx(Cpu cpu, CpuState s) { var a = CpuAddressingModes.Addr_INDX(cpu, s); s.Cycles = 6; cpu.CompleteWrite(() => cpu.Write(a, s.A)); }
    internal static void StaIndy(Cpu cpu, CpuState s) { var a = CpuAddressingModes.Addr_INDY_Write(cpu, s); s.Cycles = 6; cpu.CompleteWrite(() => cpu.Write(a, s.A)); }

    internal static void StxZp(Cpu cpu, CpuState s) { var a = CpuAddressingModes.Addr_ZP(cpu, s); s.Cycles = 3; cpu.CompleteWrite(() => cpu.Write(a, s.X)); }
    internal static void StxZpy(Cpu cpu, CpuState s) { var a = CpuAddressingModes.Addr_ZPY(cpu, s); s.Cycles = 4; cpu.CompleteWrite(() => cpu.Write(a, s.X)); }
    internal static void StxAbs(CpuState s) { s.AbsoluteStoreRegister = 2; s.Cycles = 4; }

    internal static void StyZp(Cpu cpu, CpuState s) { var a = CpuAddressingModes.Addr_ZP(cpu, s); s.Cycles = 3; cpu.CompleteWrite(() => cpu.Write(a, s.Y)); }
    internal static void StyZpx(Cpu cpu, CpuState s) { var a = CpuAddressingModes.Addr_ZPX(cpu, s); s.Cycles = 4; cpu.CompleteWrite(() => cpu.Write(a, s.Y)); }
    internal static void StyAbs(CpuState s) { s.AbsoluteStoreRegister = 3; s.Cycles = 4; }

    internal static void Tax(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.X = s.A; s.SetZeroAndNegativeFlags(s.X); }); s.Cycles = 2; }
    internal static void Tay(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.Y = s.A; s.SetZeroAndNegativeFlags(s.Y); }); s.Cycles = 2; }
    internal static void Txa(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.A = s.X; s.SetZeroAndNegativeFlags(s.A); }); s.Cycles = 2; }
    internal static void Tya(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.A = s.Y; s.SetZeroAndNegativeFlags(s.A); }); s.Cycles = 2; }
    internal static void Tsx(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.X = s.SP; s.SetZeroAndNegativeFlags(s.X); }); s.Cycles = 2; }
    internal static void Txs(Cpu cpu, CpuState s) { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { s.SP = s.X; }); s.Cycles = 2; }
}
