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
        Clock(2);

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
        Clock(2);

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
        Assert.Equal(1, GetCycles()); // Should have 1 cycle left which will be consumed on the next tick
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
        Clock(2);

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
        Clock(2);

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
        Clock(2);

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
        Clock(3);

        // Assert
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedV, GetFlag('V'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Theory]
    // ASL (Arithmetic Shift Left)
    [InlineData(0x0A, 'A', 0x42, false, 0x84, false, false, true, 2)] // ASL ACC: 01000010 -> 10000100 (N=1), C=0
    [InlineData(0x0A, 'A', 0x80, false, 0x00, true, true, false, 2)]  // ASL ACC: 10000000 -> 00000000 (Z=1), C=1
    [InlineData(0x06, 'M', 0x01, false, 0x02, false, false, false, 5)] // ASL ZP

    // LSR (Logical Shift Right)
    [InlineData(0x4A, 'A', 0x01, false, 0x00, true, true, false, 2)]  // LSR ACC: 00000001 -> 00000000 (Z=1), C=1
    [InlineData(0x4A, 'A', 0x84, false, 0x42, false, false, false, 2)] // LSR ACC: 10000100 -> 01000010, C=0

    // ROL (Rotate Left)
    [InlineData(0x2A, 'A', 0x80, false, 0x00, true, true, false, 2)]  // ROL ACC: C=0, 10000000 -> 00000000, C=1, Z=1
    [InlineData(0x2A, 'A', 0x01, true, 0x03, false, false, false, 2)]   // ROL ACC: C=1, 00000001 -> 00000011, C=0

    // ROR (Rotate Right)
    [InlineData(0x6A, 'A', 0x01, false, 0x00, true, true, false, 2)]  // ROR ACC: C=0, 00000001 -> 00000000, C=1, Z=1
    [InlineData(0x6A, 'A', 0x00, true, 0x80, false, false, true, 2)]   // ROR ACC: C=1, 00000000 -> 10000000, C=0, N=1
    public void ShiftAndRotateInstructions_SetFlagsCorrectly(
        byte opcode, char mode, byte initialValue, bool initialCarry,
        byte expectedValue, bool expectedC, bool expectedZ, bool expectedN, int expectedCycles)
    {
        // Arrange
        SetPc(0x8000);
        SetP(0);
        SetFlag('C', initialCarry);

        if (mode == 'A') // Accumulator mode
        {
            _bus.Load(0x8000, [opcode]);
            SetA(initialValue);
        }
        else // Memory mode (Zero Page for this test)
        {
            _bus.Load(0x8000, [opcode, 0x42]);
            _bus.Write(0x0042, initialValue);
        }

        // Act
        Clock(expectedCycles);

        // Assert
        var finalValue = (mode == 'A') ? GetA() : _bus.Read(0x0042);
        Assert.Equal(expectedValue, finalValue);
        Assert.Equal(expectedC, GetFlag('C'));
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Theory]
    // TAX, TAY, TXA, TYA
    [InlineData(0xAA, 'A', 'X', 0x42, false, false, true)] // TAX
    [InlineData(0xAA, 'A', 'X', 0x00, true, false, true)]  // TAX, sets Z
    [InlineData(0xA8, 'A', 'Y', 0x8F, false, true, true)]  // TAY, sets N
    [InlineData(0x8A, 'X', 'A', 0x42, false, false, true)] // TXA
    [InlineData(0x98, 'Y', 'A', 0x42, false, false, true)] // TYA
    // TSX, TXS
    [InlineData(0xBA, 'S', 'X', 0xFD, false, true, true)]  // TSX
    [InlineData(0x9A, 'X', 'S', 0x42, false, false, false)] // TXS (does not set flags)
    public void RegisterTransferInstructions_SetFlagsCorrectly(
        byte opcode, char source, char dest, byte initialValue,
        bool expectedZ, bool expectedN, bool flagsShouldChange)
    {
        // Arrange
        _bus.Load(0x8000, [opcode]);
        SetPc(0x8000);
        SetP(0); // Clear all flags

        switch (source)
        {
            case 'A': SetA(initialValue); break;
            case 'X': SetX(initialValue); break;
            case 'Y': SetY(initialValue); break;
            case 'S': SetSp(initialValue); break;
        }

        // Act
        Clock(2);

        // Assert
        byte finalValue = dest switch
        {
            'A' => GetA(), 'X' => GetX(), 'Y' => GetY(), 'S' => GetSp(), _ => 0
        };
        Assert.Equal(initialValue, finalValue);

        if (flagsShouldChange)
        {
            Assert.Equal(expectedZ, GetFlag('Z'));
            Assert.Equal(expectedN, GetFlag('N'));
        }
    }

    [Theory]
    [InlineData(0x18, 'C', false, true)]  // CLC
    [InlineData(0x38, 'C', true, false)]  // SEC
    [InlineData(0x58, 'I', false, true)]  // CLI
    [InlineData(0x78, 'I', true, false)]  // SEI
    [InlineData(0xB8, 'V', false, true)]  // CLV
    [InlineData(0xD8, 'D', false, true)]  // CLD
    [InlineData(0xF8, 'D', true, false)]  // SED
    public void FlagInstructions_SetFlagsCorrectly(byte opcode, char flag, bool expectedValue, bool initialValue)
    {
        // Arrange
        _bus.Load(0x8000, [opcode]);
        SetPc(0x8000);
        SetP(0);
        SetFlag(flag, initialValue);

        // Act
        Clock(2);

        // Assert
        Assert.Equal(expectedValue, GetFlag(flag));
    }

    [Fact]
    public void PHP_PLP_PushAndPullStatus()
    {
        // --- Test PHP ---
        // Arrange
        _bus.Load(0x8000, [0x08]); // PHP
        SetPc(0x8000);
        SetSp(0xFD);
        SetP(0b10000001); // Set N and C flags

        // Act
        Clock(3);

        // Assert
        Assert.Equal(0xFC, GetSp());
        // PHP pushes status with B flag (bit 4) and unused bit 5 set
        Assert.Equal(0b10110001, _bus.Read(0x01FD));

        // --- Test PLP ---
        // Arrange
        _bus.Load(0x8001, [0x28]); // PLP
        SetP(0); // Clear flags before pull

        // Act
        Clock(4);

        // Assert
        Assert.Equal(0xFD, GetSp());
        // PLP pulls status but ignores B flag and bit 5.
        // Our implementation also ensures bit 5 is set and B is clear after PLP.
        Assert.Equal(0b10100001, GetP());
    }

    [Fact]
    public void BRK_RTI_BreakAndReturnFromInterrupt()
    {
        // Arrange
        // At 0x8000: BRK
        _bus.Load(0x8000, [0x00]);
        // At 0x9000: RTI (our "interrupt handler")
        _bus.Load(0x9000, [0x40]);
        // Set IRQ/BRK vector to point to 0x9000
        _bus.Write(0xFFFE, 0x00);
        _bus.Write(0xFFFF, 0x90);

        SetPc(0x8000);
        SetSp(0xFD);
        SetP(0b10000001); // Set N and C flags, I is clear

        // --- Test BRK ---
        // Act
        Clock(7);

        // Assert after BRK
        Assert.Equal(0x9000, GetPc()); // PC is at the interrupt handler
        Assert.Equal(0xFA, GetSp());   // SP decremented by 3
        Assert.True(GetFlag('I'));     // Interrupt disable flag is set

        // BRK pushes PC+2 (0x8002). Pushed as hi, lo: $80, $02
        Assert.Equal(0x80, _bus.Read(0x01FD)); // High byte of return address
        Assert.Equal(0x02, _bus.Read(0x01FC)); // Low byte of return address
        // Status pushed with B flag (bit 4) and unused bit 5 set
        Assert.Equal(0b10110001, _bus.Read(0x01FB));

        // --- Test RTI ---
        // Act
        Clock(6);

        // Assert after RTI
        Assert.Equal(0x8002, GetPc()); // PC returns to address after the skipped byte
        Assert.Equal(0xFD, GetSp());   // Stack pointer is restored
        // Status is restored, but B is cleared and bit 5 is set by our implementation
        Assert.Equal(0b10100001, GetP());
    }

    [Theory]
    // STA
    [InlineData(0x85, 'A', 0x42, new byte[] { 0x80 }, 0, 0, 0x0080, 3)] // STA ZP
    [InlineData(0x95, 'A', 0x42, new byte[] { 0x80 }, 0x10, 0, 0x0090, 4)] // STA ZPX
    [InlineData(0x8D, 'A', 0x42, new byte[] { 0x34, 0x12 }, 0, 0, 0x1234, 4)] // STA ABS
    [InlineData(0x9D, 'A', 0x42, new byte[] { 0x34, 0x12 }, 0x10, 0, 0x1244, 5)] // STA ABSX
    // STX
    [InlineData(0x86, 'X', 0x42, new byte[] { 0x80 }, 0x42, 0, 0x0080, 3)] // STX ZP
    [InlineData(0x96, 'X', 0x42, new byte[] { 0x80 }, 0x42, 0x10, 0x0090, 4)] // STX ZPY
    // STY
    [InlineData(0x8C, 'Y', 0x42, new byte[] { 0x34, 0x12 }, 0, 0x42, 0x1234, 4)] // STY ABS
    public void StoreInstructions_WriteToMemoryCorrectly(
        byte opcode, char register, byte valueToStore, byte[] operands, byte initialX, byte initialY,
        ushort expectedAddress, int expectedCycles)
    {
        // Arrange
        var instruction = new byte[] { opcode }.Concat(operands).ToArray();
        _bus.Load(0x8000, instruction);
        SetPc(0x8000);
        SetP(0b11111111); // Set all flags to ensure they are not changed

        switch (register)
        {
            case 'A': SetA(valueToStore); break;
            case 'X': SetX(valueToStore); break;
            case 'Y': SetY(valueToStore); break;
        }
        SetX(initialX);
        SetY(initialY);

        // Act
        Clock(expectedCycles);

        // Assert
        Assert.Equal(valueToStore, _bus.Read(expectedAddress));
        Assert.Equal(1, GetCycles());
        Assert.Equal(0b11111111, GetP()); // Verify flags are not affected
    }

    [Theory]
    // AND
    [InlineData(0x29, 0b11001100, 0b10101010, 0b10001000, false, true)] // AND, sets N
    [InlineData(0x29, 0b00110011, 0b11001100, 0b00000000, true, false)]  // AND, sets Z
    // EOR
    [InlineData(0x49, 0b10101010, 0b10101010, 0b00000000, true, false)]  // EOR, sets Z
    [InlineData(0x49, 0b01010101, 0b10101010, 0b11111111, false, true)]  // EOR, sets N
    // ORA
    [InlineData(0x09, 0b11000000, 0b00001100, 0b11001100, false, true)] // ORA, sets N
    [InlineData(0x09, 0b00000000, 0b00000000, 0b00000000, true, false)]  // ORA, sets Z
    public void LogicalInstructions_SetFlagsCorrectly(
        byte opcode, byte initialA, byte operand,
        byte expectedA, bool expectedZ, bool expectedN)
    {
        // Arrange
        _bus.Load(0x8000, [opcode, operand]);
        SetPc(0x8000);
        SetA(initialA);
        SetP(0);

        // Act
        Clock(2);

        // Assert
        Assert.Equal(expectedA, GetA());
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Fact]
    public void NOP_DoesNothingAndTakesTwoCycles()
    {
        // Arrange
        _bus.Load(0x8000, [0xEA]); // NOP
        SetPc(0x8000);
        // Set registers and flags to known, non-zero values
        SetA(0xFF);
        SetX(0xFF);
        SetY(0xFF);
        SetSp(0xFF);
        SetP(0xFF);

        // Act
        Clock(2);

        // Assert
        Assert.Equal(0x8001, GetPc()); // PC should advance by 1
        Assert.Equal(1, GetCycles()); // Should have 1 cycle left
        // Verify that no registers or flags were changed
        Assert.Equal(0xFF, GetA());
        Assert.Equal(0xFF, GetX());
        Assert.Equal(0xFF, GetY());
        Assert.Equal(0xFF, GetSp());
        Assert.Equal(0xFF, GetP());
    }

    [Fact]
    public void Stall_AddsCyclesToCurrentInstruction()
    {
        // Arrange
        SetPc(0x8000);

        // Act
        _cpu.Stall(10);

        // Assert
        Assert.Equal(10, GetCycles());
    }

    [Fact]
    public void NMI_TriggersInterruptSequence()
    {
        // Arrange
        // Set NMI vector to point to 0x9000
        _bus.Write(0xFFFA, 0x00);
        _bus.Write(0xFFFB, 0x90);

        SetPc(0x8000);
        SetSp(0xFD);
        SetP(0b10000101); // Set N, C, and I flags to prove NMI ignores I
        _interrupts.Nmi = true;
        SetCycles(0); // Set cycles to 0 to ensure the next clock polls for interrupts

        // Act
        Clock(7);

        // Assert
        Assert.Equal(0x9000, GetPc()); // PC is at the interrupt handler
        Assert.Equal(0xFA, GetSp());   // SP decremented by 3
        Assert.True(GetFlag('I'));     // Interrupt disable flag is set by the handler
        Assert.False(_interrupts.Nmi); // NMI line is cleared

        // PC pushed is 0x8000. Pushed as hi, lo: $80, $00
        Assert.Equal(0x80, _bus.Read(0x01FD)); // High byte of return address
        Assert.Equal(0x00, _bus.Read(0x01FC)); // Low byte of return address
        // Status pushed with B flag (bit 4) clear
        Assert.Equal(0b10100101, _bus.Read(0x01FB));
    }

    [Theory]
    [InlineData(false, 0x9000, true)]  // IRQ when I flag is clear: should trigger
    [InlineData(true, 0x8001, true)]   // IRQ when I flag is set: should be ignored
    public void IRQ_TriggersInterruptSequence_OnlyWhenInterruptsEnabled(bool initialInterruptFlag, ushort expectedPc, bool expectedInterruptFlag)
    {
        // Arrange: Place a NOP at the starting PC for the case where the interrupt is ignored.
        _bus.Load(0x8000, [0xEA]); // NOP

        // Arrange
        // Set IRQ vector to point to 0x9000
        _bus.Write(0xFFFE, 0x00);
        _bus.Write(0xFFFF, 0x90);

        SetPc(0x8000);
        SetSp(0xFD);
        SetP(0);
        SetFlag('I', initialInterruptFlag);
        _interrupts.Irq = true;
        SetCycles(0); // Set cycles to 0 to ensure the next clock polls for interrupts

        // Act
        // If the interrupt is taken, it will consume 7 cycles. If not, it will execute a NOP (2 cycles).
        // We clock for the max duration to cover both cases.
        Clock(7);

        // Assert
        Assert.Equal(expectedPc, GetPc());
        Assert.Equal(expectedInterruptFlag, GetFlag('I'));

        if (!initialInterruptFlag) // If interrupt was taken
        {
            Assert.Equal(0xFA, GetSp());
        }
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
    private void SetCycles(byte value) => SetField("_cycles", value);
    private bool GetFlag(char flag)
    {
        var p = GetField<ProcessorStatus>("_p");
        return flag switch
        {
            'N' => p.Negative,
            'Z' => p.Zero,
            'C' => p.Carry,
            'V' => p.Overflow,
            'I' => p.InterruptDisable,
            'D' => p.Decimal,
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
            case 'I': p.InterruptDisable = value; break;
            case 'D': p.Decimal = value; break;
            default: throw new ArgumentException("Invalid flag specified.");
        }
        SetField("_p", p);
    }
}