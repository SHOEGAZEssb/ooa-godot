param(
    [Parameter(Mandatory)][string]$ImportScript,
    [Parameter(Mandatory)][string]$Disassembly,
    [Parameter(Mandatory)][string]$Rom,
    [Parameter(Mandatory)][string[]]$OutputDirectories,
    [Parameter(Mandatory)][string]$LogDirectory,
    [ValidateRange(1, 86400)][int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$processes = [Collections.Generic.List[object]]::new()
$timer = [Diagnostics.Stopwatch]::StartNew()
$shell = (Get-Process -Id $PID).Path
[void][IO.Directory]::CreateDirectory($LogDirectory)

# Encode a PowerShell command with literal arguments; paths may contain spaces,
# apostrophes, brackets, or command syntax. No argument is evaluated as code.
function ConvertTo-WorkerLiteral([string]$value) {
    return "'" + $value.Replace("'", "''") + "'"
}

function Save-ImportWorkerLogs($worker) {
    if ($worker.Logged) { return }
    $worker.Process.WaitForExit()
    [IO.File]::WriteAllText($worker.Out, $worker.OutputTask.GetAwaiter().GetResult())
    [IO.File]::WriteAllText($worker.Err, $worker.ErrorTask.GetAwaiter().GetResult())
    $worker.Logged = $true
}

try {
    for ($index = 0; $index -lt $OutputDirectories.Count; $index++) {
        $command = @'
$ErrorActionPreference = 'Stop'
$timer = [Diagnostics.Stopwatch]::StartNew()
try {
    & IMPORT_SCRIPT -Disassembly DISASSEMBLY_PATH -Rom ROM_PATH -OutputDirectory OUTPUT_PATH -SkipBuild
    Write-Host ('Import worker completed in {0:N2}s.' -f $timer.Elapsed.TotalSeconds)
    exit 0
}
catch {
    [Console]::Error.WriteLine($_.ToString() + [Environment]::NewLine + $_.ScriptStackTrace)
    exit 1
}
'@
        # Replace tokens in one pass so a token inside a path stays literal.
        $literals = @{
            IMPORT_SCRIPT = ConvertTo-WorkerLiteral $ImportScript
            DISASSEMBLY_PATH = ConvertTo-WorkerLiteral $Disassembly
            ROM_PATH = ConvertTo-WorkerLiteral $Rom
            OUTPUT_PATH = ConvertTo-WorkerLiteral $OutputDirectories[$index]
        }
        $command = [regex]::Replace($command,
            'IMPORT_SCRIPT|DISASSEMBLY_PATH|ROM_PATH|OUTPUT_PATH',
            [Text.RegularExpressions.MatchEvaluator]{ param($match) $literals[$match.Value] })
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
        $stdout = Join-Path $LogDirectory "$index.stdout.log"
        $stderr = Join-Path $LogDirectory "$index.stderr.log"
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = $shell
        $start.WorkingDirectory = (Get-Location).Path
        $start.Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand $encoded"
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $start
        if (-not $process.Start()) { throw "Could not start import worker $index." }
        # Drain both pipes asynchronously so verbose failures cannot deadlock.
        # Direct file writes also keep bracket characters in log paths literal.
        $processes.Add([pscustomobject]@{
            Process = $process; Index = $index; Out = $stdout; Err = $stderr
            OutputTask = $process.StandardOutput.ReadToEndAsync()
            ErrorTask = $process.StandardError.ReadToEndAsync()
            Logged = $false
        })
    }

    while ($true) {
        $running = $false
        foreach ($worker in $processes) {
            if (-not $worker.Process.HasExited) {
                $running = $true
                continue
            }
            Save-ImportWorkerLogs $worker
            if ($worker.Process.ExitCode -ne 0) {
                $errorText = [IO.File]::ReadAllText($worker.Err)
                throw "Import worker $($worker.Index) failed (exit $($worker.Process.ExitCode)):`n$errorText`nLogs: $LogDirectory"
            }
        }
        if (-not $running) { break }
        if ($timer.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
            throw "Import workers exceeded $TimeoutSeconds seconds. Logs: $LogDirectory"
        }
        Start-Sleep -Milliseconds 100
    }

    foreach ($worker in $processes) {
        Write-Host ([IO.File]::ReadAllText($worker.Out).TrimEnd())
    }
}
finally {
    foreach ($worker in $processes) {
        if (-not $worker.Process.HasExited) {
            # Stop the source-host child as well as its PowerShell worker.
            & taskkill.exe /PID $worker.Process.Id /T /F 2>&1 | Out-Null
            $worker.Process.WaitForExit()
        }
        Save-ImportWorkerLogs $worker
        $worker.Process.Dispose()
    }
}
