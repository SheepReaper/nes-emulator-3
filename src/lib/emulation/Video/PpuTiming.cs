namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Scanline and cycle state and phase progression for PPU.
/// </summary>
internal sealed class PpuTiming
{
    private readonly NesTiming _timing;
    private readonly NesVideoStandard _standard;
    private int _scanline;
    private int _cycle;
    private bool _oddFrame;
    private bool _oddFrameSkipRenderingEnabled;
    private PpuPhase _phase = PpuPhase.Visible;

    internal PpuTiming(NesTiming timing, NesVideoStandard standard)
    {
        _timing = timing;
        _standard = standard;
    }

    internal int Scanline => _scanline;
    internal int Cycle => _cycle;
    internal bool OddFrame => _oddFrame;
    internal PpuPhase Phase => _phase;
    internal int PreRenderScanline => _timing.ScanlinesPerFrame - 1;
    internal int DotsPerScanline => _timing.DotsPerScanline;
    internal int ScanlinesPerFrame => _timing.ScanlinesPerFrame;

    internal void Reset()
    {
        _scanline = 0;
        _cycle = 0;
        _oddFrame = false;
        _oddFrameSkipRenderingEnabled = false;
        _phase = PpuPhase.Visible;
    }

    internal void Advance(bool renderingEnabled)
    {
        var preRender = PreRenderScanline;
        if (_standard == NesVideoStandard.Ntsc && _oddFrame &&
            _scanline == preRender && _cycle == _timing.DotsPerScanline - 3)
        {
            _oddFrameSkipRenderingEnabled = renderingEnabled;
        }

        if (_standard == NesVideoStandard.Ntsc && _oddFrame && _oddFrameSkipRenderingEnabled &&
            _scanline == preRender && _cycle == _timing.DotsPerScanline - 2)
        {
            _cycle = 0;
            _scanline = 0;
            _oddFrame = false;
            _phase = PpuPhase.Visible;
            return;
        }

        _cycle++;
        if (_cycle < _timing.DotsPerScanline)
        {
            return;
        }

        _cycle = 0;
        _scanline++;
        if (_scanline < _timing.ScanlinesPerFrame)
        {
            _phase = _scanline switch
            {
                < Ppu.FrameHeight => PpuPhase.Visible,
                Ppu.FrameHeight => PpuPhase.PostRender,
                _ when _scanline == preRender => PpuPhase.PreRender,
                _ => PpuPhase.VBlank
            };
            return;
        }

        _scanline = 0;
        _oddFrame = !_oddFrame;
        _phase = PpuPhase.Visible;
    }

    internal void AdvanceCycleDirect(int count) => _cycle += count;
}
