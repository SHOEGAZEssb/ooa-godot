param(
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][ValidateRange(1, 64)][int]$PartitionCount,
    [Parameter(Mandatory)][ValidateRange(1, 64)][int]$WorkersPerPartition,
    [string]$TimingProfile = (Join-Path $PSScriptRoot '../.godot/validation-timings.tsv')
)

$ErrorActionPreference = 'Stop'
$files = @(Get-ChildItem -LiteralPath $ResultsDirectory -File -Filter '*.json')
if ($files.Count -ne $PartitionCount) { throw "Expected $PartitionCount partition reports, found $($files.Count)." }
$partitions = [Collections.Generic.HashSet[int]]::new()
$workers = [Collections.Generic.HashSet[int]]::new()
$names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$timings = [Collections.Generic.Dictionary[string, double]]::new([StringComparer]::Ordinal)
$registered = $null
$profileHash = $null
$totalWorkers = $PartitionCount * $WorkersPerPartition
$longest = 0.0
foreach ($file in $files) {
    $report = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    if ($report.SchemaVersion -ne 1 -or $report.PartitionCount -ne $PartitionCount -or
        $report.WorkersPerPartition -ne $WorkersPerPartition -or $report.TotalWorkers -ne $totalWorkers -or
        $report.PartitionIndex -lt 1 -or $report.PartitionIndex -gt $PartitionCount -or
        -not $partitions.Add($report.PartitionIndex)) {
        throw "Invalid or duplicate partition metadata: $($file.Name)"
    }
    if ($report.Registered -lt 1 -or ($null -ne $registered -and $registered -ne $report.Registered)) {
        throw 'Partitions disagree on the registered scenario count.'
    }
    $registered = $report.Registered
    if (-not $report.ProfileHash -or ($null -ne $profileHash -and $profileHash -cne $report.ProfileHash)) {
        throw 'Partitions used different scheduling profiles.'
    }
    $profileHash = $report.ProfileHash
    if (@($report.Workers).Count -ne $WorkersPerPartition) { throw 'Incomplete worker list.' }
    $partitionExecuted = 0
    foreach ($worker in $report.Workers) {
        $firstIndex = ($report.PartitionIndex - 1) * $WorkersPerPartition + 1
        if ($worker.Index -lt $firstIndex -or $worker.Index -ge $firstIndex + $WorkersPerPartition -or
            -not $workers.Add($worker.Index) -or $worker.Skipped -ne 0 -or $worker.Failed -ne 0 -or
            $null -eq $worker.Executed -or $worker.Executed -lt 0 -or
            @($worker.ScenarioNames).Count -ne $worker.Executed) {
            throw 'Invalid worker, incomplete scenario list, skip, or failure.'
        }
        foreach ($name in $worker.ScenarioNames) {
            if ($name -cnotmatch '^Validate[A-Za-z0-9_]+$' -or -not $names.Add($name)) {
                throw "Invalid or duplicate scenario: $name"
            }
        }
        $partitionExecuted += $worker.Executed
    }
    if (@($report.Timings).Count -ne $partitionExecuted) { throw 'Incomplete partition timings.' }
    foreach ($timing in $report.Timings) {
        $milliseconds = [double]$timing.Milliseconds
        if (-not $names.Contains($timing.Name) -or $null -eq $timing.Milliseconds -or
            [double]::IsNaN($milliseconds) -or [double]::IsInfinity($milliseconds) -or $milliseconds -lt 0 -or
            $timings.ContainsKey($timing.Name)) { throw "Invalid or duplicate scenario timing: $($timing.Name)" }
        $timings.Add($timing.Name, $milliseconds)
    }
    $longest = [math]::Max($longest, $report.ElapsedSeconds)
}
if ($workers.Count -ne $totalWorkers -or $names.Count -ne $registered -or $timings.Count -ne $registered) {
    throw "Incomplete distributed validation: $($names.Count) unique scenarios, $registered registered; $($workers.Count)/$totalWorkers workers."
}
$lines = [Collections.Generic.List[string]]::new()
$lines.Add("name`ttotal_ms")
foreach ($name in ($timings.Keys | Sort-Object)) {
    $lines.Add($name + "`t" + $timings[$name].ToString('F3', [Globalization.CultureInfo]::InvariantCulture))
}
$TimingProfile = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($TimingProfile)
[void][IO.Directory]::CreateDirectory((Split-Path $TimingProfile -Parent))
[IO.File]::WriteAllLines($TimingProfile, $lines, [Text.UTF8Encoding]::new($false))
Write-Host ("Validated all {0} scenarios exactly once: 0 skipped, 0 failed across {1} runners / {2} workers; longest partition {3:N1}s." -f $registered, $PartitionCount, $totalWorkers, $longest)
