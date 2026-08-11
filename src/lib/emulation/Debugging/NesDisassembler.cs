using System;
using System.Collections.Generic;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// 6502 instruction disassembler.
/// </summary>
internal static class NesDisassembler
{
    internal static IReadOnlyList<DisassembledInstruction> Disassemble(
        NesSystem nes,
        ushort startAddress,
        int instructionCount)
    {
        if (instructionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(instructionCount));
        }

        var currentPc = nes.Cpu.CaptureDebugState().ProgramCounter;
        var address = startAddress;
        var result = new List<DisassembledInstruction>(instructionCount);
        for (var i = 0; i < instructionCount; i++)
        {
            var opcode = nes.CpuBus.Peek(address);
            var descriptor = CpuOpcodeTable.Get(opcode);
            if (descriptor == null)
            {
                result.Add(new DisassembledInstruction(
                    address, new byte[] { opcode }, ".db", $"${opcode:X2}",
                    CpuAddressingMode.Implied, address == currentPc));
                address++;
                continue;
            }

            var bytes = new byte[descriptor.Length];
            for (var byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
            {
                bytes[byteIndex] = nes.CpuBus.Peek((ushort)(address + byteIndex));
            }

            result.Add(new DisassembledInstruction(
                address, bytes, descriptor.Mnemonic,
                FormatOperand(descriptor.Mode, address, bytes),
                descriptor.Mode, address == currentPc));
            address = (ushort)(address + descriptor.Length);
        }

        return result.AsReadOnly();
    }

    private static string FormatOperand(CpuAddressingMode mode, ushort address, byte[] bytes)
    {
        var value = bytes.Length > 1 ? bytes[1] : (byte)0;
        var absolute = bytes.Length > 2 ? (ushort)(bytes[1] | (bytes[2] << 8)) : (ushort)0;
        return mode switch
        {
            CpuAddressingMode.Implied => string.Empty,
            CpuAddressingMode.Accumulator => "A",
            CpuAddressingMode.Immediate => $"#${value:X2}",
            CpuAddressingMode.ZeroPage => $"${value:X2}",
            CpuAddressingMode.ZeroPageX => $"${value:X2},X",
            CpuAddressingMode.ZeroPageY => $"${value:X2},Y",
            CpuAddressingMode.Relative => $"${(ushort)(address + 2 + unchecked((sbyte)value)):X4}",
            CpuAddressingMode.Absolute => $"${absolute:X4}",
            CpuAddressingMode.AbsoluteX => $"${absolute:X4},X",
            CpuAddressingMode.AbsoluteY => $"${absolute:X4},Y",
            CpuAddressingMode.Indirect => $"(${absolute:X4})",
            CpuAddressingMode.IndexedIndirect => $"(${value:X2},X)",
            CpuAddressingMode.IndirectIndexed => $"(${value:X2}),Y",
            _ => string.Empty
        };
    }
}
