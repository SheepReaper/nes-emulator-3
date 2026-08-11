namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionsUnofficial
{
    internal static void AacImm(Cpu cpu, CpuState s)
    {
        s.A &= cpu.Read(CpuAddressingModes.Addr_IMM(s));
        s.SetZeroAndNegativeFlags(s.A);
        s.P.Carry = s.P.Negative;
        s.Cycles = 2;
    }

    internal static void AsrImm(Cpu cpu, CpuState s)
    {
        s.A &= cpu.Read(CpuAddressingModes.Addr_IMM(s));
        s.A = CpuAluOperations.Lsr(s, s.A);
        s.Cycles = 2;
    }

    internal static void ArrImm(Cpu cpu, CpuState s)
    {
        var oldCarry = s.P.Carry;
        var value = (byte)(s.A & cpu.Read(CpuAddressingModes.Addr_IMM(s)));
        s.A = (byte)((value >> 1) | (oldCarry ? 0x80 : 0));
        s.SetZeroAndNegativeFlags(s.A);
        s.P.Carry = (s.A & 0x40) != 0;
        s.P.Overflow = (((s.A >> 6) ^ (s.A >> 5)) & 1) != 0;
        s.Cycles = 2;
    }

    [CpuBehavior(
        CpuBehaviorKind.Nes2A03Deviation,
        "Opcode $AB uses the NES 2A03's observed $FF bus constant, producing A = X = immediate operand.",
        "https://forums.nesdev.org/viewtopic.php?t=3831")]
    internal static void AtxImm(Cpu cpu, CpuState s)
    {
        s.A = cpu.Read(CpuAddressingModes.Addr_IMM(s));
        s.X = s.A;
        s.SetZeroAndNegativeFlags(s.A);
        s.Cycles = 2;
    }

    internal static void AxsImm(Cpu cpu, CpuState s)
    {
        var value = (byte)(s.A & s.X);
        var operand = cpu.Read(CpuAddressingModes.Addr_IMM(s));
        s.X = (byte)(value - operand);
        s.P.Carry = value >= operand;
        s.SetZeroAndNegativeFlags(s.X);
        s.Cycles = 2;
    }

    internal static void XaaImm(Cpu cpu, CpuState s)
    {
        s.A = (byte)(s.X & cpu.Read(CpuAddressingModes.Addr_IMM(s)));
        s.SetZeroAndNegativeFlags(s.A);
        s.Cycles = 2;
    }

    internal static void LasAbsy(Cpu cpu, CpuState s)
    {
        s.Cycles = 4;
        var a = CpuAddressingModes.Addr_ABSY(cpu, s);
        cpu.CompleteMemoryRead(a, operand =>
        {
            var value = (byte)(operand & s.SP);
            s.A = s.X = s.SP = value;
            s.SetZeroAndNegativeFlags(value);
        });
    }

    internal static void SyaAbsx(Cpu cpu, CpuState s)
    {
        CpuUnofficialStores.StoreHighMaskedIndexed(cpu, s, s.X, s.Y);
        s.Cycles = 5;
    }

    internal static void SxaAbsy(Cpu cpu, CpuState s)
    {
        CpuUnofficialStores.StoreHighMaskedIndexed(cpu, s, s.Y, s.X);
        s.Cycles = 5;
    }

    internal static void AhxIndy(Cpu cpu, CpuState s)
    {
        var zeroPagePointer = cpu.Read(s.ProgramCounter++);
        var baseAddress = CpuStackOperations.ReadWordBug(cpu, zeroPagePointer);
        CpuUnofficialStores.StoreAhx(cpu, s, baseAddress, s.Y, updateStackPointer: false);
        s.Cycles = 6;
    }

    internal static void AhxAbsy(Cpu cpu, CpuState s)
    {
        var baseAddress = CpuStackOperations.ReadWord(cpu, s.ProgramCounter);
        s.ProgramCounter += 2;
        CpuUnofficialStores.StoreAhx(cpu, s, baseAddress, s.Y, updateStackPointer: false);
        s.Cycles = 5;
    }

    internal static void TasAbsy(Cpu cpu, CpuState s)
    {
        var baseAddress = CpuStackOperations.ReadWord(cpu, s.ProgramCounter);
        s.ProgramCounter += 2;
        CpuUnofficialStores.StoreAhx(cpu, s, baseAddress, s.Y, updateStackPointer: true);
        s.Cycles = 5;
    }
}
