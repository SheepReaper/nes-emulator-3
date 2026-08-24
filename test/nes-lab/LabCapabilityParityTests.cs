using System.Text.Json;

namespace Sheep.Nes.Lab.Tests;

public sealed class LabCapabilityParityTests
{
    [Fact]
    public void EveryAdvertisedOperationMapsToAnAllowlistedCommand()
    {
        foreach (var capability in LabCapabilityCatalog.List())
        foreach (var operation in LabCapabilityCatalog.Describe(capability.Name).Operations)
        {
            using var arguments = JsonDocument.Parse(Sample(capability.Name, operation.Name));
            var command = LabMcpCommandMapper.Map(capability.Name, operation.Name, arguments.RootElement);
            Assert.NotEmpty(command);
        }
    }

    private static string Sample(string capability, string operation) => (capability, operation) switch
    {
        ("code", "find") => "{\"symbol\":\"Cpu\"}",
        ("code", "refs" or "callers" or "tests") => "{\"symbolId\":\"id\"}",
        ("context", "build") => "{\"symbolId\":\"id\"}",
        ("memory", "search") => "{\"query\":\"timing\"}",
        ("memory", "show") => "{\"id\":1}",
        ("memory", "proposals-show" or "proposals-accept" or "proposals-reject") => "{\"id\":1}",
        ("feedback", "record") => "{\"packetId\":\"packet-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"usefulEvidenceIds\":[]}",
        ("feedback", "search") => "{\"query\":\"timing\"}",
        ("references", "search") => "{\"query\":\"dma\"}",
        ("references", "show") => "{\"id\":\"nesdev-dma\"}",
        ("experiment", "run") => "{\"scenarioPath\":\"tasks/nes-lab/experiments/controller-dma-repeat.json\"}",
        ("experiment", "run-inline") => "{\"scenario\":{\"schemaVersion\":1,\"name\":\"safe\"}}",
        ("experiment", "compare") => "{\"leftUri\":\"nes-lab://artifact/experiment/sha256/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"rightUri\":\"nes-lab://artifact/experiment/sha256/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}",
        ("media", "frame-compare") => "{\"leftUri\":\"nes-lab://artifact/frame/sha256/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"rightUri\":\"nes-lab://artifact/frame/sha256/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}",
        ("media", "audio-analyze") => "{\"uri\":\"nes-lab://artifact/audio/sha256/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}",
        ("media", "audio-compare") => "{\"leftUri\":\"nes-lab://artifact/audio/sha256/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"rightUri\":\"nes-lab://artifact/audio/sha256/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}",
        ("investigate", "task") => "{\"task\":\"cpu timing\"}",
        ("investigate", "run") => "{\"runId\":\"latest\"}",
        ("session", "close") => "{\"task\":\"handoff\"}",
        ("session", "show") => "{\"uri\":\"nes-lab://artifact/handoff/sha256/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}",
        ("host", "diagnostics-import") => "{\"bundle\":{\"schemaVersion\":1}}",
        ("host", "diagnostics-show") => "{\"uri\":\"nes-lab://artifact/host-diagnostics/sha256/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}",
        ("host", "diagnostics-compare") => "{\"leftUri\":\"nes-lab://artifact/host-diagnostics/sha256/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"rightUri\":\"nes-lab://artifact/host-diagnostics/sha256/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}",
        ("rom", "show" or "source" or "diagnose") => operation == "diagnose"
            ? "{\"name\":\"case\",\"code\":1}" : operation == "source"
                ? "{\"name\":\"case\",\"symbol\":\"main\"}" : "{\"name\":\"case\"}",
        ("trace", "query") => "{\"artifactPath\":\"trace.json\"}",
        ("trace", "diff") => "{\"expectedArtifactPath\":\"a.json\",\"actualArtifactPath\":\"b.json\"}",
        ("trace", "capture") => "{\"caseName\":\"case\"}",
        ("baseline", "update") => "{\"runId\":\"latest\"}",
        ("history", "search") => "{\"query\":\"failure\"}",
        ("diagnose", "run") => "{\"caseName\":\"case\"}",
        ("artifacts", "pin" or "unpin") => "{\"uri\":\"nes-lab://artifact/log/sha256/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}",
        ("artifacts", "describe" or "text") => "{\"uri\":\"nes-lab://artifact/log/sha256/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}",
        _ => "{}"
    };
}
