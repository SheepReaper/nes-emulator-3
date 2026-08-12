using System.Security.Cryptography;
using Sheep.Nes.Lab;

namespace Sheep.Emulation.Nes.ConformanceTests;

public sealed record TestRomDefinition(
    string Suite,
    string Name,
    string Path,
    string Sha256,
    long MaximumPpuDots,
    string VideoStandard,
    IReadOnlyList<RomProtocolDescriptor> Protocols)
{
    public TestRomDefinition(string suite, string name, string path, string sha256, long maximumPpuDots)
        : this(suite, name, path, sha256, maximumPpuDots, "Ntsc",
            [new RomProtocolDescriptor(RomProtocolKind.Blargg6000)]) { }
}

internal sealed record TestRomManifest(string UpstreamCommit, TestRomDefinition[] Tests)
{
    internal static TestRomManifest Load(string path)
    {
        var catalog = RomCatalog.Load(path);
        return new TestRomManifest(catalog.UpstreamCommit, catalog.Entries.Select(entry =>
            new TestRomDefinition(entry.Suite, entry.Name, entry.RelativePath,
                entry.ExpectedSha256, entry.MaximumPpuDots, entry.VideoStandard,
                entry.Protocols)).ToArray());
    }

    internal static void VerifyChecksum(TestRomDefinition definition, string path)
    {
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!actual.Equals(definition.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Checksum mismatch for '{definition.Name}'. Expected {definition.Sha256}, got {actual}.");
    }
}
