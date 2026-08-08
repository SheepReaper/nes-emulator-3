using System;

namespace SR.Emulation.Nes;

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

/// <summary>
/// Annotates CPU implementation methods whose behavior is hardware-specific.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class CpuBehaviorAttribute(
    CpuBehaviorKind kind,
    string description,
    string reference) : Attribute
{
    /// <summary>Gets the hardware-behavior category.</summary>
    public CpuBehaviorKind Kind { get; } = kind;

    /// <summary>Gets a concise description of the observable behavior.</summary>
    public string Description { get; } = description;

    /// <summary>Gets the authoritative reference URL for the behavior.</summary>
    public string Reference { get; } = reference;
}
