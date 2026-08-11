using System;

namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionTableOfficialPart2
{
    internal static Action? Create(byte opcode, Cpu cpu, CpuState s, InterruptLines interrupts) => opcode switch
    {
        // STA
        0x85 => () => CpuInstructionsStore.StaZp(cpu, s),
        0x95 => () => CpuInstructionsStore.StaZpx(cpu, s),
        0x8D => () => CpuInstructionsStore.StaAbs(s),
        0x9D => () => CpuInstructionsStore.StaAbsx(cpu, s),
        0x99 => () => CpuInstructionsStore.StaAbsy(cpu, s),
        0x81 => () => CpuInstructionsStore.StaIndx(cpu, s),
        0x91 => () => CpuInstructionsStore.StaIndy(cpu, s),

        // STX / STY
        0x86 => () => CpuInstructionsStore.StxZp(cpu, s),
        0x96 => () => CpuInstructionsStore.StxZpy(cpu, s),
        0x8E => () => CpuInstructionsStore.StxAbs(s),
        0x84 => () => CpuInstructionsStore.StyZp(cpu, s),
        0x94 => () => CpuInstructionsStore.StyZpx(cpu, s),
        0x8C => () => CpuInstructionsStore.StyAbs(s),

        // INC / DEC / Register inc/dec
        0xE6 => () => CpuInstructionsShift.IncZp(cpu, s),
        0xF6 => () => CpuInstructionsShift.IncZpx(cpu, s),
        0xEE => () => CpuInstructionsShift.IncAbs(cpu, s),
        0xFE => () => CpuInstructionsShift.IncAbsx(cpu, s),
        0xC6 => () => CpuInstructionsShift.DecZp(cpu, s),
        0xD6 => () => CpuInstructionsShift.DecZpx(cpu, s),
        0xCE => () => CpuInstructionsShift.DecAbs(cpu, s),
        0xDE => () => CpuInstructionsShift.DecAbsx(cpu, s),
        0xE8 => () => CpuInstructionsShift.Inx(cpu, s),
        0xC8 => () => CpuInstructionsShift.Iny(cpu, s),
        0xCA => () => CpuInstructionsShift.Dex(cpu, s),
        0x88 => () => CpuInstructionsShift.Dey(cpu, s),

        // Shifts & Rotates
        0x0A => () => CpuInstructionsShift.AslAcc(cpu, s),
        0x06 => () => CpuInstructionsShift.AslZp(cpu, s),
        0x16 => () => CpuInstructionsShift.AslZpx(cpu, s),
        0x0E => () => CpuInstructionsShift.AslAbs(cpu, s),
        0x1E => () => CpuInstructionsShift.AslAbsx(cpu, s),
        0x4A => () => CpuInstructionsShift.LsrAcc(cpu, s),
        0x46 => () => CpuInstructionsShift.LsrZp(cpu, s),
        0x56 => () => CpuInstructionsShift.LsrZpx(cpu, s),
        0x4E => () => CpuInstructionsShift.LsrAbs(cpu, s),
        0x5E => () => CpuInstructionsShift.LsrAbsx(cpu, s),
        0x2A => () => CpuInstructionsShift.RolAcc(cpu, s),
        0x26 => () => CpuInstructionsShift.RolZp(cpu, s),
        0x36 => () => CpuInstructionsShift.RolZpx(cpu, s),
        0x2E => () => CpuInstructionsShift.RolAbs(cpu, s),
        0x3E => () => CpuInstructionsShift.RolAbsx(cpu, s),
        0x6A => () => CpuInstructionsShift.RorAcc(cpu, s),
        0x66 => () => CpuInstructionsShift.RorZp(cpu, s),
        0x76 => () => CpuInstructionsShift.RorZpx(cpu, s),
        0x6E => () => CpuInstructionsShift.RorAbs(cpu, s),
        0x7E => () => CpuInstructionsShift.RorAbsx(cpu, s),

        _ => CpuInstructionTableOfficialPart3.Create(opcode, cpu, s, interrupts)
    };
}
