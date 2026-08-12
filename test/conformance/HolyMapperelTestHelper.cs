using System.Text;

namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>
/// Screen decoding and ROM location helpers for Holy Mapperel tests.
/// </summary>
internal static class HolyMapperelTestHelper
{
    internal static string ReadSmallFontScreen(NesSystem nes)
    {
        var output = new StringBuilder();
        for (ushort address = 0x2000; address < 0x3000; address++)
        {
            var tile = nes.Debugger.PeekPpuMemory(address);
            output.Append(tile switch
            {
                >= 1 and <= 26 => (char)(tile + 0x40),
                >= 0x20 and <= 0x3F => (char)tile,
                _ => ' '
            });
            if ((address & 0x1F) == 0x1F)
            {
                output.AppendLine();
            }
        }
        return output.ToString();
    }

    internal static string? FindHolyMapperelRoot()
    {
        var configured = Environment.GetEnvironmentVariable("NES_HOLY_MAPPEREL_ROMS");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "test-roms", "holy-mapperel-v0.02", "testroms");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }
}
