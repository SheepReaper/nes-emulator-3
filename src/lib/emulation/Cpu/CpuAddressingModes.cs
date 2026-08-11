namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuAddressingModes
{
    internal static ushort Addr_IMM(CpuState s) => s.ProgramCounter++;

    internal static ushort Addr_ZP(Cpu cpu, CpuState s) => cpu.Read(s.ProgramCounter++);

    internal static ushort Addr_ZPX(Cpu cpu, CpuState s)
    {
        var address = cpu.Read(s.ProgramCounter++);
        _ = cpu.Read(address);
        return (ushort)((address + s.X) & 0xFF);
    }

    internal static ushort Addr_ZPY(Cpu cpu, CpuState s)
    {
        var address = cpu.Read(s.ProgramCounter++);
        _ = cpu.Read(address);
        return (ushort)((address + s.Y) & 0xFF);
    }

    internal static ushort Addr_ABS(Cpu cpu, CpuState s)
    {
        ushort address = CpuStackOperations.ReadWord(cpu, s.ProgramCounter);
        s.ProgramCounter += 2;
        return address;
    }

    internal static ushort Addr_ABSX(Cpu cpu, CpuState s, bool addCycleOnPageCross = true)
    {
        ushort baseAddress = CpuStackOperations.ReadWord(cpu, s.ProgramCounter);
        s.ProgramCounter += 2;
        ushort effectiveAddress = (ushort)(baseAddress + s.X);
        if (addCycleOnPageCross && (effectiveAddress & 0xFF00) != (baseAddress & 0xFF00))
        {
            var dummy = (ushort)((baseAddress & 0xFF00) | (effectiveAddress & 0x00FF));
            s.PenultimateInstructionAction = () => _ = cpu.Read(dummy);
            s.Cycles++;
        }
        return effectiveAddress;
    }

    internal static ushort Addr_ABSX_Write(Cpu cpu, CpuState s)
    {
        ushort baseAddress = CpuStackOperations.ReadWord(cpu, s.ProgramCounter);
        s.ProgramCounter += 2;
        var effectiveAddress = (ushort)(baseAddress + s.X);
        s.RmwDummyReadAddress = (ushort)((baseAddress & 0xFF00) | (effectiveAddress & 0x00FF));
        return effectiveAddress;
    }

    internal static ushort Addr_ABSY(Cpu cpu, CpuState s, bool addCycleOnPageCross = true)
    {
        ushort baseAddress = CpuStackOperations.ReadWord(cpu, s.ProgramCounter);
        s.ProgramCounter += 2;
        ushort effectiveAddress = (ushort)(baseAddress + s.Y);
        if (addCycleOnPageCross && (effectiveAddress & 0xFF00) != (baseAddress & 0xFF00))
        {
            var dummy = (ushort)((baseAddress & 0xFF00) | (effectiveAddress & 0x00FF));
            s.PenultimateInstructionAction = () => _ = cpu.Read(dummy);
            s.Cycles++;
        }
        return effectiveAddress;
    }

    internal static ushort Addr_ABSY_Write(Cpu cpu, CpuState s)
    {
        ushort baseAddress = CpuStackOperations.ReadWord(cpu, s.ProgramCounter);
        s.ProgramCounter += 2;
        var effectiveAddress = (ushort)(baseAddress + s.Y);
        s.RmwDummyReadAddress = (ushort)((baseAddress & 0xFF00) | (effectiveAddress & 0x00FF));
        return effectiveAddress;
    }

    internal static ushort Addr_INDX(Cpu cpu, CpuState s)
    {
        byte zpPtr = cpu.Read(s.ProgramCounter++);
        _ = cpu.Read(zpPtr);
        byte effectiveZp = (byte)(zpPtr + s.X);
        return CpuStackOperations.ReadWordBug(cpu, effectiveZp);
    }

    internal static ushort Addr_INDY(Cpu cpu, CpuState s, bool addCycleOnPageCross = true)
    {
        byte zpPtr = cpu.Read(s.ProgramCounter++);
        ushort ptr = CpuStackOperations.ReadWordBug(cpu, zpPtr);
        ushort effectiveAddress = (ushort)(ptr + s.Y);
        if (addCycleOnPageCross && (effectiveAddress & 0xFF00) != (ptr & 0xFF00))
        {
            var dummy = (ushort)((ptr & 0xFF00) | (effectiveAddress & 0x00FF));
            s.PenultimateInstructionAction = () => _ = cpu.Read(dummy);
            s.Cycles++;
        }
        return effectiveAddress;
    }

    internal static ushort Addr_INDY_Write(Cpu cpu, CpuState s)
    {
        byte zpPtr = cpu.Read(s.ProgramCounter++);
        ushort ptr = CpuStackOperations.ReadWordBug(cpu, zpPtr);
        var effectiveAddress = (ushort)(ptr + s.Y);
        _ = cpu.Read((ushort)((ptr & 0xFF00) | (effectiveAddress & 0x00FF)));
        return effectiveAddress;
    }
}
