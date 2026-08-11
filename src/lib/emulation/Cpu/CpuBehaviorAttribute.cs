using System;
namespace Sheep.Emulation.Nes.Cpu;

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