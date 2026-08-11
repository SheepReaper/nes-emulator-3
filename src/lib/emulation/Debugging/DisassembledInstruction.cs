using System;
namespace Sheep.Emulation.Nes.Debugging;

public sealed class DisassembledInstruction(
    ushort address, ReadOnlyMemory<byte> bytes, string mnemonic, string operand,
    CpuAddressingMode addressingMode, bool isCurrent)
{
    public ushort Address { get; } = address;
    public ReadOnlyMemory<byte> Bytes { get; } = bytes;
    public string Mnemonic { get; } = mnemonic;
    public string Operand { get; } = operand;
    public CpuAddressingMode AddressingMode { get; } = addressingMode;
    public int Length => Bytes.Length;
    public bool IsCurrent { get; } = isCurrent;
}