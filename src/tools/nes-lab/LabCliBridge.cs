using System.Diagnostics;
using System.Text;

namespace Sheep.Nes.Lab;

public sealed class LabCliBridge(string repositoryRoot)
{
    public async Task<System.Text.Json.JsonElement> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var assembly = typeof(LabCliBridge).Assembly.Location;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetFullPath(repositoryRoot),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(assembly);
        startInfo.Environment["NES_LAB_CURRENT_MCP_SESSION"] = "1";
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Unable to start the nes-lab CLI process.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        var payload = (await stdoutTask.ConfigureAwait(false)).Trim();
        var stderr = (await stderrTask.ConfigureAwait(false)).Trim();
        if (payload.Length == 0)
            throw new InvalidOperationException(
                $"nes-lab exited with code {process.ExitCode}: {stderr}");
        System.Text.Json.JsonDocument document;
        try { document = System.Text.Json.JsonDocument.Parse(payload); }
        catch (System.Text.Json.JsonException exception)
        {
            throw new InvalidOperationException("nes-lab returned invalid JSON.", exception);
        }
        using (document)
        {
            var root = document.RootElement;
            if (process.ExitCode != 0 && !IsExpectedTestFailure(root))
                throw new InvalidOperationException($"nes-lab failed with exit code {process.ExitCode}: {payload}");
            return root.Clone();
        }
    }

    private static bool IsExpectedTestFailure(System.Text.Json.JsonElement root)
    {
        if (!root.TryGetProperty("operation", out var operation) || operation.GetString() != "verify" ||
            !root.TryGetProperty("results", out var results) || results.ValueKind != System.Text.Json.JsonValueKind.Array)
            return false;
        var outcomes = results.EnumerateArray().Select(item =>
            item.TryGetProperty("outcome", out var outcome) ? outcome.GetString() : null).ToArray();
        return outcomes.Contains("testFailure") && !outcomes.Contains("infrastructureFailure");
    }
}
