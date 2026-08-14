using System.Text.Json;

namespace Sheep.Nes.Lab;

public interface IVerificationCache
{
    Task<VerificationResult?> TryGetAsync(
        VerificationCommand command,
        CancellationToken cancellationToken);

    Task StoreAsync(
        VerificationCommand command,
        VerificationResult result,
        CancellationToken cancellationToken);
}

public sealed class FileVerificationCache(string repositoryRoot, string cacheRoot) : IVerificationCache
{
    public async Task<VerificationResult?> TryGetAsync(
        VerificationCommand command,
        CancellationToken cancellationToken)
    {
        var details = await VerificationFingerprint.CreateDetailsAsync(
            command,
            repositoryRoot,
            cancellationToken).ConfigureAwait(false);
        if (!details.CacheEligible) return null;
        var entryPath = GetEntryPath(command.Scope, details.Fingerprint);
        if (!File.Exists(entryPath))
            return null;

        try
        {
            await using var stream = File.OpenRead(entryPath);
            var entry = await JsonSerializer.DeserializeAsync<CacheEntry>(
                stream,
                LabResponseSerializer.Options,
                cancellationToken).ConfigureAwait(false);
            if (entry?.Result is not { Success: true } result || !File.Exists(result.ArtifactPath))
                return null;

            return result with { Cached = true };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public async Task StoreAsync(
        VerificationCommand command,
        VerificationResult result,
        CancellationToken cancellationToken)
    {
        if (!result.Success || result.Cached || !File.Exists(result.ArtifactPath))
            return;

        var details = await VerificationFingerprint.CreateDetailsAsync(
            command,
            repositoryRoot,
            cancellationToken).ConfigureAwait(false);
        if (!details.CacheEligible) return;
        var entryPath = GetEntryPath(command.Scope, details.Fingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(entryPath)!);
        var cachedLogPath = Path.ChangeExtension(entryPath, ".log");
        File.Copy(result.ArtifactPath, cachedLogPath, overwrite: true);
        var cachedResult = result with { ArtifactPath = cachedLogPath };

        var temporaryPath = entryPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new CacheEntry(cachedResult, details.Inputs),
                    LabResponseSerializer.Options,
                    cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporaryPath, entryPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private string GetEntryPath(VerificationScope scope, string fingerprint) =>
        Path.Combine(cacheRoot, scope.ToString().ToLowerInvariant(), fingerprint + ".json");

    private sealed record CacheEntry(VerificationResult Result, IReadOnlyList<string>? Inputs = null);
}
