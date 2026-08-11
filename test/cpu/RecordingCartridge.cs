using Sheep.Emulation.Nes.Cartridges;
using Sheep.Emulation.Nes.Cpu;
using Sheep.Emulation.Nes.Video;

namespace Sheep.Emulation.Nes.Tests;

internal sealed class RecordingCartridge(NametableMirroring mirroring)
    : Cartridge(new byte[0x8000], new byte[0x2000], mirroring)
{
    private readonly Dictionary<ushort, byte> _cpuValues = [];
    public ushort? LastCpuReadAddress { get; private set; }
    public ushort? LastCpuWriteAddress { get; private set; }
    public byte LastCpuWriteValue { get; private set; }
    public ushort? LastPpuWriteAddress { get; private set; }
    public byte LastPpuWriteValue { get; private set; }

    public override byte CpuRead(ushort address)
    {
        LastCpuReadAddress = address;
        return _cpuValues.GetValueOrDefault(address, (byte)address);
    }

    public void LoadCpu(ushort address, ReadOnlySpan<byte> values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            _cpuValues[(ushort)(address + i)] = values[i];
        }
    }

    public override void CpuWrite(ushort address, byte value)
    {
        LastCpuWriteAddress = address;
        LastCpuWriteValue = value;
        _cpuValues[address] = value;
    }

    public override byte PpuRead(ushort address) => (byte)address;

    public override void PpuWrite(ushort address, byte value)
    {
        LastPpuWriteAddress = address;
        LastPpuWriteValue = value;
    }
}
