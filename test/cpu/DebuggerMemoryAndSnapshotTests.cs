using Sheep.Emulation.Nes.Debugging;

using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class DebuggerMemoryAndSnapshotTests
{
    [Fact]
    public void PhysicalMemoryRegionsCanBeCopiedAndOnlyWritableRegionsCanBeEditedWhilePaused()
    {
        var nes = new NesSystem();
        nes.LoadRom(DebuggerTestHelper.CreateRom(chrBanks: 1));
        var debugger = nes.Debugger;

        Assert.Equal(0x0800, debugger.GetMemoryRegionSize(NesMemoryRegion.CpuRam));
        Assert.Equal(0x1000, debugger.GetMemoryRegionSize(NesMemoryRegion.PpuVram));
        Assert.Equal(0x20, debugger.GetMemoryRegionSize(NesMemoryRegion.PaletteRam));
        Assert.Equal(0x100, debugger.GetMemoryRegionSize(NesMemoryRegion.Oam));
        Assert.Equal(0x4000, debugger.GetMemoryRegionSize(NesMemoryRegion.PrgRom));
        Assert.Equal(0x2000, debugger.GetMemoryRegionSize(NesMemoryRegion.Chr));
        Assert.Throws<InvalidOperationException>(() =>
            debugger.WriteMemoryRegion(NesMemoryRegion.CpuRam, 0, [0x42]));

        debugger.Pause();
        debugger.WriteMemoryRegion(NesMemoryRegion.CpuRam, 0x12, [0x42, 0x43]);
        var copied = new byte[2];
        debugger.CopyMemoryRegion(NesMemoryRegion.CpuRam, 0x12, copied);
        Assert.Equal(new byte[] { 0x42, 0x43 }, copied);
        Assert.Throws<InvalidOperationException>(() =>
            debugger.WriteMemoryRegion(NesMemoryRegion.PrgRom, 0, [1]));
        Assert.Throws<InvalidOperationException>(() =>
            debugger.WriteMemoryRegion(NesMemoryRegion.Chr, 0, [1]));
    }

    [Fact]
    public void ChrRamIsAllocatedAndWritableWhenRomHasNoChrBanks()
    {
        var nes = new NesSystem();
        nes.LoadRom(DebuggerTestHelper.CreateRom(chrBanks: 0));
        nes.Debugger.Pause();

        Assert.Equal(0x2000, nes.Debugger.GetMemoryRegionSize(NesMemoryRegion.Chr));
        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.Chr, 7, [0xA5]);
        Assert.Equal(0xA5, nes.Debugger.PeekPpuMemory(7));
    }

    [Fact]
    public void MemorySnapshotCopiesOnlyRequestedRegions()
    {
        var nes = new NesSystem();
        nes.Debugger.Pause();
        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.CpuRam, 0, [0x31]);
        var snapshot = nes.Debugger.CaptureSnapshot(new NesDebugSnapshotOptions
        {
            Sections = NesDebugSnapshotSections.Memory,
            MemoryRegions = NesMemoryRegion.CpuRam | NesMemoryRegion.PaletteRam
        });

        Assert.Equal(2, snapshot.Memory!.Count);
        Assert.Contains(snapshot.Memory, x => x.Region == NesMemoryRegion.CpuRam && x.Data.Span[0] == 0x31);
        Assert.DoesNotContain(snapshot.Memory, x => x.Region == NesMemoryRegion.Oam);
    }

    [Fact]
    public void MemorySnapshotsRemainUnchangedAfterEmulatorMemoryIsEdited()
    {
        var nes = new NesSystem();
        nes.Debugger.Pause();
        var options = new NesDebugSnapshotOptions
        {
            Sections = NesDebugSnapshotSections.Memory,
            MemoryRegions = NesMemoryRegion.CpuRam
        };
        var before = nes.Debugger.CaptureSnapshot(options);
        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.CpuRam, 0, [0xEE]);

        Assert.Equal(0, before.Memory![0].Data.Span[0]);
        Assert.Equal(0xEE, nes.Debugger.CaptureSnapshot(options).Memory![0].Data.Span[0]);
    }
}
