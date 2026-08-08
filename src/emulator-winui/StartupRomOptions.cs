namespace EmuSheep;

internal static class StartupRomOptions
{
    public static string? Parse(IReadOnlyList<string> commandLineArguments)
    {
        ArgumentNullException.ThrowIfNull(commandLineArguments);

        string? romPath = null;
        for (var index = 1; index < commandLineArguments.Count; index++)
        {
            if (!string.Equals(commandLineArguments[index], "--rom", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Unknown command-line argument: {commandLineArguments[index]}");
            if (++index >= commandLineArguments.Count || string.IsNullOrWhiteSpace(commandLineArguments[index]))
                throw new ArgumentException("The --rom option requires a ROM file path.");
            if (romPath != null)
                throw new ArgumentException("The --rom option can only be specified once.");

            romPath = commandLineArguments[index];
        }

        return romPath;
    }
}
