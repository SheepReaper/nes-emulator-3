namespace Sheep.Nes.Lab;

public enum VerificationScope
{
    LabTests,
    Cpu,
    Conformance,
    WinUiTests,
    Library,
    WinUiInterop,
    WinUiApp,
    Ppu,
    Apu,
    Dma,
    Bus,
    Mapper,
    Cartridge,
    Debugger,
    WinUiVideo,
    WinUiAudio,
    All
}

public enum LabOutputFormat
{
    Json
}

public enum VerificationExitPolicy { Strict, BaselineAware }
public enum VerificationStatus { Passed, AcceptedBaseline, ImprovedBaseline, Regression, InfrastructureFailure }

public sealed record LabInvocation(
    VerificationScope Scope,
    bool PlanOnly,
    bool NoRestore,
    bool ContinueOnFailure,
    bool Changed,
    LabOutputFormat Format,
    string? CaseName,
    bool TraceOnFailure,
    bool TraceAlways,
    VerificationExitPolicy ExitPolicy = VerificationExitPolicy.Strict);

public sealed record LabError(string Code, string Message);

public sealed record LabParseResult(LabInvocation? Invocation, LabError? Error)
{
    public bool IsSuccess => Invocation is not null;

    public static LabParseResult Success(LabInvocation invocation) => new(invocation, null);

    public static LabParseResult Failure(string code, string message) =>
        new(null, new LabError(code, message));
}

public sealed record VerificationCommand(
    VerificationScope Scope,
    string FileName,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string>? Environment = null);
