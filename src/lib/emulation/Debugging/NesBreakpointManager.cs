using System;
using System.Collections.Generic;
using System.Linq;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Breakpoint table management and address matching.
/// </summary>
internal sealed class NesBreakpointManager
{
    private readonly List<NesBreakpoint> _breakpoints = [];
    private long _nextId = 1;

    internal BreakOccurredEventArgs? PendingAccessBreak { get; set; }
    internal BreakOccurredEventArgs? EventBreak { get; set; }
    internal ExecutionStateChangedEventArgs? EventState { get; set; }
    internal bool SuppressBreakpoints { get; set; }

    internal bool HasEnabledBreakpoints => _breakpoints.Any(x => x.IsEnabled);
    internal bool HasCpuAccessBreakpoints => _breakpoints.Any(x => x.IsEnabled &&
        x.Kind is NesDebugBreakKind.CpuRead or NesDebugBreakKind.CpuWrite);

    internal NesBreakpoint Add(NesDebugBreakKind kind, ushort startAddress, ushort? endAddress = null)
    {
        if (!Enum.IsDefined(typeof(NesDebugBreakKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var end = endAddress ?? startAddress;
        if (end < startAddress)
        {
            throw new ArgumentException("The breakpoint end address cannot precede its start address.", nameof(endAddress));
        }

        var breakpoint = new NesBreakpoint(_nextId++, kind, startAddress, end, true);
        _breakpoints.Add(breakpoint);
        return breakpoint;
    }

    internal bool SetEnabled(long id, bool enabled)
    {
        var index = _breakpoints.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            return false;
        }

        var current = _breakpoints[index];
        _breakpoints[index] = new NesBreakpoint(current.Id, current.Kind, current.StartAddress, current.EndAddress, enabled);
        if (!enabled && PendingAccessBreak?.Breakpoint.Id == id)
        {
            PendingAccessBreak = null;
        }
        return true;
    }

    internal bool Remove(long id)
    {
        var index = _breakpoints.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            return false;
        }

        _breakpoints.RemoveAt(index);
        if (PendingAccessBreak?.Breakpoint.Id == id)
        {
            PendingAccessBreak = null;
        }
        return true;
    }

    internal IReadOnlyList<NesBreakpoint> GetAll() => _breakpoints.ToArray();

    internal void Clear()
    {
        _breakpoints.Clear();
        PendingAccessBreak = null;
    }

    internal NesBreakpoint? Find(NesDebugBreakKind kind, ushort address) =>
        _breakpoints.FirstOrDefault(x => x.IsEnabled && x.Kind == kind &&
            address >= x.StartAddress && address <= x.EndAddress);

    internal void ResetTransient()
    {
        SuppressBreakpoints = true;
        PendingAccessBreak = null;
        EventBreak = null;
        EventState = null;
    }
}
