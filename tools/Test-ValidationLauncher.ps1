# Windows launcher regression: no ROM/game build required.
$ErrorActionPreference = 'Stop'
$launcher = Join-Path $PSScriptRoot 'validate_parallel.ps1'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('ooa-validation-launcher-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temporary)
$fixture = Join-Path $temporary 'fixture.exe'
$timingProfile = Join-Path $temporary 'timing profile [fixture].tsv'
$childPidFile = Join-Path $temporary 'child.pid'
# Explicit fixture input keeps this launcher test independent of ROM builds.
$fixtureParameters = @{ Godot = $fixture; Rom = (Join-Path $temporary 'reference ROM [US].gbc') }
try {
    Add-Type -OutputAssembly $fixture -OutputType ConsoleApplication -TypeDefinition @'
using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
public static class ValidationLauncherFixture {
    [DllImport("kernel32.dll")] public static extern uint GetErrorMode();
    [DllImport("kernel32.dll")] static extern bool IsProcessInJob(IntPtr process, IntPtr job, out bool result);
    [DllImport("kernel32.dll")] static extern bool QueryInformationJobObject(IntPtr job, int kind, byte[] data, int size, IntPtr returned);
    public static int Main(string[] args) {
        // Check inheritance before deliberately crashing; a broken launcher
        // must fail this test without displaying a real crash dialog.
        if ((GetErrorMode() & 3) != 3) return 91;
        bool inJob;
        if (!IsProcessInJob(Process.GetCurrentProcess().Handle, IntPtr.Zero, out inJob) || !inJob) return 97;
        if (Array.IndexOf(args, "--wait-child") >= 0) {
            File.WriteAllText(Environment.GetEnvironmentVariable("OOA_TEST_VALIDATION_CHILD_PID"), Process.GetCurrentProcess().Id.ToString());
            Thread.Sleep(Timeout.Infinite);
        }
        int log = Array.IndexOf(args, "--log-file");
        if (log < 0 || log + 1 == args.Length) return 92;
        using (File.Open(args[log + 1], FileMode.CreateNew)) { }
        string shard = "1/1";
        string selected = null;
        string rom = null;
        bool skipRom = false;
        bool continueOnFailure = false;
        bool profileMemory = false;
        string timingProfile = null;
        foreach (string arg in args) {
            if (arg.StartsWith("--validate-shard=")) shard = arg.Substring(17);
            if (arg.StartsWith("--validate-only=")) selected = arg.Substring(16);
            if (arg.StartsWith("--validation-rom=")) rom = arg.Substring(17);
            if (arg == "--skip-rom-validation") skipRom = true;
            if (arg == "--validate-continue-on-failure") continueOnFailure = true;
            if (arg == "--validate-memory-profile") profileMemory = true;
            const string timingPrefix = "--validate-timing-profile=";
            if (arg.StartsWith(timingPrefix)) timingProfile = arg.Substring(timingPrefix.Length);
        }
        string expectedProfile = Environment.GetEnvironmentVariable("OOA_TEST_VALIDATION_TIMING_PROFILE");
        if (expectedProfile != null && (timingProfile == null ||
            Path.GetFileName(timingProfile) != "timing-profile.tsv" ||
            File.ReadAllText(timingProfile) != expectedProfile)) return 96;
        if (selected == "ValidateRomArgument" && (rom == null || !Path.IsPathRooted(rom) ||
            Path.GetFileName(rom) != "reference ROM [US].gbc")) return 93;
        if (selected == "ValidateSkipRomArgument" && !skipRom) return 94;
        if (selected == "ValidateCrash") {
            Console.Error.WriteLine("fixture native failure");
            Environment.FailFast("fixture deliberate crash");
        }
        if (selected == "ValidateMissingMarker") return 0;
        if (selected == "ValidateMemoryProfileArgument" && !profileMemory) return 104;
        if (selected == "ValidateUnrestrictedResources") {
            // JOB_OBJECT_EXTENDED_LIMIT_INFORMATION, native SIZE_T layout.
            byte[] limits = new byte[IntPtr.Size == 8 ? 144 : 112];
            byte[] cpu = new byte[8];
            if (!QueryInformationJobObject(IntPtr.Zero, 9, limits, limits.Length, IntPtr.Zero) ||
                !QueryInformationJobObject(IntPtr.Zero, 15, cpu, cpu.Length, IntPtr.Zero)) return 99;
            ulong memory = IntPtr.Size == 8 ? BitConverter.ToUInt64(limits, 120) : BitConverter.ToUInt32(limits, 100);
            // Only KILL_ON_JOB_CLOSE remains: no memory/process CPU limits or forced priority.
            if (BitConverter.ToUInt32(limits, 16) != 0x2000 || memory != 0 ||
                BitConverter.ToUInt32(cpu, 0) != 0 || BitConverter.ToUInt32(cpu, 4) != 0) return 100;
        }
        if (selected == "ValidateTimeoutChild" || selected == "ValidateAbandonedChild") {
            var info = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--wait-child");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            using (var child = Process.Start(info)) {
                string pid = Environment.GetEnvironmentVariable("OOA_TEST_VALIDATION_CHILD_PID");
                for (int i = 0; i < 100 && !File.Exists(pid); i++) Thread.Sleep(10);
                if (!File.Exists(pid)) return 103;
                if (selected == "ValidateTimeoutChild") Thread.Sleep(Timeout.Infinite);
            }
        }
        if (selected == "ValidateStdoutWarning") Console.WriteLine("  WARNING: fixture stdout warning");
        if (selected == "ValidateStderrError") Console.Error.WriteLine("ERROR: fixture stderr error");
        if (selected == "ValidateEngineLogWarning") File.WriteAllText(args[log + 1], "WARNING: fixture engine-log warning\n");
        int workers = int.Parse(shard.Split('/')[1]);
        int executed = selected == null ? 8 / workers : 1;
        int skipped = 0;
        int failed = 0;
        if (selected == "ValidateContinueArgument" && !continueOnFailure) return 95;
        if (selected == "ValidateReportedFailure" ||
            (continueOnFailure && selected == null && shard.StartsWith("1/"))) {
            executed--;
            failed++;
        }
        if ((skipRom && (selected == "ValidateSkipRomArgument" ||
                (selected == null && shard.StartsWith("1/")))) ||
            selected == "ValidateUnexpectedSkip") {
            executed--;
            skipped++;
            Console.WriteLine("VALIDATION_SKIPPED name=ValidateSkipRomArgument reason=rom-required");
        }
        if (selected == "ValidateIncompleteSkip") executed = 0;
        if (Environment.GetEnvironmentVariable("OOA_TEST_VALIDATION_EMIT_TIMINGS") == "1") {
            int first = int.Parse(shard.Split('/')[0]) - 1;
            for (int ordinal = first; ordinal < 8; ordinal += workers)
                Console.WriteLine("VALIDATION_TIMING name=ValidateFixture" + ordinal + " setup_ms=0 total_ms=1.500");
        }
        Console.WriteLine("VALIDATION_COMPLETE shard=" + shard + " executed=" + executed +
            " skipped=" + skipped + " registered=8 failed=" + failed);
        return failed == 0 ? 0 : 1;
    }
}
'@
    # Load the launcher's interop type and exercise a successful focused run.
    & $launcher @fixtureParameters -ValidateOnly ValidateFixture -FailOnEngineDiagnostics -TimeoutSeconds 15
    & $launcher @fixtureParameters -ValidateOnly ValidateUnrestrictedResources -TimeoutSeconds 15
    & $launcher @fixtureParameters -ValidateOnly ValidateMemoryProfileArgument -ProfileMemory -TimeoutSeconds 15
    $previousPidPath = [Environment]::GetEnvironmentVariable('OOA_TEST_VALIDATION_CHILD_PID')
    try {
        [Environment]::SetEnvironmentVariable('OOA_TEST_VALIDATION_CHILD_PID', $childPidFile)
        foreach ($scenario in @('ValidateTimeoutChild', 'ValidateAbandonedChild')) {
            if ([IO.File]::Exists($childPidFile)) { [IO.File]::Delete($childPidFile) }
            $failure = $null
            try { & $launcher @fixtureParameters -ValidateOnly $scenario -TimeoutSeconds 2 }
            catch { $failure = $_.ToString() }
            if (($scenario -eq 'ValidateTimeoutChild' -and ($null -eq $failure -or -not $failure.Contains('exceeded 2 seconds'))) -or
                ($scenario -eq 'ValidateAbandonedChild' -and $null -ne $failure)) {
                throw "Unexpected descendant cleanup result for ${scenario}: $failure"
            }
            if (-not [IO.File]::Exists($childPidFile)) { throw "$scenario did not start its child." }
            $child = Get-Process -Id ([int][IO.File]::ReadAllText($childPidFile)) -ErrorAction SilentlyContinue
            if ($null -ne $child) {
                try { if (-not $child.WaitForExit(5000)) { throw "$scenario left its child running." } }
                finally { $child.Dispose() }
            }
        }
    }
    finally { [Environment]::SetEnvironmentVariable('OOA_TEST_VALIDATION_CHILD_PID', $previousPidPath) }
    & $launcher -Godot $fixture -ValidateOnly ValidateRomArgument `
        -Rom (Join-Path $temporary 'reference ROM [US].gbc') -TimeoutSeconds 15
    & $launcher @fixtureParameters -ValidateOnly ValidateSkipRomArgument `
        -SkipRomValidation -TimeoutSeconds 15
    & $launcher @fixtureParameters -ValidateOnly ValidateFixture `
        -SkipRomValidation -TimeoutSeconds 15
    & $launcher @fixtureParameters -SkipRomValidation -TimeoutSeconds 15
    $profileText = "name`ttotal_ms`nValidateFixture`t1.500`n"
    [IO.File]::WriteAllText($timingProfile, $profileText)
    $previousProfileExpectation = [Environment]::GetEnvironmentVariable('OOA_TEST_VALIDATION_TIMING_PROFILE')
    try {
        [Environment]::SetEnvironmentVariable('OOA_TEST_VALIDATION_TIMING_PROFILE', $profileText)
        & $launcher @fixtureParameters -TimingProfile $timingProfile -TimeoutSeconds 15
        if ([IO.File]::ReadAllText($timingProfile) -ne $profileText) {
            throw 'Explicit timing profile was changed by the launcher.'
        }
    }
    finally { [Environment]::SetEnvironmentVariable('OOA_TEST_VALIDATION_TIMING_PROFILE', $previousProfileExpectation) }
    & $launcher @fixtureParameters -ValidateOnly ValidateContinueArgument `
        -ContinueOnFailure -TimeoutSeconds 15
    $failure = $null
    try { & $launcher @fixtureParameters -ContinueOnFailure -TimeoutSeconds 15 }
    catch { $failure = $_.ToString() }
    if ($null -eq $failure -or -not $failure.Contains('7 passed, 0 skipped, 1 failed, 8 registered')) {
        throw "Continued failure lost coverage or counted a failure as passed: $failure"
    }
    $original = [OracleValidation.ErrorMode]::GetErrorMode()
    # An unrelated pre-existing flag must survive success and failure paths.
    $expected = $original -bor 0x8000
    [void][OracleValidation.ErrorMode]::SetErrorMode($expected)
    try {
        & $launcher @fixtureParameters -FailOnEngineDiagnostics -TimeoutSeconds 15
        if ([OracleValidation.ErrorMode]::GetErrorMode() -ne $expected) {
            throw 'Successful validation changed the calling process error mode.'
        }
        foreach ($scenario in @(
            'ValidateCrash', 'ValidateMissingMarker',
            'ValidateUnexpectedSkip', 'ValidateIncompleteSkip', 'ValidateReportedFailure',
            'ValidateStdoutWarning', 'ValidateStderrError', 'ValidateEngineLogWarning'
        )) {
            $failure = $null
            try {
                & $launcher @fixtureParameters -ValidateOnly $scenario -TimeoutSeconds 15 `
                    -FailOnEngineDiagnostics `
                    -SkipRomValidation:($scenario -eq 'ValidateIncompleteSkip')
            }
            catch { $failure = $_.ToString() }
            if ($null -eq $failure -or -not $failure.Contains('Parallel validation incomplete')) {
                throw "$scenario did not fail promptly with a validation error: $failure"
            }
            if ([OracleValidation.ErrorMode]::GetErrorMode() -ne $expected) {
                throw "$scenario changed the calling process error mode."
            }
        }
    }
    finally { [void][OracleValidation.ErrorMode]::SetErrorMode($original) }
    $reports = Join-Path $temporary 'reports'
    [void][IO.Directory]::CreateDirectory($reports)
    $mergedProfile = Join-Path $temporary 'merged.tsv'
    $previousEmitTimings = [Environment]::GetEnvironmentVariable('OOA_TEST_VALIDATION_EMIT_TIMINGS')
    try {
        [Environment]::SetEnvironmentVariable('OOA_TEST_VALIDATION_EMIT_TIMINGS', '1')
        foreach ($partition in 1..2) {
            & $launcher @fixtureParameters -Workers 4 -PartitionIndex $partition -PartitionCount 2 `
                -ResultPath (Join-Path $reports "partition-$partition.json") -TimingProfile $timingProfile -TimeoutSeconds 15
        }
        $merge = Join-Path $PSScriptRoot 'Merge-ValidationResults.ps1'
        $mergeParameters = @{ ResultsDirectory = $reports; PartitionCount = 2; WorkersPerPartition = 4; TimingProfile = $mergedProfile }
        & $merge @mergeParameters
        if (@([IO.File]::ReadAllLines($mergedProfile)).Count -ne 9) { throw 'Merged timing profile lost scenarios.' }
        $first = Get-Content -LiteralPath (Join-Path $reports 'partition-1.json') -Raw | ConvertFrom-Json
        $secondPath = Join-Path $reports 'partition-2.json'
        $secondText = [IO.File]::ReadAllText($secondPath)
        foreach ($fault in @('missing-partition', 'duplicate-partition', 'duplicate-worker', 'missing-worker', 'duplicate-scenario',
            'missing-scenario', 'skip', 'failure', 'registered-count', 'different-profile', 'missing-timing', 'invalid-timing')) {
            $second = $secondText | ConvertFrom-Json
            switch ($fault) {
                'missing-partition' { [IO.File]::Delete($secondPath) }
                'duplicate-partition' { $second.PartitionIndex = 1 }
                'duplicate-worker' { $second.Workers[0].Index = 1 }
                'missing-worker' { $second.Workers = @($second.Workers | Select-Object -Skip 1) }
                'duplicate-scenario' { $second.Workers[0].ScenarioNames = @($first.Workers[0].ScenarioNames[0]) }
                'missing-scenario' {
                    $second.Workers[0].ScenarioNames = @(); $second.Workers[0].Executed = 0
                    $second.Timings = @($second.Timings | Select-Object -Skip 1)
                }
                'skip' { $second.Workers[0].Skipped = 1 }
                'failure' { $second.Workers[0].Failed = 1 }
                'registered-count' { $second.Registered = 9 }
                'different-profile' { $second.ProfileHash = 'different' }
                'missing-timing' { $second.Timings = @($second.Timings | Select-Object -Skip 1) }
                'invalid-timing' { $second.Timings[0].Milliseconds = 'NaN' }
            }
            if ($fault -ne 'missing-partition') {
                [IO.File]::WriteAllText($secondPath, ($second | ConvertTo-Json -Depth 8))
            }
            $failure = $null
            try { & $merge @mergeParameters } catch { $failure = $_.ToString() }
            [IO.File]::WriteAllText($secondPath, $secondText)
            if ($null -eq $failure) { throw "Distributed validation accepted $fault." }
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable('OOA_TEST_VALIDATION_EMIT_TIMINGS', $previousEmitTimings)
        foreach ($partition in 1..2) { [IO.File]::Delete((Join-Path $reports "partition-$partition.json")) }
        [IO.File]::Delete($mergedProfile)
        [IO.Directory]::Delete($reports, $false)
    }
    Write-Host 'Distributed validation tests passed (global shard IDs, exact coverage, missing/duplicate reports/workers/scenarios, skips, failures, mismatched registration/profile, incomplete timings).'
    Write-Host 'Validation launcher tests passed (no CPU/memory limits or forced priority, child cleanup on timeout and normal exit, 8 workers, focused selection, ROM path forwarding, timing snapshot forwarding, explicit skips, continued failure counts, incomplete/unexpected skip rejection, stdout/stderr/engine-log diagnostic rejection, unique logs, crash exit, missing marker, inherited/restored error mode).'
}
finally {
    # Only this test's generated executable lives here; no recursive deletion.
    if ([IO.File]::Exists($fixture)) { [IO.File]::Delete($fixture) }
    if ([IO.File]::Exists($timingProfile)) { [IO.File]::Delete($timingProfile) }
    if ([IO.File]::Exists($childPidFile)) { [IO.File]::Delete($childPidFile) }
    [IO.Directory]::Delete($temporary, $false)
}
