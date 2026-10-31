# Room $0:$6c is one source-ordered event assembled from three placed
# INTERAC_GHINI_HARASSING_MOOSH lanes, the placed companion controller, and
# the positionless companion spawner. Import the independently advancing
# scripts and the special-object Moosh visual instead of inventing one master
# dialogue sequence in runtime code.
$mooshObjectPath = Join-Path $Disassembly 'objects\ages\mainData.s'
$mooshObjectSource = Read-ImportText $mooshObjectPath
$mooshPlacement = [regex]::Match(
    $mooshObjectSource,
    '(?ms)^group0Map6cObjectData:\s*' +
    'obj_Interaction \$73 \$00 \$(?<g0y>[0-9a-f]{2}) \$(?<g0x>[0-9a-f]{2})\s*' +
    'obj_Interaction \$73 \$01 \$(?<g1y>[0-9a-f]{2}) \$(?<g1x>[0-9a-f]{2})\s*' +
    'obj_Interaction \$73 \$02 \$(?<g2y>[0-9a-f]{2}) \$(?<g2x>[0-9a-f]{2})\s*' +
    'obj_Interaction \$71 \$00 \$(?<controllery>[0-9a-f]{2}) \$(?<controllerx>[0-9a-f]{2})\s*' +
    'obj_Interaction \$71 \$02 \$(?<restricty>[0-9a-f]{2}) \$(?<restrictx>[0-9a-f]{2})\s*' +
    'obj_Interaction \$67 \$00\s*obj_End')
if (-not $mooshPlacement.Success -or
    $mooshPlacement.Groups['g0y'].Value -ne '18' -or
    $mooshPlacement.Groups['g0x'].Value -ne '68' -or
    $mooshPlacement.Groups['g1y'].Value -ne '18' -or
    $mooshPlacement.Groups['g1x'].Value -ne '48' -or
    $mooshPlacement.Groups['g2y'].Value -ne '38' -or
    $mooshPlacement.Groups['g2x'].Value -ne '58' -or
    $mooshPlacement.Groups['controllery'].Value -ne '28' -or
    $mooshPlacement.Groups['controllerx'].Value -ne '58' -or
    $mooshPlacement.Groups['restricty'].Value -ne '6d' -or
    $mooshPlacement.Groups['restrictx'].Value -ne '38') {
    throw 'Room 0:6c Ghini, companion-controller, or spawner order/placement changed.'
}
$mooshGoodbyePlacement = [regex]::Match(
    $mooshObjectSource,
    '(?ms)^group0Map6bObjectData:\s*' +
    'obj_Interaction \$71 \$01 \$(?<controllery>[0-9a-f]{2}) \$(?<controllerx>[0-9a-f]{2})\s*' +
    'obj_Interaction \$67 \$01\s*obj_End')
if (-not $mooshGoodbyePlacement.Success -or
    $mooshGoodbyePlacement.Groups['controllery'].Value -ne '38' -or
    $mooshGoodbyePlacement.Groups['controllerx'].Value -ne '08') {
    throw 'Room 0:6b companion-controller/spawner order or placement changed.'
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\moosh_goodbye_placements.tsv'), @(
    "# order`tid`tsubid`ty`tx`tsource",
    "0`t71`t01`t$($mooshGoodbyePlacement.Groups['controllery'].Value)`t$($mooshGoodbyePlacement.Groups['controllerx'].Value)`tmainData.s:group0Map6bObjectData",
    "1`t67`t01`t00`t00`tmainData.s:group0Map6bObjectData"
))

$mooshScriptsPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$mooshScriptsSource = Read-ImportText $mooshScriptsPath
$ghiniNativeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\ghiniHarassingMoosh.s')
$companionNativeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\companionScripts.s')
$companionSpawnerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\companionSpawner.s')
$mooshSpecialSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\moosh.s')
$mooshExclamationSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\exclamationMark.s')
$mooshHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$mooshSpeedSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$mooshDirectionSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\directions.s')
$mooshWramSource = Read-ImportText (
    Join-Path $Disassembly 'include\wram.s')
$mooshEnemyConstants = Read-ImportText (
    Join-Path $Disassembly 'constants\common\enemies.s')
$mooshTreasureConstants = Read-ImportText (
    Join-Path $Disassembly 'constants\common\treasure.s')
$mooshSpecialConstants = Read-ImportText (
    Join-Path $Disassembly 'constants\common\specialObjects.s')
$mooshMusicConstants = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
$mooshWaterHover = [regex]::Match(
    $mooshSpecialSource,
    '(?ms)^mooshState8Substate1:.*?objectCheckIsOverHazard\s*cp \$(?<hazard>[0-9a-f]{2}).*?' +
    'objectSetSpeedZ.*?SpecialObject\.substate\s*ld \(hl\),\$(?<substate>[0-9a-f]{2}).*?' +
    'INTERAC_EXCLAMATION_MARK.*?objectCreateInteractionWithSubid00.*?dec l\s*ld a,\(hl\)\s*sub \$(?<zOffset>[0-9a-f]{2}).*?' +
    'ld a,\$(?<frames>[0-9a-f]{2})\s*ld \(hl\),a ; \[Interaction\.counter1\] = \$3c\s*ld \(de\),a ; \[Moosh\.counter1\] = \$3c.*?' +
    '^mooshState8Substate5:.*?companionDecCounter1IfNonzero.*?specialObjectAnimate.*?objectUpdateSpeedZ_paramC')
if (-not $mooshWaterHover.Success -or
    $mooshWaterHover.Groups['hazard'].Value -ne '01' -or
    $mooshWaterHover.Groups['substate'].Value -ne '05' -or
    $mooshWaterHover.Groups['zOffset'].Value -ne '20' -or
    $mooshWaterHover.Groups['frames'].Value -ne '3c' -or
    $mooshExclamationSource -notmatch
        '(?ms)^objectCreateExclamationMark_body:.*?SND_CLINK.*?playSound') {
    throw 'Moosh water-hover exclamation/fall state changed.'
}
$mooshWaterHazard = [Convert]::ToInt32(
    $mooshWaterHover.Groups['hazard'].Value, 16)
$mooshWaterHoverFrames = [Convert]::ToInt32(
    $mooshWaterHover.Groups['frames'].Value, 16)
$mooshWaterExclamationZOffset = -[Convert]::ToInt32(
    $mooshWaterHover.Groups['zOffset'].Value, 16)
if ($ghiniNativeSource -notmatch '(?ms)^interactionCode73:.*?wEssencesObtained.*?bit 1.*?wPastRoomFlags\+\$79.*?bit 6.*?wMooshState.*?and \$60.*?interactionSetAlwaysUpdateBit.*?Interaction\.zh.*?-2.*?objectApplySpeed.*?dec a.*?and \$1f.*?cp \$18' -or
    $companionNativeSource -notmatch '(?ms)^companionScript_subid00:.*?wEssencesObtained.*?bit 1.*?wPastRoomFlags\+\$79.*?bit 6.*?wMooshState.*?and \$60.*?wDisableScreenTransitions.*?wDiggingUpEnemiesForbidden' -or
    $companionNativeSource -notmatch '(?ms)^companionScript_subid00_state1:.*?Interaction\.var3a.*?dec a.*?and \$03.*?w1Companion\.xh.*?xor \$02' -or
    $companionSpawnerSource -notmatch '(?ms)^@subid00:.*?wMooshState.*?wEssencesObtained.*?bit 1.*?wPastRoomFlags\+\$79.*?bit 6.*?TREASURE_CHEVAL_ROPE.*?@loadCompanionPresetIfHasntLeft:.*?and \$40' -or
    $companionSpawnerSource -notmatch '(?m)^\s*\.db SPECIALOBJECT_MOOSH,\s+\$28, \$58, \$00 ; \$00 == \[subid\]' -or
    $mooshSpecialSource -notmatch '(?ms)^mooshState0:.*?wMooshState.*?ld a,\$20.*?and \(hl\).*?@gotoCutsceneStateA:.*?ld a,\$0a' -or
    $mooshSpecialSource -notmatch '(?ms)^mooshState1:.*?objectCheckLinkWithinDistance.*?companionTryToMount.*?^mooshState3:.*?companionCheckMountingComplete.*?companionFinalizeMounting.*?^mooshState5:.*?wGameKeysJustPressed.*?BTN_BIT_A.*?mooshPressedAButton.*?BTN_BIT_B.*?companionGotoDismountState.*?SPEED_100.*?companionUpdateMovement' -or
    $mooshSpecialSource -notmatch '(?ms)^mooshState6:.*?^@substate0:.*?ld a,\$01.*?companionDismountAndSavePosition.*?ld c,\$01.*?companionSetAnimation.*?^@substate1:.*?wLinkInAir.*?itemIncSubstate.*?^@substate2:.*?ld c,\$09.*?objectCheckLinkWithinDistance.*?mooshCheckHazards.*?ld a,\$01.*?\[state\] = \$01' -or
    $mooshSpecialSource -notmatch '(?ms)^mooshState8:.*?^mooshState8Substate0:.*?-\$140.*?SPEED_100.*?^mooshState8Substate1:.*?SND_JUMP.*?^mooshState8Substate2:.*?SND_CHARGE_SWORD.*?^mooshState8Substate3:.*?SNDCTRL_STOPSFX.*?SND_SCENT_SEED.*?ITEM_28' -or
    $mooshHelperSource -notmatch '(?ms)^ghiniHarassingMoosh_beginCircularMovement:.*?SPEED_140.*?ANGLE_LEFT' -or
    $mooshHelperSource -notmatch '(?ms)^companionScript_makeExclamationMark:.*?ld bc,\$f000.*?ld a,30.*?objectCreateExclamationMark' -or
    $mooshScriptsSource -notmatch '(?ms)^ghiniHarassingMoosh_subid00Script:.*?TX_1204.*?w1Companion\.var3e, \$01.*?ENEMY_GHINI, \$00.*?^ghiniHarassingMoosh_subid01Script:.*?TX_1205.*?w1Companion\.var3e, \$02.*?TX_1207.*?MUS_MINIBOSS.*?w1Companion\.var3e, \$10.*?^ghiniHarassingMoosh_subid02Script:.*?TX_1206.*?w1Companion\.var3e, \$08' -or
    $mooshScriptsSource -notmatch '(?ms)^companionScript_subid00Script:.*?TX_2200.*?w1Companion\.var3e, \$04.*?wNumEnemies, \$00.*?SND_DING.*?companionScript_restoreMusic.*?w1Companion\.var03, \$02.*?w1Companion\.var3d, \$01.*?TX_2201.*?companionScript_makeExclamationMark.*?wait 60.*?TX_2204.*?TX_2203.*?wMooshState, \$20.*?wLinkObjectIndex.*?TX_2205' -or
    $mooshSpeedSource -notmatch '(?m)^\s*SPEED_140\s+dsb 5 ; 0x32\s*$' -or
    $mooshDirectionSource -notmatch '(?m)^\.define ANGLE_LEFT\s+\$18\s*$' -or
    $mooshWramSource -notmatch '(?m)^wMooshState: ; \$c648/\$c645\s*$' -or
    $mooshWramSource -notmatch '(?m)^wEssencesObtained: ; \$c6bf/\$c6bb\s*$' -or
    $mooshEnemyConstants -notmatch '(?m)^\.define ENEMY_GHINI \$17\s*$' -or
    $mooshTreasureConstants -notmatch '(?m)^\s*TREASURE_CHEVAL_ROPE\s+db ; \$52\s*$' -or
    $mooshSpecialConstants -notmatch '(?m)^\s*SPECIALOBJECT_MOOSH\s+db ; \$0d\s*$' -or
    $mooshMusicConstants -notmatch '(?m)^\s*MUS_MINIBOSS\s+db ; \$2d\s*$' -or
    $mooshMusicConstants -notmatch '(?m)^\s*SND_DING\s+db ; \$c8' -or
    $mooshMusicConstants -notmatch '(?m)^\s*SND_CLINK\s+db ; \$50' -or
    $mooshMusicConstants -notmatch '(?m)^\s*SND_JUMP\s+db ; \$53' -or
    $mooshMusicConstants -notmatch '(?m)^\s*SND_CHARGE_SWORD\s+db ; \$4f' -or
    $mooshMusicConstants -notmatch '(?m)^\s*SND_SCENT_SEED\s+db ; \$85') {
    throw 'Room 0:6c Ghini/Moosh predicates, lanes, or companion handoff changed.'
}
if ($companionSpawnerSource -notmatch '(?ms)^; Moosh saying goodbye after getting cheval rope\s*@subid01:\s*ld hl,wMooshState\s*ld a,\$40\s*and \(hl\)\s*jr nz,@deleteSelf\s*ld a,TREASURE_CHEVAL_ROPE\s*call checkTreasureObtained\s*jr c,@loadCompanionPresetIfHasntLeft' -or
    $companionSpawnerSource -notmatch '(?m)^\s*\.db SPECIALOBJECT_MOOSH,\s+\$48, \$38, \$00 ; \$01\s*$' -or
    $mooshSpecialSource -notmatch '(?ms)^mooshState0:.*?ld a,\$20\s*and \(hl\).*?ld a,\$40\s*and \(hl\)\s*jr nz,@gotoCutsceneStateA.*?TREASURE_CHEVAL_ROPE.*?wActiveRoom\).*?cp \$6b\s*jr nz,@setAnimation' -or
    $mooshSpecialSource -notmatch '(?ms)^@label_05_456:\s*ld a,\$01\s*ld \(wMenuDisabled\),a\s*ld \(wDisabledObjects\),a\s*ld a,\$04\s*ld \(de\),a.*?specialObjectSetAnimation.*?objectSetVisiblec3' -or
    $mooshSpecialSource -notmatch '(?ms)^mooshStateASubstate4:.*?call mooshIncVar03.*?ld bc,TX_2208.*?jp showText.*?^mooshStateASubstate5:.*?call retIfTextIsActive.*?ld bc,-\$140.*?call objectSetSpeedZ.*?ld l,SpecialObject\.angle.*?ld \(hl\),\$10.*?ld l,SpecialObject\.speed.*?ld \(hl\),SPEED_100.*?ld a,\$0b.*?call specialObjectSetAnimation.*?jp mooshIncVar03.*?^mooshStateASubstate6:.*?call specialObjectAnimate.*?ld e,SpecialObject\.speedZ\+1.*?ld a,\(de\).*?or a.*?ld c,\$10.*?jp nz,objectUpdateSpeedZ_paramC.*?call objectApplySpeed.*?ld e,SpecialObject\.yh.*?ld a,\(de\).*?cp \$f0.*?ret c.*?xor a.*?ld \(wDisabledObjects\),a.*?ld \(wMenuDisabled\),a.*?ld \(wRememberedCompanionId\),a.*?ld hl,wMooshState.*?set 6,\(hl\).*?jp itemDelete' -or
    $mooshSpeedSource -notmatch '(?m)^\s*SPEED_100\s+dsb 5 ; 0x28\s*$' -or
    $mooshDirectionSource -notmatch '(?m)^\.define ANGLE_DOWN\s+\$10\s*$' -or
    $mooshWramSource -notmatch '(?m)^wRememberedCompanionId: ; \$cc24/\$cc40\s*$' -or
    -not $allTexts.ContainsKey(0x2208)) {
    throw 'Room 0:6b Moosh goodbye predicate, flight, persistence, or text changed.'
}

$ghiniGraphic = $interactionGraphics['115:0']
$ghiniAnimation = Resolve-NpcAnimation 0x73 0
$exclamationGraphic = $interactionGraphics['159:0']
$exclamationAnimation = Resolve-NpcAnimation 0x9f 0
if ($null -eq $ghiniGraphic -or $ghiniGraphic.Gfx -ne 0x90 -or
    $ghiniGraphic.TileBase -ne 0x16 -or $ghiniGraphic.Palette -ne 2 -or
    [string]::IsNullOrWhiteSpace($ghiniAnimation) -or
    $null -eq $exclamationGraphic -or
    [string]::IsNullOrWhiteSpace($exclamationAnimation)) {
    throw 'Room 0:6c Ghini or exclamation visual data changed.'
}

# Resolve Moosh's complete special-object animation set. Graphics rows replace
# the first N tiles in the object's virtual OBJ window; null rows retain the
# preceding frame's tile contents. This is the same hardware contract used by
# Maple and is required by Moosh's walking, hovering, and charged-stomp frames.
$specialAnimationPath = Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s'
$specialAnimationSource = Read-ImportText $specialAnimationPath
$specialAnimationNodes = @(Read-AssemblyNodes $specialAnimationPath)
$specialObjectCommonSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\commonCode.s')
$specialOamPath = Join-Path $Disassembly 'data\ages\specialObjectOamData.s'
$specialOamNodes = @(Read-AssemblyNodes $specialOamPath)
# These indices retain source order and duplicate labels. An animation label
# is an entry point; it must not truncate frames which cross the next label.
function New-CompanionAnimationIndex([object[]]$animations, [object[]]$oam) {
    $labels = @{}
    $frames = [Collections.Generic.List[object]]::new()
    $loops = [Collections.Generic.List[object]]::new()
    $oamRows = @{}
    foreach ($node in $animations) {
        if ($node.Kind -eq 'Label') {
            if (-not $labels.ContainsKey($node.Name)) { $labels[$node.Name] = [Collections.Generic.List[object]]::new() }
            $labels[$node.Name].Add($node)
        } elseif ($node.Kind -eq 'Data' -and $node.Name -ieq '.db') {
            $frames.Add($node)
        } elseif ($node.Kind -eq 'MacroInvocation' -and $node.Name -eq 'm_AnimationLoop') {
            $loops.Add($node)
        }
    }
    foreach ($node in $oam) {
        if ($node.Kind -ne 'Data' -or $node.Name -ine '.db' -or
            [string]::IsNullOrEmpty($node.EnclosingGlobalLabel)) { continue }
        $label = $node.EnclosingGlobalLabel
        if (-not $oamRows.ContainsKey($label)) { $oamRows[$label] = [Collections.Generic.List[object]]::new() }
        $oamRows[$label].Add($node)
    }
    return @{ Labels = $labels; Frames = $frames; Loops = $loops; Oam = $oamRows }
}
function Get-CompanionAnimationLabels([string]$label) {
    if ($specialAnimationIndex.Labels.ContainsKey($label)) { return @($specialAnimationIndex.Labels[$label]) }
}
function Get-CompanionNodesInRange($nodes, [int]$start, [int]$end, [switch]$First) {
    $lower = 0; $upper = $nodes.Count
    while ($lower -lt $upper) {
        $middle = $lower + [int][Math]::Floor(($upper - $lower) / 2)
        if ($nodes[$middle].Offset -le $start) { $lower = $middle + 1 } else { $upper = $middle }
    }
    for ($index = $lower; $index -lt $nodes.Count -and $nodes[$index].Offset -lt $end; $index++) {
        $nodes[$index]
        if ($First) { break }
    }
}
$specialAnimationIndex = New-CompanionAnimationIndex $specialAnimationNodes $specialOamNodes
$specialEncodedOamCache = @{}
$specialOamTables = Read-AssemblyDwTables `
    $specialAnimationPath 'specialObject(?:08|09|0b|0c|0d|0f|10|11)OamDataPointers' 'oamData[0-9a-f]+'
$mooshOamPointers = $specialOamTables['specialObject0dOamDataPointers']
if ($null -eq $mooshOamPointers) {
    $mooshOamPointers = $specialOamTables['specialObject11OamDataPointers']
}
if ($null -eq $mooshOamPointers -or $mooshOamPointers.Count -ne 0x3f -or
    $specialAnimationSource -notmatch '(?ms)^specialObject0dGfxPointers:.*?m_SpecialObjectGfxPointer \$00 spr_moosh \$0000 \$0e.*?m_SpecialObjectGfxPointer \$3d \$0000.*?^specialObject0dAnimationDataPointers:.*?\.dw animationData1ab51' -or
    $specialAnimationSource -notmatch '(?ms)^animationData1ab51:\s*\.db \$0a \$00 \$00\s*\.db \$0a \$00 \$00\s*\.db \$14 \$00 \$00\s*\.db \$0a \$3d \$00\s*\.db \$0a \$3d \$00\s*\.db \$14 \$3d \$00\s*m_AnimationLoop animationData1ab51') {
    throw 'Moosh special-object animation $00 changed.'
}
function Resolve-MooshOam([string]$label) {
    if ($specialEncodedOamCache.ContainsKey($label)) { return $specialEncodedOamCache[$label] }
    $rows = @()
    if ($specialAnimationIndex.Oam.ContainsKey($label)) { $rows = @($specialAnimationIndex.Oam[$label]) }
    if ($rows.Count -lt 2) { throw "Moosh OAM block $label is incomplete." }
    $count = Convert-AssemblyInteger $rows[0].Operands[0]
    if ($rows.Count -ne $count + 1) {
        throw "Moosh OAM block $label declares $count cells but has $($rows.Count - 1)."
    }
    $encoded = @($rows | Select-Object -Skip 1 | ForEach-Object {
        if ($_.Operands.Count -ne 4) {
            throw "Moosh OAM block $label has a malformed cell at line $($_.Line)."
        }
        ($_.Operands | ForEach-Object { Convert-AssemblyInteger $_ }) -join ','
    }) -join ';'
    $specialEncodedOamCache[$label] = $encoded
    return $encoded
}
$mooshGfxStart = $specialAnimationSource.IndexOf(
    'specialObject0dGfxPointers:', [StringComparison]::Ordinal)
$mooshAnimationsStart = $specialAnimationSource.IndexOf(
    'specialObject0dAnimationDataPointers:', [StringComparison]::Ordinal)
$mooshOamStart = $specialAnimationSource.IndexOf(
    'specialObject0dOamDataPointers:', [StringComparison]::Ordinal)
if ($mooshGfxStart -lt 0 -or $mooshAnimationsStart -le $mooshGfxStart -or
    $mooshOamStart -le $mooshAnimationsStart) {
    throw 'Could not isolate Moosh special-object visual tables.'
}
$mooshGfxOffsets = @{}
$mooshGfxCounts = @{}
foreach ($line in ($specialAnimationSource.Substring(
    $mooshGfxStart, $mooshAnimationsStart - $mooshGfxStart) -split '\r?\n')) {
    if ($line -match 'm_SpecialObjectGfxPointer\s+\$(?<index>[0-9a-f]{2})\s+spr_moosh\s+\$(?<offset>[0-9a-f]{4})\s+\$(?<size>[0-9a-f]{2})') {
        $index = [Convert]::ToInt32($Matches['index'], 16)
        $mooshGfxOffsets[$index] =
            [Convert]::ToInt32($Matches['offset'], 16) / 16
        $mooshGfxCounts[$index] =
            [Convert]::ToInt32($Matches['size'], 16)
    } elseif ($line -match 'm_SpecialObjectGfxPointer\s+\$(?<index>[0-9a-f]{2})\s+\$0000') {
        $index = [Convert]::ToInt32($Matches['index'], 16)
        $mooshGfxOffsets[$index] = 0
        $mooshGfxCounts[$index] = 0
    }
}
$mooshAnimationLabels = @(
    [regex]::Matches(
        $specialAnimationSource.Substring(
            $mooshAnimationsStart, $mooshOamStart - $mooshAnimationsStart),
        '(?m)^\s*\.dw\s+(?<label>animationData[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
if ($mooshGfxOffsets.Count -ne 0x3f -or
    $mooshAnimationLabels.Count -ne 27) {
    throw "Expected 63 Moosh graphics rows and 27 animations; got $($mooshGfxOffsets.Count)/$($mooshAnimationLabels.Count)."
}
function Resolve-MooshOamTiles(
    [string]$encoded,
    [int[]]$vramTiles,
    [string]$animationLabel,
    [int]$gfxIndex) {
    if ([string]::IsNullOrEmpty($encoded)) { return '' }
    return (@($encoded -split ';' | ForEach-Object {
        $fields = $_ -split ','
        if ($fields.Count -ne 4) { throw "Malformed Moosh OAM block: $_" }
        $tile = [int]$fields[2]
        if ($tile -lt 0 -or $tile -ge 0xff -or
            $vramTiles[$tile] -lt 0 -or
            $vramTiles[$tile + 1] -ne $vramTiles[$tile] + 1) {
            throw "$animationLabel graphic `$$($gfxIndex.ToString('x2')) references unresolved Moosh VRAM tile `$$($tile.ToString('x2'))."
        }
        "$($fields[0]),$($fields[1]),$($vramTiles[$tile]),$($fields[3])"
    }) -join ';')
}
function Resolve-MooshSpecialAnimation([string]$label) {
    $start = @(Get-CompanionAnimationLabels $label)
    if ($start.Count -ne 1) { throw "Could not resolve Moosh animation $label." }
    $loop = @(Get-CompanionNodesInRange $specialAnimationIndex.Loops $start[0].Offset $mooshOamStart -First)
    # Bit 7 is the handler's terminal signal for falling/stomp sequences.
    $terminal = @(Get-CompanionNodesInRange $specialAnimationIndex.Frames $start[0].Offset $mooshOamStart | Where-Object {
        $_.Operands.Count -eq 3 -and
        ((Convert-AssemblyInteger $_.Operands[2]) -band 0x80) -ne 0
    } | Select-Object -First 1)
    $end = if ($loop.Count -eq 1) { $loop[0].Offset } else { $mooshOamStart }
    if ($terminal.Count -eq 1 -and $terminal[0].Offset -lt $end) { $end = $terminal[0].Offset + 1; $loop = @() }
    if ($end -eq $mooshOamStart) { throw "Moosh animation $label has neither loop nor terminal signal." }
    $frameNodes = @(Get-CompanionNodesInRange $specialAnimationIndex.Frames $start[0].Offset $end)
    $loopStart = 0
    if ($loop.Count -eq 1) {
        $target = @(Get-CompanionAnimationLabels $loop[0].Operands[0])
        if ($target.Count -ne 1 -or $target[0].Offset -lt $start[0].Offset -or $target[0].Offset -gt $loop[0].Offset) {
            throw "Moosh animation $label has an invalid loop target."
        }
        $loopStart = @($frameNodes | Where-Object { $_.Offset -lt $target[0].Offset }).Count
    }
    $frames = [Collections.Generic.List[string]]::new()
    $vramTiles = [int[]]::new(0x100)
    for ($tile = 0; $tile -lt $vramTiles.Length; $tile++) {
        $vramTiles[$tile] = -1
    }
    foreach ($frame in $frameNodes) {
        if ($frame.Operands.Count -ne 3) { throw "Moosh animation $label has a malformed frame at line $($frame.Line)." }
        $duration = Convert-AssemblyInteger $frame.Operands[0]
        $gfx = Convert-AssemblyInteger $frame.Operands[1]
        $parameter = Convert-AssemblyInteger $frame.Operands[2]
        if (-not $mooshGfxOffsets.ContainsKey($gfx) -or
            $gfx -ge $mooshOamPointers.Count) {
            throw "$label references missing Moosh graphic/OAM index `$$($gfx.ToString('x2'))."
        }
        $loadedOffset = [int]$mooshGfxOffsets[$gfx]
        $loadedCount = [int]$mooshGfxCounts[$gfx]
        for ($tile = 0; $tile -lt $loadedCount; $tile++) {
            $vramTiles[$tile] = $loadedOffset + $tile
        }
        $rawOam = Resolve-MooshOam $mooshOamPointers[$gfx]
        $oam = Resolve-MooshOamTiles $rawOam $vramTiles $label $gfx
        $metadata = if ($parameter -eq 0) { "$duration" } else { "$duration,$parameter" }
        $frames.Add("$metadata@$oam")
    }
    if ($frames.Count -eq 0) { throw "Moosh animation $label has no frames." }
    $encoded = $frames -join '|'
    if ($loopStart -gt 0) { $encoded += "~$loopStart" }
    return $encoded
}
$mooshAnimations = @($mooshAnimationLabels | ForEach-Object {
    Resolve-MooshSpecialAnimation $_
})
$mooshAnimation = $mooshAnimations[0]

# Link's w1Link graphics index is copied from Moosh's animation parameter.
# Import every parameter Ricky or Moosh can emit ($00-$32) from
# SPECIALOBJECT_LINK's riding table. Keep the graphics source byte offset separate from the raw
# 8-bit OAM tile: folding spr_link+$2100 into tile $210 would wrap to tile $10
# in the compositor and incorrectly select Link's fall-in-hole graphic.
$linkGfxStart = $specialAnimationSource.IndexOf(
    'specialObject09GfxPointers:', [StringComparison]::Ordinal)
$linkGfxEnd = $specialAnimationSource.IndexOf(
    'specialObject0eGfxPointers:', $linkGfxStart,
    [StringComparison]::Ordinal)
$linkGfxRows = @([regex]::Matches(
    $specialAnimationSource.Substring($linkGfxStart, $linkGfxEnd - $linkGfxStart),
    '(?m)^\s*m_SpecialObjectGfxPointer\s+\$(?<oam>[0-9a-f]{2})\s+spr_link\s+\$(?<offset>[0-9a-f]{4})\s+\$(?<size>[0-9a-f]{2})'))
$linkRetainedGfxRow = [regex]::Match(
    $specialAnimationSource.Substring($linkGfxStart, $linkGfxEnd - $linkGfxStart),
    '(?m)^\s*m_SpecialObjectGfxPointer\s+\$(?<oam>[0-9a-f]{2})\s+\$0000\s*$')
$linkOamPointers = $specialOamTables['specialObject09OamDataPointers']
if ($linkGfxRows.Count -ne 0x32 -or -not $linkRetainedGfxRow.Success -or
    $null -eq $linkOamPointers -or
    $linkOamPointers.Count -ne 0x30) {
    throw 'Link riding graphics/OAM tables no longer cover companion parameters $00-$32.'
}

# SPECIALOBJECT_LINK_CUTSCENE $08 uses the full Link graphics table.
# linkCutscene7 selects animation $14: Link lies face-down for 180 updates,
# then switches to graphic $56 and holds that passed-out pose.
$linkCutsceneGfxEnd = $specialAnimationSource.IndexOf(
    'specialObject02GfxPointers:', [StringComparison]::Ordinal)
$linkCutsceneGfxRows = @([regex]::Matches(
    $specialAnimationSource.Substring(0, $linkCutsceneGfxEnd),
    '(?m)^\s*m_SpecialObjectGfxPointer\s+\$(?<oam>[0-9a-f]{2})\s+spr_link\s+\$(?<offset>[0-9a-f]{4})\s+\$(?<size>[0-9a-f]{2})'))
$linkCutsceneAnimation = [regex]::Match(
    $specialAnimationSource,
    '(?ms)^specialObject08AnimationDataPointers:.*?^(?:\s*\.dw animationData[0-9a-f]+\s*){20}\s*\.dw animationData19e38.*?^animationData19e38:\s*\.db \$b4 \$04 \$00\s*animationLoop19e3b:\s*\.db \$7f \$56 \$00\s*m_AnimationLoop animationLoop19e3b')
$linkCutsceneOamPointers =
    $specialOamTables['specialObject08OamDataPointers']
if ($linkCutsceneGfxRows.Count -ne 0x100 -or
    -not $linkCutsceneAnimation.Success -or
    $null -eq $linkCutsceneOamPointers -or
    $linkCutsceneOamPointers.Count -ne 0x30) {
    throw 'SPECIALOBJECT_LINK_CUTSCENE animation $14 graphics contract changed.'
}
$tokayLinkFrameBytes = @(
    @(0xb4, 0x04, 0x00),
    @(0x7f, 0x56, 0x00))
$tokayLinkRows = [Collections.Generic.List[string]]::new()
$tokayLinkRows.Add("# index`tduration`tgfx-index`tanim-parameter`tsource-offset`tbase-palette`toam-parts`tloop-start`tsource")
for ($index = 0; $index -lt $tokayLinkFrameBytes.Count; $index++) {
    $frame = $tokayLinkFrameBytes[$index]
    $gfxIndex = $frame[1]
    $gfx = $linkCutsceneGfxRows[$gfxIndex]
    $oamIndex = [Convert]::ToInt32($gfx.Groups['oam'].Value, 16)
    $sourceOffset = [Convert]::ToInt32($gfx.Groups['offset'].Value, 16)
    $tileCount = [Convert]::ToInt32($gfx.Groups['size'].Value, 16)
    if ($oamIndex -ge $linkCutsceneOamPointers.Count) {
        throw "Link cutscene graphic `$$($gfxIndex.ToString('x2')) has missing OAM `$$($oamIndex.ToString('x2'))."
    }
    $rawOam = Resolve-MooshOam $linkCutsceneOamPointers[$oamIndex]
    $encodedOam = @(($rawOam -split ';') | ForEach-Object {
        $fields = $_ -split ','
        $tile = [int]$fields[2]
        if ($fields.Count -ne 4 -or $tile -lt 0 -or $tile + 1 -ge $tileCount) {
            throw "Link cutscene graphic `$$($gfxIndex.ToString('x2')) has unresolved OAM tile `$$($tile.ToString('x2'))."
        }
        return @($fields | ForEach-Object { ([int]$_).ToString('x2') }) -join ','
    }) -join ';'
    $tokayLinkRows.Add((@(
        $index.ToString(), $frame[0].ToString(), $gfxIndex.ToString('x2'),
        $frame[2].ToString('x2'), $sourceOffset.ToString('x4'), '0', $encodedOam,
        '1', 'linkInCutscene.s:linkCutscene7;specialObjectAnimationData.s:animationData19e38/specialObject08GfxPointers') -join "`t"))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\tokay_theft_link_visual.tsv'),
    $tokayLinkRows)

$mooshLinkFrames = [Collections.Generic.List[string]]::new()
$mooshLinkSourceOffsets = [Collections.Generic.List[string]]::new()
for ($index = 0; $index -le 0x32; $index++) {
    # Parameter $32's null graphics row follows $2f in Ricky's down-facing
    # animation. The hardware retains those loaded tiles while switching to
    # OAM $15, so preserve that physical source row explicitly.
    $row = if ($index -eq 0x32) { $linkGfxRows[0x2f] } else { $linkGfxRows[$index] }
    $oamIndex = if ($index -eq 0x32) {
        [Convert]::ToInt32($linkRetainedGfxRow.Groups['oam'].Value, 16)
    } else {
        [Convert]::ToInt32($row.Groups['oam'].Value, 16)
    }
    $sourceOffset = [Convert]::ToInt32($row.Groups['offset'].Value, 16)
    $tileCount = [Convert]::ToInt32($row.Groups['size'].Value, 16)
    $rawOam = Resolve-MooshOam $linkOamPointers[$oamIndex]
    @($rawOam -split ';' | ForEach-Object {
        $fields = $_ -split ','
        if ($fields.Count -ne 4) {
            throw "Link riding graphic `$$($index.ToString('x2')) has malformed OAM data."
        }
        $tile = [int]$fields[2]
        if ($tile -lt 0 -or $tile + 1 -ge $tileCount) {
            throw "Link riding graphic `$$($index.ToString('x2')) has unresolved OAM tile `$$($tile.ToString('x2'))."
        }
    }) | Out-Null
    $mooshLinkFrames.Add("127@$rawOam")
    $mooshLinkSourceOffsets.Add($sourceOffset.ToString('x4'))
}
Copy-GeneratedFile 'gfx\common\spr_moosh.png' 'gfx\spr_moosh.png'

$mooshGoodbyeRows = @(
    "# group`troom`tcontroller-id`tcontroller-subid`tcontroller-y`tcontroller-x`tspawner-id`tspawner-subid`tmoosh-id`tmoosh-y`tmoosh-x`ttreasure-id`tmoosh-state-address`trescued-mask`tleft-mask`tdisabled-objects`tmenu-disabled`tinitial-animation`tflight-animation`tinitial-speed-z`tflight-gravity`tflight-speed`tflight-angle`texit-y`ttext-id`ttext-base64`tsource",
    (@(
        '0','6b','71','01',
        $mooshGoodbyePlacement.Groups['controllery'].Value,
        $mooshGoodbyePlacement.Groups['controllerx'].Value,
        '67','01','0d','48','38','52','c648','20','40','01','01','01','0b',
        '-320','10','28','10','f0','2208',
        [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x2208])),
        'mainData.s:group0Map6bObjectData;companionSpawner.s:@subid01;moosh.s:mooshState0/mooshStateA'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\moosh_goodbye_event.tsv'),
    $mooshGoodbyeRows)

# Present room 0:6a Ricky glove interaction. The placed companion spawner
# installs SPECIALOBJECT_RICKY at a fixed preset before INTERAC_COMPANION_SCRIPTS
# runs its interactionRunScript stream. Import the complete script graph and
# Ricky's special-object visuals so production never reads the disassembly.
$rickyPlacement = [regex]::Match(
    $mooshObjectSource,
    '(?ms)^group0Map6aObjectData:\s*' +
    'obj_Interaction \$31 \$00 \$38 \$48\s*' +
    'obj_Interaction \$dc \$08 \$48 \$02\s*' +
    'obj_Interaction \$71 \$04 \$08 \$58\s*' +
    'obj_Interaction \$71 \$05 \$40 \$98\s*' +
    'obj_Interaction \$67 \$02\s*' +
    'obj_Interaction \$71 \$03\s*obj_End')
if (-not $rickyPlacement.Success) {
    throw 'Room 0:6a Ricky spawner/controller order changed.'
}
$rickyPlacementRows = @("# order`tid`tsubid`ty`tx`tsource")
$rickyOrder = 0
foreach ($entry in [regex]::Matches($rickyPlacement.Value,
    '(?m)^\s*obj_Interaction \$(?<id>[0-9a-f]{2}) \$(?<subid>[0-9a-f]{2})(?: \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}))?\s*$')) {
    $y = if ($entry.Groups['y'].Success) { $entry.Groups['y'].Value } else { '00' }
    $x = if ($entry.Groups['x'].Success) { $entry.Groups['x'].Value } else { '00' }
    $rickyPlacementRows += "$rickyOrder`t$($entry.Groups['id'].Value)`t$($entry.Groups['subid'].Value)`t$y`t$x`tmainData.s:group0Map6aObjectData"
    $rickyOrder++
}
if ($rickyOrder -ne 6) { throw 'Room0:6a requires its complete six-interaction source stream.' }
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\ricky_gloves_placements.tsv'), $rickyPlacementRows)
$rickySpecialSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\ricky.s')
$rickyCollisionSource = Read-ImportText (
    Join-Path $Disassembly 'code\bank0.s')
$rickyScriptingSource = Read-ImportText (
    Join-Path $Disassembly 'code\scripting.s')
if ($rickyScriptingSource -notmatch '(?ms)^scriptCmd_jumpIfMemorySet:.*?and \(hl\)\s*jp z,scriptFunc_add3ToHl\s*inc hl\s*jp scriptFunc_jump_scf' -or
    $rickyScriptingSource -notmatch '(?ms)^scriptCmd_df:\s*pop hl\s*inc hl\s*ldi a,\(hl\)\s*call checkTreasureObtained\s*ld \(\$cfc1\),a\s*jr nc,\+\s*jp scriptFunc_jump\s*\+\s*inc hl\s*inc hl\s*ret') {
    throw 'Ricky glove bit/query branch carry behavior or related-byte scratch write changed.'
}
$rickyAttackItemSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\rickyMooshAttack.s')
$rickyTornadoItemSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\rickyTornado.s')
$rickyItemConstants = Read-ImportText (
    Join-Path $Disassembly 'constants\common\items.s')
$rickyBreakSourceConstants = Read-ImportText (
    Join-Path $Disassembly 'constants\common\breakableTileSources.s')
$rickyTileIndexConstants = Read-ImportText (
    Join-Path $Disassembly 'constants\common\tileIndices.s')
$rickyItemDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\itemData.s')
$rickyItemAttributesSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\itemAttributes.s')
$rickyItemAnimationsSource = Read-ImportText (
    Join-Path $Disassembly 'data\itemAnimations.s')
$rickyItemOamSource = Read-ImportText (
    Join-Path $Disassembly 'data\itemOamData.s')
$rickyFixedGfxHeaderSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\gfxHeaders.s')
$rickyGlobalFlagSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\globalFlags.s')
if ($companionSpawnerSource -notmatch '(?ms)^; Ricky looking for gloves\s*@subid02:.*?GLOBALFLAG_GAVE_ROPE_TO_RAFTON.*?wRickyState.*?@loadCompanionPresetIfHasntLeft:.*?and \$40' -or
    $companionSpawnerSource -notmatch '(?m)^\s*\.db SPECIALOBJECT_RICKY,\s+\$40, \$50, \$00 ; \$02\s*$' -or
    $companionNativeSource -notmatch '(?ms)^companionScript_subid03:.*?wRickyState.*?and \$20.*?mainScripts\.companionScript_subid03Script' -or
    $rickySpecialSource -notmatch '(?ms)^rickyState0:.*?wRickyState.*?bit 7.*?bit 6.*?and \$20.*?ld \(hl\),\$0a.*?objectAddToAButtonSensitiveObjectList' -or
    $rickySpecialSource -notmatch '(?ms)^rickyStateASubstate1:.*?objectRemoveFromAButtonSensitiveObjectList.*?companionForceMount' -or
    $mooshHelperSource -notmatch '(?ms)^companionScript_loseRickyGloves:\s*ld a,TREASURE_RICKY_GLOVES\s+jp loseTreasure' -or
    $mooshWramSource -notmatch '(?m)^wAnimalCompanion: ; \$c610\s*$' -or
    $mooshWramSource -notmatch '(?m)^wRickyState: ; \$c646/\$c643\s*$' -or
    $rickyGlobalFlagSource -notmatch '(?m)^\s*GLOBALFLAG_GAVE_ROPE_TO_RAFTON\s+db ; \$15\s*$' -or
    $mooshTreasureConstants -notmatch '(?m)^\s*TREASURE_RICKY_GLOVES\s+db ; \$48\s*$' -or
    $mooshSpecialConstants -notmatch '(?m)^\s*SPECIALOBJECT_RICKY\s+db ; \$0b\s*$' -or
    $mooshMusicConstants -notmatch '(?m)^\s*SND_RICKY\s+db ; \$c3') {
    throw 'Room 0:6a Ricky predicate, preset, script owner, or constants changed.'
}

$rickyOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'checkmemoryeq', 'disableinput', 'jumpifmemoryset', 'ormemory',
    'jumpifmemoryeq', 'showtext', 'scriptjump', 'jumpifitemobtained',
    'writememory', 'enableinput', 'asm15', 'enableallobjects',
    'enablemenu', 'scriptend')) {
    [void]$rickyOpcodes.Add($opcode)
}
$rickyCommands = @(Read-AssemblyCutsceneCommands `
    (Join-Path $Disassembly 'scripts\ages\scriptHelper.s') `
    'companionScript_subid03Script_body' $rickyOpcodes)
$rickyExpected = @(
    @('checkmemoryeq', 'w1Companion.var3d, $01'),
    @('disableinput', ''),
    @('jumpifmemoryset', 'wRickyState, $01, @alreadyExplainedSituation'),
    @('ormemory', 'wRickyState, $01'),
    @('jumpifmemoryeq', 'wAnimalCompanion, SPECIALOBJECT_RICKY, @notFirstMeeting'),
    @('showtext', 'TX_2000'),
    @('scriptjump', '@alreadyExplainedSituation'),
    @('showtext', 'TX_2001'),
    @('jumpifitemobtained', 'TREASURE_RICKY_GLOVES, @retrievedGloves'),
    @('showtext', 'TX_2003'),
    @('writememory', 'w1Companion.var3d, $00'),
    @('enableinput', ''),
    @('scriptjump', 'mainScripts.companionScript_subid03Script'),
    @('showtext', 'TX_2004'),
    @('asm15', 'companionScript_loseRickyGloves'),
    @('writememory', 'w1Companion.var03, $01'),
    @('enableallobjects', ''),
    @('checkmemoryeq', 'wLinkObjectIndex, >w1Companion'),
    @('showtext', 'TX_2005'),
    @('ormemory', 'wRickyState, $20'),
    @('enablemenu', ''),
    @('scriptend', '')
)
if ($rickyCommands.Count -ne $rickyExpected.Count) {
    throw "companionScript_subid03Script_body expected 22 commands, parsed $($rickyCommands.Count)."
}
for ($index = 0; $index -lt $rickyExpected.Count; $index++) {
    $actualOperands = if ($null -eq $rickyCommands[$index].Operands) {
        ''
    } else {
        ([string]$rickyCommands[$index].Operands).Trim()
    }
    if ($rickyCommands[$index].Opcode -ne $rickyExpected[$index][0] -or
        $actualOperands -ne $rickyExpected[$index][1]) {
        throw "Ricky glove script command $index changed from " +
            "$($rickyExpected[$index][0]) '$($rickyExpected[$index][1])' to " +
            "$($rickyCommands[$index].Opcode) '$actualOperands'."
    }
}
$rickyTargets = @{}
foreach ($command in $rickyCommands) {
    if (-not $rickyTargets.ContainsKey($command.Label)) {
        $rickyTargets[$command.Label] = $command.Index
    }
}
foreach ($entry in @(
    @('@notFirstMeeting', 7),
    @('@alreadyExplainedSituation', 8),
    @('@retrievedGloves', 13))) {
    if (-not $rickyTargets.ContainsKey($entry[0]) -or
        $rickyTargets[$entry[0]] -ne $entry[1]) {
        throw "Ricky glove script label $($entry[0]) moved from command $($entry[1])."
    }
}
foreach ($textId in @(0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Missing Ricky glove text TX_$($textId.ToString('x4'))."
    }
}
$rickyIntroText = $allTexts[0x2000].Replace(
    '\jump(TX_2002)', $allTexts[0x2002])
$rickyCommandRows = [Collections.Generic.List[string]]::new()
$rickyCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
foreach ($command in $rickyCommands) {
    $opcode = $command.Opcode
    $actor = ''
    $arg0 = ''
    $arg1 = ''
    $payload = ''
    switch ($command.Index) {
        0 { $opcode = 'checkmemoryeq'; $arg0 = '01'; $payload = 'RickyButton' }
        1 { }
        2 { $opcode = 'jumpifmemoryeqyieldonmiss'; $arg0 = '01'; $arg1 = '8'; $payload = 'RickyTalked' }
        3 { $opcode = 'writememory'; $arg0 = '01'; $payload = 'RickyStateOr' }
        4 { $opcode = 'jumpifmemoryeq'; $arg0 = '0b'; $arg1 = '7'; $payload = 'AnimalCompanion' }
        5 { $payload = $rickyIntroText; $arg0 = '2000' }
        6 { $opcode = 'scriptjumpyield'; $arg0 = '8' }
        7 { $payload = $allTexts[0x2001]; $arg0 = '2001' }
        8 { $opcode = 'jumpifmemoryeqyield'; $arg0 = '01'; $arg1 = '13'; $payload = 'HasRickyGloves' }
        9 { $payload = $allTexts[0x2003]; $arg0 = '2003' }
        10 { $opcode = 'writememory'; $arg0 = '00'; $payload = 'RickyButton' }
        11 { }
        12 { $arg0 = '0' }
        13 { $payload = $allTexts[0x2004]; $arg0 = '2004' }
        14 { $opcode = 'native'; $payload = 'LoseRickyGloves' }
        15 { $opcode = 'writememory'; $arg0 = '01'; $payload = 'RickyVar03' }
        16 { $opcode = 'setdisabledobjectscontinue'; $arg0 = '00' }
        17 { $opcode = 'checkmemoryeq'; $arg0 = '01'; $payload = 'RickyMounted' }
        18 { $payload = $allTexts[0x2005]; $arg0 = '2005' }
        19 { $opcode = 'writememory'; $arg0 = '20'; $payload = 'RickyStateOr' }
        20 { }
        21 { }
        default { throw "Unexpected Ricky command index $($command.Index)." }
    }
    $rickyCommandRows.Add((New-CutsceneCommandRow `
        $command.Script $command.Index $command.Label $command.Line `
        $opcode $actor "$arg0" "$arg1" $payload))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\ricky_gloves_commands.tsv'),
    $rickyCommandRows)

$rickyOamPointers = $specialOamTables['specialObject0bOamDataPointers']
if ($null -eq $rickyOamPointers) {
    $rickyOamPointers = $specialOamTables['specialObject0fOamDataPointers']
}
$rickyGfxStart = $specialAnimationSource.IndexOf(
    'specialObject0bGfxPointers:', [StringComparison]::Ordinal)
$rickyAnimationsStart = $specialAnimationSource.IndexOf(
    'specialObject0bAnimationDataPointers:', [StringComparison]::Ordinal)
$rickyOamStart = $specialAnimationSource.IndexOf(
    'specialObject0bOamDataPointers:', [StringComparison]::Ordinal)
if ($null -eq $rickyOamPointers -or $rickyOamPointers.Count -ne 0x34 -or
    $rickyGfxStart -lt 0 -or $rickyAnimationsStart -le $rickyGfxStart -or
    $rickyOamStart -le $rickyAnimationsStart) {
    throw 'Could not isolate Ricky special-object visual tables.'
}
$rickyOamVariables = [regex]::Match(
    $specialObjectCommonSource,
    '(?m)^\s*\.db\s+\$(?<tilebase>[0-9a-f]{2})\s+\$(?<flags>[0-9a-f]{2})\s*;\s*0x0b')
if (-not $rickyOamVariables.Success -or
    $rickyOamVariables.Groups['tilebase'].Value -ne '60' -or
    $rickyOamVariables.Groups['flags'].Value -ne '0b') {
    throw 'SPECIALOBJECT_RICKY $0b OAM tile-base/palette initialization changed.'
}
$rickyPalette =
    [Convert]::ToInt32($rickyOamVariables.Groups['flags'].Value, 16) -band 0x07
$rickyGfxOffsets = @{}
$rickyGfxCounts = @{}
$rickyOamIndices = @{}
$rickyGfxIndex = 0
foreach ($line in ($specialAnimationSource.Substring(
    $rickyGfxStart, $rickyAnimationsStart - $rickyGfxStart) -split '\r?\n')) {
    if ($line -match 'm_SpecialObjectGfxPointer\s+\$(?<oam>[0-9a-f]{2})\s+spr_ricky\s+\$(?<offset>[0-9a-f]{4})\s+\$(?<size>[0-9a-f]{2})') {
        $rickyOamIndices[$rickyGfxIndex] =
            [Convert]::ToInt32($Matches['oam'], 16)
        $rickyGfxOffsets[$rickyGfxIndex] =
            [Convert]::ToInt32($Matches['offset'], 16) / 16
        $rickyGfxCounts[$rickyGfxIndex] =
            [Convert]::ToInt32($Matches['size'], 16)
        $rickyGfxIndex++
    } elseif ($line -match 'm_SpecialObjectGfxPointer\s+\$(?<oam>[0-9a-f]{2})\s+\$0000') {
        $rickyOamIndices[$rickyGfxIndex] =
            [Convert]::ToInt32($Matches['oam'], 16)
        $rickyGfxOffsets[$rickyGfxIndex] = 0
        $rickyGfxCounts[$rickyGfxIndex] = 0
        $rickyGfxIndex++
    }
}
$rickyAnimationLabels = @(
    [regex]::Matches(
        $specialAnimationSource.Substring(
            $rickyAnimationsStart, $rickyOamStart - $rickyAnimationsStart),
        '(?m)^\s*\.dw\s+(?<label>animationData[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
if ($rickyGfxOffsets.Count -ne 0x34 -or
    $rickyOamIndices.Count -ne 0x34 -or
    $rickyAnimationLabels.Count -ne 37) {
    throw "Expected 52 Ricky graphics/OAM rows and 37 animations; got $($rickyGfxOffsets.Count)/$($rickyOamIndices.Count)/$($rickyAnimationLabels.Count)."
}
function Resolve-CompanionSpecialAnimation(
    [string]$label, [string]$companionName, [hashtable]$gfxOffsets,
    [hashtable]$gfxCounts, [hashtable]$oamIndices, [array]$oamPointers,
    [int]$oamStart) {
    $startLabel = @(Get-CompanionAnimationLabels $label)
    if ($startLabel.Count -ne 1) {
        throw "Could not resolve $companionName animation label $label."
    }
    # Labels are alternate entry points, not stream terminators. In
    # particular Ricky animation $04 falls through animationData1a60c.
    $loopNodes = @(Get-CompanionNodesInRange $specialAnimationIndex.Loops $startLabel[0].Offset $oamStart -First)
    if ($loopNodes.Count -ne 1) {
        throw "$companionName animation $label has no loop terminator before its OAM table."
    }
    $endOffset = $loopNodes[0].Offset
    $frameNodes = @(Get-CompanionNodesInRange $specialAnimationIndex.Frames $startLabel[0].Offset $endOffset | Where-Object {
        $_.Operands.Count -ge 3
    })
    $loopStart = 0
    if ($loopNodes.Count -eq 1) {
        $target = $loopNodes[0].Operands[0]
        $targetLabel = @(Get-CompanionAnimationLabels $target)
        if ($targetLabel.Count -ne 1 -or
            $targetLabel[0].Offset -lt $startLabel[0].Offset -or
            $targetLabel[0].Offset -gt $loopNodes[0].Offset) {
            throw "$companionName animation $label loops to invalid label $target."
        }
        $loopStart = @($frameNodes | Where-Object {
            $_.Offset -lt $targetLabel[0].Offset
        }).Count
    }
    $frames = [Collections.Generic.List[string]]::new()
    $sourceOffsets = [Collections.Generic.List[string]]::new()
    $vramTiles = [int[]]::new(0x100)
    for ($tile = 0; $tile -lt $vramTiles.Length; $tile++) { $vramTiles[$tile] = -1 }
    foreach ($frame in $frameNodes) {
        $duration = Convert-AssemblyInteger $frame.Operands[0]
        $gfx = Convert-AssemblyInteger $frame.Operands[1]
        $parameter = Convert-AssemblyInteger $frame.Operands[2]
        if (-not $gfxOffsets.ContainsKey($gfx) -or
            -not $oamIndices.ContainsKey($gfx)) {
            throw "$label references missing $companionName graphics row `$$($gfx.ToString('x2'))."
        }
        $oamIndex = [int]$oamIndices[$gfx]
        if ($oamIndex -ge $oamPointers.Count) {
            throw "$label graphics row `$$($gfx.ToString('x2')) references missing $companionName OAM index `$$($oamIndex.ToString('x2'))."
        }
        $loadedOffset = [int]$gfxOffsets[$gfx]
        $loadedCount = [int]$gfxCounts[$gfx]
        for ($tile = 0; $tile -lt $loadedCount; $tile++) {
            $vramTiles[$tile] = $loadedOffset + $tile
        }
        # The animation byte selects a physical graphics row. The first byte
        # emitted by m_SpecialObjectGfxPointer then selects the OAM layout;
        # those indexes intentionally diverge for several $companionName poses.
        $rawOam = Resolve-MooshOam $oamPointers[$oamIndex]
        # Resolve each hardware OAM tile through the live VRAM map. Several
        # $companionName rows load only the first two 8x16 cells and retain the remaining
        # cells from the preceding row. Rebase that mixed set from its lowest
        # absolute source tile, rather than assuming every cell belongs to the
        # latest load. The separate byte offset also keeps hardware-relative
        # tile values in range for source rows above $0fff.
        $resolvedOam = Resolve-MooshOamTiles $rawOam $vramTiles $label $gfx
        $resolvedBlocks = @($resolvedOam -split ';' | ForEach-Object {
            $fields = $_ -split ','
            [pscustomobject]@{
                Fields = $fields
                Tile = [int]$fields[2]
            }
        })
        $sourceTileBase = [int](
            $resolvedBlocks | Measure-Object -Property Tile -Minimum).Minimum
        if (($sourceTileBase -band 1) -ne 0) {
            throw "$label graphic `$$($gfx.ToString('x2')) resolved to odd $companionName source tile `$$($sourceTileBase.ToString('x2'))."
        }
        $oam = (@($resolvedBlocks | ForEach-Object {
            $relativeTile = $_.Tile - $sourceTileBase
            if ($relativeTile -lt 0 -or $relativeTile -gt 0xfe) {
                throw "$label graphic `$$($gfx.ToString('x2')) spans more than one $companionName OAM tile byte."
            }
            $_.Fields[2] = $relativeTile.ToString()
            $_.Fields -join ','
        }) -join ';')
        $metadata = if ($parameter -eq 0) { "$duration" } else { "$duration,$parameter" }
        $frames.Add("$metadata@$oam")
        $sourceOffsets.Add(($sourceTileBase * 16).ToString('x'))
    }
    if ($frames.Count -eq 0) { throw "$companionName animation $label has no frames." }
    $encoded = $frames -join '|'
    if ($loopStart -gt 0) { $encoded += "~$loopStart" }
    return [pscustomobject]@{
        Encoded = $encoded
        SourceOffsets = $sourceOffsets.ToArray()
    }
}
$rickyResolvedAnimations = @($rickyAnimationLabels | ForEach-Object {
    Resolve-CompanionSpecialAnimation $_ Ricky $rickyGfxOffsets $rickyGfxCounts `
        $rickyOamIndices $rickyOamPointers $rickyOamStart
})
$rickyAnimations = @($rickyResolvedAnimations | ForEach-Object { $_.Encoded })
$rickyAnimationSourceOffsets = @($rickyResolvedAnimations | ForEach-Object {
    $_.SourceOffsets -join ','
})
Copy-GeneratedFile 'gfx\common\spr_ricky.png' 'gfx\spr_ricky.png'

$dimitriSource = Read-ImportText (Join-Path $Disassembly 'object_code\common\specialObjects\dimitri.s')
if ($dimitriSource -notmatch '(?ms)^dimitriState5:.*?BTN_BIT_A.*?dimitriGotoEatingState.*?BTN_BIT_B.*?^dimitriUpdateMovement:.*?SPEED_c0.*?SPEED_100' -or
    $dimitriSource -notmatch '(?ms)^dimitriAddWaterfallResistance:.*?TILEINDEX_WATERFALL.*?TILEINDEX_WATERFALL_BOTTOM.*?add \$c0') {
    throw 'Dimitri riding/eating/waterfall source contract changed.'
}
$dimitriTexts = [Collections.Generic.List[string]]::new()
$dimitriTexts.Add("# text-id`ttext-base64`tsource")
foreach ($textId in @(0x2100,0x2101,0x2102,0x2104,0x2106)) {
    $message = $allTexts[$textId].Replace('\jump(TX_2103)', $allTexts[0x2103])
    $dimitriTexts.Add("$($textId.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message)))`tscripts.s:companionScript_subid06Script;scriptHelper.s:companionScript_subid07Script_body")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\dimitri_texts.tsv'), $dimitriTexts)

# Companion forest scripts retain every instruction, label and source line.
$forestOpcodes = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($op in @('showtext','checkmemoryeq','jumpifmemoryeq','jumpiftextoptioneq','scriptjump','wait','writememory','orroomflag','unsetglobalflag','setglobalflag','enableinput','disableinput','scriptend','asm15','writeobjectbyte','checktext')) { [void]$forestOpcodes.Add($op) }
$forestSymbols = $globalFlagValues.Clone()
$forestSymbols['>w1Companion'] = 0xd1
foreach ($binding in @('wTmpcfc0.fairyHideAndSeek.cfd2','w1Companion.var3d','w1Companion.var03','wLinkObjectIndex')) {
    $forestSymbols[$binding] = $binding
}
$forestBindings = @{
    'asm15|companionScript_noticeLink' = @{Opcode='native';Payload='NoticeLink'}
    'asm15|companionScript_spawnFairyAfterFindingCompanionInForest' = @{Opcode='native';Payload='SpawnRescueFairy'}
    'asm15|companionScript_warpOutOfForest' = @{Opcode='native';Payload='WarpOut'}
    'writeobjectbyte|Interaction.state, $02' = @{Opcode='nativeyield';Payload='GiveFlute'}
    'unsetglobalflag|GLOBALFLAG_FOREST_UNSCRAMBLED' = @{Opcode='native';Payload='ScrambleForest'}
}
foreach ($sub in @('08','09','0a','0b')) {
    $script = "companionScript_subid${sub}Script_body"
    $commands = @(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts\ages\scriptHelper.s') $script $forestOpcodes)
    if ($sub -eq '08') {
        $tail = @(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts\ages\scriptHelper.s') 'script15_6e71' $forestOpcodes)
        $commands += $tail
    }
    for ($i = 0; $i -lt $commands.Count; $i++) {
        $commands[$i].Index = $i
        $commands[$i].Script = $script
    }
    # mainScripts loads these bodies into wBigBuffer; local jumps yield.
    $rows = ConvertTo-CutsceneCommandRows $commands '' -symbols $forestSymbols -texts $allTexts `
        -bindings $forestBindings -yieldOnJump $true
    Write-CutsceneGeneratedTable((Join-Path $destination "cutscenes\companion_forest_$sub.tsv"), $rows)
}
$forestTextRows = [Collections.Generic.List[string]]::new()
$forestTextRows.Add("# text-id`ttext-base64`tsource")
foreach ($id in @((0x1120..0x1147) + (0x0038..0x003a) + (0x0069..0x006b))) {
    $text = $allTexts[$id]
    if ($null -eq $text) { throw "Missing forest text TX_$($id.ToString('x4'))" }
    foreach ($pair in @(@(0x1121,0x1122), @(0x1137,0x1138), @(0x113e,0x113f), @(0x1145,0x1146))) {
        if ($id -ne $pair[0]) { continue }
        if ($allTextFallthroughIds[$id] -ne $pair[1] -or !$text.EndsWith('\n')) {
            throw "Forest TX_$($id.ToString('x4')) lost its trailing newline/fallthrough."
        }
        # The unterminated record already supplies the line break. Normalize
        # that control before joining, without inserting a printable spacer.
        $text = $text.Substring(0, $text.Length - 2) + "`n" + $allTexts[$pair[1]]
    }
    foreach ($tail in @(0x1138,0x113f,0x1146)) {
        $text = $text.Replace(('\jump(TX_{0:x4})' -f $tail), $allTexts[$tail])
    }
    $forestTextRows.Add("$($id.ToString('x4'))`t$(ConvertTo-CutsceneCommandPayload $text)`ttext/ages/text.yaml:TX_$($id.ToString('x4'))")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\companion_forest_text.tsv'), $forestTextRows)
$forestNativeSource = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\companionScripts.s')
$forestNativeSource = $forestNativeSource.Substring($forestNativeSource.IndexOf('companionScript_subid09:'))
$forestCompanions = [Collections.Generic.List[string]]::new()
$forestCompanions.Add("# companion`trescue-first`trescue-unlinked`trescue-linked`treward-after`treward-unlinked`treward-linked`tnotice-animation`tsource")
$rescueTable = [regex]::Match($forestNativeSource, '(?ms)@data1:\s*(?<rows>(?:\s*\.db[^\r\n]+\r?\n){3})')
$rewardTable = [regex]::Match($forestNativeSource, '(?ms)@textIndices:\s*(?<rows>(?:\s*\.db[^\r\n]+\r?\n){3})')
$noticeTable = [regex]::Match($forestNativeSource, '(?ms)@animationWhenNoticingLink:\s*(?<rows>(?:\s*\.db[^\r\n]+\r?\n){3})')
$rescueIds = @([regex]::Matches($rescueTable.Groups['rows'].Value, 'TX_(?<id>[0-9a-f]{4})') | ForEach-Object { $_.Groups['id'].Value })
$rewardIds = @([regex]::Matches($rewardTable.Groups['rows'].Value, 'TX_(?<id>[0-9a-f]{4})') | ForEach-Object { $_.Groups['id'].Value })
$noticeIds = @([regex]::Matches($noticeTable.Groups['rows'].Value, '\.db \$(?<id>[0-9a-f]{2})') | ForEach-Object { $_.Groups['id'].Value })
if ($rescueIds.Count -ne 9 -or $rewardIds.Count -ne 9 -or $noticeIds.Count -ne 3) { throw 'companionScripts.s forest companion tables changed.' }
for ($index = 0; $index -lt 3; $index++) {
    $offset = $index * 3
    $forestCompanions.Add("$(('{0:x2}' -f (0x0b+$index)))`t$($rescueIds[$offset])`t$($rescueIds[$offset+1])`t$($rescueIds[$offset+2])`t$($rewardIds[$offset])`t$($rewardIds[$offset+1])`t$($rewardIds[$offset+2])`t$($noticeIds[$index])`tcompanionScripts.s:subid09/subid0a")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\companion_forest_companions.tsv'), $forestCompanions)
$forestRooms = [Collections.Generic.List[string]]::new()
$forestRooms.Add("# group`troom`torder`tid`tsubid`ty`tx`tsource")
$forestObjectPath = Join-Path $Disassembly 'objects/ages/mainData.s'
$forestSpecs = @(
    @(0,0x34,0,0x71,0x08,0,0), @(0,0x63,0,0x71,0x0a,0,0), @(0,0x63,1,0x67,0x05,0,0),
    @(0,0x73,0,0x71,0x0b,0,0), @(0,0x81,1,0x71,0x09,0x58,0x50), @(0,0x81,3,0x67,0x04,0,0),
    @(2,0x5e,4,0x71,0x0c,0,0), @(4,0xe6,1,0x71,0x0c,0,0), @(5,0xf6,3,0x71,0x0c,0,0))
if ([regex]::Matches($mainObjectSource, '(?m)^\s*obj_Interaction \$71 \$0[89abc]\b').Count -ne 7) {
    throw 'objects/ages/mainData.s: expected all seven placed forest companion controllers $71:$08-$0c.'
}
foreach ($spec in $forestSpecs) {
    $label = 'group' + $spec[0] + 'Map' + $spec[1].ToString('x2') + 'ObjectData'
    $nodes = @(Read-AssemblyLabelNodes $forestObjectPath $label | Where-Object { $_.Kind -eq 'MacroInvocation' })
    $matching = @($nodes | Where-Object {
        $_.Name -eq 'obj_Interaction' -and (Convert-AssemblyInteger $_.Operands[0]) -eq $spec[3] -and
        (Convert-AssemblyInteger $_.Operands[1]) -eq $spec[4]
    })
    if ($matching.Count -ne 1) { throw "objects/ages/mainData.s:${label}: expected one forest object $($spec[3].ToString('x2')):$($spec[4].ToString('x2'))." }
    $order = [array]::IndexOf($nodes, $matching[0])
    $values = @($matching[0].Operands | ForEach-Object { Convert-AssemblyInteger $_ })
    if ($values.Count -eq 2) { $values += @(0,0) }
    if ($order -ne $spec[2] -or $values.Count -ne 4 -or $values[2] -ne $spec[5] -or $values[3] -ne $spec[6]) {
        throw "objects/ages/mainData.s:${label}: forest object order or coordinates changed."
    }
    $forestRooms.Add("$($spec[0])`t$($spec[1].ToString('x2'))`t$order`t$($values[0].ToString('x2'))`t$($values[1].ToString('x2'))`t$($values[2].ToString('x2'))`t$($values[3].ToString('x2'))`tobjects/ages/mainData.s:${label}")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\companion_forest_rooms.tsv'), $forestRooms)
$forestPresets = @([regex]::Matches($companionSpawnerSource,
    '(?m)^\s*\.db \$00,\s+\$(?<y>[0-9a-f]{2}), \$(?<x>[0-9a-f]{2}), \$00 ; \$0(?<sub>[45])\s*$'))
if ($forestPresets.Count -ne 2) { throw 'companionSpawner.s:@presetCompanionData: expected both Ages forest presets.' }
$forestPresetRows = [Collections.Generic.List[string]]::new()
$forestPresetRows.Add("# subid`ty`tx`tsource")
foreach ($preset in $forestPresets) {
    $forestPresetRows.Add("0$($preset.Groups['sub'].Value)`t$($preset.Groups['y'].Value)`t$($preset.Groups['x'].Value)`tcompanionSpawner.s:@presetCompanionData")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/companion_forest_presets.tsv'), $forestPresetRows)
$forestReturn = [regex]::Match($mooshHelperSource,
    '(?ms)^@outOfForestWarp:\s+m_HardcodedWarpA ROOM_AGES_(?<room>[0-7][0-9a-f]{2}),\s*\$(?<transition>[0-9a-f]{2}),\s*\$(?<position>[0-9a-f]{2}),\s*\$(?<request>[0-9a-f]{2})')
if (!$forestReturn.Success) { throw 'scriptHelper.s:@outOfForestWarp: missing hardcoded companion return warp.' }
$forestReturnRoom = [Convert]::ToInt32($forestReturn.Groups['room'].Value,16)
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/companion_forest_return_warp.tsv'), @(
    "# group`troom`ttransition`tposition`trequest`tsource",
    "$((0x80 -bor ($forestReturnRoom -shr 8)).ToString('x2'))`t$(($forestReturnRoom -band 255).ToString('x2'))`t$($forestReturn.Groups['transition'].Value)`t$($forestReturn.Groups['position'].Value)`t$($forestReturn.Groups['request'].Value)`tscriptHelper.s:@outOfForestWarp"))
$forestFluteGraphic = $interactionGraphics['113:0']
$forestFluteAnimation = Resolve-NpcAnimation 0x71 $forestFluteGraphic.DefaultAnimation
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\companion_forest_flute.tsv'), @(
    "# sprite`ttile-base`tanimation-base64`tsource",
    "$($gfxNames[$forestFluteGraphic.Gfx])`t$($forestFluteGraphic.TileBase)`t$(ConvertTo-CutsceneCommandPayload $forestFluteAnimation)`tcompanionScripts.s:companionScript_subid0a_state2/interactionData.s:71"))
$forestExclamationGraphic = $interactionGraphics['159:0']
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\companion_forest_exclamation.tsv'), @(
    "# sprite`ttile-base`tpalette`tanimation-base64`tsource",
    "$($gfxNames[$forestExclamationGraphic.Gfx])`t$($forestExclamationGraphic.TileBase)`t$($forestExclamationGraphic.Palette)`t$(ConvertTo-CutsceneCommandPayload (Resolve-NpcAnimation 0x9f $forestExclamationGraphic.DefaultAnimation))`tcompanionScript_makeExclamationMark/exclamationMark.s:9f"))
foreach ($flag in @(@('GLOBALFLAG_TALKED_TO_HEAD_CARPENTER',0x22), @('GLOBALFLAG_GOT_FLUTE',0x23), @('GLOBALFLAG_SAVED_COMPANION_FROM_FOREST',0x24), @('GLOBALFLAG_COMPANION_LOST_IN_FOREST',0x42), @('GLOBALFLAG_FOREST_UNSCRAMBLED',0x2b), @('GLOBALFLAG_CAN_BUY_FLUTE',0x1d))) {
    if ($globalFlagValues[$flag[0]] -ne $flag[1]) { throw "Companion forest native flag binding changed: $($flag[0])" }
}
if ($mooshHelperSource -notmatch 'm_HardcodedWarpA ROOM_AGES_063, \$00, \$56, \$03' -or
    $companionSpawnerSource -notmatch '\.db \$00,\s+\$58, \$50, \$00 ; \$04' -or
    $companionSpawnerSource -notmatch '\.db \$00,\s+\$48, \$68, \$00 ; \$05') {
    throw 'Companion forest preset positions or outgoing warp contract changed.'
}

if ($companionSpawnerSource -notmatch '(?ms)^@subid03:\s*ld hl,wDimitriState\s*ld a,\(wEssencesObtained\)\s*bit 2,a\s*jr z,@deleteSelf\s*jr @loadCompanionPresetIfHasntLeft' -or
    $companionSpawnerSource -notmatch '(?m)^\s*\.db SPECIALOBJECT_DIMITRI,\s+\$48, \$30, \$00 ; \$03\s*$') {
    throw 'companionSpawner.s Dimitri preset $03 or essence predicate changed.'
}
$dimitriRooms = [Collections.Generic.List[string]]::new()
$dimitriRooms.Add("# group`troom`trole`tx`ty`tsource")
foreach ($roomMatch in [regex]::Matches($mainObjectSource, '(?ms)^group(?<group>[0-9])Map(?<room>[0-9a-f]{2})ObjectData:\s*(?<body>.*?)(?=^\w+:|\z)')) {
    $body = $roomMatch.Groups['body'].Value
    $role = if ($body -match 'obj_Interaction \$67 \$03') { 'preset' } else { $null }
    if ($null -ne $role) {
        $dimitriRooms.Add("$($roomMatch.Groups['group'].Value)`t$($roomMatch.Groups['room'].Value)`t$role`t30`t48`tmainData.s:$($roomMatch.Value.Split(':')[0])")
    }
}
if ($dimitriRooms.Count -ne 2) { throw 'Expected the native Dimitri preset placement.' }
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\dimitri_rooms.tsv'), $dimitriRooms)
$departurePlacements=[Collections.Generic.List[string]]::new()
$departurePlacements.Add('# group`troom`torder`tsource')
foreach ($roomMatch in [regex]::Matches($mainObjectSource,'(?ms)^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:\s*(?<body>.*?)(?=^\w+:|\z)')) {
    $objects=@([regex]::Matches($roomMatch.Groups['body'].Value,'(?m)^\s*obj_(?!End)\w+[^\r\n]*'))
    for($index=0;$index -lt $objects.Count;$index++) {
        if($objects[$index].Value -match 'obj_Interaction \$71 \$06\s*$') {
            $departurePlacements.Add("$($roomMatch.Groups['group'].Value)`t$($roomMatch.Groups['room'].Value)`t$index`tobjects/ages/mainData.s:$($roomMatch.Value.Split(':')[0])")
        }
    }
}
if($departurePlacements.Count -ne 4) { throw 'companionScripts.s:$71:$06 requires all three native mainland placements.' }
Write-GeneratedTable((Join-Path $destination 'objects/dimitri_departure_placements.tsv'),$departurePlacements)
$departureOps=[Collections.Generic.HashSet[string]]::new([string[]]@(
    'checkmemoryeq','writememory','disablemenu','setdisabledobjectsto11','turntofacelink','showtext','scriptend'))
$departureCommands=@(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/ages/scripts.s') 'companionScript_subid06Script' $departureOps)
$departureBindings=@{
    'checkmemoryeq|wLinkObjectIndex, >w1Link'=@{Opcode='checkmemoryeq';Arg0='d0';Payload='LinkObject'}
    'checkmemoryeq|wLinkInAir, $00'=@{Opcode='checkmemoryeq';Arg0='00';Payload='LinkInAir'}
    'writememory|wUseSimulatedInput, $00'=@{Opcode='writememory';Arg0='00';Payload='SimulatedInput'}
    'writememory|w1Companion.var03, $03'=@{Opcode='writememory';Arg0='03';Payload='CompanionVar03'}
    'writememory|wDisableScreenTransitions, $00'=@{Opcode='writememory';Arg0='00';Payload='ScreenTransitions'}
    'disablemenu|'=@{Opcode='native';Payload='DisableMenu'}
    'setdisabledobjectsto11|'=@{Opcode='setdisabledobjects';Arg0='11'}
    'turntofacelink|'=@{Opcode='native';Payload='FaceLink'}
}
if($departureCommands.Count -ne 10) { throw 'companionScript_subid06Script: native US command sequence changed.' }
Write-GeneratedTable((Join-Path $destination 'cutscenes/dimitri_departure_commands.tsv'),
    (ConvertTo-CutsceneCommandRows $departureCommands '' -texts $allTexts -bindings $departureBindings))
$departureFacing=@('# direction`tcounter`tparameter`tpointer`toam-address`tsource')
$animationTable=[int]$agesSymbols['interactionAnimationTable']; $animationBank=$animationTable -band (-bnot 0x3fff)
$readDepartureWord={param([int]$at) [int]$romBytes[$at] -bor ([int]$romBytes[$at+1] -shl 8)}
$animationRoot=$animationBank+((& $readDepartureWord ($animationTable+0x71*2)) -band 0x3fff)
$oamRoot=$animationBank+((& $readDepartureWord ([int]$agesSymbols['interactionOamDataTable']+0x71*2)) -band 0x3fff)
for($direction=0;$direction -lt 4;$direction++) {
    $at=$animationBank+((& $readDepartureWord ($animationRoot+$direction*2)) -band 0x3fff)
    if($romBytes[$at] -eq 0xff) { $at=$at+1+[int]$romBytes[$at+1]-256 }
    $counter=[int]$romBytes[$at]; $gfx=[int]$romBytes[$at+1]; $parameter=[int]$romBytes[$at+2]
    if($counter -eq 0 -or $counter -eq 0xff -or ($gfx -band 1) -ne 0) { throw "interaction71Animations: unsupported first frame for direction$direction." }
    $pointer=(($at+3) -band 0x3fff) -bor 0x4000
    $oam=((& $readDepartureWord ($oamRoot+$gfx)) -band 0x3fff) -bor 0x4000
    $departureFacing+="$direction`t$($counter.ToString('x2'))`t$($parameter.ToString('x2'))`t$($pointer.ToString('x4'))`t$($oam.ToString('x4'))`tdata/ages/interactionAnimations.s:interaction71Animations"
}
Write-GeneratedTable((Join-Path $destination 'objects/dimitri_departure_facing.tsv'),$departureFacing)
# SPECIALOBJECT_RICKY's mounted state owns A/B, movement hopping, ITEM_28's
# direction-dependent punch, and ITEM_RICKY_TORNADO. Keep the gameplay
# constants and tornado composition typed instead of reconstructing them from
# the room-specific glove handoff at runtime.
$rickyPunchAttributes = [regex]::Match(
    $rickyItemAttributesSource,
    '(?m)^\s*\.db \$99 \$(?<radius>[0-9a-f]{2}) \$(?<damage>[0-9a-f]{2}) \$00 ; \$28: ITEM_28\s*$')
$rickyTornadoData = [regex]::Match(
    $rickyItemDataSource,
    '(?m)^\s*\.db \$00 \$(?<tile>[0-9a-f]{2}) \$(?<palette>[0-9a-f]{2}) ; \$2a: ITEM_RICKY_TORNADO\s*$')
$rickyTornadoAttributes = [regex]::Match(
    $rickyItemAttributesSource,
    '(?m)^\s*\.db \$99 \$(?<radius>[0-9a-f]{2}) \$(?<damage>[0-9a-f]{2}) \$00 ; \$2a: ITEM_RICKY_TORNADO\s*$')
$rickyTornadoAnimation = [regex]::Match(
    $rickyItemAnimationsSource,
    '(?ms)^itemAnimation1e84b:\s*\.db \$(?<d0>[0-9a-f]{2}) \$00 \$00\s*\.db \$(?<d1>[0-9a-f]{2}) \$02 \$00\s*m_AnimationLoop itemAnimation1e84b')
if (-not $rickyPunchAttributes.Success -or
    -not $rickyTornadoData.Success -or
    -not $rickyTornadoAttributes.Success -or
    -not $rickyTornadoAnimation.Success -or
    $rickyFixedGfxHeaderSource -notmatch '(?ms)^m_GfxHeaderStart \$83, GFXH_COMMON_SPRITES\s*m_GfxHeader spr_common_sprites, \$8001\s*m_GfxHeaderEnd' -or
    $rickySpecialSource -notmatch '(?ms)^rickyState5Substate0:.*?bit BTN_BIT_A.*?rickyStartPunch.*?bit BTN_BIT_B.*?companionGotoDismountState.*?ld a,\$10.*?SPEED_c0.*?ld bc,-\$180.*?SpecialObject\.counter1.*?\$08.*?SPEED_200.*?ld c,\$19.*?getRandomNumber.*?and \$0f.*?SND_JUMP.*?SND_RICKY' -or
    $rickySpecialSource -notmatch '(?ms)^rickyState8:.*?^@substate0:.*?ld c,\$40.*?ld a,SND_UNKNOWN5.*?^@startTornadoCharge:.*?wGameKeysPressed.*?BTN_A.*?ld c,\$13.*?^@substate1:.*?cp \$1e.*?SND_CHARGE_SWORD.*?ITEM_RICKY_TORNADO.*?SNDCTRL_STOPSFX.*?SND_SWORDSPIN.*?^rickyStartPunch:.*?ITEM_28.*?ld c,\$09.*?SND_SWORDSLASH' -or
    $rickySpecialSource -notmatch '(?ms)^rickyState2:.*?companionDecCounter1.*?SND_RICKY.*?objectUpdateSpeedZ_paramC.*?objectApplySpeed.*?companionCalculateAdjacentWallsBitset.*?and \$0f.*?rickyStopUntilLandedOnGround' -or
    $rickySpecialSource -notmatch '(?ms)^rickyCheckHopUpCliff:.*?and \$c0.*?cp \$c0.*?wLinkAngle.*?cp \$00.*?@cliffOffset_oneUp_right:\s*\.db \$f8 \$06.*?@cliffOffset_oneUp_left:\s*\.db \$f8 \$fa.*?@cliffOffset_twoUp_right:\s*\.db \$e8 \$06.*?@cliffOffset_twoUp_left:\s*\.db \$e8 \$fa' -or
    $rickySpecialSource -notmatch '(?ms)^rickyBreakTilesOnLanding:.*?BREAKABLETILESOURCE_RICKY_LANDED.*?@offsets:\s*\.db \$04 \$00.*?\.db \$04 \$06.*?\.db \$fe \$00.*?\.db \$04 \$fa' -or
    $rickySpecialSource -notmatch '(?ms)^rickySetJumpSpeed:.*?-\$300.*?\$08.*?SPEED_140.*?\$0f.*?^rickyHoleCheckOffsets:\s*\.db \$f8 \$00.*?\.db \$05 \$08.*?\.db \$08 \$00.*?\.db \$05 \$f8' -or
    $rickySpecialSource -notmatch '(?ms)^rickyStateASubstate2:.*?objectUpdateSpeedZ_paramC.*?TX_2006.*?w1Link\.yh.*?SpecialObject\.direction.*?ld a,\$03.*?SpecialObject\.var3f.*?specialObjectSetAnimation.*?rickyIncVar03.*?rickySetJumpSpeedForCutscene.*?ld bc,-\$180.*?SpecialObject\.speed.*?SPEED_200.*?SpecialObject\.counter1.*?\$08' -or
    $rickySpecialSource -notmatch '(?ms)^rickyStateASubstate6:.*?specialObjectAnimate.*?SpecialObject\.animParameter.*?SND_RICKY.*?rlca.*?rickySetJumpSpeedForCutsceneAndSetAngle.*?ld a,\$10.*?ld c,\$05.*?companionSetAnimation.*?rickyIncVar03' -or
    $rickySpecialSource -notmatch '(?ms)^rickyStateASubstate3:.*?retIfTextIsActive.*?ld a,\$14.*?SpecialObject\.angle.*?dec e\s+ld a,\$02\s+ld \(de\),a.*?ld c,\$05.*?companionSetAnimation.*?rickyIncVar03' -or
    $rickySpecialSource -notmatch '(?ms)^rickyStateASubstate5:.*?specialObjectAnimate.*?objectApplySpeed.*?ld c,\$40.*?objectUpdateSpeedZ_paramC.*?ld a,\$18.*?specialObjectSetAnimation.*?rickyIncVar03.*?^rickyStateASubstate4:\s*rickyStateASubstate7:.*?companionSetAnimationToVar3f.*?rickyWaitUntilJumpDone.*?ld a,\$18.*?specialObjectCheckMovingTowardWall.*?ld a,\$10.*?specialObjectCheckMovingTowardWall.*?rickySetJumpSpeed.*?SND_JUMP.*?rickyIncVar03.*?objectCheckWithinScreenBoundary.*?cp \$07.*?ld a,\$10.*?ld a,\$14.*?rickySetJumpSpeedForCutscene.*?^@leftScreen:.*?wDisabledObjects.*?wMenuDisabled.*?wDeathRespawnBuffer\.rememberedCompanionId.*?itemDelete.*?wRickyState.*?set 6.*?saveLinkLocalRespawnAndCompanionPosition' -or
    $rickySpecialSource -notmatch '(?ms)^rickyWaitUntilJumpDone:\s*ld c,\$40\s*call objectUpdateSpeedZ_paramC\s*jr z,@onGround\s*call companionUpdateMovement\s*or d\s*ret\s*@onGround:\s*ld c,\$05\s*call companionSetAnimation\s*jp companionDecCounter1IfNonzero' -or
    $specialObjectCommonSource -notmatch '(?ms)^companionCheckHopDownCliff:.*?and \$e7.*?cp \$03.*?cp \$0c.*?cp \$30.*?TILEINDEX_VINE_TOP.*?cliffTilesTable.*?ld bc,-\$2c0.*?SPEED_200.*?ld a,\$14.*?^@directionOffsets:\s*\.db \$fa \$00.*?\.db \$00 \$04.*?\.db \$08 \$00.*?\.db \$00 \$fb' -or
    $specialObjectCommonSource -notmatch '(?ms)^companionCalculateAdjacentWallsBitset:.*?^@offsets:\s*\.db \$fb \$fd.*?\.db \$00 \$07.*?\.db \$0d \$f9.*?\.db \$00 \$07.*?\.db \$f5 \$f7.*?\.db \$09 \$00.*?\.db \$f7 \$0b.*?\.db \$09 \$00' -or
    $rickyCollisionSource -notmatch '(?ms)^checkCollisionPosition_disallowSmallBridges:.*?^@specialCollisions:\s*\.db %00000000 %11111111 %00000011 %11000000 %11000011 %11000011 %11000011 %00000000\s*\.db %00000000 %11111111 %00000011 %11000000 %11000001 %11000001 %11111111 %00000000' -or
    $rickyAttackItemSource -notmatch '(?ms)^itemCode28:.*?Item\.counter1.*?\$14.*?^@rickyData:\s*\.db \$10 \$0c \$f4 \$00.*?\.db \$0c \$12 \$fe \$08.*?\.db \$10 \$0c \$08 \$00.*?\.db \$0c \$12 \$fe \$f8.*?BREAKABLETILESOURCE_RICKY_PUNCH' -or
    $rickyTornadoItemSource -notmatch '(?ms)^@state0:.*?SPEED_300.*?^@offsets:\s*\.db \$f0 \$00.*?\.db \$00 \$0c.*?\.db \$08 \$00.*?\.db \$00 \$f4.*?^@state1:.*?objectApplySpeed.*?BREAKABLETILESOURCE_SWORD_L1.*?and \$0f.*?cp \$0f' -or
    $rickyItemConstants -notmatch '(?m)^\s*ITEM_28\s+db ; 0x28\s*$' -or
    $rickyItemConstants -notmatch '(?m)^\s*ITEM_RICKY_TORNADO\s+db ; 0x2a\s*$' -or
    $rickyBreakSourceConstants -notmatch '(?m)^\s*BREAKABLETILESOURCE_RICKY_PUNCH:\s*db ; 0x0f\s*$' -or
    $rickyBreakSourceConstants -notmatch '(?m)^\s*BREAKABLETILESOURCE_RICKY_LANDED:\s*db ; 0x10\s*$' -or
    $rickyTileIndexConstants -notmatch '(?ms)^\.ifdef ROM_AGES.*?TILEINDEX_VINE_TOP\s+\$d4.*?TILEINDEX_VINE_MIDDLE\s+\$d5.*?TILEINDEX_VINE_BOTTOM\s+\$d6' -or
    $rickyTileIndexConstants -notmatch '(?m)^\.define TILEINDEX_HOLE\s+\$f3\s*$' -or
    $rickyTileIndexConstants -notmatch '(?m)^\.define TILEINDEX_FD\s+\$fd' -or
    $mooshSpeedSource -notmatch '(?m)^\s*SPEED_c0\s+dsb 5 ; 0x1e\s*$' -or
    $mooshSpeedSource -notmatch '(?m)^\s*SPEED_140\s+dsb 5 ; 0x32\s*$' -or
    $mooshSpeedSource -notmatch '(?m)^\s*SPEED_200\s+dsb 5 ; 0x50\s*$' -or
    $mooshSpeedSource -notmatch '(?m)^\s*SPEED_300\s+dsb 5 ; 0x78\s*$' -or
    $mooshMusicConstants -notmatch '(?m)^\s*SND_CHARGE_SWORD\s+db ; \$4f' -or
    $mooshMusicConstants -notmatch '(?m)^\s*SND_JUMP\s+db ; \$53' -or
    $mooshMusicConstants -notmatch '(?m)^\s*SND_SWORDSPIN\s+db ; \$6b' -or
    $mooshMusicConstants -notmatch '(?m)^\s*SND_SWORDSLASH\s+db ; \$74' -or
    $mooshMusicConstants -notmatch '(?m)^\s*SND_UNKNOWN5\s+db ; \$75') {
    throw 'Ricky mounted movement, punch, charge, or tornado data changed.'
}
$rickyPunchDamageByte = [Convert]::ToInt32(
    $rickyPunchAttributes.Groups['damage'].Value, 16)
$rickyTornadoDamageByte = [Convert]::ToInt32(
    $rickyTornadoAttributes.Groups['damage'].Value, 16)
$rickyPunchDamage = 0x100 - $rickyPunchDamageByte
$rickyTornadoDamage = 0x100 - $rickyTornadoDamageByte
$rickyTornadoRadius = [Convert]::ToInt32(
    $rickyTornadoAttributes.Groups['radius'].Value, 16)
$rickyTornadoTile = [Convert]::ToInt32(
    $rickyTornadoData.Groups['tile'].Value, 16)
$rickyTornadoPalette = [Convert]::ToInt32(
    $rickyTornadoData.Groups['palette'].Value, 16) -band 7
$rickyTornadoAnimationEncoded = @(
    "$([Convert]::ToInt32($rickyTornadoAnimation.Groups['d0'].Value, 16))@8,0,0,0;8,8,2,0",
    "$([Convert]::ToInt32($rickyTornadoAnimation.Groups['d1'].Value, 16))@8,0,2,32;8,8,0,32"
) -join '|'
if ($rickyPunchDamage -ne 4 -or $rickyTornadoDamage -ne 4 -or
    $rickyTornadoRadius -ne 0x66 -or $rickyTornadoTile -ne 0x28 -or
    $rickyTornadoPalette -ne 1 -or
    $rickyItemOamSource -notmatch '(?ms)^itemOamData4cfab:\s*\.db \$02\s*\.db \$08 \$00 \$00 \$00\s*\.db \$08 \$08 \$02 \$00' -or
    $rickyItemOamSource -notmatch '(?ms)^itemOamData4cfb4:\s*\.db \$02\s*\.db \$08 \$00 \$02 \$20\s*\.db \$08 \$08 \$00 \$20') {
    throw 'Ricky ITEM_28/ITEM_RICKY_TORNADO attributes or OAM changed.'
}
$rickyDepartureRows = @(
    '# air-animation`tground-animation-base`tpunch-animation`tdiagonal-angle`twall-probe-angle`texit-angle`thop-delay`tleft-mask`tsource',
    "03`t05`t18`t14`t18`t10`t8`t40`tobject_code/common/specialObjects/ricky.s:rickyStateASubstate2-7 (ROM_AGES)")
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/ricky_departure.tsv'), $rickyDepartureRows)

$rickyEventRows = @(
    "# group`troom`tcontroller-id`tcontroller-subid`tspawner-id`tspawner-subid`tricky-id`tricky-y`tricky-x`tprerequisite-global-flag`tricky-state-address`ttalked-mask`tcomplete-mask`tleft-mask`tgloves-treasure`tanimal-companion-id`tinitial-animation`tjump-speed-z`tjump-gravity`tricky-sound`tinitial-special-object-updates`tinitial-script-updates`tsource",
    "0`t6a`t71`t03`t67`t02`t0b`t40`t50`t15`tc646`t01`t20`t40`t48`t0b`t00`t-256`t40`tc3`t2`t0`tmainData.s:group0Map6aObjectData;companionSpawner.s:@subid02;companionScripts.s:companionScript_subid03;ricky.s:rickyState0/rickyStateASubstate1"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\ricky_gloves_event.tsv'),
    $rickyEventRows)

foreach ($textId in @(0x1204,0x1205,0x1206,0x1207,0x2200,0x2201,0x2202,0x2203,0x2204,0x2205,0x2209)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Missing room 0:6c text TX_$($textId.ToString('x4'))."
    }
}
$mooshEventRows = @(
    "# group`troom`tghini-id`tghini0-y`tghini0-x`tghini1-y`tghini1-x`tghini2-y`tghini2-x`tcontroller-y`tcontroller-x`trestrict-y`trestrict-x`tessence-address`tessence-mask`tflag-group`tflag-room`tflag-mask`tmoosh-state-address`tactive-mask`trescued-mask`tcheval-rope-treasure`tmoosh-id`tmoosh-y`tmoosh-x`tmoosh-sprite`tmoosh-tile-base`tmoosh-palette`tmoosh-animation`tghini-speed`tghini-angle`tghini-frames`tshake-frames`tenemy-id`tenemy-subid`texclamation-id`texclamation-subid`texclamation-sprite`texclamation-tile-base`texclamation-palette`texclamation-animation`texclamation-y-offset`texclamation-x-offset`texclamation-frames`tding-sound`texclamation-sound`tjump-sound`tcharge-sound`tstomp-sound`tminiboss-music`trestrict-text-base64`tsource",
    (@(
        '0','6c','73',
        $mooshPlacement.Groups['g0y'].Value,$mooshPlacement.Groups['g0x'].Value,
        $mooshPlacement.Groups['g1y'].Value,$mooshPlacement.Groups['g1x'].Value,
        $mooshPlacement.Groups['g2y'].Value,$mooshPlacement.Groups['g2x'].Value,
        $mooshPlacement.Groups['controllery'].Value,$mooshPlacement.Groups['controllerx'].Value,
        $mooshPlacement.Groups['restricty'].Value,$mooshPlacement.Groups['restrictx'].Value,
        'c6bf','02','1','79','40','c648','60','20','52','0d','28','58',
        'spr_moosh','0','1',$mooshAnimation,'32','18','32','60','17','00',
        '9f','00',$gfxNames[$exclamationGraphic.Gfx],
        $exclamationGraphic.TileBase.ToString(),$exclamationGraphic.Palette.ToString(),
        $exclamationAnimation,'-16','0','30','c8','50','53','4f','85','2d',
        [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x2209])),
        'mainData.s:group0Map6cObjectData;ghiniHarassingMoosh.s;companionScripts.s;companionSpawner.s;moosh.s;scripts.s;scriptHelper.s'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\moosh_rescue_event.tsv'),
    $mooshEventRows)

$mooshPlacementRows = [Collections.Generic.List[string]]::new()
$mooshPlacementRows.Add("# order`tid`tsubid`ty`tx`tsource")
$mooshOrder = 0
foreach ($placement in [regex]::Matches($mooshPlacement.Value,
    '(?m)^\s*obj_Interaction \$(?<id>[0-9a-f]{2}) \$(?<sub>[0-9a-f]{2})(?: \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}))?\s*$')) {
    $y = if ($placement.Groups['y'].Success) { $placement.Groups['y'].Value } else { '00' }
    $x = if ($placement.Groups['x'].Success) { $placement.Groups['x'].Value } else { '00' }
    $mooshPlacementRows.Add("$mooshOrder`t$($placement.Groups['id'].Value)`t$($placement.Groups['sub'].Value)`t$y`t$x`tmainData.s:group0Map6cObjectData")
    $mooshOrder++
}
if ($mooshOrder -ne 6) { throw 'Room0:6c requires its complete six-interaction source stream.' }
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\moosh_rescue_placements.tsv'), $mooshPlacementRows)

function Write-MooshRescueCommands {
    param([string]$file, [string]$script, [object[]]$specs)
    $line = Get-AssemblySourceLine `
        $mooshScriptsSource "(?m)^$([regex]::Escape($script))\s*:" $script
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add($cutsceneCommandHeader)
    $expanded = [Collections.Generic.List[object]]::new()
    $labels = [Collections.Generic.Dictionary[string,int]]::new([StringComparer]::Ordinal)
    foreach ($spec in $specs) {
        if ($spec[0] -eq 'label') {
            $labels.Add($spec[1], $expanded.Count)
        } elseif ($spec[0] -eq 'signalwait') {
            $start = $expanded.Count
            $expanded.Add(@('jumpifmemoryeqyieldonmiss','','01',($start + 2).ToString(),$spec[1]))
            $expanded.Add(@('scriptjump','',$start.ToString(),'',''))
        } elseif ($spec[0] -eq 'ghinicircle') {
            # The native mover runs before interactionRunScript, including
            # during text. Keep the script's actual wait/jump yield loop.
            $actor = $spec[1]
            $start = $expanded.Count
            $expanded.Add(@('native','','','',"Begin${actor}Circle"))
            $expanded.Add(@('jumpifmemoryeq','','00',($start + 4).ToString(),"${actor}Speed"))
            $expanded.Add(@('wait','','1','',''))
            $expanded.Add(@('scriptjump','',($start + 1).ToString(),'',''))
        } else {
            $expanded.Add($spec)
        }
    }
    for ($index = 0; $index -lt $expanded.Count; $index++) {
        $spec = $expanded[$index]
        foreach ($operand in @(2,3)) {
            if (([string]$spec[$operand]).StartsWith('@')) {
                $target = [string]$spec[$operand]
                if (-not $labels.ContainsKey($target)) { throw "${script}: unresolved label $target." }
                $spec[$operand] = $labels[$target].ToString()
            }
        }
        $rows.Add((New-CutsceneCommandRow `
            $script $index $script $line `
            $spec[0] $spec[1] $spec[2] $spec[3] $spec[4]))
    }
    Write-CutsceneGeneratedTable(
        (Join-Path $destination "cutscenes\$file"), $rows)
}

$agesMooshCutscene = [regex]::Match($mooshSpecialSource,
    '(?ms)^\.ifdef ROM_AGES\r?\n;;\r?\n; State A: cutscene stuff\r?\n(?<body>.*?)^\.else').Groups['body'].Value
if ($agesMooshCutscene -notmatch '(?ms)^mooshStateASubstate1:.*?SpecialObject\.var3d.*?ld a,\$01\s*ld \(wDisabledObjects\),a\s*ld \(wMenuDisabled\),a' -or
    $agesMooshCutscene -notmatch '(?ms)^mooshStateASubstate3:.*?wMooshState.*?and \$20.*?wStatusBarNeedsRefresh.*?SpecialObject\.var3d.*?objectRemoveFromAButtonSensitiveObjectList.*?companionSetAnimation.*?companionForceMount' -or
    $mooshScriptsSource -notmatch '(?ms)^companionScript_subid00Script:.*?writememory\s+w1Companion\.var03, \$02\s*checkmemoryeq w1Companion\.var3d, \$01.*?showtext TX_2201\s*writememory w1Companion\.var03, \$03.*?writememory wDisableScreenTransitions, \$00\s*enablemenu\s*scriptend') {
    throw 'Ages mooshStateA controls or companionScript_subid00 native handoff/enablemenu changed.'
}

$ghiniCircleLoop = 'asm15 scriptHelp\.ghiniHarassingMoosh_beginCircularMovement\s*' +
    '@(?<loop>waitUntilStopped2?):\s*jumpifobjectbyteeq Interaction\.speed, \$00, \+\+\s*' +
    'wait 1\s*scriptjump @\k<loop>\s*\+\+\s*showtext'
foreach ($subId in @(0, 1, 2)) {
    $script = 'ghiniHarassingMoosh_subid{0:d2}Script' -f $subId
    $body = [regex]::Match($mooshScriptsSource, "(?ms)^${script}:.*?(?=^\w+Script:|\z)").Value
    $expected = if ($subId -eq 1) { 2 } else { 1 }
    if ([regex]::Matches($body, $ghiniCircleLoop).Count -ne $expected) {
        throw "scripts.s:${script}: native Ghini speed gate/wait1/scriptjump loop changed."
    }
}
function Moosh-Text([int]$id) {
    $text = $allTexts[$id]
    if ($id -in @(0x2203, 0x2204)) {
        if (-not $text.Contains('\call(TX_2202)')) {
            throw "TX_$($id.ToString('x4')) lost its source call to TX_2202."
        }
        $text = $text.Replace('\call(TX_2202)', $allTexts[0x2202])
    }
    return $text
}

$mooshControllerScript = [regex]::Match($mooshScriptsSource,
    '(?ms)^companionScript_subid00Script:.*?(?=^companionScript_subid03Script:)').Value
if ([regex]::Matches($mooshControllerScript,
        'writeobjectbyte Interaction\.var3a, 60\s*callscript @delayFrames').Count -ne 2 -or
    $mooshControllerScript -notmatch '(?ms)^@delayFrames:\s*jumpifobjectbyteeq Interaction\.var3a, \$00, \+\+\s*wait 1\s*scriptjump @delayFrames\s*\+\+\s*retscript') {
    throw 'Moosh controller var3a writes or native delay call/wait/jump/return flow changed.'
}

Write-MooshRescueCommands 'moosh_rescue_ghini0.tsv' `
    'ghiniHarassingMoosh_subid00Script' @(
        @('setdisabledobjects','','11','',''),
        @('ghinicircle','Ghini0','','',''),
        @('showtext','','1204','',(Moosh-Text 0x1204)),
        @('writememory','','01','','SignalOr'),
        @('signalwait','SignalBit10','','',''),
        @('setdisabledobjects','','00','',''),
        @('native','','','','SpawnEnemyGhini0'),
        @('scriptend','','','',''))
Write-MooshRescueCommands 'moosh_rescue_ghini1.tsv' `
    'ghiniHarassingMoosh_subid01Script' @(
        @('signalwait','SignalBit01','','',''),
        @('ghinicircle','Ghini1','','',''),
        @('showtext','','1205','',(Moosh-Text 0x1205)),
        @('writememory','','02','','SignalOr'),
        @('signalwait','SignalBit08','','',''),
        @('ghinicircle','Ghini1','','',''),
        @('showtext','','1207','',(Moosh-Text 0x1207)),
        @('playsound','','c8','',''),
        @('setmusic','','2d','',''),
        @('writememory','','10','','SignalOr'),
        @('native','','','','SpawnEnemyGhini1'),
        @('scriptend','','','',''))
Write-MooshRescueCommands 'moosh_rescue_ghini2.tsv' `
    'ghiniHarassingMoosh_subid02Script' @(
        @('signalwait','SignalBit04','','',''),
        @('ghinicircle','Ghini2','','',''),
        @('showtext','','1206','',(Moosh-Text 0x1206)),
        @('writememory','','08','','SignalOr'),
        @('signalwait','SignalBit10','','',''),
        @('native','','','','SpawnEnemyGhini2'),
        @('scriptend','','','',''))
Write-MooshRescueCommands 'moosh_rescue_companion.tsv' `
    'companionScript_subid00Script' @(
        @('signalwait','SignalBit02','','',''),
        @('writeobjectbyte','Controller','7a','3c',''),
        @('callscript','','@shake-delay','',''),
        @('showtext','','2200','',(Moosh-Text 0x2200)),
        @('writememory','','04','','SignalOr'),
        @('signalwait','SignalBit10','','',''),
        @('checkmemoryeq','','00','','RoomEnemyCount'),
        @('playsound','','c8','',''), @('wait','','20','',''),
        @('playsound','','c8','',''), @('wait','','20','',''),
        @('playsound','','c8','',''),
        @('native','','','','RestoreRoomMusic'),
        @('writememory','','02','','MooshVar03'),
        @('checkmemoryeq','','01','','MooshButton'),
        @('writeobjectbyte','Controller','7a','3c',''),
        @('callscript','','@shake-delay','',''),
        @('showtext','','2201','',(Moosh-Text 0x2201)),
        @('writememory','','03','','MooshVar03'),
        @('native','','','','SpawnExclamation'),
        @('setdisabledobjects','','11','',''),
        @('native','','','','FaceMooshTowardLink'),
        @('wait','','60','',''),
        @('jumpifmemoryeq','','00','@first-meeting','IsLinkedGame'),
        @('jumpifmemoryeq','','0d','@meeting-again','AnimalCompanion'),
        @('scriptjump','','@first-meeting','',''),
        @('label','@meeting-again','','',''),
        @('showtext','','2204','',(Moosh-Text 0x2204)),
        @('scriptjump','','@meeting-done','',''),
        @('label','@first-meeting','','',''),
        @('showtext','','2203','',(Moosh-Text 0x2203)),
        @('label','@meeting-done','','',''),
        @('writememory','','20','','MooshStateOr'),
        @('setdisabledobjects','','00','',''),
        @('checkmemoryeq','','01','','MooshMounted'),
        @('showtext','','2205','',(Moosh-Text 0x2205)),
        @('native','','','','CompleteMooshRescue'),
        @('enablemenu','','','',''),
        @('scriptend','','','',''),
        @('label','@shake-delay','','',''),
        @('jumpifmemoryeq','','00','@shake-return','ShakeCounter'),
        @('wait','','1','',''),
        @('scriptjump','','@shake-delay','',''),
        @('label','@shake-return','','',''),
        @('return','','','',''))
