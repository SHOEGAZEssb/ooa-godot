# Resolver lifecycle without network access or original game assets.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../Resolve-OracleRom.ps1')
$realBuilder = ${function:Invoke-OracleRomBuild}
$temporary = Join-Path ([IO.Path]::GetTempPath()) "ooa-rom-tests ' [0] $([Guid]::NewGuid().ToString('N'))"
[void][IO.Directory]::CreateDirectory($temporary)
$fixture = Join-Path $temporary 'reference ROM [US].gbc'
$fixtureBytes = [byte[]]@(0x00, 0x42, 0xff, 0x10)
[IO.File]::WriteAllBytes($fixture, $fixtureBytes)
$fixturePins = [pscustomobject]@{
    revision = 'fixture-revision'; wlaVersion = '10.6'; recipeVersion = 1
    romLength = $fixtureBytes.Length
    romMd5 = (Get-FileHash -LiteralPath $fixture -Algorithm MD5).Hash
}
$buildCount = 0
$buildMode = 'good'
function Get-OracleRomBuildPins { return $fixturePins }
function Invoke-OracleRomBuild([string]$Stage, $Pins) {
    $script:buildCount++
    if ($buildMode -eq 'fail') { throw 'fixture assembler failure' }
    $output = Join-Path $Stage 'disassembly/ages.gbc'
    [void][IO.Directory]::CreateDirectory((Split-Path $output -Parent))
    $bytes = if ($buildMode -eq 'bad') { [byte[]]@(0, 0, 0, 0) } else { $fixtureBytes }
    [IO.File]::WriteAllBytes($output, $bytes)
}
function Assert-Rom([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-RomFailure([scriptblock]$Action, [string]$Message) {
    $caught = $false
    $failureMessage = 'no exception'
    try { & $Action }
    catch {
        $failureMessage = $_.Exception.Message
        $caught = $failureMessage.Contains($Message)
    }
    Assert-Rom $caught "Expected ROM failure containing '$Message'; got $failureMessage."
}
try {
    $previousBash = $env:OOA_ROM_BUILD_BASH
    try {
        $env:OOA_ROM_BUILD_BASH = Join-Path $temporary 'missing-bash'
        Assert-RomFailure { & $realBuilder $temporary $fixturePins } 'ROM build needs Bash'
    }
    finally { $env:OOA_ROM_BUILD_BASH = $previousBash }
    $bashForTest = $previousBash
    if (-not $bashForTest -and $env:OS -eq 'Windows_NT') {
        if ($env:MSYS2_ROOT) { $bashForTest = Join-Path $env:MSYS2_ROOT 'usr/bin/bash.exe' }
        elseif ([IO.File]::Exists('C:/msys64/usr/bin/bash.exe')) { $bashForTest = 'C:/msys64/usr/bin/bash.exe' }
    }
    elseif (-not $bashForTest) {
        $command = Get-Command bash -CommandType Application -ErrorAction SilentlyContinue
        if ($command) { $bashForTest = $command.Source }
    }
    if ($bashForTest -and [IO.File]::Exists($bashForTest)) {
        $previousToolsRoot = $oracleRomToolsRoot
        try {
            $env:OOA_ROM_BUILD_BASH = $bashForTest
            $oracleRomToolsRoot = Join-Path $temporary 'native builder [0]'
            $scripts = Join-Path $oracleRomToolsRoot 'rom-build'
            [void][IO.Directory]::CreateDirectory($scripts)
            [IO.File]::WriteAllText((Join-Path $scripts 'build.sh'), @'
#!/usr/bin/env bash
printf 'fixture stdout\n'
printf 'fixture stderr\n' >&2
exit 7
'@, [Text.UTF8Encoding]::new($false))
            Assert-RomFailure { & $realBuilder $oracleRomToolsRoot $fixturePins } 'exited with code 7'
            $log = [IO.File]::ReadAllText((Join-Path $oracleRomToolsRoot 'build.log'))
            Assert-Rom ($log.Contains('fixture stdout') -and $log.Contains('fixture stderr')) `
                'Native failure lost stdout/stderr or bracketed/apostrophe paths.'
            Assert-Rom ($ErrorActionPreference -eq 'Stop') 'Native build changed the caller error preference.'
            Write-Host 'Native ROM builder failure/logging test passed.'
        }
        finally {
            $oracleRomToolsRoot = $previousToolsRoot
            $env:OOA_ROM_BUILD_BASH = $previousBash
        }
    }
    else { Write-Host 'Native ROM builder fixture skipped (Bash unavailable); resolver tests still run.' }
    $cache = Join-Path $temporary 'cache [0]'
    $resolved = Resolve-OracleRom -Rom $fixture -CacheDirectory $cache
    Assert-Rom ($resolved -ceq $fixture -and $buildCount -eq 0 -and -not [IO.Directory]::Exists($cache)) `
        'Explicit ROM override built or touched the cache.'
    Assert-RomFailure { Resolve-OracleRom -Rom (Join-Path $temporary 'missing.gbc') } 'Unsupported or missing ROM'
    $bad = Join-Path $temporary 'bad.gbc'
    [IO.File]::WriteAllBytes($bad, [byte[]]@(0, 0, 0, 0))
    Assert-RomFailure { Resolve-OracleRom -Rom $bad } 'Unsupported or missing ROM'
    $built = Resolve-OracleRom -CacheDirectory $cache
    Assert-Rom ($buildCount -eq 1 -and (Test-OracleRomImage $built $fixturePins)) 'Cold cache did not publish verified ROM.'
    $reused = Resolve-OracleRom -CacheDirectory $cache -LockTimeoutSeconds 0
    Assert-Rom ($reused -ceq $built -and $buildCount -eq 1) 'Warm cache rebuilt the ROM.'
    [IO.File]::WriteAllBytes($built, [byte[]]@(0, 0, 0, 0))
    $null = Resolve-OracleRom -CacheDirectory $cache
    Assert-Rom ($buildCount -eq 2 -and (Test-OracleRomImage $built $fixturePins)) 'Corrupt cache was reused.'
    [IO.File]::Delete($built)
    $buildMode = 'bad'
    Assert-RomFailure { Resolve-OracleRom -CacheDirectory $cache } 'does not match clean US MD5'
    Assert-Rom (-not [IO.File]::Exists($built)) 'Invalid build was published.'
    $buildMode = 'fail'
    Assert-RomFailure { Resolve-OracleRom -CacheDirectory $cache } 'fixture assembler failure'
    Assert-Rom (-not [IO.File]::Exists($built)) 'Failed build was published.'
    $buildMode = 'good'
    $null = Resolve-OracleRom -CacheDirectory $cache
    Assert-Rom (Test-OracleRomImage $built $fixturePins) 'Failed build left the cache locked.'
    $lockPath = Join-Path (Split-Path $built -Parent) 'build.lock'
    $held = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try { Assert-RomFailure { Resolve-OracleRom -CacheDirectory $cache -LockTimeoutSeconds 1 } 'Timed out waiting' }
    finally { $held.Dispose() }
    $fixturePins.recipeVersion++
    $newPath = Resolve-OracleRom -CacheDirectory $cache
    Assert-Rom ($newPath -cne $built -and (Test-OracleRomImage $newPath $fixturePins)) 'Changed recipe reused the previous cache.'
    Write-Host 'ROM resolver tests passed (override, missing/invalid inputs, cold/warm/corrupt cache, invalid/failed build, lock timeout/recovery, recipe invalidation).'
}
finally {
    $resolvedTemporary = [IO.Path]::GetFullPath($temporary)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedTemporary.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove non-temporary ROM test directory: $resolvedTemporary"
    }
    Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force
}
