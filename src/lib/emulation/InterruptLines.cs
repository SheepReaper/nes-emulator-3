namespace SR.Emulation.Nes;

public sealed class InterruptLines
{
    private bool _irq;

    public bool Nmi { get; set; }
    internal bool DelayNmiOneInstruction { get; set; }
    public bool Irq
    {
        get => _irq || MapperIrq;
        set => _irq = value;
    }

    internal bool MapperIrq { get; set; }
}
