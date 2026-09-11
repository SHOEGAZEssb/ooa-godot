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
    $rows = @($specialOamNodes | Where-Object {
        $_.EnclosingGlobalLabel -eq $label -and
        $_.Kind -eq 'Data' -and $_.Name -ieq '.db'
    })
    if ($rows.Count -lt 2) { throw "Moosh OAM block $label is incomplete." }
    $count = Convert-AssemblyInteger $rows[0].Operands[0]
    if ($rows.Count -ne $count + 1) {
        throw "Moosh OAM block $label declares $count cells but has $($rows.Count - 1)."
    }
    return @($rows | Select-Object -Skip 1 | ForEach-Object {
        if ($_.Operands.Count -ne 4) {
            throw "Moosh OAM block $label has a malformed cell at line $($_.Line)."
        }
        ($_.Operands | ForEach-Object { Convert-AssemblyInteger $_ }) -join ','
    }) -join ';'
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
    $body = Get-AssemblyLabelBody $specialAnimationSource $label
    $frames = [Collections.Generic.List[string]]::new()
    $vramTiles = [int[]]::new(0x100)
    for ($tile = 0; $tile -lt $vramTiles.Length; $tile++) {
        $vramTiles[$tile] = -1
    }
    foreach ($frame in [regex]::Matches(
        $body,
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $gfx = [Convert]::ToInt32($frame.Groups['gfx'].Value, 16)
        $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
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
    return $frames -join '|'
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
if ($linkCutsceneGfxRows.Count -ne 0x104 -or
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
$mooshVisualRows = @(
    "# sprite`ttile-base`tpalette`tanimations-base64`tlink-sprite`tlink-palette`tlink-frames-base64`tlink-source-offsets`twater-hazard`twater-hover-frames`twater-exclamation-z-offset`twater-exclamation-sound`tsource",
    "spr_moosh`t0`t1`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($mooshAnimations -join "`n")))`tspr_link`t0`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($mooshLinkFrames -join "`n")))`t$($mooshLinkSourceOffsets -join ',')`t$mooshWaterHazard`t$mooshWaterHoverFrames`t$mooshWaterExclamationZOffset`t50`tspecialObjectAnimationData.s:specialObject0d,specialObject09;moosh.s:mooshState8Substate1/mooshState8Substate5;exclamationMark.s:objectCreateExclamationMark_body"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\moosh_companion_visual.tsv'),
    $mooshVisualRows)
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
$rickySpecialSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\ricky.s')
$rickyCollisionSource = Read-ImportText (
    Join-Path $Disassembly 'code\bank0.s')
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
        0 { $opcode = 'checkabutton'; $actor = 'Ricky' }
        1 { }
        2 { $opcode = 'jumpifmemoryeq'; $arg0 = '01'; $arg1 = '8'; $payload = 'RickyTalked' }
        3 { $opcode = 'writememory'; $arg0 = '01'; $payload = 'RickyStateOr' }
        4 { $opcode = 'jumpifmemoryeq'; $arg0 = '0b'; $arg1 = '7'; $payload = 'AnimalCompanion' }
        5 { $payload = $rickyIntroText; $arg0 = '2000' }
        6 { $arg0 = '8' }
        7 { $payload = $allTexts[0x2001]; $arg0 = '2001' }
        8 { $opcode = 'jumpifmemoryeq'; $arg0 = '01'; $arg1 = '13'; $payload = 'HasRickyGloves' }
        9 { $payload = $allTexts[0x2003]; $arg0 = '2003' }
        10 { $opcode = 'native'; $payload = 'ResetRickyButton' }
        11 { }
        12 { $arg0 = '0' }
        13 { $payload = $allTexts[0x2004]; $arg0 = '2004' }
        14 { $opcode = 'native'; $payload = 'LoseRickyGloves' }
        15 { $opcode = 'native'; $payload = 'BeginRickyMount' }
        16 { $opcode = 'native'; $payload = 'EnableObjectsForRickyMount' }
        17 { $opcode = 'checkmemoryeq'; $arg0 = '01'; $payload = 'RickyMounted' }
        18 { $payload = $allTexts[0x2005]; $arg0 = '2005' }
        19 { $opcode = 'writememory'; $arg0 = '20'; $payload = 'RickyStateOr' }
        20 { $opcode = 'native'; $payload = 'EnableRickyMenu' }
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
    $startLabel = @($specialAnimationNodes | Where-Object {
        $_.Kind -eq 'Label' -and $_.Name -eq $label
    })
    if ($startLabel.Count -ne 1) {
        throw "Could not resolve $companionName animation label $label."
    }
    $nextAnimation = @($specialAnimationNodes | Where-Object {
        $_.Kind -eq 'Label' -and
        $_.Offset -gt $startLabel[0].Offset -and
        $_.Name -match '^animationData[0-9a-f]+$'
    } | Select-Object -First 1)
    $endOffset = if ($nextAnimation.Count -eq 1) {
        $nextAnimation[0].Offset
    } else {
        $oamStart
    }
    $frameNodes = @($specialAnimationNodes | Where-Object {
        $_.Kind -eq 'Data' -and $_.Name -ieq '.db' -and
        $_.Offset -gt $startLabel[0].Offset -and
        $_.Offset -lt $endOffset -and $_.Operands.Count -ge 3
    })
    $loopNodes = @($specialAnimationNodes | Where-Object {
        $_.Kind -eq 'MacroInvocation' -and
        $_.Name -eq 'm_AnimationLoop' -and
        $_.Offset -gt $startLabel[0].Offset -and
        $_.Offset -lt $endOffset
    })
    if ($loopNodes.Count -gt 1) {
        throw "$companionName animation $label has multiple loop terminators."
    }
    $loopStart = 0
    if ($loopNodes.Count -eq 1) {
        $target = $loopNodes[0].Operands[0]
        $targetLabel = @($specialAnimationNodes | Where-Object {
            $_.Kind -eq 'Label' -and $_.Name -eq $target
        })
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
$rickyVisualRows = @(
    "# sprite`ttile-base`tpalette`tanimations-base64`tanimation-source-offsets-base64`tlink-sprite`tlink-palette`tlink-frames-base64`tlink-source-offsets`tsource",
    "spr_ricky`t0`t$rickyPalette`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($rickyAnimations -join "`n")))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($rickyAnimationSourceOffsets -join "`n")))`tspr_link`t0`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($mooshLinkFrames -join "`n")))`t$($mooshLinkSourceOffsets -join ',')`tspecialObjectAnimationData.s:specialObject0b,specialObject09;commonCode.s:specialObjectSetOamVariables"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\ricky_companion_visual.tsv'),
    $rickyVisualRows)
Copy-GeneratedFile 'gfx\common\spr_ricky.png' 'gfx\spr_ricky.png'

# SPECIALOBJECT_DIMITRI $0c uses the same incremental VRAM/OAM format as
# Ricky, including graphics rows which retain tiles from earlier frames.
$dimitriGfxStart = $specialAnimationSource.IndexOf('specialObject0cGfxPointers:')
$dimitriAnimStart = $specialAnimationSource.IndexOf('specialObject0cAnimationDataPointers:')
$dimitriOamStart = $specialAnimationSource.IndexOf('specialObject0cOamDataPointers:')
$dimitriOam = $specialOamTables['specialObject0cOamDataPointers']
if ($null -eq $dimitriOam) { $dimitriOam = $specialOamTables['specialObject10OamDataPointers'] }
$dimitriOffsets = @{}; $dimitriCounts = @{}; $dimitriIndices = @{}
$dimitriGfx = 0
foreach ($line in ($specialAnimationSource.Substring(
    $dimitriGfxStart, $dimitriAnimStart - $dimitriGfxStart) -split '\r?\n')) {
    if ($line -match 'm_SpecialObjectGfxPointer\s+\$(?<oam>[0-9a-f]{2})\s+spr_dimitri\s+\$(?<offset>[0-9a-f]{4})\s+\$(?<size>[0-9a-f]{2})') {
        $dimitriIndices[$dimitriGfx] = [Convert]::ToInt32($Matches['oam'], 16)
        $dimitriOffsets[$dimitriGfx] = [Convert]::ToInt32($Matches['offset'], 16) / 16
        $dimitriCounts[$dimitriGfx] = [Convert]::ToInt32($Matches['size'], 16)
        $dimitriGfx++
    } elseif ($line -match 'm_SpecialObjectGfxPointer\s+\$(?<oam>[0-9a-f]{2})\s+\$0000') {
        $dimitriIndices[$dimitriGfx] = [Convert]::ToInt32($Matches['oam'], 16)
        $dimitriOffsets[$dimitriGfx] = 0
        $dimitriCounts[$dimitriGfx] = 0
        $dimitriGfx++
    }
}
$dimitriLabels = @([regex]::Matches($specialAnimationSource.Substring(
    $dimitriAnimStart, $dimitriOamStart - $dimitriAnimStart),
    '(?m)^\s*\.dw\s+(?<label>animationData[0-9a-f]+)') | ForEach-Object { $_.Groups['label'].Value })
if ($dimitriGfx -ne 54 -or $dimitriLabels.Count -ne 40 -or $null -eq $dimitriOam -or
    $specialObjectCommonSource -notmatch '(?m)^\s*\.db \$60 \$0a ; 0x0c') {
    throw "SPECIALOBJECT_DIMITRI `$0c graphics/OAM/animation contract changed: gfx=$dimitriGfx animations=$($dimitriLabels.Count) OAM=$($dimitriOam.Count)."
}
$dimitriResolved = @($dimitriLabels | ForEach-Object {
    Resolve-CompanionSpecialAnimation $_ Dimitri $dimitriOffsets $dimitriCounts `
        $dimitriIndices $dimitriOam $dimitriOamStart
})
$dimitriAnimations = @($dimitriResolved | ForEach-Object { $_.Encoded })
$dimitriSourceOffsets = @($dimitriResolved | ForEach-Object { $_.SourceOffsets -join ',' })
$dimitriRows = @(
    '# sprite`tpalette`tanimations-base64`tanimation-source-offsets-base64`tlink-frames-base64`tlink-source-offsets`tsource',
    "spr_dimitri`t2`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($dimitriAnimations -join "`n")))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($dimitriSourceOffsets -join "`n")))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($mooshLinkFrames -join "`n")))`t$($mooshLinkSourceOffsets -join ',')`tspecialObjectAnimationData.s:specialObject0c,specialObject09")
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\dimitri_visual.tsv'), $dimitriRows)
Copy-GeneratedFile 'gfx\common\spr_dimitri.png' 'gfx\spr_dimitri.png'
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
foreach ($sub in @('08','09','0a','0b')) {
    $script = "companionScript_subid${sub}Script_body"
    $commands = @(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts\ages\scriptHelper.s') $script $forestOpcodes)
    if ($sub -eq '08') {
        $tail = @(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts\ages\scriptHelper.s') 'script15_6e71' $forestOpcodes)
        $commands += $tail
    }
    $targets = @{}
    for ($i = 0; $i -lt $commands.Count; $i++) { if (!$targets.ContainsKey($commands[$i].Label)) { $targets[$commands[$i].Label] = $i } }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
    for ($i = 0; $i -lt $commands.Count; $i++) {
        $command = $commands[$i]; $op = $command.Opcode; $args = ([string]$command.Operands).Trim(); $arg0 = ''; $arg1 = ''; $payload = ''
        switch ($op) {
            'showtext' { $arg0 = $args.Substring(3); $payload = $allTexts[[Convert]::ToInt32($arg0,16)] }
            'wait' { $arg0 = $args }
            'scriptjump' { if (!$targets.ContainsKey($args)) { throw "Unresolved forest jump $script : $args" }; $arg0 = "$($targets[$args])" }
            'jumpiftextoptioneq' { if ($args -notmatch '^\$(?<value>[0-9a-f]{2}), (?<target>@\w+)$') { throw "Invalid forest choice: $args" }; if (!$targets.ContainsKey($Matches.target)) { throw "Unresolved forest choice $script : $args" }; $arg0=$Matches.value; $arg1="$($targets[$Matches.target])" }
            { $_ -in @('checkmemoryeq','jumpifmemoryeq','writememory') } {
                if ($args -notmatch '^(?<binding>[^,]+), (?<value>\$[0-9a-f]{2}|>w1Companion)(?:, (?<target>@\w+))?$') { throw "Invalid forest memory command $script : $args" }
                $payload = $Matches.binding; $arg0 = if ($Matches.value -eq '>w1Companion') {'01'} else {$Matches.value.Substring(1)}
                if ($op -eq 'jumpifmemoryeq') { if (!$targets.ContainsKey($Matches.target)) { throw "Unresolved forest memory jump $script : $args" }; $arg1="$($targets[$Matches.target])" }
                if ($op -eq 'writememory' -and $payload -eq 'w1Companion.var03') { $op='native'; $payload='ForceMount'; $arg0='' }
            }
            'writeobjectbyte' { if ($args -ne 'Interaction.state, $02') { throw "Unknown forest state write: $args" }; $op='nativeyield'; $payload='GiveFlute' }
            'asm15' {
                $op='native'; $payload = switch ($args) {
                    'companionScript_noticeLink' {'NoticeLink'}
                    'companionScript_spawnFairyAfterFindingCompanionInForest' {'SpawnRescueFairy'}
                    'companionScript_warpOutOfForest' {'WarpOut'}
                    default { throw "Unknown forest helper $args" }
                }
            }
            'orroomflag' { $arg0 = $args.TrimStart('$') }
            'setglobalflag' { if (!$globalFlagValues.ContainsKey($args)) { throw "Unknown forest flag $args" }; $arg0 = $globalFlagValues[$args].ToString('x2') }
            'unsetglobalflag' { if ($args -ne 'GLOBALFLAG_FOREST_UNSCRAMBLED') {throw "Unknown forest flag $args"}; $op='native'; $payload='ScrambleForest' }
            { $_ -in @('enableinput','disableinput','scriptend','checktext') } { }
            default { throw "Unsupported forest command $script : $op" }
        }
        $rows.Add((New-CutsceneCommandRow $script $i $command.Label $command.Line $op '' "$arg0" "$arg1" $payload))
    }
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
$forestRooms.Add("# group`troom`tsubid`tsource")
foreach ($roomMatch in [regex]::Matches($mainObjectSource, '(?ms)^group(?<group>[0-9])Map(?<room>[0-9a-f]{2})ObjectData:\s*(?<body>.*?)(?=^\w+:|\z)')) {
    foreach ($placement in [regex]::Matches($roomMatch.Groups['body'].Value, 'obj_Interaction \$71 \$(?<sub>0[89abc])')) {
        $forestRooms.Add("$($roomMatch.Groups['group'].Value)`t$($roomMatch.Groups['room'].Value)`t$($placement.Groups['sub'].Value)`tmainData.s:$($roomMatch.Value.Split(':')[0])")
    }
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\companion_forest_rooms.tsv'), $forestRooms)
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
    $role = if ($body -match 'obj_Interaction \$67 \$03') { 'preset' }
        elseif ($body -match 'obj_Interaction \$71 \$06') { 'goodbye' } else { $null }
    if ($null -ne $role) {
        $dimitriRooms.Add("$($roomMatch.Groups['group'].Value)`t$($roomMatch.Groups['room'].Value)`t$role`t30`t48`tmainData.s:$($roomMatch.Value.Split(':')[0])")
    }
}
if ($dimitriRooms.Count -ne 5) { throw 'Expected Dimitri preset and three mainland departure placements.' }
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\dimitri_rooms.tsv'), $dimitriRooms)
$dimitriCollisionSource = Read-ImportText (Join-Path $Disassembly 'data\ages\objectCollisionTable.s')
$dimitriCollisionBytes = @([regex]::Matches($dimitriCollisionSource, '(?m)^\s*\.db (?<bytes>[^;\r\n]+)') |
    ForEach-Object { [regex]::Matches($_.Groups['bytes'].Value, '\$([0-9a-f]{2})') } |
    ForEach-Object { [Convert]::ToInt32($_.Groups[1].Value, 16) })
if ($dimitriCollisionBytes.Count % 32 -ne 0) { throw 'objectCollisionTable.s row stride changed.' }
$dimitriCollisionRows = [Collections.Generic.List[string]]::new()
$dimitriCollisionRows.Add("# enemy-mode`teffect`tsource")
for ($mode = 0; $mode -lt $dimitriCollisionBytes.Count / 32; $mode++) {
    $effect = $dimitriCollisionBytes[$mode * 32 + 0x0f]
    if ($effect -notin @(0,0x1c,0x25,0x26)) { throw "Unsupported Dimitri mouth collision effect `$$( $effect.ToString('x2')) in enemy mode `$$( $mode.ToString('x2'))." }
    $dimitriCollisionRows.Add("$($mode.ToString('x2'))`t$($effect.ToString('x2'))`tobjectCollisionTable.s:ITEMCOLLISION_DIMITRI_MOUTH")
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\dimitri_collisions.tsv'), $dimitriCollisionRows)

$dimitriActiveSource = Read-ImportText (Join-Path $Disassembly 'data\ages\enemyActiveCollisions.s')
$dimitriActiveRows = [Collections.Generic.List[string]]::new()
$dimitriActiveRows.Add("# collision-type`tenabled`tsource")
$dimitriActiveMatches = [regex]::Matches($dimitriActiveSource, '(?m)^\s*dbrev (?<bits>(?:%[01]{8}\s+){3}%[01]{8})\s*; 0x(?<id>[0-9a-f]{2})')
foreach ($match in $dimitriActiveMatches) {
    $bits = $match.Groups['bits'].Value -replace '[%\s]', ''
    $id = $match.Groups['id'].Value
    $dimitriActiveRows.Add("$id`t$($bits[0x0f])`tenemyActiveCollisions.s:0x$id/ITEMCOLLISION_DIMITRI_MOUTH")
}
if ($dimitriActiveMatches.Count -ne 128) { throw 'Expected 128 enemyActiveCollisions.s rows for Dimitri collision gating.' }
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\dimitri_active_collisions.tsv'), $dimitriActiveRows)

# Native directional probes are ordered Y/X byte pairs. Collision probes are
# cumulative deltas; carry and cliff probes are independent offsets.
$dimitriNativeRows = [Collections.Generic.List[string]]::new()
$dimitriNativeRows.Add("# kind`tindex`tx`ty`tsource")
$dimitriCommonSource = Read-ImportText (Join-Path $Disassembly 'object_code\common\specialObjects\commonCode.s')
foreach ($probe in @(
    @{ Kind='carry'; Source=$dimitriSource; Pattern='(?ms)^dimitriTileOffsets:(?<body>(?:\s*\.db[^\r\n]*[\r\n]+){4})'; Count=4; Cumulative=$false; Label='dimitri.s:dimitriTileOffsets' },
    @{ Kind='cliff'; Source=$dimitriCommonSource; Pattern='(?ms)^companionCheckHopDownCliff:.*?^@directionOffsets:(?<body>(?:\s*\.db[^\r\n]*[\r\n]+){4})'; Count=4; Cumulative=$false; Label='commonCode.s:companionCheckHopDownCliff@directionOffsets' },
    @{ Kind='collision'; Source=$dimitriCommonSource; Pattern='(?ms)^companionCalculateAdjacentWallsBitset:.*?^@offsets:(?<body>(?:\s*\.db[^\r\n]*[\r\n]+){8})'; Count=8; Cumulative=$true; Label='commonCode.s:companionCalculateAdjacentWallsBitset@offsets' }
)) {
    $match = [regex]::Match($probe.Source, $probe.Pattern)
    if (!$match.Success) { throw "Missing Dimitri native probes: $($probe.Label)" }
    $pairs = @([regex]::Matches($match.Groups['body'].Value, '\.db\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})'))
    if ($pairs.Count -ne $probe.Count) { throw "Dimitri probe count changed: $($probe.Label)" }
    $nativeX = 0; $nativeY = 0
    for ($index=0; $index -lt $pairs.Count; $index++) {
        $x = [Convert]::ToInt32($pairs[$index].Groups['x'].Value,16)
        $y = [Convert]::ToInt32($pairs[$index].Groups['y'].Value,16)
        if ($x -ge 128) { $x -= 256 }; if ($y -ge 128) { $y -= 256 }
        if ($probe.Cumulative) { $nativeX += $x; $nativeY += $y } else { $nativeX=$x; $nativeY=$y }
        $dimitriNativeRows.Add("$($probe.Kind)`t$index`t$nativeX`t$nativeY`t$($probe.Label)")
    }
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\dimitri_native_probes.tsv'), $dimitriNativeRows)
$nativeCollisionSource = Read-ImportText (Join-Path $Disassembly 'code\bank0.s')
$nativeCollisionMatch = [regex]::Match($nativeCollisionSource, '(?ms)^checkCollisionPosition_disallowSmallBridges:.*?^@specialCollisions:(?<body>.*?)^_simpleCollision:')
$nativeMasks = @([regex]::Matches($nativeCollisionMatch.Groups['body'].Value, '%([01]{8})'))
if ($nativeMasks.Count -ne 16) { throw 'Companion special collision mask count changed.' }
$nativeMaskRows = @("# index`tmask`tsource")
for ($index=0; $index -lt 16; $index++) {
    $nativeMaskRows += "$index`t$([Convert]::ToInt32($nativeMasks[$index].Groups[1].Value,2).ToString('x2'))`tbank0.s:checkCollisionPosition_disallowSmallBridges@specialCollisions"
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\companion_collision_masks.tsv'), $nativeMaskRows)

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
$rickyBehaviorRows = @(
    "# idle-animation`tcancel-animation`tpunch-animation`tcharge-animation`thop-animation`tground-speed`thop-delay`thop-speed-z`thop-gravity`thop-speed`tlanding-delay`tpunch-lifetime`tpunch-damage`tpunch-boxes`tcharge-updates`ttornado-speed`ttornado-radius-y`ttornado-radius-x`ttornado-damage`ttornado-offsets`ttornado-sprite`ttornado-tile-base`ttornado-palette`ttornado-animation-base64`tjump-sound`tcharge-sound`tsword-spin-sound`tsword-slash-sound`tpunch-cue-sound`tlong-jump-animation`tlong-jump-speed-z`tlong-jump-delay`tlong-jump-speed`tcliff-down-delay`tcliff-down-speed-z`tvine-top-tile`tcompanion-collision-masks`thole-tiles`thole-offsets`tcliff-up-probes`tlanding-probes`twater-animation`thole-animation`tdeparture-air-animation`tdeparture-ground-animation-base`tdeparture-punch-animation`tdeparture-diagonal-angle`tdeparture-wall-probe-angle`tdeparture-exit-angle`tdeparture-hop-delay`tsource",
    "20`t05`t09`t13`t19`t1e`t16`t-384`t40`t50`t8`t20`t$rickyPunchDamage`t16,12,-12,0;12,18,-2,8;16,12,8,0;12,18,-2,-8`t30`t78`t$((($rickyTornadoRadius -shr 4) -band 0x0f))`t$($rickyTornadoRadius -band 0x0f)`t$rickyTornadoDamage`t-16,0;0,12;8,0;0,-12`tspr_common_sprites`t$($rickyTornadoTile.ToString('x2'))`t$rickyTornadoPalette`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($rickyTornadoAnimationEncoded)))`t53`t4f`t6b`t74`t75`t0f`t-768`t8`t32`t20`t-704`td4`t00,ff,03,c0,c3,c3,c3,00,00,ff,03,c0,c1,c1,ff,00`tf3,fd`t-8,0;5,8;8,0;5,-8`t-8,6;-8,-6;-24,6;-24,-6`t4,0;4,6;-2,0;4,-6`t0e`t0d`t03`t05`t18`t14`t18`t10`t8`tricky.s:rickyState2/rickyState5/rickyState7/rickyState8/rickyStateA;rickyMooshAttack.s:itemCode28;rickyTornado.s:itemCode2a;bank0.s:checkCollisionPosition_disallowSmallBridges;gfxHeaders.s:GFXH_COMMON_SPRITES"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\ricky_companion_behavior.tsv'),
    $rickyBehaviorRows)

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

function Write-MooshRescueCommands {
    param([string]$file, [string]$script, [object[]]$specs)
    $line = Get-AssemblySourceLine `
        $mooshScriptsSource "(?m)^$([regex]::Escape($script))\s*:" $script
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add($cutsceneCommandHeader)
    for ($index = 0; $index -lt $specs.Count; $index++) {
        $spec = $specs[$index]
        $rows.Add((New-CutsceneCommandRow `
            $script $index $script $line `
            $spec[0] $spec[1] $spec[2] $spec[3] $spec[4]))
    }
    Write-CutsceneGeneratedTable(
        (Join-Path $destination "cutscenes\$file"), $rows)
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

Write-MooshRescueCommands 'moosh_rescue_ghini0.tsv' `
    'ghiniHarassingMoosh_subid00Script' @(
        @('setdisabledobjects','','11','',''),
        @('nativeblock','Ghini0','32','','CircleGhini'),
        @('showtext','','1204','',(Moosh-Text 0x1204)),
        @('writememory','','01','','SignalOr'),
        @('checkmemoryeq','','01','','SignalBit10'),
        @('setdisabledobjects','','00','',''),
        @('native','','','','SpawnEnemyGhini0'),
        @('scriptend','','','',''))
Write-MooshRescueCommands 'moosh_rescue_ghini1.tsv' `
    'ghiniHarassingMoosh_subid01Script' @(
        @('checkmemoryeq','','01','','SignalBit01'),
        @('nativeblock','Ghini1','32','','CircleGhini'),
        @('showtext','','1205','',(Moosh-Text 0x1205)),
        @('writememory','','02','','SignalOr'),
        @('checkmemoryeq','','01','','SignalBit08'),
        @('nativeblock','Ghini1','32','','CircleGhini'),
        @('showtext','','1207','',(Moosh-Text 0x1207)),
        @('playsound','','c8','',''),
        @('setmusic','','2d','',''),
        @('writememory','','10','','SignalOr'),
        @('native','','','','SpawnEnemyGhini1'),
        @('scriptend','','','',''))
Write-MooshRescueCommands 'moosh_rescue_ghini2.tsv' `
    'ghiniHarassingMoosh_subid02Script' @(
        @('checkmemoryeq','','01','','SignalBit04'),
        @('nativeblock','Ghini2','32','','CircleGhini'),
        @('showtext','','1206','',(Moosh-Text 0x1206)),
        @('writememory','','08','','SignalOr'),
        @('checkmemoryeq','','01','','SignalBit10'),
        @('native','','','','SpawnEnemyGhini2'),
        @('scriptend','','','',''))
Write-MooshRescueCommands 'moosh_rescue_companion.tsv' `
    'companionScript_subid00Script' @(
        @('checkmemoryeq','','01','','SignalBit02'),
        @('nativeblock','Moosh','60','','ShakeMoosh'),
        @('showtext','','2200','',(Moosh-Text 0x2200)),
        @('writememory','','04','','SignalOr'),
        @('checkmemoryeq','','01','','SignalBit10'),
        @('checkmemoryeq','','00','','RoomEnemyCount'),
        @('playsound','','c8','',''), @('wait','','20','',''),
        @('playsound','','c8','',''), @('wait','','20','',''),
        @('playsound','','c8','',''),
        @('native','','','','RestoreRoomMusic'),
        @('native','','','','SetMooshTalkable'),
        @('checkmemoryeq','','01','','MooshTalked'),
        @('nativeblock','Moosh','60','','ShakeMoosh'),
        @('showtext','','2201','',(Moosh-Text 0x2201)),
        @('native','','','','SetMooshAwaitingLink'),
        @('native','','','','SpawnExclamation'),
        @('setdisabledobjects','','11','',''),
        @('native','','','','FaceMooshTowardLink'),
        @('wait','','60','',''),
        @('jumpifmemoryeq','','01','24','AlreadyMooshCompanion'),
        @('showtext','','2203','',(Moosh-Text 0x2203)),
        @('scriptjump','','25','',''),
        @('showtext','','2204','',(Moosh-Text 0x2204)),
        @('writememory','','20','','MooshStateOr'),
        @('setdisabledobjects','','00','',''),
        @('native','','','','BeginMooshMount'),
        @('checkmemoryeq','','01','','MooshMounted'),
        @('showtext','','2205','',(Moosh-Text 0x2205)),
        @('native','','','','CompleteMooshRescue'),
        @('scriptend','','','',''))
