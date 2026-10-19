param(
    [ValidateRange(1, 64)]
    [int]$Workers = 8,
    [string]$Godot = 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64_console.exe',
    [ValidatePattern('^Validate[A-Za-z0-9_]+$')]
    [string]$ValidateOnly,
    [string]$Rom,
    [switch]$SkipRomValidation,
    [switch]$ContinueOnFailure,
    [switch]$FailOnEngineDiagnostics,
    [switch]$ProfileMemory,
    [string]$TimingProfile,
    [ValidateRange(1, 64)]
    [int]$PartitionIndex = 1,
    [ValidateRange(1, 64)]
    [int]$PartitionCount = 1,
    [string]$ResultPath,
    [ValidateRange(1, 86400)]
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
if ($PartitionIndex -gt $PartitionCount) { throw 'PartitionIndex exceeds PartitionCount.' }
if ($PartitionCount -gt 1 -and ($ValidateOnly -or $SkipRomValidation -or -not $ResultPath)) {
    throw 'Distributed validation requires -ResultPath and cannot use -ValidateOnly or -SkipRomValidation.'
}
$totalWorkers = $Workers * $PartitionCount
if ($ValidateOnly) {
    if ($PSBoundParameters.ContainsKey('Workers') -and $Workers -ne 1) {
        throw '-ValidateOnly cannot be combined with multiple workers.'
    }
    $Workers = 1
    $totalWorkers = 1
}
if ($ResultPath) {
    $ResultPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ResultPath)
    if ([IO.File]::Exists($ResultPath)) { [IO.File]::Delete($ResultPath) }
}
# Windows children inherit the parent's process error mode. Keep native crashes
# noninteractive without changing machine-wide WER settings or hiding failures.
# https://learn.microsoft.com/windows/win32/api/errhandlingapi/nf-errhandlingapi-seterrormode
if (-not ('OracleValidation.ErrorMode' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
namespace OracleValidation {
    public static class ErrorMode {
        [DllImport("kernel32.dll")] public static extern uint GetErrorMode();
        [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint mode);
    }
}
'@
}
if (-not ('OracleValidation.ValidationProcessJob' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'ValidationProcessJob.cs')
}
$projectRoot = Split-Path $PSScriptRoot -Parent
$validationArguments = @()
if (-not $Rom -and -not $SkipRomValidation) {
    . (Join-Path $PSScriptRoot 'Resolve-OracleRom.ps1')
    $Rom = Resolve-OracleRom
}
if ($Rom) {
    $romPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Rom)
    $validationArguments += '"--validation-rom=' + $romPath + '"'
}
if ($SkipRomValidation) {
    $validationArguments += '--skip-rom-validation'
}
if ($ContinueOnFailure) {
    $validationArguments += '--validate-continue-on-failure'
}
if ($ProfileMemory) {
    $validationArguments += '--validate-memory-profile'
}
$logRoot = Join-Path ([IO.Path]::GetTempPath()) ('ooa-validation-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($logRoot)
$defaultTimingProfile = Join-Path $projectRoot '.godot/validation-timings.tsv'
$profileHash = 'round-robin'
if (-not $ValidateOnly) {
    $profile = if ($TimingProfile) {
        $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($TimingProfile)
    } else { $defaultTimingProfile }
    if ($TimingProfile -and -not [IO.File]::Exists($profile)) {
        throw "Timing profile does not exist: $profile"
    }
    if ([IO.File]::Exists($profile)) {
        # All workers consume one immutable snapshot, including when a later
        # run refreshes the default profile from its completion timings.
        $snapshot = Join-Path $logRoot 'timing-profile.tsv'
        [IO.File]::Copy($profile, $snapshot)
        $profileHash = (Get-FileHash -LiteralPath $snapshot -Algorithm SHA256).Hash
        $validationArguments += '"--validate-timing-profile=' + $snapshot + '"'
        Write-Host "Balancing workers from timing profile: $profile"
    }
}
$processes = [Collections.Generic.List[object]]::new()
$timer = [Diagnostics.Stopwatch]::StartNew()
Write-Host "Running $Workers validation workers. Logs: $logRoot"
if ($PartitionCount -gt 1) { Write-Host "Partition $PartitionIndex/$PartitionCount of $totalWorkers global workers." }
Write-Host "No launcher CPU or memory limits; workers inherit process priority."

$previousErrorMode = [OracleValidation.ErrorMode]::GetErrorMode()
$job = $null
try {
    $job = [OracleValidation.ValidationProcessJob]::new()
    # SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX. Preserve existing flags.
    [void][OracleValidation.ErrorMode]::SetErrorMode($previousErrorMode -bor 0x0003)
    for ($index = 1; $index -le $Workers; $index++) {
        $globalIndex = ($PartitionIndex - 1) * $Workers + $index
        $stdout = Join-Path $logRoot "$globalIndex.stdout.log"
        $stderr = Join-Path $logRoot "$globalIndex.stderr.log"
        $engineLog = Join-Path $logRoot "$globalIndex.godot.log"
        $selection = if ($ValidateOnly) { "--validate-only=$ValidateOnly" } else { "--validate-shard=$globalIndex/$totalWorkers" }
        # Deferred draw scenarios await host frames. The runner quits when all
        # assigned scenarios finish; TimeoutSeconds bounds an incomplete run.
        $arguments = (@('--headless', '--path', ('"' + $projectRoot + '"'),
                '--log-file', ('"' + $engineLog + '"'),
                '--', '--validate', $selection) + $validationArguments) -join ' '
        $process = $job.Start($Godot, $arguments, $projectRoot, $stdout, $stderr)
        $processes.Add([pscustomobject]@{ Process = $process; Index = $globalIndex; Out = $stdout; Err = $stderr; EngineLog = $engineLog })
    }

    while (@($processes | Where-Object { -not $_.Process.HasExited }).Count -gt 0) {
        if ($timer.Elapsed.TotalSeconds -gt $TimeoutSeconds) {
            throw "Validation exceeded $TimeoutSeconds seconds. Logs: $logRoot"
        }
        Start-Sleep -Milliseconds 200
    }

    $failed = $false
    $executed = 0
    $skipped = 0
    $failedScenarios = 0
    $registered = $null
    $workerResults = [Collections.Generic.List[object]]::new()
    $timings = [Collections.Generic.Dictionary[string, double]]::new([StringComparer]::Ordinal)
    foreach ($worker in $processes) {
        $worker.Process.WaitForExit()
        if ($FailOnEngineDiagnostics) {
            $logs = @($worker.Out, $worker.Err, $worker.EngineLog) |
                Where-Object { [IO.File]::Exists($_) }
            $engineDiagnostics = Select-String -LiteralPath $logs -Pattern '^\s*(ERROR|WARNING):'
            if ($engineDiagnostics) {
                $failed = $true
                Write-Host "Worker $($worker.Index): Godot emitted engine errors or warnings."
                $engineDiagnostics | ForEach-Object { Write-Host "$($_.Path):$($_.LineNumber): $($_.Line)" }
            }
        }
        $output = [string](Get-Content -LiteralPath $worker.Out -Raw)
        $pattern = "(?m)^VALIDATION_COMPLETE shard=$($worker.Index)/$totalWorkers executed=(\d+) skipped=(\d+) registered=(\d+)(?: failed=(\d+))?\r?$"
        if ($output -notmatch $pattern) {
            $failed = $true
            Write-Host "Worker $($worker.Index) failed (exit $($worker.Process.ExitCode))."
            Write-Host $output
            Get-Content -LiteralPath $worker.Err | Write-Host
            continue
        }
        $workerExecuted = [int]$Matches[1]
        $workerSkipped = [int]$Matches[2]
        $total = [int]$Matches[3]
        $workerFailed = if ($Matches[4]) { [int]$Matches[4] } else { 0 }
        $failedScenarios += $workerFailed
        $executed += $workerExecuted
        $skipped += $workerSkipped
        if ($worker.Process.ExitCode -ne 0 -or $workerFailed -gt 0) {
            $failed = $true
            Write-Host "Worker $($worker.Index): $workerFailed failed scenarios (exit $($worker.Process.ExitCode))."
            Get-Content -LiteralPath $worker.Err | Write-Host
        }
        if ($workerSkipped -gt 0 -and -not $SkipRomValidation) {
            $failed = $true
            Write-Host "Worker $($worker.Index) skipped scenarios without -SkipRomValidation."
        }
        if ($null -ne $registered -and $registered -ne $total) {
            throw 'Workers disagree on the registered validation count.'
        }
        $registered = $total
        $workerNames = [Collections.Generic.List[string]]::new()
        foreach ($line in ($output -split '\r?\n')) {
            if ($line.StartsWith('VALIDATION_SKIPPED ')) { Write-Host $line }
            if ($line -match '^VALIDATION_TIMING name=(\S+) setup_ms=\S+ total_ms=(\S+)$') {
                $milliseconds = [double]::Parse($Matches[2], [Globalization.CultureInfo]::InvariantCulture)
                if ([double]::IsNaN($milliseconds) -or [double]::IsInfinity($milliseconds) -or $milliseconds -lt 0 -or $timings.ContainsKey($Matches[1])) {
                    throw "Invalid or duplicate validation timing: $line"
                }
                $timings.Add($Matches[1], $milliseconds)
                $workerNames.Add($Matches[1])
            }
        }
        $workerResults.Add([pscustomobject]@{
            Index = $worker.Index; Executed = $workerExecuted; Skipped = $workerSkipped
            Failed = $workerFailed; ScenarioNames = @($workerNames.ToArray())
        })
        Write-Host "Worker $($worker.Index): $workerExecuted scenarios passed, $workerSkipped skipped."
    }
    $expected = if ($ValidateOnly) { 1 } elseif ($PartitionCount -gt 1) { $timings.Count } else { $registered }
    if ($PartitionCount -eq 1 -and -not $ValidateOnly -and -not $SkipRomValidation -and -not $TimingProfile -and
        $executed + $failedScenarios -eq $registered -and $timings.Count -eq $registered) {
        $lines = [Collections.Generic.List[string]]::new()
        $lines.Add("name`ttotal_ms")
        foreach ($name in ($timings.Keys | Sort-Object)) {
            $lines.Add($name + "`t" + $timings[$name].ToString('F3', [Globalization.CultureInfo]::InvariantCulture))
        }
        [void][IO.Directory]::CreateDirectory((Split-Path $defaultTimingProfile -Parent))
        [IO.File]::WriteAllLines($defaultTimingProfile, $lines, [Text.UTF8Encoding]::new($false))
    }
    if ($failed -or $executed + $skipped + $failedScenarios -ne $expected) {
        Write-Host ("Validation elapsed: {0:N1}s with {1} workers; {2} passed, {3} skipped, {4} failed, {5} registered." -f $timer.Elapsed.TotalSeconds, $Workers, $executed, $skipped, $failedScenarios, $registered)
        throw "Parallel validation incomplete: $executed passed, $skipped skipped, $failedScenarios failed, $registered registered. Logs: $logRoot"
    }
    if ($ResultPath) {
        if ($ValidateOnly -or $skipped -ne 0 -or $timings.Count -ne $executed) {
            throw 'Result reports require successful, timed scenarios without skips or a focused filter.'
        }
        [void][IO.Directory]::CreateDirectory((Split-Path $ResultPath -Parent))
        $report = [ordered]@{
            SchemaVersion = 1; PartitionIndex = $PartitionIndex; PartitionCount = $PartitionCount
            WorkersPerPartition = $Workers; TotalWorkers = $totalWorkers; Registered = $registered
            ProfileHash = $profileHash; ElapsedSeconds = $timer.Elapsed.TotalSeconds
            Workers = @($workerResults.ToArray())
            Timings = @($timings.Keys | Sort-Object | ForEach-Object {
                [ordered]@{ Name = $_; Milliseconds = $timings[$_] }
            })
        }
        [IO.File]::WriteAllText($ResultPath, ($report | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    }
    if ($PartitionCount -gt 1) {
        Write-Host ("Validated partition {0}/{1}: {2} scenarios passed, 0 skipped in {3:N1}s with {4} workers; full coverage requires merging every partition." -f $PartitionIndex, $PartitionCount, $executed, $timer.Elapsed.TotalSeconds, $Workers)
    } elseif ($ValidateOnly) {
        if ($skipped -gt 0) {
            Write-Host "Skipped $ValidateOnly (ROM required)."
        } else {
            Write-Host ("Validated {0} in {1:N1}s." -f $ValidateOnly, $timer.Elapsed.TotalSeconds)
        }
    } elseif ($skipped -gt 0) {
        Write-Host ("Validation complete: {0} passed, {1} skipped, {2} registered in {3:N1}s with {4} workers." -f $executed, $skipped, $registered, $timer.Elapsed.TotalSeconds, $Workers)
    } else {
        Write-Host ("Validated all {0} scenarios in {1:N1}s with {2} workers." -f $executed, $timer.Elapsed.TotalSeconds, $Workers)
    }
}
finally {
    [void][OracleValidation.ErrorMode]::SetErrorMode($previousErrorMode)
    if ($null -ne $job) {
        try { Write-Host ('Validation peak committed memory: {0:N1} MiB.' -f ($job.PeakMemoryBytes / 1MB)) }
        finally { $job.Dispose() } # Terminates all remaining workers AND descendants.
    }
    foreach ($worker in $processes) {
        if (-not $worker.Process.HasExited) {
            $worker.Process.WaitForExit()
        }
        $worker.Process.Dispose()
    }
}
