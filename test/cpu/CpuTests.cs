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
        Assert.Equal(7, GetCycles());
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
    [InlineData(0xF0, 'Z', true, 0x80FD, 0x05, 4, 0x8104)] // Post-operand PC $80FF crosses to page $81
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
        Assert.Equal(0, GetCycles());
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
        Assert.Equal(0, GetCycles());
        // Verify that no registers or flags were changed
        Assert.Equal(0xFF, GetA());
        Assert.Equal(0xFF, GetX());
        Assert.Equal(0xFF, GetY());
        Assert.Equal(0xFF, GetSp());
        Assert.Equal(0xFF, GetP());
    }

    [Theory]
    [InlineData(0x1A)]
    [InlineData(0x3A)]
    [InlineData(0x5A)]
    [InlineData(0x7A)]
    [InlineData(0xDA)]
    [InlineData(0xFA)]
    public void UnofficialImpliedNop_DoesNothingAndTakesTwoCycles(byte opcode)
    {
        _bus.Load(0x8000, [opcode]);
        SetPc(0x8000);
        SetA(0x12);
        SetX(0x34);
        SetY(0x56);
        SetSp(0x78);
        SetP(0xA5);

        var cycles = _cpu.Step();

        Assert.Equal(2UL, cycles);
        Assert.Equal(0x8001, GetPc());
        Assert.Equal(0x12, GetA());
        Assert.Equal(0x34, GetX());
        Assert.Equal(0x56, GetY());
        Assert.Equal(0x78, GetSp());
        Assert.Equal(0xA5, GetP());
    }

    [Fact]
    public void UnofficialSbcImmediateAlias_MatchesOfficialSbc()
    {
        _bus.Load(0x8000, [0xEB, 0x20]);
        SetPc(0x8000);
        SetA(0x80);
        SetP(0x21);

        Assert.Equal(2UL, _cpu.Step());
        Assert.Equal(0x60, GetA());
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
        Assert.Equal(0x8002, GetPc());
    }

    [Theory]
    [InlineData(0x0B)]
    [InlineData(0x2B)]
    public void AacImmediate_AndsAccumulatorAndCopiesNegativeToCarry(byte opcode)
    {
        _bus.Load(0x8000, [opcode, 0xF0]);
        SetPc(0x8000);
        SetA(0x8F);
        SetP(0x60);

        Assert.Equal(2UL, _cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
        Assert.Equal(0x8002, GetPc());
    }

    [Fact]
    public void AsrImmediate_AndsThenLogicallyShiftsAccumulator()
    {
        _bus.Load(0x8000, [0x4B, 0x0F]);
        SetPc(0x8000);
        SetA(0xFF);
        SetP(0x60);

        Assert.Equal(2UL, _cpu.Step());
        Assert.Equal(0x07, GetA());
        Assert.True(GetFlag('C'));
        Assert.False(GetFlag('N'));
        Assert.False(GetFlag('Z'));
        Assert.True(GetFlag('V'));
    }

    [Theory]
    [InlineData(0xFF, 0x00, false, false, false, false)]
    [InlineData(0xFF, 0x40, false, false, true, false)]
    [InlineData(0xFF, 0x80, true, true, true, true)]
    public void ArrImmediate_UsesRotatedBitsSixAndFiveForCarryAndOverflow(
        byte accumulator, byte operand, bool initialCarry,
        bool expectedCarry, bool expectedOverflow, bool expectedNegative)
    {
        _bus.Load(0x8000, [0x6B, operand]);
        SetPc(0x8000);
        SetA(accumulator);
        SetP((byte)(0x20 | (initialCarry ? 1 : 0)));

        Assert.Equal(2UL, _cpu.Step());
        Assert.Equal((byte)(((accumulator & operand) >> 1) | (initialCarry ? 0x80 : 0)), GetA());
        Assert.Equal(expectedCarry, GetFlag('C'));
        Assert.Equal(expectedOverflow, GetFlag('V'));
        Assert.Equal(expectedNegative, GetFlag('N'));
    }

    [Fact]
    public void AtxImmediate_UsesNes2A03MagicConstantAndLoadsAccumulatorAndX()
    {
        _bus.Load(0x8000, [0xAB, 0xF3]);
        SetPc(0x8000);
        SetA(0x01);
        SetX(0x55);
        SetP(0x61);

        Assert.Equal(2UL, _cpu.Step());
        Assert.Equal(0xF3, GetA());
        Assert.Equal(0xF3, GetX());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
    }

    [Fact]
    public void CpuVariantBehaviorAnnotations_DistinguishNesDeviationFromInheritedNmosQuirk()
    {
        var atx = typeof(Cpu).GetMethod("ATX_IMM", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var readWordBug = typeof(Cpu).GetMethod("ReadWordBug", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Equal(
            CpuBehaviorKind.Nes2A03Deviation,
            atx.GetCustomAttribute<CpuBehaviorAttribute>()!.Kind);
        Assert.Equal(
            CpuBehaviorKind.Nmos6502Quirk,
            readWordBug.GetCustomAttribute<CpuBehaviorAttribute>()!.Kind);
    }

    [Theory]
    [InlineData(0x0A, 0x04, true, false)]
    [InlineData(0x10, 0xFE, false, true)]
    public void AxsImmediate_SubtractsFromAccumulatorAndXIntersectionWithoutBorrow(
        byte operand, byte expectedResult, bool expectedCarry, bool expectedNegative)
    {
        _bus.Load(0x8000, [0xCB, operand]);
        SetPc(0x8000);
        SetA(0x3F);
        SetX(0x0E);
        SetP(0x60);

        Assert.Equal(2UL, _cpu.Step());
        Assert.Equal(expectedResult, GetX());
        Assert.Equal(0x3F, GetA());
        Assert.Equal(expectedCarry, GetFlag('C'));
        Assert.Equal(expectedNegative, GetFlag('N'));
        Assert.True(GetFlag('V'));
    }

    [Fact]
    public void SloZeroPage_ShiftsMemoryThenOrsAccumulator()
    {
        PrepareUnofficialZeroPage(0x07, 0x81, accumulator: 0x10);
        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0x02, _bus.Read(0x0042));
        Assert.Equal(0x12, GetA());
        Assert.True(GetFlag('C'));
        Assert.False(GetFlag('N'));
        Assert.False(GetFlag('Z'));
    }

    [Fact]
    public void RlaZeroPage_RotatesMemoryThenAndsAccumulator()
    {
        PrepareUnofficialZeroPage(0x27, 0x80, accumulator: 0xF0, status: 0x21);
        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0x01, _bus.Read(0x0042));
        Assert.Equal(0, GetA());
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('Z'));
    }

    [Fact]
    public void SreZeroPage_ShiftsMemoryThenExclusiveOrsAccumulator()
    {
        PrepareUnofficialZeroPage(0x47, 0x03, accumulator: 0xF0);
        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0x01, _bus.Read(0x0042));
        Assert.Equal(0xF1, GetA());
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('N'));
    }

    [Fact]
    public void RraZeroPage_UsesRotateCarryAsAdcInput()
    {
        PrepareUnofficialZeroPage(0x67, 0x01, accumulator: 0x7F);
        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0, _bus.Read(0x0042));
        Assert.Equal(0x80, GetA());
        Assert.False(GetFlag('C'));
        Assert.True(GetFlag('V'));
        Assert.True(GetFlag('N'));
    }

    [Fact]
    public void AaxZeroPage_StoresAccumulatorAndXIntersectionWithoutChangingFlags()
    {
        PrepareUnofficialZeroPage(0x87, 0, accumulator: 0xCC, x: 0xAA, status: 0xE5);
        Assert.Equal(3UL, _cpu.Step());
        Assert.Equal(0x88, _bus.Read(0x0042));
        Assert.Equal(0xE5, GetP());
    }

    [Fact]
    public void LaxZeroPage_LoadsAccumulatorAndXAndSetsZeroAndNegative()
    {
        PrepareUnofficialZeroPage(0xA7, 0x80, status: 0x61);
        Assert.Equal(3UL, _cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.Equal(0x80, GetX());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
    }

    [Fact]
    public void DcpZeroPage_DecrementsMemoryThenComparesAccumulator()
    {
        PrepareUnofficialZeroPage(0xC7, 0x10, accumulator: 0x0F, status: 0x60);
        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0x0F, _bus.Read(0x0042));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('Z'));
        Assert.True(GetFlag('V'));
    }

    [Fact]
    public void IscZeroPage_IncrementsMemoryThenSubtractsWithCarry()
    {
        PrepareUnofficialZeroPage(0xE7, 0x7F, accumulator: 0, status: 0x21);
        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0x80, _bus.Read(0x0042));
        Assert.Equal(0x80, GetA());
        Assert.False(GetFlag('C'));
        Assert.True(GetFlag('V'));
        Assert.True(GetFlag('N'));
    }

    private void PrepareUnofficialZeroPage(
        byte opcode, byte memory, byte accumulator = 0, byte x = 0, byte status = 0x20)
    {
        _bus.Load(0x8000, [opcode, 0x42]);
        _bus.Write(0x0042, memory);
        SetPc(0x8000);
        SetA(accumulator);
        SetX(x);
        SetP(status);
    }

    [Theory]
    [InlineData(0x17, 0x81, 0x10, 0x20, 0x02, 0x12)]
    [InlineData(0x37, 0x80, 0xF0, 0x21, 0x01, 0x00)]
    [InlineData(0x57, 0x03, 0xF0, 0x20, 0x01, 0xF1)]
    [InlineData(0x77, 0x01, 0x7F, 0x20, 0x00, 0x80)]
    [InlineData(0xD7, 0x10, 0x0F, 0x60, 0x0F, 0x0F)]
    [InlineData(0xF7, 0x7F, 0x00, 0x21, 0x80, 0x80)]
    public void UnofficialReadModifyWriteZeroPageX_WrapsAndTakesSixCycles(
        byte opcode, byte memory, byte accumulator, byte status,
        byte expectedMemory, byte expectedAccumulator)
    {
        _bus.Load(0x8000, [opcode, 0xFE]);
        _bus.Write(0x0003, memory);
        SetPc(0x8000);
        SetA(accumulator);
        SetX(5);
        SetP(status);

        Assert.Equal(6UL, _cpu.Step());
        Assert.Equal(expectedMemory, _bus.Read(0x0003));
        Assert.Equal(expectedAccumulator, GetA());
        Assert.Equal(0x8002, GetPc());
    }

    [Fact]
    public void AaxZeroPageY_UsesYIndexAndWrapsWithinZeroPage()
    {
        _bus.Load(0x8000, [0x97, 0xFE]);
        _bus.Write(0x00FF, 0x55);
        SetPc(0x8000);
        SetA(0xCC);
        SetX(0xAA);
        SetY(5);
        SetP(0xE5);

        Assert.Equal(4UL, _cpu.Step());
        Assert.Equal(0x88, _bus.Read(0x0003));
        Assert.Equal(0x55, _bus.Read(0x00FF));
        Assert.Equal(0xE5, GetP());
    }

    [Fact]
    public void LaxZeroPageY_UsesYIndexAndWrapsWithinZeroPage()
    {
        _bus.Load(0x8000, [0xB7, 0xFE]);
        _bus.Write(0x0003, 0x80);
        _bus.Write(0x00FF, 0x11);
        SetPc(0x8000);
        SetX(1);
        SetY(5);
        SetP(0x61);

        Assert.Equal(4UL, _cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.Equal(0x80, GetX());
        Assert.True(GetFlag('N'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
    }

    [Theory]
    [InlineData(0x0F, 0x81, 0x10, 0x20, 0x02, 0x12)]
    [InlineData(0x2F, 0x80, 0xF0, 0x21, 0x01, 0x00)]
    [InlineData(0x4F, 0x03, 0xF0, 0x20, 0x01, 0xF1)]
    [InlineData(0x6F, 0x01, 0x7F, 0x20, 0x00, 0x80)]
    [InlineData(0xCF, 0x10, 0x0F, 0x60, 0x0F, 0x0F)]
    [InlineData(0xEF, 0x7F, 0x00, 0x21, 0x80, 0x80)]
    public void UnofficialReadModifyWriteAbsolute_UsesFullAddressAndTakesSixCycles(
        byte opcode, byte memory, byte accumulator, byte status,
        byte expectedMemory, byte expectedAccumulator)
    {
        _bus.Load(0x8000, [opcode, 0x34, 0x12]);
        _bus.Write(0x1234, memory);
        SetPc(0x8000);
        SetA(accumulator);
        SetP(status);

        Assert.Equal(6UL, _cpu.Step());
        Assert.Equal(expectedMemory, _bus.Read(0x1234));
        Assert.Equal(expectedAccumulator, GetA());
        Assert.Equal(0x8003, GetPc());
    }

    [Fact]
    public void AaxAbsolute_StoresAccumulatorAndXIntersectionWithoutChangingFlags()
    {
        _bus.Load(0x8000, [0x8F, 0x34, 0x12]);
        SetPc(0x8000);
        SetA(0xCC);
        SetX(0xAA);
        SetP(0xE5);

        Assert.Equal(4UL, _cpu.Step());
        Assert.Equal(0x88, _bus.Read(0x1234));
        Assert.Equal(0xE5, GetP());
    }

    [Fact]
    public void LaxAbsolute_LoadsAccumulatorAndXAndSetsZeroAndNegative()
    {
        _bus.Load(0x8000, [0xAF, 0x34, 0x12]);
        _bus.Write(0x1234, 0x80);
        SetPc(0x8000);
        SetP(0x61);

        Assert.Equal(4UL, _cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.Equal(0x80, GetX());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
    }

    [Theory]
    [InlineData(0x1F, true, 0x81, 0x10, 0x20, 0x02, 0x12)]
    [InlineData(0x3F, true, 0x80, 0xF0, 0x21, 0x01, 0x00)]
    [InlineData(0x5F, true, 0x03, 0xF0, 0x20, 0x01, 0xF1)]
    [InlineData(0x7F, true, 0x01, 0x7F, 0x20, 0x00, 0x80)]
    [InlineData(0xDF, true, 0x10, 0x0F, 0x60, 0x0F, 0x0F)]
    [InlineData(0xFF, true, 0x7F, 0x00, 0x21, 0x80, 0x80)]
    [InlineData(0x1B, false, 0x81, 0x10, 0x20, 0x02, 0x12)]
    [InlineData(0x3B, false, 0x80, 0xF0, 0x21, 0x01, 0x00)]
    [InlineData(0x5B, false, 0x03, 0xF0, 0x20, 0x01, 0xF1)]
    [InlineData(0x7B, false, 0x01, 0x7F, 0x20, 0x00, 0x80)]
    [InlineData(0xDB, false, 0x10, 0x0F, 0x60, 0x0F, 0x0F)]
    [InlineData(0xFB, false, 0x7F, 0x00, 0x21, 0x80, 0x80)]
    public void UnofficialReadModifyWriteAbsoluteIndexed_UsesSelectedIndexAndTakesSevenCycles(
        byte opcode, bool usesX, byte memory, byte accumulator, byte status,
        byte expectedMemory, byte expectedAccumulator)
    {
        _bus.Load(0x8000, [opcode, 0x30, 0x12]);
        _bus.Write(0x1235, memory);
        SetPc(0x8000);
        SetA(accumulator);
        SetX(usesX ? (byte)5 : (byte)1);
        SetY(usesX ? (byte)1 : (byte)5);
        SetP(status);

        Assert.Equal(7UL, _cpu.Step());
        Assert.Equal(expectedMemory, _bus.Read(0x1235));
        Assert.Equal(expectedAccumulator, GetA());
        Assert.Equal(0x8003, GetPc());
    }

    [Theory]
    [InlineData(0x9C)]
    [InlineData(0x9E)]
    public void ShyAndShxAbsoluteIndexed_ReplaceAddressHighByteOnPageCross(byte opcode)
    {
        _bus.Load(0x8000, [opcode, 0xFF, 0x12]);
        _bus.Write(0x1301, 0x55);
        SetPc(0x8000);
        SetX(2);
        SetY(2);
        SetP(0xE5);

        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0x02, _bus.Read(0x0201));
        Assert.Equal(0x55, _bus.Read(0x1301));
        Assert.Equal(0xE5, GetP());
    }

    [Theory]
    [InlineData(0x9C)]
    [InlineData(0x9E)]
    public void ShyAndShxAbsoluteIndexed_StoreAtEffectiveAddressWithoutPageCross(byte opcode)
    {
        _bus.Load(0x8000, [opcode, 0x34, 0x12]);
        SetPc(0x8000);
        SetX(2);
        SetY(2);

        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0x02, _bus.Read(0x1236));
    }

    [Theory]
    [InlineData(0x1230, 4UL)]
    [InlineData(0x12FF, 5UL)]
    public void LaxAbsoluteY_AddsPageCrossCycle(ushort baseAddress, ulong expectedCycles)
    {
        _bus.Load(0x8000, [0xBF, (byte)baseAddress, (byte)(baseAddress >> 8)]);
        _bus.Write((ushort)(baseAddress + 1), 0x80);
        SetPc(0x8000);
        SetY(1);
        SetP(0x61);

        Assert.Equal(expectedCycles, _cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.Equal(0x80, GetX());
        Assert.True(GetFlag('N'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
    }

    [Theory]
    [InlineData(0x03, 0x81, 0x10, 0x20, 0x02, 0x12)]
    [InlineData(0x23, 0x80, 0xF0, 0x21, 0x01, 0x00)]
    [InlineData(0x43, 0x03, 0xF0, 0x20, 0x01, 0xF1)]
    [InlineData(0x63, 0x01, 0x7F, 0x20, 0x00, 0x80)]
    [InlineData(0xC3, 0x10, 0x0F, 0x60, 0x0F, 0x0F)]
    [InlineData(0xE3, 0x7F, 0x00, 0x21, 0x80, 0x80)]
    public void UnofficialReadModifyWriteIndexedIndirect_WrapsPointerAndTakesEightCycles(
        byte opcode, byte memory, byte accumulator, byte status,
        byte expectedMemory, byte expectedAccumulator)
    {
        PrepareIndexedIndirect(opcode, memory, accumulator, status);

        Assert.Equal(8UL, _cpu.Step());
        Assert.Equal(expectedMemory, _bus.Read(0x1234));
        Assert.Equal(expectedAccumulator, GetA());
        Assert.Equal(0x8002, GetPc());
    }

    [Fact]
    public void AaxIndexedIndirect_StoresAccumulatorAndXIntersectionWithoutChangingFlags()
    {
        PrepareIndexedIndirect(0x83, 0, accumulator: 0x03, status: 0xE5);

        Assert.Equal(6UL, _cpu.Step());
        Assert.Equal(0x02, _bus.Read(0x1234));
        Assert.Equal(0xE5, GetP());
    }

    [Fact]
    public void LaxIndexedIndirect_LoadsAccumulatorAndXAndSetsZeroAndNegative()
    {
        PrepareIndexedIndirect(0xA3, 0x80, status: 0x61);

        Assert.Equal(6UL, _cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.Equal(0x80, GetX());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
    }

    private void PrepareIndexedIndirect(byte opcode, byte memory, byte accumulator = 0, byte status = 0x20)
    {
        _bus.Load(0x8000, [opcode, 0xFD]);
        _bus.Write(0x00FF, 0x34);
        _bus.Write(0x0000, 0x12);
        _bus.Write(0x0100, 0x99);
        _bus.Write(0x1234, memory);
        SetPc(0x8000);
        SetA(accumulator);
        SetX(2);
        SetP(status);
    }

    [Theory]
    [InlineData(0x13, 0x81, 0x10, 0x20, 0x02, 0x12)]
    [InlineData(0x33, 0x80, 0xF0, 0x21, 0x01, 0x00)]
    [InlineData(0x53, 0x03, 0xF0, 0x20, 0x01, 0xF1)]
    [InlineData(0x73, 0x01, 0x7F, 0x20, 0x00, 0x80)]
    public void UnofficialReadModifyWriteIndirectIndexed_WrapsPointerAndAlwaysTakesEightCycles(
        byte opcode, byte memory, byte accumulator, byte status,
        byte expectedMemory, byte expectedAccumulator)
    {
        _bus.Load(0x8000, [opcode, 0xFF]);
        _bus.Write(0x00FF, 0xFF);
        _bus.Write(0x0000, 0x12);
        _bus.Write(0x0100, 0x99);
        _bus.Write(0x1301, memory);
        SetPc(0x8000);
        SetA(accumulator);
        SetY(2);
        SetP(status);

        Assert.Equal(8UL, _cpu.Step());
        Assert.Equal(expectedMemory, _bus.Read(0x1301));
        Assert.Equal(expectedAccumulator, GetA());
        Assert.Equal(0x8002, GetPc());
    }

    [Theory]
    [InlineData(false, 5UL)]
    [InlineData(true, 6UL)]
    public void LAX_IndirectIndexed_LoadsAccumulatorAndX_WithReadPageCrossTiming(
        bool crossesPage, ulong expectedCycles)
    {
        SetPc(0x8000);
        SetY(1);
        _bus.Load(0x8000, [0xB3, 0x40]);
        _bus.Write(0x0040, crossesPage ? (byte)0xFF : (byte)0x33);
        _bus.Write(0x0041, 0x12);
        _bus.Write(crossesPage ? (ushort)0x1300 : (ushort)0x1234, 0x80);

        Assert.Equal(expectedCycles, _cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.Equal(0x80, GetX());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
    }

    [Theory]
    [InlineData(0xD3, 0x40, 0x3F, 0x40, true)]
    [InlineData(0xF3, 0x3F, 0x40, 0x40, true)]
    public void DcpAndIsc_IndirectIndexed_ModifyThenCompareOrSubtract(
        byte opcode, byte operand, byte expectedMemory, byte initialA, bool initialCarry)
    {
        SetPc(0x8000);
        SetA(initialA);
        SetY(1);
        SetP(0);
        SetFlag('C', initialCarry);
        _bus.Load(0x8000, [opcode, 0xFF]);
        _bus.Write(0x00FF, 0xFF);
        _bus.Write(0x0000, 0x12);
        _bus.Write(0x1300, operand);

        Assert.Equal(8UL, _cpu.Step());
        Assert.Equal(expectedMemory, _bus.Read(0x1300));
        Assert.Equal(opcode == 0xF3 ? (byte)0x00 : initialA, GetA());
        Assert.Equal(opcode == 0xF3, GetFlag('Z'));
        Assert.True(GetFlag('C'));
    }

    [Theory]
    [InlineData(0x80FD, 0x05, 0x8104, 4UL)]
    [InlineData(0x8102, unchecked((byte)-5), 0x80FF, 4UL)]
    public void BvcTaken_HandlesForwardAndBackwardPageCrossings(
        ushort startPc, byte offset, ushort expectedPc, ulong expectedCycles)
    {
        _bus.Load(startPc, [0x50, offset]);
        SetPc(startPc);
        SetP(0x20);

        Assert.Equal(expectedCycles, _cpu.Step());
        Assert.Equal(expectedPc, GetPc());
        Assert.False(GetFlag('V'));
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
        SetCycles(1); // Assert the edge before the current instruction's final interrupt poll.

        // Act
        Clock(8);

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

    [Fact]
    public void NmiEdgeFirstObservedAtInstructionBoundaryWaitsThroughTheNextInstruction()
    {
        _bus.Load(0x8000, [0xEA, 0xEA]);
        _bus.Write(0xFFFA, 0x00);
        _bus.Write(0xFFFB, 0x90);
        SetPc(0x8000);
        SetCycles(0);
        _interrupts.Nmi = true;

        Clock(2);

        Assert.Equal(0x8001, GetPc());
        Assert.True(_interrupts.Nmi);

        _cpu.Clock();

        Assert.Equal(0x9000, GetPc());
        Assert.False(_interrupts.Nmi);
    }

    [Fact]
    public void DelayedNmi_AllowsTheFollowingInstructionToCompleteBeforeInterrupting()
    {
        _bus.Load(0x8000, [0xEA, 0xEA]);
        _bus.Write(0xFFFA, 0x00);
        _bus.Write(0xFFFB, 0x90);
        SetPc(0x8000);
        SetCycles(0);
        _interrupts.Nmi = true;
        typeof(InterruptLines).GetProperty("DelayNmiOneInstruction", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(_interrupts, true);

        Clock(2);

        Assert.Equal(0x8001, GetPc());
        Assert.True(_interrupts.Nmi);

        _cpu.Clock();

        Assert.Equal(0x9000, GetPc());
        Assert.False(_interrupts.Nmi);
    }

    [Fact]
    public void NmiEdge_RemainsPendingAfterTheInputLineFalls()
    {
        _bus.Write(0xFFFA, 0x00);
        _bus.Write(0xFFFB, 0x90);
        SetPc(0x8000);
        SetCycles(1);
        _interrupts.Nmi = true;

        _cpu.Clock();
        _interrupts.Nmi = false;
        _cpu.Clock();

        Assert.Equal(0x9000, GetPc());
    }

    [Fact]
    public void Cli_DelaysAnAssertedIrqUntilAfterTheFollowingInstruction()
    {
        _bus.Load(0x8000, [0x58, 0xEA, 0xEA]);
        _bus.Write(0xFFFE, 0x00);
        _bus.Write(0xFFFF, 0x90);
        SetPc(0x8000);
        SetP(0b0010_0100);
        SetCycles(0);
        _interrupts.Irq = true;

        Clock(4);

        Assert.Equal(0x8002, GetPc());

        _cpu.Clock();

        Assert.Equal(0x9000, GetPc());
    }

    [Fact]
    public void AbsoluteIoWriteOccursOnTheInstructionsFinalCycle()
    {
        _bus.Load(0x8000, [0x8D, 0x00, 0x20]);
        SetPc(0x8000);
        SetA(0x42);
        SetCycles(0);

        Clock(3);
        Assert.Equal(0, _bus.Read(0x2000));

        _cpu.Clock();
        Assert.Equal(0x42, _bus.Read(0x2000));
    }

    [Fact]
    public void AbsoluteIoReadSamplesTheBusOnTheInstructionsFinalCycle()
    {
        _bus.Load(0x8000, [0xAD, 0x02, 0x20]);
        _bus.Write(0x2002, 0x11);
        SetPc(0x8000);
        SetCycles(0);

        _cpu.Clock();
        _bus.Write(0x2002, 0x22);
        Clock(3);

        Assert.Equal(0x22, GetA());
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
        // If the interrupt is taken, it consumes 7 cycles. Otherwise, execute only the 2-cycle NOP.
        Clock(initialInterruptFlag ? 2 : 7);

        // Assert
        Assert.Equal(expectedPc, GetPc());
        Assert.Equal(expectedInterruptFlag, GetFlag('I'));

        if (!initialInterruptFlag) // If interrupt was taken
        {
            Assert.Equal(0xFA, GetSp());
        }
    }

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
        for (var i = 0; i < opcodes.Length; i++) cases.Add(opcodes[i], instruction, modes[i], cycles[i]);
    }

    [Theory]
    [MemberData(nameof(ReadInstructionAddressingCases))]
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

        Assert.Equal(expectedCycles, _cpu.Step());
        Assert.Equal(expectedA, GetA());
        Assert.Equal(expectedZero, GetFlag('Z'));
        Assert.Equal(expectedNegative, GetFlag('N'));
        if (expectedCarry.HasValue) Assert.Equal(expectedCarry.Value, GetFlag('C'));
    }

    private void LoadReadOperand(byte opcode, string addressingMode, byte operand)
    {
        switch (addressingMode)
        {
            case "Immediate":
                _bus.Load(0x8000, [opcode, operand]);
                break;
            case "ZeroPage":
                _bus.Load(0x8000, [opcode, 0x42]);
                _bus.Write(0x0042, operand);
                break;
            case "ZeroPageX":
                _bus.Load(0x8000, [opcode, 0x41]);
                _bus.Write(0x0042, operand);
                break;
            case "Absolute":
                _bus.Load(0x8000, [opcode, 0x34, 0x12]);
                _bus.Write(0x1234, operand);
                break;
            case "AbsoluteX":
            case "AbsoluteY":
                _bus.Load(0x8000, [opcode, 0x33, 0x12]);
                _bus.Write(0x1234, operand);
                break;
            case "IndirectX":
                _bus.Load(0x8000, [opcode, 0x41]);
                _bus.Write(0x0042, 0x34);
                _bus.Write(0x0043, 0x12);
                _bus.Write(0x1234, operand);
                break;
            case "IndirectY":
                _bus.Load(0x8000, [opcode, 0x40]);
                _bus.Write(0x0040, 0x33);
                _bus.Write(0x0041, 0x12);
                _bus.Write(0x1234, operand);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(addressingMode));
        }
    }

    [Theory]
    [InlineData(0xE4, 'X', 3)] // CPX zero page
    [InlineData(0xEC, 'X', 4)] // CPX absolute
    [InlineData(0xC4, 'Y', 3)] // CPY zero page
    [InlineData(0xCC, 'Y', 4)] // CPY absolute
    public void CompareIndexInstructions_ReadMemoryVariants(byte opcode, char register, ulong expectedCycles)
    {
        var absolute = expectedCycles == 4;
        _bus.Load(0x8000, absolute ? [opcode, 0x34, 0x12] : [opcode, 0x42]);
        _bus.Write(absolute ? (ushort)0x1234 : (ushort)0x0042, 0x40);
        SetPc(0x8000);
        if (register == 'X') SetX(0x40); else SetY(0x40);

        Assert.Equal(expectedCycles, _cpu.Step());
        Assert.True(GetFlag('Z'));
        Assert.True(GetFlag('C'));
        Assert.False(GetFlag('N'));
    }

    [Fact]
    public void BIT_Absolute_ReadsOperandAndSetsAllResultFlags()
    {
        _bus.Load(0x8000, [0x2C, 0x34, 0x12]);
        _bus.Write(0x1234, 0xC0);
        SetPc(0x8000);
        SetA(0x0F);

        Assert.Equal(4UL, _cpu.Step());
        Assert.True(GetFlag('Z'));
        Assert.True(GetFlag('V'));
        Assert.True(GetFlag('N'));
    }

    public static TheoryData<byte, string, string, ulong> MemoryRmwCases()
    {
        var cases = new TheoryData<byte, string, string, ulong>();
        AddRmwFamily(cases, "INC", [0xE6, 0xF6, 0xEE, 0xFE]);
        AddRmwFamily(cases, "DEC", [0xC6, 0xD6, 0xCE, 0xDE]);
        AddRmwFamily(cases, "ASL", [0x06, 0x16, 0x0E, 0x1E]);
        AddRmwFamily(cases, "LSR", [0x46, 0x56, 0x4E, 0x5E]);
        AddRmwFamily(cases, "ROL", [0x26, 0x36, 0x2E, 0x3E]);
        AddRmwFamily(cases, "ROR", [0x66, 0x76, 0x6E, 0x7E]);
        return cases;
    }

    private static void AddRmwFamily(
        TheoryData<byte, string, string, ulong> cases, string instruction, byte[] opcodes)
    {
        string[] modes = ["ZeroPage", "ZeroPageX", "Absolute", "AbsoluteX"];
        ulong[] cycles = [5, 6, 6, 7];
        for (var i = 0; i < opcodes.Length; i++) cases.Add(opcodes[i], instruction, modes[i], cycles[i]);
    }

    [Theory]
    [MemberData(nameof(MemoryRmwCases))]
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
        ushort effectiveAddress;
        switch (addressingMode)
        {
            case "ZeroPage":
                effectiveAddress = 0x0042;
                _bus.Load(0x8000, [opcode, 0x42]);
                break;
            case "ZeroPageX":
                effectiveAddress = 0x0042;
                _bus.Load(0x8000, [opcode, 0x41]);
                break;
            case "Absolute":
                effectiveAddress = 0x1234;
                _bus.Load(0x8000, [opcode, 0x34, 0x12]);
                break;
            case "AbsoluteX":
                effectiveAddress = 0x1234;
                _bus.Load(0x8000, [opcode, 0x33, 0x12]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(addressingMode));
        }
        _bus.Write(effectiveAddress, operand);

        Assert.Equal(expectedCycles, _cpu.Step());
        Assert.Equal(expected, _bus.Read(effectiveAddress));
        Assert.Equal(expectedCarry, GetFlag('C'));
        Assert.Equal(expectedZero, GetFlag('Z'));
        Assert.Equal(expectedNegative, GetFlag('N'));
    }

    public static TheoryData<byte, char, string, ulong> StoreAddressingCases()
    {
        return new TheoryData<byte, char, string, ulong>
        {
            { 0x85, 'A', "ZeroPage", 3 }, { 0x95, 'A', "ZeroPageX", 4 },
            { 0x8D, 'A', "Absolute", 4 }, { 0x9D, 'A', "AbsoluteX", 5 },
            { 0x99, 'A', "AbsoluteY", 5 }, { 0x81, 'A', "IndirectX", 6 },
            { 0x91, 'A', "IndirectY", 6 },
            { 0x86, 'X', "ZeroPage", 3 }, { 0x96, 'X', "ZeroPageY", 4 },
            { 0x8E, 'X', "Absolute", 4 },
            { 0x84, 'Y', "ZeroPage", 3 }, { 0x94, 'Y', "ZeroPageX", 4 },
            { 0x8C, 'Y', "Absolute", 4 }
        };
    }

    [Theory]
    [MemberData(nameof(StoreAddressingCases))]
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
            case "ZeroPage": effectiveAddress = 0x0042; _bus.Load(0x8000, [opcode, 0x42]); break;
            case "ZeroPageX": effectiveAddress = 0x0042; _bus.Load(0x8000, [opcode, 0x41]); break;
            case "ZeroPageY": effectiveAddress = 0x0042; _bus.Load(0x8000, [opcode, 0x41]); break;
            case "Absolute": effectiveAddress = 0x1234; _bus.Load(0x8000, [opcode, 0x34, 0x12]); break;
            case "AbsoluteX": effectiveAddress = 0x1234; _bus.Load(0x8000, [opcode, 0x33, 0x12]); break;
            case "AbsoluteY": effectiveAddress = 0x1234; _bus.Load(0x8000, [opcode, 0x33, 0x12]); break;
            case "IndirectX":
                effectiveAddress = 0x1234;
                _bus.Load(0x8000, [opcode, 0x41]);
                _bus.Write(0x0042, 0x34); _bus.Write(0x0043, 0x12);
                break;
            case "IndirectY":
                effectiveAddress = 0x1234;
                _bus.Load(0x8000, [opcode, 0x40]);
                _bus.Write(0x0040, 0x33); _bus.Write(0x0041, 0x12);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(addressingMode));
        }

        Assert.Equal(expectedCycles, _cpu.Step());
        Assert.Equal(value, _bus.Read(effectiveAddress));
        Assert.Equal(0xE3, GetP());
    }

    public static TheoryData<byte> DecodedOpcodes()
    {
        byte[] opcodes =
        [
            0x00, 0x01, 0x04, 0x05, 0x06, 0x08, 0x09, 0x0A, 0x0C, 0x0D, 0x0E,
            0x10, 0x11, 0x14, 0x15, 0x16, 0x18, 0x19, 0x1C, 0x1D, 0x1E,
            0x20, 0x21, 0x24, 0x25, 0x26, 0x28, 0x29, 0x2A, 0x2C, 0x2D, 0x2E,
            0x30, 0x31, 0x34, 0x35, 0x36, 0x38, 0x39, 0x3C, 0x3D, 0x3E,
            0x40, 0x41, 0x44, 0x45, 0x46, 0x48, 0x49, 0x4A, 0x4C, 0x4D, 0x4E,
            0x50, 0x51, 0x54, 0x55, 0x56, 0x58, 0x59, 0x5C, 0x5D, 0x5E,
            0x60, 0x61, 0x64, 0x65, 0x66, 0x68, 0x69, 0x6A, 0x6C, 0x6D, 0x6E,
            0x70, 0x71, 0x74, 0x75, 0x76, 0x78, 0x79, 0x7C, 0x7D, 0x7E,
            0x80, 0x81, 0x82, 0x84, 0x85, 0x86, 0x88, 0x89, 0x8A, 0x8C, 0x8D, 0x8E,
            0x90, 0x91, 0x94, 0x95, 0x96, 0x98, 0x99, 0x9A, 0x9D,
            0xA0, 0xA1, 0xA2, 0xA4, 0xA5, 0xA6, 0xA8, 0xA9, 0xAA, 0xAC, 0xAD, 0xAE,
            0xB0, 0xB1, 0xB4, 0xB5, 0xB6, 0xB8, 0xB9, 0xBA, 0xBC, 0xBD, 0xBE,
            0xC0, 0xC1, 0xC2, 0xC4, 0xC5, 0xC6, 0xC8, 0xC9, 0xCA, 0xCC, 0xCD, 0xCE,
            0xD0, 0xD1, 0xD4, 0xD5, 0xD6, 0xD8, 0xD9, 0xDC, 0xDD, 0xDE,
            0xE0, 0xE1, 0xE2, 0xE4, 0xE5, 0xE6, 0xE8, 0xE9, 0xEA, 0xEC, 0xED, 0xEE,
            0xF0, 0xF1, 0xF4, 0xF5, 0xF6, 0xF8, 0xF9, 0xFC, 0xFD, 0xFE
        ];

        var cases = new TheoryData<byte>();
        foreach (var opcode in opcodes) cases.Add(opcode);
        return cases;
    }

    [Theory]
    [MemberData(nameof(DecodedOpcodes))]
    public void EveryDecodedOpcode_CanExecuteToCompletion(byte opcode)
    {
        _bus.Load(0x8000, [opcode, 0x00, 0x20]);
        _bus.Write(0xFFFE, 0x00);
        _bus.Write(0xFFFF, 0x90);
        SetPc(0x8000);
        SetSp(0xFD);

        var cycles = _cpu.Step();

        Assert.InRange(cycles, 2UL, 7UL);
        Assert.Equal(0, GetCycles());
    }

    [Theory]
    [MemberData(nameof(IndexedReadCycleCases))]
    public void IndexedReadInstructions_AddOneCycleOnlyWhenCrossingAPage(
        byte opcode, string addressingMode, bool crossesPage, ulong expectedCycles)
    {
        SetPc(0x8000);
        SetA(0x55);
        SetX(1);
        SetY(1);

        var lowByte = crossesPage ? (byte)0xFF : (byte)0x10;
        if (addressingMode == "IndirectY")
        {
            _bus.Load(0x8000, [opcode, 0x40]);
            _bus.Write(0x0040, lowByte);
            _bus.Write(0x0041, 0x20);
        }
        else
        {
            _bus.Load(0x8000, [opcode, lowByte, 0x20]);
        }

        Assert.Equal(expectedCycles, _cpu.Step());
        Assert.Equal(0, GetCycles());
    }

    [Theory]
    [InlineData(0x9D, 5)] // STA abs,X
    [InlineData(0x99, 5)] // STA abs,Y
    [InlineData(0x91, 6)] // STA (indirect),Y
    public void IndexedStoreInstructions_HaveFixedCyclesAcrossPageBoundaries(byte opcode, ulong expectedCycles)
    {
        SetPc(0x8000);
        SetA(0x42);
        SetX(1);
        SetY(1);

        if (opcode == 0x91)
        {
            _bus.Load(0x8000, [opcode, 0x40]);
            _bus.Write(0x0040, 0xFF);
            _bus.Write(0x0041, 0x20);
        }
        else
        {
            _bus.Load(0x8000, [opcode, 0xFF, 0x20]);
        }

        Assert.Equal(expectedCycles, _cpu.Step());
        Assert.Equal(0x42, _bus.Read(0x2100));
    }

    [Theory]
    [InlineData(0xA2, 'X', new byte[] { 0x80 }, 0, 0, 0, 2)]          // LDX immediate
    [InlineData(0xA6, 'X', new byte[] { 0x42 }, 0, 0, 0x0042, 3)]     // LDX zero page
    [InlineData(0xB6, 'X', new byte[] { 0x42 }, 0, 1, 0x0043, 4)]     // LDX zero page,Y
    [InlineData(0xAE, 'X', new byte[] { 0x34, 0x12 }, 0, 0, 0x1234, 4)] // LDX absolute
    [InlineData(0xBE, 'X', new byte[] { 0x34, 0x12 }, 0, 1, 0x1235, 4)] // LDX absolute,Y
    [InlineData(0xA0, 'Y', new byte[] { 0x80 }, 0, 0, 0, 2)]          // LDY immediate
    [InlineData(0xA4, 'Y', new byte[] { 0x42 }, 0, 0, 0x0042, 3)]     // LDY zero page
    [InlineData(0xB4, 'Y', new byte[] { 0x42 }, 1, 0, 0x0043, 4)]     // LDY zero page,X
    [InlineData(0xAC, 'Y', new byte[] { 0x34, 0x12 }, 0, 0, 0x1234, 4)] // LDY absolute
    [InlineData(0xBC, 'Y', new byte[] { 0x34, 0x12 }, 1, 0, 0x1235, 4)] // LDY absolute,X
    public void LoadIndexInstructions_LoadEveryAddressingModeAndSetFlags(
        byte opcode, char register, byte[] operands, byte initialX, byte initialY,
        ushort effectiveAddress, ulong expectedCycles)
    {
        _bus.Load(0x8000, new byte[] { opcode }.Concat(operands).ToArray());
        if (effectiveAddress != 0) _bus.Write(effectiveAddress, 0x80);
        SetPc(0x8000);
        SetX(initialX);
        SetY(initialY);
        SetP(0);

        Assert.Equal(expectedCycles, _cpu.Step());
        Assert.Equal(0x80, register == 'X' ? GetX() : GetY());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
    }

    [Theory]
    [InlineData(0xBE, 'X')] // LDX absolute,Y
    [InlineData(0xBC, 'Y')] // LDY absolute,X
    public void LoadIndexAbsoluteIndexed_AddsCycleWhenCrossingPage(byte opcode, char register)
    {
        _bus.Load(0x8000, [opcode, 0xFF, 0x20]);
        _bus.Write(0x2100, 0x42);
        SetPc(0x8000);
        SetX(1);
        SetY(1);

        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0x42, register == 'X' ? GetX() : GetY());
    }

    [Fact]
    public void JMP_Absolute_SetsProgramCounterAndTakesThreeCycles()
    {
        _bus.Load(0x8000, [0x4C, 0x34, 0x12]);
        SetPc(0x8000);

        Assert.Equal(3UL, _cpu.Step());
        Assert.Equal(0x1234, GetPc());
    }

    [Fact]
    public void JMP_Indirect_Uses6502PageBoundaryWrapAndTakesFiveCycles()
    {
        _bus.Load(0x8000, [0x6C, 0xFF, 0x12]);
        _bus.Write(0x12FF, 0x34);
        _bus.Write(0x1200, 0x12);
        _bus.Write(0x1300, 0x99);
        SetPc(0x8000);

        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0x1234, GetPc());
    }

    [Fact]
    public void UnsupportedOpcode_CompletesWithoutCrashing()
    {
        // Arrange
        _bus.Load(0x8000, [0x02]);
        SetPc(0x8000);

        // Act
        var cycles = _cpu.Step();

        // Assert
        Assert.Equal(2UL, cycles);
        Assert.Equal(0x8001, GetPc());
        Assert.Equal(0, GetCycles());
    }

    [Fact]
    public void IsOnOddCycle_ReturnsTrueAfterOddMasterClock()
    {
        // Arrange
        _bus.Load(0x8000, [0xEA]);
        SetPc(0x8000);

        // Act
        _cpu.Clock(1);

        // Assert
        Assert.True(_cpu.IsOnOddCycle());
    }

    [Fact]
    public void LDA_AbsoluteX_AddsCycleWhenEffectiveAddressCrossesPageBoundary()
    {
        // Arrange: $20FF + X crosses into page $21.
        _bus.Load(0x8000, [0xBD, 0xFF, 0x20]);
        _bus.Write(0x2100, 0x42);
        SetPc(0x8000);
        SetX(1);

        // Act
        var cycles = _cpu.Step();

        // Assert
        Assert.Equal(5UL, cycles);
        Assert.Equal(0x42, GetA());
        Assert.Equal(0, GetCycles());
    }

    [Theory]
    [InlineData(0x10, 'N', false)] // BPL
    [InlineData(0x30, 'N', true)]  // BMI
    [InlineData(0x50, 'V', false)] // BVC
    [InlineData(0x70, 'V', true)]  // BVS
    [InlineData(0x90, 'C', false)] // BCC
    [InlineData(0xB0, 'C', true)]  // BCS
    [InlineData(0xD0, 'Z', false)] // BNE
    [InlineData(0xF0, 'Z', true)]  // BEQ
    public void EveryBranchCondition_HandlesTakenAndNotTakenPaths(byte opcode, char flag, bool takenValue)
    {
        _bus.Load(0x8000, [opcode, 0x02]);
        SetPc(0x8000);
        SetP(0);
        SetFlag(flag, takenValue);

        Assert.Equal(3UL, _cpu.Step());
        Assert.Equal(0x8004, GetPc());

        SetPc(0x8000);
        SetFlag(flag, !takenValue);

        Assert.Equal(2UL, _cpu.Step());
        Assert.Equal(0x8002, GetPc());
    }

    [Fact]
    public void ZeroPageX_AddressingWrapsFromFFTo00()
    {
        _bus.Load(0x8000, [0xB5, 0xFF]);
        _bus.Write(0x0000, 0x42);
        SetPc(0x8000);
        SetX(1);

        Assert.Equal(4UL, _cpu.Step());
        Assert.Equal(0x42, GetA());
    }

    [Fact]
    public void ZeroPageY_AddressingWrapsFromFFTo00()
    {
        _bus.Load(0x8000, [0xB6, 0xFF]);
        _bus.Write(0x0000, 0x42);
        SetPc(0x8000);
        SetY(1);

        Assert.Equal(4UL, _cpu.Step());
        Assert.Equal(0x42, GetX());
    }

    [Fact]
    public void IndexedIndirect_PointerIndexWrapsWithinZeroPage()
    {
        _bus.Load(0x8000, [0xA1, 0xFF]);
        _bus.Write(0x0000, 0x34);
        _bus.Write(0x0001, 0x12);
        _bus.Write(0x1234, 0x42);
        SetPc(0x8000);
        SetX(1);

        Assert.Equal(6UL, _cpu.Step());
        Assert.Equal(0x42, GetA());
    }

    [Fact]
    public void IndirectIndexed_PointerReadWrapsWithinZeroPage()
    {
        _bus.Load(0x8000, [0xB1, 0xFF]);
        _bus.Write(0x00FF, 0x33);
        _bus.Write(0x0000, 0x12);
        _bus.Write(0x1234, 0x42);
        SetPc(0x8000);
        SetY(1);

        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0x42, GetA());
    }

    [Fact]
    public void AbsoluteIndexed_AddressWrapsFromFFFFTo0000()
    {
        _bus.Load(0x8000, [0xBD, 0xFF, 0xFF]);
        _bus.Write(0x0000, 0x42);
        SetPc(0x8000);
        SetX(1);

        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0x42, GetA());
    }

    [Fact]
    public void OperandFetch_WrapsProgramCounterFromFFFFTo0000()
    {
        _bus.Write(0xFFFF, 0xA9);
        _bus.Write(0x0000, 0x42);
        SetPc(0xFFFF);

        Assert.Equal(2UL, _cpu.Step());
        Assert.Equal(0x42, GetA());
        Assert.Equal(0x0001, GetPc());
    }

    [Fact]
    public void NmiTakesPriorityWhenNmiAndIrqAreBothPending()
    {
        _bus.Write(0xFFFA, 0x00); _bus.Write(0xFFFB, 0x90);
        _bus.Write(0xFFFE, 0x00); _bus.Write(0xFFFF, 0xA0);
        SetPc(0x8000);
        SetSp(0xFD);
        SetP(0);
        SetCycles(1);
        _interrupts.Nmi = true;
        _interrupts.Irq = true;

        _cpu.Clock();
        Assert.Equal(7UL, _cpu.Step());
        Assert.Equal(0x9000, GetPc());
        Assert.False(_interrupts.Nmi);
        Assert.True(_interrupts.Irq);
    }

    [Fact]
    public void IrqIsPolledOnlyAfterCurrentInstructionCompletes()
    {
        _bus.Load(0x8000, [0xEA]);
        _bus.Write(0xFFFE, 0x00); _bus.Write(0xFFFF, 0x90);
        SetPc(0x8000);
        SetP(0);

        _cpu.Clock();
        _interrupts.Irq = true;
        _cpu.Clock();

        Assert.Equal(0x8001, GetPc());
        Assert.Equal(0, GetCycles());

        _cpu.Clock();
        Assert.Equal(0x9000, GetPc());
        Assert.Equal(6, GetCycles());
    }

    [Fact]
    public void ClockDoesNotFetchNextOpcodeWhileCyclesRemain()
    {
        _bus.Load(0x8000, [0xEA, 0xE8]);
        SetPc(0x8000);
        SetX(0);

        _cpu.Clock();
        _cpu.Clock();

        Assert.Equal(0x8001, GetPc());
        Assert.Equal(0, GetX());
        Assert.Equal(0, GetCycles());
    }

    [Fact]
    public void StallAddsToCyclesAlreadyRemaining()
    {
        _bus.Load(0x8000, [0xEA, 0xE8]);
        SetPc(0x8000);

        _cpu.Clock();
        _cpu.Stall(3);
        Clock(4);

        Assert.Equal(0x8001, GetPc());
        Assert.Equal(0, GetCycles());
    }

    [Fact]
    public void IsOnOddCycle_ReturnsFalseAfterEvenMasterClock()
    {
        _bus.Load(0x8000, [0xEA]);
        SetPc(0x8000);

        _cpu.Clock(2);

        Assert.False(_cpu.IsOnOddCycle());
    }

    [Fact]
    public void ConsecutiveSteps_ReportCyclesForEachInstructionIndependently()
    {
        _bus.Load(0x8000, [0xEA, 0x48]);
        SetPc(0x8000);

        Assert.Equal(2UL, _cpu.Step());
        Assert.Equal(3UL, _cpu.Step());
        Assert.Equal(0x8002, GetPc());
    }

    [Fact]
    public void DecimalFlagDoesNotChangeAdcBehaviorOnNesCpu()
    {
        _bus.Load(0x8000, [0x69, 0x01]);
        SetPc(0x8000);
        SetA(0x09);
        SetP(0);
        SetFlag('D', true);

        Assert.Equal(2UL, _cpu.Step());
        Assert.Equal(0x0A, GetA());
        Assert.True(GetFlag('D'));
    }

    public static TheoryData<byte, byte[], byte, ulong> UnofficialNopCases()
    {
        var cases = new TheoryData<byte, byte[], byte, ulong>();
        foreach (var opcode in new byte[] { 0x04, 0x44, 0x64 }) cases.Add(opcode, [0x42], 0, 3);
        cases.Add(0x0C, [0x34, 0x12], 0, 4);
        foreach (var opcode in new byte[] { 0x14, 0x34, 0x54, 0x74, 0xD4, 0xF4 }) cases.Add(opcode, [0x42], 1, 4);
        foreach (var opcode in new byte[] { 0x1C, 0x3C, 0x5C, 0x7C, 0xDC, 0xFC }) cases.Add(opcode, [0x34, 0x12], 1, 4);
        foreach (var opcode in new byte[] { 0x80, 0x82, 0x89, 0xC2, 0xE2 }) cases.Add(opcode, [0x42], 0, 2);
        return cases;
    }

    [Theory]
    [MemberData(nameof(UnofficialNopCases))]
    public void UnofficialNops_ConsumeOperandsAndDocumentedCycles(
        byte opcode, byte[] operands, byte initialX, ulong expectedCycles)
    {
        _bus.Load(0x8000, new byte[] { opcode }.Concat(operands).ToArray());
        SetPc(0x8000);
        SetX(initialX);
        SetA(0x55);
        SetP(0xA5);

        Assert.Equal(expectedCycles, _cpu.Step());
        Assert.Equal((ushort)(0x8001 + operands.Length), GetPc());
        Assert.Equal(0x55, GetA());
        Assert.Equal(0xA5, GetP());
    }

    [Theory]
    [InlineData(0xA2, 'X')]
    [InlineData(0xA0, 'Y')]
    public void LoadIndexImmediate_SetsZeroFlagForZero(byte opcode, char register)
    {
        _bus.Load(0x8000, [opcode, 0x00]);
        SetPc(0x8000);
        SetP(0);

        Assert.Equal(2UL, _cpu.Step());
        Assert.Equal(0, register == 'X' ? GetX() : GetY());
        Assert.True(GetFlag('Z'));
        Assert.False(GetFlag('N'));
    }

    [Fact]
    public void PLA_SetsZeroFlagWhenPullingZero()
    {
        _bus.Load(0x8000, [0x68]);
        _bus.Write(0x0100, 0x00);
        SetPc(0x8000);
        SetSp(0xFF);
        SetP(0x80);

        Assert.Equal(4UL, _cpu.Step());
        Assert.Equal(0x00, GetA());
        Assert.True(GetFlag('Z'));
        Assert.False(GetFlag('N'));
        Assert.Equal(0x00, GetSp());
    }

    [Fact]
    public void PHA_StackPointerWrapsFrom00ToFF()
    {
        _bus.Load(0x8000, [0x48]);
        SetPc(0x8000);
        SetSp(0x00);
        SetA(0x42);

        Assert.Equal(3UL, _cpu.Step());
        Assert.Equal(0x42, _bus.Read(0x0100));
        Assert.Equal(0xFF, GetSp());
    }

    [Fact]
    public void JMP_IndirectWithoutPageBoundaryReadsConsecutiveBytes()
    {
        _bus.Load(0x8000, [0x6C, 0x34, 0x12]);
        _bus.Write(0x1234, 0x78);
        _bus.Write(0x1235, 0x56);
        SetPc(0x8000);

        Assert.Equal(5UL, _cpu.Step());
        Assert.Equal(0x5678, GetPc());
    }

    [Fact]
    public void ResetDelayCompletesBeforeFirstOpcodeExecutes()
    {
        _bus.Write(0xFFFC, 0x00); _bus.Write(0xFFFD, 0x80);
        _bus.Load(0x8000, [0xE8]);
        _cpu.Reset();

        Assert.Equal(7UL, _cpu.Step());
        Assert.Equal(0, GetX());
        Assert.Equal(0x8000, GetPc());

        Assert.Equal(2UL, _cpu.Step());
        Assert.Equal(1, GetX());
        Assert.Equal(0x8001, GetPc());
    }

    [Fact]
    public void IrqLineRemainsAssertedAfterInterruptIsServiced()
    {
        _bus.Write(0xFFFE, 0x00); _bus.Write(0xFFFF, 0x90);
        SetPc(0x8000);
        SetP(0);
        _interrupts.Irq = true;

        Assert.Equal(7UL, _cpu.Step());
        Assert.True(_interrupts.Irq);
        Assert.Equal(0x9000, GetPc());
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
    private int GetCycles() => GetField<int>("_cycles");
    private void SetCycles(byte value) => SetField("_cycles", (int)value);
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
