using System.Diagnostics;

namespace Sheep.Nes.Lab;

public sealed record CommandExecution(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration);

public interface ICommandExecutor
{
    Task<CommandExecution> ExecuteAsync(
        VerificationCommand command,
        string workingDirectory,
        CancellationToken cancellationToken);
}

public sealed class ProcessCommandExecutor : ICommandExecutor
{
    public async Task<CommandExecution> ExecuteAsync(
        VerificationCommand command,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(command.FileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in command.Arguments)
            startInfo.ArgumentList.Add(argument);
        if (command.Environment is not null)
        {
            foreach (var variable in command.Environment)
                startInfo.Environment[variable.Key] = variable.Value;
        }

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        if (!process.Start())
            throw new InvalidOperationException($"Could not start '{command.FileName}'.");

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }

        return new CommandExecution(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false),
            stopwatch.Elapsed);
    }
}
