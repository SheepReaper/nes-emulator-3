using System;

namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuInstructionTableOfficialPart3
{
    internal static Action? Create(byte opcode, Cpu cpu, CpuState s, InterruptLines interrupts) => opcode switch
    {
        // Compare and Bit Test
        0xC9 => () => CpuInstructionsMath.CmpImm(cpu, s),
        0xC5 => () => CpuInstructionsMath.CmpZp(cpu, s),
        0xD5 => () => CpuInstructionsMath.CmpZpx(cpu, s),
        0xCD => () => CpuInstructionsMath.CmpAbs(cpu, s),
        0xDD => () => CpuInstructionsMath.CmpAbsx(cpu, s),
        0xD9 => () => CpuInstructionsMath.CmpAbsy(cpu, s),
        0xC1 => () => CpuInstructionsMath.CmpIndx(cpu, s),
        0xD1 => () => CpuInstructionsMath.CmpIndy(cpu, s),
        0xE0 => () => CpuInstructionsMath.CpxImm(cpu, s),
        0xE4 => () => CpuInstructionsMath.CpxZp(cpu, s),
        0xEC => () => CpuInstructionsMath.CpxAbs(cpu, s),
        0xC0 => () => CpuInstructionsMath.CpyImm(cpu, s),
        0xC4 => () => CpuInstructionsMath.CpyZp(cpu, s),
        0xCC => () => CpuInstructionsMath.CpyAbs(cpu, s),
        0x24 => () => CpuInstructionsMath.BitZp(cpu, s),
        0x2C => () => CpuInstructionsMath.BitAbs(cpu, s),

        // Register Transfer
        0xAA => () => CpuInstructionsStore.Tax(cpu, s),
        0xA8 => () => CpuInstructionsStore.Tay(cpu, s),
        0x8A => () => CpuInstructionsStore.Txa(cpu, s),
        0x98 => () => CpuInstructionsStore.Tya(cpu, s),
        0xBA => () => CpuInstructionsStore.Tsx(cpu, s),
        0x9A => () => CpuInstructionsStore.Txs(cpu, s),

        // Flag Control
        0x18 => () => CpuInstructionsFlags.Clc(cpu, s),
        0x38 => () => CpuInstructionsFlags.Sec(cpu, s),
        0x58 => () => CpuInstructionsFlags.Cli(cpu, s),
        0x78 => () => CpuInstructionsFlags.Sei(cpu, s),
        0xB8 => () => CpuInstructionsFlags.Clv(cpu, s),
        0xD8 => () => CpuInstructionsFlags.Cld(cpu, s),
        0xF8 => () => CpuInstructionsFlags.Sed(cpu, s),

        // System & Stack
        0x00 => () => CpuInstructionsFlow.BrkImp(cpu, s),
        0x40 => () => CpuInstructionsFlow.RtiImp(cpu, s),
        0x4C => () => CpuInstructionsFlow.JmpAbs(cpu, s),
        0x6C => () => CpuInstructionsFlow.JmpInd(cpu, s),
        0x20 => () => CpuInstructionsFlow.JsrAbs(s),
        0x60 => () => CpuInstructionsFlow.RtsImp(s),
        0x48 => () => CpuInstructionsFlow.PhaImp(cpu, s),
        0x68 => () => CpuInstructionsFlow.PlaImp(s),
        0x08 => () => CpuInstructionsFlow.PhpImp(cpu, s),
        0x28 => () => CpuInstructionsFlow.PlpImp(s),

        // Branching
        0x10 => () => CpuInstructionsFlow.Branch(cpu, s, interrupts, !s.P.Negative),
        0x30 => () => CpuInstructionsFlow.Branch(cpu, s, interrupts, s.P.Negative),
        0x50 => () => CpuInstructionsFlow.Branch(cpu, s, interrupts, !s.P.Overflow),
        0x70 => () => CpuInstructionsFlow.Branch(cpu, s, interrupts, s.P.Overflow),
        0x90 => () => CpuInstructionsFlow.Branch(cpu, s, interrupts, !s.P.Carry),
        0xB0 => () => CpuInstructionsFlow.Branch(cpu, s, interrupts, s.P.Carry),
        0xD0 => () => CpuInstructionsFlow.Branch(cpu, s, interrupts, !s.P.Zero),
        0xF0 => () => CpuInstructionsFlow.Branch(cpu, s, interrupts, s.P.Zero),

        _ => null
    };
}
