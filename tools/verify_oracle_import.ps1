param(
    [string]$Disassembly = "C:\msys64\home\timst\oracles-disasm",
    [string]$Rom = (Join-Path $PSScriptRoot `
        "..\Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc"),
    [ValidateRange(1, 2)][int]$Workers = 2,
    [ValidateRange(1, 86400)][int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$testsProject = Join-Path $PSScriptRoot `
    'OracleImporter.Tests\OracleImporter.Tests.csproj'
$importScript = Join-Path $PSScriptRoot 'import_oracles.ps1'
$workerScript = Join-Path $PSScriptRoot 'Invoke-ImportWorkers.ps1'
$assetRoot = Join-Path $project 'assets\oracle'
$ownershipAudit = Join-Path $PSScriptRoot 'verify_source_ownership.ps1'
$temporary = Join-Path ([IO.Path]::GetTempPath()) `
    "ooa-import-parity-$([Guid]::NewGuid().ToString('N'))"
[void][IO.Directory]::CreateDirectory($temporary)
$timer = [Diagnostics.Stopwatch]::StartNew()
$succeeded = $false

try {
    & $ownershipAudit -Project $project

    # The test project builds its importer reference once, before any workers.
    & dotnet run --project $testsProject --configuration Debug
    if ($LASTEXITCODE -ne 0) {
        throw "OracleImporter unit tests exited with code $LASTEXITCODE."
    }
    & (Join-Path $PSScriptRoot 'OracleImporter.Tests\Test-ImportWorkers.ps1')

    $Disassembly = (Resolve-Path -LiteralPath $Disassembly).Path
    $Rom = (Resolve-Path -LiteralPath $Rom).Path
    $importerHostPath = Join-Path $PSScriptRoot `
        'OracleImporter\bin\Debug\net8.0\OracleOfAges.Importer.dll'
    $firstManifest = Join-Path $temporary 'first.tsv'
    $secondManifest = Join-Path $temporary 'second.tsv'
    $workerParameters = @{
        ImportScript = $importScript
        Disassembly = $Disassembly
        Rom = $Rom
        TimeoutSeconds = $TimeoutSeconds
    }
    Write-Host "Running import verification with $Workers worker(s). Logs: $temporary"
    if ($Workers -eq 2) {
        $secondRoot = Join-Path $temporary 'assets'
        & $workerScript @workerParameters -OutputDirectories @($assetRoot, $secondRoot) `
            -LogDirectory (Join-Path $temporary 'parallel')
    }
    else {
        $secondRoot = $assetRoot
        & $workerScript @workerParameters -OutputDirectories @($assetRoot) `
            -LogDirectory (Join-Path $temporary 'first')
    }

    & dotnet $importerHostPath manifest $assetRoot $firstManifest
    if ($LASTEXITCODE -ne 0) {
        throw "First generated-asset manifest exited with code $LASTEXITCODE."
    }

    if ($Workers -eq 1) {
        & $workerScript @workerParameters -OutputDirectories @($assetRoot) `
            -LogDirectory (Join-Path $temporary 'second')
    }
    & dotnet $importerHostPath manifest $secondRoot $secondManifest
    if ($LASTEXITCODE -ne 0) {
        throw "Second generated-asset manifest exited with code $LASTEXITCODE."
    }

    $firstHash = (Get-FileHash -LiteralPath $firstManifest -Algorithm SHA256).Hash
    $secondHash = (Get-FileHash -LiteralPath $secondManifest -Algorithm SHA256).Hash
    if ($firstHash -ne $secondHash) {
        $difference = Compare-Object `
            ([IO.File]::ReadAllLines($firstManifest)) `
            ([IO.File]::ReadAllLines($secondManifest))
        throw "Two imports produced different assets:`n$($difference | Out-String)"
    }

    $assetCount = @([IO.File]::ReadAllLines($firstManifest) |
        Where-Object { -not $_.StartsWith('#') -and $_ -ne '' }).Count
    Write-Host (
        "Deterministic import verified for $assetCount generated assets " +
        "(manifest SHA-256 $($firstHash.ToLowerInvariant())) " +
        "in $($timer.Elapsed.TotalSeconds.ToString('F2'))s with $Workers worker(s).")
    $succeeded = $true
}
finally {
    $resolvedTemporary = [IO.Path]::GetFullPath($temporary)
    $resolvedTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') +
        [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedTemporary.StartsWith(
            $resolvedTempRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove non-temporary parity path: $resolvedTemporary"
    }
    if ($succeeded -and [IO.Directory]::Exists($resolvedTemporary)) {
        [IO.Directory]::Delete($resolvedTemporary, $true)
    }
    elseif (-not $succeeded) {
        Write-Host "Import verification failed; diagnostics retained at $resolvedTemporary"
    }
}
