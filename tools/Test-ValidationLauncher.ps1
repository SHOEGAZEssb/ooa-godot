# Windows launcher regression: no ROM/game build required.
$ErrorActionPreference = 'Stop'
$launcher = Join-Path $PSScriptRoot 'validate_parallel.ps1'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('ooa-validation-launcher-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temporary)
$fixture = Join-Path $temporary 'fixture.exe'
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
        foreach (string arg in args) {
            if (arg.StartsWith("--validate-shard=")) shard = arg.Substring(17);
            if (arg.StartsWith("--validate-only=")) selected = arg.Substring(16);
        }
        if (selected == "ValidateCrash") {
            Console.Error.WriteLine("fixture native failure");
            Environment.FailFast("fixture deliberate crash");
        }
        if (selected == "ValidateMissingMarker") return 0;
        int workers = int.Parse(shard.Split('/')[1]);
        int executed = selected == null ? 8 / workers : 1;
        Console.WriteLine("VALIDATION_COMPLETE shard=" + shard + " executed=" + executed + " registered=8");
        return 0;
    }
}
'@
    # Load the launcher's interop type and exercise a successful focused run.
    & $launcher -Godot $fixture -ValidateOnly ValidateFixture -TimeoutSeconds 15
    $original = [OracleValidation.ErrorMode]::GetErrorMode()
    # An unrelated pre-existing flag must survive success and failure paths.
    $expected = $original -bor 0x8000
    [void][OracleValidation.ErrorMode]::SetErrorMode($expected)
    try {
        & $launcher -Godot $fixture -TimeoutSeconds 15
        if ([OracleValidation.ErrorMode]::GetErrorMode() -ne $expected) {
            throw 'Successful validation changed the calling process error mode.'
        }
        foreach ($scenario in @('ValidateCrash', 'ValidateMissingMarker')) {
            $failure = $null
            try { & $launcher -Godot $fixture -ValidateOnly $scenario -TimeoutSeconds 15 }
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
    Write-Host 'Validation launcher tests passed (8 workers, focused selection, unique logs, crash exit, missing marker, inherited/restored error mode).'
}
finally {
    # Only this test's generated executable lives here; no recursive deletion.
    if ([IO.File]::Exists($fixture)) { [IO.File]::Delete($fixture) }
    [IO.Directory]::Delete($temporary, $false)
}
