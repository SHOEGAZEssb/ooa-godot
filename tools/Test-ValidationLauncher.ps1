# Windows launcher regression: no ROM/game build required.
$ErrorActionPreference = 'Stop'
$launcher = Join-Path $PSScriptRoot 'validate_parallel.ps1'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('ooa-validation-launcher-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temporary)
$fixture = Join-Path $temporary 'fixture.exe'
$timingProfile = Join-Path $temporary 'timing profile [fixture].tsv'
try {
    Add-Type -OutputAssembly $fixture -OutputType ConsoleApplication -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
public static class ValidationLauncherFixture {
    [DllImport("kernel32.dll")] public static extern uint GetErrorMode();
    public static int Main(string[] args) {
        // Check inheritance before deliberately crashing; a broken launcher
        // must fail this test without displaying a real crash dialog.
        if ((GetErrorMode() & 3) != 3) return 91;
        int log = Array.IndexOf(args, "--log-file");
        if (log < 0 || log + 1 == args.Length) return 92;
        using (File.Open(args[log + 1], FileMode.CreateNew)) { }
        string shard = "1/1";
        string selected = null;
        string rom = null;
        bool skipRom = false;
        bool continueOnFailure = false;
        string timingProfile = null;
        foreach (string arg in args) {
            if (arg.StartsWith("--validate-shard=")) shard = arg.Substring(17);
            if (arg.StartsWith("--validate-only=")) selected = arg.Substring(16);
            if (arg.StartsWith("--validation-rom=")) rom = arg.Substring(17);
            if (arg == "--skip-rom-validation") skipRom = true;
            if (arg == "--validate-continue-on-failure") continueOnFailure = true;
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
        Console.WriteLine("VALIDATION_COMPLETE shard=" + shard + " executed=" + executed +
            " skipped=" + skipped + " registered=8 failed=" + failed);
        return failed == 0 ? 0 : 1;
    }
}
'@
    # Load the launcher's interop type and exercise a successful focused run.
    & $launcher -Godot $fixture -ValidateOnly ValidateFixture -TimeoutSeconds 15
    & $launcher -Godot $fixture -ValidateOnly ValidateRomArgument `
        -Rom (Join-Path $temporary 'reference ROM [US].gbc') -TimeoutSeconds 15
    & $launcher -Godot $fixture -ValidateOnly ValidateSkipRomArgument `
        -SkipRomValidation -TimeoutSeconds 15
    & $launcher -Godot $fixture -ValidateOnly ValidateFixture `
        -SkipRomValidation -TimeoutSeconds 15
    & $launcher -Godot $fixture -SkipRomValidation -TimeoutSeconds 15
    $profileText = "name`ttotal_ms`nValidateFixture`t1.500`n"
    [IO.File]::WriteAllText($timingProfile, $profileText)
    $previousProfileExpectation = [Environment]::GetEnvironmentVariable('OOA_TEST_VALIDATION_TIMING_PROFILE')
    try {
        [Environment]::SetEnvironmentVariable('OOA_TEST_VALIDATION_TIMING_PROFILE', $profileText)
        & $launcher -Godot $fixture -TimingProfile $timingProfile -TimeoutSeconds 15
        if ([IO.File]::ReadAllText($timingProfile) -ne $profileText) {
            throw 'Explicit timing profile was changed by the launcher.'
        }
    }
    finally { [Environment]::SetEnvironmentVariable('OOA_TEST_VALIDATION_TIMING_PROFILE', $previousProfileExpectation) }
    & $launcher -Godot $fixture -ValidateOnly ValidateContinueArgument `
        -ContinueOnFailure -TimeoutSeconds 15
    $failure = $null
    try { & $launcher -Godot $fixture -ContinueOnFailure -TimeoutSeconds 15 }
    catch { $failure = $_.ToString() }
    if ($null -eq $failure -or -not $failure.Contains('7 passed, 0 skipped, 1 failed, 8 registered')) {
        throw "Continued failure lost coverage or counted a failure as passed: $failure"
    }
    $original = [OracleValidation.ErrorMode]::GetErrorMode()
    # An unrelated pre-existing flag must survive success and failure paths.
    $expected = $original -bor 0x8000
    [void][OracleValidation.ErrorMode]::SetErrorMode($expected)
    try {
        & $launcher -Godot $fixture -TimeoutSeconds 15
        if ([OracleValidation.ErrorMode]::GetErrorMode() -ne $expected) {
            throw 'Successful validation changed the calling process error mode.'
        }
        foreach ($scenario in @(
            'ValidateCrash', 'ValidateMissingMarker',
            'ValidateUnexpectedSkip', 'ValidateIncompleteSkip', 'ValidateReportedFailure'
        )) {
            $failure = $null
            try {
                & $launcher -Godot $fixture -ValidateOnly $scenario -TimeoutSeconds 15 `
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
    Write-Host 'Validation launcher tests passed (8 workers, focused selection, ROM path forwarding, timing snapshot forwarding, explicit skips, continued failure counts, incomplete/unexpected skip rejection, unique logs, crash exit, missing marker, inherited/restored error mode).'
}
finally {
    # Only this test's generated executable lives here; no recursive deletion.
    if ([IO.File]::Exists($fixture)) { [IO.File]::Delete($fixture) }
    if ([IO.File]::Exists($timingProfile)) { [IO.File]::Delete($timingProfile) }
    [IO.Directory]::Delete($temporary, $false)
}
