namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuDmaBusInspector
{
    internal static bool IsCurrentCycleWrite(CpuState s) =>
        (s.Cycles == 1 && s.EndOfInstructionIsWrite) ||
        (s.AbsoluteStoreRegister != 0 && s.Cycles == 1) ||
        (s.Cycles == 2 && s.PenultimateInstructionIsWrite) ||
        (s.RmwInProgress && s.Cycles is 2 or 1) ||
        (s.JsrInProgress && s.Cycles is 2 or 3);

    internal static ushort? GetDmaReadAddress(CpuState s)
    {
        if (s.Cycles == 1 && s.PendingIoReadAddress.HasValue) return s.PendingIoReadAddress.Value;
        if (s.Cycles == 0) return s.ProgramCounter;
        if (s.JsrInProgress && s.Cycles is 5 or 1) return s.ProgramCounter;
        if (s.JsrInProgress && s.Cycles == 4) return (ushort)(0x0100 | s.SP);
        if (s.RtsInProgress && s.Cycles is 5 or 1) return s.ProgramCounter;
        if (s.RtsInProgress && s.Cycles == 4) return (ushort)(0x0100 | s.SP);
        if (s.RtsInProgress && s.Cycles is 3 or 2) return (ushort)(0x0100 | (byte)(s.SP + 1));
        if (s.StackPullInProgress && s.Cycles == 2) return (ushort)(0x0100 | s.SP);
        if (s.StackPullInProgress && s.Cycles == 1) return (ushort)(0x0100 | (byte)(s.SP + 1));
        if (s.StackPullInProgress && s.Cycles == 3) return s.ProgramCounter;
        if (s.AbsoluteStoreRegister != 0 && s.Cycles is 3 or 2) return s.ProgramCounter;
        if (s.AbsoluteReadConsumer != null && s.Cycles is 3 or 2) return s.ProgramCounter;
        if (s.AbsoluteReadConsumer != null && s.Cycles == 1) return s.AbsoluteReadAddress;
        if (s.RmwInProgress && s.Cycles == 3) return s.RmwAddress;
        return s.RmwInProgress && s.Cycles > 3 ? s.ProgramCounter : null;
    }

    internal static ushort GetPendingBusAddress(CpuState s)
    {
        if (s.Cycles <= 2 && s.PendingIoReadAddress.HasValue) return s.PendingIoReadAddress.Value;
        if (s.AbsoluteReadConsumer != null && s.Cycles <= 2) return s.AbsoluteReadAddress;
        if (s.AbsoluteStoreRegister != 0 && s.Cycles <= 2) return s.AbsoluteStoreAddress;
        return s.RmwInProgress && s.Cycles <= 2 ? s.RmwAddress : s.ProgramCounter;
    }
}
