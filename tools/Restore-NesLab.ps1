[CmdletBinding()]
param(
    [switch]$SkipRestore,
    [switch]$SkipClientChecks,
    [switch]$Force,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArguments
)

$ErrorActionPreference = "Stop"
foreach ($argument in $RemainingArguments) {
    if ($argument -ceq "--force") { $Force = $true }
    else { throw "Unknown Restore-NesLab argument '$argument'." }
}
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "src/tools/nes-lab/Sheep.Nes.Lab.csproj"
$gatewayAssembly = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot ".artifacts/nes-lab/gateway/Sheep.Nes.Lab.dll"))

function Stop-NesLabGatewayProcess {
    param([Parameter(Mandatory)][string]$AssemblyPath)

    $gatewayProcesses = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
        Where-Object {
            $_.ProcessId -ne $PID -and
            -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
            $_.CommandLine.IndexOf($AssemblyPath, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
        }

    foreach ($gatewayProcess in $gatewayProcesses) {
        Write-Output "Stopping NES Lab MCP gateway process $($gatewayProcess.ProcessId)."
        Stop-Process -Id $gatewayProcess.ProcessId -Force -ErrorAction Stop
        try {
            Wait-Process -Id $gatewayProcess.ProcessId -Timeout 10 -ErrorAction Stop
        }
        catch [Microsoft.PowerShell.Commands.ProcessCommandException] {
            # Wait-Process reports a missing process when it exits between Stop-Process and the wait.
            if (Get-Process -Id $gatewayProcess.ProcessId -ErrorAction SilentlyContinue) { throw }
        }
    }

    $stillRunning = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
            $_.CommandLine.IndexOf($AssemblyPath, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
        }
    if ($stillRunning) {
        $processIds = ($stillRunning.ProcessId | Sort-Object) -join ", "
        throw "NES Lab MCP gateway processes did not terminate within 10 seconds: $processIds."
    }
}

Push-Location $repositoryRoot
try {
    if ($Force) {
        Stop-NesLabGatewayProcess -AssemblyPath $gatewayAssembly
    }

    if (-not $SkipRestore) {
        dotnet restore $project
        if ($LASTEXITCODE -ne 0) { throw "NES Lab restore failed." }
    }

    dotnet run --project $project --no-restore -- setup mcp --client antigravity --repair
    if ($LASTEXITCODE -ne 0) {
        $forceHint = if ($Force) { "The forced gateway shutdown did not release the publish target." }
            else { "Close active NES Lab MCP clients or retry with -Force." }
        throw "NES Lab restore failed. $forceHint"
    }

    if (-not $SkipClientChecks) {
        dotnet run --project $project --no-restore -- setup mcp --client antigravity --check
        if ($LASTEXITCODE -ne 0) { throw "Antigravity NES Lab health check failed." }
        dotnet run --project $project --no-restore -- setup mcp --client copilot --check
        if ($LASTEXITCODE -ne 0) { throw "Copilot NES Lab health check failed." }
    }

    if ($Force) {
        Write-Output "NES Lab gateway republished and restarted through a fresh MCP health probe. Connected clients may reconnect automatically."
    }
    else {
        Write-Output "NES Lab gateway restored."
    }
}
finally {
    Pop-Location
}
