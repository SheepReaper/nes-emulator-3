namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuAluOperations
{
    internal static void Compare(CpuState s, byte register, byte operand)
    {
        byte result = (byte)(register - operand);
        s.P.Carry = register >= operand;
        s.SetZeroAndNegativeFlags(result);
    }

    internal static void Adc(CpuState s, byte operand)
    {
        ushort sum = (ushort)(s.A + operand + (s.P.Carry ? 1 : 0));
        s.P.Carry = sum > 0xFF;
        s.P.Overflow = (~(s.A ^ operand) & (s.A ^ sum) & 0x80) != 0;
        s.A = (byte)sum;
        s.SetZeroAndNegativeFlags(s.A);
    }

    internal static byte Asl(CpuState s, byte operand)
    {
        s.P.Carry = (operand & 0x80) != 0;
        byte result = (byte)(operand << 1);
        s.SetZeroAndNegativeFlags(result);
        return result;
    }

    internal static byte Lsr(CpuState s, byte operand)
    {
        s.P.Carry = (operand & 0x01) != 0;
        byte result = (byte)(operand >> 1);
        s.SetZeroAndNegativeFlags(result);
        return result;
    }

    internal static byte Rol(CpuState s, byte operand)
    {
        bool oldCarry = s.P.Carry;
        s.P.Carry = (operand & 0x80) != 0;
        byte result = (byte)((operand << 1) | (oldCarry ? 1 : 0));
        s.SetZeroAndNegativeFlags(result);
        return result;
    }

    internal static byte Ror(CpuState s, byte operand)
    {
        bool oldCarry = s.P.Carry;
        s.P.Carry = (operand & 0x01) != 0;
        byte result = (byte)((operand >> 1) | (oldCarry ? 0x80 : 0));
        s.SetZeroAndNegativeFlags(result);
        return result;
    }

    internal static void Bit(CpuState s, byte operand)
    {
        s.P.Zero = (s.A & operand) == 0;
        s.P.Overflow = (operand & 0x40) != 0;
        s.P.Negative = (operand & 0x80) != 0;
    }
}
