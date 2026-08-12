using System.Text.RegularExpressions;

namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>
/// Parses test names and symbol assignments from AccuracyCoin assembly sources.
/// </summary>
internal static partial class AccuracyCoinCatalog
{
    internal static IReadOnlyDictionary<ushort, string> Load(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            return new Dictionary<ushort, string>();
        }

        var symbols = new Dictionary<string, ushort>(StringComparer.Ordinal);
        var entries = new List<(string Name, string Symbol)>();
        foreach (var line in File.ReadLines(sourcePath))
        {
            var assignment = ResultAssignment().Match(line);
            if (assignment.Success)
            {
                symbols[assignment.Groups[1].Value] = Convert.ToUInt16(assignment.Groups[2].Value, 16);
            }

            var entry = TestTableEntry().Match(line);
            if (entry.Success)
            {
                entries.Add((entry.Groups[1].Value, entry.Groups[2].Value));
            }
        }

        return entries.Where(entry => symbols.ContainsKey(entry.Symbol))
            .GroupBy(entry => symbols[entry.Symbol])
            .ToDictionary(group => group.Key, group => group.First().Name);
    }

    internal static (ushort ResultAddress, ushort RoutineAddress) GetTestEntry(INesTestMachine machine, int suiteIndex, int testIndex)
    {
        var suitePtrAddr = (ushort)(0x8100 + suiteIndex * 2);
        var suiteAddr = (ushort)(machine.PeekCpuMemory(suitePtrAddr) | (machine.PeekCpuMemory((ushort)(suitePtrAddr + 1)) << 8));
        var cursor = suiteAddr;
        while (machine.PeekCpuMemory(cursor++) != 0xFF) { }

        for (var i = 0; i <= testIndex; i++)
        {
            while (machine.PeekCpuMemory(cursor++) != 0xFF) { }
            var resultAddr = (ushort)(machine.PeekCpuMemory(cursor) | (machine.PeekCpuMemory((ushort)(cursor + 1)) << 8));
            cursor += 2;
            var routineAddr = (ushort)(machine.PeekCpuMemory(cursor) | (machine.PeekCpuMemory((ushort)(cursor + 1)) << 8));
            cursor += 2;
            if (i == testIndex)
            {
                return (resultAddr, routineAddr);
            }
        }
        throw new InvalidOperationException($"Could not locate test {testIndex} in suite {suiteIndex}");
    }

    [GeneratedRegex(@"^\s*(result_[A-Za-z0-9_]+)\s*=\s*\$([0-9A-Fa-f]+)")]
    private static partial Regex ResultAssignment();

    [GeneratedRegex("^\\s*table\\s+\"([^\"]+)\".*?,\\s*(result_[A-Za-z0-9_]+)\\s*,")]
    private static partial Regex TestTableEntry();
}
