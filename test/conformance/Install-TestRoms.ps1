[CmdletBinding()]
param(
    [string] $Destination = (Join-Path $PSScriptRoot '..\..\test-roms\nes-test-roms')
)

$ErrorActionPreference = 'Stop'
$repository = 'https://github.com/christopherpow/nes-test-roms.git'
$commit = '95d8f621ae55cee0d09b91519a8989ae0e64753b'
$resolvedDestination = [System.IO.Path]::GetFullPath($Destination)

if (-not (Test-Path -LiteralPath $resolvedDestination)) {
    git clone --no-checkout $repository $resolvedDestination
    if ($LASTEXITCODE -ne 0) { throw 'Unable to clone the NES test ROM repository.' }
}

if (-not (Test-Path -LiteralPath (Join-Path $resolvedDestination '.git'))) {
    throw "The destination exists but is not a Git repository: $resolvedDestination"
}

git -C $resolvedDestination fetch origin $commit
if ($LASTEXITCODE -ne 0) { throw 'Unable to fetch the pinned NES test ROM revision.' }
git -C $resolvedDestination checkout --detach $commit
if ($LASTEXITCODE -ne 0) { throw 'Unable to check out the pinned NES test ROM revision.' }

$actualCommit = (git -C $resolvedDestination rev-parse HEAD).Trim()
if ($actualCommit -ne $commit) { throw "Expected revision $commit but checked out $actualCommit." }

Write-Host "Installed hardware-validated NES test ROMs at $resolvedDestination"
