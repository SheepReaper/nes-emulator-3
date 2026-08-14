using System.Security.Cryptography;

namespace Sheep.Nes.Lab;

public sealed class TraceProvenance(ICommandExecutor executor, string repositoryRoot)
{
    public static async Task<string> ComputeRomSha256Async(
        string romPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(romPath);
        await using var stream = new FileStream(
            romPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, useAsync: true);
        var digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(digest);
    }

    public async Task<string> GetSourceCommitAsync(CancellationToken cancellationToken = default)
    {
        var command = new VerificationCommand(
            VerificationScope.LabTests, "git", ["rev-parse", "HEAD"]);
        var result = await executor.ExecuteAsync(command, repositoryRoot, cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var diagnostic = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput.Trim()
                : result.StandardError.Trim();
            throw new InvalidOperationException(
                $"Git source-commit discovery failed with exit code {result.ExitCode}: {diagnostic}");
        }

        return result.StandardOutput.Trim();
    }
}
