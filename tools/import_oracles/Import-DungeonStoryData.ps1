# Room 0:5c's INTERAC_MISCELLANEOUS_2 $dc:$01 waits on the signal written by
# nextToOverworldKeyhole, then runs a compact script around two native tile
# removal helpers. Keep the command boundaries sourced from scripts.s while
# preserving the helper's ordered ordinary/interleaved writes and puff spawns.
$graveyardScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$graveyardScriptSource = Read-ImportText $graveyardScriptPath
$graveyardHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$graveyardControllerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous2.s')
$graveyardObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\mainData.s')

if ($graveyardControllerSource -notmatch '(?ms)^interactiondc_subid01:.*?checkInteractionState\s+jp nz,interactionRunScript.*?getThisRoomFlags\s+and \$80\s+jp nz,interactionDelete.*?mainScripts\.interactiondcSubid01Script.*?interactionSetAlwaysUpdateBit' -or
    $graveyardObjectSource -notmatch '(?ms)^group0Map5cObjectData:\s+obj_Interaction \$71 \$05 \$40 \$98\s+obj_Interaction \$dc \$01\s+obj_End' -or
    $graveyardHelperSource -notmatch '(?ms)^interactiondc_removeGraveyardGateTiles1:\s+ld a,\$0a\s+call setScreenShakeCounter\s+ld a,\$3a\s+ld c,\$34\s+call setTile\s+ld a,\$3a\s+ld c,\$44\s+call setTile.*?^@interleavedTiles:\s+\.db \$33 \$3a \$89 \$01\s+\.db \$35 \$3a \$89 \$03\s+\.db \$43 \$98 \$ec \$01\s+\.db \$45 \$9a \$ec \$03' -or
    $graveyardHelperSource -notmatch '(?ms)^interactiondc_removeGraveyardGateTiles2:\s+ld a,\$0a\s+call setScreenShakeCounter\s+ld a,\$3a\s+ld c,\$33\s+call setTile\s+ld a,\$3a\s+ld c,\$35\s+call setTile\s+ld a,\$3a\s+ld c,\$43\s+call setTile\s+ld a,\$3a\s+ld c,\$45\s+call setTile\s+ld bc,\$4830\s+call interactiondc_spawnPuff\s+ld bc,\$4860\s+jp interactiondc_spawnPuff' -or
    $graveyardHelperSource -notmatch '(?ms)^interactiondc_spawnPuff:.*?INTERAC_PUFF.*?Interaction\.yh\s+ld \(hl\),b.*?Interaction\.xh\s+ld \(hl\),c') {
    throw 'Room 0:5c graveyard-gate controller, object order, tile phases, or puff helper changed.'
}

$graveyardSupportedOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'checkcfc0bit', 'setmusic', 'wait', 'asm15', 'resetmusic',
    'playsound', 'enableinput', 'scriptend')) {
    [void]$graveyardSupportedOpcodes.Add($opcode)
}
$graveyardParsed = @(Read-AssemblyCutsceneCommands `
    $graveyardScriptPath 'interactiondcSubid01Script' $graveyardSupportedOpcodes)
$graveyardExpected = @(
    @('checkcfc0bit', '0'),
    @('setmusic', 'SNDCTRL_STOPMUSIC'),
    @('wait', '60'),
    @('asm15', 'scriptHelp.interactiondc_removeGraveyardGateTiles1'),
    @('wait', '45'),
    @('asm15', 'scriptHelp.interactiondc_removeGraveyardGateTiles2'),
    @('wait', '60'),
    @('resetmusic', ''),
    @('playsound', 'SND_SOLVEPUZZLE'),
    @('enableinput', ''),
    @('scriptend', '')
)
if ($graveyardParsed.Count -ne $graveyardExpected.Count) {
    throw "interactiondcSubid01Script expected 11 commands, parsed $($graveyardParsed.Count)."
}
for ($index = 0; $index -lt $graveyardExpected.Count; $index++) {
    $actualOperands = if ($null -eq $graveyardParsed[$index].Operands) {
        ''
    } else {
        ([string]$graveyardParsed[$index].Operands).Trim()
    }
    if ($graveyardParsed[$index].Opcode -ne $graveyardExpected[$index][0] -or
        $actualOperands -ne $graveyardExpected[$index][1]) {
        throw "interactiondcSubid01Script command $index changed from $($graveyardExpected[$index] -join ' ')."
    }
}

$graveyardEventRows = @(
    "# group`troom`tid`tsubid`troom-flag`tclear-tile`tshake-frames`tphase1-ordinary`tphase1-interleaved`tphase1-puffs`tphase2-ordinary`tphase2-puffs`tsource"
    "0`t5c`tdc`t01`t80`t3a`t10`t34,44`t33:3a:89:1,35:3a:89:3,43:98:ec:1,45:9a:ec:3`t48:40,48:50`t33,35,43,45`t48:30,48:60`tinteractiondcSubid01Script;scriptHelper.s:interactiondc_removeGraveyardGateTiles1/2"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\graveyard_gate_event.tsv'),
    $graveyardEventRows)

$graveyardCommandRows = [Collections.Generic.List[string]]::new()
$graveyardCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
$graveyardSpecs = @(
    @('setmusic', '', 'f0', '', ''),
    @('wait', '', '60', '', ''),
    @('native', '', '', '', 'RemoveGateTiles1'),
    @('wait', '', '45', '', ''),
    @('native', '', '', '', 'RemoveGateTiles2'),
    @('wait', '', '60', '', ''),
    @('setmusic', '', 'ff', '', ''),
    @('playsound', '', '4d', '', ''),
    @('enableinput', '', '', '', ''),
    @('scriptend', '', '', '', '')
)
for ($index = 0; $index -lt $graveyardSpecs.Count; $index++) {
    # Skip the leading checkcfc0bit: the room event remains armed until the
    # reusable keyhole controller supplies that exact signal.
    $sourceCommand = $graveyardParsed[$index + 1]
    $spec = $graveyardSpecs[$index]
    $graveyardCommandRows.Add((New-CutsceneCommandRow `
        'interactiondcSubid01Script' $index $sourceCommand.Label `
        $sourceCommand.Line $spec[0] $spec[1] $spec[2] $spec[3] $spec[4]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\graveyard_gate_commands.tsv'),
    $graveyardCommandRows)

# Present room 0:83's $dc:$02 watches the unique $c3 Bracelet rock. Once
# Link reaches grab state $83, it runs the native Wing Dungeon collapse,
# including the 6x6 BG maps and the persistent 3x3 layout/collision rewrite.
$wingInteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous2.s')
$wingCutsceneSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$wingRoomGfxSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\roomGfxChanges.s')
$wingGfxHeaderSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\gfxHeaders.s')
$wingSingleTileSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\singleTileChanges.s')
$wingObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\mainData.s')
$wingExtraObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\extraData4.s')

if ($wingObjectSource -notmatch '(?ms)^group0Map83ObjectData:\s+obj_Interaction \$d5 \$00 \$28 \$58\s+obj_Interaction \$dc \$02 \$48 \$38\s+obj_End') {
    throw 'Room 0:83 Wing Dungeon object order changed.'
}
if ($wingInteractionSource -notmatch '(?ms)^interactiondc_subid02:.*?interactionDeleteAndRetIfEnabled02.*?objectGetTileAtPosition.*?TILEINDEX_OVERWORLD_STANDARD_GROUND.*?TILEINDEX_OVERWORLD_DUG_DIRT.*?wLinkGrabState.*?cp \$83.*?DIR_RIGHT.*?ld a,30.*?DISABLE_LINK.*?resetLinkInvincibility.*?SNDCTRL_STOPMUSIC.*?ld \(hl\),60.*?ld a,60.*?ld bc,\$f800.*?objectCreateExclamationMark.*?clearAllParentItems.*?dropLinkHeldItem.*?ld a,\$28.*?setScreenShakeCounter.*?CUTSCENE_D2_COLLAPSE') {
    throw 'Room 0:83 interactiondc_subid02 changed.'
}
if ($wingCutsceneSource -notmatch '(?ms)^func_7168:.*?getThisRoomFlags.*?set 7,\(hl\).*?ROOM_AGES_073.*?set 7,\(hl\).*?ld a,\$3c.*?reloadTileMap.*?INTERAC_97.*?ld \(hl\),\$2c.*?ld \(hl\),\$58.*?GFXH_WING_DUNGEON_COLLAPSING_1.*?ld a,\$0f.*?setScreenShakeCounter.*?SND_DOORCLOSE.*?GFXH_WING_DUNGEON_COLLAPSING_2.*?GFXH_WING_DUNGEON_COLLAPSING_3.*?drawCollapsedWingDungeon.*?objectData7e69.*?wDisabledObjects.*?wMenuDisabled.*?wActiveMusic') {
    throw 'CUTSCENE_D2_COLLAPSE changed.'
}
if ($wingRoomGfxSource -notmatch '(?ms)^roomTileChangesAfterLoad00:.*?and \$80.*?ret z.*?^drawCollapsedWingDungeon:.*?GFXH_WING_DUNGEON_COLLAPSED.*?^@tileReplacement:.*?\.db \$06 \$06.*?w3VramTiles\+\$08.*?w2TmpGfxBuffer.*?^@layoutReplacement:.*?wRoomLayout\+\$04.*?\.db \$03 \$03.*?\.db \$3b \$00 \$3b \$00 \$3b \$00.*?\.db \$3b \$00 \$3b \$00 \$3b \$00.*?\.db \$00 \$05 \$00 \$0f \$00 \$0a') {
    throw 'drawCollapsedWingDungeon changed.'
}
if ($wingSingleTileSource -notmatch '(?m)^\s*\.db \$83 \$80 \$43 \$1c\s*$') {
    throw 'Room 0:83 persistent single-tile change changed.'
}
if ($wingExtraObjectSource -notmatch '(?ms)^objectData7e69:\s+obj_Interaction \$8a \$00 \$00 \$00 \$01\s+obj_End') {
    throw 'Wing Dungeon post-collapse remote Maku objectData7e69 changed.'
}

$wingGfxSpecs = @(
    @(0, 0x50, 'map_wing_dungeon_collapsing_1'),
    @(1, 0x51, 'map_wing_dungeon_collapsing_2'),
    @(2, 0x52, 'map_wing_dungeon_collapsing_3'),
    @(3, 0x53, 'map_wing_dungeon_collapsed')
)
$wingMapRows = [Collections.Generic.List[string]]::new()
$wingMapRows.Add("# phase`tgfx-header`ttile-ids`tsource")
foreach ($spec in $wingGfxSpecs) {
    $phase = [int]$spec[0]
    $header = [int]$spec[1]
    $name = [string]$spec[2]
    $headerPattern =
        "(?ms)^m_GfxHeaderStart \`$$($header.ToString('x2')), GFXH_" +
        "(?:WING_DUNGEON_COLLAPSING_$($phase + 1)|WING_DUNGEON_COLLAPSED)" +
        "\s+m_GfxHeader $name, w2TmpGfxBuffer\s+m_GfxHeaderEnd"
    if ($wingGfxHeaderSource -notmatch $headerPattern) {
        throw "Could not verify Wing Dungeon GFX header `$$($header.ToString('x2'))."
    }
    $mapPath = Join-Path $Disassembly "gfx_compressible\ages\$name.bin"
    $bytes = [IO.File]::ReadAllBytes($mapPath)
    if ($bytes.Length -ne 192) {
        throw "$mapPath expected 192 bytes, got $($bytes.Length)."
    }
    $tileIds = [Collections.Generic.List[string]]::new()
    for ($row = 0; $row -lt 6; $row++) {
        for ($column = 0; $column -lt 6; $column++) {
            $tileIds.Add($bytes[$row * 32 + $column].ToString('x2'))
        }
    }
    $wingMapRows.Add(
        "$phase`t$($header.ToString('x2'))`t$($tileIds -join ',')`t" +
        "gfxHeaders.s:GFXH_$($header.ToString('x2'));$name.bin")
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\wing_dungeon_collapse_maps.tsv'),
    $wingMapRows)

$wingExclamationGraphic = $interactionGraphics['159:0']
$wingExclamationAnimation = Resolve-NpcAnimation 0x9f 0
if ($null -eq $wingExclamationGraphic -or
    -not $gfxNames.ContainsKey($wingExclamationGraphic.Gfx) -or
    -not $wingExclamationAnimation) {
    throw 'Could not resolve INTERAC_EXCLAMATION_MARK graphics for room 0:83.'
}
$wingEventRows = @(
    "# group`troom`tid`tsubid`ty`tx`trock-position`trock-tile`tground-tile`tdug-tile`troom-flag`tlinked-room`tlinked-room-flag`tpickup-wait`texclamation-frames`tpre-collapse-shake`tcollapse-initial-wait`tphase-wait`tfinal-wait`tcollapse-shake`tdust-y`tdust-x`tdust-frames`tdust-interval`texclamation-id`texclamation-subid`texclamation-sprite`texclamation-tile-base`texclamation-palette`texclamation-animation`tfacade-position`tfacade-width`tfacade-height`tfinal-tiles`tfinal-collisions`tsource",
    (@(
        '0', '83', 'dc', '02', '48', '38', '43', 'c3', '3a', '1c',
        '80', '73', '80', '30', '60', '40', '60', '30', '60', '15',
        '2c', '58', '106', '3', '9f', '00',
        $gfxNames[$wingExclamationGraphic.Gfx],
        $wingExclamationGraphic.TileBase.ToString(),
        $wingExclamationGraphic.Palette.ToString(),
        $wingExclamationAnimation,
        '04', '3', '3',
        '3b,3b,3b,3b,3b,3b,00,00,00',
        '00,00,00,00,00,00,05,0f,0a',
        'miscellaneous2.s:interactiondc_subid02;miscCutscenes.s:CUTSCENE_D2_COLLAPSE;roomGfxChanges.s:drawCollapsedWingDungeon'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\wing_dungeon_collapse_event.tsv'),
    $wingEventRows)

# INTERAC_REMOTE_MAKU_CUTSCENE preserves sprite palette 0 while the background
# fades to black, runs the era-selected $62 confetti emitter, reports the next
# objective through TX_05b0-$05bb (TX_05c0-$05cb in a linked game), then
# updates the corresponding Maku map/state bytes. Import the supported room
# 0:8d first-Essence, room 0:83 Wing Dungeon, room 0:3a post-Harp, room 1:83
# second-Essence, and post-D3 room 0:ba lanes while retaining each native
# var03 predicate and dynamic-spawn boundary.
$remoteMakuScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$remoteMakuScriptSource = Read-ImportText $remoteMakuScriptPath
$remoteMakuHelperPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$remoteMakuHelperSource = Read-ImportText $remoteMakuHelperPath
$remoteMakuInteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\remoteMakuCutscene.s')
$makuConfettiSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\makuConfetti.s')
$sparkleSourceForRemoteMaku = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\sparkle.s')
$remoteMakuObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\mainData.s')
$postD3InteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous1.s')
$postD3AmbiSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\ambi.s')
$postD3NayruSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\nayru.s')
$postD3ExtraObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\extraData3.s')

if ($remoteMakuObjectSource -notmatch '(?ms)^group0Map8dObjectData:\s+obj_Interaction \$8a \$00 \$00 \$00 \$00\s+obj_End' -or
    $remoteMakuObjectSource -notmatch '(?ms)^group0Map3aObjectData:.*?obj_Interaction \$8a \$00 \$00 \$00 \$02.*?obj_End' -or
    $remoteMakuObjectSource -notmatch '(?ms)^group1Map83ObjectData:\s+obj_Interaction \$41 \$00 \$38 \$4e\s+obj_Interaction \$8a \$01 \$00 \$00 \$03\s+obj_Pointer group1Map83EnemyObjectData\s+obj_End' -or
    $remoteMakuInteractionSource -notmatch '(?ms)^@state0:.*?returnIfScrollMode01Unset.*?^@checkConditionsAndSetText:.*?^@val00:\s+xor a\s+call @checkEssenceObtained\s+jp z,@deleteSelfAndReturn\s+ldbc \$00, <TX_05b0.*?^@checkEssenceObtained:\s+ld hl,wEssencesObtained\s+jp checkFlag' -or
    $remoteMakuInteractionSource -notmatch '(?ms)^@val02:\s+ld a,TREASURE_HARP\s+call checkTreasureObtained\s+jp nc,@deleteSelfAndReturn\s+ldbc \$00, <TX_05b2\s+jp @setTextForScript' -or
    $remoteMakuInteractionSource -notmatch '(?ms)^@val03:\s+ld a,\$01\s+call @checkEssenceObtained\s+jp z,@deleteSelfAndReturn\s+ldbc \$00, <TX_05b3\s+jp @setTextForScript' -or
    $remoteMakuInteractionSource -notmatch '(?ms)^@val04:\s+ld a,\$02\s+call @checkEssenceObtained\s+jp z,@deleteSelfAndReturn\s+ld hl,wPastRoomFlags\+\$76\s+set 0,\(hl\)\s+call checkIsLinkedGame\s+ld a,GLOBALFLAG_CAN_BUY_FLUTE\s+call z,setGlobalFlag\s+ldbc \$00, <TX_05b4\s+jp @setTextForScript' -or
    $remoteMakuInteractionSource -notmatch '(?ms)^@state0:.*?getThisRoomFlags\s+and \$40\s+jp nz,interactionDelete.*?^@scriptTable:\s+\.dw mainScripts\.remoteMakuCutsceneScript' -or
    $remoteMakuHelperSource -notmatch '(?ms)^remoteMakuCutscene_fadeoutToBlackWithDelay:.*?fadeoutToBlackWithDelay.*?ld a,\$ff\s+ld \(wDirtyFadeBgPalettes\),a\s+ld \(wFadeBgPaletteSources\),a\s+ld a,\$01\s+ld \(wDirtyFadeSprPalettes\),a\s+ld a,\$fe\s+ld \(wFadeSprPaletteSources\),a' -or
    $remoteMakuHelperSource -notmatch '(?ms)^makuTree_modifyTextIndexForLinked:.*?checkIsLinkedGame.*?^@getLinkedTextOffset:.*?INTERAC_REMOTE_MAKU_CUTSCENE.*?dec a.*?INTERAC_MAKU_TREE.*?^makuTree_textOffsetsForLinked:\s+\.db \$20, \$20, \$10') {
    throw 'Remote-Maku placement, predicate, palette mask, or linked-text offset changed.'
}

if ($remoteMakuObjectSource -notmatch
        '(?ms)^group0MapbaObjectData:\s+obj_Interaction \$6b \$06\s+obj_End' -or
    $postD3InteractionSource -notmatch
        '(?ms)^interaction6b_subid06:.*?^@state0:.*?wEssencesObtained.*?bit 2,a.*?getThisRoomFlags.*?and \$40.*?wDisabledObjects.*?wMenuDisabled.*?Interaction\.counter1.*?\(hl\),90.*?^@substate0:.*?interactionDecCounter1.*?wGenericCutscene\.cbb3.*?wGenericCutscene\.cbba.*?SND_LIGHTNING.*?^@substate1:.*?ld b,\$01.*?flashScreen.*?fadeoutToWhite.*?^@substate2:.*?\$0116.*?disableLcdAndLoadRoom.*?ambiAndNayruInPostD3Cutscene.*?MUS_DISASTER.*?fadeinFromWhite' -or
    $postD3ExtraObjectSource -notmatch
        '(?ms)^ambiAndNayruInPostD3Cutscene:\s+obj_Interaction \$4d \$08 \$28 \$48\s+obj_Interaction \$36 \$0e \$28 \$58\s+obj_End' -or
    $remoteMakuScriptSource -notmatch
        '(?ms)^ambiSubid08Script:\s+checkpalettefadedone\s+wait 60\s+showtext TX_1316\s+wait 60\s+asm15 fadeoutToWhite\s+checkpalettefadedone\s+scriptend' -or
    $postD3AmbiSource -notmatch
        '(?ms)^ambi_runSubid08:.*?ambi_updateAnimationAndRunScript.*?ld a,\$01.*?\(\$cbb8\).*?CUTSCENE_BLACK_TOWER_EXPLANATION.*?wCutsceneTrigger' -or
    $postD3NayruSource -notmatch
        '(?ms)^@init0e:.*?ld a,\$06.*?Interaction\.oamFlags.*?^@loadEvilPalette:.*?PALH_97' -or
    $blackTowerCutsceneSource -notmatch
        '(?ms)^@cbb8_01:.*?^@@state5:.*?decCbb3.*?fadeoutToWhite.*?^@@state6:.*?ROOM_AGES_0ba.*?INTERAC_REMOTE_MAKU_CUTSCENE.*?\(hl\),\$04.*?w1Link\.yh.*?\(hl\),\$65.*?w1Link\.xh.*?\(hl\),\$58.*?DIR_DOWN.*?SNDCTRL_STOPMUSIC.*?fadeinFromWhiteToRoom.*?showStatusBar.*?^@@state7:.*?wMenuDisabled.*?wDisabledObjects') {
    throw 'Room 0:ba post-D3 palace, tower, or remote-Maku route changed.'
}

$remoteMakuSupportedOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'disableinput', 'writememory', 'setmusic', 'wait', 'asm15',
    'checkpalettefadedone', 'jumpifobjectbyteeq', 'spawninteraction',
    'scriptjump', 'resetmusic', 'orroomflag', 'enableinput', 'scriptend')) {
    [void]$remoteMakuSupportedOpcodes.Add($opcode)
}
$remoteMakuParsed = @(Read-AssemblyCutsceneCommands `
    $remoteMakuScriptPath 'remoteMakuCutsceneScript' $remoteMakuSupportedOpcodes)
$remoteMakuExpected = @(
    @('disableinput', ''),
    @('writememory', 'wTextboxFlags, TEXTBOXFLAG_ALTPALETTE1'),
    @('setmusic', 'MUS_MAKU_TREE'),
    @('wait', '40'),
    @('writememory', 'wDontUpdateStatusBar, $77'),
    @('asm15', 'hideStatusBar'),
    @('asm15', 'scriptHelp.remoteMakuCutscene_fadeoutToBlackWithDelay, $02'),
    @('checkpalettefadedone', ''),
    @('jumpifobjectbyteeq', 'Interaction.subid, $01, @past'),
    @('spawninteraction', 'INTERAC_MAKU_CONFETTI, $00, $00, $00'),
    @('wait', '240'),
    @('wait', '180'),
    @('scriptjump', '++'),
    @('spawninteraction', 'INTERAC_MAKU_CONFETTI, $01, $00, $00'),
    @('wait', '240'),
    @('wait', '60'),
    @('asm15', 'scriptHelp.makuTree_showTextWithOffsetAndUpdateMapText, $00'),
    @('wait', '1'),
    @('asm15', 'showStatusBar'),
    @('asm15', 'clearFadingPalettes'),
    @('asm15', 'scriptHelp.remoteMakuCutscene_checkinitUnderwaterWaves'),
    @('asm15', 'fadeinFromWhiteWithDelay, $02'),
    @('checkpalettefadedone', ''),
    @('resetmusic', ''),
    @('orroomflag', '$40'),
    @('asm15', 'incMakuTreeState'),
    @('jumpifobjectbyteeq', 'Interaction.var03, $07, @spawnGoronAfterCrownDungeon'),
    @('enableinput', ''),
    @('scriptend', ''),
    @('spawninteraction', 'INTERAC_GORON, $03, $58, $a8'),
    @('scriptend', '')
)
if ($remoteMakuParsed.Count -ne $remoteMakuExpected.Count) {
    throw "remoteMakuCutsceneScript expected 31 commands, parsed $($remoteMakuParsed.Count)."
}
for ($index = 0; $index -lt $remoteMakuExpected.Count; $index++) {
    $actualOperands = if ($null -eq $remoteMakuParsed[$index].Operands) {
        ''
    } else {
        ([string]$remoteMakuParsed[$index].Operands).Trim()
    }
    if ($remoteMakuParsed[$index].Opcode -ne $remoteMakuExpected[$index][0] -or
        $actualOperands -ne $remoteMakuExpected[$index][1]) {
        throw "remoteMakuCutsceneScript command $index changed from $($remoteMakuExpected[$index] -join ' ')."
    }
}

$confettiData = [regex]::Match(
    $makuConfettiSource,
    '(?ms)^@initialPositionsAndAccelerations:\s*(?<positions>(?:\s*dbbww[^\r\n]+\r?\n){5}).*?^@spawnDelayValues:\s+\.db \$01 \$32 \$14 \$1e \$28 \$1e.*?^@yOffset:\s+\.dw \$00c0')
$confettiPositions = @([regex]::Matches(
    $confettiData.Groups['positions'].Value,
    'dbbww \$(?<y>[0-9a-f]{2}), \$(?<x>[0-9a-f]{2}), \$(?<ay>[0-9a-f]{4}), \$(?<ax>[0-9a-f]{4})'))
if (-not $confettiData.Success -or $confettiPositions.Count -ne 5 -or
    $makuConfettiSource -notmatch '(?ms)^@state1:.*?Interaction\.counter2.*?180.*?SND_MAGIC_POWDER.*?cp \$05.*?interactionDelete' -or
    $makuConfettiSource -notmatch '(?ms)^@state2:.*?Interaction\.var3a.*?\$18.*?@makeSparkle.*?Interaction\.yh.*?cp \$88.*?cp \$d8.*?speedY > \$100.*?speedX > \$200.*?interactionSetAnimation' -or
    $makuConfettiSource -notmatch '(?ms)^@makeSparkle:.*?INTERAC_SPARKLE.*?\$02.*?objectCopyPosition' -or
    $sparkleSourceForRemoteMaku -notmatch '(?ms)^@initSubid02:.*?objectSetVisible82.*?^@runSubid02:.*?objectApplyComponentSpeed.*?Interaction\.animParameter.*?cp \$ff.*?interactionDelete.*?interactionAnimate') {
    throw 'Present Maku confetti positions, counters, movement, sparkle, or deletion rules changed.'
}

$pastConfettiBlock = [regex]::Match(
    $makuConfettiSource,
    '(?ms)^makuConfetti_subid1:(?<body>.*?)(?=^makuConfetti_updateSpeedY:)')
$pastConfettiPositions = @([regex]::Matches(
    $pastConfettiBlock.Groups['body'].Value,
    '(?m)^\s*\.db \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})\s*;'))
$pastConfettiDelayMatch = [regex]::Match(
    $pastConfettiBlock.Groups['body'].Value,
    '(?ms)^@spawnDelayValues:\s*(?<delays>(?:^[ \t]*\.db[^\r\n]*\r?\n?)+)')
$pastConfettiDelays = @([regex]::Matches(
    $pastConfettiDelayMatch.Groups['delays'].Value,
    '\$(?<value>[0-9a-f]{2})') | ForEach-Object {
        [Convert]::ToInt32($_.Groups['value'].Value, 16)
    })
if (-not $pastConfettiBlock.Success -or
    $pastConfettiPositions.Count -ne 6 -or
    -not $pastConfettiDelayMatch.Success -or
    $pastConfettiDelays.Count -ne 12 -or
    ($pastConfettiDelays -join ',') -ne
        '1,50,30,15,15,15,15,15,15,15,15,20' -or
    $pastConfettiBlock.Groups['body'].Value -notmatch
        '(?ms)^@state0:.*?Interaction\.counter2\s+ld \(hl\),\$0a.*?ld b,\$80\s+ld c,\$fd\s+call @setSpeedComponent.*?ld b,\$00\s+ld c,\$04\s+call @setSpeedComponent.*?ld b,\$f0\s+ld c,\$ff\s+call @setSpeedComponent' -or
    $pastConfettiBlock.Groups['body'].Value -notmatch
        '(?ms)^@state1:.*?interactionDecCounter2.*?ld \(hl\),45\s+ld a,SND_MAKU_TREE_PAST.*?Interaction\.counter1.*?cp 12\s+jp z,interactionDelete' -or
    $pastConfettiBlock.Groups['body'].Value -notmatch
        '(?ms)^@state2:\s+call makuConfetti_updateSpeedXUsingSpeedZ\s+ld e,Interaction\.speedX\+1\s+ld a,\(de\)\s+bit 7,a\s+jp nz,interactionDelete\s+jp objectApplyComponentSpeed') {
    throw 'Past Maku confetti positions, counters, movement, or deletion rules changed.'
}

$confettiGraphic = $interactionGraphics['98:0']
$confettiAnimations = @(0..1 | ForEach-Object { Resolve-NpcAnimation 0x62 $_ })
$pastConfettiGraphic = $interactionGraphics['98:1']
$pastConfettiAnimation = Resolve-NpcAnimation 0x62 0
$remoteMakuSparkleGraphic = $interactionGraphics['132:2']
$remoteMakuSparkleAnimation = Resolve-NpcAnimation 0x84 $remoteMakuSparkleGraphic.DefaultAnimation
if ($null -eq $confettiGraphic -or $confettiGraphic.Gfx -ne 0x6c -or
    $confettiGraphic.TileBase -ne 4 -or $confettiGraphic.Palette -ne 2 -or
    ($confettiAnimations | Where-Object { -not $_ }).Count -ne 0 -or
    $null -eq $pastConfettiGraphic -or
    $pastConfettiGraphic.Gfx -ne 0x6c -or
    $pastConfettiGraphic.TileBase -ne 0 -or
    $pastConfettiGraphic.Palette -ne 3 -or
    $pastConfettiGraphic.DefaultAnimation -ne 0 -or
    -not $pastConfettiAnimation -or
    $null -eq $remoteMakuSparkleGraphic -or
    $remoteMakuSparkleGraphic.Gfx -ne 0x6b -or
    $remoteMakuSparkleGraphic.TileBase -ne 0x0a -or
    $remoteMakuSparkleGraphic.Palette -ne 0 -or
    $remoteMakuSparkleGraphic.DefaultAnimation -ne 1 -or
    -not $remoteMakuSparkleAnimation) {
    throw 'INTERAC_MAKU_CONFETTI or its $84:$02 sparkle graphics changed.'
}
if (-not $allTexts.ContainsKey(0x05b0) -or
    -not $allTexts.ContainsKey(0x05c0) -or
    -not $allTexts.ContainsKey(0x05b1) -or
    -not $allTexts.ContainsKey(0x05c1) -or
    -not $allTexts.ContainsKey(0x05b2) -or
    -not $allTexts.ContainsKey(0x05c2) -or
    -not $allTexts.ContainsKey(0x05b3) -or
    -not $allTexts.ContainsKey(0x05c3) -or
    -not $allTexts.ContainsKey(0x05b4) -or
    -not $allTexts.ContainsKey(0x05c4) -or
    -not $allTexts.ContainsKey(0x1316) -or
    -not $allTexts.ContainsKey(0x1317)) {
    throw 'Remote Maku text through TX_05b4/TX_05c4 or post-D3 text TX_1316/TX_1317 was not imported.'
}

$positionPayload = @($confettiPositions | ForEach-Object {
    $y = [Convert]::ToInt32($_.Groups['y'].Value, 16)
    if ($y -ge 0x80) { $y -= 0x100 }
    $x = [Convert]::ToInt32($_.Groups['x'].Value, 16)
    $ay = [Convert]::ToInt32($_.Groups['ay'].Value, 16)
    $ax = [Convert]::ToInt32($_.Groups['ax'].Value, 16)
    "$y`:$x`:$ay`:$ax"
}) -join ','
$pastPositionPayload = @(
    for ($index = 0; $index -lt 12; $index++) {
        $position = $pastConfettiPositions[$index % 6]
        $y = [Convert]::ToInt32($position.Groups['y'].Value, 16)
        $x = [Convert]::ToInt32($position.Groups['x'].Value, 16)
        "$y`:$x`:0`:0"
    }
) -join ','
$remoteMakuEventHeader =
    "# group`troom`tid`tsubid`tvar03`tessence-mask`trequired-treasure`troom-flag`tstandard-text-id`tlinked-text-id`tstandard-map-text`tlinked-map-text`tmusic`thud-lock-byte`tfade-delay`tfade-frames`tinitial-wait`tconfetti-hold1`tconfetti-hold2`tpost-text-wait`tconfetti-pieces`tspawn-delays`tpositions-and-accelerations`ty-offset-fixed`tsparkle-initial-delay`tsparkle-repeat-delay`tsound-counter`tsound`ty-speed-limit`tx-speed-limit`tdelete-y`tconfetti-kind`tsound-initial-counter`tinitial-speed-y`tinitial-speed-x`tacceleration-x"
$remoteMakuEventRows = @(
    $remoteMakuEventHeader
    "0`t8d`t8a`t00`t00`t01`tff`t40`t05b0`t05c0`tb0`tc0`t1e`t77`t2`t65`t40`t240`t180`t1`t5`t1,50,20,30,40,30`t$positionPayload`t192`t16`t24`t180`t83`t256`t512`t136`tpresent`t180`t0`t0`t0"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_first_essence_event.tsv'),
    $remoteMakuEventRows)
$remoteMakuWingEventRows = @(
    $remoteMakuEventRows[0]
    "0`t83`t8a`t00`t01`t00`tff`t40`t05b1`t05c1`tb1`tc1`t1e`t77`t2`t65`t40`t240`t180`t1`t5`t1,50,20,30,40,30`t$positionPayload`t192`t16`t24`t180`t83`t256`t512`t136`tpresent`t180`t0`t0`t0"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_wing_dungeon_event.tsv'),
    $remoteMakuWingEventRows)
$remoteMakuHarpEventRows = @(
    $remoteMakuEventRows[0]
    "0`t3a`t8a`t00`t02`t00`t$($treasureIds['TREASURE_HARP'].ToString('x2'))`t40`t05b2`t05c2`tb2`tc2`t1e`t77`t2`t65`t40`t240`t180`t1`t5`t1,50,20,30,40,30`t$positionPayload`t192`t16`t24`t180`t83`t256`t512`t136`tpresent`t180`t0`t0`t0"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_harp_event.tsv'),
    $remoteMakuHarpEventRows)
$remoteMakuSecondEssenceEventRows = @(
    $remoteMakuEventHeader
    "1`t83`t8a`t01`t03`t02`tff`t40`t05b3`t05c3`tb3`tc3`t1e`t77`t2`t65`t40`t240`t60`t1`t12`t$($pastConfettiDelays -join ',')`t$pastPositionPayload`t0`t0`t0`t45`tce`t0`t0`t0`tpast`t10`t-640`t1024`t-16"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_second_essence_event.tsv'),
    $remoteMakuSecondEssenceEventRows)
$remoteMakuThirdEssenceEventRows = @(
    $remoteMakuEventHeader
    "0`tba`t8a`t00`t04`t04`tff`t40`t05b4`t05c4`tb4`tc4`t1e`t77`t2`t65`t40`t240`t180`t1`t5`t1,50,20,30,40,30`t$positionPayload`t192`t16`t24`t180`t83`t256`t512`t136`tpresent`t180`t0`t0`t0"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_third_essence_event.tsv'),
    $remoteMakuThirdEssenceEventRows)

$postD3EventRows = @(
    "# group`troom`tid`tsubid`tessence-mask`troom-flag`tinitial-wait`tflash-frames`tfade-frames`tpalace-group`tpalace-room`tambi-id`tambi-subid`tambi-y`tambi-x`tnayru-id`tnayru-subid`tnayru-y`tnayru-x`tpalace-wait`tpalace-post-wait`tpalace-text-id`tpalace-text-base64`texplanation-wait`texplanation-post-wait`texplanation-text-id`texplanation-text-base64`texplanation-textbox-flags`tscreen-offset-y`treturn-y`treturn-x`treturn-direction`tpast-flag-group`tpast-flag-room`tpast-room-flag`tstandard-global-flag`tmusic`tsource",
    (@(
        '0', 'ba', '6b', '06', '04', '40', '90', '13', '32',
        '1', '16', '4d', '08', '28', '48', '36', '0e', '28', '58',
        '60', '60', '1316',
        (ConvertTo-CutsceneCommandPayload $allTexts[0x1316]),
        '60', '60', '1317',
        (ConvertTo-CutsceneCommandPayload $allTexts[0x1317]),
        '01', '70', '65', '58', '02', '1', '76', '01',
        $globalFlagValues['GLOBALFLAG_CAN_BUY_FLUTE'].ToString('x2'),
        '21',
        'miscellaneous1.s:interaction6b_subid06;extraData3.s:ambiAndNayruInPostD3Cutscene;miscCutscenes.s:CUTSCENE_BLACK_TOWER_EXPLANATION/cbb8_01'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\post_d3_remote_maku_event.tsv'),
    $postD3EventRows)

$remoteMakuVisualRows = @(
    "# key`tsprite`ttile-base`tpalette`tanimation"
    "confetti-left`t$($gfxNames[$confettiGraphic.Gfx])`t$($confettiGraphic.TileBase)`t$($confettiGraphic.Palette)`t$($confettiAnimations[0])"
    "confetti-right`t$($gfxNames[$confettiGraphic.Gfx])`t$($confettiGraphic.TileBase)`t$($confettiGraphic.Palette)`t$($confettiAnimations[1])"
    "confetti-past`t$($gfxNames[$pastConfettiGraphic.Gfx])`t$($pastConfettiGraphic.TileBase)`t$($pastConfettiGraphic.Palette)`t$pastConfettiAnimation"
    "sparkle`t$($gfxNames[$remoteMakuSparkleGraphic.Gfx])`t$($remoteMakuSparkleGraphic.TileBase)`t$($remoteMakuSparkleGraphic.Palette)`t$remoteMakuSparkleAnimation"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_first_essence_visuals.tsv'),
    $remoteMakuVisualRows)
Copy-GeneratedFile `
    "gfx_compressible\ages\$($gfxNames[$confettiGraphic.Gfx]).png" `
    "gfx\$($gfxNames[$confettiGraphic.Gfx]).png"
Copy-GeneratedFile `
    "gfx_compressible\ages\$($gfxNames[$remoteMakuSparkleGraphic.Gfx]).png" `
    "gfx\$($gfxNames[$remoteMakuSparkleGraphic.Gfx]).png"

$remoteMakuCommandHeader =
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64"
$remoteMakuCommandSpecs = @(
    @($remoteMakuParsed[0],  'disableinput', '', '', '', ''),
    @($remoteMakuParsed[1],  'writememory', '', '04', '', 'TextboxFlags'),
    @($remoteMakuParsed[2],  'setmusic', '', '1e', '', ''),
    @($remoteMakuParsed[3],  'wait', '', '40', '', ''),
    @($remoteMakuParsed[4],  'writememory', '', '77', '', 'DontUpdateStatusBar'),
    @($remoteMakuParsed[5],  'native', '', '', '', 'HideHud'),
    @($remoteMakuParsed[6],  'nativeblock', '', '65', '', "FadeOutBlack`0"),
    @($remoteMakuParsed[9],  'native', '', '', '', 'SpawnPresentConfetti'),
    @($remoteMakuParsed[10], 'wait', '', '240', '', ''),
    @($remoteMakuParsed[11], 'wait', '', '180', '', ''),
    @($remoteMakuParsed[16], 'showtextdifferentforlinked', '', '', '', ''),
    @($remoteMakuParsed[17], 'wait', '', '1', '', ''),
    @($remoteMakuParsed[18], 'native', '', '', '', 'ShowHud'),
    @($remoteMakuParsed[19], 'native', '', '', '', 'ClearFadingPalettes'),
    @($remoteMakuParsed[21], 'nativeblock', '', '65', '', "FadeInWhite`0"),
    @($remoteMakuParsed[23], 'native', '', '', '', 'ResetMusic'),
    @($remoteMakuParsed[24], 'orroomflag', '', '40', '', ''),
    @($remoteMakuParsed[25], 'native', '', '', '', 'IncMakuTreeState'),
    @($remoteMakuParsed[27], 'enableinput', '', '', '', ''),
    @($remoteMakuParsed[28], 'scriptend', '', '', '', '')
)
# These placements execute the same @present branch. Only @val00/@val01/
# @val02/@val04's text selection differs; preserve the shared source order.
foreach ($variant in @(
    @('first_essence', 0x05b0, 0x05c0),
    @('wing_dungeon', 0x05b1, 0x05c1),
    @('harp', 0x05b2, 0x05c2),
    @('third_essence', 0x05b4, 0x05c4)
)) {
    $remoteMakuCommandRows = [Collections.Generic.List[string]]::new()
    $remoteMakuCommandRows.Add($remoteMakuCommandHeader)
    for ($index = 0; $index -lt $remoteMakuCommandSpecs.Count; $index++) {
        $spec = $remoteMakuCommandSpecs[$index]
        $sourceCommand = $spec[0]
        $arg0 = $spec[3]
        $arg1 = $spec[4]
        $payload = $spec[5]
        if ($spec[1] -eq 'showtextdifferentforlinked') {
            $arg0 = $variant[1].ToString('x4')
            $arg1 = $variant[2].ToString('x4')
            $payload = "$($allTexts[$variant[1]])`0$($allTexts[$variant[2]])"
        }
        $remoteMakuCommandRows.Add((New-CutsceneCommandRow `
            'remoteMakuCutsceneScript' $index $sourceCommand.Label `
            $sourceCommand.Line $spec[1] $spec[2] $arg0 $arg1 $payload))
    }
    Write-CutsceneGeneratedTable(
        (Join-Path $destination "cutscenes\remote_maku_$($variant[0])_commands.tsv"),
        $remoteMakuCommandRows)
}
$remoteMakuSecondEssenceCommandRows =
    [Collections.Generic.List[string]]::new()
$remoteMakuSecondEssenceCommandRows.Add($remoteMakuCommandHeader)
$remoteMakuSecondEssenceCommandSpecs = @(
    @($remoteMakuParsed[0],  'disableinput', '', '', '', ''),
    @($remoteMakuParsed[1],  'writememory', '', '04', '', 'TextboxFlags'),
    @($remoteMakuParsed[2],  'setmusic', '', '1e', '', ''),
    @($remoteMakuParsed[3],  'wait', '', '40', '', ''),
    @($remoteMakuParsed[4],  'writememory', '', '77', '', 'DontUpdateStatusBar'),
    @($remoteMakuParsed[5],  'native', '', '', '', 'HideHud'),
    @($remoteMakuParsed[6],  'nativeblock', '', '65', '', "FadeOutBlack`0"),
    @($remoteMakuParsed[13], 'native', '', '', '', 'SpawnPastConfetti'),
    @($remoteMakuParsed[14], 'wait', '', '240', '', ''),
    @($remoteMakuParsed[15], 'wait', '', '60', '', ''),
    @($remoteMakuParsed[16], 'showtextdifferentforlinked', '', '05b3', '05c3',
        "$($allTexts[0x05b3])`0$($allTexts[0x05c3])"),
    @($remoteMakuParsed[17], 'wait', '', '1', '', ''),
    @($remoteMakuParsed[18], 'native', '', '', '', 'ShowHud'),
    @($remoteMakuParsed[19], 'native', '', '', '', 'ClearFadingPalettes'),
    @($remoteMakuParsed[21], 'nativeblock', '', '65', '', "FadeInWhite`0"),
    @($remoteMakuParsed[23], 'native', '', '', '', 'ResetMusic'),
    @($remoteMakuParsed[24], 'orroomflag', '', '40', '', ''),
    @($remoteMakuParsed[25], 'native', '', '', '', 'IncMakuTreeState'),
    @($remoteMakuParsed[27], 'enableinput', '', '', '', ''),
    @($remoteMakuParsed[28], 'scriptend', '', '', '', '')
)
for ($index = 0; $index -lt $remoteMakuSecondEssenceCommandSpecs.Count; $index++) {
    $spec = $remoteMakuSecondEssenceCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $remoteMakuSecondEssenceCommandRows.Add((New-CutsceneCommandRow `
        'remoteMakuCutsceneScript' $index $sourceCommand.Label `
        $sourceCommand.Line $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_second_essence_commands.tsv'),
    $remoteMakuSecondEssenceCommandRows)
