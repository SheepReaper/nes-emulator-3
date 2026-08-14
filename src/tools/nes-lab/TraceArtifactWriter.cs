using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed class TraceArtifactWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<string> WriteAsync(
        TraceArtifact artifact,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var absolutePath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(absolutePath)!;
        Directory.CreateDirectory(directory);

        await using var stream = new FileStream(
            absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read,
            bufferSize: 16 * 1024, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, artifact, SerializerOptions, cancellationToken);
        return absolutePath;
    }
}
