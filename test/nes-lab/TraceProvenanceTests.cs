using System.Security.Cryptography;

namespace Sheep.Nes.Lab.Tests;

public sealed class TraceProvenanceTests
{
    [Fact]
    public async Task ComputeRomSha256Async_ReturnsUppercaseDigest()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, [1, 2, 3], TestContext.Current.CancellationToken);
            var expected = Convert.ToHexString(SHA256.HashData([1, 2, 3]));

            var actual = await TraceProvenance.ComputeRomSha256Async(
                path, TestContext.Current.CancellationToken);

            Assert.Equal(expected, actual);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetSourceCommitAsync_ReturnsExactHeadCommit()
    {
        var executor = new RecordingExecutor(new CommandExecution(
            0, "abc123\n", "", TimeSpan.Zero));
        var provenance = new TraceProvenance(executor, "repo");

        var commit = await provenance.GetSourceCommitAsync(TestContext.Current.CancellationToken);

        Assert.Equal("abc123", commit);
        Assert.Equal("git", executor.Command!.FileName);
        Assert.Equal(["rev-parse", "HEAD"], executor.Command.Arguments);
    }

    [Fact]
    public async Task GetSourceCommitAsync_ReportsGitFailure()
    {
        var provenance = new TraceProvenance(
            new RecordingExecutor(new CommandExecution(128, "", "bad git", TimeSpan.Zero)),
            "repo");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provenance.GetSourceCommitAsync(TestContext.Current.CancellationToken));

        Assert.Contains("bad git", exception.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingExecutor(CommandExecution execution) : ICommandExecutor
    {
        public VerificationCommand? Command { get; private set; }

        public Task<CommandExecution> ExecuteAsync(
            VerificationCommand command,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            Command = command;
            return Task.FromResult(execution);
        }
    }
}
