using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed record HostUsageTelemetry(string? Provider = null, string? Model = null,
    long? CloudInputTokens = null, long? CloudOutputTokens = null, long? CachedTokens = null,
    int? GatewayCalls = null, double? TimeToUsefulEvidenceMilliseconds = null,
    double? TimeToDiagnosisMilliseconds = null, double? TimeToPassingVerificationMilliseconds = null,
    int? DirectSourceReads = null, IReadOnlyList<string>? PacketIds = null,
    IReadOnlyList<string>? EvidenceIds = null, string? Outcome = null)
{
    public string CloudInputTokensStatus => CloudInputTokens.HasValue ? "Reported" : "Unavailable";
    public string CloudOutputTokensStatus => CloudOutputTokens.HasValue ? "Reported" : "Unavailable";

    public static HostUsageTelemetry Parse(string json)
    {
        var result = JsonSerializer.Deserialize<HostUsageTelemetry>(json, LabResponseSerializer.Options)
            ?? throw new ArgumentException("Telemetry JSON is empty.");
        foreach (var value in new long?[] { result.CloudInputTokens, result.CloudOutputTokens, result.CachedTokens })
            if (value < 0) throw new ArgumentException("Token usage cannot be negative.");
        foreach (var value in new int?[] { result.GatewayCalls, result.DirectSourceReads })
            if (value < 0) throw new ArgumentException("Telemetry counts cannot be negative.");
        foreach (var value in new double?[] { result.TimeToUsefulEvidenceMilliseconds,
                     result.TimeToDiagnosisMilliseconds, result.TimeToPassingVerificationMilliseconds })
            if (value < 0 || double.IsNaN(value.GetValueOrDefault()) || double.IsInfinity(value.GetValueOrDefault()))
                throw new ArgumentException("Telemetry durations must be finite and non-negative.");
        return result;
    }

    public static HostUsageTelemetry ParseArgument(string value)
    {
        var json = File.Exists(value) ? File.ReadAllText(value) : value;
        return Parse(json);
    }
}
