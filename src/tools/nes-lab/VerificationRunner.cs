using System.Text;

namespace Sheep.Nes.Lab;

public enum VerificationOutcome
{
    Passed,
    TestFailure,
    InfrastructureFailure,
    Cancelled
}

public sealed record VerificationResult(
    VerificationScope Scope,
    bool Success,
    VerificationOutcome Outcome,
    int ExitCode,
    long DurationMilliseconds,
    VerificationSummary Summary,
    IReadOnlyList<VerificationIssue> Failures,
    string ArtifactPath,
    int RawOutputBytes,
    bool Cached = false,
    string? TraceArtifactPath = null,
    BuildDiagnosticReport? BuildDiagnostics = null);

public sealed record VerificationBatchResult(
    bool Success,
    IReadOnlyList<VerificationResult> Results,
    int RawOutputBytes,
    int CacheHits = 0);

public enum VerificationProgressPhase { Started, Heartbeat, CacheHit, Completed, Failed, Cancelled }
public sealed record VerificationProgress(
    VerificationScope Scope, VerificationProgressPhase Phase, long ElapsedMilliseconds);

public sealed class VerificationRunner(
    ICommandExecutor executor,
    string artifactRoot,
    IVerificationCache? cache = null,
    IVerificationRunSink? runSink = null,
    Action<VerificationProgress>? progress = null,
    TimeSpan? progressInterval = null)
{
    private readonly TimeSpan heartbeatInterval = progressInterval ?? TimeSpan.FromSeconds(10);

    public async Task<VerificationBatchResult> RunAsync(
        IReadOnlyList<VerificationCommand> commands,
        CancellationToken cancellationToken,
        bool continueOnFailure = false)
    {
        var runDirectory = Path.GetFullPath(Path.Combine(
            artifactRoot,
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"),
            Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(runDirectory);

        List<VerificationResult> results = [];
        foreach (var command in commands)
        {
            var commandStarted = System.Diagnostics.Stopwatch.StartNew();
            progress?.Invoke(new(command.Scope, VerificationProgressPhase.Started, 0));
            var reportPath = Path.Combine(runDirectory, $"{ToFileName(command.Scope)}.trx");
            var executionCommand = WithStructuredReport(command, runDirectory, reportPath);
            if (cache is not null)
            {
                var cachedResult = await cache.TryGetAsync(command, cancellationToken).ConfigureAwait(false);
                if (cachedResult is not null)
                {
                    progress?.Invoke(new(command.Scope, VerificationProgressPhase.CacheHit,
                        commandStarted.ElapsedMilliseconds));
                    results.Add(cachedResult);
                    if (runSink is not null)
                        await runSink.RecordAsync(command, cachedResult, cancellationToken).ConfigureAwait(false);
                    continue;
                }
            }

            CommandExecution execution;
            using var heartbeatStop = new CancellationTokenSource();
            var heartbeat = ReportHeartbeatAsync(command.Scope, commandStarted, heartbeatStop.Token);
            try
            {
                execution = await executor.ExecuteAsync(
                    executionCommand,
                    Directory.GetCurrentDirectory(),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                progress?.Invoke(new(command.Scope, VerificationProgressPhase.Cancelled,
                    commandStarted.ElapsedMilliseconds));
                var cancelledPath = Path.Combine(runDirectory, $"{ToFileName(command.Scope)}.log");
                await File.WriteAllTextAsync(cancelledPath, "Verification cancelled.", Encoding.UTF8,
                    CancellationToken.None).ConfigureAwait(false);
                if (runSink is not null)
                    await runSink.RecordAsync(command, new VerificationResult(
                        command.Scope, false, VerificationOutcome.Cancelled, -2, 0,
                        new VerificationSummary(null, null, null, null),
                        [new VerificationIssue(null, "Verification cancelled.")], cancelledPath,
                        Encoding.UTF8.GetByteCount("Verification cancelled.")), CancellationToken.None)
                        .ConfigureAwait(false);
                throw;
            }
            catch (Exception exception)
            {
                progress?.Invoke(new(command.Scope, VerificationProgressPhase.Failed,
                    commandStarted.ElapsedMilliseconds));
                var exceptionArtifactPath = Path.Combine(runDirectory, $"{ToFileName(command.Scope)}.log");
                var exceptionOutput = CreateExceptionOutput(command, exception);
                await File.WriteAllTextAsync(exceptionArtifactPath, exceptionOutput, Encoding.UTF8, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(new VerificationResult(
                    command.Scope,
                    false,
                    VerificationOutcome.InfrastructureFailure,
                    -1,
                    0,
                    new VerificationSummary(null, null, null, null),
                    [new VerificationIssue(null, exception.Message)],
                    exceptionArtifactPath,
                    Encoding.UTF8.GetByteCount(exceptionOutput)));
                if (runSink is not null)
                    await runSink.RecordAsync(command, results[^1], cancellationToken).ConfigureAwait(false);
                if (!continueOnFailure)
                    break;
                continue;
            }
            finally
            {
                heartbeatStop.Cancel();
                try { await heartbeat.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            progress?.Invoke(new(command.Scope, VerificationProgressPhase.Completed,
                commandStarted.ElapsedMilliseconds));

            var completeOutput = CreateCompleteOutput(command, execution);
            var artifactPath = Path.Combine(runDirectory, $"{ToFileName(command.Scope)}.log");
            await File.WriteAllTextAsync(artifactPath, completeOutput, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);

            var structured = StructuredTestReportParser.TryParse(reportPath);
            var failures = structured?.Failures ?? VerificationOutputParser.ParseFailures(
                execution.StandardOutput + "\n" + execution.StandardError);
            var outcome = execution.ExitCode == 0
                ? VerificationOutcome.Passed
                : failures.Count > 0
                    ? VerificationOutcome.TestFailure
                    : VerificationOutcome.InfrastructureFailure;
            var result = new VerificationResult(
                command.Scope,
                execution.ExitCode == 0,
                outcome,
                execution.ExitCode,
                (long)execution.Duration.TotalMilliseconds,
                structured?.Summary ?? VerificationOutputParser.Parse(execution.StandardOutput + "\n" + execution.StandardError),
                failures.Count > 0
                    ? failures
                    : outcome == VerificationOutcome.InfrastructureFailure
                        ? [new VerificationIssue(null, FirstDiagnostic(execution))]
                        : [],
                artifactPath,
                Encoding.UTF8.GetByteCount(completeOutput),
                TraceArtifactPath: GetTraceArtifactPath(command),
                BuildDiagnostics: command.Arguments.FirstOrDefault() == "build"
                    ? BuildDiagnosticParser.Parse(execution.StandardOutput + "\n" + execution.StandardError) : null);
            results.Add(result);
            if (runSink is not null)
                await runSink.RecordAsync(command, result, cancellationToken).ConfigureAwait(false);
            if (result.Success && cache is not null)
                await cache.StoreAsync(command, result, cancellationToken).ConfigureAwait(false);
            if (!result.Success && !continueOnFailure)
                break;
        }

        return new VerificationBatchResult(
            results.All(result => result.Success) && results.Count == commands.Count,
            results,
            results.Sum(result => result.RawOutputBytes),
            results.Count(result => result.Cached));
    }

    private async Task ReportHeartbeatAsync(VerificationScope scope,
        System.Diagnostics.Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        if (progress is null) return;
        while (true)
        {
            await Task.Delay(heartbeatInterval, cancellationToken).ConfigureAwait(false);
            progress(new(scope, VerificationProgressPhase.Heartbeat, stopwatch.ElapsedMilliseconds));
        }
    }

    public async Task<VerificationBatchResult> RunWithEscalationAsync(
        IReadOnlyList<VerificationCommand> focusedCommands,
        IReadOnlyList<VerificationCommand> expandedCommands,
        CancellationToken cancellationToken,
        bool continueOnFailure = false)
    {
        var focused = await RunAsync(focusedCommands, cancellationToken, continueOnFailure)
            .ConfigureAwait(false);
        if (!focused.Success || expandedCommands.Count == 0)
            return focused;

        var expanded = await RunAsync(expandedCommands, cancellationToken, continueOnFailure)
            .ConfigureAwait(false);
        return new VerificationBatchResult(
            expanded.Success,
            focused.Results.Concat(expanded.Results).ToArray(),
            focused.RawOutputBytes + expanded.RawOutputBytes,
            focused.CacheHits + expanded.CacheHits);
    }

    private static string CreateCompleteOutput(VerificationCommand command, CommandExecution execution) =>
        $"COMMAND: {command.FileName} {string.Join(' ', command.Arguments)}{Environment.NewLine}" +
        $"EXIT CODE: {execution.ExitCode}{Environment.NewLine}" +
        $"DURATION: {execution.Duration}{Environment.NewLine}" +
        $"{Environment.NewLine}STDOUT{Environment.NewLine}{execution.StandardOutput}" +
        $"{Environment.NewLine}STDERR{Environment.NewLine}{execution.StandardError}";

    private static string CreateExceptionOutput(VerificationCommand command, Exception exception) =>
        $"COMMAND: {command.FileName} {string.Join(' ', command.Arguments)}{Environment.NewLine}" +
        $"INFRASTRUCTURE EXCEPTION{Environment.NewLine}{exception}";

    private static string FirstDiagnostic(CommandExecution execution) =>
        (execution.StandardError + "\n" + execution.StandardOutput)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0) ?? $"Command exited with code {execution.ExitCode}.";

    private static string ToFileName(VerificationScope scope) =>
        scope.ToString().ToLowerInvariant();

    private static string? GetTraceArtifactPath(VerificationCommand command)
    {
        if (command.Environment is null ||
            !command.Environment.TryGetValue("NES_LAB_TRACE_PATH", out var path))
            return null;
        var fullPath = Path.GetFullPath(path);
        return File.Exists(fullPath) ? fullPath : null;
    }

    private static VerificationCommand WithStructuredReport(
        VerificationCommand command, string runDirectory, string reportPath)
    {
        if (!command.FileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase) ||
            command.Arguments.Count == 0 || command.Arguments[0] != "test") return command;
        var arguments = command.Arguments.ToList();
        arguments.Add("--results-directory");
        arguments.Add(runDirectory);
        arguments.Add("--report-xunit-trx");
        arguments.Add("--report-xunit-trx-filename");
        arguments.Add(Path.GetFileName(reportPath));
        return command with { Arguments = arguments };
    }
}
