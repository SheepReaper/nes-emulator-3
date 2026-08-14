namespace Sheep.Nes.Lab.Tests;

public sealed class SetupCommandParserTests
{
    [Theory]
    [InlineData("codex")]
    [InlineData("antigravity")]
    [InlineData("copilot")]
    public void McpCheck_ParsesSupportedClients(string client)
    {
        var result = SetupCommandParser.Parse(["setup", "mcp", "--client", client, "--check"]);

        var invocation = Assert.IsType<McpSetupInvocation>(result.Invocation);
        Assert.Equal(client, invocation.Client);
        Assert.Equal(SetupAction.Check, invocation.Action);
    }

    [Fact]
    public void ModelApply_ParsesExplicitMutation()
    {
        var result = SetupCommandParser.Parse(["setup", "model", "--apply"]);

        Assert.Equal(SetupAction.Apply, Assert.IsType<ModelSetupInvocation>(result.Invocation).Action);
    }
}
