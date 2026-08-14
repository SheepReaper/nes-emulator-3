using System.Globalization;

namespace Sheep.Nes.Lab;

public sealed record VerificationSummary(int? Total, int? Failed, int? Succeeded, int? Skipped);
public sealed record VerificationIssue(string? Name, string Diagnostic);

public static class VerificationOutputParser
{
    public static VerificationSummary Parse(string output)
    {
        int? total = null;
        int? failed = null;
        int? succeeded = null;
        int? skipped = null;

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            total ??= ParseCount(trimmed, "total:");
            failed ??= ParseCount(trimmed, "failed:");
            succeeded ??= ParseCount(trimmed, "succeeded:");
            skipped ??= ParseCount(trimmed, "skipped:");
        }

        return new VerificationSummary(total, failed, succeeded, skipped);
    }

    public static IReadOnlyList<VerificationIssue> ParseFailures(string output)
    {
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        List<VerificationIssue> failures = [];
        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("failed ", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = RemoveDuration(trimmed[7..]);
            var diagnostic = "Test failed.";
            for (var detailIndex = index + 1; detailIndex < lines.Length; detailIndex++)
            {
                var detail = lines[detailIndex].Trim();
                if (detail.StartsWith("failed ", StringComparison.OrdinalIgnoreCase) ||
                    detail.StartsWith("Test run summary:", StringComparison.OrdinalIgnoreCase))
                    break;
                if (detail.StartsWith("from ", StringComparison.OrdinalIgnoreCase) ||
                    detail.StartsWith("at ", StringComparison.OrdinalIgnoreCase))
                    continue;

                diagnostic = detail;
                break;
            }

            failures.Add(new VerificationIssue(name, diagnostic));
        }

        return failures;
    }

    private static int? ParseCount(string line, string prefix)
    {
        if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return int.TryParse(line.AsSpan(prefix.Length).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string RemoveDuration(string value)
    {
        var durationStart = value.LastIndexOf(" (", StringComparison.Ordinal);
        if (durationStart < 0 || !value.EndsWith(')'))
            return value;

        var suffix = value[(durationStart + 2)..^1];
        return suffix.EndsWith("ms", StringComparison.OrdinalIgnoreCase) ||
               suffix.EndsWith('s')
            ? value[..durationStart]
            : value;
    }
}
