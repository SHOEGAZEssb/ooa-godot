# Preserve the overworld map's 14x14 region-text table, cursor popup table,
# tree icons, and every text bank the map resolver can select. Conditional
# popup behavior remains a runtime concern because it reads live room/global
# flags, but the table bytes and TX strings come directly from the disassembly.
$mapDataSource = Get-Content -Raw (Join-Path $Disassembly 'data\ages\mapTextAndPopups.s')
$mapDataSource = [regex]::Replace(
    $mapDataSource,
    '(?ms)\.ifdef REGION_JP\s*.*?\.else\s*(?<us>.*?)\.endif',
    '${us}')

function Read-MapByteArray([string]$label, [string]$nextLabel) {
    $match = [regex]::Match(
        $mapDataSource,
        "(?ms)^${label}:\s*(?<body>.*?)(?=^${nextLabel}:)")
    if (-not $match.Success) { throw "Could not find map data array $label." }
    return @([regex]::Matches($match.Groups['body'].Value, '\$(?<value>[0-9a-f]{2})') |
        ForEach-Object { [Convert]::ToInt32($_.Groups['value'].Value, 16) })
}

function Read-MinimapPopups([string]$label, [string]$nextLabel) {
    $match = [regex]::Match(
        $mapDataSource,
        "(?ms)^${label}:\s*(?<body>.*?)(?=^${nextLabel}:|\z)")
    if (-not $match.Success) { throw "Could not find minimap popup array $label." }
    $result = @{}
    foreach ($entry in [regex]::Matches(
        $match.Groups['body'].Value,
        '(?m)^\s*\.db\s+\$(?<room>[0-9a-f]{2})\s+\$(?<popup>[0-9a-f]{2})')) {
        $room = [Convert]::ToInt32($entry.Groups['room'].Value, 16)
        if ($room -eq 0xff) { continue }
        # mapMenu_loadPopupData stops at the first matching room. A few source
        # tables intentionally repeat room IDs, so retain the first record.
        if (-not $result.ContainsKey($room)) {
            $result[$room] = [Convert]::ToInt32($entry.Groups['popup'].Value, 16)
        }
    }
    return $result
}

$presentMapTexts = Read-MapByteArray 'presentMapTextIndices' 'pastMapTextIndices'
$pastMapTexts = Read-MapByteArray 'pastMapTextIndices' 'presentMinimapPopups'
if ($presentMapTexts.Count -ne 196 -or $pastMapTexts.Count -ne 196) {
    throw "Expected 196 present and past map text indices, got $($presentMapTexts.Count) and $($pastMapTexts.Count)."
}
$presentMapPopups = Read-MinimapPopups 'presentMinimapPopups' 'pastMinimapPopups'
$pastMapPopups = Read-MinimapPopups 'pastMinimapPopups' '__end_of_file__'
if ($presentMapPopups.Count -ne 44 -or $pastMapPopups.Count -ne 38) {
    throw "Expected 44 present and 38 past popup rooms, got $($presentMapPopups.Count) and $($pastMapPopups.Count)."
}
$mapRows = [Collections.Generic.List[string]]::new()
$mapRows.Add('# room`tpresent-text`tpast-text`tpresent-popup`tpast-popup')
for ($y = 0; $y -lt 14; $y++) {
    for ($x = 0; $x -lt 14; $x++) {
        $index = $y * 14 + $x
        $room = $y * 16 + $x
        $presentPopup = if ($presentMapPopups.ContainsKey($room)) { $presentMapPopups[$room] } else { 0 }
        $pastPopup = if ($pastMapPopups.ContainsKey($room)) { $pastMapPopups[$room] } else { 0 }
        $mapRows.Add(
            "$($room.ToString('x2'))`t$($presentMapTexts[$index].ToString('x2'))`t$($pastMapTexts[$index].ToString('x2'))`t$($presentPopup.ToString('x2'))`t$($pastPopup.ToString('x2'))")
    }
}
$mapMetadataPath = Join-Path $destination 'map\overworld.tsv'
[IO.File]::WriteAllLines($mapMetadataPath, $mapRows, [Text.UTF8Encoding]::new($false))

$mapTextRows = [Collections.Generic.List[string]]::new()
$mapTextRows.Add('# text-id`tposition`tmessage-base64')
foreach ($textId in @($allTexts.Keys | Sort-Object)) {
    if ($textId -lt 0x0200 -or $textId -ge 0x0600) { continue }
    $position = if ($allTextPositions.ContainsKey($textId)) { $allTextPositions[$textId] } else { 0 }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $mapTextRows.Add("$($textId.ToString('x4'))`t$position`t$encoded")
}
$mapTextsPath = Join-Path $destination 'map\texts.tsv'
[IO.File]::WriteAllLines($mapTextsPath, $mapTextRows, [Text.UTF8Encoding]::new($false))

$treeWarpSource = Get-Content -Raw (Join-Path $Disassembly 'data\ages\treeWarps.s')
$treeWarpRows = [Collections.Generic.List[string]]::new()
$treeWarpRows.Add('# group`troom`tpopup')
foreach ($treeGroup in @(
    @{ Label = 'presentTreeWarps'; Group = 0; Next = 'pastTreeWarps' },
    @{ Label = 'pastTreeWarps'; Group = 1; Next = '__end_of_file__' })) {
    $pattern = if ($treeGroup.Next -eq '__end_of_file__') {
        "(?ms)^$($treeGroup.Label):\s*(?<body>.*)"
    } else {
        "(?ms)^$($treeGroup.Label):\s*(?<body>.*?)(?=^$($treeGroup.Next):)"
    }
    $block = [regex]::Match($treeWarpSource, $pattern)
    if (-not $block.Success) { throw "Could not find $($treeGroup.Label)." }
    foreach ($entry in [regex]::Matches(
        $block.Groups['body'].Value,
        '(?m)^\s*\.db\s+\$(?<room>[0-9a-f]{2})\s+\$[0-9a-f]{2}\s+\$(?<popup>[0-9a-f]{2})')) {
        $room = [Convert]::ToInt32($entry.Groups['room'].Value, 16)
        if ($room -eq 0) { continue }
        $treeWarpRows.Add("$($treeGroup.Group)`t$($room.ToString('x2'))`t$($entry.Groups['popup'].Value)")
    }
}
$treeWarpsPath = Join-Path $destination 'map\tree_warps.tsv'
if ($treeWarpRows.Count -ne 11) {
    throw "Expected 10 nonzero Ages tree-warp popup records, parsed $($treeWarpRows.Count - 1)."
}
[IO.File]::WriteAllLines($treeWarpsPath, $treeWarpRows, [Text.UTF8Encoding]::new($false))

$mapMenuCode = Get-Content -Raw (Join-Path $Disassembly 'code\bank2.s')
$entranceBlock = [regex]::Match(
    $mapMenuCode,
    '(?ms)^mapMenu_dungeonEntranceText:\s*\r?\n\s*\.ifdef ROM_AGES\s*(?<body>.*?)\s*\.else; ROM_SEASONS')
if (-not $entranceBlock.Success) { throw 'Could not find the Ages dungeon entrance text table.' }
$entranceRows = [Collections.Generic.List[string]]::new()
$entranceRows.Add('# dungeon`tgroup`troom`tfallback-text')
$dungeonIndex = 0
foreach ($entry in [regex]::Matches(
    $entranceBlock.Groups['body'].Value,
    '(?m)^\s*\.db\s+\$(?<room>[0-9a-f]{2}),\s*(?<group>\$80\|)?\(<TX_03(?<text>[0-9a-f]{2})\)')) {
    $group = if ($entry.Groups['group'].Success) { 4 } else { 5 }
    $entranceRows.Add(
        "$dungeonIndex`t$group`t$($entry.Groups['room'].Value)`t$($entry.Groups['text'].Value)")
    $dungeonIndex++
}
if ($dungeonIndex -ne 16) { throw "Expected 16 Ages dungeon entrance rows, parsed $dungeonIndex." }
$entrancePath = Join-Path $destination 'map\dungeon_entrances.tsv'
[IO.File]::WriteAllLines($entrancePath, $entranceRows, [Text.UTF8Encoding]::new($false))

function Read-ConstantIds([string]$path, [string]$prefix) {
    $ids = @{}
    foreach ($line in Get-Content $path) {
        if ($line -match "^\s*(?<name>${prefix}[A-Z0-9_]+)\s+(?:\.?db|db)\s*;\s*(?:0x|\$)(?<id>[0-9a-f]{2})") {
            $ids[$Matches['name']] = [Convert]::ToInt32($Matches['id'], 16)
        }
    }
    return $ids
}

function Convert-AsmByte([string]$value) {
    $trimmed = $value.Trim()
    if ($trimmed -match '^\$([0-9a-f]{2})$') {
        return [Convert]::ToInt32($Matches[1], 16)
    }
    return -1
}

function Resolve-TreasureId([string]$value, [hashtable]$treasureIds) {
    $trimmed = $value.Trim()
    if ($trimmed -match '^\$([0-9a-f]{2})$') {
        return [Convert]::ToInt32($Matches[1], 16)
    }
    if ($treasureIds.ContainsKey($trimmed)) {
        return $treasureIds[$trimmed]
    }
    return 0
}

$treasureIds = Read-ConstantIds (Join-Path $Disassembly "constants\common\treasure.s") "TREASURE_"
$itemIds = Read-ConstantIds (Join-Path $Disassembly "constants\common\items.s") "ITEM_"
if ($treasureIds['TREASURE_SWORD'] -ne 0x05 -or $itemIds['ITEM_SWORD'] -ne 0x05) {
    throw "Treasure/item constants no longer match the expected first-32 inventory ID identity."
}

# ITEM_SEED_SATCHEL ($19) creates the selected $20-$24 child item. Preserve
# the complete Ember child used by the first Satchel rather than duplicating
# its item tables and native constants in the runtime.
$itemDataSource = Get-Content -Raw (Join-Path $Disassembly 'data\ages\itemData.s')
$itemAttributesSource = Get-Content -Raw (Join-Path $Disassembly 'data\ages\itemAttributes.s')
$itemAnimationsSource = Get-Content -Raw (Join-Path $Disassembly 'data\itemAnimations.s')
$itemOamDataSource = Get-Content -Raw (Join-Path $Disassembly 'data\itemOamData.s')
$itemUsageSource = Get-Content -Raw (Join-Path $Disassembly 'data\ages\itemUsageTables.s')
$specialObjectAnimationsSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')
$specialObjectAnimationLogicSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\specialObjectAnimationsAndDamage.s')
$objectGfxHeadersSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\objectGfxHeaders.s')
$gfxHeadersSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\gfxHeaders.s')
$seedCodeSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\items\seeds.s')
$seedParentSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\itemParents\seedsParent.s')
$swordBeamSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\items\swordBeam.s')
$shieldParentSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\itemParents\shieldParent.s')
$collisionEffectsSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\collisionEffects.s')
$objectCollisionTableSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\objectCollisionTable.s')
$partDataSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\partData.s')
$partActiveCollisionsSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\partActiveCollisions.s')
$soundIds = Read-ConstantIds (Join-Path $Disassembly 'constants\common\music.s') 'SND_'

# ITEM_SHIELD ($01) uses the held-input parent slot and changes Link's ordinary
# walking graphics directly. Keep the exact source offsets and collision table
# boundary asserted here so runtime shield behavior cannot silently drift from
# the supported disassembly.
$linkGfxPointerBlock = [regex]::Match(
    $specialObjectAnimationsSource,
    '(?ms)^specialObject00GfxPointers:(?<body>.*?)(?=^specialObject00AnimationDataPointers:)')
$linkGfxEntries = if ($linkGfxPointerBlock.Success) {
    @([regex]::Matches(
        $linkGfxPointerBlock.Groups['body'].Value,
        'm_SpecialObjectGfxPointer \$(?<oam>[0-9a-f]{2}) spr_link \$(?<offset>[0-9a-f]{4}) \$[0-9a-f]{2}'))
} else { @() }
$expectedShieldLinkGfx = @{
    0x68 = 0x0400; 0x69 = 0x0500; 0x6a = 0x0480; 0x6b = 0x0080
    0x6c = 0x0400; 0x6d = 0x0580; 0x6e = 0x0480; 0x6f = 0x0080
    0x70 = 0x0600; 0x71 = 0x0780; 0x72 = 0x0680; 0x73 = 0x0700
    0x74 = 0x0600; 0x75 = 0x0780; 0x76 = 0x0880; 0x77 = 0x0700
    0x94 = 0x0440; 0x95 = 0x0540; 0x96 = 0x04c0; 0x97 = 0x00c0
    0x98 = 0x0440; 0x99 = 0x05c0; 0x9a = 0x04c0; 0x9b = 0x00c0
    0x9c = 0x0640; 0x9d = 0x07c0; 0x9e = 0x06c0; 0x9f = 0x0740
    0xa0 = 0x0640; 0xa1 = 0x07c0; 0xa2 = 0x08c0; 0xa3 = 0x0740
}
$shieldLinkGfxValid = $linkGfxEntries.Count -gt 0xa3
if ($shieldLinkGfxValid) {
    foreach ($index in $expectedShieldLinkGfx.Keys) {
        $entry = $linkGfxEntries[$index]
        if ([Convert]::ToInt32($entry.Groups['oam'].Value, 16) -ne 0 -or
            [Convert]::ToInt32($entry.Groups['offset'].Value, 16) -ne
                $expectedShieldLinkGfx[$index]) {
            $shieldLinkGfxValid = $false
            break
        }
    }
}
if ($itemIds['ITEM_SHIELD'] -ne 0x01 -or
    $treasureIds['TREASURE_SHIELD'] -ne 0x01 -or
    $soundIds['SND_SHIELD'] -ne 0x76 -or
    $soundIds['SND_CLINK2'] -ne 0x58 -or
    -not $shieldLinkGfxValid -or
    $itemUsageSource -notmatch
        '(?m)^\s*\.db\s+\$05,\s*<wGameKeysPressed\s*;\s*ITEM_SHIELD' -or
    $itemUsageSource -notmatch
        '(?m)^\s*\.db\s+\$00,\s*LINK_ANIM_MODE_NONE\s*;\s*ITEM_SHIELD' -or
    $itemAttributesSource -notmatch
        '(?m)^\s*\.db\s+\$01\s+\$00\s+\$00\s+\$00\s*;\s*\$01:\s*ITEM_SHIELD' -or
    $shieldParentSource -notmatch
        '(?ms)^parentItemCode_shield:.*?call @checkShieldIsUsable.*?call checkNoOtherParentItemsInUse.*?^@state0:.*?SND_SHIELD.*?^@state1:.*?wShieldLevel.*?wUsingShield.*?^@checkShieldIsUsable:.*?wLinkSwimmingState.*?call isLinkUnderwater.*?parentItemCheckButtonPressed' -or
    $specialObjectAnimationsSource -notmatch
        '(?m)^specialObject00GfxPointers:' -or
    $specialObjectAnimationLogicSource -notmatch
        '(?ms)Check if he.s holding out the shield, and what level.*?wUsingShield.*?ld c,\$07.*?cp \$02.*?inc c.*?@shieldEquipped:.*?ld c,\$05.*?wShieldLevel.*?cp \$01.*?ld c,\$06' -or
    $collisionEffectsSource -notmatch
        '(?ms)^@shieldPositionOffsets:\s*\.db \$f9 \$01 \$01 \$06 ; DIR_UP\s*\.db \$00 \$06 \$07 \$01 ; DIR_RIGHT\s*\.db \$06 \$ff \$01 \$06 ; DIR_DOWN\s*\.db \$00 \$f9 \$07 \$01 ; DIR_LEFT' -or
    $collisionEffectsSource -notmatch
        '(?ms)^collisionEffect1f:\s*ldhl LINKDMG_20, ENEMYDMG_34' -or
    $objectCollisionTableSource -notmatch
        '(?ms)ENEMYCOLLISION_PROJECTILE \(0x06\).*?\.db \$02 \$1f \$1f \$1f.*?ENEMYCOLLISION_PROJECTILE_WITH_RING_MOD \(0x07\).*?\.db \$3c \$1f \$1f \$1f' -or
    $partDataSource -notmatch
        '(?m)^\s*\.db \$8f \$87 \$22 \$fc \$40 \$0c \$03 \$00 ; \$18' -or
    $partDataSource -notmatch
        '(?m)^\s*\.db \$8e \$86 \$22 \$fc \$40 \$00 \$02 \$00 ; \$1a' -or
    $partActiveCollisionsSource -notmatch
        '(?m)^\s*dbrev %11111111 %10000010 %00001000 %00000000 ; 0x18' -or
    $partActiveCollisionsSource -notmatch
        '(?m)^\s*dbrev %11111111 %10000010 %00001000 %00000000 ; 0x1a') {
    throw 'ITEM_SHIELD usage, Link graphics, hitbox, sounds, or supported projectile collisions changed in the disassembly.'
}

$emberData = [regex]::Match(
    $itemDataSource,
    '(?m)^\s*\.db\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s+\$(?<palette>[0-9a-f]{2})\s*;\s*\$20:\s*ITEM_EMBER_SEED')
$emberAttributes = [regex]::Match(
    $itemAttributesSource,
    '(?m)^\s*\.db\s+\$(?<collision>[0-9a-f]{2})\s+\$(?<radius>[0-9a-f]{2})\s+\$(?<damage>[0-9a-f]{2})\s+\$[0-9a-f]{2}\s*;\s*\$20:\s*ITEM_EMBER_SEED')
if (-not $emberData.Success -or -not $emberAttributes.Success) {
    throw 'Could not parse ITEM_EMBER_SEED item data/attributes.'
}

$gfxIndex = [Convert]::ToInt32($emberData.Groups['gfx'].Value, 16)
$tileBase = [Convert]::ToInt32($emberData.Groups['tile'].Value, 16)
$palette = [Convert]::ToInt32($emberData.Groups['palette'].Value, 16)
$collision = [Convert]::ToInt32($emberAttributes.Groups['collision'].Value, 16)
$radius = [Convert]::ToInt32($emberAttributes.Groups['radius'].Value, 16)
$damage = [Convert]::ToInt32($emberAttributes.Groups['damage'].Value, 16)
if ($gfxIndex -ne 0x78 -or
    $objectGfxHeadersSource -notmatch '(?m)^\s*/\*\s*\$78\s*\*/\s*m_ObjectGfxHeader\s+spr_common_items') {
    throw 'ITEM_EMBER_SEED no longer resolves object GFX header $78 to spr_common_items.'
}

$emberFlameData = [regex]::Match(
    $seedCodeSource,
    '(?m)^@data:\s*\r?\n\s*\.db\s+\$(?<flags>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s+\$(?<counter>[0-9a-f]{2})\s+SND_LIGHTTORCH')
if (-not $emberFlameData.Success) {
    throw 'Could not parse ITEM_EMBER_SEED ignition graphics data.'
}
$flameFlags = [Convert]::ToInt32($emberFlameData.Groups['flags'].Value, 16)
$flameTileBase = [Convert]::ToInt32($emberFlameData.Groups['tile'].Value, 16)
$flameCounter = [Convert]::ToInt32($emberFlameData.Groups['counter'].Value, 16)
if (($flameFlags -band 0x08) -eq 0 -or
    $gfxHeadersSource -notmatch '(?m)^\s*m_GfxHeader\s+spr_common_sprites,\s*\$8001') {
    throw 'ITEM_EMBER_SEED ignition no longer selects spr_common_sprites in fixed VRAM bank 1.'
}

$expectedSourceFragments = @(
    @{ Source = $itemUsageSource; Pattern = '(?m)^\s*\.db\s+\$02,\s*<wGameKeysJustPressed\s*;\s*ITEM_SEED_SATCHEL'; Name = 'Satchel usage parameter' },
    @{ Source = $itemUsageSource; Pattern = '(?m)^\s*\.db\s+\$a0,\s*LINK_ANIM_MODE_21\s*;\s*ITEM_SEED_SATCHEL'; Name = 'Satchel Link animation' },
    @{ Source = $specialObjectAnimationsSource; Pattern = '(?ms)^animationData19fe9:\s*\.db\s+\$08\s+\$b0\s+\$06\s*\.db\s+\$7f\s+\$b0\s+\$86'; Name = 'Satchel Link pose timing' },
    @{ Source = $seedParentSource; Pattern = '(?ms)^parentItemCode_satchel:.*?ld c,\$00\s*ld e,\$01\s*call itemCreateChildWithID.*?jp c,clearParentItem.*?ld a,b\s*jp decNumActiveSeeds'; Name = 'Satchel child allocation/decrement order' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)^\s*ld bc,\$ffe0\s*call objectSetSpeedZ.*?@satchelPositionOffsets:\s*\.db \$fc \$00 \$fe.*?\.db \$01 \$04 \$fe.*?\.db \$05 \$00 \$fe.*?\.db \$01 \$fb \$fe'; Name = 'Satchel seed Z and directional offsets' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)call objectApplySpeed\s*ld c,\$1c\s*call itemUpdateThrowingVerticallyAndCheckHazards.*?ld a,SND_BOMB_LAND'; Name = 'Satchel flight and landing' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)@data:\s*\.db \$0a \$06 \$3a SND_LIGHTTORCH'; Name = 'Ember flame data' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)emberSeedBurn:.*?dec \(hl\).*?ld a,BREAKABLETILESOURCE_EMBER_SEED\s*call itemTryToBreakTile'; Name = 'Ember burn counter and break source' })
foreach ($fragment in $expectedSourceFragments) {
    if ($fragment.Source -notmatch $fragment.Pattern) {
        throw "Could not verify $($fragment.Name) in the supported disassembly."
    }
}

$emberAnimation = [regex]::Match(
    $itemAnimationsSource,
    '(?ms)^itemAnimation1e818:\s*\.db \$(?<d0>[0-9a-f]{2}) \$(?<t0>[0-9a-f]{2}) \$(?<p0>[0-9a-f]{2})\s*\.db \$(?<d1>[0-9a-f]{2}) \$(?<t1>[0-9a-f]{2}) \$(?<p1>[0-9a-f]{2})\s*^itemAnimation1e818Loop:\s*\.db \$(?<d2>[0-9a-f]{2}) \$(?<t2>[0-9a-f]{2}) \$(?<p2>[0-9a-f]{2})\s*\.db \$(?<d3>[0-9a-f]{2}) \$(?<t3>[0-9a-f]{2}) \$(?<p3>[0-9a-f]{2})\s*\.db \$(?<d4>[0-9a-f]{2}) \$(?<t4>[0-9a-f]{2}) \$(?<p4>[0-9a-f]{2})\s*m_AnimationLoop itemAnimation1e818Loop')
if (-not $emberAnimation.Success) {
    throw 'Could not parse itemAnimation1e818 for ITEM_EMBER_SEED.'
}
$animationParts = [Collections.Generic.List[string]]::new()
$emberOamPointers = [regex]::Match(
    $itemAnimationsSource,
    '(?ms)^item20OamDataPointers:.*?\r?\n(?<body>(?:\s*\.dw\s+itemOamData[0-9a-f]+\s*\r?\n){4})')
if (-not $emberOamPointers.Success) {
    throw 'Could not parse item20OamDataPointers for ITEM_EMBER_SEED.'
}
$emberOamLabels = @([regex]::Matches(
    $emberOamPointers.Groups['body'].Value,
    '(?m)^\s*\.dw\s+(?<label>itemOamData[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if ($emberOamLabels.Count -ne 4) {
    throw "Expected four ITEM_EMBER_SEED OAM pointers, parsed $($emberOamLabels.Count)."
}

function Read-ItemOamComposition([string]$label) {
    $block = [regex]::Match(
        $itemOamDataSource,
        "(?ms)^${label}:\s*(?<body>.*?)(?=^itemOamData[0-9a-f]+:|\z)")
    if (-not $block.Success) { throw "Could not resolve item OAM label $label." }
    $bytes = @([regex]::Matches(
        $block.Groups['body'].Value,
        '\$(?<value>[0-9a-f]{2})') |
        ForEach-Object { [Convert]::ToInt32($_.Groups['value'].Value, 16) })
    if ($bytes.Count -lt 1 -or $bytes.Count -ne 1 + $bytes[0] * 4) {
        throw "Malformed item OAM composition $label."
    }
    $parts = [Collections.Generic.List[string]]::new()
    for ($part = 0; $part -lt $bytes[0]; $part++) {
        $offset = 1 + $part * 4
        $parts.Add("$($bytes[$offset]),$($bytes[$offset + 1]),$($bytes[$offset + 2]),$($bytes[$offset + 3])")
    }
    return $parts -join ';'
}

for ($index = 0; $index -lt 5; $index++) {
    $duration = [Convert]::ToInt32($emberAnimation.Groups["d$index"].Value, 16)
    $oamIndex = [Convert]::ToInt32($emberAnimation.Groups["t$index"].Value, 16)
    $parameter = [Convert]::ToInt32($emberAnimation.Groups["p$index"].Value, 16)
    if (($oamIndex -band 1) -ne 0 -or ($oamIndex / 2) -ge $emberOamLabels.Count) {
        throw "ITEM_EMBER_SEED animation referenced invalid OAM pointer offset `$$($oamIndex.ToString('x2'))."
    }
    $encodedOam = Read-ItemOamComposition $emberOamLabels[$oamIndex / 2]
    $animationParts.Add("$duration,$parameter@$encodedOam")
}
$encodedEmberAnimation = ($animationParts -join '|') + '~2'
$radiusY = ($radius -shr 4) -band 0x0f
$radiusX = $radius -band 0x0f
$seedRows = [Collections.Generic.List[string]]::new()
$seedRows.Add('# parent-item`tseed-item`ttreasure-id`tsprite`ttile-base`tpalette`tcollision`tradius-y`tradius-x`tdamage`tinitial-z`tspeed-z`tgravity`tspeed-raw`tup-y`tup-x`tright-y`tright-x`tdown-y`tdown-x`tleft-y`tleft-x`tlink-frames`tflame-sprite`tflame-tile-base`tflame-oam-flags`tflame-counter`tlanding-sound`tflame-sound`tanimation`tsource')
$seedRows.Add(
    "$($itemIds['ITEM_SEED_SATCHEL'].ToString('x2'))`t$($itemIds['ITEM_EMBER_SEED'].ToString('x2'))`t$($treasureIds['TREASURE_EMBER_SEEDS'].ToString('x2'))`tspr_common_items`t$($tileBase.ToString('x2'))`t$($palette.ToString('x2'))`t$($collision.ToString('x2'))`t$radiusY`t$radiusX`t$($damage.ToString('x2'))`t-2`t-32`t28`t1e`t-4`t0`t1`t4`t5`t0`t1`t-5`t8`tspr_common_sprites`t$($flameTileBase.ToString('x2'))`t$($flameFlags.ToString('x2'))`t$flameCounter`t$($soundIds['SND_BOMB_LAND'].ToString('x2'))`t$($soundIds['SND_LIGHTTORCH'].ToString('x2'))`t$encodedEmberAnimation`tobject_code/common/items/seeds.s:itemCode20")
[IO.File]::WriteAllLines(
    (Join-Path $destination 'metadata\seed_satchel.tsv'),
    $seedRows,
    [Text.UTF8Encoding]::new($false))

# ITEM_SWORD_BEAM ($27) is created by a level-2 sword at the source health
# threshold and by the Energy Ring when charging completes. Preserve its four
# directional OAM compositions and native movement/collision constants.
$swordBeamData = [regex]::Match(
    $itemDataSource,
    '(?m)^\s*\.db\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s+\$(?<palette>[0-9a-f]{2})\s*;\s*\$27:\s*ITEM_SWORD_BEAM')
$swordBeamAttributes = [regex]::Match(
    $itemAttributesSource,
    '(?m)^\s*\.db\s+\$(?<collision>[0-9a-f]{2})\s+\$(?<radius>[0-9a-f]{2})\s+\$(?<damage>[0-9a-f]{2})\s+\$[0-9a-f]{2}\s*;\s*\$27:\s*ITEM_SWORD_BEAM')
$swordBeamOffsets = [regex]::Match(
    $swordBeamSource,
    '(?ms)^@initialOffsetsTable:\s*\.db \$(?<uy>[0-9a-f]{2}) \$(?<ux>[0-9a-f]{2}) \$00.*?\.db \$(?<ry>[0-9a-f]{2}) \$(?<rx>[0-9a-f]{2}) \$00.*?\.db \$(?<dy>[0-9a-f]{2}) \$(?<dx>[0-9a-f]{2}) \$00.*?\.db \$(?<ly>[0-9a-f]{2}) \$(?<lx>[0-9a-f]{2}) \$00')
$swordBeamOamPointers = [regex]::Match(
    $itemAnimationsSource,
    '(?ms)^item27OamDataPointers:[^\r\n]*\r?\n(?<body>(?:\s*\.dw\s+itemOamData[0-9a-f]+\s*\r?\n){4})')
if (-not $swordBeamData.Success -or -not $swordBeamAttributes.Success -or
    -not $swordBeamOffsets.Success -or -not $swordBeamOamPointers.Success -or
    $swordBeamSource -notmatch
        '(?ms)^@state0:.*?ld \(hl\),SPEED_300.*?ld a,SND_SWORDBEAM.*?^@state1:.*?call itemUpdateDamageToApply.*?call objectApplySpeed.*?call objectCheckTileCollision_allowHoles.*?call itemCheckCanPassSolidTile.*?and \$03.*?xor \$01.*?ldbc INTERAC_CLINK, \$81') {
    throw 'Could not verify ITEM_SWORD_BEAM data and native behavior.'
}
$swordBeamOamLabels = @(
    [regex]::Matches(
        $swordBeamOamPointers.Groups['body'].Value,
        '(?m)^\s*\.dw\s+(?<label>itemOamData[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
if ($swordBeamOamLabels.Count -ne 4) {
    throw "Expected four ITEM_SWORD_BEAM OAM pointers, parsed $($swordBeamOamLabels.Count)."
}
function Convert-SignedItemByte([string]$value) {
    $parsed = [Convert]::ToInt32($value, 16)
    if ($parsed -ge 0x80) { return $parsed - 0x100 }
    return $parsed
}
$swordBeamTileBase = [Convert]::ToInt32(
    $swordBeamData.Groups['tile'].Value, 16)
$swordBeamPalette = [Convert]::ToInt32(
    $swordBeamData.Groups['palette'].Value, 16) -band 7
$swordBeamRadius = [Convert]::ToInt32(
    $swordBeamAttributes.Groups['radius'].Value, 16)
$swordBeamDamage = -(Convert-SignedItemByte $swordBeamAttributes.Groups['damage'].Value)
if ($swordBeamDamage -le 0) {
    throw "Expected ITEM_SWORD_BEAM to have negative source damage, parsed $swordBeamDamage."
}
$swordBeamRows = [Collections.Generic.List[string]]::new()
$swordBeamRows.Add(
    "# direction`toffset-y`toffset-x`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage`tspeed-raw`tsound`toam")
$directionPrefixes = @('u', 'r', 'd', 'l')
for ($direction = 0; $direction -lt 4; $direction++) {
    $prefix = $directionPrefixes[$direction]
    $offsetY = Convert-SignedItemByte $swordBeamOffsets.Groups["${prefix}y"].Value
    $offsetX = Convert-SignedItemByte $swordBeamOffsets.Groups["${prefix}x"].Value
    $swordBeamRows.Add(
        "$direction`t$offsetY`t$offsetX`tspr_common_items`t$swordBeamTileBase`t$swordBeamPalette`t$(($swordBeamRadius -shr 4) -band 0x0f)`t$($swordBeamRadius -band 0x0f)`t$swordBeamDamage`t78`t$($soundIds['SND_SWORDBEAM'].ToString('x2'))`t$(Read-ItemOamComposition $swordBeamOamLabels[$direction])")
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'metadata\sword_beam.tsv'),
    $swordBeamRows,
    [Text.UTF8Encoding]::new($false))

# Preserve the common giveTreasure lookup data so the runtime can update
# inventory variables from original treasure IDs and parameters.
$behaviourRows = [Collections.Generic.List[string]]::new()
$behaviourRows.Add("# treasure-id`tvariable`tmode`tsound")
$behaviourSource = Get-Content (Join-Path $Disassembly "data\ages\treasureCollectionBehaviours.s")
$currentBehaviourTreasure = -1
$behaviourFields = @()
foreach ($line in $behaviourSource) {
    if ($line -match '^\s*;\s+TREASURE_[A-Z0-9_]+\s+\(0x[0-9a-f]{2}\)') {
        $currentBehaviourTreasure = $behaviourRows.Count - 1
        $behaviourFields = @()
        continue
    }
    if ($currentBehaviourTreasure -lt 0 -or
        $line -notmatch '^\s*\.db\s+(?<value>[^;]+)') {
        continue
    }

    $behaviourFields += $Matches['value'].Trim()
    if ($behaviourFields.Count -ne 3) { continue }

    $variable = $behaviourFields[0]
    if ($variable.StartsWith('<')) { $variable = $variable.Substring(1) }
    $mode = Convert-AsmByte $behaviourFields[1]
    if ($mode -lt 0) { throw "Could not parse treasure behaviour mode '$($behaviourFields[1])'." }
    $behaviourRows.Add("$($currentBehaviourTreasure.ToString('x2'))`t$variable`t$($mode.ToString('x2'))`t$($behaviourFields[2])")
    $currentBehaviourTreasure = -1
    $behaviourFields = @()
}
if ($behaviourRows.Count -ne 105) {
    throw "Expected 104 treasure collection behaviour rows, parsed $($behaviourRows.Count - 1)."
}
[IO.File]::WriteAllLines(
    (Join-Path $destination "metadata\treasure_behaviours.tsv"),
    $behaviourRows,
    [Text.UTF8Encoding]::new($false))

# Treasure objects encode the object subid found in chestData.s and the exact
# b/c values passed to giveTreasure.
$treasureObjectRows = [Collections.Generic.List[string]]::new()
$treasureObjectRows.Add("# treasure-object`ttreasure-id`tsubid`tparameter`ttext-id`tgraphic`tmessage-base64")
$treasureObjectRecords = @{}
$treasureObjectSource = Get-Content (Join-Path $Disassembly "data\ages\treasureObjectData.s")
$currentTreasure = -1
foreach ($line in $treasureObjectSource) {
    if ($line -match 'm_BeginTreasureSubids\s+(?<treasure>TREASURE_[A-Z0-9_]+)') {
        if (-not $treasureIds.ContainsKey($Matches['treasure'])) {
            throw "Unknown treasure constant $($Matches['treasure']) in treasureObjectData.s."
        }
        $currentTreasure = $treasureIds[$Matches['treasure']]
        continue
    }
    $commentTreasure = -1
    if ($line -match '/\*\s+\$(?<id>[0-9a-f]{2})\s+\*/') {
        $commentTreasure = [Convert]::ToInt32($Matches['id'], 16)
    }
    if ($line -notmatch 'm_TreasureSubid\s+(?<spawn>\$[0-9a-f]{2}),\s*(?<parameter>\$[0-9a-f]{2}),\s*(?<text><?[A-Za-z0-9_]+|\$[0-9a-f]{2}),\s*(?<graphic>\$[0-9a-f]{2}),\s*(?<label>TREASURE_OBJECT_[A-Z0-9_]+)') {
        continue
    }

    $treasure = if ($currentTreasure -ge 0) { $currentTreasure } else { $commentTreasure }
    if ($treasure -lt 0) { throw "Could not resolve treasure index for $($Matches['label'])." }
    $label = $Matches['label']
    $parameterText = $Matches['parameter']
    $textText = $Matches['text']
    $graphicText = $Matches['graphic']
    $subid = if ($label -match '_([0-9a-f]{2})$') { [Convert]::ToInt32($Matches[1], 16) } else { 0 }
    $parameter = Convert-AsmByte $parameterText
    $textId = Convert-AsmByte $textText
    $graphic = Convert-AsmByte $graphicText
    if ($parameter -lt 0 -or $graphic -lt 0) { throw "Could not parse $label treasure object data." }
    $message = if ($textId -ge 0 -and $textId -ne 0xff -and $allTexts.ContainsKey($textId)) {
        $allTexts[$textId]
    } else {
        ''
    }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message))
    $row = "$label`t$($treasure.ToString('x2'))`t$($subid.ToString('x2'))`t$($parameter.ToString('x2'))`t$($textId.ToString('x2'))`t$($graphic.ToString('x2'))`t$encoded"
    $treasureObjectRows.Add($row)
    $treasureObjectRecords[$label] = @{
        Treasure = $treasure
        Subid = $subid
        Parameter = $parameter
        TextId = $textId
        Graphic = $graphic
        Message = $message
    }
}
if (-not $treasureObjectRecords.ContainsKey('TREASURE_OBJECT_SWORD_00') -or
    $treasureObjectRecords['TREASURE_OBJECT_SWORD_00'].Treasure -ne $treasureIds['TREASURE_SWORD']) {
    throw "Could not resolve TREASURE_OBJECT_SWORD_00 to TREASURE_SWORD."
}
[IO.File]::WriteAllLines(
    (Join-Path $destination "metadata\treasure_objects.tsv"),
    $treasureObjectRows,
    [Text.UTF8Encoding]::new($false))

# Export the item icon rows used by loadTreasureDisplayData. Runtime code only
# consumes a subset today, but keeping all rows makes the inventory foundation
# data-driven for later menu/equipment slices.
$displayRows = [Collections.Generic.List[string]]::new()
$displayRows.Add("# table`tindex`ttreasure-id`tleft-sprite`tleft-palette`tright-sprite`tright-palette`textra-mode`ttext-low")
$displaySource = Get-Content (Join-Path $Disassembly "data\ages\treasureDisplayData.s")
$displayTable = ''
$displayIndex = 0
foreach ($line in $displaySource) {
    if ($line -match '^(treasureDisplayData_[A-Za-z0-9]+):') {
        $displayTable = $Matches[1]
        $displayIndex = 0
        continue
    }
    if (-not $displayTable -or $line -notmatch '^\s*\.db\s+(?<values>[^;]+)') {
        continue
    }
    $values = @($Matches['values'].Split(',') | ForEach-Object { $_.Trim() })
    if ($values.Count -ne 7) { continue }
    $treasure = Resolve-TreasureId $values[0] $treasureIds
    $leftSprite = Convert-AsmByte $values[1]
    $leftPalette = Convert-AsmByte $values[2]
    $rightSprite = Convert-AsmByte $values[3]
    $rightPalette = Convert-AsmByte $values[4]
    $extraMode = Convert-AsmByte $values[5]
    $textMatch = [regex]::Match($values[6], '<(?<name>TX_[A-Za-z0-9_]+)')
    if (-not $textMatch.Success -or
        -not $allTextIdsByName.ContainsKey($textMatch.Groups['name'].Value)) {
        throw "Could not resolve inventory text symbol '$($values[6])' in row '$line'."
    }
    $textId = $allTextIdsByName[$textMatch.Groups['name'].Value]
    if (($textId -band 0xff00) -ne 0x0900) {
        throw "Inventory display row '$line' resolved outside text group `$09."
    }
    $textLow = $textId -band 0xff
    if ($leftSprite -lt 0 -or $leftPalette -lt 0 -or $rightSprite -lt 0 -or
        $rightPalette -lt 0 -or $extraMode -lt 0) {
        throw "Could not parse treasure display row '$line'."
    }
    $displayRows.Add("$displayTable`t$displayIndex`t$($treasure.ToString('x2'))`t$($leftSprite.ToString('x2'))`t$($leftPalette.ToString('x2'))`t$($rightSprite.ToString('x2'))`t$($rightPalette.ToString('x2'))`t$($extraMode.ToString('x2'))`t$($textLow.ToString('x2'))")
    $displayIndex++
}
if (($displayRows | Where-Object { $_ -match '^treasureDisplayData_sword\t0\t05\t90\t' }).Count -ne 1) {
    throw "Could not export the level-1 sword display icon row."
}
$expectedShieldDisplayRows = @(
    "treasureDisplayData_shield`t0`t01`t93`t00`t00`t00`t00`t20"
    "treasureDisplayData_shield`t1`t01`t94`t05`t00`t00`t00`t21"
    "treasureDisplayData_shield`t2`t01`t95`t04`t00`t00`t00`t22"
)
foreach ($expectedRow in $expectedShieldDisplayRows) {
    if (-not $displayRows.Contains($expectedRow)) {
        throw "Could not export exact shield display row '$expectedRow'."
    }
}
[IO.File]::WriteAllLines(
    (Join-Path $destination "metadata\treasure_display.tsv"),
    $displayRows,
    [Text.UTF8Encoding]::new($false))

# showItemText2 reads normal inventory labels from TX_09XX. Ring slots set bit
# 7 and substitute TX_3040+ring and TX_3080+ring into TX_30c1; export that
# already-resolved pair while retaining both source IDs in the generated row.
$inventoryTextRows = [Collections.Generic.List[string]]::new()
$inventoryTextRows.Add('# kind`tindex`tname-text-id`tdescription-text-id`tmessage-base64')
foreach ($textId in @($allTexts.Keys | Sort-Object)) {
    if ($textId -lt 0x0900 -or $textId -ge 0x0a00) { continue }
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $inventoryTextRows.Add(
        "item`t$(($textId -band 0xff).ToString('x2'))`t$($textId.ToString('x4'))`tffff`t$encoded")
}
foreach ($ring in 0..0x3f) {
    $nameId = 0x3040 + $ring
    $descriptionId = 0x3080 + $ring
    if (-not $allTexts.ContainsKey($nameId) -or
        -not $allTexts.ContainsKey($descriptionId)) {
        throw "Could not resolve inventory ring text `$${ring}: TX_$($nameId.ToString('x4')) / TX_$($descriptionId.ToString('x4'))."
    }
    $message = "$($allTexts[$nameId])`n$($allTexts[$descriptionId])"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message))
    $inventoryTextRows.Add(
        "ring`t$($ring.ToString('x2'))`t$($nameId.ToString('x4'))`t$($descriptionId.ToString('x4'))`t$encoded")
}
if (($inventoryTextRows | Where-Object { $_ -match '^item\t23\t0923\t' }).Count -ne 1 -or
    ($inventoryTextRows | Where-Object { $_ -match '^ring\t00\t3040\t3080\t' }).Count -ne 1) {
    throw 'Could not export Wooden Sword and Friendship Ring inventory text records.'
}
[IO.File]::WriteAllLines(
    (Join-Path $destination "metadata\inventory_text.tsv"),
    $inventoryTextRows,
    [Text.UTF8Encoding]::new($false))

# Export the breakable tile tables used by tryToBreakTile. The source masks
# retain the disassembly's left-to-right bit order from breakableTileSources.s.
# Effect bit 7 calls updateRoomFlagsForBrokenTile, whose collision-indexed
# room-flag and Gasha-maturity tables are retained on each applicable row.
$breakableSource = Get-Content (Join-Path $Disassembly "data\ages\tile_properties\breakableTiles.s")
$breakableModes = @{}
foreach ($line in $breakableSource) {
    if ($line -match 'm_BreakableTileData\s+%(?<m0>[01]{8})\s+%(?<m1>[01]{8})\s+%(?<m2>[01]{4})\s+\$(?<drop>[0-9a-f])\s+\$(?<effect>[0-9a-f]{2})\s+\$(?<replacement>[0-9a-f]{2})\s*;\s*\$(?<index>[0-9a-f]{2})') {
        $bits = $Matches['m0'] + $Matches['m1'] + $Matches['m2']
        $mask = 0
        for ($i = 0; $i -lt $bits.Length; $i++) {
            if ($bits[$i] -eq '1') {
                $mask = $mask -bor (1 -shl $i)
            }
        }
        $breakableModes[[Convert]::ToInt32($Matches['index'], 16)] = @{
            SourceMask = $mask
            Drop = [Convert]::ToInt32($Matches['drop'], 16)
            Effect = [Convert]::ToInt32($Matches['effect'], 16)
            Replacement = [Convert]::ToInt32($Matches['replacement'], 16)
        }
    }
}

$breakableCollisionModes = @{
    overworld = 0
    indoors = 1
    dungeons = 2
    sidescrolling = 3
    underwater = 4
    five = 5
}

function Read-BreakableCollisionValueTable([string]$path) {
    $result = @{}
    $activeLabels = [Collections.Generic.List[string]]::new()
    foreach ($line in Get-Content $path) {
        if ($line -match '^\s*@(?<label>[A-Za-z0-9_]+):') {
            $label = $Matches['label']
            if ($breakableCollisionModes.ContainsKey($label)) {
                $activeLabels.Add($label)
            }
            continue
        }
        if ($activeLabels.Count -eq 0 -or
            $line -notmatch '^\s*\.db\s+\$(?<tile>[0-9a-f]{2})(?:\s+(?<value>\$[0-9a-f]{2}|[0-9]+))?') {
            continue
        }
        $tile = [Convert]::ToInt32($Matches['tile'], 16)
        if (-not $Matches.ContainsKey('value') -or $Matches['value'] -eq '') {
            if ($tile -ne 0) { throw "Unexpected collision-value terminator `$$($tile.ToString('x2'))." }
            $activeLabels.Clear()
            continue
        }
        $rawValue = $Matches['value']
        $value = if ($rawValue.StartsWith('$')) {
            [Convert]::ToInt32($rawValue.Substring(1), 16)
        } else {
            [int]$rawValue
        }
        foreach ($label in $activeLabels) {
            $key = $breakableCollisionModes[$label] * 256 + $tile
            if ($result.ContainsKey($key)) {
                throw "Duplicate collision-value row $label`:$$($tile.ToString('x2'))."
            }
            $result[$key] = $value
        }
    }
    return $result
}

$breakableRoomFlagActions = Read-BreakableCollisionValueTable (
    Join-Path $Disassembly 'data\ages\tile_properties\breakableTileRoomFlags.s')
$breakableGashaMaturity = Read-BreakableCollisionValueTable (
    Join-Path $Disassembly 'data\ages\tile_properties\breakableTileGashaMaturity.s')
$breakableRows = [Collections.Generic.List[string]]::new()
$breakableRows.Add("# active-collisions`ttile`tmode`tsource-mask`tdrop`teffect`treplacement`troom-flag-action`tgasha-maturity")
$activeLabels = [Collections.Generic.List[string]]::new()
foreach ($line in $breakableSource) {
    if ($line -match '^\s*@(?<label>[A-Za-z0-9_]+):') {
        $label = $Matches['label']
        if ($breakableCollisionModes.ContainsKey($label)) {
            $activeLabels.Add($label)
        }
        continue
    }
    if ($activeLabels.Count -eq 0 -or $line -notmatch '^\s*\.db\s+\$(?<tile>[0-9a-f]{2})(?:(?:\s*,)?\s+\$(?<mode>[0-9a-f]{2}))?') {
        continue
    }
    if (-not $Matches.ContainsKey('mode') -or $Matches['mode'] -eq '') {
        $activeLabels.Clear()
        continue
    }

    $tile = [Convert]::ToInt32($Matches['tile'], 16)
    $modeIndex = [Convert]::ToInt32($Matches['mode'], 16)
    if (-not $breakableModes.ContainsKey($modeIndex)) {
        throw "Breakable tile collision row referenced missing mode $($modeIndex.ToString('x2'))."
    }
    $mode = $breakableModes[$modeIndex]
    foreach ($label in $activeLabels) {
        $collisionMode = $breakableCollisionModes[$label]
        $key = $collisionMode * 256 + $tile
        $roomFlagAction = if ($breakableRoomFlagActions.ContainsKey($key)) {
            $breakableRoomFlagActions[$key]
        } else { 0xff }
        $gashaMaturity = if ($breakableGashaMaturity.ContainsKey($key)) {
            $breakableGashaMaturity[$key]
        } else { 0 }
        $breakableRows.Add("$collisionMode`t$($tile.ToString('x2'))`t$($modeIndex.ToString('x2'))`t$($mode.SourceMask.ToString('x5'))`t$($mode.Drop.ToString('x1'))`t$($mode.Effect.ToString('x2'))`t$($mode.Replacement.ToString('x2'))`t$($roomFlagAction.ToString('x2'))`t$gashaMaturity")
    }
}
if (($breakableRows | Where-Object { $_ -eq "2`t10`t1d`t00125`t2`t06`ta0`tff`t0" }).Count -ne 1 -or
    ($breakableRows | Where-Object { $_ -eq "0`tc6`t04`t6b1b7`t0`tc0`tdc`t07`t30" }).Count -ne 1 -or
    ($breakableRows | Where-Object { $_ -eq "0`tcb`t12`t00040`t0`tca`td2`t07`t50" }).Count -ne 1) {
    throw 'Could not export dungeon moving pot tile $10 as bracelet-breakable mode $1d.'
}
[IO.File]::WriteAllLines(
    (Join-Path $destination "metadata\breakable_tiles.tsv"),
    $breakableRows,
    [Text.UTF8Encoding]::new($false))

# Preserve checkTileValidForEnemySpawn's collision-mode-specific exceptions.
# The routine rejects every nonzero collision byte first, then consults this
# table for metatiles which remain forbidden despite having collision $00.
$enemyUnspawnableSource = Get-Content (
    Join-Path $Disassembly "data\ages\tile_properties\enemyUnspawnableTiles.s")
$enemyUnspawnableModes = @{
    overworld = 0
    indoors = 1
    dungeons = 2
    sidescrolling = 3
    underwater = 4
    five = 5
}
$enemyUnspawnableBytes = [byte[]]::new(6 * 256)
$enemyUnspawnableLabels = [Collections.Generic.List[string]]::new()
$enemyUnspawnableTileCount = 0
foreach ($line in $enemyUnspawnableSource) {
    if ($line -match '^\s*@(?<label>[A-Za-z0-9_]+):') {
        $label = $Matches['label']
        if ($enemyUnspawnableModes.ContainsKey($label)) {
            $enemyUnspawnableLabels.Add($label)
        }
        continue
    }
    if ($enemyUnspawnableLabels.Count -eq 0 -or
        $line -notmatch '^\s*\.db\s+\$(?<tile>[0-9a-f]{2})(?:\s+\$(?<value>[0-9a-f]{2}))?') {
        continue
    }

    $tile = [Convert]::ToInt32($Matches['tile'], 16)
    if (-not $Matches.ContainsKey('value') -or $Matches['value'] -eq '') {
        if ($tile -ne 0) {
            throw "Unexpected enemy-unspawnable terminator `$$($tile.ToString('x2'))."
        }
        $enemyUnspawnableLabels.Clear()
        continue
    }
    if ([Convert]::ToInt32($Matches['value'], 16) -ne 1) {
        throw "Enemy-unspawnable tile `$$($tile.ToString('x2')) did not retain value `$01."
    }

    foreach ($label in $enemyUnspawnableLabels) {
        $mode = $enemyUnspawnableModes[$label]
        $index = $mode * 256 + $tile
        if ($enemyUnspawnableBytes[$index] -ne 0) {
            throw "Duplicate enemy-unspawnable tile $label`:$$($tile.ToString('x2'))."
        }
        $enemyUnspawnableBytes[$index] = 1
        $enemyUnspawnableTileCount++
    }
}
if ($enemyUnspawnableTileCount -ne 63 -or
    $enemyUnspawnableBytes[0 * 256 + 0xe9] -ne 1 -or
    $enemyUnspawnableBytes[2 * 256 + 0x44] -ne 1 -or
    $enemyUnspawnableBytes[3 * 256 + 0xf3] -ne 0 -or
    $enemyUnspawnableBytes[4 * 256 + 0xfd] -ne 1) {
    throw "Expected 63 collision-mode enemy-unspawnable tile records, parsed $enemyUnspawnableTileCount."
}
[IO.File]::WriteAllBytes(
    (Join-Path $destination "metadata\enemyUnspawnableTiles.bin"),
    $enemyUnspawnableBytes)

# Chests are interactable $f1 metatiles whose room/position and treasure
# records live in chestData.s. Preserve every record with the resolved
# TREASURE_OBJECT_* b/c values that will be passed to giveTreasure.
$rupeeValues = @(
    0, 1, 2, 5, 10, 20, 40, 30, 60, 70,
    25, 50, 100, 200, 400, 150, 300, 500, 900, 80
)
$rupeeRewards = @{}
$treasureObjectSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\treasureObjectData.s")
foreach ($match in [regex]::Matches(
    $treasureObjectSource,
    'm_TreasureSubid\s+\$[0-9a-f]{2},\s*\$(?<parameter>[0-9a-f]{2}),\s*\$(?<text>[0-9a-f]{2}),\s*\$[0-9a-f]{2},\s*TREASURE_OBJECT_RUPEES_(?<subid>[0-9a-f]{2})'
)) {
    $parameter = [Convert]::ToInt32($match.Groups['parameter'].Value, 16)
    $textId = [Convert]::ToInt32($match.Groups['text'].Value, 16)
    if ($parameter -ge $rupeeValues.Count -or -not $allTexts.ContainsKey($textId)) { continue }
    $rupeeRewards[$match.Groups['subid'].Value] = @{
        Amount = $rupeeValues[$parameter]
        TextId = $textId
        Message = $allTexts[$textId]
    }
}

$chestRows = [Collections.Generic.List[string]]::new()
$chestRows.Add("# group`troom`tposition`ttreasure-object`ttreasure-id`tsubid`tparameter`ttext-id`tgraphic`tamount`tutf8-base64")
$currentChestGroup = -1
foreach ($line in Get-Content (Join-Path $Disassembly "data\ages\chestData.s")) {
    if ($line -match '^chestGroup(?<group>[0-7])Data:') {
        $currentChestGroup = [int]$Matches['group']
        continue
    }
    if ($currentChestGroup -lt 0 -or
        $line -notmatch '^\s*m_ChestData\s+\$(?<position>[0-9a-f]{2}),\s*\$(?<room>[0-9a-f]{2}),\s*(?<treasure>TREASURE_OBJECT_[A-Z0-9_]+)') {
        continue
    }

    $room = $Matches['room']
    $position = $Matches['position']
    $treasure = $Matches['treasure']
    if (-not $treasureObjectRecords.ContainsKey($treasure)) {
        throw "Chest $currentChestGroup`:$room/$position references unresolved $treasure."
    }
    $treasureRecord = $treasureObjectRecords[$treasure]
    $amount = 0
    if ($treasureRecord.Treasure -eq $treasureIds['TREASURE_RUPEES']) {
        if ($treasureRecord.Parameter -ge $rupeeValues.Count) {
            throw "$treasure uses unsupported rupee value index $($treasureRecord.Parameter)."
        }
        $amount = $rupeeValues[$treasureRecord.Parameter]
    }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($treasureRecord.Message))
    $chestRows.Add(
        "$currentChestGroup`t$room`t$position`t$treasure`t$($treasureRecord.Treasure.ToString('x2'))`t$($treasureRecord.Subid.ToString('x2'))`t$($treasureRecord.Parameter.ToString('x2'))`t$($treasureRecord.TextId.ToString('x2'))`t$($treasureRecord.Graphic.ToString('x2'))`t$amount`t$encoded")
}
if ($chestRows.Count -ne 134) {
    throw "Expected 133 chest records, parsed $($chestRows.Count - 1)."
}
$testChest = $chestRows | Where-Object { $_ -match '^0\t49\t51\tTREASURE_OBJECT_RUPEES_04\t28\t04\t07\t05\t2b\t30\t' } | Select-Object -First 1
if (-not $testChest) {
    throw "The canonical room 0:49/$51 chest no longer resolves to the 30-rupee TX_0005 reward."
}
$chestPath = Join-Path $destination "objects\chests.tsv"
[IO.File]::WriteAllLines($chestPath, $chestRows, [Text.UTF8Encoding]::new($false))
