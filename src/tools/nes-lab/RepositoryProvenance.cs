using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Sheep.Nes.Lab;

public sealed record RepositoryProvenance(
    string Head,
    bool IsDirty,
    string WorkingTreeDigest,
    string LabVersion,
    int ContractSchemaVersion)
{
    public static RepositoryProvenance Capture(string repositoryRoot)
    {
        var head = Read(repositoryRoot, "rev-parse", "HEAD");
        var status = Read(repositoryRoot, "status", "--porcelain=v1", "--untracked-files=all");
        var diff = Read(repositoryRoot, "diff", "--no-ext-diff", "--no-textconv", "--unified=0", "HEAD", "--");
        var digest = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(status + "\0" + diff)));
        var version = typeof(RepositoryProvenance).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(RepositoryProvenance).Assembly.GetName().Version?.ToString()
            ?? "unknown";
        return new RepositoryProvenance(head, status.Length != 0, digest, version,
            TraceArtifact.CurrentSchemaVersion);
    }

    private static string Read(string root, params string[] arguments)
    {
        try
        {
            using var process = new Process { StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }};
            foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(outputTask, errorTask);
            return process.ExitCode == 0 ? outputTask.Result.Trim() : "unavailable";
        }
        catch { return "unavailable"; }
    }
}
