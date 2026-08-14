using System.Text.Json.Nodes;

namespace Sheep.Nes.Lab.Tests;

public sealed class McpRegistrationTests
{
    [Fact]
    public void SetupParser_RecognizesRepairAsPublishRegisterProbe()
    {
        var invocation = Assert.IsType<McpSetupInvocation>(SetupCommandParser.Parse(
            ["setup", "mcp", "--client", "codex", "--repair"]).Invocation);
        Assert.Equal(SetupAction.Repair, invocation.Action);
    }

    [Fact]
    public void MergeAntigravity_PreservesExistingServersAndAddsPortableStdioEntry()
    {
        var document = JsonNode.Parse("""{"mcpServers":{"existing":{"command":"tool"}}}""")!;

        var result = McpRegistration.MergeJson(document, McpClientKind.Antigravity,
            "dotnet", ["lab.dll", "mcp"], "repo");

        Assert.NotNull(result["mcpServers"]?["existing"]);
        Assert.Equal("dotnet", result["mcpServers"]?["nes-lab"]?["command"]?.GetValue<string>());
    }

    [Fact]
    public void MergeCopilot_AddsRequiredLocalTypeAndTools()
    {
        var result = McpRegistration.MergeJson(new JsonObject(), McpClientKind.Copilot,
            "dotnet", ["lab.dll", "mcp"], "repo");

        var server = result["mcpServers"]?["nes-lab"];
        Assert.Equal("local", server?["type"]?.GetValue<string>());
        Assert.Equal("*", server?["tools"]?[0]?.GetValue<string>());
    }

    [Fact]
    public void RemoveJson_PreservesUnrelatedServers()
    {
        var document = JsonNode.Parse("""{"mcpServers":{"nes-lab":{},"existing":{}}}""")!;

        var result = McpRegistration.RemoveJson(document);

        Assert.Null(result["mcpServers"]?["nes-lab"]);
        Assert.NotNull(result["mcpServers"]?["existing"]);
    }

    [Fact]
    public void ExtractJsonEnvelope_SeparatesUnexpectedMachineOutput()
    {
        const string output = "Import-Clixml: warning\n{\"success\":true}\n";

        Assert.Equal("{\"success\":true}", McpRegistration.ExtractJsonEnvelope(output));
        Assert.Equal("Import-Clixml: warning", McpRegistration.UnexpectedOutput(output));
    }

    [Fact]
    public void ExtractJsonEnvelope_RejectsMalformedOutput()
    {
        Assert.Throws<InvalidDataException>(() => McpRegistration.ExtractJsonEnvelope("warning only"));
    }
}
