$ErrorActionPreference = 'Stop'
$audit = Join-Path $PSScriptRoot '../verify_source_ownership.ps1'
$temporary = Join-Path ([IO.Path]::GetTempPath()) "oracle-source-ownership-$([Guid]::NewGuid().ToString('N'))"
$fixture = Join-Path $temporary 'src/Features/Fixture.cs'
[void][IO.Directory]::CreateDirectory((Split-Path $fixture -Parent))
try {
    foreach ($source in @(
        'void Begin(OracleGame game) { }',
        'OracleGame selectedGame = OracleGame.Ages;'
    )) {
        [IO.File]::WriteAllText($fixture, $source)
        & $audit -Project $temporary | Out-Null
    }
    foreach ($source in @(
        'void Begin(OracleGame game = OracleGame.Ages) { }',
        'void Begin(OracleGame game = OracleGame.Seasons) { }',
        'void Begin(OracleGame game = default) { }',
        'static Database Shared => For(AgesGameContent.AssetRoot);',
        'memory.Read(AgesWramAddress.wLinkHealth);'
    )) {
        [IO.File]::WriteAllText($fixture, $source)
        $rejected = $false
        try { & $audit -Project $temporary | Out-Null }
        catch { $rejected = $_.Exception.Message.Contains('Source ownership audit failed') }
        if (-not $rejected) { throw "Source ownership accepted: $source" }
    }
    Write-Host 'Source ownership fixtures passed (explicit game selection and pack-independent shared code).'
}
finally {
    $target = [IO.Path]::GetFullPath($temporary)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $target.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "Invalid fixture cleanup path: $target" }
    Remove-Item -LiteralPath $target -Recurse -Force
}
