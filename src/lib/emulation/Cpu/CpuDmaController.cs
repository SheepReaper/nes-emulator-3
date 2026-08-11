using System;
using Sheep.Emulation.Nes.Debugging;

namespace Sheep.Emulation.Nes.Cpu;

internal sealed class CpuDmaController
{
    private readonly CpuOamDmaState _oam = new();
    private readonly CpuDmcDmaState _dmc = new();

    internal bool OamDmaPending => _oam.Pending;
    internal bool OamDmaActive => _oam.Active;
    internal int OamDmaIndex => _oam.Index;
    internal bool OamDmaReadPhase => _oam.ReadPhase;
    internal bool DmcDmaPending => _dmc.Pending;
    internal int DmcDmaCycles => _dmc.Cycles;
    internal ushort DmcDmaAddress => _dmc.Address;

    internal void TriggerOamDma(byte page) => _oam.Trigger(page);
    internal void RequestDmcDma(ushort address, Action<byte>? completed) => _dmc.Request(address, completed);
    internal void AbortDmcDma() => _dmc.Abort();
    internal void NotifyWriteCycle()
    {
        if (_dmc.Pending)
        {
            if (_dmc.Completed is null)
            {
                _dmc.Pending = false;
                _dmc.Cycles = 0;
                _dmc.HaltLatched = false;
                _dmc.Completed = null;
                _dmc.ControllerReadClocked = false;
            }
            else
            {
                _dmc.HaltLatched = true;
            }
        }
    }

    internal bool ClockDma(ulong cpuClock, ushort? cpuReadAddress, CpuBus bus, Ppu ppu, out NesCpuClockActor actor)
    {
        actor = NesCpuClockActor.Cpu;
        if (cpuReadAddress.HasValue) bus.RecordCpuReadAddress(cpuReadAddress.Value);

        if (_oam.Pending && !_oam.Active)
        {
            actor = NesCpuClockActor.OamDma;
            _oam.Active = true;
            _oam.Pending = false;
            _oam.Index = 0;
            _oam.StartupCycles = (cpuClock & 1) == 0 ? 0 : 1;
            _oam.ReadPhase = true;
            _oam.Realign = false;
            return true;
        }

        if (_oam.Active && _dmc.Pending) return ClockOverlappedDmc(cpuClock, bus, ppu, out actor);
        if (_oam.Realign)
        {
            actor = NesCpuClockActor.OamDma;
            _oam.Realign = false;
            return true;
        }

        if (_dmc.Pending) return ClockStandaloneDmc(cpuClock, bus, out actor);
        if (!_oam.Active)
        {
            if (_dmc.Pending) _dmc.HaltLatched = true;
            return false;
        }
        actor = NesCpuClockActor.OamDma;
        return _oam.ClockCycle(bus, ppu);
    }

    private bool ClockOverlappedDmc(ulong cpuClock, CpuBus bus, Ppu ppu, out NesCpuClockActor actor)
    {
        actor = NesCpuClockActor.DmcDma;
        if (_dmc.Completed is null && _dmc.Cycles == 1)
        {
            var address = bus.GetDmcDummyReadAddress();
            if (address is not (0x4016 or 0x4017) || !_dmc.ControllerReadClocked)
            {
                _ = bus.Read(address);
                if (address is 0x4016 or 0x4017) _dmc.ControllerReadClocked = true;
            }
            _dmc.Pending = false;
            _dmc.Cycles = 0;
            _dmc.Completed = null;
            _dmc.ControllerReadClocked = false;
            return _oam.ClockCycle(bus, ppu);
        }
        if (_dmc.Cycles == 0) _dmc.Cycles = (cpuClock & 1) == 0 ? 4 : 3;
        if (--_dmc.Cycles > 0) return _oam.ClockCycle(bus, ppu);

        var value = bus.ReadDmaSource(_dmc.Address);
        var completed = _dmc.Completed;
        _dmc.Pending = false;
        _dmc.Completed = null;
        _oam.Realign = true;
        completed?.Invoke(value);
        return true;
    }

    private bool ClockStandaloneDmc(ulong cpuClock, CpuBus bus, out NesCpuClockActor actor)
    {
        actor = NesCpuClockActor.DmcDma;
        if (_dmc.Completed is null && _dmc.Cycles == 1)
        {
            var address = bus.GetDmcDummyReadAddress();
            if (address is not (0x4016 or 0x4017) || !_dmc.ControllerReadClocked)
            {
                _ = bus.Read(address);
                if (address is 0x4016 or 0x4017) _dmc.ControllerReadClocked = true;
            }
            _dmc.Pending = false;
            _dmc.Cycles = 0;
            _dmc.Completed = null;
            _dmc.ControllerReadClocked = false;
            return true;
        }
        if (_dmc.Cycles == 0) _dmc.Cycles = (cpuClock & 1) == 0 ? 4 : 3;
        _dmc.Cycles--;
        if (_dmc.Cycles > 0)
        {
            var address = bus.GetDmcDummyReadAddress();
            if (address is not (0x4016 or 0x4017) || !_dmc.ControllerReadClocked)
            {
                _ = bus.Read(address);
                if (address is 0x4016 or 0x4017) _dmc.ControllerReadClocked = true;
            }
            return true;
        }

        var value = bus.ReadDmaSource(_dmc.Address);
        var completed = _dmc.Completed;
        _dmc.Pending = false;
        _dmc.Completed = null;
        completed?.Invoke(value);
        return true;
    }
}
