using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuParametricRmwAndStoreTests : CpuTestFixture
{
    [Theory]
    [MemberData(nameof(CpuTests.MemoryRmwCases), MemberType = typeof(CpuTests))]
    public void MemoryReadModifyWriteInstructions_UpdateMemoryFlagsAndCycles(
        byte opcode, string instruction, string addressingMode, ulong expectedCycles)
    {
        byte operand;
        byte expected;
        bool initialCarry = false;
        bool expectedCarry = false;
        bool expectedZero = false;
        bool expectedNegative = false;

        switch (instruction)
        {
            case "INC": operand = 0x7F; expected = 0x80; expectedNegative = true; break;
            case "DEC": operand = 0x01; expected = 0x00; expectedZero = true; break;
            case "ASL": operand = 0x40; expected = 0x80; expectedNegative = true; break;
            case "LSR": operand = 0x01; expected = 0x00; expectedCarry = true; expectedZero = true; break;
            case "ROL": operand = 0x40; expected = 0x81; initialCarry = true; expectedNegative = true; break;
            case "ROR": operand = 0x02; expected = 0x81; initialCarry = true; expectedNegative = true; break;
            default: throw new ArgumentOutOfRangeException(nameof(instruction));
        }

        SetPc(0x8000);
        SetX(1);
        SetP(0);
        SetFlag('C', initialCarry);
        ushort effectiveAddress = addressingMode switch
        {
            "ZeroPage" => 0x0042,
            "ZeroPageX" => 0x0042,
            "Absolute" => 0x1234,
            "AbsoluteX" => 0x1234,
            _ => throw new ArgumentOutOfRangeException(nameof(addressingMode))
        };
        switch (addressingMode)
        {
            case "ZeroPage": Bus.Load(0x8000, [opcode, 0x42]); break;
            case "ZeroPageX": Bus.Load(0x8000, [opcode, 0x41]); break;
            case "Absolute": Bus.Load(0x8000, [opcode, 0x34, 0x12]); break;
            case "AbsoluteX": Bus.Load(0x8000, [opcode, 0x33, 0x12]); break;
        }
        Bus.Write(effectiveAddress, operand);

        Assert.Equal(expectedCycles, Cpu.Step());
        Assert.Equal(expected, Bus.Read(effectiveAddress));
        Assert.Equal(expectedCarry, GetFlag('C'));
        Assert.Equal(expectedZero, GetFlag('Z'));
        Assert.Equal(expectedNegative, GetFlag('N'));
    }

    [Theory]
    [MemberData(nameof(CpuTests.StoreAddressingCases), MemberType = typeof(CpuTests))]
    public void StoreInstructions_WriteEveryAddressingVariantWithoutChangingFlags(
        byte opcode, char register, string addressingMode, ulong expectedCycles)
    {
        const byte value = 0xA5;
        SetPc(0x8000);
        SetX(register == 'X' ? value : (byte)1);
        SetY(register == 'Y' ? value : (byte)1);
        SetA(value);
        SetP(0xE3);
        ushort effectiveAddress;

        switch (addressingMode)
        {
            case "ZeroPage": effectiveAddress = 0x0042; Bus.Load(0x8000, [opcode, 0x42]); break;
            case "ZeroPageX": effectiveAddress = 0x0042; Bus.Load(0x8000, [opcode, 0x41]); break;
            case "ZeroPageY": effectiveAddress = 0x0042; Bus.Load(0x8000, [opcode, 0x41]); break;
            case "Absolute": effectiveAddress = 0x1234; Bus.Load(0x8000, [opcode, 0x34, 0x12]); break;
            case "AbsoluteX": effectiveAddress = 0x1234; Bus.Load(0x8000, [opcode, 0x33, 0x12]); break;
            case "AbsoluteY": effectiveAddress = 0x1234; Bus.Load(0x8000, [opcode, 0x33, 0x12]); break;
            case "IndirectX":
                effectiveAddress = 0x1234;
                Bus.Load(0x8000, [opcode, 0x41]);
                Bus.Write(0x0042, 0x34); Bus.Write(0x0043, 0x12);
                break;
            case "IndirectY":
                effectiveAddress = 0x1234;
                Bus.Load(0x8000, [opcode, 0x40]);
                Bus.Write(0x0040, 0x33); Bus.Write(0x0041, 0x12);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(addressingMode));
        }

        Assert.Equal(expectedCycles, Cpu.Step());
        Assert.Equal(value, Bus.Read(effectiveAddress));
        Assert.Equal(0xE3, GetP());
    }
}
