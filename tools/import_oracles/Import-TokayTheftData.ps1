# Room $1:$aa's five INTERAC_TOKAY thieves are the direct destination of the
# raftwreck hardcoded warp. Their scripts run in parallel while subid $02
# removes Link's inventory and owns SPECIALOBJECT_LINK_CUTSCENE $08's
# linkCutscene7 state.
$tokaySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\tokay.s')
$tokayScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$tokayScriptHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$tokayLinkSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\specialObjects\linkInCutscene.s')
$tokayBank0Source = Read-ImportText (Join-Path $Disassembly 'code\bank0.s')
if ($raftObjectSource -notmatch
        '(?ms)^group1MapaaObjectData:\s*obj_Interaction \$48 \$00 \$48 \$18\s*obj_Interaction \$48 \$01 \$58 \$38\s*obj_Interaction \$48 \$02 \$58 \$28\s*obj_Interaction \$48 \$03 \$58 \$18\s*obj_Interaction \$48 \$04 \$48 \$38' -or
    $tokayScriptSource -notmatch
        '(?ms)^tokayThiefScript:\s*wait 240\s*setanimation \$00.*?^tokayThiefCommon:\s*setangle \$10\s*setspeed SPEED_200\s*applyspeed \$10\s*wait 30\s*setangle \$08\s*setspeed SPEED_080\s*applyspeed \$20.*?wait 60.*?applyspeed \$20.*?wait 60\s*scriptend' -or
    $tokayScriptSource -notmatch
        '(?ms)^tokayMainThiefScript:\s*disablemenu\s*wait 240\s*asm15 scriptHelp\.tokayMakeLinkJump\s*setanimation \$00\s*playsound SND_STRIKE\s*writememory wTmpcfc0\.genericCutscene\.cfd1, \$01\s*scriptjump tokayThiefCommon' -or
    $tokaySource -notmatch
        '(?ms)^@initSubid02:.*?SPECIALOBJECT_LINK_CUTSCENE.*?ld \(hl\),\$07.*?ld a,\$46.*?SNDCTRL_STOPMUSIC' -or
    $tokaySource -notmatch
        '(?ms)^tokayThief_countdownToStealNextItem:.*?ld \(hl\),\$0a.*?cp \$09.*?tokayIslandStolenItems.*?TREASURE_SEED_SATCHEL.*?TREASURE_EMBER_SEEDS.*?TREASURE_MYSTERY_SEEDS.*?SND_UNKNOWN5' -or
    $tokaySource -notmatch
        '(?ms)^tokayThiefSubstate0:.*?ld \(hl\),\$5a.*?^tokayThiefSubstate1:.*?swap a\s*add \$14.*?^tokayThiefSubstate2:.*?interactionAnimate3Times.*?ld \(hl\),\$06.*?SPEED_280.*?ld bc,-\$1c0.*?SND_JUMP.*?^tokayThiefSubstate3:.*?ld c,\$20.*?ld \(hl\),\$06.*?^tokayThiefSubstate4:.*?tokayThief_jump.*?^tokayThiefSubstate5:.*?objectCheckWithinScreenBoundary.*?cp \$03.*?ld \(hl\),\$3c.*?^tokayThiefSubstate6:.*?wDisabledObjects.*?wUseSimulatedInput.*?wMenuDisabled.*?set 6,\(hl\).*?setDeathRespawnPoint' -or
    $tokayLinkSource -notmatch
        '(?ms)^linkCutscene7:.*?ld \(hl\),\$f0.*?ld a,\$14.*?^@state1:.*?itemDecCounter1.*?SPECIALOBJECT_LINK.*?ld \(hl\),\$02.*?wUseSimulatedInput.*?wMenuDisabled' -or
    $tokayScriptHelperSource -notmatch
        '(?ms)^tokayMakeLinkJump:\s*ld a,\$81.*?wLinkInAir.*?w1Link\.speedZ.*?ld \(hl\),\$00.*?ld \(hl\),\$fe.*?SND_JUMP' -or
    $tokayBank0Source -notmatch
        '(?ms)^tokayIslandStolenItems:\s*\.db TREASURE_SWORD\s*\.db TREASURE_SHOVEL\s*\.db TREASURE_HARP\s*\.db TREASURE_FLIPPERS\s*\.db TREASURE_SEED_SATCHEL\s*\.db TREASURE_SHIELD\s*\.db TREASURE_BOMBS\s*\.db TREASURE_BRACELET\s*\.db TREASURE_FEATHER') {
    throw 'Room 1:aa Tokay theft cutscene source contract changed.'
}
$tokayAnimations = @(@(0, 1, 3, 5) | ForEach-Object {
    Resolve-NpcAnimation 0x48 $_
})
if (@($tokayAnimations | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -ne 0) {
    throw 'Could not resolve INTERAC_TOKAY theft animations.'
}
$tokayEventRows = @(
    "# group`troom`tinteraction-id`troom-flag`tmain-subid`tlink-wait`tsteal-first-wait`tsteal-repeat-wait`titem-wait`tfinal-wait`tdown-speed`tright-speed`tjump-speed`tjump-angle`tjump-z-fixed`tjump-gravity-fixed`tinitial-animations`tanimation0`tanimation1`tanimation3`tanimation5`tstolen-items`taccessory-subids`tsource",
    (@('1','aa','48','40','02','f0','46','0a','5a','3c',
        (Resolve-ObjectSpeed '200').ToString('x2'),
        (Resolve-ObjectSpeed '80').ToString('x2'),
        (Resolve-ObjectSpeed '280').ToString('x2'),'06','fe40','0020',
        '01,03,00,01,03',$tokayAnimations[0],$tokayAnimations[1],
        $tokayAnimations[2],$tokayAnimations[3],
        '05,15,11,2e,19,01,03,16,17','10,1b,68,31,20',
        'mainData.s:group1MapaaObjectData;tokay.s:interactionCode48;linkInCutscene.s:linkCutscene7') -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\tokay_theft_event.tsv'),
    $tokayEventRows)

$tokayAccessoryRows = [Collections.Generic.List[string]]::new()
$tokayAccessoryRows.Add('# item-index`tsubid`tsprite`ttile-base`tpalette`tanimation')
$tokayAccessorySubids = @(0x10, 0x1b, 0x68, 0x31, 0x20)
for ($index = 0; $index -lt $tokayAccessorySubids.Count; $index++) {
    $subid = $tokayAccessorySubids[$index]
    $graphic = $interactionGraphics["99:$subid"]
    # interactionInitGraphics selects the animation encoded in the low nibble
    # of this subid's interaction-data flags. Some held items (including the
    # harp and flippers) use animation $03's two-cell OAM composition.
    $animation = Resolve-NpcAnimation 0x63 $graphic.DefaultAnimation
    if ($null -eq $graphic -or [string]::IsNullOrWhiteSpace($animation) -or
        ($graphic.Gfx -ne 0 -and -not $gfxNames.ContainsKey($graphic.Gfx))) {
        throw "Could not resolve Tokay accessory `$$($subid.ToString('x2'))."
    }
    $sprite = if ($graphic.Gfx -eq 0) { 'spr_common_sprites' } else { $gfxNames[$graphic.Gfx] }
    $tokayAccessoryRows.Add("$index`t$($subid.ToString('x2'))`t$sprite`t$($graphic.TileBase)`t$($graphic.Palette)`t$animation")
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\tokay_theft_accessories.tsv'),
    $tokayAccessoryRows)

# Fairies' Woods hide-and-seek. INTERAC_FAIRY_HIDING_MINIGAME ($6c) owns
# three interaction scripts, while INTERAC_FOREST_FAIRY ($49) implements the
# exact table-driven flight used by both the introduction and each reveal.
$fairyMinigamePath = Join-Path $Disassembly `
    'object_code\ages\interactions\fairyHidingMinigame.s'
$fairyMinigameSource = Read-ImportText $fairyMinigamePath
$forestFairyPath = Join-Path $Disassembly `
    'object_code\ages\interactions\forestFairy.s'
$forestFairySource = Read-ImportText $forestFairyPath
$fairyScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$fairyScriptSource = Read-ImportText $fairyScriptPath
$forestFairyScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$forestFairyScriptSource = Read-ImportText $forestFairyScriptPath
$forestTransitionPath = Join-Path $Disassembly 'code\bank1.s'
$forestTransitionSource = Read-ImportText $forestTransitionPath
$paletteFadePath = Join-Path $Disassembly 'code\bank0.s'
$paletteFadeSource = Read-ImportText $paletteFadePath
$miscCutscenePath = Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s'
$miscCutsceneSource = Read-ImportText $miscCutscenePath
$roomSpecificPath = Join-Path $Disassembly 'code\ages\roomSpecificCode.s'
$roomSpecificSource = Read-ImportText $roomSpecificPath

if ($fairyMinigameSource -notmatch
        '(?ms)^@table:\s+\.db \$25 \$03\s+\.db \$54 \$04\s+\.db \$32 \$05\s+\.db \$00' -or
    $fairyMinigameSource -notmatch
        '(?ms)^@state1:.*?objectGetTileAtPosition.*?Interaction\.var38.*?interactionDecCounter1.*?fairyHidingMinigame_checkBeginCutscene.*?wDisableScreenTransitions' -or
    $fairyMinigameSource -notmatch
        '(?ms)^fairyHidingMinigame_subid00:.*?^@state1:\s+call fairyHidingMinigame_checkBeginCutscene\s+ret nc\s+ld a,\(wScreenTransitionDirection\)\s+ld \(w1Link\.direction\),a\s+ld a,\$01\s+ld \(wTmpcfc0\.fairyHideAndSeek\.active\),a' -or
    $fairyMinigameSource -notmatch
        '(?ms)^@warpDestination:\s+m_HardcodedWarpA ROOM_AGES_082, \$00, \$64, \$03' -or
    $fairyMinigameSource -notmatch
        '(?ms)^fairyHidingMinigame_subid02:.*?interactionRunScript.*?ld hl,wTmpcfc0\.fairyHideAndSeek\.active\s+ld b,\$10\s+call clearMemory' -or
    $forestFairySource -notmatch
        '(?ms)^forestFairy_subid00State1:.*?sub c\s+add \$04\s+cp \$09.*?sub b\s+add \$04\s+cp \$09.*?Interaction\.var3a.*?\$5a.*?INTERAC_SPARKLE, \$02.*?objectGetRelativeAngleWithTempVars\s+call objectNudgeAngleTowards.*?objectApplySpeed.*?SND_MAGIC_POWDER' -or
    $forestFairySource -notmatch
        '(?ms)^forestFairy_subid00State0:.*?Interaction\.speed\s+ld \(hl\),SPEED_200' -or
    $forestFairySource -notmatch
        '(?ms)^forestFairy_subid00State1:.*?ld e,Interaction\.subid\s+ld a,\(de\)\s+cp \$03\s+jr nc,@label_09_160\s+ld \(hl\),c\s+ld l,Interaction\.yh\s+ld \(hl\),b\s+ld l,Interaction\.state\s+inc \(hl\).*?^forestFairy_subid00State2:\s+ld a,\(wTmpcfc0\.fairyHideAndSeek\.cfd2\)\s+or a\s+jr nz,forestFairy_animate\s+ld e,Interaction\.var03\s+ld a,\(de\)\s+cp \$06\s+jr nc,@createPuffAndDelete.*?^@createPuffAndDelete:\s+call objectCreatePuff\s+jr forestFairy_deleteSelf' -or
    $miscCutsceneSource -notmatch
        '(?ms)^@spawnForestFairy:\s+call getFreeInteractionSlot\s+ret nz\s+ld \(hl\),INTERAC_FOREST_FAIRY\s+ld l,Interaction\.var03\s+ld \(hl\),b\s+jp fairyCutscene_incState' -or
    $forestFairySource -notmatch
        '(?ms)^forestFairy_discoveredPositions:\s+\.db \$48 \$38\s+\.db \$48 \$68\s+\.db \$28 \$50') {
    throw "Fairies' Woods minigame state, cutscene spawn, tile-entry puff, flight, or discovered-fairy behavior changed."
}
if ($mainObjectSource -notmatch
        '(?ms)^group0Map80ObjectData:\s+obj_Interaction \$6c \$01 \$58 \$48' -or
    $mainObjectSource -notmatch
        '(?ms)^group0Map81ObjectData:\s+obj_Interaction \$6c \$01 \$28 \$58' -or
    $mainObjectSource -notmatch
        '(?ms)^group0Map82ObjectData:\s+obj_Interaction \$6c \$00' -or
    $mainObjectSource -notmatch
        '(?ms)^group0Map91ObjectData:\s+obj_Interaction \$6c \$01 \$38 \$28' -or
    $mainObjectSource -notmatch
        '(?ms)^group0Map92ObjectData:\s+obj_Interaction \$6c \$02 \$28 \$9f') {
    throw "Fairies' Woods $6c controller placements changed."
}
if ($fairyScriptSource -notmatch
        '(?ms)^fairyHidingMinigame_subid00Script:\s+asm15 fairyHidingMinigame_spawnForestFairyIndex, \$00\s+wait 20\s+asm15 fairyHidingMinigame_spawnForestFairyIndex, \$01\s+wait 20\s+asm15 fairyHidingMinigame_spawnForestFairyIndex, \$02\s+checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$03\s+wait 20\s+showtext TX_1100\s+wait 8\s+showtext TX_1101\s+wait 8\s+showtext TX_1102\s+wait 8\s+showtext TX_1103\s+checktext\s+writememory\s+wTmpcfc0\.fairyHideAndSeek\.cfd2, \$00\s+checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$03\s+scriptend' -or
    $fairyScriptSource -notmatch
        '(?ms)^fairyHidingMinigame_subid01Script:\s+checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$01\s+wait 20\s+asm15 fairyHidingMinigame_showFairyFoundText\s+writememory\s+wTmpcfc0\.fairyHideAndSeek\.cfd2, \$00\s+checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$01\s+scriptend' -or
    $fairyScriptSource -notmatch
        '(?ms)^fairyHidingMinigame_subid02Script:\s+setcollisionradii \$20, \$01\s+makeabuttonsensitive\s+^@checkLinkLeaving:\s+checkcollidedwithlink_ignorez\s+showtext TX_110c\s+jumpiftextoptioneq \$00, @leave\s+asm15 fairyHidingMinigame_moveLinkBackLeft\s+wait 10\s+scriptjump @checkLinkLeaving\s+^@leave:\s+scriptend' -or
    $forestFairyScriptSource -notmatch
        '(?ms)^forestFairyScript_firstDiscovered:\s+makeabuttonsensitive\s+^@npcLoop:\s+checkabutton\s+showtext TX_1108\s+scriptjump @npcLoop\s+.*?^forestFairyScript_secondDiscovered:\s+makeabuttonsensitive\s+^@npcLoop:\s+checkabutton\s+showtext TX_1109\s+scriptjump @npcLoop') {
    throw "Fairies' Woods interaction scripts changed."
}
if ($forestTransitionSource -notmatch
        '(?ms)^screenTransitionForestScrambler:.*?GLOBALFLAG_FOREST_UNSCRAMBLED.*?^@forestScramblerTable:\s+\.db \$00 \$71 \$90 \$00\s+\.db \$00 \$82 \$91 \$80\s+\.db \$00 \$00 \$92 \$82\s+\.db \$72 \$82 \$80 \$00\s+\.db \$80 \$82 \$82 \$71\s+\.db \$70 \$71 \$82 \$71\s+\.db \$81 \$92 \$00 \$00\s+\.db \$72 \$91 \$00 \$92\s+\.db \$82 \$00 \$00 \$92' -or
    $miscCutsceneSource -notmatch
        '(?ms)CUTSCENE_FAIRIES_HIDE.*?ROOM_AGES_081.*?ROOM_AGES_080.*?ROOM_AGES_091.*?ROOM_AGES_082' -or
    $miscCutsceneSource -notmatch
        '(?ms)^fairyCutscene_cfd1is07:.*?TX_110a.*?^@state1:.*?\$0c.*?wTmpcbb6.*?SND_MYSTERY_SEED.*?fastFadeinFromWhite.*?^@state2:.*?wTmpcbb6.*?jr @state1.*?^@state3:.*?wTmpcbb6.*?\(\$cfd0\).*?SND_MYSTERY_SEED.*?\$08.*?fadeinFromWhiteWithDelay.*?TX_110b.*?GLOBALFLAG_WON_FAIRY_HIDING_GAME.*?GLOBALFLAG_FOREST_UNSCRAMBLED' -or
    $roomSpecificSource -notmatch
        '(?ms)^roomSpecificCodeGroup0Table:\s+\.db \$93 \$00.*?^roomSpecificCode0:\s+ld a,GLOBALFLAG_WON_FAIRY_HIDING_GAME.*?ld hl,\$cfd0\s+ld b,\$10\s+jp clearMemory') {
    throw 'Fairies'' Woods forest transition, hiding cutscene, completion, or room $0:$93 reset changed.'
}

$normalFadeOutMatch = [regex]::Match(
    $paletteFadeSource,
    '(?ms)^fadeoutToWhite:\s+ld a,\$01\s+ld \(wPaletteThread_mode\),a\s+ld a,\$(?<speed>[0-9a-f]{2})\s+^\+\+\s+ld \(wPaletteThread_speed\),a\s+xor a\s+ld \(wPaletteThread_fadeOffset\),a')
$normalFadeInMatch = [regex]::Match(
    $paletteFadeSource,
    '(?ms)^fadeinFromWhite:\s+ld a,\$02\s+ld \(wPaletteThread_mode\),a\s+ld a,\$(?<speed>[0-9a-f]{2})\s+^\+\+\s+ld \(wPaletteThread_speed\),a\s+ld a,\$20\s+ld \(wPaletteThread_fadeOffset\),a')
$fastFadeInMatch = [regex]::Match(
    $paletteFadeSource,
    '(?ms)^fastFadeinFromWhite:\s+ld a,\$02\s+ld \(wPaletteThread_mode\),a\s+ld a,\$(?<speed>[0-9a-f]{2})\s+jr \+\+')
$delayedFadeMatch = [regex]::Match(
    $miscCutsceneSource,
    '(?ms)^fairyCutscene_cfd1is07:.*?^@state3:.*?ld a,\$(?<refill>[0-9a-f]{2})\s+jp fadeinFromWhiteWithDelay')
if (-not $normalFadeOutMatch.Success -or
    -not $normalFadeInMatch.Success -or
    -not $fastFadeInMatch.Success -or
    -not $delayedFadeMatch.Success -or
    $paletteFadeSource -notmatch
        '(?ms)^setPaletteThreadDelay:\s+ld \(wPaletteThread_counterRefill\),a\s+ld a,\$01\s+ld \(wPaletteThread_counter\),a' -or
    $forestTransitionSource -notmatch
        '(?ms)^paletteFadeHandler01:.*?wPaletteThread_speed.*?wPaletteThread_fadeOffset.*?add c\s+cp \$20\s+jp nc,paletteThread_stop' -or
    $forestTransitionSource -notmatch
        '(?ms)^paletteFadeHandler02:.*?wPaletteThread_speed.*?wPaletteThread_fadeOffset.*?sub c\s+jr c,paletteThread_stop' -or
    $forestTransitionSource -notmatch
        '(?ms)^paletteThread_decCounter:\s+ld hl,wPaletteThread_counter\s+dec \(hl\)\s+ret nz\s+ld a,\(wPaletteThread_counterRefill\)\s+ld \(wPaletteThread_counter\),a') {
    throw "Fairies' Woods palette fade initialization or update cadence changed."
}
$normalFadeOutSpeed = [Convert]::ToInt32(
    $normalFadeOutMatch.Groups['speed'].Value, 16)
$normalFadeInSpeed = [Convert]::ToInt32(
    $normalFadeInMatch.Groups['speed'].Value, 16)
$fastFadeInSpeed = [Convert]::ToInt32(
    $fastFadeInMatch.Groups['speed'].Value, 16)
$delayedFadeRefill = [Convert]::ToInt32(
    $delayedFadeMatch.Groups['refill'].Value, 16)
if ($normalFadeOutSpeed -ne $normalFadeInSpeed) {
    throw "Fairies' Woods normal white fade speeds no longer match."
}
$normalFadeOutDuration = [int][Math]::Ceiling(
    32.0 / $normalFadeOutSpeed)
$normalFadeInDuration =
    [int][Math]::Floor(32.0 / $normalFadeInSpeed) + 1
$fastFadeInDuration =
    [int][Math]::Floor(32.0 / $fastFadeInSpeed) + 1
$delayedFadeInDuration =
    1 + (($normalFadeInDuration - 1) * $delayedFadeRefill)
if (($normalFadeOutSpeed, $normalFadeInSpeed, $fastFadeInSpeed,
     $delayedFadeRefill, $normalFadeOutDuration, $normalFadeInDuration,
     $fastFadeInDuration, $delayedFadeInDuration) -join ',' -ne
    '1,1,3,8,32,33,11,257') {
    throw "Fairies' Woods white fade constants diverged from the supported ROM."
}

$fairyMovementMatch = [regex]::Match(
    $forestFairySource,
    '(?ms)^@data:\s*(?<rows>(?:\s*\.db \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2}){22})')
if (-not $fairyMovementMatch.Success) {
    throw 'Could not locate the 22-row forestFairy movement table.'
}
$fairyMovementRows = @([regex]::Matches(
    $fairyMovementMatch.Groups['rows'].Value,
    '\.db \$(?<b0>[0-9a-f]{2}) \$(?<b1>[0-9a-f]{2}) \$(?<b2>[0-9a-f]{2}) \$(?<b3>[0-9a-f]{2})'))
if ($fairyMovementRows.Count -ne 22) {
    throw "Expected 22 forestFairy movement rows, got $($fairyMovementRows.Count)."
}

$forestFairyGraphic = $interactionGraphics['73:0']
$forestSparkleGraphic = $interactionGraphics['132:2']
if ($null -eq $forestFairyGraphic -or
    $forestFairyGraphic.Gfx -ne 0x4c -or
    $forestFairyGraphic.TileBase -ne 0x16 -or
    $null -eq $forestSparkleGraphic -or
    $forestSparkleGraphic.Gfx -ne 0x6b -or
    $forestSparkleGraphic.TileBase -ne 0x0a -or
    $forestSparkleGraphic.Palette -ne 0 -or
    $forestSparkleGraphic.DefaultAnimation -ne 1) {
    throw 'Forest fairy or $84:$02 sparkle graphics changed.'
}
$forestFairyAnimation0 = Resolve-NpcAnimation 0x49 0
$forestFairyAnimation1 = Resolve-NpcAnimation 0x49 1
$forestSparkleAnimation = Resolve-NpcAnimation 0x84 1
if (-not $forestFairyAnimation0 -or -not $forestFairyAnimation1 -or
    -not $forestSparkleAnimation) {
    throw 'Could not resolve forest fairy or sparkle animations.'
}

$fairyTextRows = [Collections.Generic.List[string]]::new()
$fairyTextRows.Add("# text-id`ttext-base64")
foreach ($textId in 0x1100..0x110c) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Fairies' Woods text TX_$($textId.ToString('x4'))."
    }
    $fairyTextRows.Add(
        "$($textId.ToString('x4'))`t$(ConvertTo-CutsceneCommandPayload $allTexts[$textId])")
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_text.tsv'),
    $fairyTextRows)

$introBody = [regex]::Match(
    $fairyScriptSource,
    '(?ms)^fairyHidingMinigame_subid00Script:(?<body>.*?)(?=^; Hiding spot for fairy revealed)')
if (-not $introBody.Success) { throw 'Could not locate fairy intro script body.' }
function Add-FairyCommand(
    [Collections.Generic.List[string]]$rows,
    [string]$script,
    [int]$index,
    [string]$label,
    [string]$pattern,
    [string]$opcode,
    [string]$actor,
    [string]$arg0,
    [string]$arg1,
    [string]$payload,
    [int]$occurrence = 0) {
    $line = Find-CutsceneCommandSourceLine `
        $fairyScriptSource `
        $introBody.Groups['body'].Index `
        ($introBody.Groups['body'].Index + $introBody.Groups['body'].Length) `
        $pattern $script $occurrence
    $rows.Add((New-CutsceneCommandRow `
        $script $index $label $line $opcode $actor $arg0 $arg1 $payload))
}

$fairyIntroRows = [Collections.Generic.List[string]]::new()
$fairyIntroRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 0 `
    'fairyHidingMinigame_subid00Script' `
    '^\s*asm15 fairyHidingMinigame_spawnForestFairyIndex, \$00\s*$' `
    'nativeyield' '' '' '' 'SpawnForestFairy:0'
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 1 `
    'fairyHidingMinigame_subid00Script' '^\s*wait 20\s*$' `
    'wait' '' '20' '' ''
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 2 `
    'fairyHidingMinigame_subid00Script' `
    '^\s*asm15 fairyHidingMinigame_spawnForestFairyIndex, \$01\s*$' `
    'nativeyield' '' '' '' 'SpawnForestFairy:1'
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 3 `
    'fairyHidingMinigame_subid00Script' '^\s*wait 20\s*$' `
    'wait' '' '20' '' '' 1
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 4 `
    'fairyHidingMinigame_subid00Script' `
    '^\s*asm15 fairyHidingMinigame_spawnForestFairyIndex, \$02\s*$' `
    'nativeyield' '' '' '' 'SpawnForestFairy:2'
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 5 `
    'fairyHidingMinigame_subid00Script' `
    '^\s*checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$03\s*$' `
    'checkmemoryeq' '' '03' '' 'FairySignal'
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 6 `
    'fairyHidingMinigame_subid00Script' '^\s*wait 20\s*$' `
    'wait' '' '20' '' '' 2
$introIndex = 7
$introWait8Occurrence = 0
foreach ($textId in 0x1100..0x1103) {
    Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' `
        $introIndex 'fairyHidingMinigame_subid00Script' `
        "^\s*showtext TX_$($textId.ToString('x4'))\s*$" `
        'showtext' '' $textId.ToString('x4') '' $allTexts[$textId]
    $introIndex++
    if ($textId -ne 0x1103) {
        Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' `
            $introIndex 'fairyHidingMinigame_subid00Script' `
            '^\s*wait 8\s*$' 'wait' '' '8' '' '' $introWait8Occurrence
        $introWait8Occurrence++
        $introIndex++
    }
}
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 14 `
    'fairyHidingMinigame_subid00Script' `
    '^\s*writememory\s+wTmpcfc0\.fairyHideAndSeek\.cfd2, \$00\s*$' `
    'writememory' '' '00' '' 'FairySignal'
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 15 `
    'fairyHidingMinigame_subid00Script' `
    '^\s*checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$03\s*$' `
    'checkmemoryeq' '' '03' '' 'FairySignal' 1
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 16 `
    'fairyHidingMinigame_subid00Script' '^\s*scriptend\s*$' `
    'scriptend' '' '' '' ''
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_intro_commands.tsv'),
    $fairyIntroRows)

$fairyRevealRows = [Collections.Generic.List[string]]::new()
$fairyRevealRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
$revealBody = [regex]::Match(
    $fairyScriptSource,
    '(?ms)^fairyHidingMinigame_subid01Script:(?<body>.*?)(?=^; Checks for Link leaving)')
if (-not $revealBody.Success) { throw 'Could not locate fairy reveal script body.' }
$revealPatterns = @(
    @('checkmemoryeq', '', '01', '', 'FairySignal',
        '^\s*checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$01\s*$'),
    @('wait', '', '20', '', '', '^\s*wait 20\s*$'),
    @('nativeyield', '', '', '', 'ShowFairyFoundText',
        '^\s*asm15 fairyHidingMinigame_showFairyFoundText\s*$'),
    @('writememory', '', '00', '', 'FairySignal',
        '^\s*writememory\s+wTmpcfc0\.fairyHideAndSeek\.cfd2, \$00\s*$'),
    @('checkmemoryeq', '', '01', '', 'FairySignal',
        '^\s*checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$01\s*$'),
    @('scriptend', '', '', '', '', '^\s*scriptend\s*$')
)
for ($index = 0; $index -lt $revealPatterns.Count; $index++) {
    $entry = $revealPatterns[$index]
    $occurrence = if ($index -eq 4) { 1 } else { 0 }
    $line = Find-CutsceneCommandSourceLine `
        $fairyScriptSource $revealBody.Groups['body'].Index `
        ($revealBody.Groups['body'].Index + $revealBody.Groups['body'].Length) `
        $entry[5] 'fairyHidingMinigame_subid01Script' $occurrence
    $fairyRevealRows.Add((New-CutsceneCommandRow `
        'fairyHidingMinigame_subid01Script' $index `
        'fairyHidingMinigame_subid01Script' $line `
        $entry[0] $entry[1] $entry[2] $entry[3] $entry[4]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_reveal_commands.tsv'),
    $fairyRevealRows)

$fairyExitRows = [Collections.Generic.List[string]]::new()
$fairyExitRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
$exitBody = [regex]::Match(
    $fairyScriptSource,
    '(?ms)^fairyHidingMinigame_subid02Script:(?<body>.*?)(?=^; =+)')
if (-not $exitBody.Success) { throw 'Could not locate fairy exit script body.' }
$exitCommands = @(
    @('fairyHidingMinigame_subid02Script', 'setcollisionradii', 'FairyExit', '20', '01', '',
        '^\s*setcollisionradii \$20, \$01\s*$'),
    @('fairyHidingMinigame_subid02Script', 'makeabuttonsensitive', 'FairyExit', '', '', '',
        '^\s*makeabuttonsensitive\s*$'),
    @('@checkLinkLeaving', 'nativeblock', 'FairyExit', '1', '', 'WaitForExitCollision',
        '^\s*checkcollidedwithlink_ignorez\s*$'),
    @('@checkLinkLeaving', 'showtext', '', '110c', '', $allTexts[0x110c],
        '^\s*showtext TX_110c\s*$'),
    @('@checkLinkLeaving', 'jumpiftextoptioneq', '', '00', '8', '',
        '^\s*jumpiftextoptioneq \$00, @leave\s*$'),
    @('@checkLinkLeaving', 'nativeyield', '', '', '', 'MoveLinkBackLeft',
        '^\s*asm15 fairyHidingMinigame_moveLinkBackLeft\s*$'),
    @('@checkLinkLeaving', 'wait', '', '10', '', '', '^\s*wait 10\s*$'),
    @('@checkLinkLeaving', 'scriptjump', '', '2', '', '',
        '^\s*scriptjump @checkLinkLeaving\s*$'),
    @('@leave', 'scriptend', '', '', '', '', '^\s*scriptend\s*$')
)
for ($index = 0; $index -lt $exitCommands.Count; $index++) {
    $entry = $exitCommands[$index]
    $line = Find-CutsceneCommandSourceLine `
        $fairyScriptSource $exitBody.Groups['body'].Index `
        ($exitBody.Groups['body'].Index + $exitBody.Groups['body'].Length) `
        $entry[6] 'fairyHidingMinigame_subid02Script'
    $fairyExitRows.Add((New-CutsceneCommandRow `
        'fairyHidingMinigame_subid02Script' $index $entry[0] $line `
        $entry[1] $entry[2] $entry[3] $entry[4] $entry[5]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_exit_commands.tsv'),
    $fairyExitRows)

$fairyEventRows = @(
    "# group`tstart-room`texit-room`treset-room`tessence-treasure`tactive-address`tfound-address`tsignal-address`tcompletion-flag`tunscrambled-flag`thidden-delay`texit-y`texit-x`texit-radius-y`texit-radius-x`tmagic-sound`tpuff-sound`tmystery-sound`tnormal-fade-out`tnormal-fade-in`tfast-fade-in`tcompletion-hold`tdelayed-fade-in`tnormal-fade-speed`tfast-fade-speed`tdelayed-fade-refill`tforest-fairy-sprite`tfairy-tile-base`tanimation0`tanimation1`tsparkle-sprite`tsparkle-tile-base`tsparkle-palette`tsparkle-animation"
    (@(
        '0', '82', '92', '93', '40', 'cfd0', 'cfd1', 'cfd2',
        '0e', '2b', '12', '28', '9f', '20', '01',
        '83', '98', '7b',
        $normalFadeOutDuration, $normalFadeInDuration, $fastFadeInDuration,
        '12', $delayedFadeInDuration,
        $normalFadeInSpeed, $fastFadeInSpeed, $delayedFadeRefill,
        $gfxNames[$forestFairyGraphic.Gfx], $forestFairyGraphic.TileBase,
        $forestFairyAnimation0, $forestFairyAnimation1,
        $gfxNames[$forestSparkleGraphic.Gfx], $forestSparkleGraphic.TileBase,
        $forestSparkleGraphic.Palette, $forestSparkleAnimation
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_event.tsv'),
    $fairyEventRows)

$fairyMovementOutput = [Collections.Generic.List[string]]::new()
$fairyMovementOutput.Add(
    "# index`tinitial-y`tinitial-x`tangle`tcounter`ttarget-y`ttarget-x`tdirection`tpalette`tsource")
for ($index = 0; $index -lt $fairyMovementRows.Count; $index++) {
    $row = $fairyMovementRows[$index]
    $b0 = [Convert]::ToInt32($row.Groups['b0'].Value, 16)
    $b1 = [Convert]::ToInt32($row.Groups['b1'].Value, 16)
    $b2 = [Convert]::ToInt32($row.Groups['b2'].Value, 16)
    $b3 = [Convert]::ToInt32($row.Groups['b3'].Value, 16)
    $fairyMovementOutput.Add(@(
        $index,
        (($b0 -band 0xf8).ToString('x2')),
        (($b1 -band 0xf8).ToString('x2')),
        ((($b0 -band 7) * 4).ToString('x2')),
        (($b1 -band 7) + 1),
        (($b2 -band 0xf8).ToString('x2')),
        (($b3 -band 0xf8).ToString('x2')),
        ($b2 -band 1),
        ($b3 -band 7),
        "forestFairy.s:@data+$($index.ToString('x2'))"
    ) -join "`t")
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_movement.tsv'),
    $fairyMovementOutput)

$retiredFairyVelocityPath =
    Join-Path $destination 'cutscenes\fairies_woods_velocity.tsv'
if (Test-Path -LiteralPath $retiredFairyVelocityPath) {
    Remove-Item -LiteralPath $retiredFairyVelocityPath -Force
}

$fairyHiddenRows = @(
    "# room`tpacked-position`tfairy-index`tsource"
    "81`t25`t03`tfairyHidingMinigame.s:@table"
    "80`t54`t04`tfairyHidingMinigame.s:@table"
    "91`t32`t05`tfairyHidingMinigame.s:@table"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_hidden_spots.tsv'),
    $fairyHiddenRows)

$fairyHidingRoomRows = @(
    "# index`troom`tpreset`tsource"
    "0`t81`t0c`tmiscCutscenes.s:CUTSCENE_FAIRIES_HIDE"
    "1`t80`t0d`tmiscCutscenes.s:CUTSCENE_FAIRIES_HIDE"
    "2`t91`t0e`tmiscCutscenes.s:CUTSCENE_FAIRIES_HIDE"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_hiding_rooms.tsv'),
    $fairyHidingRoomRows)

$fairyDiscoveredRows = @(
    "# index`ty`tx`tpalette`tanimation`tsource"
    "0`t48`t38`t1`t$forestFairyAnimation0`tforestFairy.s:forestFairy_discoveredPositions"
    "1`t48`t68`t2`t$forestFairyAnimation1`tforestFairy.s:forestFairy_discoveredPositions"
    "2`t28`t50`t3`t$forestFairyAnimation1`tforestFairy.s:forestFairy_discoveredPositions"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_discovered.tsv'),
    $fairyDiscoveredRows)

$scramblerRooms = @(0x70, 0x71, 0x72, 0x80, 0x81, 0x82, 0x90, 0x91, 0x92)
$scramblerValues = @(
    @(0x00, 0x71, 0x90, 0x00),
    @(0x00, 0x82, 0x91, 0x80),
    @(0x00, 0x00, 0x92, 0x82),
    @(0x72, 0x82, 0x80, 0x00),
    @(0x80, 0x82, 0x82, 0x71),
    @(0x70, 0x71, 0x82, 0x71),
    @(0x81, 0x92, 0x00, 0x00),
    @(0x72, 0x91, 0x00, 0x92),
    @(0x82, 0x00, 0x00, 0x92)
)
$fairyScramblerRows = [Collections.Generic.List[string]]::new()
$fairyScramblerRows.Add("# room`tup`tright`tdown`tleft`tsource")
for ($index = 0; $index -lt $scramblerRooms.Count; $index++) {
    $values = $scramblerValues[$index]
    $fairyScramblerRows.Add(
        "$($scramblerRooms[$index].ToString('x2'))`t" +
        (($values | ForEach-Object { $_.ToString('x2') }) -join "`t") +
        "`tbank1.s:@forestScramblerTable")
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_scrambler.tsv'),
    $fairyScramblerRows)
