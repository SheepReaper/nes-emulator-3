namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuStackOperations
{
    internal static void Push(Cpu cpu, CpuState s, byte value)
    {
        cpu.Write((ushort)(0x0100 + (s.SP & 0xFF)), value);
        s.SP--;
    }

    internal static byte Pull(Cpu cpu, CpuState s)
    {
        s.SP++;
        return cpu.Read((ushort)(0x0100 + s.SP));
    }

    internal static ushort ReadWord(Cpu cpu, ushort address)
    {
        byte lo = cpu.Read(address);
        byte hi = cpu.Read((ushort)(address + 1));
        return (ushort)((hi << 8) | lo);
    }

    [CpuBehavior(
        CpuBehaviorKind.Nmos6502Quirk,
        "Indirect 16-bit reads wrap the high-byte fetch within the same page when the pointer ends in $FF.",
        "https://www.nesdev.org/wiki/Instruction_reference#JMP_-_Jump")]
    internal static ushort ReadWordBug(Cpu cpu, ushort address)
    {
        byte lo = cpu.Read(address);
        byte hi = cpu.Read((ushort)((address & 0xFF00) | ((address + 1) & 0x00FF)));
        return (ushort)((hi << 8) | lo);
    }
}
