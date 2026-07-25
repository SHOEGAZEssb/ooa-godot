param(
    [string]$Disassembly = "C:\msys64\home\timst\oracles-disasm",
    [string]$Rom = (Join-Path $PSScriptRoot `
        "..\Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc")
)

$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$importerProject = Join-Path $PSScriptRoot 'OracleImporter\OracleImporter.csproj'
$testsProject = Join-Path $PSScriptRoot `
    'OracleImporter.Tests\OracleImporter.Tests.csproj'
$importScript = Join-Path $PSScriptRoot 'import_oracles.ps1'
$assetRoot = Join-Path $project 'assets\oracle'
$temporary = Join-Path ([IO.Path]::GetTempPath()) `
    "ooa-import-parity-$([Guid]::NewGuid().ToString('N'))"
[void][IO.Directory]::CreateDirectory($temporary)

try {
    & dotnet run --project $testsProject --configuration Debug
    if ($LASTEXITCODE -ne 0) {
        throw "OracleImporter unit tests exited with code $LASTEXITCODE."
    }

    & $importScript -Disassembly $Disassembly -Rom $Rom
    $importerHostPath = Join-Path $PSScriptRoot `
        'OracleImporter\bin\Debug\net8.0\OracleOfAges.Importer.dll'
    $firstManifest = Join-Path $temporary 'first.tsv'
    & dotnet $importerHostPath manifest $assetRoot $firstManifest
    if ($LASTEXITCODE -ne 0) {
        throw "First generated-asset manifest exited with code $LASTEXITCODE."
    }

    & $importScript -Disassembly $Disassembly -Rom $Rom
    $secondManifest = Join-Path $temporary 'second.tsv'
    & dotnet $importerHostPath manifest $assetRoot $secondManifest
    if ($LASTEXITCODE -ne 0) {
        throw "Second generated-asset manifest exited with code $LASTEXITCODE."
    }

    $firstHash = (Get-FileHash -LiteralPath $firstManifest -Algorithm SHA256).Hash
    $secondHash = (Get-FileHash -LiteralPath $secondManifest -Algorithm SHA256).Hash
    if ($firstHash -ne $secondHash) {
        $difference = Compare-Object `
            ([IO.File]::ReadAllLines($firstManifest)) `
            ([IO.File]::ReadAllLines($secondManifest))
        throw "Two consecutive imports produced different assets:`n$($difference | Out-String)"
    }

    $assetCount = @([IO.File]::ReadAllLines($firstManifest) |
        Where-Object { -not $_.StartsWith('#') -and $_ -ne '' }).Count
    Write-Host (
        "Deterministic import verified for $assetCount generated assets " +
        "(manifest SHA-256 $($firstHash.ToLowerInvariant())).")
}
finally {
    $resolvedTemporary = [IO.Path]::GetFullPath($temporary)
    $resolvedTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolvedTemporary.StartsWith(
            $resolvedTempRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove non-temporary parity path: $resolvedTemporary"
    }
    if ([IO.Directory]::Exists($resolvedTemporary)) {
        [IO.Directory]::Delete($resolvedTemporary, $true)
    }
}
