using System.Text;

namespace Sheep.Nes.Lab.Tests;

public sealed class McpArtifactProjectionTests
{
    [Fact]
    public void Create_SmallText_InlinesCompleteContent()
    {
        var resource = Resource("context", "small payload");

        var projection = McpArtifactProjection.Create(resource);

        Assert.Equal("available", projection.Status);
        Assert.Equal("small payload", projection.Content);
        Assert.Null(projection.Preview);
    }

    [Fact]
    public void Create_TenMegabyteTrace_DoesNotInlineContentAndStaysBounded()
    {
        var resource = Resource("trace", new string('x', 10 * 1024 * 1024));

        var projection = McpArtifactProjection.Create(resource);
        var json = System.Text.Json.JsonSerializer.Serialize(projection, LabResponseSerializer.Options);

        Assert.Equal("contentNotInlined", projection.Status);
        Assert.Null(projection.Content);
        Assert.NotNull(projection.Preview);
        Assert.True(Encoding.UTF8.GetByteCount(json) <= McpResponseLimits.MaximumResourceBytes);
        Assert.Contains(projection.FollowUpCommands,
            command => command.StartsWith("trace query", StringComparison.Ordinal));
    }

    private static ImmutableArtifactResource Resource(string kind, string text)
    {
        var bytes = Encoding.UTF8.GetByteCount(text);
        var digest = new string('a', 64);
        var metadata = new ImmutableArtifactMetadata(1, kind, digest, "application/json",
            bytes, DateTimeOffset.UtcNow, false);
        return new ImmutableArtifactResource(metadata, ImmutableArtifactStore.Uri(kind, digest), text, null);
    }
}
