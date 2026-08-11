namespace Sheep.Emulation.Nes.Cpu;

/// <summary>
/// Classifies CPU behavior that must remain visible during CPU-family refactoring.
/// </summary>
public enum CpuBehaviorKind
{
    /// <summary>Behavior inherited from the original NMOS MOS 6502.</summary>
    Nmos6502Quirk,

    /// <summary>Behavior specific to the NES Ricoh 2A03/2A07 CPU.</summary>
    Nes2A03Deviation
}