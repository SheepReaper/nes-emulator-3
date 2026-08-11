using System;

namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionDecoder
{
    internal static Action Decode(byte opcode, Cpu cpu, CpuState s, InterruptLines interrupts)
    {
        var action = CpuInstructionTableOfficial.CreateOfficial(opcode, cpu, s, interrupts);
        return action != null ? action : CpuInstructionTableUnofficialPart1.Create(opcode, cpu, s) ?? (() => s.Cycles = 2);
    }
}
