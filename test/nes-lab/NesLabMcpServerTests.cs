using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Sheep.Nes.Lab.Tests;

public sealed class NesLabMcpServerTests
{
    [Fact(Timeout = 30_000)]
    public async Task Server_ExposesSmallSurfaceAndExecutesExistingContextCommand()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "nes-lab-test",
            Command = "dotnet",
            Arguments = [typeof(NesLabMcpServer).Assembly.Location, "mcp"],
            WorkingDirectory = FindRepositoryRoot(),
            InheritEnvironmentVariables = true
        });
        await using var client = await McpClient.CreateAsync(
            transport, cancellationToken: TestContext.Current.CancellationToken);

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(["nes_lab_discover", "nes_lab_inspect", "nes_lab_run"],
            tools.Select(tool => tool.Name).Order());

        var prompts = await client.ListPromptsAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(prompts, prompt => prompt.Name == "diagnose-timing-failure");
        var prompt = await client.GetPromptAsync("diagnose-timing-failure",
            new Dictionary<string, object?> { ["runId"] = "latest" },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("nes_lab_inspect", string.Concat(prompt.Messages.SelectMany(message =>
            message.Content is TextContentBlock text ? [text.Text] : Array.Empty<string>())),
            StringComparison.Ordinal);

        var templates = await client.ListResourceTemplatesAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(templates, template => template.UriTemplate ==
            "nes-lab://artifact/{kind}/sha256/{digest}");
        Assert.Contains(templates, template => template.UriTemplate == "nes://roms/{suite}/{case}");

        var artifactRoot = Path.Combine(FindRepositoryRoot(), ".artifacts", "nes-lab");
        var published = await new ImmutableArtifactStore(artifactRoot).PublishTextAsync(
            "context", $"{{\"nonce\":\"{Guid.NewGuid():N}\"}}", "application/json",
            cancellationToken: TestContext.Current.CancellationToken);
        var resource = await client.ReadResourceAsync(
            ImmutableArtifactStore.Uri("context", published.Digest),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(published.Digest, string.Concat(resource.Contents
            .OfType<TextResourceContents>().Select(content => content.Text)), StringComparison.Ordinal);
        var timing = await client.ReadResourceAsync("nes://architecture/timing",
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("NES timing", string.Concat(timing.Contents
            .OfType<TextResourceContents>().Select(content => content.Text)), StringComparison.Ordinal);
        var listedResources = await client.ListResourcesAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(listedResources, item => item.Uri ==
            ImmutableArtifactStore.Uri("context", published.Digest));
        var objectDirectory = Path.Combine(artifactRoot, "objects", "context", published.Digest[..2]);
        File.Delete(Path.Combine(objectDirectory, published.Digest + ".data"));
        File.Delete(Path.Combine(objectDirectory, published.Digest + ".meta.json"));

        var discovery = await client.CallToolAsync("nes_lab_discover",
            new Dictionary<string, object?> { ["capability"] = "context" },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("budgetBytes", Text(discovery), StringComparison.Ordinal);

        var execution = await client.CallToolAsync("nes_lab_inspect",
            new Dictionary<string, object?>
            {
                ["capability"] = "context",
                ["operation"] = "build",
                ["arguments"] = new { symbol = "CpuClockDriver", budgetBytes = 2048 }
            }, cancellationToken: TestContext.Current.CancellationToken);
        var payload = Text(execution);
        Assert.True(payload.Contains("\"operation\":\"context-build\"", StringComparison.Ordinal), payload);
        Assert.Contains("\"budgetBytes\":2048", payload, StringComparison.Ordinal);
    }

    private static string Text(CallToolResult result) => string.Concat(
        result.Content.OfType<TextContentBlock>().Select(content => content.Text));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nes-emulator-3.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
