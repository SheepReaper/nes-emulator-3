namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Status and Control register state and VBlank/NMI tracking for PPU.
/// </summary>
internal sealed class PpuState(InterruptLines interrupts)
{
    private PpuCtrl _ctrl;
    private PpuMask _mask;
    private PpuStatus _status;
    private bool _suppressVblank;
    private int _vblankNmiDelayDots;

    internal ref PpuCtrl Ctrl => ref _ctrl;
    internal ref PpuMask Mask => ref _mask;
    internal ref PpuStatus Status => ref _status;
    internal bool SuppressVblank { get => _suppressVblank; set => _suppressVblank = value; }
    internal int VblankNmiDelayDots { get => _vblankNmiDelayDots; set => _vblankNmiDelayDots = value; }

    internal void Reset()
    {
        _ctrl.Value = 0;
        _mask.Value = 0;
        _status.Value = 0;
        _suppressVblank = false;
        _vblankNmiDelayDots = 0;
        interrupts.Nmi = false;
        interrupts.DelayNmiOneInstruction = false;
    }

    internal void WriteControl(byte value)
    {
        var wasNmiEnabled = _ctrl.VBlankNmiEnable;
        _ctrl.Value = value;
        if (!wasNmiEnabled && _ctrl.VBlankNmiEnable && _status.VBlank)
        {
            _vblankNmiDelayDots = 0;
            interrupts.Nmi = true;
            interrupts.DelayNmiOneInstruction = true;
        }

        if (!_ctrl.VBlankNmiEnable)
        {
            _vblankNmiDelayDots = 0;
            interrupts.Nmi = false;
        }
    }

    internal byte ReadStatus(byte ioLatch)
    {
        var value = (byte)((_status.Value & 0xE0) | (ioLatch & 0x1F));
        _status.VBlank = false;
        _vblankNmiDelayDots = 0;
        interrupts.Nmi = false;
        interrupts.DelayNmiOneInstruction = false;
        return value;
    }

    internal void AdvanceDot()
    {
        if (_vblankNmiDelayDots > 0 && --_vblankNmiDelayDots == 0 &&
            _status.VBlank && _ctrl.VBlankNmiEnable)
        {
            interrupts.Nmi = true;
        }
    }
}
