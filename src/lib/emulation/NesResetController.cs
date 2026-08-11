namespace Sheep.Emulation.Nes;

/// <summary>
/// Handles ROM loading, soft/hard reset, and paused state enforcement for NesSystem.
/// </summary>
internal static class NesResetController
{
    internal static void LoadRom(
        NesSystem nes,
        NesSystemUnits u,
        byte[] romData)
    {
        var cartridge = u.CartridgeFactory.Create(romData);
        lock (nes.SyncRoot)
        {
            u.CartridgeSlot.Insert(cartridge);
            ResetLocked(nes, u);
        }
    }

    internal static void Reset(NesSystem nes, NesSystemUnits u)
    {
        lock (nes.SyncRoot)
        {
            ResetLocked(nes, u);
        }
    }

    internal static void ResetLocked(NesSystem nes, NesSystemUnits u)
    {
        u.Debugger.ResetTransientStateLocked();
        try
        {
            u.CartridgeSlot.Cartridge?.Reset();
            u.Cpu.Reset();
            u.Ppu.Reset();
            u.Apu.Reset();
        }
        finally
        {
            u.Debugger.FinishResetLocked();
        }

        nes.ResetTimingCountersInternal();
        nes.IsPausedLocked = false;
    }
}
