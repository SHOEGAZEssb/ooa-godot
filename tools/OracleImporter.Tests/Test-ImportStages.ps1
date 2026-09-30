# Exercise real stage scopes without reading the ROM or disassembly.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\import_oracles\Invoke-ImportStage.ps1')
$temporary = Join-Path ([IO.Path]::GetTempPath()) "ooa-stage-tests [0] $([Guid]::NewGuid().ToString('N'))"
[void][IO.Directory]::CreateDirectory($temporary)
$modules = [Collections.Generic.List[Management.Automation.PSModuleInfo]]::new()

function Stage-Contract($name, $inputs = @(), $outputs = @(), $functionInputs = @(), $functionOutputs = @()) {
    [pscustomobject]@{ Name = $name; Script = "$name.ps1"; Inputs = $inputs
        Outputs = $outputs; FunctionInputs = $functionInputs; FunctionOutputs = $functionOutputs }
}
function Run-Stage($contract, $values = @{}, $functions = @{}) {
    $stage = Invoke-ImportStage $contract $temporary $values $functions
    $modules.Add($stage)
    return $stage
}
function Assert-Stage([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}
function Assert-StageFailure($contract, [string]$message, $values = @{}, $functions = @{}) {
    $caught = $false
    try { $null = Run-Stage $contract $values $functions }
    catch { $caught = $_.Exception.Message.Contains($contract.Name) -and $_.Exception.Message.Contains($message) }
    Assert-Stage $caught "Stage '$($contract.Name)' did not reject $message."
}

try {
    [IO.File]::WriteAllText((Join-Path $temporary 'producer.ps1'), @'
$script:ownerValue = 'producer'
$shared = [Collections.Generic.List[string]]::new()
$bytes = [byte[]]@(0x42, 0x80)
$empty = $null
$privateValue = 'hidden'
function Private-Helper { $script:ownerValue }
function Read-Owner { Private-Helper }
'@)
    $producer = Run-Stage (Stage-Contract 'producer' -outputs @('shared', 'bytes', 'empty') -functionOutputs @('Read-Owner'))
    Assert-Stage ($producer.ExportedVariables.Count -eq 3 -and $producer.ExportedFunctions.Count -eq 1) 'Stage exports included private implementation details.'
    $values = @{ shared = $producer.ExportedVariables['shared'].Value; bytes = $producer.ExportedVariables['bytes'].Value }
    $functions = @{ 'Read-Owner' = $producer.ExportedFunctions['Read-Owner'].ScriptBlock }

    [IO.File]::WriteAllText((Join-Path $temporary 'consumer.ps1'), @'
$script:ownerValue = 'consumer'
function Private-Helper { 'wrong owner' }
$result = Read-Owner
$shared.Add('consumer')
$bytes[0] = 0x33
'@)
    $consumer = Run-Stage (Stage-Contract 'consumer' -inputs @('shared', 'bytes') -outputs @('result') -functionInputs @('Read-Owner')) $values $functions
    Assert-Stage ($consumer.ExportedVariables['result'].Value -eq 'producer') 'Exported helper lost its owner scope or private dependencies.'
    Assert-Stage ($values.shared.Count -eq 1 -and $values.shared[0] -eq 'consumer' -and $values.bytes[0] -eq 0x33) 'Stage inputs lost collection identity or ordered shared writes.'
    Assert-Stage ($producer.ExportedVariables['empty'].Value -eq $null) 'An explicit null output must remain available.'

    [IO.File]::WriteAllText((Join-Path $temporary 'hidden-variable.ps1'), @'
# An assignment in an unexecuted branch must not mask an undeclared read.
if ($false) { $privateValue = 'unused' }
$result = $privateValue
'@)
    Assert-StageFailure (Stage-Contract 'hidden-variable' -outputs @('result')) 'privateValue'
    [IO.File]::WriteAllText((Join-Path $temporary 'hidden-helper.ps1'), '$result = Private-Helper')
    Assert-StageFailure (Stage-Contract 'hidden-helper' -outputs @('result')) 'Private-Helper'
    [IO.File]::WriteAllText((Join-Path $temporary 'unpublished-helper.ps1'), '$result = Read-Owner')
    Assert-StageFailure (Stage-Contract 'unpublished-helper' -outputs @('result')) 'Read-Owner' @{} $functions
    [IO.File]::WriteAllText((Join-Path $temporary 'unpublished-variable.ps1'), '$result = $shared')
    Assert-StageFailure (Stage-Contract 'unpublished-variable' -outputs @('result')) 'shared' $values

    Assert-StageFailure (Stage-Contract 'missing-input' -inputs @('absent')) "input 'absent' is unavailable"
    Assert-StageFailure (Stage-Contract 'missing-helper' -functionInputs @('Absent-Helper')) "function input 'Absent-Helper' is unavailable"
    [IO.File]::WriteAllText((Join-Path $temporary 'missing-output.ps1'), '$other = 1')
    Assert-StageFailure (Stage-Contract 'missing-output' -outputs @('absent')) "variable output 'absent' was not produced"
    Assert-StageFailure (Stage-Contract 'missing-output' -functionOutputs @('Absent-Helper')) "function output 'Absent-Helper' was not produced"
    [IO.File]::WriteAllText((Join-Path $temporary 'source-failure.ps1'), "throw 'fixture.s:0x42: unsupported opcode'")
    Assert-StageFailure (Stage-Contract 'source-failure') 'fixture.s:0x42: unsupported opcode'

    # A separate import gets a new owner and mutable collections. Existing
    # exported helpers must continue to refer to their original owner.
    $again = Run-Stage (Stage-Contract 'producer' -outputs @('shared', 'bytes') -functionOutputs @('Read-Owner'))
    Assert-Stage (-not [object]::ReferenceEquals($values.shared, $again.ExportedVariables['shared'].Value)) 'Separate imports shared mutable stage state.'
    Assert-Stage ($again.ExportedVariables['shared'].Value.Count -eq 0 -and (& $functions['Read-Owner']) -eq 'producer') 'A later stage scope replaced an existing helper owner.'
    Write-Host 'Import-stage fixtures passed (declared boundaries, owner-bound helpers, shared identity, fresh scopes and diagnostics).'
}
finally {
    foreach ($module in $modules) { Remove-Module -ModuleInfo $module -Force }
    $resolvedTemporary = [IO.Path]::GetFullPath($temporary)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedTemporary.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove non-temporary stage fixture path: $resolvedTemporary"
    }
    Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force
}
