using System.Text.Json;
using Xunit;

namespace EmuSheep.Tests;

public sealed class WinUiHostDiagnosticsBuilderTests
{
    [Fact]
    public void Serialize_ProducesNesLabPortableSchemaWithoutLabDependency()
    {
        var json = WinUiHostDiagnosticsBuilder.Serialize(new("1.0", "1.0", new string('a', 64), 4,
            "Ntsc", "running", "device", 480, 512, 500, 1, null, 1280, 720, "Uniform", 0, 60));
        using var document = JsonDocument.Parse(json);

        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("device", document.RootElement.GetProperty("audio").GetProperty("deviceId").GetString());
        Assert.Equal(60UL, document.RootElement.GetProperty("video").GetProperty("presentedFrames").GetUInt64());
    }
}
