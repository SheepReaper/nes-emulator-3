using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public static class CpuInstructionAddressingData
{
    public static TheoryData<byte, string, bool, ulong> IndexedReadCycleCases()
    {
        var cases = new TheoryData<byte, string, bool, ulong>();
        byte[] absoluteX = [0xBD, 0x3D, 0x5D, 0x1D, 0x7D, 0xFD, 0xDD];
        byte[] absoluteY = [0xB9, 0x39, 0x59, 0x19, 0x79, 0xF9, 0xD9];
        byte[] indirectY = [0xB1, 0x31, 0x51, 0x11, 0x71, 0xF1, 0xD1];

        foreach (var opcode in absoluteX)
        {
            cases.Add(opcode, "AbsoluteX", false, 4);
            cases.Add(opcode, "AbsoluteX", true, 5);
        }
        foreach (var opcode in absoluteY)
        {
            cases.Add(opcode, "AbsoluteY", false, 4);
            cases.Add(opcode, "AbsoluteY", true, 5);
        }
        foreach (var opcode in indirectY)
        {
            cases.Add(opcode, "IndirectY", false, 5);
            cases.Add(opcode, "IndirectY", true, 6);
        }
        return cases;
    }

    public static TheoryData<byte, string, string, ulong> ReadInstructionAddressingCases()
    {
        var cases = new TheoryData<byte, string, string, ulong>();
        AddReadFamily(cases, "LDA", [0xA9, 0xA5, 0xB5, 0xAD, 0xBD, 0xB9, 0xA1, 0xB1]);
        AddReadFamily(cases, "AND", [0x29, 0x25, 0x35, 0x2D, 0x3D, 0x39, 0x21, 0x31]);
        AddReadFamily(cases, "EOR", [0x49, 0x45, 0x55, 0x4D, 0x5D, 0x59, 0x41, 0x51]);
        AddReadFamily(cases, "ORA", [0x09, 0x05, 0x15, 0x0D, 0x1D, 0x19, 0x01, 0x11]);
        AddReadFamily(cases, "ADC", [0x69, 0x65, 0x75, 0x6D, 0x7D, 0x79, 0x61, 0x71]);
        AddReadFamily(cases, "SBC", [0xE9, 0xE5, 0xF5, 0xED, 0xFD, 0xF9, 0xE1, 0xF1]);
        AddReadFamily(cases, "CMP", [0xC9, 0xC5, 0xD5, 0xCD, 0xDD, 0xD9, 0xC1, 0xD1]);
        return cases;
    }

    private static void AddReadFamily(
        TheoryData<byte, string, string, ulong> cases, string instruction, byte[] opcodes)
    {
        string[] modes = ["Immediate", "ZeroPage", "ZeroPageX", "Absolute", "AbsoluteX", "AbsoluteY", "IndirectX", "IndirectY"];
        ulong[] cycles = [2, 3, 4, 4, 4, 4, 6, 5];
        for (var i = 0; i < opcodes.Length; i++)
        {
            cases.Add(opcodes[i], instruction, modes[i], cycles[i]);
        }
    }
}
