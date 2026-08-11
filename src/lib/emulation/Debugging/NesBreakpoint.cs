namespace Sheep.Emulation.Nes.Debugging;

public sealed class NesBreakpoint(
    long id, NesDebugBreakKind kind, ushort startAddress, ushort endAddress, bool isEnabled)
{
    public long Id { get; } = id;
    public NesDebugBreakKind Kind { get; } = kind;
    public ushort StartAddress { get; } = startAddress;
    public ushort EndAddress { get; } = endAddress;
    public bool IsEnabled { get; } = isEnabled;
}