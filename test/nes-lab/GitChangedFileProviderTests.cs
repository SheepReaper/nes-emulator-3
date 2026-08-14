using Sheep.Nes.Lab;

namespace Sheep.Nes.Lab.Tests;

public sealed class GitChangedFileProviderTests
{
    [Fact]
    public async Task GetChangedFilesAsync_MergesTrackedAndUntrackedFiles()
    {
        var executor = new SequenceExecutor(
            new CommandExecution(0, "src/lib/emulation/Cpu.cs\ntest/cpu/CpuTests.cs\n", "", TimeSpan.Zero),
            new CommandExecution(0, "test/nes-lab/NewTests.cs\ntest/cpu/CpuTests.cs\n", "", TimeSpan.Zero));
        var provider = new GitChangedFileProvider(executor, "repo");

        var files = await provider.GetChangedFilesAsync(TestContext.Current.CancellationToken);

        Assert.Equal([
            "src/lib/emulation/Cpu.cs",
            "test/cpu/CpuTests.cs",
            "test/nes-lab/NewTests.cs"
        ], files);
    }

    [Fact]
    public async Task GetChangedFilesAsync_GitFailure_ThrowsUsefulInfrastructureError()
    {
        var executor = new SequenceExecutor(
            new CommandExecution(128, "", "fatal: not a git repository", TimeSpan.Zero));
        var provider = new GitChangedFileProvider(executor, "repo");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetChangedFilesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("not a git repository", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetChangedFilesAsync_IgnoresUntrackedFilesOutsideRepositoryPaths()
    {
        var executor = new SequenceExecutor(
            new CommandExecution(0, "", "", TimeSpan.Zero),
            new CommandExecution(
                0,
                "homelab-discovery/capture.txt\nsrc/tools/nes-lab/NewCommand.cs\n",
                "",
                TimeSpan.Zero));
        var provider = new GitChangedFileProvider(executor, "repo");

        var files = await provider.GetChangedFilesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["src/tools/nes-lab/NewCommand.cs"], files);
    }

    [Fact]
    public async Task GetChangedFilesAsync_WithBase_IncludesCommittedDirtyAndUntrackedChanges()
    {
        var executor = new RecordingExecutor(
            new CommandExecution(0, "abc123\n", "", TimeSpan.Zero),
            new CommandExecution(0, "src/lib/emulation/Ppu.cs\n", "", TimeSpan.Zero),
            new CommandExecution(0, "test/cpu/PpuTests.cs\n", "", TimeSpan.Zero),
            new CommandExecution(0, "src/lib/emulation/New.cs\n", "", TimeSpan.Zero));
        var provider = new GitChangedFileProvider(executor, "repo");

        var files = await provider.GetChangedFilesAsync(
            TestContext.Current.CancellationToken, "origin/main");

        Assert.Equal(["src/lib/emulation/Ppu.cs", "test/cpu/PpuTests.cs", "src/lib/emulation/New.cs"], files);
        Assert.Equal(["merge-base", "HEAD", "origin/main"], executor.Commands[0].Arguments);
        Assert.Contains("abc123..HEAD", executor.Commands[1].Arguments);
    }

    private sealed class SequenceExecutor(params CommandExecution[] executions) : ICommandExecutor
    {
        private int _index;

        public Task<CommandExecution> ExecuteAsync(
            VerificationCommand command,
            string workingDirectory,
            CancellationToken cancellationToken) => Task.FromResult(executions[_index++]);
    }

    private sealed class RecordingExecutor(params CommandExecution[] executions) : ICommandExecutor
    {
        private int index;
        public List<VerificationCommand> Commands { get; } = [];
        public Task<CommandExecution> ExecuteAsync(VerificationCommand command, string workingDirectory,
            CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(executions[index++]);
        }
    }
}
