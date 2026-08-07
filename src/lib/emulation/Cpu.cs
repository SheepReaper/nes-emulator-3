using System;
using System.Diagnostics;

using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;

public sealed class Cpu(InterruptLines interrupts) : IBusMaster
{
    private IBus? _bus;

    // Registers
    private byte _a;                // Accumulator
    private byte _x;                // Index Register X
    private byte _y;                // Index Register Y
    private byte _sp;               // Stack Pointer
    private ushort _pc;             // Program Counter
    private ProcessorStatus _p;     // Processor Status

    // For debugging and internal access
    // Internal state for instruction execution
    private byte _cycles;           // Cycles remaining for the current instruction
    private byte _opcode;           // Current opcode being executed
    private ulong _masterClock;     // Tracks the master clock for odd/even cycle checks

    public void ConnectBus(IBus bus)
    {
        _bus = bus;
    }

    public void Reset()
    {
        Debug.Assert(_bus != null, "CPU bus is not connected.");

        // Read the 16-bit address from the reset vector ($FFFC)
        var lo = _bus.Read(0xFFFC);
        var hi = _bus.Read(0xFFFD);
        _pc = (ushort)((hi << 8) | lo);

        // Set initial register states
        _a = 0;
        _x = 0;
        _y = 0;
        _sp = 0xFD;
        // Set I flag, clear others. Bit 5 is unused and always 1.
        _p.Value = 0b0010_0100;

        // Reset takes 8 cycles
        _cycles = 8;
    }

    public void Clock(ulong masterClock)
    {
        Debug.Assert(_bus != null, "CPU bus is not connected.");

        _masterClock = masterClock;

        if (_cycles == 0) // Instruction has just completed, or CPU was idle. Time to fetch new instruction.
        {
            // Check for interrupts before fetching the next instruction
            if (interrupts.Nmi)
            {
                HandleNmi();
                interrupts.Nmi = false;
                // NMI sets _cycles to 7. We consume 1 cycle here.
                _cycles--; // Consume the first cycle of the interrupt sequence
                return;
            }
            else if (interrupts.Irq && !_p.InterruptDisable)
            {
                HandleIrq();
                // The IRQ line is not cleared by the CPU, but by the device that asserted it.
                // IRQ sets _cycles to 7. We consume 1 cycle here.
                _cycles--; // Consume the first cycle of the interrupt sequence
                return;
            }

            // Fetch opcode
            _opcode = Read(_pc++);

            // Execute instruction and set cycles
            Action instruction = _opcode switch
            {
                0xEA => NOP, // NOP - Implied

                // LDA (Load Accumulator) instructions
                0xA9 => LDA_IMM, // LDA - Immediate
                0xA5 => LDA_ZP,  // LDA - Zero Page
                0xB5 => LDA_ZPX, // LDA - Zero Page, X
                0xAD => LDA_ABS, // LDA - Absolute
                0xBD => LDA_ABSX, // LDA - Absolute, X
                0xB9 => LDA_ABSY, // LDA - Absolute, Y
                0xA1 => LDA_INDX, // LDA - Indirect, X
                0xB1 => LDA_INDY, // LDA - Indirect, Y

                // Logical instructions
                0x29 => AND_IMM, 0x25 => AND_ZP, 0x35 => AND_ZPX, 0x2D => AND_ABS, 0x3D => AND_ABSX, 0x39 => AND_ABSY, 0x21 => AND_INDX, 0x31 => AND_INDY, // AND
                0x49 => EOR_IMM, 0x45 => EOR_ZP, 0x55 => EOR_ZPX, 0x4D => EOR_ABS, 0x5D => EOR_ABSX, 0x59 => EOR_ABSY, 0x41 => EOR_INDX, 0x51 => EOR_INDY, // EOR
                0x09 => ORA_IMM, 0x05 => ORA_ZP, 0x15 => ORA_ZPX, 0x0D => ORA_ABS, 0x1D => ORA_ABSX, 0x19 => ORA_ABSY, 0x01 => ORA_INDX, 0x11 => ORA_INDY, // ORA

                // Arithmetic instructions
                0x69 => ADC_IMM, 0x65 => ADC_ZP, 0x75 => ADC_ZPX, 0x6D => ADC_ABS, 0x7D => ADC_ABSX, 0x79 => ADC_ABSY, 0x61 => ADC_INDX, 0x71 => ADC_INDY, // ADC
                0xE9 => SBC_IMM, 0xE5 => SBC_ZP, 0xF5 => SBC_ZPX, 0xED => SBC_ABS, 0xFD => SBC_ABSX, 0xF9 => SBC_ABSY, 0xE1 => SBC_INDX, 0xF1 => SBC_INDY, // SBC

                // STA (Store Accumulator) instructions
                0x85 => STA_ZP,   // STA - Zero Page
                0x95 => STA_ZPX,  // STA - Zero Page, X
                0x8D => STA_ABS,  // STA - Absolute
                0x9D => STA_ABSX, // STA - Absolute, X
                0x99 => STA_ABSY, // STA - Absolute, Y
                0x81 => STA_INDX, // STA - (Indirect, X)
                0x91 => STA_INDY, // STA - (Indirect), Y

                // STX / STY (Store Index) instructions
                0x86 => STX_ZP, 0x96 => STX_ZPY, 0x8E => STX_ABS, // STX
                0x84 => STY_ZP, 0x94 => STY_ZPX, 0x8C => STY_ABS, // STY

                // Increment/Decrement instructions
                0xE6 => INC_ZP, 0xF6 => INC_ZPX, 0xEE => INC_ABS, 0xFE => INC_ABSX, // INC
                0xC6 => DEC_ZP, 0xD6 => DEC_ZPX, 0xCE => DEC_ABS, 0xDE => DEC_ABSX, // DEC
                0xE8 => INX_IMP, 0xC8 => INY_IMP, // INX, INY
                0xCA => DEX_IMP, 0x88 => DEY_IMP, // DEX, DEY

                // Shift and Rotate instructions
                0x0A => ASL_ACC, 0x06 => ASL_ZP, 0x16 => ASL_ZPX, 0x0E => ASL_ABS, 0x1E => ASL_ABSX, // ASL
                0x4A => LSR_ACC, 0x46 => LSR_ZP, 0x56 => LSR_ZPX, 0x4E => LSR_ABS, 0x5E => LSR_ABSX, // LSR
                0x2A => ROL_ACC, 0x26 => ROL_ZP, 0x36 => ROL_ZPX, 0x2E => ROL_ABS, 0x3E => ROL_ABSX, // ROL
                0x6A => ROR_ACC, 0x66 => ROR_ZP, 0x76 => ROR_ZPX, 0x6E => ROR_ABS, 0x7E => ROR_ABSX, // ROR

                // Compare and Bit Test instructions
                0xC9 => CMP_IMM, 0xC5 => CMP_ZP, 0xD5 => CMP_ZPX, 0xCD => CMP_ABS, 0xDD => CMP_ABSX, 0xD9 => CMP_ABSY, 0xC1 => CMP_INDX, 0xD1 => CMP_INDY, // CMP
                0xE0 => CPX_IMM, 0xE4 => CPX_ZP, 0xEC => CPX_ABS, // CPX
                0xC0 => CPY_IMM, 0xC4 => CPY_ZP, 0xCC => CPY_ABS, // CPY
                0x24 => BIT_ZP, 0x2C => BIT_ABS, // BIT

                // Register Transfer instructions
                0xAA => TAX_IMP, // TAX
                0xA8 => TAY_IMP, // TAY
                0x8A => TXA_IMP, // TXA
                0x98 => TYA_IMP, // TYA
                0xBA => TSX_IMP, // TSX
                0x9A => TXS_IMP, // TXS

                // Flag Control instructions
                0x18 => CLC_IMP, 0x38 => SEC_IMP, // Carry
                0x58 => CLI_IMP, 0x78 => SEI_IMP, // Interrupt
                0xB8 => CLV_IMP,                  // Overflow
                0xD8 => CLD_IMP, 0xF8 => SED_IMP, // Decimal

                // System instructions
                0x00 => BRK_IMP, 0x40 => RTI_IMP,

                // Stack and Subroutine instructions
                0x20 => JSR_ABS, // JSR - Absolute
                0x60 => RTS_IMP, // RTS - Implied

                // Stack instructions
                0x48 => PHA_IMP, // PHA - Push Accumulator
                0x68 => PLA_IMP, // PLA - Pull Accumulator
                0x08 => PHP_IMP, // PHP - Push Processor Status
                0x28 => PLP_IMP, // PLP - Pull Processor Status
                // Branch instructions
                0x10 => () => Branch(!_p.Negative), // BPL (Branch on Plus)
                0x30 => () => Branch(_p.Negative),  // BMI (Branch on Minus)
                0x50 => () => Branch(!_p.Overflow), // BVC (Branch on Overflow Clear)
                0x70 => () => Branch(_p.Overflow),  // BVS (Branch on Overflow Set)
                0x90 => () => Branch(!_p.Carry),    // BCC (Branch on Carry Clear)
                0xB0 => () => Branch(_p.Carry),     // BCS (Branch on Carry Set)
                0xD0 => () => Branch(!_p.Zero),     // BNE (Branch on Not Equal)
                0xF0 => () => Branch(_p.Zero),      // BEQ (Branch on Equal)

                _ => () => _cycles = 1 // For now, unsupported opcodes will do nothing for 1 cycle
            };

            instruction();
            _cycles--; // Consume the first cycle of this new instruction.
        }
        else
        {
            // We are in the middle of a multi-cycle instruction. Just consume a cycle.
            _cycles--;
        }
    }

    private void HandleNmi()
    {
        // Push PC and status register to stack
        Push((byte)(_pc >> 8));
        Push((byte)(_pc & 0xFF));
        Push((byte)(_p.Value & ~0b00010000)); // Clear B flag

        // Set interrupt disable flag
        _p.InterruptDisable = true;

        // Load PC from NMI vector
        _pc = ReadWord(0xFFFA);
        _cycles = 7;
    }

    private void HandleIrq()
    {
        // Push PC and status register to stack
        Push((byte)(_pc >> 8));
        Push((byte)(_pc & 0xFF));
        Push((byte)(_p.Value & ~0b00010000)); // Clear B flag

        _p.InterruptDisable = true;
        _pc = ReadWord(0xFFFE); // Load PC from IRQ/BRK vector
        _cycles = 7;
    }

    public void Stall(int cycles)
    {
        _cycles += (byte)cycles;
    }

    public bool IsOnOddCycle()
    {
        return (_masterClock % 2) != 0;
    }

    // Helper method to read a byte from the bus
    public byte Read(ushort address)
    {
        Debug.Assert(_bus != null, "CPU bus is not connected.");
        return _bus.Read(address);
    }

    // Helper method to write a byte to the bus
    public void Write(ushort address, byte value)
    {
        Debug.Assert(_bus != null, "CPU bus is not connected.");
        _bus.Write(address, value);
    }

    // Helper method to read a 16-bit word from the bus (little-endian)
    private ushort ReadWord(ushort address)
    {
        byte lo = Read(address);
        byte hi = Read((ushort)(address + 1));
        return (ushort)((hi << 8) | lo);
    }

    // Helper for 6502 indirect addressing bug (page boundary wrap around for low byte of address)
    private ushort ReadWordBug(ushort address)
    {
        byte lo = Read(address);
        // If address is $xxFF, then (address + 1) wraps to $xx00 for the high byte fetch
        byte hi = Read((ushort)((address & 0xFF00) | ((address + 1) & 0x00FF)));
        return (ushort)((hi << 8) | lo);
    }

    // Helper to set Zero and Negative flags based on a value
    private void SetZeroAndNegativeFlags(byte value)
    {
        _p.Zero = (value == 0x00);
        _p.Negative = (value & 0x80) != 0x00; // Check if MSB is set
    }

    // --- Branching ---

    private void Branch(bool condition)
    {
        if (condition)
        {
            // Capture the PC before adding the offset to check for page crossing later.
            ushort oldPc = _pc;
            sbyte offset = (sbyte)Read(_pc++);
            ushort targetAddress = (ushort)(_pc + offset);

            // Branch is taken. Base time is 3 cycles.
            _cycles = 3;

            // If the branch crosses a page boundary, add another cycle.
            if ((oldPc & 0xFF00) != (targetAddress & 0xFF00)) _cycles++;
            _pc = targetAddress;
        }
        else
        {
            // If branch is not taken, we still need to read the offset to advance the PC.
            _pc++;

            // Branch not taken. Total time is 2 cycles.
            _cycles = 2;
        }
    }

    // --- Stack Operations ---

    // Push a byte onto the stack
    private void Push(byte value)
    {
        Write((ushort)(0x0100 + _sp), value);
        _sp--;
    }

    // Pull a byte from the stack
    private byte Pull()
    {
        _sp++;
        return Read((ushort)(0x0100 + _sp));
    }

    // --- Addressing Mode Implementations ---

    // Immediate: operand is the next byte
    private ushort Addr_IMM() => _pc++;

    // Zero Page: operand is at address $00NN
    private ushort Addr_ZP() => Read(_pc++);

    // Zero Page, X: operand is at address $00(NN+X), wraps around zero page
    private ushort Addr_ZPX() => (ushort)((Read(_pc++) + _x) & 0xFF);

    // Zero Page, Y: operand is at address $00(NN+Y), wraps around zero page
    private ushort Addr_ZPY() => (ushort)((Read(_pc++) + _y) & 0xFF);

    // Absolute: operand is at address $NNNN
    private ushort Addr_ABS()
    {
        ushort abs_addr = ReadWord(_pc);
        _pc += 2;
        return abs_addr;
    }

    // Absolute, X (for reads): operand is at address $NNNN+X, potentially crosses page boundary
    private ushort Addr_ABSX(ref byte cycles)
    {
        ushort abs_addr = ReadWord(_pc);
        _pc += 2;
        ushort effective_addr = (ushort)(abs_addr + _x);
        if ((effective_addr & 0xFF00) != (abs_addr & 0xFF00)) { cycles++; } // Page boundary crossed
        return effective_addr;
    }

    // Absolute, X (for writes): operand is at address $NNNN+X. Cycle count is fixed.
    private ushort Addr_ABSX_Write()
    {
        ushort abs_addr = ReadWord(_pc);
        _pc += 2;
        // Note: The effective address calculation still happens, but the cycle count is fixed for writes.
        return (ushort)(abs_addr + _x);
    }

    // Absolute, Y (for reads): operand is at address $NNNN+Y, potentially crosses page boundary
    private ushort Addr_ABSY(ref byte cycles)
    {
        ushort abs_addr = ReadWord(_pc);
        _pc += 2;
        ushort effective_addr = (ushort)(abs_addr + _y);
        if ((effective_addr & 0xFF00) != (abs_addr & 0xFF00)) { cycles++; } // Page boundary crossed
        return effective_addr;
    }

    // Absolute, Y (for writes): operand is at address $NNNN+Y. Cycle count is fixed.
    private ushort Addr_ABSY_Write()
    {
        ushort abs_addr = ReadWord(_pc);
        _pc += 2;
        // Note: The effective address calculation still happens, but the cycle count is fixed for writes.
        return (ushort)(abs_addr + _y);
    }

    // Indirect, X: operand is at address ($00NN+X) (indirect), zero page wraps
    private ushort Addr_INDX()
    {
        byte zp_ptr = Read(_pc++);
        byte effective_zp_ptr = (byte)(zp_ptr + _x); // Zero page wraps around
        return ReadWordBug(effective_zp_ptr); // Apply 6502 indirect addressing bug
    }

    // Indirect, Y (for reads): operand is at address ($00NN)+Y (indirect), potentially crosses page boundary
    private ushort Addr_INDY(ref byte cycles)
    {
        byte zp_ptr = Read(_pc++);
        ushort ptr = ReadWordBug(zp_ptr); // Apply 6502 indirect addressing bug
        ushort effective_addr = (ushort)(ptr + _y);
        if ((effective_addr & 0xFF00) != (ptr & 0xFF00)) { cycles++; } // Page boundary crossed
        return effective_addr;
    }

    // Indirect, Y (for writes): operand is at address ($00NN)+Y. Cycle count is fixed.
    private ushort Addr_INDY_Write()
    {
        byte zp_ptr = Read(_pc++);
        ushort ptr = ReadWordBug(zp_ptr); // Apply 6502 indirect addressing bug
        // Note: The effective address calculation still happens, but the cycle count is fixed for writes.
        return (ushort)(ptr + _y);
    }

    // --- Instruction Implementations ---

    // NOP (No Operation)
    private void NOP()
    {
        _cycles = 2;
    }

    // LDA (Load Accumulator)
    private void LDA_IMM() { _a = Read(Addr_IMM()); SetZeroAndNegativeFlags(_a); _cycles = 2; }
    private void LDA_ZP() { _a = Read(Addr_ZP()); SetZeroAndNegativeFlags(_a); _cycles = 3; }
    private void LDA_ZPX() { _a = Read(Addr_ZPX()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void LDA_ABS() { _a = Read(Addr_ABS()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void LDA_ABSX() { byte baseCycles = 4; _a = Read(Addr_ABSX(ref baseCycles)); SetZeroAndNegativeFlags(_a); _cycles = baseCycles; }
    private void LDA_ABSY() { byte baseCycles = 4; _a = Read(Addr_ABSY(ref baseCycles)); SetZeroAndNegativeFlags(_a); _cycles = baseCycles; }
    private void LDA_INDX() { _a = Read(Addr_INDX()); SetZeroAndNegativeFlags(_a); _cycles = 6; }
    private void LDA_INDY() { byte baseCycles = 5; _a = Read(Addr_INDY(ref baseCycles)); SetZeroAndNegativeFlags(_a); _cycles = baseCycles; }

    // AND (Logical AND)
    private void AND_IMM() { _a &= Read(Addr_IMM()); SetZeroAndNegativeFlags(_a); _cycles = 2; }
    private void AND_ZP() { _a &= Read(Addr_ZP()); SetZeroAndNegativeFlags(_a); _cycles = 3; }
    private void AND_ZPX() { _a &= Read(Addr_ZPX()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void AND_ABS() { _a &= Read(Addr_ABS()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void AND_ABSX() { byte baseCycles = 4; _a &= Read(Addr_ABSX(ref baseCycles)); SetZeroAndNegativeFlags(_a); _cycles = baseCycles; }
    private void AND_ABSY() { byte baseCycles = 4; _a &= Read(Addr_ABSY(ref baseCycles)); SetZeroAndNegativeFlags(_a); _cycles = baseCycles; }
    private void AND_INDX() { _a &= Read(Addr_INDX()); SetZeroAndNegativeFlags(_a); _cycles = 6; }
    private void AND_INDY() { byte baseCycles = 5; _a &= Read(Addr_INDY(ref baseCycles)); SetZeroAndNegativeFlags(_a); _cycles = baseCycles; }

    // EOR (Logical Exclusive OR)
    private void EOR_IMM() { _a ^= Read(Addr_IMM()); SetZeroAndNegativeFlags(_a); _cycles = 2; }
    private void EOR_ZP() { _a ^= Read(Addr_ZP()); SetZeroAndNegativeFlags(_a); _cycles = 3; }
    private void EOR_ZPX() { _a ^= Read(Addr_ZPX()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void EOR_ABS() { _a ^= Read(Addr_ABS()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void EOR_ABSX() { byte baseCycles = 4; _a ^= Read(Addr_ABSX(ref baseCycles)); SetZeroAndNegativeFlags(_a); _cycles = baseCycles; }
    private void EOR_ABSY() { byte baseCycles = 4; _a ^= Read(Addr_ABSY(ref baseCycles)); SetZeroAndNegativeFlags(_a); _cycles = baseCycles; }
    private void EOR_INDX() { _a ^= Read(Addr_INDX()); SetZeroAndNegativeFlags(_a); _cycles = 6; }
    private void EOR_INDY() { byte baseCycles = 5; _a ^= Read(Addr_INDY(ref baseCycles)); SetZeroAndNegativeFlags(_a); _cycles = baseCycles; }

    // ORA (Logical Inclusive OR)
    private void ORA_IMM() { _a |= Read(Addr_IMM()); SetZeroAndNegativeFlags(_a); _cycles = 2; }
    private void ORA_ZP() { _a |= Read(Addr_ZP()); SetZeroAndNegativeFlags(_a); _cycles = 3; }
    private void ORA_ZPX() { _a |= Read(Addr_ZPX()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void ORA_ABS() { _a |= Read(Addr_ABS()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void ORA_ABSX() { byte baseCycles = 4; _a |= Read(Addr_ABSX(ref baseCycles)); SetZeroAndNegativeFlags(_a); _cycles = baseCycles; }
    private void ORA_ABSY() { byte baseCycles = 4; _a |= Read(Addr_ABSY(ref baseCycles)); SetZeroAndNegativeFlags(_a); _cycles = baseCycles; }
    private void ORA_INDX() { _a |= Read(Addr_INDX()); SetZeroAndNegativeFlags(_a); _cycles = 6; }
    private void ORA_INDY() { byte baseCycles = 5; _a |= Read(Addr_INDY(ref baseCycles)); SetZeroAndNegativeFlags(_a); _cycles = baseCycles; }

    // ADC (Add with Carry)
    private void ADC(byte operand)
    {
        ushort sum = (ushort)(_a + operand + (_p.Carry ? 1 : 0));
        _p.Carry = sum > 0xFF;
        _p.Overflow = (~(_a ^ operand) & (_a ^ sum) & 0x80) != 0;
        _a = (byte)sum;
        SetZeroAndNegativeFlags(_a);
    }
    private void ADC_IMM() { ADC(Read(Addr_IMM())); _cycles = 2; }
    private void ADC_ZP() { ADC(Read(Addr_ZP())); _cycles = 3; }
    private void ADC_ZPX() { ADC(Read(Addr_ZPX())); _cycles = 4; }
    private void ADC_ABS() { ADC(Read(Addr_ABS())); _cycles = 4; }
    private void ADC_ABSX() { byte baseCycles = 4; ADC(Read(Addr_ABSX(ref baseCycles))); _cycles = baseCycles; }
    private void ADC_ABSY() { byte baseCycles = 4; ADC(Read(Addr_ABSY(ref baseCycles))); _cycles = baseCycles; }
    private void ADC_INDX() { ADC(Read(Addr_INDX())); _cycles = 6; }
    private void ADC_INDY() { byte baseCycles = 5; ADC(Read(Addr_INDY(ref baseCycles))); _cycles = baseCycles; }

    // SBC (Subtract with Carry) - Implemented as ADC with a complemented operand
    private void SBC_IMM() { ADC((byte)~Read(Addr_IMM())); _cycles = 2; }
    private void SBC_ZP() { ADC((byte)~Read(Addr_ZP())); _cycles = 3; }
    private void SBC_ZPX() { ADC((byte)~Read(Addr_ZPX())); _cycles = 4; }
    private void SBC_ABS() { ADC((byte)~Read(Addr_ABS())); _cycles = 4; }
    private void SBC_ABSX() { byte baseCycles = 4; ADC((byte)~Read(Addr_ABSX(ref baseCycles))); _cycles = baseCycles; }
    private void SBC_ABSY() { byte baseCycles = 4; ADC((byte)~Read(Addr_ABSY(ref baseCycles))); _cycles = baseCycles; }
    private void SBC_INDX() { ADC((byte)~Read(Addr_INDX())); _cycles = 6; }
    private void SBC_INDY() { byte baseCycles = 5; ADC((byte)~Read(Addr_INDY(ref baseCycles))); _cycles = baseCycles; }

    // STA (Store Accumulator)
    private void STA_ZP() { Write(Addr_ZP(), _a); _cycles = 3; }
    private void STA_ZPX() { Write(Addr_ZPX(), _a); _cycles = 4; }
    private void STA_ABS() { Write(Addr_ABS(), _a); _cycles = 4; }
    private void STA_ABSX() { Write(Addr_ABSX_Write(), _a); _cycles = 5; }
    private void STA_ABSY() { Write(Addr_ABSY_Write(), _a); _cycles = 5; }
    private void STA_INDX() { Write(Addr_INDX(), _a); _cycles = 6; }
    private void STA_INDY() { Write(Addr_INDY_Write(), _a); _cycles = 6; }

    // STX (Store Index X)
    private void STX_ZP() { Write(Addr_ZP(), _x); _cycles = 3; }
    private void STX_ZPY() { Write(Addr_ZPY(), _x); _cycles = 4; }
    private void STX_ABS() { Write(Addr_ABS(), _x); _cycles = 4; }

    // STY (Store Index Y)
    private void STY_ZP() { Write(Addr_ZP(), _y); _cycles = 3; }
    private void STY_ZPX() { Write(Addr_ZPX(), _y); _cycles = 4; }
    private void STY_ABS() { Write(Addr_ABS(), _y); _cycles = 4; }

    // INC (Increment Memory)
    private void INC_ZP() { ushort addr = Addr_ZP(); byte val = (byte)(Read(addr) + 1); Write(addr, val); SetZeroAndNegativeFlags(val); _cycles = 5; }
    private void INC_ZPX() { ushort addr = Addr_ZPX(); byte val = (byte)(Read(addr) + 1); Write(addr, val); SetZeroAndNegativeFlags(val); _cycles = 6; }
    private void INC_ABS() { ushort addr = Addr_ABS(); byte val = (byte)(Read(addr) + 1); Write(addr, val); SetZeroAndNegativeFlags(val); _cycles = 6; }
    private void INC_ABSX() { ushort addr = Addr_ABSX_Write(); byte val = (byte)(Read(addr) + 1); Write(addr, val); SetZeroAndNegativeFlags(val); _cycles = 7; }

    // DEC (Decrement Memory)
    private void DEC_ZP() { ushort addr = Addr_ZP(); byte val = (byte)(Read(addr) - 1); Write(addr, val); SetZeroAndNegativeFlags(val); _cycles = 5; }
    private void DEC_ZPX() { ushort addr = Addr_ZPX(); byte val = (byte)(Read(addr) - 1); Write(addr, val); SetZeroAndNegativeFlags(val); _cycles = 6; }
    private void DEC_ABS() { ushort addr = Addr_ABS(); byte val = (byte)(Read(addr) - 1); Write(addr, val); SetZeroAndNegativeFlags(val); _cycles = 6; }
    private void DEC_ABSX() { ushort addr = Addr_ABSX_Write(); byte val = (byte)(Read(addr) - 1); Write(addr, val); SetZeroAndNegativeFlags(val); _cycles = 7; }

    // INX, INY, DEX, DEY (Increment/Decrement Registers)
    private void INX_IMP() { _x++; SetZeroAndNegativeFlags(_x); _cycles = 2; }
    private void INY_IMP() { _y++; SetZeroAndNegativeFlags(_y); _cycles = 2; }
    private void DEX_IMP() { _x--; SetZeroAndNegativeFlags(_x); _cycles = 2; }
    private void DEY_IMP() { _y--; SetZeroAndNegativeFlags(_y); _cycles = 2; }

    // --- Compare and Bit Test ---

    private void Compare(byte register, byte operand)
    {
        byte result = (byte)(register - operand);
        _p.Carry = register >= operand;
        SetZeroAndNegativeFlags(result);
    }

    // CMP (Compare Accumulator)
    private void CMP_IMM() { Compare(_a, Read(Addr_IMM())); _cycles = 2; }
    private void CMP_ZP() { Compare(_a, Read(Addr_ZP())); _cycles = 3; }
    private void CMP_ZPX() { Compare(_a, Read(Addr_ZPX())); _cycles = 4; }
    private void CMP_ABS() { Compare(_a, Read(Addr_ABS())); _cycles = 4; }
    private void CMP_ABSX() { byte baseCycles = 4; Compare(_a, Read(Addr_ABSX(ref baseCycles))); _cycles = baseCycles; }
    private void CMP_ABSY() { byte baseCycles = 4; Compare(_a, Read(Addr_ABSY(ref baseCycles))); _cycles = baseCycles; }
    private void CMP_INDX() { Compare(_a, Read(Addr_INDX())); _cycles = 6; }
    private void CMP_INDY() { byte baseCycles = 5; Compare(_a, Read(Addr_INDY(ref baseCycles))); _cycles = baseCycles; }

    // CPX (Compare X Register)
    private void CPX_IMM() { Compare(_x, Read(Addr_IMM())); _cycles = 2; }
    private void CPX_ZP() { Compare(_x, Read(Addr_ZP())); _cycles = 3; }
    private void CPX_ABS() { Compare(_x, Read(Addr_ABS())); _cycles = 4; }

    // CPY (Compare Y Register)
    private void CPY_IMM() { Compare(_y, Read(Addr_IMM())); _cycles = 2; }
    private void CPY_ZP() { Compare(_y, Read(Addr_ZP())); _cycles = 3; }
    private void CPY_ABS() { Compare(_y, Read(Addr_ABS())); _cycles = 4; }

    // BIT (Test Bits)
    private void BIT(ushort addr)
    {
        byte operand = Read(addr);
        _p.Zero = (_a & operand) == 0;
        _p.Overflow = (operand & 0x40) != 0; // Bit 6 of operand
        _p.Negative = (operand & 0x80) != 0; // Bit 7 of operand
    }
    private void BIT_ZP() { BIT(Addr_ZP()); _cycles = 3; }
    private void BIT_ABS() { BIT(Addr_ABS()); _cycles = 4; }

    // ASL (Arithmetic Shift Left)
    private byte ASL(byte operand) { _p.Carry = (operand & 0x80) != 0; byte result = (byte)(operand << 1); SetZeroAndNegativeFlags(result); return result; }
    private void ASL_ACC() { _a = ASL(_a); _cycles = 2; }
    private void ASL_ZP() { ushort addr = Addr_ZP(); byte val = Read(addr); val = ASL(val); Write(addr, val); _cycles = 5; }
    private void ASL_ZPX() { ushort addr = Addr_ZPX(); byte val = Read(addr); val = ASL(val); Write(addr, val); _cycles = 6; }
    private void ASL_ABS() { ushort addr = Addr_ABS(); byte val = Read(addr); val = ASL(val); Write(addr, val); _cycles = 6; }
    private void ASL_ABSX() { ushort addr = Addr_ABSX_Write(); byte val = Read(addr); val = ASL(val); Write(addr, val); _cycles = 7; }

    // LSR (Logical Shift Right)
    private byte LSR(byte operand) { _p.Carry = (operand & 0x01) != 0; byte result = (byte)(operand >> 1); SetZeroAndNegativeFlags(result); return result; }
    private void LSR_ACC() { _a = LSR(_a); _cycles = 2; }
    private void LSR_ZP() { ushort addr = Addr_ZP(); byte val = Read(addr); val = LSR(val); Write(addr, val); _cycles = 5; }
    private void LSR_ZPX() { ushort addr = Addr_ZPX(); byte val = Read(addr); val = LSR(val); Write(addr, val); _cycles = 6; }
    private void LSR_ABS() { ushort addr = Addr_ABS(); byte val = Read(addr); val = LSR(val); Write(addr, val); _cycles = 6; }
    private void LSR_ABSX() { ushort addr = Addr_ABSX_Write(); byte val = Read(addr); val = LSR(val); Write(addr, val); _cycles = 7; }

    // ROL (Rotate Left)
    private byte ROL(byte operand)
    {
        bool oldCarry = _p.Carry;
        _p.Carry = (operand & 0x80) != 0;
        byte result = (byte)((operand << 1) | (oldCarry ? 1 : 0));
        SetZeroAndNegativeFlags(result);
        return result;
    }
    private void ROL_ACC() { _a = ROL(_a); _cycles = 2; }
    private void ROL_ZP() { ushort addr = Addr_ZP(); byte val = Read(addr); val = ROL(val); Write(addr, val); _cycles = 5; }
    private void ROL_ZPX() { ushort addr = Addr_ZPX(); byte val = Read(addr); val = ROL(val); Write(addr, val); _cycles = 6; }
    private void ROL_ABS() { ushort addr = Addr_ABS(); byte val = Read(addr); val = ROL(val); Write(addr, val); _cycles = 6; }
    private void ROL_ABSX() { ushort addr = Addr_ABSX_Write(); byte val = Read(addr); val = ROL(val); Write(addr, val); _cycles = 7; }

    // ROR (Rotate Right)
    private byte ROR(byte operand)
    {
        bool oldCarry = _p.Carry;
        _p.Carry = (operand & 0x01) != 0;
        byte result = (byte)((operand >> 1) | (oldCarry ? 0x80 : 0));
        SetZeroAndNegativeFlags(result);
        return result;
    }
    private void ROR_ACC() { _a = ROR(_a); _cycles = 2; }
    private void ROR_ZP() { ushort addr = Addr_ZP(); byte val = Read(addr); val = ROR(val); Write(addr, val); _cycles = 5; }
    private void ROR_ZPX() { ushort addr = Addr_ZPX(); byte val = Read(addr); val = ROR(val); Write(addr, val); _cycles = 6; }
    private void ROR_ABS() { ushort addr = Addr_ABS(); byte val = Read(addr); val = ROR(val); Write(addr, val); _cycles = 6; }
    private void ROR_ABSX() { ushort addr = Addr_ABSX_Write(); byte val = Read(addr); val = ROR(val); Write(addr, val); _cycles = 7; }

    // JSR (Jump to Subroutine)
    private void JSR_ABS()
    {
        ushort sub_addr = ReadWord(_pc); // Read the target address
        _pc += 2; // Advance PC past the operand

        Push((byte)((_pc - 1) >> 8)); // Push high byte of return address (PC-1)
        Push((byte)((_pc - 1) & 0xFF)); // Push low byte of return address (PC-1)

        _pc = sub_addr; // Set PC to the subroutine address
        _cycles = 6;
    }

    // RTS (Return from Subroutine)
    private void RTS_IMP()
    {
        ushort lo = Pull(); // Pull low byte of return address
        ushort hi = Pull(); // Pull high byte of return address
        _pc = (ushort)(((hi << 8) | lo) + 1); // Set PC to (pulled address + 1)
        _cycles = 6;
    }

    // PHA (Push Accumulator)
    private void PHA_IMP()
    {
        Push(_a);
        _cycles = 3;
    }

    // PLA (Pull Accumulator)
    private void PLA_IMP()
    {
        _a = Pull();
        SetZeroAndNegativeFlags(_a);
        _cycles = 4;
    }

    // PHP (Push Processor Status)
    private void PHP_IMP()
    {
        // When PHP pushes the status register, the B (Break) flag is always set to 1
        // in the pushed value, and bit 5 (unused) is also always 1.
        Push((byte)(_p.Value | 0b00110000)); // Set B and unused bit 5
        _cycles = 3;
    }

    // PLP (Pull Processor Status)
    private void PLP_IMP()
    {
        _p.Value = Pull();
        // When PLP pulls the status register, the B (Break) flag and bit 5 (unused)
        // are unaffected by the pulled value. They remain as they were.
        // Since our ProcessorStatus struct doesn't have a field for bit 5, it's implicitly 0.
        // We need to ensure bit 5 is always 1 and B flag is always 0 (for normal operation).
        _p.Value = (byte)((_p.Value & ~0b00010000) | 0b00100000); // Clear B flag, set unused bit 5
        _cycles = 4;
    }

    // --- Register Transfer Instructions ---

    // TAX (Transfer Accumulator to X)
    private void TAX_IMP() { _x = _a; SetZeroAndNegativeFlags(_x); _cycles = 2; }
    // TAY (Transfer Accumulator to Y)
    private void TAY_IMP() { _y = _a; SetZeroAndNegativeFlags(_y); _cycles = 2; }
    // TXA (Transfer X to Accumulator)
    private void TXA_IMP() { _a = _x; SetZeroAndNegativeFlags(_a); _cycles = 2; }
    // TYA (Transfer Y to Accumulator)
    private void TYA_IMP() { _a = _y; SetZeroAndNegativeFlags(_a); _cycles = 2; }
    // TSX (Transfer Stack Pointer to X)
    private void TSX_IMP() { _x = _sp; SetZeroAndNegativeFlags(_x); _cycles = 2; }
    // TXS (Transfer X to Stack Pointer)
    private void TXS_IMP() { _sp = _x; _cycles = 2; }

    // --- Flag and System Instructions ---

    // CLC (Clear Carry Flag)
    private void CLC_IMP() { _p.Carry = false; _cycles = 2; }
    // SEC (Set Carry Flag)
    private void SEC_IMP() { _p.Carry = true; _cycles = 2; }
    // CLI (Clear Interrupt Disable)
    private void CLI_IMP() { _p.InterruptDisable = false; _cycles = 2; }
    // SEI (Set Interrupt Disable)
    private void SEI_IMP() { _p.InterruptDisable = true; _cycles = 2; }
    // CLV (Clear Overflow Flag)
    private void CLV_IMP() { _p.Overflow = false; _cycles = 2; }
    // CLD (Clear Decimal Mode)
    private void CLD_IMP() { _p.Decimal = false; _cycles = 2; }
    // SED (Set Decimal Mode)
    private void SED_IMP() { _p.Decimal = true; _cycles = 2; }

    // BRK (Force Break)
    private void BRK_IMP()
    {
        _pc++; // BRK is a 2-byte instruction in practice
        _p.InterruptDisable = true;
        Push((byte)(_pc >> 8));
        Push((byte)(_pc & 0xFF));
        Push((byte)(_p.Value | 0b00110000)); // Set B and unused bit 5

        ushort lo = Read(0xFFFE);
        ushort hi = Read(0xFFFF);
        _pc = (ushort)((hi << 8) | lo);
        _cycles = 7;
    }

    // RTI (Return from Interrupt)
    private void RTI_IMP()
    {
        _p.Value = Pull();
        _p.Value = (byte)((_p.Value & ~0b00010000) | 0b00100000); // Clear B flag, set unused bit 5
        _pc = (ushort)(Pull() | (Pull() << 8));
        _cycles = 6;
    }
}
