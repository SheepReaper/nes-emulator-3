using System;

namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuReadModifyWrite
{
    internal static void BeginRmw(CpuState s, ushort address, Func<byte, byte> operation)
    {
        s.RmwAddress = address;
        s.RmwOperation = operation;
        s.RmwInProgress = true;
    }

    internal static void Slo(CpuState s, ushort address) =>
        BeginRmw(s, address, val => { var res = CpuAluOperations.Asl(s, val); s.A |= res; s.SetZeroAndNegativeFlags(s.A); return res; });

    internal static void Rla(CpuState s, ushort address) =>
        BeginRmw(s, address, val => { var res = CpuAluOperations.Rol(s, val); s.A &= res; s.SetZeroAndNegativeFlags(s.A); return res; });

    internal static void Sre(CpuState s, ushort address) =>
        BeginRmw(s, address, val => { var res = CpuAluOperations.Lsr(s, val); s.A ^= res; s.SetZeroAndNegativeFlags(s.A); return res; });

    internal static void Rra(CpuState s, ushort address) =>
        BeginRmw(s, address, val => { var res = CpuAluOperations.Ror(s, val); CpuAluOperations.Adc(s, res); return res; });

    internal static void Dcp(CpuState s, ushort address) =>
        BeginRmw(s, address, val => { var res = (byte)(val - 1); CpuAluOperations.Compare(s, s.A, res); return res; });

    internal static void Isc(CpuState s, ushort address) =>
        BeginRmw(s, address, val => { var res = (byte)(val + 1); CpuAluOperations.Adc(s, (byte)~res); return res; });

    internal static void Inc(CpuState s, ushort address) =>
        BeginRmw(s, address, val => { var res = (byte)(val + 1); s.SetZeroAndNegativeFlags(res); return res; });

    internal static void Dec(CpuState s, ushort address) =>
        BeginRmw(s, address, val => { var res = (byte)(val - 1); s.SetZeroAndNegativeFlags(res); return res; });
}
