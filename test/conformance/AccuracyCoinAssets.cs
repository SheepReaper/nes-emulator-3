namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>
/// Finds the root directory for AccuracyCoin test assets.
/// </summary>
internal static class AccuracyCoinAssets
{
    internal static string? FindRoot()
    {
        var configured = Environment.GetEnvironmentVariable("NES_ACCURACY_COIN");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "test-roms", "accuracy-coin");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }
}
