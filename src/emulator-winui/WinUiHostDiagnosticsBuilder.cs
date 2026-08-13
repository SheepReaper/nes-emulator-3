using System.Text.Json;

namespace EmuSheep;

public sealed record WinUiHostDiagnosticState(string ApplicationVersion, string EmulatorVersion,
    string? RomSha256, int? Mapper, string VideoStandard, string? AudioGraphStatus,
    string? AudioDeviceId, int? QuantumSamples, long? RequiredSamples, long? SubmittedSamples,
    long? AudioUnderruns, string? AudioException, int? WindowWidth, int? WindowHeight,
    string? ScalingMode, long? DispatcherBacklog, ulong? PresentedFrames);

public static class WinUiHostDiagnosticsBuilder
{
    public static string Serialize(WinUiHostDiagnosticState state) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        applicationVersion = state.ApplicationVersion,
        emulatorVersion = state.EmulatorVersion,
        romSha256 = state.RomSha256,
        mapper = state.Mapper,
        videoStandard = state.VideoStandard,
        audio = new
        {
            graphStatus = state.AudioGraphStatus,
            deviceId = state.AudioDeviceId,
            quantumSamples = state.QuantumSamples,
            requiredSamples = state.RequiredSamples,
            submittedSamples = state.SubmittedSamples,
            underruns = state.AudioUnderruns,
            exception = state.AudioException
        },
        video = new
        {
            windowWidth = state.WindowWidth,
            windowHeight = state.WindowHeight,
            scalingMode = state.ScalingMode,
            dispatcherBacklog = state.DispatcherBacklog,
            presentedFrames = state.PresentedFrames
        }
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
