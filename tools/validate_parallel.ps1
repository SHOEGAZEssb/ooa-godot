param(
    [ValidateRange(1, 64)]
    [int]$Workers = 8,
    [string]$Godot = 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64_console.exe',
    [ValidatePattern('^Validate[A-Za-z0-9_]+$')]
    [string]$ValidateOnly,
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
$logRoot = Join-Path ([IO.Path]::GetTempPath()) ('ooa-validation-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($logRoot)
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
            -ArgumentList @('--headless', '--path', ('"' + $projectRoot + '"'),
                '--log-file', ('"' + $engineLog + '"'), '--quit-after', '10',
                '--', '--validate', $selection) `
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
    $registered = $null
    foreach ($worker in $processes) {
        $worker.Process.WaitForExit()
        $output = [string](Get-Content -LiteralPath $worker.Out -Raw)
        $pattern = "(?m)^VALIDATION_COMPLETE shard=$($worker.Index)/$Workers executed=(\d+) registered=(\d+)\r?$"
        if ($worker.Process.ExitCode -ne 0 -or $output -notmatch $pattern) {
            $failed = $true
            Write-Host "Worker $($worker.Index) failed (exit $($worker.Process.ExitCode))."
            Write-Host $output
            Get-Content -LiteralPath $worker.Err | Write-Host
            continue
        }
        $executed += [int]$Matches[1]
        $total = [int]$Matches[2]
        if ($null -ne $registered -and $registered -ne $total) {
            throw 'Workers disagree on the registered validation count.'
        }
        $registered = $total
        Write-Host "Worker $($worker.Index): $($Matches[1]) scenarios passed."
    }
    $expected = if ($ValidateOnly) { 1 } else { $registered }
    if ($failed -or $executed -ne $expected) {
        throw "Parallel validation incomplete: $executed/$registered scenarios passed. Logs: $logRoot"
    }
    if ($ValidateOnly) {
        Write-Host ("Validated {0} in {1:N1}s." -f $ValidateOnly, $timer.Elapsed.TotalSeconds)
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
