using System;
namespace Sheep.Emulation.Nes.Debugging;

public sealed class ApuDebugState(
    bool isImplemented, ReadOnlyMemory<byte> registers, int frameCycle = 0,
    bool fiveStepMode = false, bool frameIrq = false, bool dmcIrq = false,
    byte pulse1Length = 0, byte pulse2Length = 0, byte triangleLength = 0,
    byte noiseLength = 0, byte pulse1Output = 0, byte pulse2Output = 0,
    byte triangleOutput = 0, byte noiseOutput = 0, byte dmcOutput = 0,
    ushort dmcAddress = 0, ushort dmcBytesRemaining = 0)
{
    public bool IsImplemented { get; } = isImplemented;
    public ReadOnlyMemory<byte> Registers { get; } = registers;
    public int FrameCycle { get; } = frameCycle;
    public bool FiveStepMode { get; } = fiveStepMode;
    public bool FrameIrq { get; } = frameIrq;
    public bool DmcIrq { get; } = dmcIrq;
    public byte Pulse1Length { get; } = pulse1Length;
    public byte Pulse2Length { get; } = pulse2Length;
    public byte TriangleLength { get; } = triangleLength;
    public byte NoiseLength { get; } = noiseLength;
    public byte Pulse1Output { get; } = pulse1Output;
    public byte Pulse2Output { get; } = pulse2Output;
    public byte TriangleOutput { get; } = triangleOutput;
    public byte NoiseOutput { get; } = noiseOutput;
    public byte DmcOutput { get; } = dmcOutput;
    public ushort DmcAddress { get; } = dmcAddress;
    public ushort DmcBytesRemaining { get; } = dmcBytesRemaining;
}