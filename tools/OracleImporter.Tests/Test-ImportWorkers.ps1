# Focused orchestration regression; run independently of the full ROM imports.
$ErrorActionPreference = 'Stop'
$workerScript = Join-Path $PSScriptRoot '..\Invoke-ImportWorkers.ps1'
$temporary = Join-Path ([IO.Path]::GetTempPath()) `
    "ooa-worker-tests ' [0] $([Guid]::NewGuid().ToString('N'))"
[void][IO.Directory]::CreateDirectory($temporary)
$succeeded = $false

function Assert-WorkerTest([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

try {
    $fixture = Join-Path $temporary 'fixture.ps1'
    $fixtureSource = @'
param([string]$Disassembly, [string]$Rom, [string]$OutputDirectory, [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
if (-not $SkipBuild) { throw 'Worker attempted to build.' }
[void][IO.Directory]::CreateDirectory($OutputDirectory)
@{ Disassembly = $Disassembly; Rom = $Rom } |
    Export-Clixml -LiteralPath (Join-Path $OutputDirectory 'arguments.xml')
$mode = [IO.File]::ReadAllText((Join-Path $OutputDirectory 'mode.txt'))
if ($mode -eq 'fail') { throw 'fixture source-aware failure: fixture.s:1:1' }
if ($mode -eq 'timeout') {
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes('Start-Sleep -Seconds 60'))
    $child = Start-Process -FilePath (Get-Process -Id $PID).Path `
        -ArgumentList @('-NoProfile', '-NonInteractive', '-EncodedCommand', $encoded) `
        -WindowStyle Hidden -PassThru
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'child.pid'), [string]$child.Id)
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'worker.pid'), [string]$PID)
    Start-Sleep -Seconds 60
}
if ($mode -eq 'parallel') {
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'ready.txt'), '')
    $root = Split-Path $OutputDirectory -Parent
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ([IO.Directory]::GetFiles($root, 'ready.txt', 'AllDirectories').Count -ne 2) {
        if ($timer.Elapsed.TotalSeconds -gt 10) { throw 'Workers did not overlap.' }
        Start-Sleep -Milliseconds 50
    }
}
[IO.File]::AppendAllText((Join-Path $OutputDirectory 'completed.txt'), "pass`n")
'@
    [IO.File]::WriteAllText($fixture, $fixtureSource)
    $literalPath = 'source '' [0] $(throw "path was evaluated") IMPORT_SCRIPT'
    $parameters = @{
        ImportScript = $fixture
        Disassembly = $literalPath
        Rom = $literalPath
    }

    $roots = @((Join-Path $temporary 'one'), (Join-Path $temporary 'two'))
    foreach ($root in $roots) {
        [void][IO.Directory]::CreateDirectory($root)
        [IO.File]::WriteAllText((Join-Path $root 'mode.txt'), 'parallel')
    }
    & $workerScript @parameters -OutputDirectories $roots `
        -LogDirectory (Join-Path $temporary 'parallel-logs') -TimeoutSeconds 20 6>$null
    foreach ($root in $roots) {
        Assert-WorkerTest ([IO.File]::Exists((Join-Path $root 'completed.txt'))) `
            'A concurrent worker did not complete.'
        $arguments = Import-Clixml -LiteralPath (Join-Path $root 'arguments.xml')
        Assert-WorkerTest ($arguments.Disassembly -ceq $literalPath -and $arguments.Rom -ceq $literalPath) `
            'Worker arguments were not preserved literally.'
    }

    $serialRoot = Join-Path $temporary 'serial'
    [void][IO.Directory]::CreateDirectory($serialRoot)
    [IO.File]::WriteAllText((Join-Path $serialRoot 'mode.txt'), 'serial')
    foreach ($pass in 1..2) {
        & $workerScript @parameters -OutputDirectories @($serialRoot) `
            -LogDirectory (Join-Path $temporary "serial-$pass-logs") 6>$null
    }
    Assert-WorkerTest ([IO.File]::ReadAllLines((Join-Path $serialRoot 'completed.txt')).Count -eq 2) `
        'Consecutive workers did not reuse the same output directory.'

    [IO.File]::WriteAllText((Join-Path $serialRoot 'mode.txt'), 'fail')
    $rejected = $false
    try {
        & $workerScript @parameters -OutputDirectories @($serialRoot) `
            -LogDirectory (Join-Path $temporary 'failure-logs') 6>$null
    }
    catch { $rejected = $_.ToString().Contains('fixture source-aware failure: fixture.s:1:1') }
    Assert-WorkerTest $rejected 'Worker failure lost its exit status or source diagnostic.'

    [IO.File]::WriteAllText((Join-Path $serialRoot 'mode.txt'), 'timeout')
    $rejected = $false
    try {
        & $workerScript @parameters -OutputDirectories @($serialRoot) `
            -LogDirectory (Join-Path $temporary 'timeout-logs') -TimeoutSeconds 3 6>$null
    }
    catch { $rejected = $_.ToString().Contains('exceeded 3 seconds') }
    Assert-WorkerTest $rejected 'Worker timeout did not fail verification.'
    foreach ($name in @('worker.pid', 'child.pid')) {
        $processId = [int][IO.File]::ReadAllText((Join-Path $serialRoot $name))
        Assert-WorkerTest ($null -eq (Get-Process -Id $processId -ErrorAction SilentlyContinue)) `
            "Timeout left $name process $processId running."
    }
    Write-Host 'Import worker tests passed (concurrency, literal paths, consecutive imports, failure, process-tree timeout).'
    $succeeded = $true
}
finally {
    $resolved = [IO.Path]::GetFullPath($temporary)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove non-temporary worker test path: $resolved"
    }
    if ($succeeded) { [IO.Directory]::Delete($resolved, $true) }
    else { Write-Host "Worker test diagnostics retained at $resolved" }
}
