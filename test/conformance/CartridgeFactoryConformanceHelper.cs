namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>
/// Verifies that installed ROM files can be parsed and accepted by CartridgeFactory.
/// </summary>
internal static class CartridgeFactoryConformanceHelper
{
    internal static void VerifyInstalledRoms(string root)
    {
        var failures = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*.nes", SearchOption.AllDirectories))
        {
            var rom = File.ReadAllBytes(path);
            if (rom.Length < 16 || rom[0] != 'N' || rom[1] != 'E' || rom[2] != 'S' || rom[3] != 0x1A)
            {
                continue;
            }
            try
            {
                _ = new CartridgeFactory().Create(rom);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                failures.Add($"{Path.GetRelativePath(root, path)}: {exception.Message}");
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
        }
    }
}
