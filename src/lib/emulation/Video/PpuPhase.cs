namespace Sheep.Emulation.Nes.Video;

internal enum PpuPhase : byte
{
    Visible,
    PostRender,
    VBlank,
    PreRender
}