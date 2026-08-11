using System;

namespace Sheep.Emulation.Nes.Debugging;

/// <summary>
/// Mutators for memory regions, registers, and audio/video state.
/// </summary>
internal static class NesDebugMutator
{
    internal static void CopyMemoryRegion(NesSystem nes, NesMemoryRegion region, int offset, Span<byte> destination)
    {
        lock (nes.SyncRoot) NesMemoryAccessBridge.Copy(nes, region, offset, destination);
    }

    internal static void WriteMemoryRegion(NesSystem nes, NesMemoryRegion region, int offset, ReadOnlySpan<byte> source)
    {
        lock (nes.SyncRoot)
        {
            RequirePaused(nes);
            NesMemoryAccessBridge.Write(nes, region, offset, source);
        }
    }

    internal static void SetCpuRegisters(NesSystem nes, CpuRegisterValues registers)
    {
        lock (nes.SyncRoot)
        {
            RequirePaused(nes);
            nes.Cpu.SetRegisters(registers);
        }
    }

    internal static void WritePpuRegister(NesSystem nes, ushort register, byte value)
    {
        lock (nes.SyncRoot)
        {
            RequirePaused(nes);
            NesRegisterMutator.WritePpuRegister(nes, register, value);
        }
    }

    internal static void WriteApuRegister(NesSystem nes, ushort register, byte value)
    {
        lock (nes.SyncRoot)
        {
            RequirePaused(nes);
            NesRegisterMutator.WriteApuRegister(nes, register, value);
        }
    }

    private static void RequirePaused(NesSystem nes)
    {
        if (!nes.IsPausedLocked)
        {
            throw new InvalidOperationException("The NES must be paused for this operation.");
        }
    }
}
