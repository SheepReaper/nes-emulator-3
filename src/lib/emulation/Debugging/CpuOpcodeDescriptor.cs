namespace Sheep.Emulation.Nes.Debugging;

internal sealed class CpuOpcodeDescriptor(
    byte opcode, string mnemonic, CpuAddressingMode mode, int length, int cycles, bool isOfficial)
{
    public byte Opcode { get; } = opcode;
    public string Mnemonic { get; } = mnemonic;
    public CpuAddressingMode Mode { get; } = mode;
    public int Length { get; } = length;
    public int Cycles { get; } = cycles;
    public bool IsOfficial { get; } = isOfficial;
}