namespace Sheep.Emulation.Nes.Debugging;

public sealed class PpuDebugState(
    byte control, byte mask, byte status, byte oamAddress, ushort vramAddress,
    ushort temporaryVramAddress, byte fineX, bool writeToggle, byte dataBuffer,
    int scanline, int dot, ulong frameNumber, bool oddFrame, int evaluatedSpriteCount)
{
    public byte Control { get; } = control;
    public byte Mask { get; } = mask;
    public byte Status { get; } = status;
    public byte OamAddress { get; } = oamAddress;
    public ushort VramAddress { get; } = vramAddress;
    public ushort TemporaryVramAddress { get; } = temporaryVramAddress;
    public byte FineX { get; } = fineX;
    public bool WriteToggle { get; } = writeToggle;
    public byte DataBuffer { get; } = dataBuffer;
    public int Scanline { get; } = scanline;
    public int Dot { get; } = dot;
    public ulong FrameNumber { get; } = frameNumber;
    public bool IsOddFrame { get; } = oddFrame;
    public int EvaluatedSpriteCount { get; } = evaluatedSpriteCount;
}