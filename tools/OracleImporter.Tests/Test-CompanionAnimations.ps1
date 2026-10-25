$ErrorActionPreference = 'Stop'
# Function signatures carry their parameters outside the body, so retain each
# complete definition when loading it into the fixture's script scope.
$definitions = @(
    @{ Path = 'Shared/Initialize-Import.ps1'; Names = @('Convert-AssemblyInteger') },
    @{ Path = 'Ages/Import-CompanionData.ps1'; Names = @('New-CompanionAnimationIndex', 'Get-CompanionAnimationLabels', 'Get-CompanionNodesInRange', 'Resolve-MooshOam', 'Resolve-MooshOamTiles', 'Resolve-MooshSpecialAnimation', 'Resolve-CompanionSpecialAnimation') }
)
foreach ($spec in $definitions) {
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot ('../import_oracles/' + $spec.Path)), [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw $errors[0] }
    foreach ($name in $spec.Names) {
        $definition = $ast.Find({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
        }, $true)
        if ($null -eq $definition) { throw "Missing companion helper $name." }
        . ([scriptblock]::Create($definition.Extent.Text))
    }
}
function Node([string]$kind, [string]$name, [int]$offset, [string[]]$operands = @(), [string]$owner = '') {
    [pscustomobject]@{ Kind=$kind; Name=$name; Offset=$offset; Operands=$operands; EnclosingGlobalLabel=$owner; Line=1+$offset }
}
function Assert-Companion([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Assert-CompanionFailure([scriptblock]$action, [string]$message) {
    $caught = $false
    try { $null = & $action } catch { $caught = $_.Exception.Message.Contains($message) }
    Assert-Companion $caught "Missing companion diagnostic: $message"
}
$animations = @(
    (Node 'Label' 'animationA' 0),
    (Node 'Data' '.db' 10 @('$01','$00','$00')),
    (Node 'Label' 'animationB' 20),
    (Node 'Data' '.db' 30 @('$02','$01','$00')),
    (Node 'MacroInvocation' 'm_AnimationLoop' 40 @('animationB')),
    (Node 'Label' 'terminal' 50),
    (Node 'Data' '.db' 60 @('$01','$00','$80')),
    (Node 'MacroInvocation' 'm_AnimationLoop' 70 @('terminal')),
    (Node 'Label' 'malformed' 80),
    (Node 'Data' '.db' 90 @('$01','$00')),
    (Node 'MacroInvocation' 'm_AnimationLoop' 100 @('malformed')),
    (Node 'Label' 'badLoop' 110),
    (Node 'Data' '.db' 120 @('$01','$00','$00')),
    (Node 'MacroInvocation' 'm_AnimationLoop' 130 @('missing')),
    (Node 'Label' 'duplicate' 140),
    (Node 'Label' 'duplicate' 150)
)
$oam = @(
    (Node 'Data' '.db' 0 @('$01') 'oam0'),
    (Node 'Data' '.db' 10 @('$00','$00','$00','$00') 'oam0'),
    (Node 'Data' '.db' 20 @('$02') 'oam1'),
    (Node 'Data' '.db' 30 @('$00','$00','$00','$00') 'oam1'),
    (Node 'Data' '.db' 40 @('$00','$08','$02','$00') 'oam1')
)
$specialAnimationIndex = New-CompanionAnimationIndex $animations $oam
$specialEncodedOamCache = @{}
$mooshOamStart = 200
$mooshGfxOffsets = @{ 0=0; 1=10 }; $mooshGfxCounts = @{ 0=4; 1=2 }
$mooshOamPointers = @('oam0','oam1')
$encoded = Resolve-MooshSpecialAnimation 'animationA'
Assert-Companion ($encoded -eq '1@0,0,0,0|2@0,0,10,0;0,8,2,0~1') 'Moosh fallthrough, interior loop, or retained VRAM tiles changed.'
$resolved = Resolve-CompanionSpecialAnimation 'animationA' Ricky $mooshGfxOffsets $mooshGfxCounts @{0=0;1=1} $mooshOamPointers 200
Assert-Companion ($resolved.Encoded -eq '1@0,0,0,0|2@0,0,8,0;0,8,0,0~1' -and
    ($resolved.SourceOffsets -join ',') -eq '0,20') 'Companion OAM rebasing lost mixed retained/uploaded tiles or source offsets.'
Assert-Companion ((Resolve-MooshSpecialAnimation 'terminal') -eq '1,128@0,0,0,0') 'Moosh terminal parameter failed to override a later loop.'
Assert-CompanionFailure { Resolve-CompanionSpecialAnimation 'animationB' Ricky $mooshGfxOffsets $mooshGfxCounts @{0=0;1=1} $mooshOamPointers 200 } 'unresolved Moosh VRAM tile'
Assert-CompanionFailure { Resolve-MooshSpecialAnimation 'malformed' } 'malformed frame'
Assert-CompanionFailure { Resolve-MooshSpecialAnimation 'badLoop' } 'invalid loop target'
Assert-CompanionFailure { Resolve-MooshSpecialAnimation 'duplicate' } 'Could not resolve Moosh animation'
Assert-CompanionFailure { Resolve-MooshOam 'missing' } 'incomplete'
Assert-Companion (@(Get-CompanionNodesInRange $specialAnimationIndex.Frames 10 30).Count -eq 0) 'Companion range included an exclusive boundary.'
Write-Host 'Companion animation fixtures passed (fallthrough, loops, terminal signals, retained VRAM, rebasing, per-animation reset and diagnostics).'
