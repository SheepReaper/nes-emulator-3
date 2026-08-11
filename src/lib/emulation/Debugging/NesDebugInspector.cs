using System.Collections.Generic;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Direct memory and hardware inspection operations for debugger.
/// </summary>
internal static class NesDebugInspector
{
    internal static byte PeekCpu(NesSystem nes, ushort address)
    {
        lock (nes.SyncRoot) return nes.CpuBus.Peek(address);
    }

    internal static byte PeekPpu(NesSystem nes, ushort address)
    {
        lock (nes.SyncRoot) return nes.PpuBus.Peek(address);
    }

    internal static IReadOnlyList<DisassembledInstruction> Disassemble(NesSystem nes, ushort address, int count)
    {
        lock (nes.SyncRoot) return NesDisassembler.Disassemble(nes, address, count);
    }

    internal static IReadOnlyList<DisassembledInstruction> DisassembleAtPc(NesSystem nes, int count)
    {
        lock (nes.SyncRoot) return NesDisassembler.Disassemble(nes, nes.Cpu.CaptureDebugState().ProgramCounter, count);
    }

    internal static PatternTableSnapshot CapturePatternTable(NesSystem nes, int tableIndex, int paletteIndex)
    {
        lock (nes.SyncRoot) return NesPatternTableInspector.Capture(nes, tableIndex, paletteIndex);
    }
}
