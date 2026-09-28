param(
    [string]$Movie = (Join-Path $PSScriptRoot 'fidelity/dependencies/scorpianman42-lozoracleofages.bk2'),
    [string]$BizHawk = (Join-Path $PSScriptRoot 'fidelity/dependencies/bizhawk-1.11.5'),
    [string]$Rom,
    [string]$Disassembly,
    [string]$Godot,
    [string]$Output,
    [int]$MaxFrames = 0,
    [int]$MaxUpdates = -1,
    [int]$TimeoutSeconds = 600,
    [ValidateRange(1,1024)][int]$BatchSize = 1,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$replayArguments = @((Join-Path $PSScriptRoot 'fidelity/run_tas.py'), '--movie', $Movie, '--bizhawk', $BizHawk, '--timeout', "$TimeoutSeconds", '--batch-size', "$BatchSize")
foreach ($pair in @(@('--rom', $Rom), @('--disassembly', $Disassembly), @('--godot', $Godot), @('--output', $Output))) {
    if ($pair[1]) { $replayArguments += $pair }
}
if ($MaxFrames -gt 0) { $replayArguments += @('--max-frames', "$MaxFrames") }
if ($MaxUpdates -ge 0) { $replayArguments += @('--max-updates', "$MaxUpdates") }
if ($SkipBuild) { $replayArguments += '--skip-build' }
if (-not (Get-Command python -ErrorAction SilentlyContinue)) { throw 'Python is required to run the TAS comparison.' }
& python @replayArguments
exit $LASTEXITCODE
