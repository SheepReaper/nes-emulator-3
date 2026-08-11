using System;

namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionTableOfficial
{
    internal static Action? CreateOfficial(byte opcode, Cpu cpu, CpuState s, InterruptLines interrupts) => opcode switch
    {
        0xEA => () => { cpu.CompleteMemoryRead(s.ProgramCounter, _ => { }); s.Cycles = 2; },

        // LDA
        0xA9 => () => CpuInstructionsLogic.LdaImm(cpu, s),
        0xA5 => () => CpuInstructionsLogic.LdaZp(cpu, s),
        0xB5 => () => CpuInstructionsLogic.LdaZpx(cpu, s),
        0xAD => () => CpuInstructionsLogic.LdaAbs(cpu, s),
        0xBD => () => CpuInstructionsLogic.LdaAbsx(cpu, s),
        0xB9 => () => CpuInstructionsLogic.LdaAbsy(cpu, s),
        0xA1 => () => CpuInstructionsLogic.LdaIndx(cpu, s),
        0xB1 => () => CpuInstructionsLogic.LdaIndy(cpu, s),

        // LDX
        0xA2 => () => CpuInstructionsLogic.LdxImm(cpu, s),
        0xA6 => () => CpuInstructionsLogic.LdxZp(cpu, s),
        0xB6 => () => CpuInstructionsLogic.LdxZpy(cpu, s),
        0xAE => () => CpuInstructionsLogic.LdxAbs(cpu, s),
        0xBE => () => CpuInstructionsLogic.LdxAbsy(cpu, s),

        // LDY
        0xA0 => () => CpuInstructionsLogic.LdyImm(cpu, s),
        0xA4 => () => CpuInstructionsLogic.LdyZp(cpu, s),
        0xB4 => () => CpuInstructionsLogic.LdyZpx(cpu, s),
        0xAC => () => CpuInstructionsLogic.LdyAbs(cpu, s),
        0xBC => () => CpuInstructionsLogic.LdyAbsx(cpu, s),

        // AND
        0x29 => () => CpuInstructionsLogic.AndImm(cpu, s),
        0x25 => () => CpuInstructionsLogic.AndZp(cpu, s),
        0x35 => () => CpuInstructionsLogic.AndZpx(cpu, s),
        0x2D => () => CpuInstructionsLogic.AndAbs(cpu, s),
        0x3D => () => CpuInstructionsLogic.AndAbsx(cpu, s),
        0x39 => () => CpuInstructionsLogic.AndAbsy(cpu, s),
        0x21 => () => CpuInstructionsLogic.AndIndx(cpu, s),
        0x31 => () => CpuInstructionsLogic.AndIndy(cpu, s),

        // EOR
        0x49 => () => CpuInstructionsLogic.EorImm(cpu, s),
        0x45 => () => CpuInstructionsLogic.EorZp(cpu, s),
        0x55 => () => CpuInstructionsLogic.EorZpx(cpu, s),
        0x4D => () => CpuInstructionsLogic.EorAbs(cpu, s),
        0x5D => () => CpuInstructionsLogic.EorAbsx(cpu, s),
        0x59 => () => CpuInstructionsLogic.EorAbsy(cpu, s),
        0x41 => () => CpuInstructionsLogic.EorIndx(cpu, s),
        0x51 => () => CpuInstructionsLogic.EorIndy(cpu, s),

        // ORA
        0x09 => () => CpuInstructionsLogic.OraImm(cpu, s),
        0x05 => () => CpuInstructionsLogic.OraZp(cpu, s),
        0x15 => () => CpuInstructionsLogic.OraZpx(cpu, s),
        0x0D => () => CpuInstructionsLogic.OraAbs(cpu, s),
        0x1D => () => CpuInstructionsLogic.OraAbsx(cpu, s),
        0x19 => () => CpuInstructionsLogic.OraAbsy(cpu, s),
        0x01 => () => CpuInstructionsLogic.OraIndx(cpu, s),
        0x11 => () => CpuInstructionsLogic.OraIndy(cpu, s),

        // ADC
        0x69 => () => CpuInstructionsMath.AdcImm(cpu, s),
        0x65 => () => CpuInstructionsMath.AdcZp(cpu, s),
        0x75 => () => CpuInstructionsMath.AdcZpx(cpu, s),
        0x6D => () => CpuInstructionsMath.AdcAbs(cpu, s),
        0x7D => () => CpuInstructionsMath.AdcAbsx(cpu, s),
        0x79 => () => CpuInstructionsMath.AdcAbsy(cpu, s),
        0x61 => () => CpuInstructionsMath.AdcIndx(cpu, s),
        0x71 => () => CpuInstructionsMath.AdcIndy(cpu, s),

        // SBC
        0xE9 => () => CpuInstructionsMath.SbcImm(cpu, s),
        0xE5 => () => CpuInstructionsMath.SbcZp(cpu, s),
        0xF5 => () => CpuInstructionsMath.SbcZpx(cpu, s),
        0xED => () => CpuInstructionsMath.SbcAbs(cpu, s),
        0xFD => () => CpuInstructionsMath.SbcAbsx(cpu, s),
        0xF9 => () => CpuInstructionsMath.SbcAbsy(cpu, s),
        0xE1 => () => CpuInstructionsMath.SbcIndx(cpu, s),
        0xF1 => () => CpuInstructionsMath.SbcIndy(cpu, s),

        _ => CpuInstructionTableOfficialPart2.Create(opcode, cpu, s, interrupts)
    };
}
