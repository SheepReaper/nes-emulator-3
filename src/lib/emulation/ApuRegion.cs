namespace SR.Emulation.Nes;

public record ApuRegion
{
    private readonly string _value;
    private ApuRegion(string value)
    {
        _value = value;
    }

    public static readonly ApuRegion Ntsc = new("ntsc");
    public static readonly ApuRegion Pal = new("pal");
    public static ApuRegion Default => Ntsc;
    public override string ToString()
    {
        return _value;
    }
}

