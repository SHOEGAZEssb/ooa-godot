$ErrorActionPreference = 'Stop'
$initialize = Join-Path $PSScriptRoot '../import_oracles/Shared/Initialize-Import.ps1'
$ast = [Management.Automation.Language.Parser]::ParseFile($initialize, [ref]$null, [ref]$null)
foreach ($name in 'Read-ImportText', 'Resolve-ImportReadPath', 'Resolve-AssemblySourceTextPath', 'Get-AssemblyLabelBody', 'Find-ImportSpriteSource') {
    $definition = $ast.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    if ($null -eq $definition) { throw "Missing source-cache helper $name." }
    . ([ScriptBlock]::Create($definition.Extent.Text))
}
$Disassembly = [IO.Path]::GetTempPath()
$assemblySourceHost = $null
$assemblyTextCache = @{}
$assemblyTextPaths = [Runtime.CompilerServices.ConditionalWeakTable[string,string]]::new()
$reads = 0
function Invoke-AssemblySourceHost($hostProcess, $command, $path) {
    if ($command -ne 'TEXT') { throw "Unexpected source command $command." }
    $script:reads++
    if ([IO.Path]::GetFileName($path).StartsWith('empty')) { return [string]::Empty }
    return [string]::new("root:`n.db `$01`n".ToCharArray())
}
function Read-AssemblyLabelBlock($path, $label) { return "$path|$label" }

$first = Read-ImportText 'first.s'
$second = Read-ImportText 'second.s'
$again = Read-ImportText 'first.s'
if ($reads -ne 2 -or -not [object]::ReferenceEquals($first, $again)) {
    throw 'Repeated source reads lost their cached string identity.'
}
foreach ($entry in @(@($first, 'first.s'), @($second, 'second.s'))) {
    $path = Resolve-ImportReadPath $entry[1]
    if ((Resolve-AssemblySourceTextPath $entry[0]) -cne $path -or
        (Get-AssemblyLabelBody $entry[0] 'root') -cne "$path|root") {
        throw 'Equal-content source files lost their distinct source paths.'
    }
}
$copy = [string]::new($first.ToCharArray())
if ($null -ne (Resolve-AssemblySourceTextPath $copy)) { throw 'Untracked equal-content text was accepted.' }
$rejected = $false
try { $null = Get-AssemblyLabelBody $copy 'root' }
catch { $rejected = $_.Exception.Message.Contains('untracked source text') }
if (-not $rejected) { throw 'Untracked source text lost its diagnostic.' }
$null = Read-ImportText 'empty-first.s'
$null = Read-ImportText 'empty-second.s'
if ($null -eq (Resolve-AssemblySourceTextPath ([string]::Empty)) -or $reads -ne 4) {
    throw 'Empty assembly sources could not share the empty-string instance.'
}
Write-Host 'Source-cache fixtures passed (read-once identity, equal-content files, untracked copies and empty sources).'

$Disassembly = (Resolve-Path (Join-Path $PSScriptRoot '../../external/oracles-disasm')).Path
$spriteSourceIndex = @{}
foreach ($game in 'ages', 'seasons') {
    $expected = (Resolve-Path (Join-Path $Disassembly "gfx_compressible/$game/spr_essences.png")).Path
    if ((Find-ImportSpriteSource 'spr_essences' $game).FullName -cne $expected) {
        throw "$game must select its own Essence sheet when both games define spr_essences."
    }
}
if ((Find-ImportSpriteSource 'spr_common_sprites' 'seasons').Directory.Name -ne 'common') {
    throw 'Selected-game sprite lookup lost common graphics fallback.'
}
$rejected = $false
try { $null = Find-ImportSpriteSource 'spr_aquamentus_1' 'ages' }
catch { $rejected = $_.Exception.Message.Contains('ages/spr_aquamentus_1.png') }
if (-not $rejected) { throw "Selected-game lookup accepted another game's exclusive sheet." }
Write-Host 'Sprite-source fixtures passed (selected-game overrides, common fallback and cross-game rejection).'
