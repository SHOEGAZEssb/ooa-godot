param(
    [ValidateRange(1, 64)]
    [int]$Workers = 8,
    [string]$Godot = 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64_console.exe',
    [ValidatePattern('^Validate[A-Za-z0-9_]+$')]
    [string]$ValidateOnly,
    [string]$Rom,
    [switch]$SkipRomValidation,
    [switch]$ContinueOnFailure,
    [string]$TimingProfile,
    [ValidateRange(1, 86400)]
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
if ($ValidateOnly) {
    if ($PSBoundParameters.ContainsKey('Workers') -and $Workers -ne 1) {
        throw '-ValidateOnly cannot be combined with multiple workers.'
    }
    $Workers = 1
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
$projectRoot = Split-Path $PSScriptRoot -Parent
$validationArguments = @()
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
$logRoot = Join-Path ([IO.Path]::GetTempPath()) ('ooa-validation-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($logRoot)
$defaultTimingProfile = Join-Path $projectRoot '.godot/validation-timings.tsv'
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
        $validationArguments += '"--validate-timing-profile=' + $snapshot + '"'
        Write-Host "Balancing workers from timing profile: $profile"
    }
}
$processes = [Collections.Generic.List[object]]::new()
$timer = [Diagnostics.Stopwatch]::StartNew()
Write-Host "Running $Workers validation workers. Logs: $logRoot"

$previousErrorMode = [OracleValidation.ErrorMode]::GetErrorMode()
try {
    # SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX. Preserve existing flags.
    [void][OracleValidation.ErrorMode]::SetErrorMode($previousErrorMode -bor 0x0003)
    for ($index = 1; $index -le $Workers; $index++) {
        $stdout = Join-Path $logRoot "$index.stdout.log"
        $stderr = Join-Path $logRoot "$index.stderr.log"
        $engineLog = Join-Path $logRoot "$index.godot.log"
        $selection = if ($ValidateOnly) { "--validate-only=$ValidateOnly" } else { "--validate-shard=$index/$Workers" }
        $process = Start-Process -FilePath $Godot -WorkingDirectory $projectRoot `
            -ArgumentList (@('--headless', '--path', ('"' + $projectRoot + '"'),
                '--log-file', ('"' + $engineLog + '"'), '--quit-after', '10',
                '--', '--validate', $selection) + $validationArguments) `
            -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        # Retain the native handle so Windows PowerShell can read ExitCode even
        # when the worker exits before we reach WaitForExit.
        $null = $process.Handle
        $processes.Add([pscustomobject]@{ Process = $process; Index = $index; Out = $stdout; Err = $stderr })
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
    $timings = [Collections.Generic.Dictionary[string, double]]::new([StringComparer]::Ordinal)
    foreach ($worker in $processes) {
        $worker.Process.WaitForExit()
        $output = [string](Get-Content -LiteralPath $worker.Out -Raw)
        $pattern = "(?m)^VALIDATION_COMPLETE shard=$($worker.Index)/$Workers executed=(\d+) skipped=(\d+) registered=(\d+)(?: failed=(\d+))?\r?$"
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
        foreach ($line in ($output -split '\r?\n')) {
            if ($line.StartsWith('VALIDATION_SKIPPED ')) { Write-Host $line }
            if ($line -match '^VALIDATION_TIMING name=(\S+) setup_ms=\S+ total_ms=(\S+)$') {
                $milliseconds = [double]::Parse($Matches[2], [Globalization.CultureInfo]::InvariantCulture)
                if ([double]::IsNaN($milliseconds) -or [double]::IsInfinity($milliseconds) -or $milliseconds -lt 0 -or $timings.ContainsKey($Matches[1])) {
                    throw "Invalid or duplicate validation timing: $line"
                }
                $timings.Add($Matches[1], $milliseconds)
            }
        }
        Write-Host "Worker $($worker.Index): $workerExecuted scenarios passed, $workerSkipped skipped."
    }
    $expected = if ($ValidateOnly) { 1 } else { $registered }
    if (-not $ValidateOnly -and -not $SkipRomValidation -and -not $TimingProfile -and
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
    if ($ValidateOnly) {
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
    foreach ($worker in $processes) {
        if (-not $worker.Process.HasExited) {
            $worker.Process.Kill()
            $worker.Process.WaitForExit()
        }
        $worker.Process.Dispose()
    }
}
