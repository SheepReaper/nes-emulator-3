namespace Sheep.Nes.Lab.Tests;

public sealed class LabCapabilityCatalogTests
{
    [Fact]
    public void List_ReturnsCompactStableGroupsWithoutDetailedSchemas()
    {
        var groups = LabCapabilityCatalog.List();

        Assert.Equal(["code", "context", "memory", "feedback", "references", "experiment", "media", "investigate", "session", "host", "build", "rom", "trace", "verify", "history", "diagnose", "artifacts"],
            groups.Select(group => group.Name));
        Assert.All(groups, group => Assert.False(string.IsNullOrWhiteSpace(group.Summary)));
        Assert.DoesNotContain("parameters", System.Text.Json.JsonSerializer.Serialize(groups),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_ReturnsOnlyRequestedCapabilityWithOperationsAndProvenancePolicy()
    {
        var capability = LabCapabilityCatalog.Describe("trace");

        Assert.Equal("trace", capability.Name);
        Assert.Contains(capability.Operations, operation => operation.Name == "diff");
        Assert.All(capability.Operations, operation => Assert.NotEmpty(operation.Parameters));
        Assert.Contains("artifact", capability.Provenance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_RejectsUnknownCapability()
    {
        Assert.Throws<KeyNotFoundException>(() => LabCapabilityCatalog.Describe("unknown"));
    }
}
