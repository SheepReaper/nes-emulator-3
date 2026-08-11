namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuMemorySequencer
{
    internal static void StepAbsoluteStore(Cpu cpu, CpuState s)
    {
        if (s.Cycles == 3) s.AbsoluteStoreLow = cpu.Read(s.ProgramCounter++);
        if (s.Cycles == 2) s.AbsoluteStoreAddress = (ushort)((cpu.Read(s.ProgramCounter++) << 8) | s.AbsoluteStoreLow);
        if (s.Cycles == 1)
        {
            var value = s.AbsoluteStoreRegister switch { 1 => s.A, 2 => s.X, 3 => s.Y, _ => (byte)(s.A & s.X) };
            cpu.Write(s.AbsoluteStoreAddress, value);
            s.AbsoluteStoreRegister = 0;
        }
    }

    internal static void StepAbsoluteRead(Cpu cpu, CpuState s)
    {
        if (s.Cycles == 3) s.AbsoluteReadLow = cpu.Read(s.ProgramCounter++);
        if (s.Cycles == 2) s.AbsoluteReadAddress = (ushort)((cpu.Read(s.ProgramCounter++) << 8) | s.AbsoluteReadLow);
        if (s.Cycles == 1)
        {
            var consumer = s.AbsoluteReadConsumer;
            s.AbsoluteReadConsumer = null;
            var value = cpu.Read(s.AbsoluteReadAddress);
            consumer!(value);
        }
    }

    internal static void StepRmw(Cpu cpu, CpuState s)
    {
        if (s.Cycles == 4 && s.RmwDummyReadAddress.HasValue)
        {
            _ = cpu.Read(s.RmwDummyReadAddress.Value);
            s.RmwDummyReadAddress = null;
        }
        if (s.Cycles == 3)
        {
            s.RmwOriginal = cpu.Read(s.RmwAddress);
            s.RmwModified = s.RmwOperation!(s.RmwOriginal);
        }
        if (s.Cycles == 2) cpu.Write(s.RmwAddress, s.RmwOriginal);
        if (s.Cycles == 1)
        {
            cpu.Write(s.RmwAddress, s.RmwModified);
            s.RmwInProgress = false;
        }
    }
}
