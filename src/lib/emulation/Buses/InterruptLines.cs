namespace Sheep.Emulation.Nes.Buses;

public sealed class InterruptLines
{
    private bool _nmiEdgePending;
    private int _nmiEdgeAgePpuDots;

    public bool Nmi
    {
        get;
        set
        {
            if (value && !field)
            {
                _nmiEdgePending = true;
                _nmiEdgeAgePpuDots = 0;
            }
            field = value;
        }
    }

    internal bool ConsumeNmiEdge()
    {
        var pending = _nmiEdgePending;
        _nmiEdgePending = false;
        return pending;
    }

    internal void AdvancePpuDot()
    {
        if (_nmiEdgePending) _nmiEdgeAgePpuDots++;
    }

    internal void AdvancePpuDots(int count)
    {
        if (_nmiEdgePending) _nmiEdgeAgePpuDots += count;
    }

    internal void CancelShortNmiEdge(int minimumPpuDots)
    {
        if (_nmiEdgePending && _nmiEdgeAgePpuDots < minimumPpuDots)
            _nmiEdgePending = false;
    }

    internal bool DelayNmiOneInstruction { get; set; }
    public bool Irq
    {
        get => field || MapperIrq || (ApuFrameIrq && !ApuFrameIrqInhibited) || ApuDmcIrq;
        set;
    }

    internal bool MapperIrq { get; set; }
    internal bool ApuFrameIrq { get; set; }
    internal bool ApuFrameIrqInhibited { get; set; }
    internal bool ApuDmcIrq { get; set; }
}