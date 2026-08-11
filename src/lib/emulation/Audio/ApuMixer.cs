using System;

namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Mixes the five APU channel outputs into a single audio sample using
/// the NES non-linear mixing formula, then applies high-pass and low-pass
/// filters matching the original hardware.
/// </summary>
internal sealed class ApuMixer
{
    internal const int SampleRate = 48_000;
    private static readonly double[] PulseLookup = BuildPulseLookup();
    private static readonly double[] TndLookup = BuildTndLookup();

    private NesAudioFilterMode _filterMode = NesAudioFilterMode.Nes;
    private double _hp90In, _hp90Out;
    private double _hp440In, _hp440Out;
    private double _lpOut;

    internal NesAudioFilterMode FilterMode
    {
        get => _filterMode;
        set
        {
            if (!Enum.IsDefined(typeof(NesAudioFilterMode), value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_filterMode == value) return;
            _filterMode = value;
            ResetFilters();
        }
    }

    internal void ResetFilters()
    {
        _hp90In = _hp90Out = 0;
        _hp440In = _hp440Out = 0;
        _lpOut = 0;
    }

    internal static double Mix(byte pulse1, byte pulse2, byte triangle, byte noise, byte dmc)
        => PulseLookup[pulse1 + pulse2] + TndLookup[(triangle << 11) | (noise << 7) | dmc];

    internal float ApplyFilter(double input)
    {
        if (_filterMode == NesAudioFilterMode.Raw) return (float)input;
        var hp90 = HighPass(input, 90, ref _hp90In, ref _hp90Out);
        var hp440 = HighPass(hp90, 440, ref _hp440In, ref _hp440Out);
        var lpCoeff = 1.0 - Math.Exp(-2.0 * Math.PI * 14_000 / SampleRate);
        _lpOut += lpCoeff * (hp440 - _lpOut);
        return (float)_lpOut;
    }

    private static double HighPass(
        double input, double freq, ref double prevIn, ref double prevOut)
    {
        var c = Math.Exp(-2.0 * Math.PI * freq / SampleRate);
        var output = c * (prevOut + input - prevIn);
        prevIn = input;
        prevOut = output;
        return output;
    }

    private static double[] BuildPulseLookup()
    {
        var t = new double[31];
        for (var s = 1; s < t.Length; s++)
            t[s] = 95.88 / (8128.0 / s + 100.0);
        return t;
    }

    private static double[] BuildTndLookup()
    {
        var t = new double[16 * 16 * 128];
        for (var tr = 0; tr < 16; tr++)
            for (var n = 0; n < 16; n++)
                for (var d = 0; d < 128; d++)
                {
                    var v = tr / 8227.0 + n / 12241.0 + d / 22638.0;
                    t[(tr << 11) | (n << 7) | d] = v == 0 ? 0 : 159.79 / (1.0 / v + 100.0);
                }
        return t;
    }
}
