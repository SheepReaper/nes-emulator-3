using System.Reflection;

using SR.Emulation.Nes.Abtractions;

using Xunit;

namespace SR.Emulation.Nes.Tests;

public class CpuTests
{
    /// <summary>
    /// A simple mock bus for testing the CPU in isolation.
    /// It uses a simple byte array as its addressable memory.
    /// </summary>
    private class MockBus : IBus
    {
        private readonly byte[] _memory = new byte[0x10000];

        public byte Read(ushort address) => _memory[address];

        public void Write(ushort address, byte value) => _memory[address] = value;

        public void Load(ushort address, byte[] data)
        {
            Array.Copy(data, 0, _memory, address, data.Length);
        }
    }

    private readonly InterruptLines _interrupts = new();
    private readonly MockBus _bus = new();
    private readonly Cpu _cpu;

    public CpuTests()
    {
        _cpu = new Cpu(_interrupts);
        _cpu.ConnectBus(_bus);
    }

    [Fact]
    public void Reset_SetsInitialStateCorrectly()
    {
        // Arrange: Set the reset vector to point to 0x8000
        _bus.Write(0xFFFC, 0x00);
        _bus.Write(0xFFFD, 0x80);

        // Act
        _cpu.Reset();

        // Assert
        Assert.Equal(0x8000, GetPc());
        Assert.Equal(0x00, GetA());
        Assert.Equal(0x00, GetX());
        Assert.Equal(0x00, GetY());
        Assert.Equal(0xFD, GetSp());
        Assert.Equal(0b0010_0100, GetP()); // I flag and unused bit 5 set
        Assert.Equal(8, GetCycles());
    }

    [Fact]
    public void LDA_Immediate_LoadsAccumulatorAndSetsFlags()
    {
        // Arrange: LDA #$42
        _bus.Load(0x8000, [0xA9, 0x42]);
        SetPc(0x8000);
        SetP(0); // Clear flags

        // Act
        _cpu.Clock(0); // Cycle 1: Fetch opcode
        _cpu.Clock(1); // Cycle 2: Fetch operand, execute

        // Assert
        Assert.Equal(0x42, GetA());
        Assert.False(GetFlag('Z')); // Not zero
        Assert.False(GetFlag('N')); // Not negative
    }

    [Fact]
    public void LDA_Immediate_SetsZeroFlag()
    {
        // Arrange: LDA #$00
        _bus.Load(0x8000, [0xA9, 0x00]);
        SetPc(0x8000);
        SetP(0);

        // Act
        _cpu.Clock(0);
        _cpu.Clock(1);

        // Assert
        Assert.Equal(0x00, GetA());
        Assert.True(GetFlag('Z')); // Is zero
        Assert.False(GetFlag('N'));
    }

    [Fact]
    public void PHA_PushesAccumulatorToStack()
    {
        // Arrange: PHA
        _bus.Load(0x8000, [0x48]);
        SetPc(0x8000);
        SetA(0x42);
        SetSp(0xFD);

        // Act
        Clock(3); // PHA takes 3 cycles

        // Assert
        Assert.Equal(0xFC, GetSp()); // Stack pointer decrements
        Assert.Equal(0x42, _bus.Read(0x01FD)); // Accumulator value pushed to stack
    }

    [Fact]
    public void PLA_PullsValueFromStackAndSetsFlags()
    {
        // Arrange: PLA
        _bus.Load(0x8000, [0x68]);
        _bus.Write(0x01FD, 0x8F); // Pre-load stack with a negative value
        SetPc(0x8000);
        SetSp(0xFC); // SP points to the "empty" slot before the value
        SetP(0);

        // Act
        Clock(4); // PLA takes 4 cycles

        // Assert
        Assert.Equal(0xFD, GetSp()); // Stack pointer increments
        Assert.Equal(0x8F, GetA());
        Assert.True(GetFlag('N')); // Negative flag should be set
        Assert.False(GetFlag('Z')); // Zero flag should not be set
    }

    [Fact]
    public void JSR_RTS_SubroutineCallAndReturn()
    {
        // Arrange:
        // At 0x8000: JSR $9000
        _bus.Load(0x8000, [0x20, 0x00, 0x90]);
        // At 0x9000: RTS
        _bus.Load(0x9000, [0x60]);

        SetPc(0x8000);
        SetSp(0xFD);

        // Act: Execute JSR
        Clock(6); // JSR takes 6 cycles

        // Assert after JSR
        Assert.Equal(0x9000, GetPc()); // PC is at the start of the subroutine
        Assert.Equal(0xFB, GetSp());   // Stack pointer has been decremented twice

        // The return address is PC-1 of the instruction *after* JSR.
        // JSR is 3 bytes, so next instruction is at 0x8003. PC-1 is 0x8002.
        // Pushed as hi, lo: $80, $02
        Assert.Equal(0x80, _bus.Read(0x01FD)); // High byte of return address
        Assert.Equal(0x02, _bus.Read(0x01FC)); // Low byte of return address

        // Act: Execute RTS
        Clock(6); // RTS takes 6 cycles

        // Assert after RTS
        // RTS pulls PC and adds 1. It should return to 0x8002 + 1 = 0x8003.
        Assert.Equal(0x8003, GetPc());
        Assert.Equal(0xFD, GetSp()); // Stack pointer is back to its original state
    }

    [Theory]
    // Branch not taken
    [InlineData(0xF0, 'Z', false, 0x8000, 0x05, 2, 0x8002)] // BEQ not taken
    // Branch taken, no page cross
    [InlineData(0xF0, 'Z', true, 0x8000, 0x05, 3, 0x8007)] // BEQ taken, forward
    [InlineData(0xD0, 'Z', false, 0x8005, -5, 3, 0x8002)] // BNE taken, backward
    // Branch taken, with page cross
    [InlineData(0xF0, 'Z', true, 0x80FE, 0x05, 4, 0x8105)] // BEQ taken, forward across page
    [InlineData(0xD0, 'Z', false, 0x8102, -5, 4, 0x80FF)] // BNE taken, backward across page
    public void BranchInstructions_HaveCorrectBehaviorAndCycles(
        byte opcode, char flag, bool flagValue, ushort startPc, int offset, int expectedCycles, ushort expectedPc)
    {
        // Arrange
        _bus.Load(startPc, [opcode, (byte)offset]);
        SetPc(startPc);
        SetP(0); // Clear all flags first
        SetFlag(flag, flagValue);

        // Act
        Clock(expectedCycles);

        // Assert
        Assert.Equal(0, GetCycles());
        Assert.Equal(expectedPc, GetPc());
    }

    [Theory]
    // ADC Tests
    [InlineData(0x69, 0x10, 0x20, false, 0x30, false, false, false, false)] // ADC: 16 + 32 = 48
    [InlineData(0x69, 0x00, 0x00, false, 0x00, true, false, false, false)]  // ADC: 0 + 0 = 0 (Z)
    [InlineData(0x69, 0xFF, 0x01, false, 0x00, true, true, false, false)]   // ADC: 255 + 1 = 0 (Z, C)
    [InlineData(0x69, 0x7F, 0x01, false, 0x80, false, false, true, true)]   // ADC: 127 + 1 = 128 (N, V)
    [InlineData(0x69, 0x80, 0xFF, false, 0x7F, false, true, true, false)]   // ADC: -128 + -1 = 127 (C, V)
    [InlineData(0x69, 0x10, 0x20, true, 0x31, false, false, false, false)]  // ADC with carry: 16 + 32 + 1 = 49

    // SBC Tests (SBC is ADC of one's complement, with Carry set for no borrow)
    [InlineData(0xE9, 0x05, 0x03, true, 0x02, false, true, false, false)]  // SBC: 5 - 3 = 2
    [InlineData(0xE9, 0x03, 0x05, true, 0xFE, false, false, false, true)]  // SBC: 3 - 5 = -2 (N, no C)
    [InlineData(0xE9, 0x00, 0x01, true, 0xFF, false, false, false, true)]  // SBC: 0 - 1 = -1 (N, no C)
    [InlineData(0xE9, 0x80, 0x01, false, 0x7E, false, true, true, false)]  // SBC with borrow: -128 - 1 - 1 = 126 (V)
    public void ArithmeticInstructions_SetFlagsCorrectly(
        byte opcode, byte initialA, byte operand, bool initialCarry,
        byte expectedA, bool expectedZ, bool expectedC, bool expectedV, bool expectedN)
    {
        // Arrange
        _bus.Load(0x8000, [opcode, operand]);
        SetPc(0x8000);
        SetA(initialA);
        SetP(0);
        SetFlag('C', initialCarry);

        // Act
        Clock(2); // All immediate arithmetic instructions take 2 cycles

        // Assert
        Assert.Equal(expectedA, GetA());
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedC, GetFlag('C'));
        Assert.Equal(expectedV, GetFlag('V'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Theory]
    // Register Increment/Decrement
    [InlineData(0xE8, 'X', 0x41, 0x42, false, false)] // INX
    [InlineData(0xE8, 'X', 0xFF, 0x00, true, false)]  // INX, wraps to 0, sets Z
    [InlineData(0xCA, 'X', 0x00, 0xFF, false, true)]  // DEX, wraps to 255, sets N
    [InlineData(0xC8, 'Y', 0x80, 0x81, false, true)]  // INY, result is negative
    [InlineData(0x88, 'Y', 0x01, 0x00, true, false)]  // DEY, result is 0, sets Z
    public void IncrementDecrementRegister_SetsFlagsCorrectly(
        byte opcode, char register, byte initialValue, byte expectedValue, bool expectedZ, bool expectedN)
    {
        // Arrange
        _bus.Load(0x8000, [opcode]);
        SetPc(0x8000);
        SetP(0);
        if (register == 'X') SetX(initialValue);
        else SetY(initialValue);

        // Act
        Clock(2); // All implied inc/dec instructions take 2 cycles

        // Assert
        var finalValue = register == 'X' ? GetX() : GetY();
        Assert.Equal(expectedValue, finalValue);
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Theory]
    [InlineData(0xE6, 0x41, 0x42, 5, false, false)] // INC ZP
    [InlineData(0xC6, 0x00, 0xFF, 5, false, true)]  // DEC ZP, wraps, sets N
    public void IncrementDecrementMemory_SetsFlagsCorrectly(
        byte opcode, byte initialValue, byte expectedValue, int expectedCycles, bool expectedZ, bool expectedN)
    {
        // Arrange
        _bus.Load(0x8000, [opcode, 0x42]); // Instruction at 0x8000, operand points to 0x42
        _bus.Write(0x0042, initialValue);
        SetPc(0x8000);
        SetP(0);

        // Act
        Clock(expectedCycles);

        // Assert
        Assert.Equal(expectedValue, _bus.Read(0x0042));
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Theory]
    // CMP, CPX, CPY
    [InlineData(0xC9, 'A', 0x42, 0x42, true, true, false)]   // CMP: A == M -> Z=1, C=1
    [InlineData(0xC9, 'A', 0x43, 0x42, false, true, false)]  // CMP: A > M  -> Z=0, C=1
    [InlineData(0xC9, 'A', 0x42, 0x43, false, false, true)]   // CMP: A < M  -> Z=0, C=0, N=1 (result is 0xFF)
    [InlineData(0xE0, 'X', 0x80, 0x80, true, true, false)]   // CPX: X == M
    [InlineData(0xC0, 'Y', 0x10, 0x20, false, false, true)]   // CPY: Y < M
    public void CompareInstructions_SetFlagsCorrectly(
        byte opcode, char register, byte registerValue, byte operand,
        bool expectedZ, bool expectedC, bool expectedN)
    {
        // Arrange
        _bus.Load(0x8000, [opcode, operand]);
        SetPc(0x8000);
        SetP(0);
        switch (register)
        {
            case 'A': SetA(registerValue); break;
            case 'X': SetX(registerValue); break;
            case 'Y': SetY(registerValue); break;
        }

        // Act
        Clock(2); // All immediate compare instructions take 2 cycles

        // Assert
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedC, GetFlag('C'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Theory]
    [InlineData(0xAA, 0x55, true, true, false)]   // A(10101010) & M(01010101) == 0 -> Z=1. M's bit 6 is 1 -> V=1. M's bit 7 is 0 -> N=0.
    [InlineData(0x55, 0xAA, true, false, true)]   // A(01010101) & M(10101010) == 0 -> Z=1. M's bit 6 is 0 -> V=0. M's bit 7 is 1 -> N=1.
    [InlineData(0x0F, 0xF0, true, true, true)]    // A(00001111) & M(11110000) == 0 -> Z=1. M's bit 6 is 1 -> V=1. M's bit 7 is 1 -> N=1.
    [InlineData(0xFF, 0x01, false, false, false)] // A(11111111) & M(00000001) != 0 -> Z=0. M's bits 6,7 are 0 -> V=0, N=0.
    public void BitInstruction_SetsFlagsCorrectly(byte initialA, byte operand, bool expectedZ, bool expectedV, bool expectedN)
    {
        // Arrange
        _bus.Load(0x8000, [0x24, 0x42]); // BIT $42
        _bus.Write(0x0042, operand);
        SetPc(0x8000);
        SetA(initialA);

        // Act
        Clock(3); // BIT ZP takes 3 cycles

        // Assert
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedV, GetFlag('V'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    // Helper to clock the CPU a specific number of times
    private void Clock(int cycles)
    {
        for (var i = 0; i < cycles; i++) _cpu.Clock((ulong)i);
    }

    // Helper methods to access private fields via reflection for testing
    private T GetField<T>(string name) => (T)typeof(Cpu).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_cpu)!;
    private void SetField<T>(string name, T value) => typeof(Cpu).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_cpu, value);

    private ushort GetPc() => GetField<ushort>("_pc");
    private void SetPc(ushort value) => SetField("_pc", value);
    private byte GetA() => GetField<byte>("_a");
    private void SetA(byte value) => SetField("_a", value);
    private byte GetX() => GetField<byte>("_x");
    private byte GetY() => GetField<byte>("_y");
    private void SetX(byte value) => SetField("_x", value);
    private void SetY(byte value) => SetField("_y", value);
    private byte GetSp() => GetField<byte>("_sp");
    private void SetSp(byte value) => SetField("_sp", value);
    private byte GetP() => GetField<ProcessorStatus>("_p").Value;
    private void SetP(byte value) => SetField("_p", new ProcessorStatus { Value = value });
    private byte GetCycles() => GetField<byte>("_cycles");
    private bool GetFlag(char flag)
    {
        var p = GetField<ProcessorStatus>("_p");
        return flag switch
        {
            'N' => p.Negative,
            'Z' => p.Zero,
            'C' => p.Carry,
            'V' => p.Overflow,
            _ => throw new ArgumentException("Invalid flag specified.")
        };
    }
    private void SetFlag(char flag, bool value)
    {
        var p = GetField<ProcessorStatus>("_p");
        switch (flag)
        {
            case 'N': p.Negative = value; break;
            case 'Z': p.Zero = value; break;
            case 'C': p.Carry = value; break;
            case 'V': p.Overflow = value; break;
            default: throw new ArgumentException("Invalid flag specified.");
        }
        SetField("_p", p);
    }
}