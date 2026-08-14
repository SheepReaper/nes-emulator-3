namespace Sheep.Nes.Lab;

public static class ModelSetup
{
    public static async Task<SetupResult> ExecuteAsync(ModelSetupInvocation invocation,
        string repositoryRoot, CancellationToken cancellationToken = default)
    {
        var show = await RunAsync(["show", invocation.Model], repositoryRoot, cancellationToken);
        if (invocation.Action == SetupAction.Check)
            return new("Ollama", invocation.Action, show.ExitCode == 0, false, null, "ollama",
                ["show", invocation.Model], Detail: show.ExitCode == 0 ? show.StandardOutput.Trim() : show.StandardError.Trim());
        var modelfile = invocation.Modelfile ?? Path.Combine(repositoryRoot, "src", "tools", "nes-lab", "ollama", "Modelfile");
        if (!File.Exists(modelfile)) throw new FileNotFoundException("NES Lab Ollama Modelfile was not found.", modelfile);
        var create = await RunAsync(["create", invocation.Model, "-f", modelfile], repositoryRoot, cancellationToken);
        if (create.ExitCode != 0) throw new InvalidOperationException(create.StandardError);
        return new("Ollama", invocation.Action, true, true, modelfile, "ollama",
            ["create", invocation.Model, "-f", modelfile], Detail: create.StandardOutput.Trim());
    }

    private static Task<CommandExecution> RunAsync(IReadOnlyList<string> arguments, string root,
        CancellationToken cancellationToken) => new ProcessCommandExecutor().ExecuteAsync(
            new VerificationCommand(VerificationScope.LabTests, "ollama", arguments), root, cancellationToken);
}
