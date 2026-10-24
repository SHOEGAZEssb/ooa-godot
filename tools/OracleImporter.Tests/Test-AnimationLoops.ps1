$ErrorActionPreference = 'Stop'
$initialize = Join-Path $PSScriptRoot '../import_oracles/Initialize-Import.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($initialize, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw 'Could not parse shared animation readers.' }
foreach ($name in 'Convert-AssemblyInteger', 'Convert-AssemblyAnimationFrame', 'Read-AssemblyAnimationDefinitions') {
    $definition = $ast.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    if ($null -eq $definition) { throw "Missing animation helper $name." }
    . ([ScriptBlock]::Create($definition.Extent.Text))
}
function Resolve-ImportReadPath([string]$path) { return $path }
function Node([string]$kind, [string]$name, [int]$offset, [string[]]$operands = @()) {
    [pscustomobject]@{ Kind=$kind; Name=$name; Offset=$offset; Length=8; Operands=$operands }
}
function Read-AssemblyNodes([string]$path) {
    # Parsed-node fixtures: an FF caller signal with a self-looping final
    # frame, a separate terminal stream, then an unrelated looping stream.
    Node 'Label' 'animationa' 0
    Node 'Data' '.db' 10 @('$09','$04','$00')
    Node 'Label' 'animationaLoop' 20
    Node 'Data' '.db' 30 @('$01','$0a','$ff')
    Node 'MacroInvocation' 'm_AnimationLoop' 40 @('animationaLoop')
    Node 'Label' 'animationb' 50
    Node 'Data' '.db' 60 @('$01','$0c','$ff')
    Node 'Label' 'animationc' 70
    Node 'Data' '.db' 80 @('$02','$0e','$00')
    Node 'MacroInvocation' 'm_AnimationLoop' 90 @('animationc')
    Node 'Label' 'animationData1000' 100
    Node 'Data' '.db' 110 @('$c8','$56','$00')
    Node 'Label' 'animationLoop1003' 120
    Node 'Data' '.db' 130 @('$0c','$27','$00')
    Node 'Data' '.db' 140 @('$01','$2b','$ff')
    Node 'MacroInvocation' 'm_AnimationLoop' 150 @('animationLoop1003')
    Node 'Label' 'animationData100c' 160
    Node 'Data' '.db' 170 @('$01','$00','$ff')
}
foreach ($bounded in $false,$true) {
    $assemblyAnimationCache = @{}
    $definitions = Read-AssemblyAnimationDefinitions 'fixture.s' 'animation[a-f]+(?:Loop)?' $bounded
    if ($definitions.animationa.Frames.Count -ne 2 -or $definitions.animationa.LoopStart -ne 1 -or
        $definitions.animationa.Frames[1].Parameter -ne 255) {
        throw 'An FF caller signal discarded the following native loop target.'
    }
    if ($definitions.animationb.Frames.Count -ne 1 -or $definitions.animationb.Frames[0].PointerOffset -ne 12) {
        throw 'A terminal stream consumed its next animation or loop.'
    }
    if ($definitions.animationc.Frames.Count -ne 1 -or $definitions.animationc.LoopStart -ne 0) {
        throw 'An ordinary full-animation loop changed.'
    }
    $definitions = Read-AssemblyAnimationDefinitions 'fixture.s' 'animation(?:Data|Loop)[0-9a-f]+' $bounded
    if ($definitions.animationData1000.Frames.Count -ne 3 -or $definitions.animationData1000.LoopStart -ne 1 -or
        $definitions.animationLoop1003.Frames.Count -ne 2 -or $definitions.animationLoop1003.LoopStart -ne 0 -or
        $definitions.animationData100c.Frames.Count -ne 1) {
        throw 'A prefix-style native loop anchor split its parent stream, lost its addressable tail or consumed the next stream.'
    }
}
Write-Host 'Animation loop fixtures passed (FF caller signal, prefix/suffix anchors, addressable tails, adjacent stream boundary).'
