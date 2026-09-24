param(
    [ValidateRange(1, 64)]
    [int]$Workers = 8,
    [string]$Godot = 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64_console.exe',
    [ValidateRange(1, 86400)]
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$logRoot = Join-Path ([IO.Path]::GetTempPath()) ('ooa-validation-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($logRoot)
$processes = [Collections.Generic.List[object]]::new()
$timer = [Diagnostics.Stopwatch]::StartNew()
Write-Host "Running $Workers validation workers. Logs: $logRoot"

try {
    for ($index = 1; $index -le $Workers; $index++) {
        $stdout = Join-Path $logRoot "$index.stdout.log"
        $stderr = Join-Path $logRoot "$index.stderr.log"
        $engineLog = Join-Path $logRoot "$index.godot.log"
        $process = Start-Process -FilePath $Godot -WorkingDirectory $projectRoot `
            -ArgumentList @('--headless', '--path', ('"' + $projectRoot + '"'),
                '--log-file', ('"' + $engineLog + '"'), '--quit-after', '10',
                '--', '--validate', "--validate-shard=$index/$Workers") `
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
        $output = Get-Content -LiteralPath $worker.Out -Raw
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
    if ($failed -or $executed -ne $registered) {
        throw "Parallel validation incomplete: $executed/$registered scenarios passed. Logs: $logRoot"
    }
    Write-Host ("Validated all {0} scenarios in {1:N1}s with {2} workers." -f $executed, $timer.Elapsed.TotalSeconds, $Workers)
}
finally {
    foreach ($worker in $processes) {
        if (-not $worker.Process.HasExited) {
            $worker.Process.Kill()
            $worker.Process.WaitForExit()
        }
        $worker.Process.Dispose()
    }
}
