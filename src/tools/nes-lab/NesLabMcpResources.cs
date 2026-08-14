using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Sheep.Nes.Lab;

[McpServerResourceType]
public static class NesLabMcpResources
{
    [McpServerResource(Name = "nes-lab-architecture-timing", UriTemplate = "nes://architecture/timing",
        MimeType = "text/markdown")]
    [Description("Stable NES CPU, PPU, DMA, interrupt, and bus timing guidance.")]
    public static string Timing() =>
        "# NES timing\n\n" +
        "Use CPU-clock records as the alignment authority. Treat CPU, PPU, APU, DMA, NMI, and IRQ " +
        "state as a single ordered clock boundary. Require a compatible trace provenance before " +
        "calling a difference causal. Reproduce timing fixes with the smallest conformance case.\n";

    [McpServerResource(Name = "nes-lab-architecture-bus-map", UriTemplate = "nes://architecture/bus-map",
        MimeType = "text/markdown")]
    [Description("Stable NES CPU-visible bus ownership and side-effect guidance.")]
    public static string BusMap() =>
        "# NES bus map\n\n" +
        "CPU-visible I/O includes PPU registers at $2000-$2007, APU and controller registers " +
        "at $4000-$4017, and DMA arbitration around $4014 and DMC reads. Use trace bus accesses " +
        "and actor transitions to distinguish CPU reads from DMA-owned cycles.\n";

    [McpServerResource(Name = "nes-lab-roms", UriTemplate = "nes://roms/{suite}/{case}",
        MimeType = "application/json")]
    [Description("ROM catalog provenance and protocol lookup route.")]
    public static async Task<string> Rom(string suite, string @case,
        CancellationToken cancellationToken = default)
    {
        var root = Directory.GetCurrentDirectory();
        var catalog = RomCatalog.Load(Path.Combine(root, "test", "conformance", "test-roms.json"),
            Environment.GetEnvironmentVariable("NES_TEST_ROMS"));
        var entry = catalog.Find(suite, @case);
        return await Task.FromResult(JsonSerializer.Serialize(entry, LabResponseSerializer.Options));
    }

    [McpServerResource(
        Name = "nes-lab-immutable-artifact",
        Title = "Immutable NES Lab artifact",
        UriTemplate = "nes-lab://artifact/{kind}/sha256/{digest}",
        MimeType = "application/json")]
    [Description("Retrieves a content-addressed NES Lab artifact and verifies its SHA-256 digest.")]
    public static async Task<string> ReadAsync(string kind, string digest,
        CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".artifacts", "nes-lab");
        var resource = await new ImmutableArtifactStore(root).ReadAsync(
            kind, digest.ToLowerInvariant(), cancellationToken).ConfigureAwait(false);
        var projection = McpArtifactProjection.Create(resource);
        var json = JsonSerializer.Serialize(projection, LabResponseSerializer.Options);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > McpResponseLimits.MaximumResourceBytes)
            throw new InvalidDataException("Bounded artifact projection exceeded the MCP resource limit.");
        return json;
    }
}
