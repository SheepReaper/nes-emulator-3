namespace Sheep.Nes.Lab;

public sealed record RomDiagnosis(
    int SchemaVersion,
    string Suite,
    string Name,
    int Code,
    string ExpectedRomSha256,
    RomAvailability Availability,
    IReadOnlyList<RomProtocolDescriptor> Protocols,
    IReadOnlyList<AssemblyResultEncoding> Meanings,
    RomCatalogProvenance ManifestProvenance,
    string? KnownGap);

public static class RomDiagnosisBuilder
{
    public static RomDiagnosis Diagnose(
        RomCatalog catalog,
        string assetRoot,
        string? suite,
        string name,
        int code)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);
        ArgumentOutOfRangeException.ThrowIfNegative(code);
        var entry = catalog.Find(suite, name);
        var source = AssemblySourceIndex.Build(entry, assetRoot);
        return new RomDiagnosis(
            1, entry.Suite, entry.Name, code, entry.ExpectedSha256, entry.Availability,
            entry.Protocols, source.ResultEncodings.Where(item => item.Code == code).ToArray(),
            entry.Provenance, entry.KnownGap);
    }
}
