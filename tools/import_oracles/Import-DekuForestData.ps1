# Past room 1:81 runs roomSpecificCode3 after the Mystery Seeds are obtained.
# Until room flag $40 is set it allocates INTERAC_SOLDIER $40:$0a, whose
# interaction script brings the red soldier in from X=$f0, reacts to Link,
# speaks TX_590b, and installs the hardcoded warp to room 1:46.
$dekuSoldierRoomSpecificPath =
    Join-Path $Disassembly 'code\ages\roomSpecificCode.s'
$dekuSoldierRoomSpecificSource =
    Read-ImportText $dekuSoldierRoomSpecificPath
$dekuSoldierSourcePath =
    Join-Path $Disassembly 'object_code\ages\interactions\soldier.s'
$dekuSoldierSource = Read-ImportText $dekuSoldierSourcePath
$dekuSoldierScriptPath =
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$dekuSoldierScriptSource = Read-ImportText $dekuSoldierScriptPath

if ($dekuSoldierRoomSpecificSource -notmatch
        '(?ms)^roomSpecificCodeGroup1Table:\s+\.db \$81 \$03.*?^roomSpecificCodeGroup2Table:' -or
    $dekuSoldierRoomSpecificSource -notmatch
        '(?ms)^roomSpecificCode3:\s+call getThisRoomFlags\s+bit 6,a\s+ret nz\s+ld a,TREASURE_MYSTERY_SEEDS\s+call checkTreasureObtained\s+ret nc\s+ld hl,wcc05\s+res 1,\(hl\)\s+call getFreeInteractionSlot\s+ret nz\s+ld \(hl\),\$40\s+inc l\s+ld \(hl\),\$0a\s+ld a,\$01\s+ld \(wDiggingUpEnemiesForbidden\),a\s+ret') {
    throw 'Room 1:81 no longer allocates soldier $40:$0a from roomSpecificCode3 after the Mystery Seeds.'
}

$dekuSoldierInteractionMatch = [regex]::Match(
    $dekuSoldierSource,
    '(?ms)^soldierSubid0a:(?<body>.*?)(?=^soldierSubid0b:)')
if (-not $dekuSoldierInteractionMatch.Success) {
    throw 'Could not locate soldier $40:$0a.'
}
$dekuSoldierInteraction = $dekuSoldierInteractionMatch.Groups['body'].Value
$dekuSoldierStateMatch = [regex]::Match(
    $dekuSoldierInteraction,
    '(?ms)call soldierInitGraphicsAndLoadScript\s+ld l,Interaction\.oamFlags\s+ld \(hl\),\$02\s+ld bc,\$(?<position>[0-9a-f]{4})\s+jp interactionSetPosition.*?call soldierUpdateAnimationAndRunScript\s+ret nc\s+ld hl,wcc05\s+set 1,\(hl\)\s+ld hl,@warpDest\s+jp setWarpDestVariables\s+^@warpDest:\s+m_HardcodedWarpA ROOM_AGES_(?<destination>[0-9a-f]{3}), \$(?<sourceTransition>[0-9a-f]{2}), \$(?<destinationPosition>[0-9a-f]{2}), \$(?<destinationTransition>[0-9a-f]{2})')
if (-not $dekuSoldierStateMatch.Success) {
    throw 'Could not parse the soldier $40:$0a initialization and hardcoded warp.'
}
$dekuSoldierPosition = $dekuSoldierStateMatch.Groups['position'].Value
if ($dekuSoldierPosition -ne '68f0' -or
    $dekuSoldierStateMatch.Groups['destination'].Value -ne '146' -or
    $dekuSoldierStateMatch.Groups['sourceTransition'].Value -ne '00' -or
    $dekuSoldierStateMatch.Groups['destinationPosition'].Value -ne '34' -or
    $dekuSoldierStateMatch.Groups['destinationTransition'].Value -ne '03') {
    throw 'Soldier $40:$0a moved from $68/$f0 or changed its room 1:46 hardcoded warp.'
}

$dekuSoldierScriptMatch = [regex]::Match(
    $dekuSoldierScriptSource,
    '(?ms)^soldierSubid0aScript:(?<body>.*?)(?=^; =+\s*^; INTERAC_TOKAY)')
if (-not $dekuSoldierScriptMatch.Success) {
    throw 'Could not parse soldierSubid0aScript.'
}
$dekuSoldierBody = $dekuSoldierScriptMatch.Groups['body'].Value
$dekuSoldierSequenceMatch = [regex]::Match(
    $dekuSoldierBody,
    '(?ms)^\s*checkmemoryeq w1Link\.yh, \$(?<triggerY>[0-9a-f]{2})\s+asm15 objectSetVisible82\s+asm15 dropLinkHeldItem\s+writememory wDisabledObjects, \$(?<disabledObjects>[0-9a-f]{2})\s+disablemenu\s+wait (?<introWait>\d+)\s+setspeed SPEED_0c0\s+moveright \$(?<rightCounter>[0-9a-f]{2})\s+wait (?<rightWait>\d+)\s+setanimation \$(?<upAnimation>[0-9a-f]{2})\s+wait (?<reactionWait>\d+)\s+asm15 createExclamationMark, \$(?<effectFrames>[0-9a-f]{2})\s+wait (?<effectWait>\d+)\s+setspeed SPEED_180\s+moveup \$(?<upCounter>[0-9a-f]{2})\s+wait (?<textWait>\d+)\s+showtext TX_(?<text>[0-9a-f]{4})\s+wait (?<postTextWait>\d+)\s+orroomflag \$(?<roomFlag>[0-9a-f]{2})\s+scriptend\s*$')
if (-not $dekuSoldierSequenceMatch.Success) {
    throw 'soldierSubid0aScript no longer matches its expected entrance, reaction, dialogue, and completion sequence.'
}
$dekuSoldierScript = $dekuSoldierSequenceMatch.Groups
if ($dekuSoldierScript['triggerY'].Value -ne '2a' -or
    $dekuSoldierScript['disabledObjects'].Value -ne '01' -or
    $dekuSoldierScript['introWait'].Value -ne '30' -or
    $dekuSoldierScript['rightCounter'].Value -ne '4b' -or
    $dekuSoldierScript['rightWait'].Value -ne '6' -or
    $dekuSoldierScript['upAnimation'].Value -ne '00' -or
    $dekuSoldierScript['reactionWait'].Value -ne '20' -or
    $dekuSoldierScript['effectFrames'].Value -ne '28' -or
    $dekuSoldierScript['effectWait'].Value -ne '60' -or
    $dekuSoldierScript['upCounter'].Value -ne '1e' -or
    $dekuSoldierScript['textWait'].Value -ne '30' -or
    $dekuSoldierScript['postTextWait'].Value -ne '30' -or
    $dekuSoldierScript['roomFlag'].Value -ne '40') {
    throw 'soldierSubid0aScript timing or predicate operands changed.'
}

$dekuTreasureSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\treasure.s')
$dekuMysterySeedsMatch = [regex]::Match(
    $dekuTreasureSource,
    '(?m)^\s*TREASURE_MYSTERY_SEEDS\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $dekuMysterySeedsMatch.Success -or
    $dekuMysterySeedsMatch.Groups['value'].Value -ne '24') {
    throw 'TREASURE_MYSTERY_SEEDS no longer resolves to $24.'
}
$dekuSlowSpeedMatch = [regex]::Match(
    $speedSource,
    '(?m)^\s*SPEED_c0\s+dsb\s+(?<count>\d+)\s*;\s*0x(?<value>[0-9a-f]{2})')
$dekuFastSpeedMatch = [regex]::Match(
    $speedSource,
    '(?m)^\s*SPEED_180\s+dsb\s+(?<count>\d+)\s*;\s*0x(?<value>[0-9a-f]{2})')
if (-not $dekuSlowSpeedMatch.Success -or
    $dekuSlowSpeedMatch.Groups['value'].Value -ne '1e' -or
    $speedSource -notmatch '(?m)^\.define SPEED_0c0\s+SPEED_c0\s*$' -or
    -not $dekuFastSpeedMatch.Success -or
    $dekuFastSpeedMatch.Groups['value'].Value -ne '3c') {
    throw 'Could not resolve soldierSubid0aScript SPEED_0c0/SPEED_180 as $1e/$3c.'
}

$dekuSoldierGraphic = $interactionGraphics['64:0']
$dekuSoldierAnimations =
    @(0..3 | ForEach-Object { Resolve-NpcAnimation 0x40 $_ })
$dekuExclamationGraphic = $interactionGraphics['159:0']
$dekuExclamationAnimation = Resolve-NpcAnimation 0x9f 0
if ($null -eq $dekuSoldierGraphic -or
    -not $gfxNames.ContainsKey($dekuSoldierGraphic.Gfx) -or
    $gfxNames[$dekuSoldierGraphic.Gfx] -ne 'spr_soldier' -or
    $dekuSoldierGraphic.TileBase -ne 0 -or
    $dekuSoldierGraphic.DefaultAnimation -ne 2 -or
    $dekuSoldierAnimations.Count -ne 4 -or
    $dekuSoldierAnimations.Where({ [string]::IsNullOrEmpty($_) }).Count -ne 0 -or
    $null -eq $dekuExclamationGraphic -or
    -not $gfxNames.ContainsKey($dekuExclamationGraphic.Gfx) -or
    -not $dekuExclamationAnimation) {
    throw 'Could not resolve the room 1:81 red soldier and exclamation graphics.'
}

$dekuSoldierTextId =
    [Convert]::ToInt32($dekuSoldierScript['text'].Value, 16)
if ($dekuSoldierTextId -ne 0x590b -or
    -not $allTexts.ContainsKey($dekuSoldierTextId) -or
    $allTextPositions.ContainsKey($dekuSoldierTextId)) {
    throw 'Expected room 1:81 soldier dialogue TX_590b without a fixed textbox position.'
}

$dekuSoldierEventRows = @(
    "# group`troom`tid`tsubid`ttrigger-treasure`troom-flag`ttrigger-y`tinitial-y`tinitial-x`tpalette`tinitial-animation`tsprite`ttile-base`tanimation-0`tanimation-1`tanimation-2`tanimation-3`tslow-speed`tfast-speed`teffect-id`teffect-subid`teffect-sprite`teffect-tile-base`teffect-palette`teffect-animation`teffect-y-offset`teffect-x-offset`teffect-frames`tclink-sound`tdestination-group`tdestination-room`tdestination-position`tdestination-parameter`tsource-transition`tdestination-transition`ttext-id`ttext-base64`tsource",
    (@(
        '1', '81', '40', '0a',
        $dekuMysterySeedsMatch.Groups['value'].Value,
        $dekuSoldierScript['roomFlag'].Value,
        $dekuSoldierScript['triggerY'].Value,
        $dekuSoldierPosition.Substring(0, 2),
        $dekuSoldierPosition.Substring(2, 2),
        '2',
        $dekuSoldierGraphic.DefaultAnimation.ToString(),
        $gfxNames[$dekuSoldierGraphic.Gfx],
        $dekuSoldierGraphic.TileBase.ToString(),
        $dekuSoldierAnimations[0],
        $dekuSoldierAnimations[1],
        $dekuSoldierAnimations[2],
        $dekuSoldierAnimations[3],
        $dekuSlowSpeedMatch.Groups['value'].Value,
        $dekuFastSpeedMatch.Groups['value'].Value,
        '9f',
        '00',
        $gfxNames[$dekuExclamationGraphic.Gfx],
        $dekuExclamationGraphic.TileBase.ToString(),
        $dekuExclamationGraphic.Palette.ToString(),
        $dekuExclamationAnimation,
        '-13',
        '0',
        [Convert]::ToInt32(
            $dekuSoldierScript['effectFrames'].Value, 16).ToString(),
        '50',
        '1',
        $dekuSoldierStateMatch.Groups['destination'].Value.Substring(1, 2),
        $dekuSoldierStateMatch.Groups['destinationPosition'].Value,
        '00',
        $dekuSoldierStateMatch.Groups['sourceTransition'].Value,
        $dekuSoldierStateMatch.Groups['destinationTransition'].Value,
        $dekuSoldierTextId.ToString('x4'),
        [Convert]::ToBase64String(
            [Text.Encoding]::UTF8.GetBytes($allTexts[$dekuSoldierTextId])),
        'roomSpecificCode.s:roomSpecificCode3;soldier.s:soldierSubid0a;scriptHelper.s:soldierSubid0aScript'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\deku_forest_soldier_event.tsv'),
    $dekuSoldierEventRows)

$dekuSoldierBodyStart = $dekuSoldierScriptMatch.Groups['body'].Index
$dekuSoldierBodyEnd =
    $dekuSoldierBodyStart + $dekuSoldierScriptMatch.Groups['body'].Length
$findDekuSoldierSourceLine = {
    param([string]$pattern, [int]$occurrence = 0)
    return Find-CutsceneCommandSourceLine `
        $dekuSoldierScriptSource $dekuSoldierBodyStart $dekuSoldierBodyEnd `
        $pattern 'soldierSubid0aScript' $occurrence
}
$newDekuSoldierCommandRow = {
    param(
        [int]$index,
        [int]$line,
        [string]$opcode,
        [string]$actor,
        [string]$arg0,
        [string]$arg1,
        [string]$payload)
    return New-CutsceneCommandRow `
        'soldierSubid0aScript' $index 'soldierSubid0aScript' $line `
        $opcode $actor $arg0 $arg1 $payload
}
$dekuSoldierCommandRows = @(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64",
    (& $newDekuSoldierCommandRow 0 (& $findDekuSoldierSourceLine '^\s*checkmemoryeq\s+w1Link\.yh,\s*\$2a\s*$') 'checkmemoryeq' '' '2a' '' 'PlayerY'),
    (& $newDekuSoldierCommandRow 1 (& $findDekuSoldierSourceLine '^\s*asm15\s+objectSetVisible82\s*$') 'native' '' '' '' 'ObjectSetVisible82'),
    (& $newDekuSoldierCommandRow 2 (& $findDekuSoldierSourceLine '^\s*asm15\s+dropLinkHeldItem\s*$') 'native' '' '' '' 'DropLinkHeldItem'),
    (& $newDekuSoldierCommandRow 3 (& $findDekuSoldierSourceLine '^\s*writememory\s+wDisabledObjects,\s*\$01\s*$') 'writememory' '' '01' '' 'DisabledObjects'),
    (& $newDekuSoldierCommandRow 4 (& $findDekuSoldierSourceLine '^\s*disablemenu\s*$') 'disablemenu' '' '' '' ''),
    (& $newDekuSoldierCommandRow 5 (& $findDekuSoldierSourceLine '^\s*wait\s+30\s*$' 0) 'wait' '' '30' '' ''),
    (& $newDekuSoldierCommandRow 6 (& $findDekuSoldierSourceLine '^\s*setspeed\s+SPEED_0c0\s*$') 'setspeed' 'Soldier' $dekuSlowSpeedMatch.Groups['value'].Value '' ''),
    (& $newDekuSoldierCommandRow 7 (& $findDekuSoldierSourceLine '^\s*moveright\s+\$4b\s*$') 'move' 'Soldier' '08' '4b' $dekuSoldierAnimations[1]),
    (& $newDekuSoldierCommandRow 8 (& $findDekuSoldierSourceLine '^\s*wait\s+6\s*$') 'wait' '' '6' '' ''),
    (& $newDekuSoldierCommandRow 9 (& $findDekuSoldierSourceLine '^\s*setanimation\s+\$00\s*$') 'setanimation' 'Soldier' '00' '' $dekuSoldierAnimations[0]),
    (& $newDekuSoldierCommandRow 10 (& $findDekuSoldierSourceLine '^\s*wait\s+20\s*$') 'wait' '' '20' '' ''),
    (& $newDekuSoldierCommandRow 11 (& $findDekuSoldierSourceLine '^\s*asm15\s+createExclamationMark,\s*\$28\s*$') 'native' '' '' '' 'CreateExclamationMark'),
    (& $newDekuSoldierCommandRow 12 (& $findDekuSoldierSourceLine '^\s*wait\s+60\s*$') 'wait' '' '60' '' ''),
    (& $newDekuSoldierCommandRow 13 (& $findDekuSoldierSourceLine '^\s*setspeed\s+SPEED_180\s*$') 'setspeed' 'Soldier' $dekuFastSpeedMatch.Groups['value'].Value '' ''),
    (& $newDekuSoldierCommandRow 14 (& $findDekuSoldierSourceLine '^\s*moveup\s+\$1e\s*$') 'move' 'Soldier' '00' '1e' $dekuSoldierAnimations[0]),
    (& $newDekuSoldierCommandRow 15 (& $findDekuSoldierSourceLine '^\s*wait\s+30\s*$' 1) 'wait' '' '30' '' ''),
    (& $newDekuSoldierCommandRow 16 (& $findDekuSoldierSourceLine '^\s*showtext\s+TX_590b\s*$') 'showtext' '' '590b' '' $allTexts[$dekuSoldierTextId]),
    (& $newDekuSoldierCommandRow 17 (& $findDekuSoldierSourceLine '^\s*wait\s+30\s*$' 2) 'wait' '' '30' '' ''),
    (& $newDekuSoldierCommandRow 18 (& $findDekuSoldierSourceLine '^\s*orroomflag\s+\$40\s*$') 'orroomflag' '' '40' '' ''),
    (& $newDekuSoldierCommandRow 19 (& $findDekuSoldierSourceLine '^\s*scriptend\s*$') 'scriptend' '' '' '' '')
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\deku_forest_soldier_commands.tsv'),
    $dekuSoldierCommandRows)

# The room 1:81 warp hands control to the palace guards. The retail sequence
# then scrolls through rooms 1:46, 1:36, and 1:26, runs four independent
# scripts in throne room 1:16, and uses Nayru's disableLcdAndLoadRoom path to
# return to a manually parsed two-guard list in 1:46.
$palaceMainScriptPath =
    Join-Path $Disassembly 'scripts\ages\scripts.s'
$palaceMainScriptSource = Read-ImportText $palaceMainScriptPath
$palaceObjectSource = $mainObjectSource
$palaceExtraObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\extraData3.s')
$palaceNayruSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\nayru.s')

if ($palaceObjectSource -notmatch
        '(?ms)^group1Map46ObjectData:.*?obj_Interaction \$40 \$02 \$28 \$48.*?obj_Interaction \$40 \$09 \$28 \$58.*?obj_Interaction \$40 \$0b \$38 \$38.*?obj_End' -or
    $palaceObjectSource -notmatch
        '(?ms)^group1Map36ObjectData:.*?obj_Interaction \$40 \$05 \$68 \$50.*?obj_Interaction \$40 \$03 \$18 \$48.*?obj_Interaction \$40 \$03 \$18 \$58.*?obj_Interaction \$40 \$03 \$28 \$38.*?obj_Interaction \$40 \$03 \$28 \$68.*?obj_End' -or
    $palaceObjectSource -notmatch
        '(?ms)^group1Map26ObjectData:.*?obj_Interaction \$40 \$05 \$68 \$50.*?obj_End' -or
    $palaceObjectSource -notmatch
        '(?ms)^group1Map16ObjectData:.*?obj_Interaction \$40 \$04 \$38 \$38.*?obj_Interaction \$40 \$06 \$68 \$50.*?obj_Interaction \$4d \$00 \$28 \$50.*?obj_Interaction \$36 \$01 \$f0 \$58.*?obj_End' -or
    $palaceExtraObjectSource -notmatch
        '(?ms)^ambisPalaceEntranceGuards:\s+obj_Interaction \$40 \$07 \$28 \$48\s+obj_Interaction \$40 \$02 \$28 \$58\s+obj_End' -or
    $dekuSoldierSource -notmatch
        '(?ms)^linkEnterPalaceSimulatedInput:\s+dwb \$7fff BTN_UP\s+\.dw \$ffff.*?^linkExitPalaceSimulatedInput\s+dwb 30\s+\$00\s+dwb 40\s+BTN_DOWN\s+dwb \$7fff \$00\s+\.dw \$ffff' -or
    $dekuSoldierSource -notmatch
        '(?ms)^soldierSubid04Substate0:.*?cp \$06.*?ld \(hl\),30.*?^soldierSubid04Substate1:.*?ld bc,\$fe40.*?SND_JUMP.*?^soldierSubid04Substate2:.*?ld c,\$20.*?ld \(hl\),\$08.*?^soldierSubid04Substate3:.*?ld \(hl\),\$10.*?ld \(hl\),SPEED_200' -or
    $palaceNayruSource -notmatch
        '(?ms)^nayruSubid01:.*?ld bc,\$0146\s+call disableLcdAndLoadRoom\s+call resetCamera.*?ambisPalaceEntranceGuards.*?ld \(hl\),\$03.*?ld \(hl\),\$38.*?ld \(hl\),\$50.*?loadGfxRegisterStateIndex.*?wActiveMusic2.*?clearPaletteFadeVariablesAndRefreshPalettes') {
    throw 'The Mystery Seeds palace actor order, simulated input, reward jump, or direct return changed.'
}

$palaceSpeeds = @{
    'SPEED_080' = 0x14
    'SPEED_100' = 0x28
    'SPEED_0a0' = 0x19
    'SPEED_200' = 0x50
}
if ($speedSource -notmatch '(?m)^\s*SPEED_80\s+dsb\s+5\s*;\s*0x14$' -or
    $speedSource -notmatch '(?m)^\s*SPEED_a0\s+dsb\s+5\s*;\s*0x19$' -or
    $speedSource -notmatch '(?m)^\s*SPEED_100\s+dsb\s+5\s*;\s*0x28$' -or
    $speedSource -notmatch '(?m)^\s*SPEED_200\s+dsb\s+5\s*;\s*0x50$') {
    throw 'Could not resolve the four Ambi palace object speeds.'
}
if ($globalFlagValues['GLOBALFLAG_10'] -ne 0x10 -or
    $globalFlagValues['GLOBALFLAG_0b'] -ne 0x0b -or
    $treasureIds['TREASURE_MYSTERY_SEEDS'] -ne 0x24 -or
    $soundIds['SND_GETSEED'] -ne 0x5e -or
    $soundIds['SND_JUMP'] -ne 0x53) {
    throw 'Ambi palace flags, Mystery Seeds, music, or sound constants changed.'
}
$palaceMusicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
if ($palaceMusicSource -notmatch
        '(?m)^\s*MUS_LADX_SIDEVIEW\s+db\s*;\s*\$2f$' -or
    $palaceMusicSource -notmatch
        '(?m)^\.define SNDCTRL_MEDIUM_FADEOUT \$fb$') {
    throw 'Ambi palace music $2f or medium fade control $fb changed.'
}
$palaceSoundValues = @{
    'SND_GETSEED' = 0x5e
    'SND_JUMP' = 0x53
    'MUS_LADX_SIDEVIEW' = 0x2f
    'SNDCTRL_MEDIUM_FADEOUT' = 0xfb
}

$palaceBombReward =
    $treasureObjectRecords['TREASURE_OBJECT_BOMBS_02']
if ($null -eq $palaceBombReward -or
    $palaceBombReward.Treasure -ne 0x03 -or
    $palaceBombReward.SubId -ne 0x02 -or
    $palaceBombReward.Parameter -ne 0x10) {
    throw 'TREASURE_OBJECT_BOMBS_02 no longer grants the ten-Bomb palace reward.'
}
foreach ($textId in @(
    0x5904, 0x5905, 0x5906, 0x5907, 0x5908, 0x5909, 0x590c,
    0x1300, 0x1301, 0x1302, 0x1303, 0x1304, 0x1305, 0x1306,
    0x1d01, 0x1d02, 0x1d03)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Missing Ambi palace text TX_$($textId.ToString('x4'))."
    }
}
$palaceTextPositions = @{
    0x5905 = 0
    0x5906 = 0
    0x5907 = 0
    0x5908 = 2
    0x5909 = 2
    0x590c = 0
    0x1300 = 2
    0x1301 = 2
    0x1302 = 2
    0x1303 = 2
    0x1304 = 2
    0x1305 = 2
    0x1306 = 2
    0x1d01 = 2
    0x1d02 = 2
    0x1d03 = 1
}
if ($allTextPositions.ContainsKey(0x5904)) {
    throw 'Ambi palace entrance TX_5904 unexpectedly gained an explicit textbox position.'
}
foreach ($entry in $palaceTextPositions.GetEnumerator()) {
    if (-not $allTextPositions.ContainsKey($entry.Key) -or
        $allTextPositions[$entry.Key] -ne $entry.Value) {
        throw "Ambi palace TX_$($entry.Key.ToString('x4')) textbox position changed."
    }
}

$palaceAmbiAnimations = @(0..3 | ForEach-Object {
    Resolve-NpcAnimation 0x4d $_
})
$palaceNayruAnimations = @(0..6 | ForEach-Object {
    Resolve-NpcAnimation 0x36 $_
})
if ($palaceAmbiAnimations.Count -ne 4 -or
    $palaceAmbiAnimations.Where({
        [string]::IsNullOrWhiteSpace($_)
    }).Count -ne 0 -or
    $palaceNayruAnimations.Count -ne 7 -or
    $palaceNayruAnimations.Where({
        [string]::IsNullOrWhiteSpace($_)
    }).Count -ne 0) {
    throw 'Could not resolve Ambi animations $00-$03 or possessed Nayru animations $00-$06.'
}

$palaceEventRows = @(
    "# group`tentrance-room`tcorridor-room-1`tcorridor-room-2`tthrone-room`tmystery-seeds`tentrance-flag`tcompletion-flag`tnormal-speed`tstairs-speed`tslow-speed`tflight-speed`tside-guard-trigger-y`tside-guard-move-frames`treward-jump-delay`treward-jump-speed-z`treward-jump-gravity`treward-land-delay`texit-idle-frames`texit-down-frames`tfade-delay`tfade-frames`ttextbox-flags`treward-treasure`treward-subid`treward-object`treward-parameter`texit-player-y`texit-player-x`tterminal-text-id`tterminal-text-base64`tsource",
    (@(
        '1', '46', '36', '26', '16',
        $treasureIds['TREASURE_MYSTERY_SEEDS'].ToString('x2'),
        $globalFlagValues['GLOBALFLAG_10'].ToString('x2'),
        $globalFlagValues['GLOBALFLAG_0b'].ToString('x2'),
        '28', '19', '14', '50', '60', '10',
        '30', '-448', '20', '8', '30', '40', '3', '97', '04',
        $palaceBombReward.Treasure.ToString('x2'),
        $palaceBombReward.SubId.ToString('x2'),
        'TREASURE_OBJECT_BOMBS_02',
        $palaceBombReward.Parameter.ToString('x2'),
        '38', '50', '5909',
        [Convert]::ToBase64String(
            [Text.Encoding]::UTF8.GetBytes($allTexts[0x5909])),
        'mainData.s:group1Map46/36/26/16ObjectData;soldier.s:soldierSubid02-07;nayru.s:nayruSubid01'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\deku_forest_palace_event.tsv'),
    $palaceEventRows)

# PALH_97 puts possessed Nayru in OBJ palette slot 6, the first of the two
# palettes in paletteData44d8. Preserve all four RGB15 colors in source order.
$palacePaletteStart = $paletteDataSource.IndexOf(
    'paletteData44d8:', [StringComparison]::Ordinal)
$palacePaletteEnd = $paletteDataSource.IndexOf(
    'paletteData44e8:', $palacePaletteStart, [StringComparison]::Ordinal)
if ($palacePaletteStart -lt 0 -or $palacePaletteEnd -lt 0) {
    throw 'Could not locate possessed Nayru PALH_97 paletteData44d8.'
}
$palacePaletteBlock = $paletteDataSource.Substring(
    $palacePaletteStart, $palacePaletteEnd - $palacePaletteStart)
$palacePaletteColors = [regex]::Matches(
    $palacePaletteBlock,
    'm_RGB16 \$(?<r>[0-9a-f]{2}) \$(?<g>[0-9a-f]{2}) \$(?<b>[0-9a-f]{2})')
if ($palacePaletteColors.Count -ne 8) {
    throw "PALH_97 should contain eight colors, got $($palacePaletteColors.Count)."
}
$palacePaletteBytes = [Collections.Generic.List[byte]]::new()
for ($color = 0; $color -lt 4; $color++) {
    $palacePaletteBytes.Add(
        [Convert]::ToByte($palacePaletteColors[$color].Groups['r'].Value, 16))
    $palacePaletteBytes.Add(
        [Convert]::ToByte($palacePaletteColors[$color].Groups['g'].Value, 16))
    $palacePaletteBytes.Add(
        [Convert]::ToByte($palacePaletteColors[$color].Groups['b'].Value, 16))
}
Write-GeneratedBytes(
    (Join-Path $destination 'metadata\nayru_possessed_palette.bin'),
    $palacePaletteBytes.ToArray())

$palaceSupportedOpcodes =
    [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'checkmemoryeq', 'setanimation', 'wait', 'setspeed',
    'moveup', 'moveright', 'movedown', 'moveleft', 'showtext',
    'asm15', 'setangle', 'applyspeed', 'giveitem',
    'setdisabledobjectsto11', 'playsound', 'writememory',
    'enableallobjects', 'setglobalflag', 'enableinput',
    'checkpalettefadedone', 'rungenericnpc', 'scriptend')) {
    [void]$palaceSupportedOpcodes.Add($opcode)
}

$palaceCorridorCommands = Read-AssemblyCutsceneCommands `
    $palaceMainScriptPath 'soldierSubid05Script' `
    $palaceSupportedOpcodes 'soldierSubid06Script'
$palaceRewardCommands = Read-AssemblyCutsceneCommands `
    $palaceMainScriptPath 'soldierSubid04Script' `
    $palaceSupportedOpcodes 'soldierSubid05Script'
$palaceEscortCommands = Read-AssemblyCutsceneCommands `
    $palaceMainScriptPath 'soldierSubid06Script' `
    $palaceSupportedOpcodes 'soldierSubid07Script'
$palaceExitCommands = Read-AssemblyCutsceneCommands `
    $palaceMainScriptPath 'soldierSubid07Script' `
    $palaceSupportedOpcodes 'soldierSubid09Script'
$palaceAmbiCommands = Read-AssemblyCutsceneCommands `
    $palaceMainScriptPath 'ambiSubid00Script' `
    $palaceSupportedOpcodes 'ambiSubid01Script_part1'
$palaceNayruCommands = Read-AssemblyCutsceneCommands `
    $dekuSoldierScriptPath 'nayruScript01' `
    $palaceSupportedOpcodes 'nayruScript02_part2'

function Convert-PalaceCommandRows(
    [Collections.Generic.List[object]]$commands,
    [string]$actor,
    [object[]]$animations
) {
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add(
        "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
    $index = 0
    foreach ($command in $commands) {
        if ($command.Opcode -eq 'checkpalettefadedone') {
            # FadeOutBlack's blocking native command includes this source
            # gate; the runner must not add another update after it.
            continue
        }
        $opcode = $command.Opcode
        $rowActor = ''
        $arg0 = ''
        $arg1 = ''
        $payload = ''
        switch ($command.Opcode) {
            'checkmemoryeq' {
                if ($command.Operands -notmatch
                    '^wTmpcfc0\.genericCutscene\.cfd1,\s*\$(?<value>[0-9a-f]{2})$') {
                    throw "Unexpected palace memory gate '$($command.Operands)' at line $($command.Line)."
                }
                $arg0 = $Matches['value']
                $payload = 'CutsceneSignal'
            }
            'setanimation' {
                if ($command.Operands -notmatch '^\$(?<value>[0-9a-f]{2})$') {
                    throw "Malformed palace animation at line $($command.Line)."
                }
                $animation = [Convert]::ToInt32($Matches['value'], 16)
                if ($animation -lt 0 -or $animation -ge $animations.Count) {
                    throw "Palace actor $actor has no animation `$$($Matches['value'])."
                }
                $rowActor = $actor
                $arg0 = $Matches['value']
                $payload = $animations[$animation]
            }
            'wait' {
                if ($command.Operands -notmatch '^(?<frames>[0-9]+)$') {
                    throw "Malformed palace wait at line $($command.Line)."
                }
                $arg0 = $Matches['frames']
            }
            'setspeed' {
                if (-not $palaceSpeeds.ContainsKey($command.Operands)) {
                    throw "Unknown palace speed '$($command.Operands)' at line $($command.Line)."
                }
                $rowActor = $actor
                $arg0 = $palaceSpeeds[$command.Operands].ToString('x2')
            }
            { $_ -in @('moveup', 'moveright', 'movedown', 'moveleft') } {
                if ($command.Operands -notmatch '^\$(?<counter>[0-9a-f]{2})$') {
                    throw "Malformed palace movement at line $($command.Line)."
                }
                $direction = switch ($command.Opcode) {
                    'moveup' { 0 }
                    'moveright' { 1 }
                    'movedown' { 2 }
                    'moveleft' { 3 }
                }
                $angle = @(0x00, 0x08, 0x10, 0x18)[$direction]
                $opcode = 'move'
                $rowActor = $actor
                $arg0 = $angle.ToString('x2')
                $arg1 = $Matches['counter']
                $payload = $animations[$direction]
            }
            'showtext' {
                if ($command.Operands -notmatch '^TX_(?<id>[0-9a-f]{4})$') {
                    throw "Malformed palace text at line $($command.Line)."
                }
                $textId = [Convert]::ToInt32($Matches['id'], 16)
                if (-not $allTexts.ContainsKey($textId)) {
                    throw "Missing palace text TX_$($Matches['id'])."
                }
                $arg0 = $Matches['id']
                if ($allTextPositions.ContainsKey($textId)) {
                    $arg1 = $allTextPositions[$textId].ToString()
                }
                $payload = $allTexts[$textId]
            }
            'setangle' {
                if ($command.Operands -notmatch '^\$(?<angle>[0-9a-f]{2})$') {
                    throw "Malformed palace angle at line $($command.Line)."
                }
                $rowActor = $actor
                $arg0 = $Matches['angle']
            }
            'applyspeed' {
                if ($command.Operands -notmatch '^\$(?<counter>[0-9a-f]{2})$') {
                    throw "Malformed palace applyspeed at line $($command.Line)."
                }
                $rowActor = $actor
                $arg0 = $Matches['counter']
            }
            'playsound' {
                if (-not $palaceSoundValues.ContainsKey($command.Operands)) {
                    throw "Unknown palace sound '$($command.Operands)' at line $($command.Line)."
                }
                $arg0 = $palaceSoundValues[$command.Operands].ToString('x2')
            }
            'writememory' {
                if ($command.Operands -match
                    '^wTmpcfc0\.genericCutscene\.cfd1,\s*\$(?<value>[0-9a-f]{2})$') {
                    $arg0 = $Matches['value']
                    $payload = 'CutsceneSignal'
                } elseif ($command.Operands -match
                    '^wNumMysterySeeds,\s*\$(?<value>[0-9a-f]{2})$') {
                    $arg0 = $Matches['value']
                    $payload = 'MysterySeeds'
                } elseif ($command.Operands -match
                    '^wScrollMode,\s*\$(?<value>[0-9a-f]{2})$') {
                    $arg0 = $Matches['value']
                    $payload = 'ScrollMode'
                } elseif ($command.Operands -match
                    '^wUseSimulatedInput,\s*\$(?<value>[0-9a-f]{2})$') {
                    $arg0 = $Matches['value']
                    $payload = 'SimulatedInput'
                } elseif ($command.Operands -eq
                    'wTextboxFlags, TEXTBOXFLAG_ALTPALETTE1') {
                    $arg0 = '04'
                    $payload = 'TextboxFlags'
                } else {
                    throw "Unexpected palace memory write '$($command.Operands)' at line $($command.Line)."
                }
            }
            'giveitem' {
                if ($command.Operands -ne 'TREASURE_OBJECT_BOMBS_02') {
                    throw "Unexpected palace reward '$($command.Operands)'."
                }
                $arg0 = $palaceBombReward.Treasure.ToString('x2')
                $arg1 = $palaceBombReward.SubId.ToString('x2')
            }
            'setdisabledobjectsto11' {
                $opcode = 'setdisabledobjects'
                $arg0 = '11'
            }
            'enableallobjects' {
                $opcode = 'native'
                $payload = 'EnableAllObjects'
            }
            'setglobalflag' {
                if ($command.Operands -ne 'GLOBALFLAG_0b') {
                    throw "Unexpected palace global flag '$($command.Operands)'."
                }
                $arg0 = $globalFlagValues[$command.Operands].ToString('x2')
            }
            'asm15' {
                $handler = switch -Regex ($command.Operands) {
                    '^scriptHelp\.soldierGiveMysterySeeds$' {
                        'GiveMysterySeeds'; break
                    }
                    '^scriptHelp\.forceLinkDirection,\s*\$03$' {
                        'ForceLinkLeft'; break
                    }
                    '^scriptHelp\.forceLinkDirection,\s*\$00$' {
                        'ForceLinkUp'; break
                    }
                    '^scriptHelp\.soldierSetSimulatedInputToEscortLink,\s*\$01$' {
                        'StartExitInput'; break
                    }
                    '^objectSetVisible$' { 'ShowNayru'; break }
                    '^scriptHelp\.soldierUpdateMinimap$' {
                        'UpdateMinimap'; break
                    }
                    '^fadeoutToBlackWithDelay,\s*\$03$' {
                        'FadeOutBlack'; break
                    }
                    default {
                        throw "Unknown palace asm15 '$($command.Operands)' at line $($command.Line)."
                    }
                }
                if ($handler -eq 'FadeOutBlack') {
                    $opcode = 'nativeblock'
                    $arg0 = '97'
                } else {
                    $opcode = 'native'
                }
                $payload = $handler
            }
            'rungenericnpc' {
                if ($command.Operands -ne 'TX_5909') {
                    throw "Unexpected palace generic NPC text '$($command.Operands)'."
                }
                $opcode = 'native'
                $payload = 'BecomeGenericGuard'
            }
        }
        $rows.Add((New-CutsceneCommandRow `
            $command.Script $index $command.Label $command.Line `
            $opcode $rowActor $arg0 $arg1 $payload))
        $index++
        if ($command.Opcode -eq 'rungenericnpc') {
            $rows.Add((New-CutsceneCommandRow `
                $command.Script $index $command.Label $command.Line `
                'scriptend' '' '' '' ''))
            $index++
        }
    }
    return $rows
}

$palaceCorridorRows = Convert-PalaceCommandRows `
    $palaceCorridorCommands 'CorridorGuard' $dekuSoldierAnimations
$palaceRewardRows = Convert-PalaceCommandRows `
    $palaceRewardCommands 'RewardGuard' $dekuSoldierAnimations
$palaceEscortRows = Convert-PalaceCommandRows `
    $palaceEscortCommands 'EscortGuard' $dekuSoldierAnimations
$palaceAmbiRows = Convert-PalaceCommandRows `
    $palaceAmbiCommands 'Ambi' $palaceAmbiAnimations
$palaceNayruRows = Convert-PalaceCommandRows `
    $palaceNayruCommands 'Nayru' $palaceNayruAnimations
$palaceExitRows = Convert-PalaceCommandRows `
    $palaceExitCommands 'ExitGuard' $dekuSoldierAnimations

$palaceEntranceMatch = [regex]::Match(
    $dekuSoldierScriptSource,
    '(?ms)^soldierSubid02Script:(?<body>.*?)(?=^soldierSubid0aScript:)')
if (-not $palaceEntranceMatch.Success) {
    throw 'Could not isolate soldierSubid02Script.'
}
$palaceEntranceStart = $palaceEntranceMatch.Groups['body'].Index
$palaceEntranceEnd =
    $palaceEntranceStart + $palaceEntranceMatch.Groups['body'].Length
$findPalaceEntranceLine = {
    param([string]$pattern, [int]$occurrence = 0)
    return Find-CutsceneCommandSourceLine `
        $dekuSoldierScriptSource $palaceEntranceStart $palaceEntranceEnd `
        $pattern 'soldierSubid02Script:@escortCutscene' $occurrence
}
$newPalaceEntranceRow = {
    param(
        [int]$index,
        [int]$line,
        [string]$opcode,
        [string]$actor,
        [string]$arg0,
        [string]$arg1,
        [string]$payload)
    return New-CutsceneCommandRow `
        'soldierSubid02Script' $index '@escortCutscene' $line `
        $opcode $actor $arg0 $arg1 $payload
}
$palaceEntranceRows = @(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64",
    (& $newPalaceEntranceRow 0 (& $findPalaceEntranceLine '^\s*disableinput\s*$') 'disableinput' '' '' '' ''),
    (& $newPalaceEntranceRow 1 (& $findPalaceEntranceLine '^\s*asm15\s+forceLinkDirection,\s*\$00\s*$') 'native' '' '' '' 'ForceLinkUp'),
    (& $newPalaceEntranceRow 2 (& $findPalaceEntranceLine '^\s*wait\s+60\s*$') 'wait' '' '60' '' ''),
    (& $newPalaceEntranceRow 3 (& $findPalaceEntranceLine '^\s*setglobalflag\s+GLOBALFLAG_10\s*$') 'setglobalflag' '' '10' '' ''),
    (& $newPalaceEntranceRow 4 (& $findPalaceEntranceLine '^\s*showtext\s+TX_5904\s*$') 'showtext' '' '5904' '' $allTexts[0x5904]),
    (& $newPalaceEntranceRow 5 (& $findPalaceEntranceLine '^\s*wait\s+30\s*$') 'wait' '' '30' '' ''),
    (& $newPalaceEntranceRow 6 (& $findPalaceEntranceLine '^\s*setanimation\s+\$00\s*$') 'setanimation' 'EntranceGuard' '00' '' $dekuSoldierAnimations[0]),
    (& $newPalaceEntranceRow 7 (& $findPalaceEntranceLine '^\s*setspeed\s+SPEED_100\s*$') 'setspeed' 'EntranceGuard' '28' '' ''),
    (& $newPalaceEntranceRow 8 (& $findPalaceEntranceLine '^\s*setangle\s+\$04\s*$') 'setangle' 'EntranceGuard' '04' '' ''),
    (& $newPalaceEntranceRow 9 (& $findPalaceEntranceLine '^\s*asm15\s+soldierSetSimulatedInputToEscortLink,\s*\$00\s*$') 'native' '' '' '' 'StartEntranceInput'),
    (& $newPalaceEntranceRow 10 (& $findPalaceEntranceLine '^\s*applyspeed\s+\$0b\s*$') 'applyspeed' 'EntranceGuard' '0b' '' ''),
    (& $newPalaceEntranceRow 11 (& $findPalaceEntranceLine '^\s*setangle\s+\$00\s*$') 'setangle' 'EntranceGuard' '00' '' ''),
    (& $newPalaceEntranceRow 12 (& $findPalaceEntranceLine '^\s*applyspeed\s+\$80\s*$') 'applyspeed' 'EntranceGuard' '80' '' ''),
    (& $newPalaceEntranceRow 13 (& $findPalaceEntranceLine '^\s*scriptend\s*$') 'scriptend' '' '' '' '')
)

Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\deku_forest_palace_entrance_commands.tsv'),
    $palaceEntranceRows)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\deku_forest_palace_corridor_commands.tsv'),
    $palaceCorridorRows)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\deku_forest_palace_reward_guard_commands.tsv'),
    $palaceRewardRows)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\deku_forest_palace_escort_guard_commands.tsv'),
    $palaceEscortRows)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\deku_forest_palace_ambi_commands.tsv'),
    $palaceAmbiRows)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\deku_forest_palace_nayru_commands.tsv'),
    $palaceNayruRows)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\deku_forest_palace_exit_guard_commands.tsv'),
    $palaceExitRows)
