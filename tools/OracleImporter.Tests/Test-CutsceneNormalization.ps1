$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\import_oracles\Shared\Import-CutsceneData.ps1')
. (Join-Path $PSScriptRoot '..\import_oracles\Shared\Convert-CutsceneCommands.ps1')

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
$text = "Source text`nwith preserved formatting."
$callRows = @(ConvertTo-CutsceneCommandRows @(
    (Command 0 'driver' 'callscript' 'mainScripts.fixture'),
    (Command 1 'driver' 'scriptjump' 'driver'),
    (Command 2 'mainScripts.fixture' 'retscript' '')) 'Actor' -yieldOnJump $true `
    -externalTargets @{ 'mainScripts.fixture'=2 })
Assert-Normalization ($callRows[1].Split([char]9)[4] -eq 'callscript' -and
    $callRows[1].Split([char]9)[6] -eq '2' -and $callRows[3].Split([char]9)[4] -eq 'return') `
    'Copied-driver callscript/retscript lost their target or source return operation.'
$textRows = @(ConvertTo-CutsceneCommandRows @(
    (Command 0 'root' 'settextid' 'TX_1441'),
    (Command 1 'root' 'showloadedtext' '')) 'Blocker' -texts @{ 0x1441=$text })
$setText = $textRows[1].Split([char]9)
$showText = $textRows[2].Split([char]9)
Assert-Normalization ($setText[4] -eq 'settextid' -and $setText[5] -eq 'Blocker' -and $setText[6] -eq '1441' -and
    [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($setText[8])) -eq $text) 'settextid lost its actor, full text ID or formatting.'
Assert-Normalization ($showText[4] -eq 'showloadedtext' -and $showText[5] -eq '') 'showloadedtext changed its source operand shape.'
foreach ($case in @(
    @{ Op='moveright'; Args='$1d, $02'; Error='expects 1 operands' },
    @{ Op='moveright'; Args='UNKNOWN'; Error='unresolved scalar' },
    @{ Op='moveup'; Args='$01'; Error='unbound movement animation' },
    @{ Op='scriptjump'; Args='missingStub'; Error='unresolved branch' },
    @{ Op='settextid'; Args='TX_1441'; Error='unbound text' },
    @{ Op='unknown'; Args=''; Error='unsupported command' }
)) {
    $caught=$false
    try { $null=ConvertTo-CutsceneCommandRows @((Command 0 'root' $case.Op $case.Args)) 'Postman' -animations @{ 1='right-oam' } }
    catch { $caught=$_.Exception.Message.Contains('fixture.s:10: fixture/root') -and $_.Exception.Message.Contains($case.Error) }
    Assert-Normalization $caught "Normalizer lost source-aware diagnostic: $($case.Op) $($case.Args)"
}
Write-Host 'Cutscene normalization fixtures passed (relocation, movement, buffer/ROM/conditional jumps, diagnostics).'

# Cached schema results must preserve case and retain negative results. The
# actual compiled validator remains the only producer of those results.
$assemblySourceHost = $null
$script:scalarRequestCount = 0
function Invoke-AssemblySourceHost($process, [string]$command, [string]$payload) {
    if ($command -ne 'CUTSCENE_SCALAR') { throw "Unexpected fixture command $command." }
    $script:scalarRequestCount++
    if ($payload -ceq ('actor' + [char]0 + 'Actor')) { return '1' }
    return '0'
}
foreach ($repeat in 1..2) {
    Assert-Normalization (Test-CutsceneSchemaScalar 'actor' 'Actor') 'Valid schema result was lost.'
    Assert-Normalization (-not (Test-CutsceneSchemaScalar 'actor' 'actor')) 'Schema cache conflated differently cased values.'
}
Assert-Normalization ($script:scalarRequestCount -eq 2) 'Repeated positive/negative scalar results were not reused.'
Write-Host 'Scalar-cache fixtures passed (case-sensitive keys and retained positive/negative results).'

# The generic macro occupies four commands; later labels must target the expanded stream.
$tables = @(
    "# script`tlabel`tindex`tsource-line`topcode`toperands-base64`tjump-targets",
    "fixture`tfirst`t0`t10`tjumptable_objectbyte`tSW50ZXJhY3Rpb24udmFyM2E=`tfirst,second",
    "fixture`tsecond`t1`t11`tjumptable_objectbyte`tSW50ZXJhY3Rpb24udmFyM2E=`tsecond,first")
$tableRows = @(ConvertTo-NpcProgramRows $tables 'Family' @{})
foreach ($index in 1..2) {
    $payload = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($tableRows[$index].Split([char]9)[8]))
    $expected = if ($index -eq 1) { 'Interaction.var3a|0,1' } else { 'Interaction.var3a|1,0' }
    Assert-Normalization ($payload -eq $expected) 'Distinct jump tables sharing an operand were conflated.'
}
$program = @("# script`tlabel`tindex`tsource-line`topcode`toperands-base64`tjump-targets")
foreach ($entry in @(@('start','rungenericnpclowindex','<TX_5501'), @('tail','scriptjump','start'))) {
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($entry[2]))
    $program += "fixture`t$($entry[0])`t0`t12`t$($entry[1])`t$encoded`t"
}
foreach ($actor in 'Family','Farore') {
    $rows = @(ConvertTo-NpcProgramRows $program $actor @{ 0x5501='fixture text' })
    Assert-Normalization ($rows.Count -eq 6) 'NPC macro expansion changed command count.'
    $show = $rows[3].Split([char]9)
    Assert-Normalization ($show[4] -eq 'showtext' -and $show[6] -eq '5501') 'NPC macro lost text ID.'
    $position = if ($actor -eq 'Farore') { '2' } else { '' }
    Assert-Normalization ($show[7] -eq $position) 'NPC macro changed textbox position.'
    Assert-Normalization ($rows[4].Split([char]9)[6] -eq '1' -and
        $rows[5].Split([char]9)[6] -eq '0') 'NPC macro lost internal or external branch targets.'
}
