using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed class TraceArtifactReader
{
    public const long MaximumArtifactBytes = 16 * 1024 * 1024;
    public const int MaximumRecords = 4_096;
    public const int MaximumBusAccessesPerRecord = 64;

    public async Task<TraceArtifact> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (info.Length > MaximumArtifactBytes)
            throw new InvalidDataException($"Trace artifact '{path}' exceeds the {MaximumArtifactBytes}-byte limit.");
        await using var stream = info.OpenRead();
        var artifact = await JsonSerializer.DeserializeAsync<TraceArtifact>(
            stream, LabResponseSerializer.Options, cancellationToken).ConfigureAwait(false);
        if (artifact is null)
            throw new InvalidDataException($"Trace artifact '{path}' is empty or invalid.");
        if (artifact.SchemaVersion is < 1 or > TraceArtifact.CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Unsupported trace schema version {artifact.SchemaVersion} in '{path}'.");
        Validate(artifact, path);
        return artifact;
    }

    private static void Validate(TraceArtifact artifact, string path)
    {
        if (artifact.Records.Count > MaximumRecords)
            throw new InvalidDataException($"Trace artifact '{path}' contains too many records.");
        foreach (var record in artifact.Records)
            Validate(record, path);
        foreach (var window in artifact.Windows ?? [])
        {
            if (window.Records.Count > MaximumRecords)
                throw new InvalidDataException($"Trace window '{window.Name}' contains too many records.");
            foreach (var record in window.Records) Validate(record, path);
        }
    }

    private static void Validate(TraceClockRecord record, string path)
    {
        if (record.BusAccesses.Count > MaximumBusAccessesPerRecord)
            throw new InvalidDataException(
                $"Trace artifact '{path}' contains more than {MaximumBusAccessesPerRecord} bus accesses at CPU clock {record.CpuClock}.");
    }
}
