using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuParametricReadCasesTests : CpuTestFixture
{
    [Theory]
    [MemberData(nameof(CpuTests.ReadInstructionAddressingCases), MemberType = typeof(CpuTests))]
    public void ReadInstructions_UseOperandFromEveryAddressingMode(
        byte opcode, string instruction, string addressingMode, ulong expectedCycles)
    {
        byte initialA;
        byte operand;
        byte expectedA;
        bool initialCarry = false;
        bool expectedZero = false;
        bool expectedNegative = false;
        bool? expectedCarry = null;

        switch (instruction)
        {
            case "LDA": initialA = 0; operand = 0x80; expectedA = 0x80; expectedNegative = true; break;
            case "AND": initialA = 0xF0; operand = 0x0F; expectedA = 0x00; expectedZero = true; break;
            case "EOR": initialA = 0xF0; operand = 0x0F; expectedA = 0xFF; expectedNegative = true; break;
            case "ORA": initialA = 0x80; operand = 0x01; expectedA = 0x81; expectedNegative = true; break;
            case "ADC": initialA = 0x10; operand = 0x20; expectedA = 0x30; expectedCarry = false; break;
            case "SBC": initialA = 0x20; operand = 0x10; expectedA = 0x10; initialCarry = true; expectedCarry = true; break;
            case "CMP": initialA = 0x20; operand = 0x10; expectedA = 0x20; expectedCarry = true; break;
            default: throw new ArgumentOutOfRangeException(nameof(instruction));
        }

        SetPc(0x8000);
        SetA(initialA);
        SetX(1);
        SetY(1);
        SetP(0);
        SetFlag('C', initialCarry);
        LoadReadOperand(opcode, addressingMode, operand);

        Assert.Equal(expectedCycles, Cpu.Step());
        Assert.Equal(expectedA, GetA());
        Assert.Equal(expectedZero, GetFlag('Z'));
        Assert.Equal(expectedNegative, GetFlag('N'));
        if (expectedCarry.HasValue)
        {
            Assert.Equal(expectedCarry.Value, GetFlag('C'));
        }
    }

    private void LoadReadOperand(byte opcode, string addressingMode, byte operand)
    {
        switch (addressingMode)
        {
            case "Immediate":
                Bus.Load(0x8000, [opcode, operand]);
                break;
            case "ZeroPage":
                Bus.Load(0x8000, [opcode, 0x42]);
                Bus.Write(0x0042, operand);
                break;
            case "ZeroPageX":
                Bus.Load(0x8000, [opcode, 0x41]);
                Bus.Write(0x0042, operand);
                break;
            case "Absolute":
                Bus.Load(0x8000, [opcode, 0x34, 0x12]);
                Bus.Write(0x1234, operand);
                break;
            case "AbsoluteX":
            case "AbsoluteY":
                Bus.Load(0x8000, [opcode, 0x33, 0x12]);
                Bus.Write(0x1234, operand);
                break;
            case "IndirectX":
                Bus.Load(0x8000, [opcode, 0x41]);
                Bus.Write(0x0042, 0x34);
                Bus.Write(0x0043, 0x12);
                Bus.Write(0x1234, operand);
                break;
            case "IndirectY":
                Bus.Load(0x8000, [opcode, 0x40]);
                Bus.Write(0x0040, 0x33);
                Bus.Write(0x0041, 0x12);
                Bus.Write(0x1234, operand);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(addressingMode));
        }
    }
}
