using System.Xml.Linq;

namespace Sheep.Nes.Lab;

public sealed record StructuredTestReport(
    VerificationSummary Summary,
    IReadOnlyList<VerificationIssue> Failures);

public static class StructuredTestReportParser
{
    public static StructuredTestReport? TryParse(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var document = XDocument.Load(path, LoadOptions.None);
            var counters = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Counters");
            var summary = counters is null
                ? new VerificationSummary(null, null, null, null)
                : new VerificationSummary(Int(counters, "total"), Int(counters, "failed"),
                    Int(counters, "passed"), Int(counters, "notExecuted"));
            var definitions = document.Descendants()
                .Where(element => element.Name.LocalName == "UnitTest")
                .ToDictionary(element => (string?)element.Attribute("id") ?? "",
                    element => (string?)element.Attribute("name"), StringComparer.OrdinalIgnoreCase);
            var failures = document.Descendants()
                .Where(element => element.Name.LocalName == "UnitTestResult" &&
                    !string.Equals((string?)element.Attribute("outcome"), "Passed", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals((string?)element.Attribute("outcome"), "NotExecuted", StringComparison.OrdinalIgnoreCase))
                .Select(element =>
                {
                    var id = (string?)element.Attribute("testId") ?? "";
                    var name = (string?)element.Attribute("testName") ?? definitions.GetValueOrDefault(id);
                    var error = element.Descendants().FirstOrDefault(item => item.Name.LocalName == "ErrorInfo");
                    var message = error?.Elements().FirstOrDefault(item => item.Name.LocalName == "Message")?.Value;
                    var stack = error?.Elements().FirstOrDefault(item => item.Name.LocalName == "StackTrace")?.Value;
                    var diagnostic = string.Join('\n', new[] { message, stack }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                    return new VerificationIssue(name, diagnostic.Length == 0 ? "Structured test failure." : diagnostic);
                }).ToArray();
            return new StructuredTestReport(summary, failures);
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Xml.XmlException)
        {
            return null;
        }
    }

    private static int? Int(XElement element, string name) =>
        int.TryParse((string?)element.Attribute(name), out var value) ? value : null;
}
