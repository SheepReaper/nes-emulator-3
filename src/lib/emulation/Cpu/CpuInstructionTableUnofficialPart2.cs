using System;

namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionTableUnofficialPart2
{
    internal static Action? Create(byte opcode, Cpu cpu, CpuState s) => opcode switch
    {
        // Unofficial absolute indexed
        0x1F => () => CpuInstructionsCompound.SloAbsx(cpu, s),
        0x3F => () => CpuInstructionsCompound.RlaAbsx(cpu, s),
        0x5F => () => CpuInstructionsCompound.SreAbsx(cpu, s),
        0x7F => () => CpuInstructionsCompound.RraAbsx(cpu, s),
        0x9C => () => CpuInstructionsUnofficial.SyaAbsx(cpu, s),
        0xDF => () => CpuInstructionsCompound.DcpAbsx(cpu, s),
        0xFF => () => CpuInstructionsCompound.IscAbsx(cpu, s),
        0x1B => () => CpuInstructionsCompound.SloAbsy(cpu, s),
        0x3B => () => CpuInstructionsCompound.RlaAbsy(cpu, s),
        0x5B => () => CpuInstructionsCompound.SreAbsy(cpu, s),
        0x7B => () => CpuInstructionsCompound.RraAbsy(cpu, s),
        0x9E => () => CpuInstructionsUnofficial.SxaAbsy(cpu, s),
        0xBF => () => CpuInstructionsCompound.LaxAbsy(cpu, s),
        0xDB => () => CpuInstructionsCompound.DcpAbsy(cpu, s),
        0xFB => () => CpuInstructionsCompound.IscAbsy(cpu, s),

        // Unofficial indexed-indirect
        0x03 => () => CpuInstructionsCompound.SloIndx(cpu, s),
        0x23 => () => CpuInstructionsCompound.RlaIndx(cpu, s),
        0x43 => () => CpuInstructionsCompound.SreIndx(cpu, s),
        0x63 => () => CpuInstructionsCompound.RraIndx(cpu, s),
        0x83 => () => CpuInstructionsCompound.AaxIndx(cpu, s),
        0xA3 => () => CpuInstructionsCompound.LaxIndx(cpu, s),
        0xC3 => () => CpuInstructionsCompound.DcpIndx(cpu, s),
        0xE3 => () => CpuInstructionsCompound.IscIndx(cpu, s),

        // Unofficial indirect-indexed
        0x13 => () => CpuInstructionsCompound.SloIndy(cpu, s),
        0x33 => () => CpuInstructionsCompound.RlaIndy(cpu, s),
        0x53 => () => CpuInstructionsCompound.SreIndy(cpu, s),
        0x73 => () => CpuInstructionsCompound.RraIndy(cpu, s),
        0xB3 => () => CpuInstructionsCompound.LaxIndy(cpu, s),
        0xD3 => () => CpuInstructionsCompound.DcpIndy(cpu, s),
        0xF3 => () => CpuInstructionsCompound.IscIndy(cpu, s),

        // Unofficial NOPs
        0x1A => () => { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { }); s.Cycles = 2; },
        0x3A => () => { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { }); s.Cycles = 2; },
        0x5A => () => { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { }); s.Cycles = 2; },
        0x7A => () => { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { }); s.Cycles = 2; },
        0xDA => () => { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { }); s.Cycles = 2; },
        0xFA => () => { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { }); s.Cycles = 2; },
        0x04 => () => CpuInstructionsFlags.NopZp(cpu, s),
        0x44 => () => CpuInstructionsFlags.NopZp(cpu, s),
        0x64 => () => CpuInstructionsFlags.NopZp(cpu, s),
        0x0C => () => cpu.StartAbsoluteRead(_ => { }),
        0x14 => () => CpuInstructionsFlags.NopZpx(cpu, s),
        0x34 => () => CpuInstructionsFlags.NopZpx(cpu, s),
        0x54 => () => CpuInstructionsFlags.NopZpx(cpu, s),
        0x74 => () => CpuInstructionsFlags.NopZpx(cpu, s),
        0xD4 => () => CpuInstructionsFlags.NopZpx(cpu, s),
        0xF4 => () => CpuInstructionsFlags.NopZpx(cpu, s),
        0x80 => () => CpuInstructionsFlags.NopImm(cpu, s),
        0x82 => () => CpuInstructionsFlags.NopImm(cpu, s),
        0x89 => () => CpuInstructionsFlags.NopImm(cpu, s),
        0xC2 => () => CpuInstructionsFlags.NopImm(cpu, s),
        0xE2 => () => CpuInstructionsFlags.NopImm(cpu, s),
        0x1C => () => CpuInstructionsFlags.NopAbsx(cpu, s),
        0x3C => () => CpuInstructionsFlags.NopAbsx(cpu, s),
        0x5C => () => CpuInstructionsFlags.NopAbsx(cpu, s),
        0x7C => () => CpuInstructionsFlags.NopAbsx(cpu, s),
        0xDC => () => CpuInstructionsFlags.NopAbsx(cpu, s),
        0xFC => () => CpuInstructionsFlags.NopAbsx(cpu, s),

        _ => () => { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { }); s.Cycles = 2; }
    };
}
