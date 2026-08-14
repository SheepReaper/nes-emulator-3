using Sheep.Nes.Lab;

namespace Sheep.Nes.Lab.Tests;

public sealed class VerificationFingerprintTests
{
    [Fact]
    public async Task CreateAsync_SourceContentChange_ChangesFingerprint()
    {
        var root = CreateTemporaryRepository();
        try
        {
            var source = Path.Combine(root, "src", "tools", "nes-lab", "Program.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            await File.WriteAllTextAsync(source, "version one", TestContext.Current.CancellationToken);
            var command = new VerificationCommand(VerificationScope.LabTests, "dotnet", ["test"]);

            var first = await VerificationFingerprint.CreateAsync(
                command,
                root,
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(source, "version two", TestContext.Current.CancellationToken);
            var second = await VerificationFingerprint.CreateAsync(
                command,
                root,
                TestContext.Current.CancellationToken);

            Assert.NotEqual(first, second);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsync_CommandChange_ChangesFingerprint()
    {
        var root = CreateTemporaryRepository();
        try
        {
            var first = await VerificationFingerprint.CreateAsync(
                new VerificationCommand(VerificationScope.LabTests, "dotnet", ["test"]),
                root,
                TestContext.Current.CancellationToken);
            var second = await VerificationFingerprint.CreateAsync(
                new VerificationCommand(VerificationScope.LabTests, "dotnet", ["test", "--restore"]),
                root,
                TestContext.Current.CancellationToken);

            Assert.NotEqual(first, second);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsync_EnvironmentChange_ChangesFingerprint()
    {
        var root = CreateTemporaryRepository();
        try
        {
            var first = await VerificationFingerprint.CreateAsync(
                new VerificationCommand(VerificationScope.Conformance, "dotnet", ["test"],
                    new Dictionary<string, string> { ["TRACE"] = "one" }),
                root, TestContext.Current.CancellationToken);
            var second = await VerificationFingerprint.CreateAsync(
                new VerificationCommand(VerificationScope.Conformance, "dotnet", ["test"],
                    new Dictionary<string, string> { ["TRACE"] = "two" }),
                root, TestContext.Current.CancellationToken);

            Assert.NotEqual(first, second);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsync_InstalledRomContentChange_ChangesConformanceFingerprint()
    {
        var root = CreateTemporaryRepository();
        try
        {
            var assets = Path.Combine(root, "roms");
            Directory.CreateDirectory(assets);
            var rom = Path.Combine(assets, "case.nes");
            await File.WriteAllBytesAsync(rom, [1], TestContext.Current.CancellationToken);
            WriteManifest(root, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData([1])));
            var command = ConformanceCommand(assets);

            var first = await VerificationFingerprint.CreateAsync(command, root,
                TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(rom, [2], TestContext.Current.CancellationToken);
            var second = await VerificationFingerprint.CreateAsync(command, root,
                TestContext.Current.CancellationToken);

            Assert.NotEqual(first, second);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task CreateDetailsAsync_MissingSelectedRom_IsNotCacheEligible()
    {
        var root = CreateTemporaryRepository();
        try
        {
            var assets = Path.Combine(root, "roms");
            Directory.CreateDirectory(assets);
            WriteManifest(root, new string('0', 64));

            var details = await VerificationFingerprint.CreateDetailsAsync(
                ConformanceCommand(assets), root, TestContext.Current.CancellationToken);

            Assert.False(details.CacheEligible);
            Assert.Contains(details.Inputs, item => item.Contains("rom:missing", StringComparison.Ordinal));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static VerificationCommand ConformanceCommand(string assets) => new(
        VerificationScope.Conformance, "dotnet", ["test"],
        new Dictionary<string, string> { ["NES_TEST_ROMS"] = assets });

    private static void WriteManifest(string root, string sha256)
    {
        var directory = Path.Combine(root, "test", "conformance");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "test-roms.json"), $$"""
            {"upstreamCommit":"commit","defaultProtocols":[{"kind":"Blargg6000"}],"tests":[
              {"suite":"suite","name":"case","path":"case.nes","sha256":"{{sha256}}","maximumPpuDots":10}
            ]}
            """);
    }

    private static string CreateTemporaryRepository()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-fingerprint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
