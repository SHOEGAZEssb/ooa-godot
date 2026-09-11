# INTERAC_SYMMETRY_NPC: retain independent scripts and initialization branches.
$symmetryMain = Join-Path $Disassembly 'scripts\ages\scripts.s'
$symmetryHelper = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$symmetryNativePath = Join-Path $Disassembly 'object_code\ages\interactions\symmetryNpc.s'
$symmetryNative = Read-ImportText $symmetryNativePath
$symmetryHelperSource = Read-ImportText $symmetryHelper
if ($symmetryNative -notmatch '(?s)@subid0cInit:.*?GLOBALFLAG_TUNI_NUT_PLACED.*?jp z,interactionDelete' -or
    $symmetryNative -notmatch '(?s)@state2:.*?wTmpcfc0.genericCutscene.state.*?bit 0,\(hl\).*?symmetryNpcSubid8And9Script_afterTuniNutRestored.*?ld a,\$01' -or
    $symmetryHelperSource -notmatch '(?s)symmetryNpc_getTuniNutState:\s+ld a,TREASURE_TUNI_NUT\s+call checkTreasureObtained\s+ld b,\$00\s+jr nc,\+\+\s+inc b\s+or a\s+jr z,\+\+\s+inc b' -or
    $symmetryHelperSource -notmatch '(?s)symmetryNpc_setRoomFlagIfTalkedToRightSister:\s+call getThisRoomFlags\s+ld e,Interaction.subid\s+ld a,\(de\)\s+sub \$08\s+or \(hl\)\s+ld \(hl\),a' -or
    $symmetryHelperSource -notmatch '(?s)symmetryNpc_getTuniNutStateForSister:.*?or a\s+ret nz.*?sub \$08.*?and \$0f\s+cp b\s+ld c,\$00\s+jr z,\+\s+ld c,\$03' -or
    $symmetryHelperSource -notmatch '(?s)symmetryNpc_getUpgradeCapacityForText:.*?TREASURE_RING_BOX.*?ld c,\$03.*?wRingBoxLevel.*?dec a\s+ld c,\$03\s+jr z,\+\+\s+ld c,\$05.*?wTextNumberSubstitution.*?ld \(hl\),\$00') {
    throw 'symmetryNpc.s/scriptHelper.s: $bf native visibility, state $02 signal, sister identity, nut state, or ring-box capacity contract changed.'
}
$symmetryRoots = @('symmetryNpcSubid0And1Script', 'symmetryNpcSubid2And3Script',
    'symmetryNpcSubid4And5Script', 'symmetryNpcSubid6And7Script',
    'symmetryNpcSubid8And9Script', 'symmetryNpcSubid8And9Script_afterTuniNutRestored',
    'symmetryNpcSubidAScript', 'symmetryNpcSubidBScript',
    'symmetryNpcSubidAOrBScript_afterTuniNutRestored', 'symmetryNpcSubidCScript')
$symmetryNodes = [Collections.Generic.List[object]]::new()
$symmetryLabels = @{}
foreach ($root in $symmetryRoots) {
    $path = if ($root -in @('symmetryNpcSubid6And7Script','symmetryNpcSubid8And9Script')) { $symmetryHelper } else { $symmetryMain }
    $symmetryLabels[$root] = $symmetryNodes.Count
    foreach ($node in @(Read-AssemblyLabelNodes $path $root)) {
        if ($node.Kind -eq 'Label') {
            $key = if ($node.Name.StartsWith('@') -or $node.Name -eq '++') { "$root/$($node.Name)" } else { $node.Name }
            if ($symmetryLabels.ContainsKey($key)) { throw "Duplicate symmetry label $key" }
            $symmetryLabels[$key] = $symmetryNodes.Count
        } elseif ($node.Kind -in @('MacroInvocation','Instruction','Data')) {
            if ($node.Code.Trim().StartsWith('.dw')) {
                $previous = $symmetryNodes[$symmetryNodes.Count-1]
                if ($previous.Node.Name -ne 'jumptable_memoryaddress') { throw "$path`:$($node.Line): unexpected symmetry table" }
                $previous.Targets += @($node.Code.Trim().Substring(3).Trim() -split ',\s*')
            } else {
                $symmetryNodes.Add([pscustomobject]@{ Root=$root; Node=$node; Targets=@(); Expansion=0 })
                if ($node.Name -eq 'rungenericnpclowindex') {
                    foreach ($extra in 1..4) { $symmetryNodes.Add([pscustomobject]@{ Root=$root; Node=$node; Targets=@(); Expansion=$extra }) }
                }
            }
        } elseif ($node.Kind -notin @('Blank','Comment')) { throw "$path`:$($node.Line): unsupported symmetry node $($node.Code)" }
    }
}
function Resolve-SymmetryTarget([string]$root, [string]$label) {
    $label = $label -replace '^mainScripts\.', ''
    $key = if ($label.StartsWith('@') -or $label -eq '++') { "$root/$label" } else { $label }
    if (!$symmetryLabels.ContainsKey($key)) { throw "Unresolved symmetry target $key" }
    return $symmetryLabels[$key].ToString()
}
function Read-SymmetryValue([string]$value) {
    if ($value -match '^\$([0-9a-f]+)$') { return [Convert]::ToInt32($Matches[1],16) }
    if ($value -match '^\d+$') { return [int]$value }
    if ($value -eq 'ROOMFLAG_40') { return 0x40 }
    if ($value -eq 'ROOMFLAG_ITEM') { return 0x20 }
    throw "Unsupported symmetry value '$value'"
}
$symmetryRows = [Collections.Generic.List[string]]::new()
$symmetryRows.Add('# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
foreach ($entry in $symmetryNodes) {
    $node=$entry.Node; $op=$node.Name.ToLowerInvariant(); $args=([string]$node.OperandText).Trim(); $parts=@($args -split ',\s*')
    $actor=''; $a=''; $b=''; $payload=''; $index=$symmetryRows.Count-1
    switch ($op) {
        'rungenericnpclowindex' {
            switch ($entry.Expansion) {
                0 { $op='nativeyield'; $payload='LoadText:'+$args.Substring(4) }
                1 { $op='initcollisions'; $actor='Symmetry' }
                2 { $op='checkabutton'; $actor='Symmetry' }
                3 { $op='showtext'; $a=$args.Substring(4); $payload=[string]$allTexts[[Convert]::ToInt32($a,16)] }
                4 { $op='scriptjump'; $a=($index-2).ToString() }
            }
        }
        'initcollisions' { $actor='Symmetry' }
        'checkabutton' { $actor='Symmetry' }
        'incstate' { $op='nativeyield'; $payload='ListenForNut' }
        'jumpifglobalflagset' { if (!$globalFlagValues.ContainsKey($parts[0])) { throw "Unknown symmetry flag $args" }; $op='jumpifmemoryeq'; $a='01'; $b=Resolve-SymmetryTarget $entry.Root $parts[1]; $payload="Global:$($globalFlagValues[$parts[0]])" }
        'jumpifitemobtained' { $op='jumpifmemoryeq'; $a='01'; $b=Resolve-SymmetryTarget $entry.Root $parts[1]; $payload="Treasure:$($parts[0])" }
        'jumpifroomflagset' { $a=(Read-SymmetryValue $parts[0]).ToString('x2'); $b=Resolve-SymmetryTarget $entry.Root $parts[1] }
        'jumpiftextoptioneq' { $a=(Read-SymmetryValue $parts[0]).ToString('x2'); $b=Resolve-SymmetryTarget $entry.Root $parts[1] }
        'jumpifmemoryeq' { $a=(Read-SymmetryValue $parts[1]).ToString('x2'); $b=Resolve-SymmetryTarget $entry.Root $parts[2]; $payload=$parts[0] }
        'jumptable_memoryaddress' { $op='jumptablememory'; $payload=$args+'|'+(($entry.Targets | ForEach-Object { Resolve-SymmetryTarget $entry.Root $_ }) -join ',') }
        'scriptjump' {
            $a=Resolve-SymmetryTarget $entry.Root $args
            # Local targets in these copied $100-byte helper bodies take
            # scriptCmd_jump's carry-clear relocation return. The expanded
            # genericNpcScript loop is in ROM and keeps its continuing jump.
            if ($entry.Root -in @('symmetryNpcSubid6And7Script','symmetryNpcSubid8And9Script') -and
                ($args.StartsWith('@') -or $args -eq '++')) { $op='scriptjumpyield' }
        }
        'showtextlowindex' { $op='showtext'; $a=$args.Substring(4); $payload=[string]$allTexts[[Convert]::ToInt32($a,16)] }
        'wait' { $a=(Read-SymmetryValue $args).ToString() }
        'setglobalflag' { if (!$globalFlagValues.ContainsKey($args)) { throw "Unknown symmetry flag $args" }; $a=([int]$globalFlagValues[$args]).ToString('x2') }
        'orroomflag' { $a=(Read-SymmetryValue $args).ToString('x2') }
        'giveitem' { $reward=$treasureObjectRecords[$args]; if (!$reward) { throw "Missing symmetry reward $args" }; $a=$reward.Treasure.ToString('x2'); $b=$reward.Subid.ToString('x2') }
        'asm15' {
            if ($args -notin @('symmetryNpc_setRoomFlagIfTalkedToRightSister','symmetryNpc_getTuniNutStateForSister','symmetryNpc_getTuniNutState','symmetryNpc_getUpgradeCapacityForText')) { throw "Unknown symmetry native $args" }
            $op='native'; $payload=$args
        }
        'askforsecret' { if ($args -ne 'SYMMETRY_SECRET') { throw "Unexpected symmetry secret $args" }; $op='nativeyield'; $payload='AskSecret' }
        'generatesecret' { if ($args -ne 'SYMMETRY_RETURN_SECRET') { throw "Unexpected symmetry secret $args" }; $op='nativeyield'; $payload='GenerateSecret' }
        'disableinput' { }
        'enableinput' { }
        'setdisabledobjectsto91' { $op='setdisabledobjects'; $a='91' }
        default { throw "$($node.Path):$($node.Line): unsupported symmetry opcode $op" }
    }
    if ($op -eq 'showtext' -and $a -eq '2d14') {
        if (!$payload.Contains('\jump(TX_2d11)')) { throw 'TX_2d14 lost its explanation jump.' }
        $payload=$payload.Replace('\jump(TX_2d11)', [string]$allTexts[0x2d11])
    }
    if ($op -eq 'showtext' -and (!$payload -or $payload -match '\\(?:call|jump)\(')) { throw "Unresolved symmetry TX_$a" }
    $symmetryRows.Add((New-CutsceneCommandRow 'symmetry' $index $entry.Root $node.Line $op $actor $a $b $payload))
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\symmetry_commands.tsv'), $symmetryRows)
$symmetryEntries=@('# subid`tentry')
$symmetryDispatch=@(Read-AssemblyDataDirectives $symmetryNativePath '@scriptTable' '.dw' | ForEach-Object { $_.OperandText -replace '^mainScripts\.', '' })
if ($symmetryDispatch.Count -ne 13) { throw 'symmetryNpc.s:@scriptTable must contain subids $00-$0c.' }
for ($i=0; $i -lt $symmetryDispatch.Count; $i++) { $symmetryEntries += "$($i.ToString('x2'))`t$(Resolve-SymmetryTarget '' $symmetryDispatch[$i])" }
$symmetryEntries += "0d`t$($symmetryLabels['symmetryNpcSubid8And9Script_afterTuniNutRestored'])"
Write-GeneratedTable((Join-Path $destination 'objects\symmetry_scripts.tsv'), $symmetryEntries)
Write-GeneratedTable((Join-Path $destination 'objects\symmetry_constants.tsv'), @('# key`tvalue',
    "placed-flag`t$($globalFlagValues['GLOBALFLAG_TUNI_NUT_PLACED'])",
    "sister-flag`t$($globalFlagValues['GLOBALFLAG_TALKED_TO_SYMMETRY_SISTER'])",
    "brother-flag`t$($globalFlagValues['GLOBALFLAG_TALKED_TO_SYMMETRY_BROTHER'])",
    "finished-flag`t$($globalFlagValues['GLOBALFLAG_FINISHEDGAME'])"))

$tuniSource = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\tuniNutMain.s')
$tuniObjects = Read-ImportText (Join-Path $Disassembly 'objects\ages\mainData.s')
if ($tuniObjects -notmatch '(?ms)^group5Mapf6ObjectData:\s+obj_Interaction \$b1 \$00 \$28 \$78\s+obj_Interaction \$bf \$08 \$58 \$58\s+obj_Interaction \$bf \$09 \$58 \$98' -or
    $tuniSource -notmatch '(?s)cp \$02.*?ld bc,\$0810' -or
    $tuniSource -notmatch '(?s)tuniNut_gotoState4:.*?ld bc,\$1878.*?ld a,\$06' -or
    $tuniSource -notmatch '(?s)ld a,60.*?ldbc INTERAC_SPARKLE, \$07.*?call darkenRoomLightly.*?SNDCTRL_STOPMUSIC' -or
    $tuniSource -notmatch '(?s)@substate0:.*?ld \(hl\),\$10.*?@substate1:.*?wFrameCounter.*?rrca.*?ret c.*?dec \(hl\).*?@substate2:.*?SPEED_40.*?cp \$18.*?@substate3:.*?ld c,\$20.*?SND_DROPESSENCE.*?ld a,90.*?SND_SOLVEPUZZLE_2' -or
    $tuniSource -notmatch '(?s)@substate5:.*?wPaletteThread_mode.*?GLOBALFLAG_TUNI_NUT_PLACED.*?TREASURE_TUNI_NUT.*?loseTreasure.*?wTmpcfc0.genericCutscene.state.*?set 0,\(hl\)' -or
    $tuniSource -notmatch '(?s)@setSymmetryVillageRoomFlags:.*?wPresentRoomFlags\+\$02.*?ld l,\$12.*?@setRow:\s+set 0,\(hl\)\s+inc l\s+set 0,\(hl\)\s+inc l\s+set 0,\(hl\)') {
    throw 'tuniNutMain.s:$b1 native placement, counters, motion, or persistent restoration contract changed.'
}
$tuniVisuals=@('# name`tsprite`tsource-offset`ttile-base`tpalette`tanimation')
foreach ($spec in @(@('nut',0xb1,0), @('sparkle',0x84,7))) {
    $g=$interactionGraphics["$($spec[1]):$($spec[2])"]
    $sprite=$gfxNames[$g.Gfx]
    $offset=if ($spec[0] -eq 'sparkle') { 0x1c00 } else { 0 }
    if (!$g -or !$sprite) { throw "Missing Tuni Nut visual $($spec[0])" }
    $tuniVisuals += "$($spec[0])`t$sprite`t$offset`t$($g.TileBase)`t$($g.Palette)`t$(Resolve-NpcAnimation $spec[1] $g.DefaultAnimation)"
}
Write-GeneratedTable((Join-Path $destination 'objects\tuni_nut_visuals.tsv'), $tuniVisuals)
Write-GeneratedTable((Join-Path $destination 'objects\tuni_nut_constants.tsv'), @('# key`tvalue',
    "group`t5", "room`t246", "x`t120", "y`t40", "placed-y`t24", "rise-wait`t60", "rise-distance`t16",
    "land-wait`t90", "gravity`t32", "darken`t-9", "speed`t$(Resolve-ObjectSpeed '40')",
    "stop-music`t$([Convert]::ToInt32($makuStopSound.Groups['value'].Value,16))", "drop-sound`t$($soundIds['SND_DROPESSENCE'])",
    "solve-sound`t$($soundIds['SND_SOLVEPUZZLE_2'])"))

# Every normalized command row emitted above must conform to the same schema
# that runtime startup consumes.
