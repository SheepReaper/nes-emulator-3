using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sheep.Nes.Lab;

public enum RomAvailability { NotChecked, Missing, InstalledVerified, ChecksumMismatch }
public enum RomProtocolKind { Blargg6000, LegacyResultAddress, SuccessProgramCounter, TextConsole }

public sealed record RomProtocolDescriptor(
    RomProtocolKind Kind,
    ushort? Address = null,
    long MinimumPpuDots = 0,
    IReadOnlyList<string>? SuccessMarkers = null);

public sealed record RomCatalogProvenance(
    string ManifestPath,
    string ManifestSha256,
    string UpstreamCommit);

public sealed record RomCatalogEntry(
    string Suite,
    string Name,
    string RelativePath,
    string ExpectedSha256,
    string? ActualSha256,
    long MaximumPpuDots,
    string VideoStandard,
    string? SkipReason,
    string? KnownGap,
    RomAvailability Availability,
    string? RomPath,
    IReadOnlyList<RomProtocolDescriptor> Protocols,
    RomCatalogProvenance Provenance);

public sealed record RomCatalog(
    string UpstreamCommit,
    string ManifestSha256,
    string ManifestPath,
    IReadOnlyList<RomCatalogEntry> Entries)
{
    public static RomCatalog Load(string manifestPath, string? assetRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        var absoluteManifestPath = Path.GetFullPath(manifestPath);
        var bytes = File.ReadAllBytes(absoluteManifestPath);
        var manifestHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        serializerOptions.Converters.Add(new JsonStringEnumConverter());
        var document = JsonSerializer.Deserialize<ManifestDocument>(bytes, serializerOptions)
            ?? throw new InvalidDataException("The test ROM manifest is empty.");
        var provenance = new RomCatalogProvenance(
            absoluteManifestPath, manifestHash, document.UpstreamCommit);
        var entries = document.Tests.Select(test => CreateEntry(
            test, assetRoot, provenance, ResolveProtocols(document, test))).ToArray();
        return new RomCatalog(
            document.UpstreamCommit, manifestHash, absoluteManifestPath, entries);
    }

    public RomCatalogEntry Find(string? suite, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var matches = Entries.Where(entry =>
            entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            (suite is null || entry.Suite.Equals(suite, StringComparison.OrdinalIgnoreCase))).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new KeyNotFoundException($"No ROM case named '{name}' was found."),
            _ => throw new InvalidOperationException(
                $"ROM case name '{name}' is ambiguous; specify its suite.")
        };
    }

    private static RomCatalogEntry CreateEntry(
        ManifestTest test,
        string? assetRoot,
        RomCatalogProvenance provenance,
        IReadOnlyList<RomProtocolDescriptor> protocols)
    {
        string? romPath = null;
        string? actualHash = null;
        var availability = RomAvailability.NotChecked;
        if (assetRoot is not null)
        {
            romPath = Path.GetFullPath(Path.Combine(
                assetRoot, test.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(romPath))
            {
                availability = RomAvailability.Missing;
            }
            else
            {
                actualHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(romPath)));
                availability = actualHash.Equals(test.Sha256, StringComparison.OrdinalIgnoreCase)
                    ? RomAvailability.InstalledVerified
                    : RomAvailability.ChecksumMismatch;
            }
        }

        return new RomCatalogEntry(
            test.Suite, test.Name, test.Path, test.Sha256, actualHash,
            test.MaximumPpuDots, test.VideoStandard ?? "Ntsc", test.SkipReason,
            test.KnownGap, availability, romPath,
            protocols, provenance);
    }

    private static IReadOnlyList<RomProtocolDescriptor> ResolveProtocols(
        ManifestDocument document, ManifestTest test) =>
        (document.ProtocolRules ?? [])
            .Where(rule => rule.Suite.Equals(test.Suite, StringComparison.OrdinalIgnoreCase) &&
                (rule.Name is null || rule.Name.Equals(test.Name, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(rule => rule.Protocols)
            .Concat(document.DefaultProtocols ?? [])
            .ToArray();

    private sealed record ManifestDocument(
        string UpstreamCommit,
        ManifestTest[] Tests,
        RomProtocolDescriptor[]? DefaultProtocols = null,
        ManifestProtocolRule[]? ProtocolRules = null);
    private sealed record ManifestProtocolRule(
        string Suite,
        RomProtocolDescriptor[] Protocols,
        string? Name = null);
    private sealed record ManifestTest(
        string Suite,
        string Name,
        string Path,
        string Sha256,
        long MaximumPpuDots,
        string? VideoStandard = null,
        string? SkipReason = null,
        string? KnownGap = null);
}
