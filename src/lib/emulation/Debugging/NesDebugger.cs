using System;
using System.Collections.Generic;
using System.Linq;

namespace SR.Emulation.Nes;

internal sealed class NesDebugger : INesDebugger
{
    private readonly Nes nes;
    private readonly List<NesBreakpoint> _breakpoints = new();
    private long _nextBreakpointId = 1;
    private BreakOccurredEventArgs? _pendingAccessBreak;
    private BreakOccurredEventArgs? _eventBreak;
    private ExecutionStateChangedEventArgs? _eventState;
    private bool _suppressBreakpoints;

    public NesDebugger(Nes nes)
    {
        this.nes = nes;
        nes.CpuBus.DebugAccessed = ObserveCpuAccess;
    }
    public bool IsPaused { get { lock (nes.SyncRoot) return nes.IsPausedUnsafe; } }
    public NesExecutionState ExecutionState => IsPaused ? NesExecutionState.Paused : NesExecutionState.Running;

    public event EventHandler<ExecutionStateChangedEventArgs>? ExecutionStateChanged;
    public event EventHandler<BreakOccurredEventArgs>? BreakOccurred;

    public void Pause() => SetExecutionState(true, NesDebugPauseReason.Manual);
    public void Resume() => SetExecutionState(false, NesDebugPauseReason.Manual);

    public NesDebugSnapshot CaptureSnapshot(NesDebugSnapshotOptions? options = null)
    {
        options ??= new NesDebugSnapshotOptions();
        lock (nes.SyncRoot)
        {
            var sections = options.Sections;
            var cpu = sections.HasFlag(NesDebugSnapshotSections.Cpu) ? nes.Cpu.CaptureDebugState() : null;
            var ppu = sections.HasFlag(NesDebugSnapshotSections.Ppu) ? nes.Ppu.CaptureDebugState() : null;
            var apu = sections.HasFlag(NesDebugSnapshotSections.Apu) ? nes.Apu.CaptureDebugState() : null;
            var timing = sections.HasFlag(NesDebugSnapshotSections.Timing)
                ? new NesTimingDebugState(nes.VideoStandard, ExecutionState, nes.CpuClockCounter,
                    nes.CurrentFrameNumber, nes.RequestedSpeedMultiplierUnsafe)
                : null;
            var memory = sections.HasFlag(NesDebugSnapshotSections.Memory)
                ? CaptureMemorySnapshots(options.MemoryRegions)
                : null;
            var disassembly = sections.HasFlag(NesDebugSnapshotSections.Disassembly)
                ? DisassembleUnsafe(options.DisassemblyStartAddress ?? nes.Cpu.CaptureDebugState().ProgramCounter,
                    options.DisassemblyInstructionCount)
                : null;
            IReadOnlyList<PatternTableSnapshot>? patterns = null;
            if (sections.HasFlag(NesDebugSnapshotSections.PatternTables))
            {
                patterns = new List<PatternTableSnapshot>
                {
                    CapturePatternTableUnsafe(0, options.PatternPalette),
                    CapturePatternTableUnsafe(1, options.PatternPalette)
                }.AsReadOnly();
            }
            return new NesDebugSnapshot(cpu, ppu, apu, timing, memory, disassembly, patterns);
        }
    }

    private void SetExecutionState(bool paused, NesDebugPauseReason reason)
    {
        NesExecutionState previous;
        NesExecutionState current;
        lock (nes.SyncRoot)
        {
            previous = nes.IsPausedUnsafe ? NesExecutionState.Paused : NesExecutionState.Running;
            nes.SetPausedUnsafe(paused);
            current = paused ? NesExecutionState.Paused : NesExecutionState.Running;
        }
        if (previous != current) ExecutionStateChanged?.Invoke(this, new(previous, current, reason));
    }

    public NesRunResult StepPpuDot() => StepUntil((_, _, dots) => dots >= 1);

    public NesRunResult StepCpuCycle()
    {
        ulong target;
        lock (nes.SyncRoot) target = nes.CpuClockCounter + 1;
        return StepUntil((cpu, _, _) => cpu >= target);
    }

    public NesRunResult StepInstruction()
    {
        ulong startingCpu;
        lock (nes.SyncRoot) startingCpu = nes.CpuClockCounter;
        return StepUntil((cpu, _, _) => cpu > startingCpu && nes.Cpu.IsInstructionBoundary);
    }

    public NesRunResult StepFrame()
    {
        ulong target;
        lock (nes.SyncRoot) target = nes.CurrentFrameNumber + 1;
        return StepUntil((_, frame, _) => frame >= target);
    }
    public int GetMemoryRegionSize(NesMemoryRegion region)
    {
        ValidateSingleRegion(region);
        lock (nes.SyncRoot) return GetMemoryRegionSizeUnsafe(region);
    }

    public void CopyMemoryRegion(NesMemoryRegion region, int offset, Span<byte> destination)
    {
        ValidateSingleRegion(region);
        lock (nes.SyncRoot)
        {
            ValidateRange(offset, destination.Length, GetMemoryRegionSizeUnsafe(region));
            CopyMemoryRegionUnsafe(region, offset, destination);
        }
    }

    public void WriteMemoryRegion(NesMemoryRegion region, int offset, ReadOnlySpan<byte> source)
    {
        ValidateSingleRegion(region);
        lock (nes.SyncRoot)
        {
            RequirePaused();
            ValidateRange(offset, source.Length, GetMemoryRegionSizeUnsafe(region));
            switch (region)
            {
                case NesMemoryRegion.CpuRam: nes.CpuBus.WriteRam(offset, source); break;
                case NesMemoryRegion.PpuVram: nes.PpuBus.WriteVram(offset, source); break;
                case NesMemoryRegion.PaletteRam: nes.PpuBus.WritePaletteRam(offset, source); break;
                case NesMemoryRegion.Oam: nes.Ppu.WriteOam(offset, source); break;
                case NesMemoryRegion.Chr:
                    if (nes.Cartridge == null || !nes.Cartridge.IsChrWritable)
                        throw new InvalidOperationException("The loaded cartridge contains read-only CHR ROM.");
                    nes.Cartridge.WriteChr(offset, source);
                    break;
                case NesMemoryRegion.PrgRom:
                    throw new InvalidOperationException("PRG ROM is read-only.");
                case NesMemoryRegion.CartridgeRam:
                    if (nes.Cartridge == null || nes.Cartridge.CartridgeRamSize == 0)
                        throw new InvalidOperationException("The current cartridge has no writable cartridge RAM.");
                    nes.Cartridge.WriteCartridgeRam(offset, source);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(region));
            }
        }
    }

    public byte PeekCpuMemory(ushort address)
    {
        lock (nes.SyncRoot) return nes.CpuBus.Peek(address);
    }

    public byte PeekPpuMemory(ushort address)
    {
        lock (nes.SyncRoot) return nes.PpuBus.Peek(address);
    }

    public void SetCpuRegisters(CpuRegisterValues registers)
    {
        lock (nes.SyncRoot)
        {
            RequirePaused();
            nes.Cpu.SetRegisters(registers);
        }
    }

    public void WritePpuRegister(ushort register, byte value)
    {
        if (register is < 0x2000 or > 0x2007) throw new ArgumentOutOfRangeException(nameof(register));
        lock (nes.SyncRoot)
        {
            RequirePaused();
            nes.Ppu.Write(register, value);
        }
    }

    public void WriteApuRegister(ushort register, byte value)
    {
        if (!(register is >= 0x4000 and <= 0x4013 or 0x4015 or 0x4017))
            throw new ArgumentOutOfRangeException(nameof(register));
        lock (nes.SyncRoot)
        {
            RequirePaused();
            nes.Apu.Write(register, value);
        }
    }
    public IReadOnlyList<DisassembledInstruction> Disassemble(ushort startAddress, int instructionCount)
    {
        lock (nes.SyncRoot) return DisassembleUnsafe(startAddress, instructionCount);
    }

    public IReadOnlyList<DisassembledInstruction> DisassembleAtProgramCounter(int instructionCount = 32)
    {
        lock (nes.SyncRoot)
            return DisassembleUnsafe(nes.Cpu.CaptureDebugState().ProgramCounter, instructionCount);
    }

    public PatternTableSnapshot CapturePatternTable(int tableIndex, int paletteIndex)
    {
        lock (nes.SyncRoot) return CapturePatternTableUnsafe(tableIndex, paletteIndex);
    }
    public NesBreakpoint AddBreakpoint(NesDebugBreakKind kind, ushort startAddress, ushort? endAddress = null)
    {
        if (!Enum.IsDefined(typeof(NesDebugBreakKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var end = endAddress ?? startAddress;
        if (end < startAddress) throw new ArgumentException("The breakpoint end address cannot precede its start address.", nameof(endAddress));
        lock (nes.SyncRoot)
        {
            var breakpoint = new NesBreakpoint(_nextBreakpointId++, kind, startAddress, end, true);
            _breakpoints.Add(breakpoint);
            return breakpoint;
        }
    }

    public bool SetBreakpointEnabled(long id, bool enabled)
    {
        lock (nes.SyncRoot)
        {
            var index = _breakpoints.FindIndex(x => x.Id == id);
            if (index < 0) return false;
            var current = _breakpoints[index];
            _breakpoints[index] = new NesBreakpoint(current.Id, current.Kind, current.StartAddress, current.EndAddress, enabled);
            if (!enabled && _pendingAccessBreak?.Breakpoint.Id == id) _pendingAccessBreak = null;
            return true;
        }
    }

    public bool RemoveBreakpoint(long id)
    {
        lock (nes.SyncRoot)
        {
            var index = _breakpoints.FindIndex(x => x.Id == id);
            if (index < 0) return false;
            _breakpoints.RemoveAt(index);
            if (_pendingAccessBreak?.Breakpoint.Id == id) _pendingAccessBreak = null;
            return true;
        }
    }

    public IReadOnlyList<NesBreakpoint> GetBreakpoints()
    {
        lock (nes.SyncRoot) return _breakpoints.ToArray();
    }

    public void ClearBreakpoints()
    {
        lock (nes.SyncRoot)
        {
            _breakpoints.Clear();
            _pendingAccessBreak = null;
        }
    }

    internal bool TryBreakBeforeCpuClockUnsafe()
    {
        if (_suppressBreakpoints || !nes.WillClockCpuUnsafe || !nes.Cpu.IsInstructionBoundary) return false;
        var pc = nes.Cpu.ProgramCounter;
        var breakpoint = FindBreakpoint(NesDebugBreakKind.Execute, pc);
        if (breakpoint == null) return false;
        QueueBreakpointUnsafe(new BreakOccurredEventArgs(breakpoint, pc, null, pc));
        return true;
    }

    internal void CompleteDotUnsafe()
    {
        if (_pendingAccessBreak == null || !nes.Cpu.IsInstructionBoundary) return;
        var hit = _pendingAccessBreak;
        _pendingAccessBreak = null;
        QueueBreakpointUnsafe(hit);
    }

    internal void DispatchPendingEvents()
    {
        ExecutionStateChangedEventArgs? state;
        BreakOccurredEventArgs? hit;
        lock (nes.SyncRoot)
        {
            state = _eventState;
            hit = _eventBreak;
            _eventState = null;
            _eventBreak = null;
        }
        if (state != null) ExecutionStateChanged?.Invoke(this, state);
        if (hit != null) BreakOccurred?.Invoke(this, hit);
    }

    internal void ResetTransientStateUnsafe()
    {
        _suppressBreakpoints = true;
        _pendingAccessBreak = null;
        _eventBreak = null;
        _eventState = null;
    }

    internal void FinishResetUnsafe() => _suppressBreakpoints = false;

    private IReadOnlyList<MemoryRegionSnapshot> CaptureMemorySnapshots(NesMemoryRegion requested)
    {
        var result = new List<MemoryRegionSnapshot>();
        foreach (var region in Enum.GetValues(typeof(NesMemoryRegion)).Cast<NesMemoryRegion>())
        {
            if (region is NesMemoryRegion.None or NesMemoryRegion.All || !requested.HasFlag(region)) continue;
            var size = GetMemoryRegionSizeUnsafe(region);
            if (size == 0) continue;
            var data = new byte[size];
            CopyMemoryRegionUnsafe(region, 0, data);
            result.Add(new MemoryRegionSnapshot(region, data, IsRegionWritable(region)));
        }
        return result.AsReadOnly();
    }

    private IReadOnlyList<DisassembledInstruction> DisassembleUnsafe(ushort startAddress, int instructionCount)
    {
        if (instructionCount <= 0) throw new ArgumentOutOfRangeException(nameof(instructionCount));
        var currentPc = nes.Cpu.CaptureDebugState().ProgramCounter;
        var address = startAddress;
        var result = new List<DisassembledInstruction>(instructionCount);
        for (var i = 0; i < instructionCount; i++)
        {
            var opcode = nes.CpuBus.Peek(address);
            var descriptor = CpuOpcodeTable.Get(opcode);
            if (descriptor == null)
            {
                result.Add(new DisassembledInstruction(address, new byte[] { opcode }, ".db", $"${opcode:X2}",
                    CpuAddressingMode.Implied, address == currentPc));
                address++;
                continue;
            }

            var bytes = new byte[descriptor.Length];
            for (var byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
                bytes[byteIndex] = nes.CpuBus.Peek((ushort)(address + byteIndex));
            result.Add(new DisassembledInstruction(address, bytes, descriptor.Mnemonic,
                FormatOperand(descriptor.Mode, address, bytes), descriptor.Mode, address == currentPc));
            address = (ushort)(address + descriptor.Length);
        }
        return result.AsReadOnly();
    }

    private PatternTableSnapshot CapturePatternTableUnsafe(int tableIndex, int paletteIndex)
    {
        if ((uint)tableIndex > 1) throw new ArgumentOutOfRangeException(nameof(tableIndex));
        if ((uint)paletteIndex > 3) throw new ArgumentOutOfRangeException(nameof(paletteIndex));
        var rgba = new byte[PatternTableSnapshot.Width * PatternTableSnapshot.Height * 4];
        var ppuMask = nes.Ppu.CaptureDebugState().Mask;
        var patternBase = tableIndex * 0x1000;
        for (var tileY = 0; tileY < 16; tileY++)
        for (var tileX = 0; tileX < 16; tileX++)
        {
            var tileAddress = patternBase + ((tileY * 16 + tileX) * 16);
            for (var row = 0; row < 8; row++)
            {
                var low = nes.PpuBus.Peek((ushort)(tileAddress + row));
                var high = nes.PpuBus.Peek((ushort)(tileAddress + row + 8));
                for (var column = 0; column < 8; column++)
                {
                    var bit = 7 - column;
                    var pixel = ((high >> bit) & 1) * 2 + ((low >> bit) & 1);
                    var paletteAddress = pixel == 0 ? 0x3F00 : 0x3F00 + (paletteIndex * 4) + pixel;
                    var color = nes.PpuBus.Peek((ushort)paletteAddress) & 0x3F;
                    if ((ppuMask & 1) != 0) color &= 0x30;
                    NesPalette.GetColor(nes.VideoStandard, color, ppuMask, out var red, out var green, out var blue);
                    var x = tileX * 8 + column;
                    var y = tileY * 8 + row;
                    var offset = (y * PatternTableSnapshot.Width + x) * 4;
                    rgba[offset] = red; rgba[offset + 1] = green; rgba[offset + 2] = blue; rgba[offset + 3] = 0xFF;
                }
            }
        }
        return new PatternTableSnapshot(tableIndex, paletteIndex, rgba);
    }

    private static string FormatOperand(CpuAddressingMode mode, ushort address, byte[] bytes)
    {
        var value = bytes.Length > 1 ? bytes[1] : (byte)0;
        var absolute = bytes.Length > 2 ? (ushort)(bytes[1] | (bytes[2] << 8)) : (ushort)0;
        return mode switch
        {
            CpuAddressingMode.Implied => string.Empty,
            CpuAddressingMode.Accumulator => "A",
            CpuAddressingMode.Immediate => $"#${value:X2}",
            CpuAddressingMode.ZeroPage => $"${value:X2}",
            CpuAddressingMode.ZeroPageX => $"${value:X2},X",
            CpuAddressingMode.ZeroPageY => $"${value:X2},Y",
            CpuAddressingMode.Relative => $"${(ushort)(address + 2 + unchecked((sbyte)value)):X4}",
            CpuAddressingMode.Absolute => $"${absolute:X4}",
            CpuAddressingMode.AbsoluteX => $"${absolute:X4},X",
            CpuAddressingMode.AbsoluteY => $"${absolute:X4},Y",
            CpuAddressingMode.Indirect => $"(${absolute:X4})",
            CpuAddressingMode.IndexedIndirect => $"(${value:X2},X)",
            CpuAddressingMode.IndirectIndexed => $"(${value:X2}),Y",
            _ => string.Empty
        };
    }

    private int GetMemoryRegionSizeUnsafe(NesMemoryRegion region) => region switch
    {
        NesMemoryRegion.CpuRam => nes.CpuBus.RamSize,
        NesMemoryRegion.PpuVram => nes.PpuBus.VramSize,
        NesMemoryRegion.PaletteRam => nes.PpuBus.PaletteRamSize,
        NesMemoryRegion.Oam => 0x100,
        NesMemoryRegion.PrgRom => nes.Cartridge?.PrgRomSize ?? 0,
        NesMemoryRegion.Chr => nes.Cartridge?.ChrSize ?? 0,
        NesMemoryRegion.CartridgeRam => nes.Cartridge?.CartridgeRamSize ?? 0,
        _ => throw new ArgumentOutOfRangeException(nameof(region))
    };

    private void CopyMemoryRegionUnsafe(NesMemoryRegion region, int offset, Span<byte> destination)
    {
        if (destination.Length == 0) return;
        switch (region)
        {
            case NesMemoryRegion.CpuRam: nes.CpuBus.CopyRam(offset, destination); break;
            case NesMemoryRegion.PpuVram: nes.PpuBus.CopyVram(offset, destination); break;
            case NesMemoryRegion.PaletteRam: nes.PpuBus.CopyPaletteRam(offset, destination); break;
            case NesMemoryRegion.Oam: nes.Ppu.CopyOam(offset, destination); break;
            case NesMemoryRegion.PrgRom: nes.Cartridge!.CopyPrgRom(offset, destination); break;
            case NesMemoryRegion.Chr: nes.Cartridge!.CopyChr(offset, destination); break;
            case NesMemoryRegion.CartridgeRam: nes.Cartridge!.CopyCartridgeRam(offset, destination); break;
            default: throw new ArgumentOutOfRangeException(nameof(region));
        }
    }

    private bool IsRegionWritable(NesMemoryRegion region) => region switch
    {
        NesMemoryRegion.CpuRam or NesMemoryRegion.PpuVram or NesMemoryRegion.PaletteRam or NesMemoryRegion.Oam => true,
        NesMemoryRegion.Chr => nes.Cartridge?.IsChrWritable == true,
        NesMemoryRegion.CartridgeRam => GetMemoryRegionSizeUnsafe(region) > 0,
        _ => false
    };

    private void RequirePaused()
    {
        if (!nes.IsPausedUnsafe) throw new InvalidOperationException("The NES must be paused for this operation.");
    }

    private NesRunResult StepUntil(Func<ulong, ulong, int, bool> completed)
    {
        var frames = new List<FrameReadyEventArgs>();
        NesRunResult result;
        lock (nes.SyncRoot)
        {
            RequirePaused();
            var startingCpu = nes.CpuClockCounter;
            var startingFrame = nes.CurrentFrameNumber;
            var dots = 0;
            _suppressBreakpoints = true;
            try
            {
                do
                {
                    if (!nes.ExecuteDotUnsafe(false, out var frame)) break;
                    dots++;
                    if (frame != null) frames.Add(frame);
                }
                while (!completed(nes.CpuClockCounter, nes.CurrentFrameNumber, dots));
            }
            finally { _suppressBreakpoints = false; }
            result = new NesRunResult(dots, nes.CpuClockCounter - startingCpu,
                nes.CurrentFrameNumber - startingFrame, NesRunStopReason.Completed);
        }
        foreach (var frame in frames) nes.RaiseFrameReady(frame);
        return result;
    }

    private void ObserveCpuAccess(NesDebugBreakKind kind, ushort address, byte value)
    {
        if (_suppressBreakpoints || _pendingAccessBreak != null) return;
        var breakpoint = FindBreakpoint(kind, address);
        if (breakpoint == null) return;
        _pendingAccessBreak = new BreakOccurredEventArgs(breakpoint, address, value, nes.Cpu.ProgramCounter);
    }

    private NesBreakpoint? FindBreakpoint(NesDebugBreakKind kind, ushort address) =>
        _breakpoints.FirstOrDefault(x => x.IsEnabled && x.Kind == kind &&
            address >= x.StartAddress && address <= x.EndAddress);

    private void QueueBreakpointUnsafe(BreakOccurredEventArgs hit)
    {
        var previous = nes.IsPausedUnsafe ? NesExecutionState.Paused : NesExecutionState.Running;
        nes.SetPausedUnsafe(true);
        _eventBreak = hit;
        if (previous != NesExecutionState.Paused)
            _eventState = new ExecutionStateChangedEventArgs(previous, NesExecutionState.Paused, NesDebugPauseReason.Breakpoint);
    }

    private static void ValidateSingleRegion(NesMemoryRegion region)
    {
        var value = (int)region;
        if (value == 0 || (value & (value - 1)) != 0) throw new ArgumentException("Specify exactly one memory region.", nameof(region));
    }

    private static void ValidateRange(int offset, int length, int size)
    {
        if (offset < 0 || length < 0 || offset > size - length)
            throw new ArgumentOutOfRangeException(nameof(offset), "The requested range is outside the memory region.");
    }
}
