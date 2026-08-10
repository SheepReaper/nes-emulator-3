using System;
using System.Diagnostics;

using SR.Emulation.Nes.Abtractions;

namespace SR.Emulation.Nes;

public sealed class Cpu(InterruptLines interrupts) : IBusMaster
{
    private readonly InterruptLines _interrupts = interrupts; // Store the InterruptLines instance
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
    private int _cycles;            // Cycles remaining for the current instruction or DMA stall
    private byte _opcode;           // Current opcode being executed
    private ulong _masterClock;     // Tracks the master clock for odd/even cycle checks
    private ulong _totalCyclesExecuted;
    private bool _nmiPending;
    private bool _delayPendingNmiOneInstruction;
    private bool _irqPending;
    private bool _deferIrqOneInstruction;
    private Action? _endOfInstructionAction;

    public void ConnectBus(IBus bus)
    {
        _bus = bus;
    }

    public void Reset()
    {
        // Read the 16-bit address from the reset vector ($FFFC)
        var lo = Read(0xFFFC);
        var hi = Read(0xFFFD);
        _pc = (ushort)((hi << 8) | lo);

        // Set initial register states
        _a = 0;
        _x = 0;
        _y = 0;
        _sp = 0xFD;
        // Set I flag, clear others. Bit 5 is unused and always 1.
        _p.Value = 0b0010_0100;

        _nmiPending = false;
        _ = _interrupts.ConsumeNmiEdge();
        _delayPendingNmiOneInstruction = false;
        _irqPending = false;
        _deferIrqOneInstruction = false;
        _endOfInstructionAction = null;

        // The 2A03 reset sequence takes 7 cycles before opcode execution begins.
        _cycles = 7;
    }

    public void Clock(ulong masterClock = 0)
    {
        _masterClock = masterClock;

        if (_interrupts.ConsumeNmiEdge())
        {
            _nmiPending = true;
            // The 2A03 polls the latched NMI state before an instruction's final
            // cycle. An edge first observed at the following instruction boundary
            // has missed that poll, so the next opcode must execute before the NMI
            // sequence begins.
            _delayPendingNmiOneInstruction = _interrupts.DelayNmiOneInstruction || _cycles == 0;
            _interrupts.DelayNmiOneInstruction = false;
        }
        if (_cycles == 0)
        {
            // Check for interrupts before fetching the next instruction
            if (_nmiPending)
            {
                if (_delayPendingNmiOneInstruction)
                {
                    _delayPendingNmiOneInstruction = false;
                }
                else
                {
                    HandleNmi();
                    _nmiPending = false;
                    _interrupts.Nmi = false;
                    _cycles--;
                    _totalCyclesExecuted++;
                    return;
                }
            }

            _deferIrqOneInstruction = false;
            if (_irqPending)
            {
                HandleIrq();
                _irqPending = false;
                // The IRQ line is not cleared by the CPU, but by the device that asserted it.
                _cycles--;
                _totalCyclesExecuted++;
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

                // LDX (Load X Register) instructions
                0xA2 => LDX_IMM,
                0xA6 => LDX_ZP,
                0xB6 => LDX_ZPY,
                0xAE => LDX_ABS,
                0xBE => LDX_ABSY,

                // LDY (Load Y Register) instructions
                0xA0 => LDY_IMM,
                0xA4 => LDY_ZP,
                0xB4 => LDY_ZPX,
                0xAC => LDY_ABS,
                0xBC => LDY_ABSX,

                // Logical instructions
                0x29 => AND_IMM,
                0x25 => AND_ZP,
                0x35 => AND_ZPX,
                0x2D => AND_ABS,
                0x3D => AND_ABSX,
                0x39 => AND_ABSY,
                0x21 => AND_INDX,
                0x31 => AND_INDY, // AND
                0x49 => EOR_IMM,
                0x45 => EOR_ZP,
                0x55 => EOR_ZPX,
                0x4D => EOR_ABS,
                0x5D => EOR_ABSX,
                0x59 => EOR_ABSY,
                0x41 => EOR_INDX,
                0x51 => EOR_INDY, // EOR
                0x09 => ORA_IMM,
                0x05 => ORA_ZP,
                0x15 => ORA_ZPX,
                0x0D => ORA_ABS,
                0x1D => ORA_ABSX,
                0x19 => ORA_ABSY,
                0x01 => ORA_INDX,
                0x11 => ORA_INDY, // ORA

                // Arithmetic instructions
                0x69 => ADC_IMM,
                0x65 => ADC_ZP,
                0x75 => ADC_ZPX,
                0x6D => ADC_ABS,
                0x7D => ADC_ABSX,
                0x79 => ADC_ABSY,
                0x61 => ADC_INDX,
                0x71 => ADC_INDY, // ADC
                0xE9 => SBC_IMM,
                0xE5 => SBC_ZP,
                0xF5 => SBC_ZPX,
                0xED => SBC_ABS,
                0xFD => SBC_ABSX,
                0xF9 => SBC_ABSY,
                0xE1 => SBC_INDX,
                0xF1 => SBC_INDY, // SBC

                // STA (Store Accumulator) instructions
                0x85 => STA_ZP,   // STA - Zero Page
                0x95 => STA_ZPX,  // STA - Zero Page, X
                0x8D => STA_ABS,  // STA - Absolute
                0x9D => STA_ABSX, // STA - Absolute, X
                0x99 => STA_ABSY, // STA - Absolute, Y
                0x81 => STA_INDX, // STA - (Indirect, X)
                0x91 => STA_INDY, // STA - (Indirect), Y

                // STX / STY (Store Index) instructions
                0x86 => STX_ZP,
                0x96 => STX_ZPY,
                0x8E => STX_ABS, // STX
                0x84 => STY_ZP,
                0x94 => STY_ZPX,
                0x8C => STY_ABS, // STY

                // Increment/Decrement instructions
                0xE6 => INC_ZP,
                0xF6 => INC_ZPX,
                0xEE => INC_ABS,
                0xFE => INC_ABSX, // INC
                0xC6 => DEC_ZP,
                0xD6 => DEC_ZPX,
                0xCE => DEC_ABS,
                0xDE => DEC_ABSX, // DEC
                0xE8 => INX_IMP,
                0xC8 => INY_IMP, // INX, INY
                0xCA => DEX_IMP,
                0x88 => DEY_IMP, // DEX, DEY

                // Shift and Rotate instructions
                0x0A => ASL_ACC,
                0x06 => ASL_ZP,
                0x16 => ASL_ZPX,
                0x0E => ASL_ABS,
                0x1E => ASL_ABSX, // ASL
                0x4A => LSR_ACC,
                0x46 => LSR_ZP,
                0x56 => LSR_ZPX,
                0x4E => LSR_ABS,
                0x5E => LSR_ABSX, // LSR
                0x2A => ROL_ACC,
                0x26 => ROL_ZP,
                0x36 => ROL_ZPX,
                0x2E => ROL_ABS,
                0x3E => ROL_ABSX, // ROL
                0x6A => ROR_ACC,
                0x66 => ROR_ZP,
                0x76 => ROR_ZPX,
                0x6E => ROR_ABS,
                0x7E => ROR_ABSX, // ROR

                // Compare and Bit Test instructions
                0xC9 => CMP_IMM,
                0xC5 => CMP_ZP,
                0xD5 => CMP_ZPX,
                0xCD => CMP_ABS,
                0xDD => CMP_ABSX,
                0xD9 => CMP_ABSY,
                0xC1 => CMP_INDX,
                0xD1 => CMP_INDY, // CMP
                0xE0 => CPX_IMM,
                0xE4 => CPX_ZP,
                0xEC => CPX_ABS, // CPX
                0xC0 => CPY_IMM,
                0xC4 => CPY_ZP,
                0xCC => CPY_ABS, // CPY
                0x24 => BIT_ZP,
                0x2C => BIT_ABS, // BIT

                // Register Transfer instructions
                0xAA => TAX_IMP, // TAX
                0xA8 => TAY_IMP, // TAY
                0x8A => TXA_IMP, // TXA
                0x98 => TYA_IMP, // TYA
                0xBA => TSX_IMP, // TSX
                0x9A => TXS_IMP, // TXS

                // Flag Control instructions
                0x18 => CLC_IMP,
                0x38 => SEC_IMP, // Carry
                0x58 => CLI_IMP,
                0x78 => SEI_IMP, // Interrupt
                0xB8 => CLV_IMP,                  // Overflow
                0xD8 => CLD_IMP,
                0xF8 => SED_IMP, // Decimal

                // System instructions
                0x00 => BRK_IMP,
                0x40 => RTI_IMP,

                // Stack and Subroutine instructions
                0x4C => JMP_ABS,
                0x6C => JMP_IND,
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

                // Stable unofficial immediate instructions
                0xEB => SBC_IMM,
                0x0B => AAC_IMM,
                0x2B => AAC_IMM,
                0x4B => ASR_IMM,
                0x6B => ARR_IMM,
                0xAB => ATX_IMM,
                0x8B => XAA_IMM,
                0x93 => AHX_INDY,
                0x9B => TAS_ABSY,
                0x9F => AHX_ABSY,
                0xBB => LAS_ABSY,
                0xCB => AXS_IMM,

                // Stable unofficial zero-page instructions
                0x07 => SLO_ZP,
                0x27 => RLA_ZP,
                0x47 => SRE_ZP,
                0x67 => RRA_ZP,
                0x87 => AAX_ZP,
                0xA7 => LAX_ZP,
                0xC7 => DCP_ZP,
                0xE7 => ISC_ZP,

                // Stable unofficial zero-page indexed instructions
                0x17 => SLO_ZPX,
                0x37 => RLA_ZPX,
                0x57 => SRE_ZPX,
                0x77 => RRA_ZPX,
                0x97 => AAX_ZPY,
                0xB7 => LAX_ZPY,
                0xD7 => DCP_ZPX,
                0xF7 => ISC_ZPX,

                // Stable unofficial absolute instructions
                0x0F => SLO_ABS,
                0x2F => RLA_ABS,
                0x4F => SRE_ABS,
                0x6F => RRA_ABS,
                0x8F => AAX_ABS,
                0xAF => LAX_ABS,
                0xCF => DCP_ABS,
                0xEF => ISC_ABS,

                // Stable unofficial absolute indexed instructions
                0x1F => SLO_ABSX,
                0x3F => RLA_ABSX,
                0x5F => SRE_ABSX,
                0x7F => RRA_ABSX,
                0x9C => SYA_ABSX,
                0xDF => DCP_ABSX,
                0xFF => ISC_ABSX,
                0x1B => SLO_ABSY,
                0x3B => RLA_ABSY,
                0x5B => SRE_ABSY,
                0x7B => RRA_ABSY,
                0x9E => SXA_ABSY,
                0xBF => LAX_ABSY,
                0xDB => DCP_ABSY,
                0xFB => ISC_ABSY,

                // Stable unofficial indexed-indirect instructions
                0x03 => SLO_INDX,
                0x23 => RLA_INDX,
                0x43 => SRE_INDX,
                0x63 => RRA_INDX,
                0x83 => AAX_INDX,
                0xA3 => LAX_INDX,
                0xC3 => DCP_INDX,
                0xE3 => ISC_INDX,

                // Stable unofficial indirect-indexed instructions
                0x13 => SLO_INDY,
                0x33 => RLA_INDY,
                0x53 => SRE_INDY,
                0x73 => RRA_INDY,
                0xB3 => LAX_INDY,
                0xD3 => DCP_INDY,
                0xF3 => ISC_INDY,

                // Unofficial / Common NOP Variants with operand
                0x1A => NOP,
                0x3A => NOP,
                0x5A => NOP,
                0x7A => NOP,
                0xDA => NOP,
                0xFA => NOP,
                0x04 => () => { Addr_ZP(); _cycles = 3; }, // DOP ZP
                0x44 => () => { Addr_ZP(); _cycles = 3; }, // DOP ZP
                0x64 => () => { Addr_ZP(); _cycles = 3; }, // DOP ZP
                0x0C => () => { Addr_ABS(); _cycles = 4; }, // TOP ABS
                0x14 => () => { Addr_ZPX(); _cycles = 4; }, // DOP ZP,X
                0x34 => () => { Addr_ZPX(); _cycles = 4; }, // DOP ZP,X
                0x54 => () => { Addr_ZPX(); _cycles = 4; }, // DOP ZP,X
                0x74 => () => { Addr_ZPX(); _cycles = 4; }, // DOP ZP,X
                0xD4 => () => { Addr_ZPX(); _cycles = 4; }, // DOP ZP,X
                0xF4 => () => { Addr_ZPX(); _cycles = 4; }, // DOP ZP,X
                0x1C => () => { _cycles = 4; Addr_ABSX(); }, // TOP ABS,X
                0x3C => () => { _cycles = 4; Addr_ABSX(); }, // TOP ABS,X
                0x5C => () => { _cycles = 4; Addr_ABSX(); }, // TOP ABS,X
                0x7C => () => { _cycles = 4; Addr_ABSX(); }, // TOP ABS,X
                0xDC => () => { _cycles = 4; Addr_ABSX(); }, // TOP ABS,X
                0xFC => () => { _cycles = 4; Addr_ABSX(); }, // TOP ABS,X
                0x80 => () => { Addr_IMM(); _cycles = 2; }, // DOP IMM
                0x82 => () => { Addr_IMM(); _cycles = 2; }, // DOP IMM
                0x89 => () => { Addr_IMM(); _cycles = 2; }, // DOP IMM
                0xC2 => () => { Addr_IMM(); _cycles = 2; }, // DOP IMM
                0xE2 => () => { Addr_IMM(); _cycles = 2; }, // DOP IMM

                _ => CpuOpcodeTable.IsOfficial(_opcode)
                    ? () => throw new InvalidOperationException($"Official opcode ${_opcode:X2} is missing from the CPU decoder.")
                    : () => _cycles = 2
            };

            instruction();

            var descriptor = CpuOpcodeTable.Get(_opcode);
            Debug.Assert(descriptor == null || _cycles >= descriptor.Cycles,
                $"Opcode ${_opcode:X2} executed in fewer cycles than its descriptor.");
        }

        if (_cycles == 1 && _endOfInstructionAction != null)
        {
            var action = _endOfInstructionAction;
            _endOfInstructionAction = null;
            action();
        }

        // The 2A03 polls IRQ on the penultimate instruction cycle. For a
        // two-cycle instruction this is the opcode-fetch cycle, so an IRQ that
        // arrives during its final cycle is not serviced until one instruction later.
        if (_cycles == 2)
            _irqPending = _interrupts.Irq && !_p.InterruptDisable && !_deferIrqOneInstruction;

        // The CPU always consumes one cycle per clock tick.
        // If a new instruction was fetched, _cycles now holds the remaining duration.
        _cycles--;
        _totalCyclesExecuted++;
    }



    private void HandleNmi()
    {
        // Push PC and status register to stack
        // For NMI/IRQ, B flag is clear and U is set. PC is pushed *before* incrementing.
        Push((byte)(_pc >> 8));
        Push((byte)(_pc & 0xFF));
        Push((byte)((_p.Value | 0b00100000) & ~0b00010000));

        // Set interrupt disable flag
        _p.InterruptDisable = true;

        // Load PC from NMI vector
        _pc = ReadWord(0xFFFA);
        _cycles = 7;
    }

    private void HandleIrq()
    {
        // Push PC and status register to stack
        // For NMI/IRQ, B flag is clear and U is set. PC is pushed *before* incrementing.
        Push((byte)(_pc >> 8));
        Push((byte)(_pc & 0xFF));
        Push((byte)((_p.Value | 0b00100000) & ~0b00010000));

        _p.InterruptDisable = true;
        _pc = ReadWord(0xFFFE); // Load PC from IRQ/BRK vector
        _cycles = 7;
    }

    public void Stall(int cycles)
    {
        _cycles += cycles;
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

    private static bool IsIoAddress(ushort address) => address is >= 0x2000 and <= 0x401F;

    private void CompleteIoAccess(Action action) => _endOfInstructionAction = action;

    // Helper method to read a 16-bit word from the bus (little-endian)
    private ushort ReadWord(ushort address)
    {
        byte lo = Read(address);
        byte hi = Read((ushort)(address + 1));
        return (ushort)((hi << 8) | lo);
    }

    [CpuBehavior(
        CpuBehaviorKind.Nmos6502Quirk,
        "Indirect 16-bit reads wrap the high-byte fetch within the same page when the pointer ends in $FF.",
        "https://www.nesdev.org/wiki/Instruction_reference#JMP_-_Jump")]
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
            sbyte offset = (sbyte)Read(_pc++);
            ushort oldPc = _pc;
            ushort targetAddress = (ushort)(_pc + offset);

            // A taken branch costs 1 extra cycle.
            _cycles = 3;

            // If the branch crosses a page boundary, add another cycle.
            if ((oldPc & 0xFF00) != (targetAddress & 0xFF00)) _cycles++;

            _pc = targetAddress;
        }
        else
        {
            // We must still read the operand to advance the PC past it.
            _pc++;
            _cycles = 2;
        }
    }

    // Executes a single complete instruction by clocking the CPU
    // until the current instruction finishes and cycles returns to 0.
    // Returns the total cycles spent on this instruction.
    public ulong Step()
    {
        var startCycles = _totalCyclesExecuted;

        do
        {
            Clock();
        } while (_cycles > 0);

        return _totalCyclesExecuted - startCycles;
    }

    // --- Stack Operations ---

    // Push a byte onto the stack
    private void Push(byte value)
    {
        Write((ushort)(0x0100 + (_sp & 0xFF)), value);
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
    private ushort Addr_ABSX(bool addCycleOnPageCross = true)
    {
        ushort abs_addr = ReadWord(_pc);
        _pc += 2;
        ushort effective_addr = (ushort)(abs_addr + _x);
        if (addCycleOnPageCross && (effective_addr & 0xFF00) != (abs_addr & 0xFF00)) { _cycles++; } // Page boundary crossed
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
    private ushort Addr_ABSY(bool addCycleOnPageCross = true)
    {
        ushort abs_addr = ReadWord(_pc);
        _pc += 2;
        ushort effective_addr = (ushort)(abs_addr + _y);
        if (addCycleOnPageCross && (effective_addr & 0xFF00) != (abs_addr & 0xFF00)) { _cycles++; } // Page boundary crossed
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
    private ushort Addr_INDY(bool addCycleOnPageCross = true)
    {
        byte zp_ptr = Read(_pc++);
        ushort ptr = ReadWordBug(zp_ptr); // Apply 6502 indirect addressing bug
        ushort effective_addr = (ushort)(ptr + _y);
        if (addCycleOnPageCross && (effective_addr & 0xFF00) != (ptr & 0xFF00)) { _cycles++; } // Page boundary crossed
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
    private void LDA_ABS()
    {
        var address = Addr_ABS();
        _cycles = 4;
        void Load() { _a = Read(address); SetZeroAndNegativeFlags(_a); }
        if (IsIoAddress(address)) CompleteIoAccess(Load); else Load();
    }
    private void LDA_ABSX() { _cycles = 4; _a = Read(Addr_ABSX()); SetZeroAndNegativeFlags(_a); }
    private void LDA_ABSY() { _cycles = 4; _a = Read(Addr_ABSY()); SetZeroAndNegativeFlags(_a); }
    private void LDA_INDX() { _a = Read(Addr_INDX()); SetZeroAndNegativeFlags(_a); _cycles = 6; }
    private void LDA_INDY() { _cycles = 5; _a = Read(Addr_INDY()); SetZeroAndNegativeFlags(_a); }

    // LDX (Load X Register)
    private void LDX_IMM() { _x = Read(Addr_IMM()); SetZeroAndNegativeFlags(_x); _cycles = 2; }
    private void LDX_ZP() { _x = Read(Addr_ZP()); SetZeroAndNegativeFlags(_x); _cycles = 3; }
    private void LDX_ZPY() { _x = Read(Addr_ZPY()); SetZeroAndNegativeFlags(_x); _cycles = 4; }
    private void LDX_ABS()
    {
        var address = Addr_ABS();
        _cycles = 4;
        void Load() { _x = Read(address); SetZeroAndNegativeFlags(_x); }
        if (IsIoAddress(address)) CompleteIoAccess(Load); else Load();
    }
    private void LDX_ABSY() { _cycles = 4; _x = Read(Addr_ABSY()); SetZeroAndNegativeFlags(_x); }

    // LDY (Load Y Register)
    private void LDY_IMM() { _y = Read(Addr_IMM()); SetZeroAndNegativeFlags(_y); _cycles = 2; }
    private void LDY_ZP() { _y = Read(Addr_ZP()); SetZeroAndNegativeFlags(_y); _cycles = 3; }
    private void LDY_ZPX() { _y = Read(Addr_ZPX()); SetZeroAndNegativeFlags(_y); _cycles = 4; }
    private void LDY_ABS()
    {
        var address = Addr_ABS();
        _cycles = 4;
        void Load() { _y = Read(address); SetZeroAndNegativeFlags(_y); }
        if (IsIoAddress(address)) CompleteIoAccess(Load); else Load();
    }
    private void LDY_ABSX() { _cycles = 4; _y = Read(Addr_ABSX()); SetZeroAndNegativeFlags(_y); }

    // AND (Logical AND)
    private void AND_IMM() { _a &= Read(Addr_IMM()); SetZeroAndNegativeFlags(_a); _cycles = 2; }
    private void AND_ZP() { _a &= Read(Addr_ZP()); SetZeroAndNegativeFlags(_a); _cycles = 3; }
    private void AND_ZPX() { _a &= Read(Addr_ZPX()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void AND_ABS() { _a &= Read(Addr_ABS()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void AND_ABSX() { _cycles = 4; _a &= Read(Addr_ABSX()); SetZeroAndNegativeFlags(_a); }
    private void AND_ABSY() { _cycles = 4; _a &= Read(Addr_ABSY()); SetZeroAndNegativeFlags(_a); }
    private void AND_INDX() { _a &= Read(Addr_INDX()); SetZeroAndNegativeFlags(_a); _cycles = 6; }
    private void AND_INDY() { _cycles = 5; _a &= Read(Addr_INDY()); SetZeroAndNegativeFlags(_a); }

    // EOR (Logical Exclusive OR)
    private void EOR_IMM() { _a ^= Read(Addr_IMM()); SetZeroAndNegativeFlags(_a); _cycles = 2; }
    private void EOR_ZP() { _a ^= Read(Addr_ZP()); SetZeroAndNegativeFlags(_a); _cycles = 3; }
    private void EOR_ZPX() { _a ^= Read(Addr_ZPX()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void EOR_ABS() { _a ^= Read(Addr_ABS()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void EOR_ABSX() { _cycles = 4; _a ^= Read(Addr_ABSX()); SetZeroAndNegativeFlags(_a); }
    private void EOR_ABSY() { _cycles = 4; _a ^= Read(Addr_ABSY()); SetZeroAndNegativeFlags(_a); }
    private void EOR_INDX() { _a ^= Read(Addr_INDX()); SetZeroAndNegativeFlags(_a); _cycles = 6; }
    private void EOR_INDY() { _cycles = 5; _a ^= Read(Addr_INDY()); SetZeroAndNegativeFlags(_a); }

    // ORA (Logical Inclusive OR)
    private void ORA_IMM() { _a |= Read(Addr_IMM()); SetZeroAndNegativeFlags(_a); _cycles = 2; }
    private void ORA_ZP() { _a |= Read(Addr_ZP()); SetZeroAndNegativeFlags(_a); _cycles = 3; }
    private void ORA_ZPX() { _a |= Read(Addr_ZPX()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void ORA_ABS() { _a |= Read(Addr_ABS()); SetZeroAndNegativeFlags(_a); _cycles = 4; }
    private void ORA_ABSX() { _cycles = 4; _a |= Read(Addr_ABSX()); SetZeroAndNegativeFlags(_a); }
    private void ORA_ABSY() { _cycles = 4; _a |= Read(Addr_ABSY()); SetZeroAndNegativeFlags(_a); }
    private void ORA_INDX() { _a |= Read(Addr_INDX()); SetZeroAndNegativeFlags(_a); _cycles = 6; }
    private void ORA_INDY() { _cycles = 5; _a |= Read(Addr_INDY()); SetZeroAndNegativeFlags(_a); }

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
    private void ADC_ABSX() { _cycles = 4; ADC(Read(Addr_ABSX())); }
    private void ADC_ABSY() { _cycles = 4; ADC(Read(Addr_ABSY())); }
    private void ADC_INDX() { ADC(Read(Addr_INDX())); _cycles = 6; }
    private void ADC_INDY() { _cycles = 5; ADC(Read(Addr_INDY())); }

    // SBC (Subtract with Carry) - Implemented as ADC with a complemented operand
    private void SBC_IMM() { ADC((byte)~Read(Addr_IMM())); _cycles = 2; }
    private void SBC_ZP() { ADC((byte)~Read(Addr_ZP())); _cycles = 3; }
    private void SBC_ZPX() { ADC((byte)~Read(Addr_ZPX())); _cycles = 4; }
    private void SBC_ABS() { ADC((byte)~Read(Addr_ABS())); _cycles = 4; }
    private void SBC_ABSX() { _cycles = 4; ADC((byte)~Read(Addr_ABSX())); }
    private void SBC_ABSY() { _cycles = 4; ADC((byte)~Read(Addr_ABSY())); }
    private void SBC_INDX() { ADC((byte)~Read(Addr_INDX())); _cycles = 6; }
    private void SBC_INDY() { _cycles = 5; ADC((byte)~Read(Addr_INDY())); }

    // Stable unofficial immediate instructions
    private void AAC_IMM()
    {
        _a &= Read(Addr_IMM());
        SetZeroAndNegativeFlags(_a);
        _p.Carry = _p.Negative;
        _cycles = 2;
    }

    private void ASR_IMM()
    {
        _a &= Read(Addr_IMM());
        _a = LSR(_a);
        _cycles = 2;
    }

    private void ARR_IMM()
    {
        var oldCarry = _p.Carry;
        var value = (byte)(_a & Read(Addr_IMM()));
        _a = (byte)((value >> 1) | (oldCarry ? 0x80 : 0));
        SetZeroAndNegativeFlags(_a);
        _p.Carry = (_a & 0x40) != 0;
        _p.Overflow = (((_a >> 6) ^ (_a >> 5)) & 1) != 0;
        _cycles = 2;
    }

    [CpuBehavior(
        CpuBehaviorKind.Nes2A03Deviation,
        "Opcode $AB uses the NES 2A03's observed $FF bus constant, producing A = X = immediate operand.",
        "https://forums.nesdev.org/viewtopic.php?t=3831")]
    private void ATX_IMM()
    {
        // The NES 2A03's observed bus constant is $FF, so (A | $FF) & operand reduces to operand.
        _a = Read(Addr_IMM());
        _x = _a;
        SetZeroAndNegativeFlags(_a);
        _cycles = 2;
    }

    private void AXS_IMM()
    {
        var value = (byte)(_a & _x);
        var operand = Read(Addr_IMM());
        _x = (byte)(value - operand);
        _p.Carry = value >= operand;
        SetZeroAndNegativeFlags(_x);
        _cycles = 2;
    }

    private void SLO(ushort address)
    {
        var value = ASL(Read(address));
        Write(address, value);
        _a |= value;
        SetZeroAndNegativeFlags(_a);
    }

    private void RLA(ushort address)
    {
        var value = ROL(Read(address));
        Write(address, value);
        _a &= value;
        SetZeroAndNegativeFlags(_a);
    }

    private void SRE(ushort address)
    {
        var value = LSR(Read(address));
        Write(address, value);
        _a ^= value;
        SetZeroAndNegativeFlags(_a);
    }

    private void RRA(ushort address)
    {
        var value = ROR(Read(address));
        Write(address, value);
        ADC(value);
    }

    private void DCP(ushort address)
    {
        var value = (byte)(Read(address) - 1);
        Write(address, value);
        Compare(_a, value);
    }

    private void ISC(ushort address)
    {
        var value = (byte)(Read(address) + 1);
        Write(address, value);
        ADC((byte)~value);
    }

    private void SLO_ZP() { SLO(Addr_ZP()); _cycles = 5; }
    private void RLA_ZP() { RLA(Addr_ZP()); _cycles = 5; }
    private void SRE_ZP() { SRE(Addr_ZP()); _cycles = 5; }
    private void RRA_ZP() { RRA(Addr_ZP()); _cycles = 5; }
    private void AAX_ZP() { Write(Addr_ZP(), (byte)(_a & _x)); _cycles = 3; }
    private void LAX_ZP()
    {
        _a = _x = Read(Addr_ZP());
        SetZeroAndNegativeFlags(_a);
        _cycles = 3;
    }
    private void DCP_ZP() { DCP(Addr_ZP()); _cycles = 5; }
    private void ISC_ZP() { ISC(Addr_ZP()); _cycles = 5; }
    private void SLO_ZPX() { SLO(Addr_ZPX()); _cycles = 6; }
    private void RLA_ZPX() { RLA(Addr_ZPX()); _cycles = 6; }
    private void SRE_ZPX() { SRE(Addr_ZPX()); _cycles = 6; }
    private void RRA_ZPX() { RRA(Addr_ZPX()); _cycles = 6; }
    private void AAX_ZPY() { Write(Addr_ZPY(), (byte)(_a & _x)); _cycles = 4; }
    private void LAX_ZPY()
    {
        _a = _x = Read(Addr_ZPY());
        SetZeroAndNegativeFlags(_a);
        _cycles = 4;
    }
    private void DCP_ZPX() { DCP(Addr_ZPX()); _cycles = 6; }
    private void ISC_ZPX() { ISC(Addr_ZPX()); _cycles = 6; }
    private void SLO_ABS() { SLO(Addr_ABS()); _cycles = 6; }
    private void RLA_ABS() { RLA(Addr_ABS()); _cycles = 6; }
    private void SRE_ABS() { SRE(Addr_ABS()); _cycles = 6; }
    private void RRA_ABS() { RRA(Addr_ABS()); _cycles = 6; }
    private void AAX_ABS() { Write(Addr_ABS(), (byte)(_a & _x)); _cycles = 4; }
    private void LAX_ABS()
    {
        _a = _x = Read(Addr_ABS());
        SetZeroAndNegativeFlags(_a);
        _cycles = 4;
    }
    private void DCP_ABS() { DCP(Addr_ABS()); _cycles = 6; }
    private void ISC_ABS() { ISC(Addr_ABS()); _cycles = 6; }
    private void SLO_ABSX() { SLO(Addr_ABSX_Write()); _cycles = 7; }
    private void RLA_ABSX() { RLA(Addr_ABSX_Write()); _cycles = 7; }
    private void SRE_ABSX() { SRE(Addr_ABSX_Write()); _cycles = 7; }
    private void RRA_ABSX() { RRA(Addr_ABSX_Write()); _cycles = 7; }
    private void DCP_ABSX() { DCP(Addr_ABSX_Write()); _cycles = 7; }
    private void ISC_ABSX() { ISC(Addr_ABSX_Write()); _cycles = 7; }
    private void SLO_ABSY() { SLO(Addr_ABSY_Write()); _cycles = 7; }
    private void RLA_ABSY() { RLA(Addr_ABSY_Write()); _cycles = 7; }
    private void SRE_ABSY() { SRE(Addr_ABSY_Write()); _cycles = 7; }
    private void RRA_ABSY() { RRA(Addr_ABSY_Write()); _cycles = 7; }
    private void DCP_ABSY() { DCP(Addr_ABSY_Write()); _cycles = 7; }
    private void ISC_ABSY() { ISC(Addr_ABSY_Write()); _cycles = 7; }
    private void LAX_ABSY()
    {
        _cycles = 4;
        _a = _x = Read(Addr_ABSY());
        SetZeroAndNegativeFlags(_a);
    }

    private void SYA_ABSX()
    {
        StoreHighMaskedIndexed(_x, _y);
        _cycles = 5;
    }

    private void SXA_ABSY()
    {
        StoreHighMaskedIndexed(_y, _x);
        _cycles = 5;
    }

    [CpuBehavior(
        CpuBehaviorKind.Nmos6502Quirk,
        "SHY/SHX mask the stored register with base-high + 1 and replace the destination high byte with that value on page crossing.",
        "https://www.nesdev.org/wiki/CPU_unofficial_opcodes")]
    private void StoreHighMaskedIndexed(byte index, byte registerValue)
    {
        var baseAddress = ReadWord(_pc);
        _pc += 2;
        var effectiveAddress = (ushort)(baseAddress + index);
        var value = (byte)(registerValue & (((baseAddress >> 8) + 1) & 0xFF));
        if ((baseAddress & 0xFF) + index > 0xFF)
            effectiveAddress = (ushort)((value << 8) | (effectiveAddress & 0xFF));
        Write(effectiveAddress, value);
    }

    private void SLO_INDX() { SLO(Addr_INDX()); _cycles = 8; }
    private void RLA_INDX() { RLA(Addr_INDX()); _cycles = 8; }
    private void SRE_INDX() { SRE(Addr_INDX()); _cycles = 8; }
    private void RRA_INDX() { RRA(Addr_INDX()); _cycles = 8; }
    private void AAX_INDX() { Write(Addr_INDX(), (byte)(_a & _x)); _cycles = 6; }
    private void LAX_INDX()
    {
        _a = _x = Read(Addr_INDX());
        SetZeroAndNegativeFlags(_a);
        _cycles = 6;
    }
    private void DCP_INDX() { DCP(Addr_INDX()); _cycles = 8; }
    private void ISC_INDX() { ISC(Addr_INDX()); _cycles = 8; }
    private void SLO_INDY() { SLO(Addr_INDY(false)); _cycles = 8; }
    private void RLA_INDY() { RLA(Addr_INDY(false)); _cycles = 8; }
    private void SRE_INDY() { SRE(Addr_INDY(false)); _cycles = 8; }
    private void RRA_INDY() { RRA(Addr_INDY(false)); _cycles = 8; }
    private void LAX_INDY()
    {
        _cycles = 5;
        _a = _x = Read(Addr_INDY());
        SetZeroAndNegativeFlags(_a);
    }
    private void DCP_INDY() { DCP(Addr_INDY(false)); _cycles = 8; }
    private void ISC_INDY() { ISC(Addr_INDY(false)); _cycles = 8; }

    // STA (Store Accumulator)
    private void STA_ZP() { Write(Addr_ZP(), _a); _cycles = 3; }
    private void STA_ZPX() { Write(Addr_ZPX(), _a); _cycles = 4; }
    private void STA_ABS()
    {
        var address = Addr_ABS();
        _cycles = 4;
        if (IsIoAddress(address)) CompleteIoAccess(() => Write(address, _a));
        else Write(address, _a);
    }

    private void XAA_IMM()
    {
        // XAA is electrically unstable on NMOS parts. The 2A03's commonly
        // observed deterministic component is X AND the immediate operand.
        _a = (byte)(_x & Read(Addr_IMM()));
        SetZeroAndNegativeFlags(_a);
        _cycles = 2;
    }

    private void AHX_INDY()
    {
        var address = Addr_INDY_Write();
        Write(address, (byte)(_a & _x & (((address >> 8) + 1) & 0xFF)));
        _cycles = 6;
    }

    private void AHX_ABSY()
    {
        var address = Addr_ABSY_Write();
        Write(address, (byte)(_a & _x & (((address >> 8) + 1) & 0xFF)));
        _cycles = 5;
    }

    private void TAS_ABSY()
    {
        var address = Addr_ABSY_Write();
        _sp = (byte)(_a & _x);
        Write(address, (byte)(_sp & (((address >> 8) + 1) & 0xFF)));
        _cycles = 5;
    }

    private void LAS_ABSY()
    {
        _cycles = 4;
        var value = (byte)(Read(Addr_ABSY()) & _sp);
        _a = _x = _sp = value;
        SetZeroAndNegativeFlags(value);
    }
    private void STA_ABSX() { Write(Addr_ABSX_Write(), _a); _cycles = 5; }
    private void STA_ABSY() { Write(Addr_ABSY_Write(), _a); _cycles = 5; }
    private void STA_INDX() { Write(Addr_INDX(), _a); _cycles = 6; }
    private void STA_INDY() { Write(Addr_INDY_Write(), _a); _cycles = 6; }

    // STX (Store Index X)
    private void STX_ZP() { Write(Addr_ZP(), _x); _cycles = 3; }
    private void STX_ZPY() { Write(Addr_ZPY(), _x); _cycles = 4; }
    private void STX_ABS()
    {
        var address = Addr_ABS();
        _cycles = 4;
        if (IsIoAddress(address)) CompleteIoAccess(() => Write(address, _x));
        else Write(address, _x);
    }

    // STY (Store Index Y)
    private void STY_ZP() { Write(Addr_ZP(), _y); _cycles = 3; }
    private void STY_ZPX() { Write(Addr_ZPX(), _y); _cycles = 4; }
    private void STY_ABS()
    {
        var address = Addr_ABS();
        _cycles = 4;
        if (IsIoAddress(address)) CompleteIoAccess(() => Write(address, _y));
        else Write(address, _y);
    }

    // INC (Increment Memory)
    private void INC_ZP()
    {
        var address = Addr_ZP();
        _cycles = 5;
        _endOfInstructionAction = () =>
        {
            var value = (byte)(Read(address) + 1);
            Write(address, value);
            SetZeroAndNegativeFlags(value);
        };
    }
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
    private void CMP_ABSX() { _cycles = 4; Compare(_a, Read(Addr_ABSX())); }
    private void CMP_ABSY() { _cycles = 4; Compare(_a, Read(Addr_ABSY())); }
    private void CMP_INDX() { Compare(_a, Read(Addr_INDX())); _cycles = 6; }
    private void CMP_INDY() { _cycles = 5; Compare(_a, Read(Addr_INDY())); }

    // CPX (Compare X Register)
    private void CPX_IMM() { Compare(_x, Read(Addr_IMM())); _cycles = 2; }
    private void CPX_ZP() { Compare(_x, Read(Addr_ZP())); _cycles = 3; }
    private void CPX_ABS() { Compare(_x, Read(Addr_ABS())); _cycles = 4; }

    // CPY (Compare Y Register)
    private void CPY_IMM() { Compare(_y, Read(Addr_IMM())); _cycles = 2; }
    private void CPY_ZP() { Compare(_y, Read(Addr_ZP())); _cycles = 3; }
    private void CPY_ABS() { Compare(_y, Read(Addr_ABS())); _cycles = 4; }

    // BIT (Test Bits)
    private void BIT(ushort addr) { byte operand = Read(addr); _p.Zero = (_a & operand) == 0; _p.Overflow = (operand & 0x40) != 0; _p.Negative = (operand & 0x80) != 0; }
    private void BIT_ZP() { BIT(Addr_ZP()); _cycles = 3; }
    private void BIT_ABS()
    {
        var address = Addr_ABS();
        _cycles = 4;
        if (IsIoAddress(address)) CompleteIoAccess(() => BIT(address));
        else BIT(address);
    }

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
    private void JMP_ABS()
    {
        _pc = Addr_ABS();
        _cycles = 3;
    }

    internal CpuDebugState CaptureDebugState() => new(
        _a, _x, _y, _sp, _pc, _p.Value, _opcode, _cycles, _totalCyclesExecuted, _cycles == 0);
    internal ushort ProgramCounter => _pc;
    internal bool IsInstructionBoundary => _cycles == 0;

    internal void SetRegisters(CpuRegisterValues registers)
    {
        _a = registers.Accumulator;
        _x = registers.X;
        _y = registers.Y;
        _sp = registers.StackPointer;
        _pc = registers.ProgramCounter;
        _p.Value = (byte)(registers.Status | 0x20);
    }

    private void JMP_IND()
    {
        ushort pointer = Addr_ABS();
        _pc = ReadWordBug(pointer);
        _cycles = 5;
    }

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
    private void CLI_IMP()
    {
        _deferIrqOneInstruction = _p.InterruptDisable;
        _p.InterruptDisable = false;
        _cycles = 2;
    }
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
        Push((byte)(_pc >> 8));
        Push((byte)(_pc & 0xFF));
        // For BRK, both B and U flags are set when pushing to stack.
        Push((byte)(_p.Value | 0b00110000)); // Set B and unused bit 5
        _p.InterruptDisable = true;

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
