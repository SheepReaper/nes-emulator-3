namespace Sheep.Nes.Lab.Tests;

public sealed class ArtifactCommandParserTests
{
    [Fact]
    public void Pin_RequiresImmutableContentAddressedUri()
    {
        var digest = new string('a', 64);
        var invocation = Assert.IsType<ArtifactInvocation>(ArtifactCommandParser.Parse([
            "artifacts", "pin", "--uri", $"nes-lab://artifact/trace/sha256/{digest}"
        ]).Invocation);
        Assert.Equal("trace", invocation.Kind);
        Assert.Equal(digest, invocation.Digest);
        Assert.Equal("invalid_uri", ArtifactCommandParser.Parse([
            "artifacts", "pin", "--uri", "nes-lab://artifact/trace/latest"
        ]).Error?.Code);
    }

    [Fact]
    public void Prune_DefaultsToThirtyDays()
    {
        var invocation = Assert.IsType<ArtifactInvocation>(ArtifactCommandParser.Parse([
            "artifacts", "prune"
        ]).Invocation);
        Assert.Equal(TimeSpan.FromDays(30), invocation.OlderThan);
    }
}
