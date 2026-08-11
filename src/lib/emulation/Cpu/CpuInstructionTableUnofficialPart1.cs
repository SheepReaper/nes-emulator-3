using System;

namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionTableUnofficialPart1
{
    internal static Action? Create(byte opcode, Cpu cpu, CpuState s) => opcode switch
    {
        // Unofficial immediate
        0xEB => () => CpuInstructionsMath.SbcImm(cpu, s),
        0x0B => () => CpuInstructionsUnofficial.AacImm(cpu, s),
        0x2B => () => CpuInstructionsUnofficial.AacImm(cpu, s),
        0x4B => () => CpuInstructionsUnofficial.AsrImm(cpu, s),
        0x6B => () => CpuInstructionsUnofficial.ArrImm(cpu, s),
        0xAB => () => CpuInstructionsUnofficial.AtxImm(cpu, s),
        0x8B => () => CpuInstructionsUnofficial.XaaImm(cpu, s),
        0x93 => () => CpuInstructionsUnofficial.AhxIndy(cpu, s),
        0x9B => () => CpuInstructionsUnofficial.TasAbsy(cpu, s),
        0x9F => () => CpuInstructionsUnofficial.AhxAbsy(cpu, s),
        0xBB => () => CpuInstructionsUnofficial.LasAbsy(cpu, s),
        0xCB => () => CpuInstructionsUnofficial.AxsImm(cpu, s),

        // Unofficial zero-page
        0x07 => () => CpuInstructionsCompound.SloZp(cpu, s),
        0x27 => () => CpuInstructionsCompound.RlaZp(cpu, s),
        0x47 => () => CpuInstructionsCompound.SreZp(cpu, s),
        0x67 => () => CpuInstructionsCompound.RraZp(cpu, s),
        0x87 => () => CpuInstructionsCompound.AaxZp(cpu, s),
        0xA7 => () => CpuInstructionsCompound.LaxZp(cpu, s),
        0xC7 => () => CpuInstructionsCompound.DcpZp(cpu, s),
        0xE7 => () => CpuInstructionsCompound.IscZp(cpu, s),

        // Unofficial zero-page indexed
        0x17 => () => CpuInstructionsCompound.SloZpx(cpu, s),
        0x37 => () => CpuInstructionsCompound.RlaZpx(cpu, s),
        0x57 => () => CpuInstructionsCompound.SreZpx(cpu, s),
        0x77 => () => CpuInstructionsCompound.RraZpx(cpu, s),
        0x97 => () => CpuInstructionsCompound.AaxZpy(cpu, s),
        0xB7 => () => CpuInstructionsCompound.LaxZpy(cpu, s),
        0xD7 => () => CpuInstructionsCompound.DcpZpx(cpu, s),
        0xF7 => () => CpuInstructionsCompound.IscZpx(cpu, s),

        // Unofficial absolute
        0x0F => () => CpuInstructionsCompound.SloAbs(cpu, s),
        0x2F => () => CpuInstructionsCompound.RlaAbs(cpu, s),
        0x4F => () => CpuInstructionsCompound.SreAbs(cpu, s),
        0x6F => () => CpuInstructionsCompound.RraAbs(cpu, s),
        0x8F => () => CpuInstructionsCompound.AaxAbs(s),
        0xAF => () => CpuInstructionsCompound.LaxAbs(cpu, s),
        0xCF => () => CpuInstructionsCompound.DcpAbs(cpu, s),
        0xEF => () => CpuInstructionsCompound.IscAbs(cpu, s),

        _ => CpuInstructionTableUnofficialPart2.Create(opcode, cpu, s)
    };
}
