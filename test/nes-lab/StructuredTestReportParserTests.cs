namespace Sheep.Nes.Lab.Tests;

public sealed class StructuredTestReportParserTests
{
    [Fact]
    public void TryParse_RetainsCompleteExpectedAndActualDiagnostic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"report-{Guid.NewGuid():N}.trx");
        try
        {
            File.WriteAllText(path, """
                <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                  <ResultSummary><Counters total="2" passed="1" failed="1" notExecuted="0" /></ResultSummary>
                  <Results><UnitTestResult testId="1" testName="Timing fails" outcome="Failed">
                    <Output><ErrorInfo><Message>Expected: 3\nActual: 4</Message><StackTrace>at CpuTests</StackTrace></ErrorInfo></Output>
                  </UnitTestResult></Results>
                </TestRun>
                """);

            var report = Assert.IsType<StructuredTestReport>(StructuredTestReportParser.TryParse(path));

            Assert.Equal(2, report.Summary.Total);
            Assert.Equal(1, report.Summary.Failed);
            Assert.Equal(1, report.Summary.Succeeded);
            var failure = Assert.Single(report.Failures);
            Assert.Contains("Expected: 3", failure.Diagnostic);
            Assert.Contains("Actual: 4", failure.Diagnostic);
            Assert.Contains("at CpuTests", failure.Diagnostic);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TryParse_ReturnsNullForTruncatedReport()
    {
        var path = Path.Combine(Path.GetTempPath(), $"report-{Guid.NewGuid():N}.trx");
        try
        {
            File.WriteAllText(path, "<TestRun><Results>");
            Assert.Null(StructuredTestReportParser.TryParse(path));
        }
        finally { File.Delete(path); }
    }
}
