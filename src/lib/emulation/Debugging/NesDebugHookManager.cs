namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Breakpoint and trace bus observation hooks.
/// </summary>
internal static class NesDebugHookManager
{
    internal static void Refresh(NesSystem nes, NesBreakpointManager bp, NesCpuClockTracer tracer)
    {
        nes.CpuBus.DebugObserved = tracer.IsTracing || bp.HasCpuAccessBreakpoints
            ? (kind, address, value) => ObserveCpuAccess(nes, bp, tracer, kind, address, value)
            : null;
    }

    private static void ObserveCpuAccess(
        NesSystem nes,
        NesBreakpointManager bp,
        NesCpuClockTracer tracer,
        NesDebugBreakKind kind,
        ushort address,
        byte value)
    {
        tracer.RecordAccess(kind, address, value);
        if (bp.SuppressBreakpoints || bp.PendingAccessBreak != null)
        {
            return;
        }

        var breakpoint = bp.Find(kind, address);
        if (breakpoint == null)
        {
            return;
        }

        bp.PendingAccessBreak = new BreakOccurredEventArgs(breakpoint, address, value, nes.Cpu.ProgramCounter);
    }
}
