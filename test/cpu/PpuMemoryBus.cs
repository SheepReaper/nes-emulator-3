namespace Sheep.Emulation.Nes.Tests;

/// <summary>
/// Simple mock memory bus for testing PPU.
/// </summary>
internal sealed class MemoryBus : IBus
{
    public byte[] Memory { get; } = new byte[0x4000];
    public byte Read(ushort address) => Memory[address & 0x3FFF];
    public void Write(ushort address, byte value) => Memory[address & 0x3FFF] = value;
}
