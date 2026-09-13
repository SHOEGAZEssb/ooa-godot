# Native Skull Dungeon records retain their position in each main object
# stream so the runtime can merge them with shared doors and entrances.
$skullRows = [Collections.Generic.List[string]]::new()
$skullRows.Add("# group`troom`torder`tkind`tid`tsubid`ty`tx`tcondition`tsource`tvar03")
for ($room = 0x68; $room -le 0x92; $room++) {
    $label = 'group4Map' + $room.ToString('x2') + 'ObjectData'
    $body = [regex]::Match($mainObjectSource,
        ('(?ms)^' + $label + ':\s*(?<body>.*?)(?=^[A-Za-z_][A-Za-z0-9_]*:|\z)')).Groups['body'].Value
    $order = 0
    foreach ($line in $body -split '\r?\n') {
        if ($line -notmatch '^\s*obj_(?<kind>\w+)\s*(?<args>[^;]*)') { continue }
        $kind = $Matches['kind']
        if ($kind -eq 'BeforeEvent' -and $room -in @(0x80, 0x6b)) {
            $sourceLabel = 'group4Map' + $room.ToString('x2') + 'BeforeEventObjectData'
            $enemyId = if ($room -eq 0x80) { '73' } else { '7b' }
            $enemyKind = if ($room -eq 0x80) { 'armos-warrior' } else { 'eyesoar' }
            if ($line -notmatch "obj_BeforeEvent $sourceLabel\s*$") { throw 'Skull miniboss pointer changed.' }
            $boss = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/ages/enemyData.s') $sourceLabel 'obj_SpecificEnemyA')
            if ($boss.Count -ne 1 -or ($boss[0].Operands -join ' ') -ne ('$00 $' + $enemyId + ' $00 $58 $78')) {
                throw "${sourceLabel}: expected native $enemyKind spawner."
            }
            $skullRows.Add("4`t$($room.ToString('x2'))`t$order`t$enemyKind`t$enemyId`t00`t58`t78`tflag80-clear`tobjects/ages/enemyData.s:$sourceLabel`t00")
        }
        $operands = @([regex]::Matches($Matches['args'], '\$([0-9a-f]+)') | ForEach-Object {
            [Convert]::ToInt32($_.Groups[1].Value, 16)
        })
        if ($kind -eq 'Interaction' -and ($operands[0] -in @(0x15, 0x19, 0x1a, 0x1b, 0x22, 0x25, 0x61, 0x78, 0x79, 0x7f, 0xd8) -or
            ($room -eq 0x80 -and $operands[0] -eq 0x20 -and $operands[1] -eq 0) -or
            ($room -eq 0x6b -and $operands[0] -eq 0x20 -and $operands[1] -eq 1) -or
            ($operands[0] -eq 0x20 -and $operands[1] -in @(2, 3)) -or
            ($operands[0] -eq 0x21 -and $operands[1] -in @(0x03, 0x07, 0x08, 0x0f, 0x10, 0x11, 0x12)))) {
            $id = $operands[0]
            $subid = $operands[1]
            $expectedOperands = if ($id -in @(0x15, 0xd8) -or ($id -eq 0x21 -and $subid -eq 0x0f)) { 2 } else { 4 }
            if ($operands.Count -ne $expectedOperands -or
                ($id -in @(0x15, 0x1a, 0x22, 0x25) -and $subid -ne 0) -or
                ($id -in @(0x19, 0x1b) -and $subid -notin @(0, 1)) -or
                ($id -eq 0x61 -and $subid -notin @(0x30, 0x31)) -or
                ($id -eq 0x78 -and $subid -notin @(0x04, 0x08)) -or
                ($id -eq 0x79 -and $subid -notin @(0x03, 0x08, 0x11, 0x1a, 0x23, 0x29)) -or
                ($id -eq 0x7f -and $subid -ne 0) -or
                ($id -eq 0xd8 -and $subid -notin @(0, 1, 2))) {
                throw "${label}: unsupported Skull interaction operands in $line."
            }
            $nativeKind = switch ($id) {
                0x15 { 'toggle-floor' }
                0x19 { 'colored-cube' }
                0x1a { 'cube-flame' }
                0x1b { 'minecart-gate' }
                0x20 { if ($subid -eq 0) { 'miniboss-reward' } elseif ($subid -eq 1) { 'boss-reward' } else { 'orb-chest' } }
                0x22 { 'floor-color-changer' }
                0x25 { 'tile-filler' }
                0x61 { 'lever' }
                0x78 { 'switch-tile-toggler' }
                0x79 { 'moving-platform' }
                0x7f { 'essence' }
                0xd8 { 'lever-lava-filler' }
                0x21 { switch ($subid) {
                    0x03 { 'cube-light-sensor' }
                    0x07 { 'floor-switch-bit' }
                    0x08 { 'cube-switch-sensor' }
                    0x0f { 'floor-pattern-trigger' }
                    0x10 { 'floor-pattern-key' }
                    0x11 { 'floor-fill-chest' }
                    0x12 { 'blue-flame-chest' }
                } }
            }
            $y = if ($operands.Count -eq 4) { $operands[2] } else { 0 }
            $x = if ($operands.Count -eq 4) { $operands[3] } else { 0 }
            $condition = if ($id -eq 0x21 -and $subid -in @(0x10, 0x11, 0x12)) { 'item-clear' } else { 'always' }
            # dungeonScript_bossDeath re-creates an uncollected heart on
            # completed-room entry; only the boss itself uses flag80-clear.
            if ($id -eq 0x20) { $condition = if ($subid -eq 0) { 'flag80-clear' } else { 'item-clear' } }
            $skullRows.Add("4`t$($room.ToString('x2'))`t$order`t$nativeKind`t$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($y.ToString('x2'))`t$($x.ToString('x2'))`t$condition`tobjects/ages/mainData.s:$label`t00")
        }
        if ($kind -eq 'Part' -and $operands[0] -eq 0x0b) {
            if ($operands.Count -ne 5 -or $operands[1] -ne 0 -or $operands[4] -eq 0) { throw "${label}: unsupported moving orb operands." }
            $skullRows.Add("4`t$($room.ToString('x2'))`t$order`tmoving-orb`t0b`t00`t$($operands[2].ToString('x2'))`t$($operands[3].ToString('x2'))`talways`tobjects/ages/mainData.s:$label`t$($operands[4].ToString('x2'))")
        }
        if ($kind -notin @('End', 'EndPointer', 'Pointer', 'IfRoomFlag', 'IfRoomFlagUnset', 'EndIf')) { $order++ }
    }
}
if ($skullRows.Count -ne 47 -or ($skullRows[1..46] -join "`n") -notmatch
    '(?s)4\t71\t2\tfloor-color-changer\t22\t00\t58\t78.*4\t71\t3\ttoggle-floor.*4\t72\t0\ttoggle-floor.*4\t79\t1\ttoggle-floor.*4\t7b\t1\ttoggle-floor') {
    throw 'Skull Dungeon floor controllers lost source positions/order in 4:71/72/79/7b.'
}
Write-GeneratedTable((Join-Path $destination 'objects/skull_dungeon_objects.tsv'), $skullRows)

$orbChestSource = Read-ImportText (Join-Path $Disassembly 'scripts/ages/dungeonScripts.s')
$orbChestDispatch = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/dungeonScript.s')
if ($orbChestDispatch -notmatch '(?ms)^@dungeon4:\s*\.dw mainScripts.dungeonScript_minibossDeath\s*\.dw mainScripts.dungeonScript_bossDeath\s*\.dw mainScripts.skullDungeonScript_spawnChestWhenOrb0Hit\s*\.dw mainScripts.skullDungeonScript_spawnChestWhenOrb1Hit' -or
    $orbChestSource -notmatch '(?ms)^spawnChestAfterPuff:\s*playsound SND_SOLVEPUZZLE\s*createpuff\s*wait (?<wait>\d+)\s*settilehere TILEINDEX_CHEST\s*scriptend') {
    throw 'Orb chest scripts lost their dispatch or puff/wait/tile sequence.'
}
$orbChestWait = [int]$Matches['wait']
$orbChestRows = [Collections.Generic.List[string]]::new()
$orbChestRows.Add("# subid`tmask`twait`tsource")
for ($orb = 0; $orb -lt 2; $orb++) {
    $label = "skullDungeonScript_spawnChestWhenOrb${orb}Hit"
    if ($orbChestSource -notmatch ("(?ms)^${label}:\s*stopifitemflagset\s*checkflagset \`$0${orb}, wToggleBlocksState\s*scriptjump spawnChestAfterPuff")) {
        throw "${label}: unsupported orb gate."
    }
    $orbChestRows.Add("$((2 + $orb).ToString('x2'))`t$((1 -shl $orb).ToString('x2'))`t$orbChestWait`tscripts/ages/dungeonScripts.s:${label}->spawnChestAfterPuff")
}
Write-GeneratedTable((Join-Path $destination 'objects/orb_chest_scripts.tsv'), $orbChestRows)

$skullEssenceSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/essence.s')
if ($skullEssenceSource -notmatch '(?ms)^@essenceWarps:\s+\.ifdef ROM_AGES\s+\.db \$80, \$8d, \$26, TRANSITION_DEST_SET_RESPAWN\s+\.db \$81, \$83, \$25, TRANSITION_DEST_SET_RESPAWN\s+\.db \$80, \$ba, \$55, TRANSITION_DEST_SET_RESPAWN\s+\.db \$80, \$03, \$35, TRANSITION_DEST_X_SHIFTED' -or
    $skullEssenceSource -notmatch '(?ms)^@getEssenceTextTable:\s+\.db <TX_000e\s+\.db <TX_000f\s+\.db <TX_0010\s+\.db <TX_0011' -or
    -not $allTexts.ContainsKey(0x0011)) { throw 'Skull Essence text or shifted exit warp changed.' }
$transitions = Read-ImportText (Join-Path $Disassembly 'constants/common/transitions.s')
if ($transitions -notmatch '(?m)^\.define TRANSITION_DEST_X_SHIFTED\s+\$e\b') { throw 'Skull Essence exit transition changed.' }
$textPosition = if ($allTextPositions.ContainsKey(0x0011)) { $allTextPositions[0x0011] } else { 0 }
$message = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x0011]))
Write-GeneratedTable((Join-Path $destination 'objects/skull_dungeon_essence.tsv'), @(
    "# index`ttext-id`ttext-position`tmessage-base64`tdestination-group`tdestination-room`tdestination-position`tdestination-transition`tsource",
    "3`t0011`t$textPosition`t$message`t0`t03`t35`t0e`tobject_code/common/interactions/essence.s:@getEssenceTextTable/@essenceWarps"
))

$lavaSource = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/leverLavaFiller.s')
$lavaTileSource = Read-ImportText (Join-Path $Disassembly 'constants/common/tileIndices.s')
foreach ($tile in @{
    TILEINDEX_DRIED_LAVA = '01'; TILEINDEX_DUNGEON_LAVA_1 = '61';
    TILEINDEX_LAVA_SOURCE_UP_LEFT = 'c3'; TILEINDEX_LAVA_SOURCE_DOWN_LEFT = 'c6';
    TILEINDEX_LAVA_SOURCE_UP_LEFT_EMPTY = 'c9'; TILEINDEX_LAVA_SOURCE_DOWN_LEFT_EMPTY = 'cc'
}.GetEnumerator()) {
    if ($lavaTileSource -notmatch ('(?m)^\.define ' + $tile.Key + '\s+\$' + $tile.Value + '\b')) {
        throw "INTERAC_LEVER_LAVA_FILLER tile constant $($tile.Key) changed."
    }
}
if ($lavaSource -notmatch '(?ms)^@counter2Vals:\s+\.db \$04 \$06 \$06 \$06 \$06 \$06 \$06 \$06' -or
    $lavaSource -notmatch '(?ms)^@state1:.*?wLever1PullDistance.*?bit 7,a.*?ld \(hl\),30.*?SND_SOLVEPUZZLE' -or
    $lavaSource -notmatch '(?ms)^@state3:.*?wLever1PullDistance.*?or a.*?SND_DOORCLOSE.*?^@state4:.*?interactionDecCounter1.*?getRandomNumber\s+and \$03\s+add TILEINDEX_DUNGEON_LAVA_1.*?setTileInAllBuffers' -or
    $lavaSource -notmatch '(?ms)^@toggleLavaSource:.*?ld b,\$06.*?ld b,\$fa.*?sub TILEINDEX_LAVA_SOURCE_UP_LEFT\s+cp \$0c') {
    throw 'INTERAC_LEVER_LAVA_FILLER timing, lever signal, source toggle, or lava RNG changed.'
}
$lavaRows = [Collections.Generic.List[string]]::new()
$lavaRows.Add("# subid`tinterval`tscript`tsource")
for ($subid = 0; $subid -lt 6; $subid++) {
    $label = "@subid${subid}Script"
    $body = [regex]::Match($lavaSource, "(?ms)^${label}:\s*(?<body>.*?)(?=^@subid|\z)").Groups['body'].Value
    $bytes = [Collections.Generic.List[string]]::new()
    foreach ($line in $body -split '\r?\n') {
        if ($line -notmatch '^\s*\.db\s+(?<bytes>[^;]+)') { continue }
        foreach ($value in [regex]::Matches($Matches['bytes'], '\$([0-9a-f]{2})')) {
            $bytes.Add($value.Groups[1].Value)
        }
    }
    if ($bytes.Count -lt 3 -or $bytes[0] -eq '00' -or $bytes[-1] -ne '00' -or $bytes[-2] -ne '00' -or
        ($bytes[0..($bytes.Count - 2)] -join ',') -match '00,00') {
        throw "${label}: expected ordered zero-terminated lava groups and a final empty group."
    }
    $interval = if ($subid -eq 0) { '04' } else { '06' }
    $lavaRows.Add("$($subid.ToString('x2'))`t$interval`t$($bytes -join ',')`tobject_code/ages/interactions/leverLavaFiller.s:$label")
}
Write-GeneratedTable((Join-Path $destination 'objects/lever_lava_scripts.tsv'), $lavaRows)

$skullEventSource = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/dungeonEvents.s')
$fillerSource = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/tileFiller.s')
if ($fillerSource -notmatch '(?ms)^interactionCode25:\s+call returnIfScrollMode01Unset.*?@state0:.*?ld a,TILEINDEX_YELLOW_FLOOR.*?@state1:.*?callab getLinkTilePosition.*?add \$f0.*?inc a.*?add \$10.*?dec a.*?@updateFloor:.*?cp TILEINDEX_BLUE_FLOOR.*?ld a,TILEINDEX_RED_FLOOR.*?ld a,TILEINDEX_YELLOW_FLOOR.*?ld a,SND_GETSEED' -or
    $skullEventSource -notmatch '(?ms)^interaction21_subid11:\s+call interactionDeleteAndRetIfEnabled02\s+call interactionDeleteAndRetIfItemFlagSet\s+ld a,TILEINDEX_BLUE_FLOOR\s+call findTileInRoom\s+ret z\s+spawnChestAndDeleteSelf:') {
    throw 'Skull tile-filler adjacency/color/scroll gates or no-blue chest condition changed.'
}
$skullPatterns = [Collections.Generic.List[string]]::new()
$skullPatterns.Add("# subid`tcolor`tpositions`tsource")
$skullColors = @{ TILEINDEX_RED_TOGGLE_FLOOR = 0; TILEINDEX_YELLOW_TOGGLE_FLOOR = 1; TILEINDEX_BLUE_TOGGLE_FLOOR = 2 }
foreach ($subid in @('0f', '10')) {
    $label = "interaction21_subid${subid}"
    $body = [regex]::Match($skullEventSource, "(?ms)^${label}:\s*(?<body>.*?)(?=^interaction21_subid|\z)").Groups['body'].Value
    if ($body -notmatch 'call interactionDeleteAndRetIfEnabled02' -or
        ($subid -eq '0f' -and $body -notmatch 'ld \(wActiveTriggers\),a') -or
        ($subid -eq '10' -and $body -notmatch 'call interactionDeleteAndRetIfItemFlagSet')) {
        throw "${label}: native pattern event lifetime/side effects changed."
    }
    $groups = [regex]::Matches($body, '(?m)^\s*\.db\s+(?<tile>TILEINDEX_[A-Z0-9_]+)\s+(?<values>[^;\r\n]+)')
    $expectedGroups = if ($subid -eq '0f') { 3 } else { 2 }
    if ($groups.Count -ne $expectedGroups) { throw "${label}: missing tile pattern groups." }
    for ($i = 0; $i -lt $groups.Count; $i++) {
        $tile = $groups[$i].Groups['tile'].Value
        if (-not $skullColors.ContainsKey($tile)) { throw "${label}: unsupported pattern tile $tile." }
        $values = @([regex]::Matches($groups[$i].Groups['values'].Value, '\$([0-9a-f]{2})') | ForEach-Object { $_.Groups[1].Value })
        $terminator = if ($i -eq $groups.Count - 1) { '00' } else { 'ff' }
        if ($values.Count -lt 2 -or $values[-1] -ne $terminator) { throw "${label}: invalid pattern terminator." }
        $skullPatterns.Add("$subid`t$($skullColors[$tile])`t$($values[0..($values.Count - 2)] -join ',')`tobject_code/ages/interactions/dungeonEvents.s:$label")
    }
}
Write-GeneratedTable((Join-Path $destination 'objects/skull_dungeon_patterns.tsv'), $skullPatterns)

$floorSource = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/floorColorChanger.s')
$toggleSource = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/toggleFloor.s')
if ($floorSource -notmatch '(?ms)^@subid1:.*?ld \(hl\),\$ff.*?generateRandomBuffer.*?ld de,wBigBuffer.*?^@done:\s+call @convertNextTile' -or
    $floorSource -notmatch '(?ms)^@notColoredFloor:.*?call setTileInRoomLayoutBuffer' -or
    $toggleSource -notmatch '(?ms)^@subid01:.*?ld a,\(bc\)\s+inc a\s+cp TILEINDEX_RED_TOGGLE_FLOOR\+3\s+jr c,\+.*?setTileInRoomLayoutBuffer') {
    throw 'Skull shared floor handlers changed their native worker/buffer/landing contract.'
}
