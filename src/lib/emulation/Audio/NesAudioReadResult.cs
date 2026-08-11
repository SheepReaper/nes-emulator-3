namespace Sheep.Emulation.Nes.Audio;

/// <summary>Describes one pull from the emulated audio queue.</summary>
public readonly struct NesAudioReadResult(int samplesWritten, int samplesRemaining, bool underrun)
{
    /// <summary>Gets the number of samples copied to the destination.</summary>
    public int SamplesWritten { get; } = samplesWritten;

    /// <summary>Gets the samples still queued after the read.</summary>
    public int SamplesRemaining { get; } = samplesRemaining;

    /// <summary>Gets whether the destination could not be completely filled.</summary>
    public bool Underrun { get; } = underrun;
}