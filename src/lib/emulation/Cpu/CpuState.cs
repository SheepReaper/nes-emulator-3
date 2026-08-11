using System;
using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes.Cpu;

public sealed class CpuState
{
    public byte A, X, Y, SP;
    public ProcessorStatus P;
    public ushort ProgramCounter;

    public int Cycles;
    public byte Opcode;
    public ulong MasterClock;
    public ulong TotalCyclesExecuted;

    public bool NmiPending;
    public bool DelayPendingNmiOneInstruction;
    public bool IrqPending;

    public Action? EndOfInstructionAction;
    public Action? PenultimateInstructionAction;
    public bool PenultimateInstructionIsWrite;
    public bool EndOfInstructionIsWrite;
    public ushort? PendingIoReadAddress;

    public byte JsrTargetLow;
    public ushort JsrReturnAddress;
    public bool JsrInProgress;

    public byte RtsTargetLow;
    public bool RtsInProgress;
    public bool StackPullInProgress;
    public bool BrkInProgress;
    public bool IrqInProgress;

    public byte AbsoluteStoreRegister;
    public byte AbsoluteStoreLow;
    public ushort AbsoluteStoreAddress;

    public Action<byte>? AbsoluteReadConsumer;
    public byte AbsoluteReadLow;
    public ushort AbsoluteReadAddress;

    public bool DmaHaltOccurred;
    public bool BranchTakenNoPageCross;

    public ushort RmwAddress;
    public ushort? RmwDummyReadAddress;
    public Func<byte, byte>? RmwOperation;
    public byte RmwOriginal;
    public byte RmwModified;
    public bool RmwInProgress;

    public void Reset()
    {
        A = 0;
        X = 0;
        Y = 0;
        SP = 0xFD;
        P.Value = 0b0010_0100;
        NmiPending = false;
        DelayPendingNmiOneInstruction = false;
        IrqPending = false;
        EndOfInstructionAction = null;
        PenultimateInstructionAction = null;
        PenultimateInstructionIsWrite = false;
        EndOfInstructionIsWrite = false;
        PendingIoReadAddress = null;
        JsrInProgress = false;
        RtsInProgress = false;
        StackPullInProgress = false;
        BrkInProgress = false;
        IrqInProgress = false;
        AbsoluteStoreRegister = 0;
        AbsoluteReadConsumer = null;
        DmaHaltOccurred = false;
        BranchTakenNoPageCross = false;
        RmwInProgress = false;
        RmwDummyReadAddress = null;
        RmwOperation = null;
        Cycles = 7;
    }

    public void SetZeroAndNegativeFlags(byte value)
    {
        P.Zero = value == 0x00;
        P.Negative = (value & 0x80) != 0x00;
    }

    internal CpuDebugState CaptureDebugState() => new(
        A, X, Y, SP, ProgramCounter, P.Value, Opcode, Cycles, TotalCyclesExecuted, Cycles == 0);

    internal void SetRegisters(CpuRegisterValues registers)
    {
        A = registers.Accumulator;
        X = registers.X;
        Y = registers.Y;
        SP = registers.StackPointer;
        ProgramCounter = registers.ProgramCounter;
        P.Value = (byte)(registers.Status | 0x20);
    }
}
