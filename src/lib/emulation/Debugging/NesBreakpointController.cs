using System.Collections.Generic;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Breakpoint operations facade with locking and hook updates.
/// </summary>
internal static class NesBreakpointController
{
    internal static NesBreakpoint Add(
        NesSystem nes,
        NesBreakpointManager bp,
        NesCpuClockTracer tracer,
        NesDebugBreakKind kind,
        ushort start,
        ushort? end)
    {
        lock (nes.SyncRoot)
        {
            var item = bp.Add(kind, start, end);
            NesDebugHookManager.Refresh(nes, bp, tracer);
            return item;
        }
    }

    internal static bool SetEnabled(
        NesSystem nes,
        NesBreakpointManager bp,
        NesCpuClockTracer tracer,
        long id,
        bool enabled)
    {
        lock (nes.SyncRoot)
        {
            var ok = bp.SetEnabled(id, enabled);
            if (ok)
            {
                NesDebugHookManager.Refresh(nes, bp, tracer);
            }
            return ok;
        }
    }

    internal static bool Remove(
        NesSystem nes,
        NesBreakpointManager bp,
        NesCpuClockTracer tracer,
        long id)
    {
        lock (nes.SyncRoot)
        {
            var ok = bp.Remove(id);
            if (ok)
            {
                NesDebugHookManager.Refresh(nes, bp, tracer);
            }
            return ok;
        }
    }

    internal static IReadOnlyList<NesBreakpoint> GetAll(NesSystem nes, NesBreakpointManager bp)
    {
        lock (nes.SyncRoot) return bp.GetAll();
    }

    internal static void Clear(NesSystem nes, NesBreakpointManager bp, NesCpuClockTracer tracer)
    {
        lock (nes.SyncRoot)
        {
            bp.Clear();
            NesDebugHookManager.Refresh(nes, bp, tracer);
        }
    }
}
