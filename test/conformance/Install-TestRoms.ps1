[CmdletBinding()]
param(
    [string] $Destination = (Join-Path $PSScriptRoot '..\..\test-roms\nes-test-roms'),
    [string] $HolyMapperelDestination = (Join-Path $PSScriptRoot '..\..\test-roms\holy-mapperel-v0.02'),
    [string] $AccuracyCoinDestination = (Join-Path $PSScriptRoot '..\..\test-roms\accuracy-coin')
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

$accuracyCoinRepository = 'https://github.com/100thCoin/AccuracyCoin.git'
$accuracyCoinCommit = '7dc08e5aeb4c3dd146b009a32797f16ae45c78a4'
$accuracyCoinRomHash = '448df0e3e6aed4d36972d79d63715c0fccbe89bd435ef3a2a97fbfb70184cc96'
$resolvedAccuracyCoinDestination = [System.IO.Path]::GetFullPath($AccuracyCoinDestination)
if (-not (Test-Path -LiteralPath $resolvedAccuracyCoinDestination)) {
    git clone --no-checkout $accuracyCoinRepository $resolvedAccuracyCoinDestination
    if ($LASTEXITCODE -ne 0) { throw 'Unable to clone the AccuracyCoin repository.' }
}
if (-not (Test-Path -LiteralPath (Join-Path $resolvedAccuracyCoinDestination '.git'))) {
    throw "The AccuracyCoin destination exists but is not a Git repository: $resolvedAccuracyCoinDestination"
}
git -C $resolvedAccuracyCoinDestination fetch origin $accuracyCoinCommit
if ($LASTEXITCODE -ne 0) { throw 'Unable to fetch the pinned AccuracyCoin revision.' }
git -C $resolvedAccuracyCoinDestination checkout --detach $accuracyCoinCommit
if ($LASTEXITCODE -ne 0) { throw 'Unable to check out the pinned AccuracyCoin revision.' }
$actualAccuracyCoinCommit = (git -C $resolvedAccuracyCoinDestination rev-parse HEAD).Trim()
if ($actualAccuracyCoinCommit -ne $accuracyCoinCommit) {
    throw "Expected AccuracyCoin revision $accuracyCoinCommit but checked out $actualAccuracyCoinCommit."
}
$accuracyCoinRom = Join-Path $resolvedAccuracyCoinDestination 'AccuracyCoin.nes'
$actualAccuracyCoinHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $accuracyCoinRom).Hash.ToLowerInvariant()
if ($actualAccuracyCoinHash -ne $accuracyCoinRomHash) {
    throw "Expected AccuracyCoin ROM SHA-256 $accuracyCoinRomHash but found $actualAccuracyCoinHash."
}

$holyMapperelUri = 'https://github.com/pinobatch/holy-mapperel/releases/download/v0.02/holy-mapperel-bin-0.02.7z'
$holyMapperelArchiveHash = '70f85671e21f293599baebb662faeb06a4c04e9c9ceb283d96d4197f09e4ce7a'
$resolvedHolyMapperelDestination = [System.IO.Path]::GetFullPath($HolyMapperelDestination)
$archive = Join-Path $resolvedHolyMapperelDestination 'holy-mapperel-bin-0.02.7z'
New-Item -ItemType Directory -Force $resolvedHolyMapperelDestination | Out-Null
Invoke-WebRequest -Uri $holyMapperelUri -OutFile $archive
$actualArchiveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
if ($actualArchiveHash -ne $holyMapperelArchiveHash) {
    throw "Expected Holy Mapperel archive SHA-256 $holyMapperelArchiveHash but downloaded $actualArchiveHash."
}
tar -xf $archive -C $resolvedHolyMapperelDestination
if ($LASTEXITCODE -ne 0) { throw 'Unable to extract the pinned Holy Mapperel release.' }

Write-Host "Installed hardware-validated NES test ROMs at $resolvedDestination"
Write-Host "Installed Holy Mapperel v0.02 ROMs at $resolvedHolyMapperelDestination"
Write-Host "Installed AccuracyCoin at $resolvedAccuracyCoinDestination"
