using System.Text;

namespace Sheep.Nes.Lab;

public sealed record McpArtifactProjection(
    ImmutableArtifactMetadata Metadata,
    string Uri,
    string Status,
    string? Content,
    string? Preview,
    IReadOnlyList<string> FollowUpCommands)
{
    public static McpArtifactProjection Create(ImmutableArtifactResource resource)
    {
        if (!resource.Status.Equals("available", StringComparison.OrdinalIgnoreCase))
            return new(resource.Metadata, resource.Uri, resource.Status, null, null, []);

        var content = resource.Text ?? resource.Base64;
        if (content is null)
            return new(resource.Metadata, resource.Uri, "available", null, null, []);

        if (Encoding.UTF8.GetByteCount(content) <= McpResponseLimits.MaximumResourceBytes / 2)
            return new(resource.Metadata, resource.Uri, "available", content, null, []);

        var preview = Utf8Prefix(content, McpResponseLimits.MaximumPreviewBytes);
        var commands = resource.Metadata.Kind switch
        {
            "trace" => new[]
            {
                $"trace query --artifact-uri {resource.Uri} --instruction-boundaries --max 64",
                $"nes_lab_inspect trace.query with artifactUri={resource.Uri} and maximumResults=64"
            },
            "log" => new[] { $"nes_lab_inspect artifacts.text with uri={resource.Uri}, startLine=1, maximumLines=200" },
            _ => new[] { $"nes_lab_inspect artifacts.describe with uri={resource.Uri}" }
        };
        return new(resource.Metadata, resource.Uri, "contentNotInlined", null, preview, commands);
    }

    private static string Utf8Prefix(string value, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maximumBytes) return value;
        var length = Math.Min(value.Length, maximumBytes);
        while (length > 0 && Encoding.UTF8.GetByteCount(value.AsSpan(0, length)) > maximumBytes)
            length--;
        return value[..length] + "\n…[preview truncated]";
    }
}
