$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\import_oracles\Import-CutsceneData.ps1')
. (Join-Path $PSScriptRoot '..\import_oracles\Convert-CutsceneCommands.ps1')

function Assert-Normalization([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}
function Command([int]$index, [string]$label, [string]$opcode, [string]$operands) {
    [pscustomobject]@{ Path='fixture.s'; Line=10+$index; Script='fixture'; Label=$label
        Index=$index; Opcode=$opcode; Operands=$operands }
}
# Parsed-command fixtures deliberately differ from Postman's command count and
# layout. Conditional carry wrappers continue in either execution location.
foreach ($padding in 0, 3) {
    $commands = @()
    for ($i=0; $i -lt $padding; $i++) { $commands += Command $i "@padding$i" 'wait' '1' }
    $commands += Command $padding '@local' 'moveright' '$1d'
    $commands += Command ($padding+1) '@branch' 'scriptjump' '@local'
    $commands += Command ($padding+2) '@conditional' 'jumpiftextoptioneq' '$00, @local'
    $commands += Command ($padding+3) '@external' 'scriptjump' 'mainScripts.stubScript'
    foreach ($copied in $false, $true) {
        $rows = @(ConvertTo-CutsceneCommandRows $commands 'Postman' -animations @{ 1='right-oam' } `
            -yieldOnJump $copied -externalTargets @{ 'mainScripts.stubScript'=($padding+4) })
        $move=$rows[$padding+1].Split([char]9); $jump=$rows[$padding+2].Split([char]9)
        $conditional=$rows[$padding+3].Split([char]9); $external=$rows[$padding+4].Split([char]9)
        Assert-Normalization ($move[4] -eq 'move' -and $move[5] -eq 'Postman' -and $move[6] -eq '08' -and $move[7] -eq '1d') 'Movement normalization lost source operands/binding.'
        $expected=if ($copied) { 'scriptjumpyield' } else { 'scriptjump' }
        Assert-Normalization ($jump[4] -eq $expected -and [int]$jump[6] -eq $padding) 'Local target relocation or buffer/ROM carry boundary changed.'
        Assert-Normalization ($conditional[4] -eq 'jumpiftextoptioneq' -and [int]$conditional[7] -eq $padding) 'Conditional carry wrapper inherited unconditional jump timing.'
        Assert-Normalization ($external[4] -eq 'scriptjump' -and [int]$external[6] -eq ($padding+4)) 'External ROM jump was treated as buffer-local.'
    }
}
foreach ($case in @(
    @{ Op='moveright'; Args='$1d, $02'; Error='expects 1 operands' },
    @{ Op='moveright'; Args='UNKNOWN'; Error='unresolved scalar' },
    @{ Op='moveup'; Args='$01'; Error='unbound movement animation' },
    @{ Op='scriptjump'; Args='missingStub'; Error='unresolved branch' },
    @{ Op='unknown'; Args=''; Error='unsupported command' }
)) {
    $caught=$false
    try { $null=ConvertTo-CutsceneCommandRows @((Command 0 'root' $case.Op $case.Args)) 'Postman' -animations @{ 1='right-oam' } }
    catch { $caught=$_.Exception.Message.Contains('fixture.s:10: fixture/root') -and $_.Exception.Message.Contains($case.Error) }
    Assert-Normalization $caught "Normalizer lost source-aware diagnostic: $($case.Op) $($case.Args)"
}
Write-Host 'Cutscene normalization fixtures passed (relocation, movement, buffer/ROM/conditional jumps, diagnostics).'
