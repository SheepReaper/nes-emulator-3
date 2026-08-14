using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sheep.Nes.Lab;

public sealed record VerificationFingerprintDetails(
    string Fingerprint,
    IReadOnlyList<string> Inputs,
    bool CacheEligible);

public static class VerificationFingerprint
{
    private const string SchemaVersion = "verification-cache-v2";
    private static readonly Lazy<string> Toolchain = new(ReadToolchain);

    public static async Task<string> CreateAsync(
        VerificationCommand command,
        string repositoryRoot,
        CancellationToken cancellationToken)
        => (await CreateDetailsAsync(command, repositoryRoot, cancellationToken).ConfigureAwait(false)).Fingerprint;

    public static async Task<VerificationFingerprintDetails> CreateDetailsAsync(
        VerificationCommand command,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        List<string> inputs = [];
        Add(SchemaVersion);
        Add($"scope:{command.Scope}");
        Add($"command:{command.FileName}");
        foreach (var argument in command.Arguments)
            Add($"argument:{argument}");
        if (command.Environment is not null)
        {
            foreach (var variable in command.Environment.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                Add($"environment:{variable.Key}={variable.Value}");
            }
        }

        Add($"toolchain:{Toolchain.Value}");
        Add($"framework:{RuntimeInformation.FrameworkDescription}");
        Add($"os:{RuntimeInformation.OSDescription}");
        Add($"processArchitecture:{RuntimeInformation.ProcessArchitecture}");
        Add($"osArchitecture:{RuntimeInformation.OSArchitecture}");
        Add($"configuration:{Configuration(command)}");
        foreach (var file in EnumerateInputs(command.Scope, repositoryRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
            var digest = await HashFileAsync(file, cancellationToken).ConfigureAwait(false);
            Add($"file:{relativePath}:{digest}");
        }

        var cacheEligible = true;
        if (command.Scope == VerificationScope.Conformance)
            cacheEligible = await AppendConformanceInputsAsync(
                hash, inputs, command, repositoryRoot, cancellationToken).ConfigureAwait(false);

        return new VerificationFingerprintDetails(
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), inputs, cacheEligible);

        void Add(string value)
        {
            inputs.Add(value);
            AppendText(hash, value);
        }
    }

    private static async Task<bool> AppendConformanceInputsAsync(
        IncrementalHash hash,
        List<string> inputs,
        VerificationCommand command,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(repositoryRoot, "test", "conformance", "test-roms.json");
        if (!File.Exists(manifestPath)) return false;
        var assetRoot = command.Environment?.GetValueOrDefault("NES_TEST_ROMS") ??
            Environment.GetEnvironmentVariable("NES_TEST_ROMS") ??
            Path.Combine(repositoryRoot, "test-roms", "nes-test-roms");
        var catalog = RomCatalog.Load(manifestPath, assetRoot);
        var caseFilter = NamedCaseFilter(command);
        var selected = caseFilter is null ? catalog.Entries : catalog.Entries.Where(entry =>
            entry.Name.Contains(caseFilter, StringComparison.OrdinalIgnoreCase) ||
            $"{entry.Suite}/{entry.Name}".Contains(caseFilter, StringComparison.OrdinalIgnoreCase)).ToArray();
        var eligible = selected.Count > 0;
        foreach (var entry in selected.OrderBy(item => item.Suite).ThenBy(item => item.Name))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = entry.Availability switch
            {
                RomAvailability.InstalledVerified => $"verified:{entry.ActualSha256}",
                RomAvailability.ChecksumMismatch => $"checksumMismatch:{entry.ActualSha256}",
                _ => "missing"
            };
            var value = $"rom:{state}:{entry.Suite}/{entry.Name}:{entry.RelativePath}:{entry.ExpectedSha256}";
            inputs.Add(value);
            AppendText(hash, value);
            eligible &= entry.Availability == RomAvailability.InstalledVerified;
        }
        return eligible;
    }

    private static string? NamedCaseFilter(VerificationCommand command)
    {
        for (var index = 0; index + 1 < command.Arguments.Count; index++)
            if (command.Arguments[index] == "--filter-display-name")
                return command.Arguments[index + 1].Trim('*');
        return command.Environment?.GetValueOrDefault("NES_LAB_TRACE_CASE");
    }

    private static string Configuration(VerificationCommand command)
    {
        for (var index = 0; index + 1 < command.Arguments.Count; index++)
            if (command.Arguments[index] is "-c" or "--configuration") return command.Arguments[index + 1];
        return "Debug";
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static string ReadToolchain() => ReadProcess(Directory.GetCurrentDirectory(), "dotnet", "--info");

    private static string ReadProcess(string workingDirectory, string fileName, params string[] arguments)
    {
        try
        {
            using var process = new Process { StartInfo = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }};
            foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Trim() : "unavailable";
        }
        catch { return "unavailable"; }
    }

    private static IReadOnlyList<string> EnumerateInputs(
        VerificationScope scope,
        string repositoryRoot)
    {
        var relativeRoots = scope switch
        {
            VerificationScope.LabTests => new[]
            {
                "src/tools/nes-lab-contracts", "src/tools/nes-lab", "test/nes-lab"
            },
            VerificationScope.Cpu => new[] { "src/lib/emulation", "test/cpu" },
            VerificationScope.Conformance => new[]
            {
                "src/lib/emulation", "src/tools/nes-lab-contracts", "test/conformance"
            },
            VerificationScope.WinUiTests => new[]
            {
                "src/lib/emulation", "src/lib/interop-winui", "src/emulator-winui", "test/emulator-winui"
            },
            VerificationScope.Library => new[] { "src/lib/emulation" },
            VerificationScope.WinUiInterop => new[] { "src/lib/interop-winui" },
            VerificationScope.WinUiApp => new[]
            {
                "src/lib/emulation", "src/lib/interop-winui", "src/emulator-winui"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "A concrete scope is required.")
        };

        List<string> files = [];
        foreach (var relativeRoot in relativeRoots)
        {
            var root = Path.Combine(repositoryRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(root))
                files.Add(root);
            else if (Directory.Exists(root))
                files.AddRange(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(path => !IsGeneratedPath(path)));
        }

        foreach (var rootFile in new[] { "Directory.Packages.props", "global.json" })
        {
            var path = Path.Combine(repositoryRoot, rootFile);
            if (File.Exists(path))
                files.Add(path);
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => Path.GetRelativePath(repositoryRoot, path), StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsGeneratedPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/.artifacts/", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendText(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData([0]);
    }
}
