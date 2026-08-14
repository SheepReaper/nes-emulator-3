using Sheep.Nes.Lab;

namespace Sheep.Nes.Lab.Tests;

public sealed class VerificationRunnerTests
{
    [Fact]
    public async Task RunAsync_WritesFullOutputAndReturnsCompactResult()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"nes-lab-{Guid.NewGuid():N}");
        try
        {
            var executor = new StubExecutor(new CommandExecution(
                0,
                "Test run summary: Passed!\n  total: 5\n  failed: 0\n  succeeded: 5\n  skipped: 0\n",
                "",
                TimeSpan.FromMilliseconds(12)));
            var command = new VerificationCommand(VerificationScope.Cpu, "dotnet", ["test", "cpu.csproj"]);
            var runner = new VerificationRunner(executor, artifactRoot);

            var result = await runner.RunAsync([command], cancellationToken);

            var item = Assert.Single(result.Results);
            Assert.True(result.Success);
            Assert.Equal(5, item.Summary.Total);
            Assert.True(item.RawOutputBytes > 0);
            Assert.True(File.Exists(item.ArtifactPath));
            Assert.Contains("Test run summary", await File.ReadAllTextAsync(item.ArtifactPath, cancellationToken));
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
                Directory.Delete(artifactRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_FailedTest_IsClassifiedSeparatelyFromInfrastructure()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"nes-lab-{Guid.NewGuid():N}");
        try
        {
            var executor = new StubExecutor(new CommandExecution(
                2,
                "failed Example.Tests.One (1ms)\n  Expected true but was false\n  total: 1\n  failed: 1\n",
                "",
                TimeSpan.FromMilliseconds(5)));
            var runner = new VerificationRunner(executor, artifactRoot);

            var result = await runner.RunAsync(
                [new VerificationCommand(VerificationScope.Cpu, "dotnet", ["test"])],
                TestContext.Current.CancellationToken);

            var item = Assert.Single(result.Results);
            Assert.Equal(VerificationOutcome.TestFailure, item.Outcome);
            Assert.Equal("Example.Tests.One", Assert.Single(item.Failures).Name);
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
                Directory.Delete(artifactRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ExecutorException_ReturnsInfrastructureFailureAndArtifact()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"nes-lab-{Guid.NewGuid():N}");
        try
        {
            var runner = new VerificationRunner(
                new ThrowingExecutor(new InvalidOperationException("dotnet was unavailable")),
                artifactRoot);

            var result = await runner.RunAsync(
                [new VerificationCommand(VerificationScope.Library, "dotnet", ["build"])],
                TestContext.Current.CancellationToken);

            var item = Assert.Single(result.Results);
            Assert.Equal(VerificationOutcome.InfrastructureFailure, item.Outcome);
            Assert.Equal("dotnet was unavailable", Assert.Single(item.Failures).Diagnostic);
            Assert.True(File.Exists(item.ArtifactPath));
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
                Directory.Delete(artifactRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ContinueOnFailure_RunsRemainingScopes()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"nes-lab-{Guid.NewGuid():N}");
        try
        {
            var executor = new SequenceExecutor(
                new CommandExecution(2, "failed Example.One\n  failure", "", TimeSpan.Zero),
                new CommandExecution(0, "Build succeeded.", "", TimeSpan.Zero));
            var runner = new VerificationRunner(executor, artifactRoot);

            var result = await runner.RunAsync(
                [
                    new VerificationCommand(VerificationScope.Cpu, "dotnet", ["test"]),
                    new VerificationCommand(VerificationScope.Library, "dotnet", ["build"])
                ],
                TestContext.Current.CancellationToken,
                continueOnFailure: true);

            Assert.Equal(2, result.Results.Count);
            Assert.False(result.Success);
            Assert.Equal(VerificationOutcome.Passed, result.Results[1].Outcome);
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
                Directory.Delete(artifactRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunWithEscalation_RunsExpandedCommandsAfterFocusedSuccess()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"nes-lab-{Guid.NewGuid():N}");
        try
        {
            var executor = new SequenceExecutor(
                new CommandExecution(0, "passed", "", TimeSpan.Zero),
                new CommandExecution(0, "passed", "", TimeSpan.Zero));
            var runner = new VerificationRunner(executor, artifactRoot);

            var result = await runner.RunWithEscalationAsync(
                [new VerificationCommand(VerificationScope.Cpu, "dotnet", ["test"])],
                [new VerificationCommand(VerificationScope.Conformance, "dotnet", ["test"])],
                TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal(2, result.Results.Count);
        }
        finally { if (Directory.Exists(artifactRoot)) Directory.Delete(artifactRoot, true); }
    }

    [Fact]
    public async Task RunWithEscalation_DoesNotRunExpandedCommandsAfterFocusedFailure()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"nes-lab-{Guid.NewGuid():N}");
        try
        {
            var executor = new SequenceExecutor(new CommandExecution(2, "failed Example.One", "", TimeSpan.Zero));
            var runner = new VerificationRunner(executor, artifactRoot);

            var result = await runner.RunWithEscalationAsync(
                [new VerificationCommand(VerificationScope.Cpu, "dotnet", ["test"])],
                [new VerificationCommand(VerificationScope.Conformance, "dotnet", ["test"])],
                TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Single(result.Results);
        }
        finally { if (Directory.Exists(artifactRoot)) Directory.Delete(artifactRoot, true); }
    }

    [Fact]
    public async Task RunAsync_CacheHit_DoesNotExecuteCommand()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"nes-lab-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(artifactRoot);
            var cachedLog = Path.Combine(artifactRoot, "cached.log");
            await File.WriteAllTextAsync(cachedLog, "cached", TestContext.Current.CancellationToken);
            var cachedResult = new VerificationResult(
                VerificationScope.Cpu,
                true,
                VerificationOutcome.Passed,
                0,
                12,
                new VerificationSummary(5, 0, 5, 0),
                [],
                cachedLog,
                6,
                Cached: true);
            var executor = new CountingExecutor();
            var runner = new VerificationRunner(
                executor,
                artifactRoot,
                new StubCache(cachedResult));

            var result = await runner.RunAsync(
                [new VerificationCommand(VerificationScope.Cpu, "dotnet", ["test"])],
                TestContext.Current.CancellationToken);

            Assert.Equal(0, executor.ExecutionCount);
            Assert.Equal(1, result.CacheHits);
            Assert.True(Assert.Single(result.Results).Cached);
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
                Directory.Delete(artifactRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_CancellationIsRecordedBeforeItPropagates()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"nes-lab-{Guid.NewGuid():N}");
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var sink = new CapturingSink();
            var runner = new VerificationRunner(
                new ThrowingExecutor(new OperationCanceledException(cancellation.Token)), artifactRoot,
                runSink: sink);

            await Assert.ThrowsAsync<OperationCanceledException>(() => runner.RunAsync(
                [new VerificationCommand(VerificationScope.Cpu, "dotnet", ["test"])], cancellation.Token));

            Assert.Equal(VerificationOutcome.Cancelled, Assert.IsType<VerificationResult>(sink.Result).Outcome);
        }
        finally { if (Directory.Exists(artifactRoot)) Directory.Delete(artifactRoot, true); }
    }

    [Fact]
    public async Task RunAsync_ReportsStartHeartbeatAndCompletionWithoutChangingResult()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"nes-lab-{Guid.NewGuid():N}");
        try
        {
            List<VerificationProgress> progress = [];
            var runner = new VerificationRunner(
                new DelayedExecutor(TimeSpan.FromMilliseconds(30)), artifactRoot,
                progress: progress.Add, progressInterval: TimeSpan.FromMilliseconds(5));

            var result = await runner.RunAsync(
                [new VerificationCommand(VerificationScope.Cpu, "dotnet", ["test"])],
                TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal(VerificationProgressPhase.Started, progress[0].Phase);
            Assert.Contains(progress, item => item.Phase == VerificationProgressPhase.Heartbeat);
            Assert.Equal(VerificationProgressPhase.Completed, progress[^1].Phase);
        }
        finally { if (Directory.Exists(artifactRoot)) Directory.Delete(artifactRoot, true); }
    }

    private sealed class StubExecutor(CommandExecution execution) : ICommandExecutor
    {
        public Task<CommandExecution> ExecuteAsync(
            VerificationCommand command,
            string workingDirectory,
            CancellationToken cancellationToken) => Task.FromResult(execution);
    }

    private sealed class ThrowingExecutor(Exception exception) : ICommandExecutor
    {
        public Task<CommandExecution> ExecuteAsync(
            VerificationCommand command,
            string workingDirectory,
            CancellationToken cancellationToken) => Task.FromException<CommandExecution>(exception);
    }

    private sealed class SequenceExecutor(params CommandExecution[] executions) : ICommandExecutor
    {
        private int _index;

        public Task<CommandExecution> ExecuteAsync(
            VerificationCommand command,
            string workingDirectory,
            CancellationToken cancellationToken) => Task.FromResult(executions[_index++]);
    }

    private sealed class CountingExecutor : ICommandExecutor
    {
        public int ExecutionCount { get; private set; }

        public Task<CommandExecution> ExecuteAsync(
            VerificationCommand command,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.FromResult(new CommandExecution(0, "", "", TimeSpan.Zero));
        }
    }

    private sealed class DelayedExecutor(TimeSpan delay) : ICommandExecutor
    {
        public async Task<CommandExecution> ExecuteAsync(
            VerificationCommand command, string workingDirectory, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return new CommandExecution(0,
                "Test run summary: Passed!\n total: 1\n failed: 0\n succeeded: 1\n skipped: 0",
                "", delay);
        }
    }

    private sealed class StubCache(VerificationResult result) : IVerificationCache
    {
        public Task<VerificationResult?> TryGetAsync(
            VerificationCommand command,
            CancellationToken cancellationToken) => Task.FromResult<VerificationResult?>(result);

        public Task StoreAsync(
            VerificationCommand command,
            VerificationResult result,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CapturingSink : IVerificationRunSink
    {
        public VerificationResult? Result { get; private set; }
        public Task RecordAsync(VerificationCommand command, VerificationResult result,
            CancellationToken cancellationToken) { Result = result; return Task.CompletedTask; }
    }
}
