using System.Text;

namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>
/// Reads text output from PPU memory (nametables) for tests with console screens.
/// </summary>
internal static class NesTextConsoleReader
{
    internal static string Read(INesTestMachine machine)
    {
        var output = new StringBuilder();
        for (ushort address = 0x2000; address < 0x3000; address++)
        {
            var value = machine.PeekPpuMemory(address);
            output.Append(value is >= 0x20 and <= 0x7E ? (char)value : ' ');
            if ((address & 0x1F) == 0x1F)
            {
                output.AppendLine();
            }
        }
        return output.ToString().Trim();
    }

    internal static bool? ReadResult(INesTestMachine machine, string[]? markers)
    {
        if (markers != null)
        {
            foreach (var marker in markers)
            {
                if (ContainsText(machine, marker))
                {
                    return true;
                }
            }
        }

        return ContainsText(machine, "Passed") || ContainsText(machine, "PASSED")
            ? true
            : ContainsText(machine, "Failed") || ContainsText(machine, "FAILED") ? false : null;
    }

    private static bool ContainsText(INesTestMachine machine, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var matched = 0;
        for (ushort address = 0x2000; address < 0x3000; address++)
        {
            var value = machine.PeekPpuMemory(address);
            matched = value == text[matched] ? matched + 1 : value == text[0] ? 1 : 0;
            if (matched == text.Length)
            {
                return true;
            }
        }
        return false;
    }
}
