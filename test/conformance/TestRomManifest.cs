using System.Security.Cryptography;
using System.Text.Json;

namespace SR.Emulation.Nes.ConformanceTests;

internal sealed record TestRomDefinition(
    string Suite,
    string Name,
    string Path,
    string Sha256,
    long MaximumPpuDots);

internal sealed record TestRomManifest(string UpstreamCommit, TestRomDefinition[] Tests)
{
    internal static TestRomManifest Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<TestRomManifest>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("The test ROM manifest is empty.");
    }

    internal static void VerifyChecksum(TestRomDefinition definition, string path)
    {
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!actual.Equals(definition.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Checksum mismatch for '{definition.Name}'. Expected {definition.Sha256}, got {actual}.");
    }
}
