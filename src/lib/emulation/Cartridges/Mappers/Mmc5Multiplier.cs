namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>
/// Hardware 8-bit multiplier unit for MMC5 mapper ($5205 / $5206).
/// </summary>
internal sealed class Mmc5Multiplier
{
    private byte _multiplicand;
    private byte _multiplier;

    internal void WriteMultiplicand(byte value) => _multiplicand = value;
    internal void WriteMultiplier(byte value) => _multiplier = value;
    internal byte ReadLow() => (byte)(_multiplicand * _multiplier);
    internal byte ReadHigh() => (byte)((_multiplicand * _multiplier) >> 8);

    internal void Reset()
    {
        _multiplicand = 0;
        _multiplier = 0;
    }
}
