# Preserve the overworld map's 14x14 region-text table, cursor popup table,
# tree icons, and every text bank the map resolver can select. Conditional
# popup behavior remains a runtime concern because it reads live room/global
# flags, but the table bytes and TX strings come directly from the disassembly.
$mapDataPath = Join-Path $Disassembly 'data\ages\mapTextAndPopups.s'

function Read-MinimapPopups([string]$label) {
    $result = @{}
    foreach ($node in Read-AssemblyDataDirectives `
        $mapDataPath $label '.db') {
        if ($node.Operands.Count -lt 2) { continue }
        $room = Convert-AssemblyInteger $node.Operands[0]
        if ($room -eq 0xff) { continue }
        # mapMenu_loadPopupData stops at the first matching room. A few source
        # tables intentionally repeat room IDs, so retain the first record.
        if (-not $result.ContainsKey($room)) {
            $result[$room] = Convert-AssemblyInteger $node.Operands[1]
        }
    }
    return $result
}

$presentMapTexts = @(Read-AssemblyLiteralValues `
    $mapDataPath 'presentMapTextIndices')
$pastMapTexts = @(Read-AssemblyLiteralValues `
    $mapDataPath 'pastMapTextIndices')
if ($presentMapTexts.Count -ne 196 -or $pastMapTexts.Count -ne 196) {
    throw "Expected 196 present and past map text indices, got $($presentMapTexts.Count) and $($pastMapTexts.Count)."
}
$presentMapPopups = Read-MinimapPopups 'presentMinimapPopups'
$pastMapPopups = Read-MinimapPopups 'pastMinimapPopups'
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
Write-GeneratedTable($mapMetadataPath, $mapRows)

$mapTextRows = [Collections.Generic.List[string]]::new()
$mapTextRows.Add('# text-id`tposition`tmessage-base64')
foreach ($textId in @($allTexts.Keys | Sort-Object)) {
    if ($textId -lt 0x0200 -or $textId -ge 0x0600) { continue }
    $position = if ($allTextPositions.ContainsKey($textId)) { $allTextPositions[$textId] } else { 0 }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $mapTextRows.Add("$($textId.ToString('x4'))`t$position`t$encoded")
}
$mapTextsPath = Join-Path $destination 'map\texts.tsv'
Write-GeneratedTable($mapTextsPath, $mapTextRows)

$treeWarpPath = Join-Path $Disassembly 'data\ages\treeWarps.s'
$treeWarpRows = [Collections.Generic.List[string]]::new()
$treeWarpRows.Add('# group`troom`tpopup')
foreach ($treeGroup in @(
    @{ Label = 'presentTreeWarps'; Group = 0 },
    @{ Label = 'pastTreeWarps'; Group = 1 })) {
    foreach ($node in Read-AssemblyDataDirectives `
        $treeWarpPath $treeGroup.Label '.db') {
        if ($node.Operands.Count -lt 3) { continue }
        $room = Convert-AssemblyInteger $node.Operands[0]
        if ($room -eq 0) { continue }
        $popup = Convert-AssemblyInteger $node.Operands[2]
        $treeWarpRows.Add(
            "$($treeGroup.Group)`t$($room.ToString('x2'))`t$($popup.ToString('x2'))")
    }
}
$treeWarpsPath = Join-Path $destination 'map\tree_warps.tsv'
if ($treeWarpRows.Count -ne 11) {
    throw "Expected 10 nonzero Ages tree-warp popup records, parsed $($treeWarpRows.Count - 1)."
}
Write-GeneratedTable($treeWarpsPath, $treeWarpRows)

$mapMenuPath = Join-Path $Disassembly 'code\bank2.s'
$entranceRows = [Collections.Generic.List[string]]::new()
$entranceRows.Add('# dungeon`tgroup`troom`tfallback-text')
$dungeonIndex = 0
foreach ($node in Read-AssemblyDataDirectives `
    $mapMenuPath 'mapMenu_dungeonEntranceText' '.db') {
    if ($node.Operands.Count -ne 2 -or
        $node.Operands[1] -notmatch
            '^(?<group>\$80\|)?\(<TX_03(?<text>[0-9a-f]{2})\)$') {
        continue
    }
    $group = if ($Matches['group']) { 4 } else { 5 }
    $room = Convert-AssemblyInteger $node.Operands[0]
    $entranceRows.Add(
        "$dungeonIndex`t$group`t$($room.ToString('x2'))`t$($Matches['text'])")
    $dungeonIndex++
}
if ($dungeonIndex -ne 16) { throw "Expected 16 Ages dungeon entrance rows, parsed $dungeonIndex." }
$entrancePath = Join-Path $destination 'map\dungeon_entrances.tsv'
Write-GeneratedTable($entrancePath, $entranceRows)

function Read-ConstantIds([string]$path, [string]$prefix) {
    $ids = @{}
    foreach ($node in Read-AssemblyMacroInvocations $path) {
        if ($node.Name -match "^${prefix}[A-Z0-9_]+$" -and
            $node.Operands.Count -eq 1 -and
            $node.Operands[0] -ieq 'db' -and
            $node.Comment -match '^\s*(?:0x|\$)(?<id>[0-9a-f]{2})') {
            $ids[$node.Name] = [Convert]::ToInt32($Matches['id'], 16)
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
$itemDataSource = Read-ImportText (Join-Path $Disassembly 'data\ages\itemData.s')
$itemAttributesSource = Read-ImportText (Join-Path $Disassembly 'data\ages\itemAttributes.s')
$itemAnimationsSource = Read-ImportText (Join-Path $Disassembly 'data\itemAnimations.s')
$itemOamDataSource = Read-ImportText (Join-Path $Disassembly 'data\itemOamData.s')
$itemUsageSource = Read-ImportText (Join-Path $Disassembly 'data\ages\itemUsageTables.s')
$specialObjectAnimationsSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')
$specialObjectAnimationLogicSource = Read-ImportText (
    Join-Path $Disassembly 'code\specialObjectAnimationsAndDamage.s')
$objectGfxHeadersSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\objectGfxHeaders.s')
$gfxHeadersSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\gfxHeaders.s')
$seedCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\seeds.s')
$seedParentSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\seedsParent.s')
$bombCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\bombs.s')
$conveyorItemSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tile_properties\conveyorItemTiles.s')
$swordBeamSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\swordBeam.s')
$shieldParentSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\shieldParent.s')
$shovelParentSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\shovelParent.s')
$swordParentSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\swordParent.s')
$swordItemSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\sword.s')
$itemPostUpdateSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\postUpdate.s')
$itemCommonCode2Source = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\commonCode2.s')
$itemCommonCode1Source = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\commonCode1.s')
$clinkSoundSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tile_properties\clinkSounds.s')
$specialObjectOamDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\specialObjectOamData.s')
$braceletParentSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\bombsBraceletParent.s')
$braceletItemSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\bracelet.s')
$braceletThrowSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\commonBombAndBraceletCode.s')
$pushBlockSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\pushblock.s')
$objectSpeedsSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$parentItemCommonSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\commonCode.s')
$collisionEffectsSource = Read-ImportText (
    Join-Path $Disassembly 'code\collisionEffects.s')
$objectCollisionTableSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\objectCollisionTable.s')
$partDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\partData.s')
$partActiveCollisionsSource = Read-ImportText (
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
$expectedMinecartLinkGfx = @{
    0x58 = @(0x04, 0x0800); 0x59 = @(0x01, 0x0840)
    0x5a = @(0x04, 0x0820); 0x5b = @(0x00, 0x0840)
    0x84 = @(0x04, 0x0800); 0x85 = @(0x01, 0x0840)
    0x86 = @(0x04, 0x0820); 0x87 = @(0x00, 0x0840)
}
$expectedMinecartAttackLinkGfx = @{
    0xc8 = @(0x00, 0x1200); 0xc9 = @(0x01, 0x1300)
    0xca = @(0x00, 0x1280); 0xcb = @(0x00, 0x1300)
    0xcc = @(0x00, 0x1240); 0xcd = @(0x01, 0x1340)
    0xce = @(0x00, 0x12c0); 0xcf = @(0x00, 0x1340)
}
$minecartAttackLinkGfxValid = $linkGfxEntries.Count -gt 0xcf
if ($minecartAttackLinkGfxValid) {
    foreach ($index in $expectedMinecartAttackLinkGfx.Keys) {
        $entry = $linkGfxEntries[$index]
        $expected = $expectedMinecartAttackLinkGfx[$index]
        if ([Convert]::ToInt32($entry.Groups['oam'].Value, 16) -ne
                $expected[0] -or
            [Convert]::ToInt32($entry.Groups['offset'].Value, 16) -ne
                $expected[1]) {
            $minecartAttackLinkGfxValid = $false
            break
        }
    }
}
$minecartLinkGfxValid = $linkGfxEntries.Count -gt 0x87
if ($minecartLinkGfxValid) {
    foreach ($index in $expectedMinecartLinkGfx.Keys) {
        $entry = $linkGfxEntries[$index]
        $expected = $expectedMinecartLinkGfx[$index]
        if ([Convert]::ToInt32($entry.Groups['oam'].Value, 16) -ne
                $expected[0] -or
            [Convert]::ToInt32($entry.Groups['offset'].Value, 16) -ne
                $expected[1]) {
            $minecartLinkGfxValid = $false
            break
        }
    }
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
    -not $minecartLinkGfxValid -or
    -not $minecartAttackLinkGfxValid -or
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
    $specialObjectAnimationLogicSource -notmatch
        '(?ms)Check if he.s riding a minecart.*?cp \$0a.*?inc c.*?Done if holding something or riding a minecart' -or
    $parentItemCommonSource -notmatch
        '(?ms)Check if Link is riding something.*?cp LINK_ANIM_MODE_20.*?cp LINK_ANIM_MODE_24.*?add \$04' -or
    $specialObjectAnimationsSource -notmatch
        '(?ms)^animationData1a019:\s*\.db \$03 \$c8 \$00\s*^animationData1a01c:\s*\.db \$03 \$cc \$02\s*\.db \$08 \$cc \$26\s*\.db \$7f \$58 \$86' -or
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
    throw 'ITEM_SHIELD or ridden Link animation data, graphics, hitbox, sounds, or supported projectile collisions changed in the disassembly.'
}

# ITEM_BRACELET ($16) is a held-input parent item. It first grabs the wall in
# front of Link, waits for the opposite direction, lifts a metatile into an
# ITEM_BRACELET child, and later releases that child through the common
# weight-0 throwing path. Export the native constants as one typed record.
$braceletAttributes = [regex]::Match(
    $itemAttributesSource,
    '(?m)^\s*\.db\s+\$(?<collision>[0-9a-f]{2})\s+\$(?<radius>[0-9a-f]{2})\s+\$(?<damage>[0-9a-f]{2})\s+\$[0-9a-f]{2}\s*;\s*\$16:\s*ITEM_BRACELET')
$braceletWeight = [regex]::Match(
    $braceletThrowSource,
    '(?ms)^itemWeights:\s*\.db\s+\$(?<gravity>[0-9a-f]{2})\s+\$(?<speedz>[0-9a-f]{2})\s+SPEED_180\s+SPEED_280')
$expectedBraceletLinkGfx = @{
    0x5c = @(0x00, 0x0040); 0x5d = @(0x01, 0x01c0)
    0x5e = @(0x00, 0x0180); 0x5f = @(0x00, 0x01c0)
    0xb0 = @(0x00, 0x1040); 0xb1 = @(0x01, 0x02c0)
    0xb2 = @(0x00, 0x10c0); 0xb3 = @(0x00, 0x02c0)
    0xdc = @(0x00, 0x0a40); 0xdd = @(0x01, 0x0b80)
    0xde = @(0x04, 0x0ac0); 0xdf = @(0x00, 0x0b80)
    0xe0 = @(0x0c, 0x0a80); 0xe1 = @(0x0d, 0x0bc0)
    0xe2 = @(0x0e, 0x0ae0); 0xe3 = @(0x0f, 0x0bc0)
}
$braceletLinkGfxValid = $linkGfxEntries.Count -gt 0xe3
if ($braceletLinkGfxValid) {
    foreach ($index in $expectedBraceletLinkGfx.Keys) {
        $entry = $linkGfxEntries[$index]
        $expected = $expectedBraceletLinkGfx[$index]
        if ([Convert]::ToInt32($entry.Groups['oam'].Value, 16) -ne $expected[0] -or
            [Convert]::ToInt32($entry.Groups['offset'].Value, 16) -ne $expected[1]) {
            $braceletLinkGfxValid = $false
            break
        }
    }
}
if (-not $braceletAttributes.Success -or -not $braceletWeight.Success -or
    [Convert]::ToInt32($braceletAttributes.Groups['collision'].Value, 16) -ne 0x16 -or
    [Convert]::ToInt32($braceletAttributes.Groups['radius'].Value, 16) -ne 0x00 -or
    [Convert]::ToInt32($braceletAttributes.Groups['damage'].Value, 16) -ne 0xfd -or
    $itemIds['ITEM_BRACELET'] -ne 0x16 -or
    $treasureIds['TREASURE_BRACELET'] -ne 0x16 -or
    $soundIds['SND_PICKUP'] -ne 0x9c -or
    $soundIds['SND_THROW'] -ne 0x51 -or
    -not $braceletLinkGfxValid -or
    $itemUsageSource -notmatch
        '(?m)^\s*\.db\s+\$13,\s*<wGameKeysPressed\s*;\s*ITEM_BRACELET' -or
    $itemUsageSource -notmatch
        '(?m)^\s*\.db\s+\$40,\s*LINK_ANIM_MODE_LIFT_3\s*;\s*ITEM_BRACELET' -or
    $braceletParentSource -notmatch
        '(?ms)^parentItemCode_bracelet:.*?^@state0:.*?call checkLinkOnGround.*?call @checkWallInFrontOfLink.*?^@state1:.*?@counterDirections.*?lda BREAKABLETILESOURCE_BRACELET.*?call tryToBreakTile.*?SND_PICKUP.*?^@state2:.*?^@state3:.*?SND_THROW.*?^@state4:' -or
    $braceletParentSource -notmatch
        '(?ms)^@@throwItem:\s*ld a,\(wLinkAngle\)\s*rlca\s*jr c,\+\s*ld a,\(w1Link\.direction\)\s*swap a\s*rrca\s*\+\s*ld l,Item\.angle\s*ld \(hl\),a' -or
    $braceletParentSource -notmatch
        '(?ms)^@checkWallInFrontOfLink:.*?w1Link\.adjacentWallsBitset.*?^@@data:\s*\.db \$c0 \$fb \$00 ; DIR_UP\s*\.db \$03 \$00 \$07 ; DIR_RIGHT\s*\.db \$30 \$07 \$00 ; DIR_DOWN\s*\.db \$0c \$00 \$f8 ; DIR_LEFT' -or
    $braceletItemSource -notmatch
        '(?ms)^itemCode16:.*?call itemMimicBgTile.*?ld a,\$06\s*ldd \(hl\),a\s*ldd \(hl\),a.*?call itemBeginThrow.*?call itemUpdateThrowingLaterally.*?call itemUpdateThrowingVertically.*?itemMakeInteractionForBreakableTile' -or
    $braceletThrowSource -notmatch
        '(?ms)^itemBeginThrow:.*?ld a,\(w1Link\.direction\).*?If angle is \$ff \(motionless\), skip the rest\..*?ld e,Item\.angle\s*ld a,\(de\)\s*rlca\s*jr c,@clearItemSpeed.*?^@clearItemSpeed:.*?ld l,Item\.speed\s*xor a\s*ld \(hl\),a\s*ld l,Item\.speedZ\s*ldi \(hl\),a\s*ldi \(hl\),a' -or
    $parentItemCommonSource -notmatch
        '(?ms)^@liftedObjectPositions:.*?Weight 0\s*\.db \$f8 \$00 \$00 \$07 \$06 \$00 \$00 \$f8.*?\.db \$fa \$00 \$f8 \$03 \$04 \$00 \$f8 \$fc.*?\.db \$f3 \$00 \$f2 \$00 \$f3 \$00 \$f2 \$00.*?\.db \$f3 \$00 \$f3 \$00 \$f3 \$00 \$f3 \$00' -or
    $specialObjectAnimationsSource -notmatch
        '(?ms)^animationData19f47:\s*\.db \$01 \$dc \$00.*?\.db \$0a \$e0 \$00\s*\.db \$6e \$e0 \$ff.*?^animationData19f5b:\s*\.db \$03 \$e0 \$00\s*^animationData19f5e:\s*\.db \$04 \$e0 \$00\s*\.db \$04 \$dc \$04\s*\.db \$02 \$5c \$08\s*\.db \$7f \$5c \$ff.*?^animationData19f6a:\s*\.db \$08 \$b0 \$04\s*\.db \$7f \$b0 \$ff' -or
    $objectCollisionTableSource -notmatch
        '(?ms)ENEMYCOLLISION_STANDARD_ENEMY \(0x10\)\s*\.db [^\r\n]+\s*\.db \$00 \$00 \$00 \$22 \$0d \$2f \$09' -or
    $collisionEffectsSource -notmatch
        '(?ms)^collisionEffect09:\s*ld e,ENEMYDMG_04\s*j[rp] label_07_027.*?^label_07_027:.*?jp applyDamageToEnemyOrPart' -or
    $collisionEffectsSource -notmatch
        '(?ms)ld bc,\$0e07.*?cp ITEMCOLLISION_BOMB.*?ld l,Item\.zh.*?sub \(hl\).*?add c.*?cp b.*?jr nc,@nextItem.*?ld l,Item\.yh.*?ld b,\(hl\).*?ld l,Item\.xh' -or
    $pushBlockSource -notmatch
        '(?ms)Determine speed to push with.*?ldbc SPEED_80, \$20.*?wBraceletLevel.*?cp \$02.*?bit 5,\(hl\).*?ldbc SPEED_c0, \$15' -or
    $objectSpeedsSource -notmatch
        '(?m)^\s*SPEED_80\s+dsb 5 ; 0x14$' -or
    $objectSpeedsSource -notmatch
        '(?m)^\s*SPEED_c0\s+dsb 5 ; 0x1e$') {
    throw 'ITEM_BRACELET usage, Link graphics, lift offsets, sounds, or weight-0 throwing behavior changed in the disassembly.'
}
$braceletDamageRaw = [Convert]::ToInt32(
    $braceletAttributes.Groups['damage'].Value, 16)
$braceletDamage = 0x100 - $braceletDamageRaw
$braceletGravity = [Convert]::ToInt32(
    $braceletWeight.Groups['gravity'].Value, 16)
$braceletSpeedZLow = [Convert]::ToInt32(
    $braceletWeight.Groups['speedz'].Value, 16)
$braceletInitialSpeedZ = 0xff00 + $braceletSpeedZLow - 0x10000
$braceletRows = [Collections.Generic.List[string]]::new()
$braceletRows.Add(
    '# item`tpickup-sound`tthrow-sound`tdamage`tradius-y`tradius-x`tcollision-z-radius`tgravity`tinitial-speed-z`tspeed-raw`ttoss-speed-raw`tpush-speed-raw`tpush-frames`tpower-glove-push-speed-raw`tpower-glove-push-frames`theavy-property-mask`tgrab-pull-frames`tlift-low-frames`tlift-mid-frames`tlift-high-frames`tthrow-frames`tsource')
$braceletRows.Add(
    "$($itemIds['ITEM_BRACELET'].ToString('x2'))`t$($soundIds['SND_PICKUP'].ToString('x2'))`t$($soundIds['SND_THROW'].ToString('x2'))`t$braceletDamage`t6`t6`t7`t$braceletGravity`t$braceletInitialSpeedZ`t3c`t64`t14`t32`t1e`t21`t20`t11`t7`t4`t2`t8`tobject_code/common/itemParents/bombsBraceletParent.s:parentItemCode_bracelet")
Write-GeneratedTable(
    (Join-Path $destination 'metadata\bracelet.tsv'),
    $braceletRows)

$emberData = [regex]::Match(
    $itemDataSource,
    '(?m)^\s*\.db\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s+\$(?<palette>[0-9a-f]{2})\s*;\s*\$20:\s*ITEM_EMBER_SEED')
$emberAttributes = [regex]::Match(
    $itemAttributesSource,
    '(?m)^\s*\.db\s+\$(?<collision>[0-9a-f]{2})\s+\$(?<radius>[0-9a-f]{2})\s+\$(?<damage>[0-9a-f]{2})\s+\$[0-9a-f]{2}\s*;\s*\$20:\s*ITEM_EMBER_SEED')
$mysteryData = [regex]::Match(
    $itemDataSource,
    '(?m)^\s*\.db\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s+\$(?<palette>[0-9a-f]{2})\s*;\s*\$24:\s*ITEM_MYSTERY_SEED')
$mysteryAttributes = [regex]::Match(
    $itemAttributesSource,
    '(?m)^\s*\.db\s+\$(?<collision>[0-9a-f]{2})\s+\$(?<radius>[0-9a-f]{2})\s+\$(?<damage>[0-9a-f]{2})\s+\$[0-9a-f]{2}\s*;\s*\$24:\s*ITEM_MYSTERY_SEED')
if (-not $emberData.Success -or -not $emberAttributes.Success -or
    -not $mysteryData.Success -or -not $mysteryAttributes.Success) {
    throw 'Could not parse ITEM_EMBER_SEED / ITEM_MYSTERY_SEED item data/attributes.'
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
$mysteryGfxIndex = [Convert]::ToInt32(
    $mysteryData.Groups['gfx'].Value, 16)
if ($mysteryGfxIndex -ne $gfxIndex) {
    throw 'ITEM_MYSTERY_SEED no longer shares ITEM_EMBER_SEED object GFX header $78.'
}

$emberFlameData = [regex]::Match(
    $seedCodeSource,
    '(?m)^@data:\s*\r?\n\s*\.db\s+\$(?<flags>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s+\$(?<counter>[0-9a-f]{2})\s+SND_LIGHTTORCH')
if (-not $emberFlameData.Success) {
    throw 'Could not parse ITEM_EMBER_SEED ignition graphics data.'
}
$mysteryEffectData = [regex]::Match(
    $seedCodeSource,
    '(?m)^\s*\.db\s+\$(?<flags>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s+\$(?<counter>[0-9a-f]{2})\s+SND_MYSTERY_SEED')
if (-not $mysteryEffectData.Success) {
    throw 'Could not parse ITEM_MYSTERY_SEED effect graphics data.'
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
    @{ Source = $seedCodeSource; Pattern = '(?ms)emberSeedBurn:.*?dec \(hl\).*?ld a,BREAKABLETILESOURCE_EMBER_SEED\s*call itemTryToBreakTile'; Name = 'Ember burn counter and break source' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)If it''s a mystery seed, get a random effect.*?call getRandomNumber_noPreserveVars\s*and \$03.*?add \$80\|ITEMCOLLISION_EMBER_SEED'; Name = 'Mystery Seed random-effect selection' },
    @{ Source = $seedCodeSource; Pattern = '(?ms)@mysteryStandard:.*?call objectSetVisible82.*?\.db \$08 \$18 \$00 SND_MYSTERY_SEED'; Name = 'Mystery Seed activation data' })
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

# ITEM_BOMB ($03) shares Bracelet's weight-0 lift/throw parent, but remains an
# independently updating item actor after allocation. Preserve both native
# animations, the explosion probes, and the motion tables in one typed record.
$bombData = [regex]::Match(
    $itemDataSource,
    '(?m)^\s*\.db\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s+\$(?<palette>[0-9a-f]{2})\s*;\s*\$03:\s*ITEM_BOMB')
$bombAttributes = [regex]::Match(
    $itemAttributesSource,
    '(?m)^\s*\.db\s+\$(?<collision>[0-9a-f]{2})\s+\$(?<radius>[0-9a-f]{2})\s+\$(?<damage>[0-9a-f]{2})\s+\$[0-9a-f]{2}\s*;\s*\$03:\s*ITEM_BOMB')
$bombOamPointers = [regex]::Match(
    $itemAnimationsSource,
    '(?ms)^item03OamDataPointers:.*?\r?\n(?<body>(?:\s*\.dw\s+itemOamData[0-9a-f]+\s*\r?\n){7})')
$bombFuseBlock = [regex]::Match(
    $itemAnimationsSource,
    '(?ms)^itemAnimation1e777:\s*(?<body>.*?)(?=^itemAnimation1e798:)')
$bombExplosionBlock = [regex]::Match(
    $itemAnimationsSource,
    '(?ms)^itemAnimation1e798:\s*(?<body>.*?)(?=^itemAnimation1e7ad:)')
if (-not $bombData.Success -or -not $bombAttributes.Success -or
    -not $bombOamPointers.Success -or -not $bombFuseBlock.Success -or
    -not $bombExplosionBlock.Success) {
    throw 'Could not parse ITEM_BOMB item data, attributes, animations, or OAM pointers.'
}
$bombOamLabels = @([regex]::Matches(
    $bombOamPointers.Groups['body'].Value,
    '(?m)^\s*\.dw\s+(?<label>itemOamData[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if ($bombOamLabels.Count -ne 7) {
    throw "Expected seven ITEM_BOMB OAM pointers, parsed $($bombOamLabels.Count)."
}

function Convert-ItemAnimationBlock(
    [string]$body,
    [string[]]$oamLabels,
    [string]$name) {
    $rows = @([regex]::Matches(
        $body,
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<oam>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})'))
    if ($rows.Count -eq 0) {
        throw "Could not parse $name animation rows."
    }
    $parts = [Collections.Generic.List[string]]::new()
    foreach ($row in $rows) {
        $duration = [Convert]::ToInt32(
            $row.Groups['duration'].Value, 16)
        $oamOffset = [Convert]::ToInt32(
            $row.Groups['oam'].Value, 16)
        $parameter = [Convert]::ToInt32(
            $row.Groups['parameter'].Value, 16)
        if (($oamOffset -band 1) -ne 0 -or
            ($oamOffset / 2) -ge $oamLabels.Count) {
            throw "$name referenced invalid OAM pointer offset `$$($oamOffset.ToString('x2'))."
        }
        $parts.Add(
            "$duration,$parameter@$(Read-ItemOamComposition $oamLabels[$oamOffset / 2])")
    }
    return $parts -join '|'
}

$encodedBombFuse = Convert-ItemAnimationBlock `
    -body $bombFuseBlock.Groups['body'].Value `
    -oamLabels $bombOamLabels `
    -name 'ITEM_BOMB fuse'
$encodedBombExplosion = Convert-ItemAnimationBlock `
    -body $bombExplosionBlock.Groups['body'].Value `
    -oamLabels $bombOamLabels `
    -name 'ITEM_BOMB explosion'
$bombRadius = [Convert]::ToInt32(
    $bombAttributes.Groups['radius'].Value, 16)
$bombDamageRaw = [Convert]::ToInt32(
    $bombAttributes.Groups['damage'].Value, 16)
$bombDamage = 0x100 - $bombDamageRaw
$bombGfxIndex = [Convert]::ToInt32(
    $bombData.Groups['gfx'].Value, 16)

$bombSourceValid =
    $itemIds['ITEM_BOMB'] -eq 0x03 -and
    $treasureIds['TREASURE_BOMBS'] -eq 0x03 -and
    $bombGfxIndex -eq 0x78 -and
    $objectGfxHeadersSource -match
        '(?m)^\s*/\*\s*\$78\s*\*/\s*m_ObjectGfxHeader\s+spr_common_items' -and
    $bombData.Groups['tile'].Value -eq '10' -and
    $bombData.Groups['palette'].Value -eq '04' -and
    $bombAttributes.Groups['collision'].Value -eq '18' -and
    $bombRadius -eq 0x44 -and
    $bombDamageRaw -eq 0xfc -and
    $itemUsageSource -match
        '(?m)^\s*\.db\s+\$23,\s*<wGameKeysJustPressed\s*;\s*ITEM_BOMB' -and
    $itemUsageSource -match
        '(?m)^\s*\.db\s+\$30,\s*LINK_ANIM_MODE_LIFT\s*;\s*ITEM_BOMB' -and
    $braceletParentSource -match
        '(?ms)^parentItemCode_bomb:.*?call tryPickupBombs.*?wNumBombs.*?ld e,\$01.*?BOMBERS_RING.*?inc e.*?call itemCreateChild.*?call makeLinkPickupObjectH.*?parentItemCode_bracelet@beginPickup' -and
    $bombCodeSource -match
        '(?ms)^@heldState1:.*?PEACE_RING.*?bombResetAnimationAndSetVisiblec1.*?call bombUpdateAnimation.*?jp dropLinkHeldItem' -and
    $bombCodeSource -match
        '(?ms)^itemInitializeBombExplosion:.*?ld a,\$0a.*?ld \(hl\),\$0c.*?BLAST_RING.*?dec \(hl\).*?dec \(hl\).*?ld \(hl\),\$08.*?SND_EXPLOSION' -and
    $bombCodeSource -match
        '(?ms)^@data:\s*\.db \$f8 \$f3 \$f3\s*\.db \$f8 \$0c \$f3\s*\.db \$f8 \$0c \$0c\s*\.db \$f8 \$f3 \$0c\s*\.db \$f4 \$00 \$f3\s*\.db \$f4 \$0c \$00\s*\.db \$f4 \$00 \$0c\s*\.db \$f4 \$f3 \$00\s*\.db \$f2 \$00 \$00' -and
    $itemCommonCode1Source -match
        '(?ms)^bombEdgeOffsets:\s*\.db \$fd \$00.*?\.db \$00 \$03.*?\.db \$07 \$00.*?\.db \$00 \$fd' -and
    $braceletThrowSource -match
        '(?ms)^itemWeights:\s*\.db \$1c \$10 SPEED_180 SPEED_280' -and
    $braceletThrowSource -match
        '(?ms)^bounceSpeedReductionMapping:.*?SPEED_020 SPEED_000.*?SPEED_180 SPEED_0c0.*?SPEED_280 SPEED_140.*?\.db \$00 \$00' -and
    $conveyorItemSource -match
        '(?ms)^itemConveyorTilesTable:.*?@dungeons:.*?TILEINDEX_CONVEYOR_UP,\s+ANGLE_UP.*?TILEINDEX_CONVEYOR_RIGHT,\s+ANGLE_RIGHT.*?TILEINDEX_CONVEYOR_DOWN,\s+ANGLE_DOWN.*?TILEINDEX_CONVEYOR_LEFT,\s+ANGLE_LEFT'
if (-not $bombSourceValid) {
    throw 'ITEM_BOMB allocation, graphics, fuse, explosion, or weight-0 motion behavior changed in the supported disassembly.'
}

$bombRows = [Collections.Generic.List[string]]::new()
$bombRows.Add(
    '# item`ttreasure-id`tsprite`ttile-base`tpalette`tcollision`tradius-y`tradius-x`tbase-damage`texplosion-sprite`texplosion-tile-base`texplosion-oam-flags`tpickup-sound`tthrow-sound`tlanding-sound`texplosion-sound`tgravity`tinitial-speed-z`tspeed-raw`ttoss-speed-raw`tconveyor-speed-raw`tlift-low-frames`tlift-mid-frames`tlift-high-frames`tthrow-frames`tedge-offsets`tbounce-speeds`tbreak-probes`tfuse-animation`texplosion-animation`tsource')
$bombRows.Add(
    "$($itemIds['ITEM_BOMB'].ToString('x2'))`t$($treasureIds['TREASURE_BOMBS'].ToString('x2'))`tspr_common_items`t$($bombData.Groups['tile'].Value)`t$($bombData.Groups['palette'].Value)`t$($bombAttributes.Groups['collision'].Value)`t$(($bombRadius -shr 4) -band 0x0f)`t$($bombRadius -band 0x0f)`t$bombDamage`tspr_common_sprites`t0c`t0a`t$($soundIds['SND_PICKUP'].ToString('x2'))`t$($soundIds['SND_THROW'].ToString('x2'))`t$($soundIds['SND_BOMB_LAND'].ToString('x2'))`t$($soundIds['SND_EXPLOSION'].ToString('x2'))`t28`t-240`t3c`t64`t14`t7`t4`t2`t8`t-3,0;0,3;7,0;0,-3`t0:0;5:0;10:5;15:5;20:10;25:10;30:15;35:15;40:20;45:20;50:25;55:25;60:30;65:30;70:35;75:35;80:40;85:40;90:45;95:45;100:50;105:50;110:55;115:55;120:60`t-8,-13,-13;-8,12,-13;-8,12,12;-8,-13,12;-12,0,-13;-12,12,0;-12,0,12;-12,-13,0;-14,0,0`t$encodedBombFuse`t$encodedBombExplosion`tobject_code/common/items/bombs.s:itemCode03")
Write-GeneratedTable(
    (Join-Path $destination 'metadata\bomb.tsv'),
    $bombRows)

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

$mysteryAnimation = [regex]::Match(
    $itemAnimationsSource,
    '(?ms)^itemAnimation1e829:\s*\.db \$(?<d0>[0-9a-f]{2}) \$(?<t0>[0-9a-f]{2}) \$(?<p0>[0-9a-f]{2})\s*\.db \$(?<d1>[0-9a-f]{2}) \$(?<t1>[0-9a-f]{2}) \$(?<p1>[0-9a-f]{2})\s*\.db \$(?<d2>[0-9a-f]{2}) \$(?<t2>[0-9a-f]{2}) \$(?<p2>[0-9a-f]{2})\s*\.db \$(?<d3>[0-9a-f]{2}) \$(?<t3>[0-9a-f]{2}) \$(?<p3>[0-9a-f]{2})\s*\.db \$(?<d4>[0-9a-f]{2}) \$(?<t4>[0-9a-f]{2}) \$(?<p4>[0-9a-f]{2})')
$mysteryOamPointers = [regex]::Match(
    $itemAnimationsSource,
    '(?ms)^item22OamDataPointers:(?<aliases>.*?)^item26OamDataPointers:\s*(?<body>.*?)^item23OamDataPointers:')
if (-not $mysteryAnimation.Success -or
    -not $mysteryOamPointers.Success -or
    -not $mysteryOamPointers.Groups['aliases'].Value.Contains(
        'item24OamDataPointers:')) {
    throw 'Could not parse ITEM_MYSTERY_SEED animation 0 / shared OAM pointers.'
}
$mysteryOamLabels = @([regex]::Matches(
    $mysteryOamPointers.Groups['body'].Value,
    '(?m)^\s*\.dw\s+(?<label>itemOamData[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if ($mysteryOamLabels.Count -ne 4) {
    throw "Expected four ITEM_MYSTERY_SEED OAM pointers, parsed $($mysteryOamLabels.Count)."
}
$mysteryAnimationParts = [Collections.Generic.List[string]]::new()
for ($index = 0; $index -lt 5; $index++) {
    $duration = [Convert]::ToInt32(
        $mysteryAnimation.Groups["d$index"].Value, 16)
    $oamOffset = [Convert]::ToInt32(
        $mysteryAnimation.Groups["t$index"].Value, 16)
    $parameter = [Convert]::ToInt32(
        $mysteryAnimation.Groups["p$index"].Value, 16)
    if (($oamOffset -band 1) -ne 0 -or
        ($oamOffset / 2) -ge $mysteryOamLabels.Count) {
        throw "ITEM_MYSTERY_SEED animation referenced invalid OAM pointer offset `$$($oamOffset.ToString('x2'))."
    }
    $encodedOam = Read-ItemOamComposition $mysteryOamLabels[$oamOffset / 2]
    $mysteryAnimationParts.Add("$duration,$parameter@$encodedOam")
}
$encodedMysteryAnimation = ($mysteryAnimationParts -join '|') + '~4'
$mysteryRadius = [Convert]::ToInt32(
    $mysteryAttributes.Groups['radius'].Value, 16)
$mysteryEffectFlags = [Convert]::ToInt32(
    $mysteryEffectData.Groups['flags'].Value, 16)
$mysteryEffectTile = [Convert]::ToInt32(
    $mysteryEffectData.Groups['tile'].Value, 16)
$mysteryEffectCounter = [Convert]::ToInt32(
    $mysteryEffectData.Groups['counter'].Value, 16)
if ($mysteryRadius -ne $radius -or
    $mysteryEffectFlags -ne 0x08 -or
    $mysteryEffectTile -ne 0x18 -or
    $mysteryEffectCounter -ne 0) {
    throw 'ITEM_MYSTERY_SEED radius or activation graphics changed.'
}
$seedRows = [Collections.Generic.List[string]]::new()
$seedRows.Add('# parent-item`tseed-item`ttreasure-id`tsprite`ttile-base`tpalette`tcollision`tradius-y`tradius-x`tdamage`tinitial-z`tspeed-z`tgravity`tspeed-raw`tup-y`tup-x`tright-y`tright-x`tdown-y`tdown-x`tleft-y`tleft-x`tlink-frames`tflame-sprite`tflame-tile-base`tflame-oam-flags`tflame-counter`tlanding-sound`tflame-sound`tanimation`tsource')
$seedRows.Add(
    "$($itemIds['ITEM_SEED_SATCHEL'].ToString('x2'))`t$($itemIds['ITEM_EMBER_SEED'].ToString('x2'))`t$($treasureIds['TREASURE_EMBER_SEEDS'].ToString('x2'))`tspr_common_items`t$($tileBase.ToString('x2'))`t$($palette.ToString('x2'))`t$($collision.ToString('x2'))`t$radiusY`t$radiusX`t$($damage.ToString('x2'))`t-2`t-32`t28`t1e`t-4`t0`t1`t4`t5`t0`t1`t-5`t8`tspr_common_sprites`t$($flameTileBase.ToString('x2'))`t$($flameFlags.ToString('x2'))`t$flameCounter`t$($soundIds['SND_BOMB_LAND'].ToString('x2'))`t$($soundIds['SND_LIGHTTORCH'].ToString('x2'))`t$encodedEmberAnimation`tobject_code/common/items/seeds.s:itemCode20")
$seedRows.Add(
    "$($itemIds['ITEM_SEED_SATCHEL'].ToString('x2'))`t$($itemIds['ITEM_MYSTERY_SEED'].ToString('x2'))`t$($treasureIds['TREASURE_MYSTERY_SEEDS'].ToString('x2'))`tspr_common_items`t$($mysteryData.Groups['tile'].Value)`t$($mysteryData.Groups['palette'].Value)`t$($mysteryAttributes.Groups['collision'].Value)`t$radiusY`t$radiusX`t$($mysteryAttributes.Groups['damage'].Value)`t-2`t-32`t28`t1e`t-4`t0`t1`t4`t5`t0`t1`t-5`t8`tspr_common_sprites`t$($mysteryEffectTile.ToString('x2'))`t$($mysteryEffectFlags.ToString('x2'))`t$mysteryEffectCounter`t$($soundIds['SND_BOMB_LAND'].ToString('x2'))`t$($soundIds['SND_MYSTERY_SEED'].ToString('x2'))`t$encodedMysteryAnimation`tobject_code/common/items/seeds.s:itemCode24")
Write-GeneratedTable(
    (Join-Path $destination 'metadata\seed_satchel.tsv'),
    $seedRows)

# Link-facing item presentation and sword-tile probes are runtime data, not
# state-machine policy. Preserve the source tables as typed rows so Player,
# BraceletController, and CombatController do not maintain parallel copies.
function Convert-SignedLinkItemByte([int]$value) {
    if ($value -ge 0x80) { return $value - 0x100 }
    return $value
}

function Read-HexBytes([string]$text) {
    return @([regex]::Matches($text, '\$(?<value>[0-9a-f]{2})') |
        ForEach-Object {
            [Convert]::ToInt32($_.Groups['value'].Value, 16)
        })
}

$specialObjectOamPointers = [regex]::Match(
    $specialObjectAnimationsSource,
    '(?ms)^specialObject00OamDataPointers:.*?\r?\n(?<body>.*?)(?=^specialObject02GfxPointers:)')
$specialObjectOamLabels = if ($specialObjectOamPointers.Success) {
    @([regex]::Matches(
        $specialObjectOamPointers.Groups['body'].Value,
        '(?m)^\s*\.dw\s+(?<label>oamData[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
} else { @() }
if ($specialObjectOamLabels.Count -ne 48) {
    throw "Expected 48 Link OAM pointers, parsed $($specialObjectOamLabels.Count)."
}

function Read-SpecialObjectOamComposition([int]$index) {
    if ($index -lt 0 -or $index -ge $specialObjectOamLabels.Count) {
        throw "Link graphics referenced invalid OAM index `$$($index.ToString('x2'))."
    }
    $label = $specialObjectOamLabels[$index]
    $block = [regex]::Match(
        $specialObjectOamDataSource,
        "(?ms)^${label}:\s*(?<body>.*?)(?=^oamData[0-9a-f]+:|\z)")
    if (-not $block.Success) { throw "Could not resolve Link OAM label $label." }
    $bytes = @(Read-HexBytes $block.Groups['body'].Value)
    if ($bytes.Count -lt 1 -or $bytes.Count -ne 1 + $bytes[0] * 4) {
        throw "Malformed Link OAM composition $label."
    }
    $parts = [Collections.Generic.List[string]]::new()
    for ($part = 0; $part -lt $bytes[0]; $part++) {
        $offset = 1 + $part * 4
        $parts.Add(
            "$($bytes[$offset]),$($bytes[$offset + 1]),$($bytes[$offset + 2]),$($bytes[$offset + 3])")
    }
    return $parts -join ';'
}

function Add-LinkGraphicRow(
    [Collections.Generic.List[string]]$rows,
    [string]$kind,
    [int]$variant,
    [int]$phase,
    [int]$direction,
    [int]$graphicsIndex) {
    if ($graphicsIndex -lt 0 -or $graphicsIndex -ge $linkGfxEntries.Count) {
        throw "$kind referenced invalid Link graphics index `$$($graphicsIndex.ToString('x2'))."
    }
    $entry = $linkGfxEntries[$graphicsIndex]
    $oamIndex = [Convert]::ToInt32($entry.Groups['oam'].Value, 16)
    $byteOffset = [Convert]::ToInt32($entry.Groups['offset'].Value, 16)
    $rows.Add(
        "$kind`t$variant`t$phase`t$direction`t$($graphicsIndex.ToString('x2'))`t$($oamIndex.ToString('x2'))`t$($byteOffset.ToString('x4'))`t$(Read-SpecialObjectOamComposition $oamIndex)`tdata/ages/specialObjectAnimationData.s:specialObject00GfxPointers")
}

$linkItemSourceValid =
    $swordParentSource -match
        '(?ms)^parentItemCode_sword:.*?ld \(hl\),\$28.*?^@label_4c8b:.*?ld a,\$05.*?ld a,\$09.*?ld \(hl\),\$0f.*?^@triggerSwordPoke:.*?ld \(hl\),\$08' -and
    $swordItemSource -match
        '(?ms)^@swordSounds:\s*\.db SND_SWORDSLASH\s*\.db SND_UNKNOWN5\s*\.db SND_BOOMERANG\s*\.db SND_SWORDSLASH\s*\.db SND_SWORDSLASH\s*\.db SND_UNKNOWN5\s*\.db SND_SWORDSLASH\s*\.db SND_SWORDSLASH' -and
    $itemPostUpdateSource -match
        '(?ms)^@data:\s*\.db \$02 \$41 \$80 \$c0 \$10 \$51 \$92 \$d2\s*\.db \$26 \$65 \$a4 \$e4 \$30 \$77 \$b6 \$f6\s*\.db \$00 \$11 \$22 \$33 \$44 \$55 \$66 \$77' -and
    $specialObjectAnimationsSource -match
        '(?ms)^animationData19fe9:\s*\.db \$08 \$b0 \$06\s*\.db \$7f \$b0 \$86' -and
    $specialObjectAnimationsSource -match
        '(?ms)^animationData19faa:.*?\.db \$01 \$36 \$81\s*\.db \$7f \$1c \$ff' -and
    $specialObjectAnimationsSource -match
        '(?ms)^animationData1a025:\s*\.db \$0c \$c8 \$00\s*\.db \$04 \$cc \$02\s*\.db \$04 \$cc \$04\s*\.db \$04 \$d0 \$06\s*\.db \$08 \$d0 \$08\s*\.db \$7f \$d0 \$88' -and
    $specialObjectAnimationsSource -match
        '(?ms)^animationData19ffe:\s*\.db \$0c \$ac \$40\s*\.db \$04 \$b0 \$42\s*\.db \$04 \$b0 \$44\s*\.db \$04 \$b8 \$46\s*\.db \$08 \$b8 \$48\s*\.db \$7f \$b8 \$88'
if (-not $linkItemSourceValid) {
    throw 'Sword/shovel Link timing, animation, or sound tables changed in the disassembly.'
}

$linkItemConstantRows = [Collections.Generic.List[string]]::new()
$linkItemConstantRows.Add(
    '# sword-swing-frames`tsword-tile-hit-frame`tsword-restart-frame`tsword-charge-counter`tsword-poke-frames`tsword-spin-frames`tshovel-action-frames`tshovel-dig-frame`tshovel-second-pose-frame`tswing-phase-starts`tspin-phase-starts`tshield-sound`tshield-collision-effect`tshield-link-response`tshield-projectile-response`tprojectile-collision-mode`tring-projectile-collision-mode`tsource')
$linkItemConstantRows.Add(
    "17`t6`t3`t40`t12`t23`t23`t4`t8`t0,3,6,14`t0,3,5,8,10,13,15,18,20`t$($soundIds['SND_SHIELD'].ToString('x2'))`t1f`t20`t34`t06`t07`tcode/collisionEffects.s:collisionEffect1f")
Write-GeneratedTable(
    (Join-Path $destination 'metadata\link_item_constants.tsv'),
    $linkItemConstantRows)

$linkGraphicRows = [Collections.Generic.List[string]]::new()
$linkGraphicRows.Add(
    '# kind`tvariant`tphase`tdirection`tgraphics-index`toam-index`tbyte-offset`toam`tsource')
for ($phase = 0; $phase -lt 3; $phase++) {
    $phaseBase = @(0xac, 0xb0, 0xb4)[$phase]
    for ($direction = 0; $direction -lt 4; $direction++) {
        Add-LinkGraphicRow $linkGraphicRows 'attack' 0 $phase $direction (
            $phaseBase + $direction)
    }
}
for ($phase = 0; $phase -lt 4; $phase++) {
    # parentItemLoadAnimationAndIncState changes LINK_ANIM_MODE_22 to $26
    # while wLinkObjectIndex selects the cart. Mode $26 uses $c8, $cc,
    # $cc, then the seated $58 frame for its four sword phases.
    $phaseBase = @(0xc8, 0xcc, 0xcc, 0x58)[$phase]
    for ($direction = 0; $direction -lt 4; $direction++) {
        Add-LinkGraphicRow $linkGraphicRows 'minecart-attack' 0 $phase (
            $direction) ($phaseBase + $direction)
    }
}
for ($phase = 0; $phase -lt 2; $phase++) {
    $phaseBase = @(0xf8, 0xfc)[$phase]
    for ($direction = 0; $direction -lt 4; $direction++) {
        Add-LinkGraphicRow $linkGraphicRows 'shovel' 0 $phase $direction (
            $phaseBase + $direction)
    }
}
for ($pose = 0; $pose -lt 3; $pose++) {
    # BraceletActionPose Pull, PullStrain, Throw.
    $poseBase = @(0xdc, 0xe0, 0xb0)[$pose]
    for ($direction = 0; $direction -lt 4; $direction++) {
        Add-LinkGraphicRow $linkGraphicRows 'bracelet' $pose 0 $direction (
            $poseBase + $direction)
    }
}
for ($phase = 0; $phase -lt 2; $phase++) {
    $phaseBase = @(0x58, 0x84)[$phase]
    for ($direction = 0; $direction -lt 4; $direction++) {
        Add-LinkGraphicRow $linkGraphicRows 'minecart' 0 $phase $direction (
            $phaseBase + $direction)
    }
}
for ($variant = 0; $variant -lt 4; $variant++) {
    for ($phase = 0; $phase -lt 2; $phase++) {
        for ($direction = 0; $direction -lt 4; $direction++) {
            $graphicsIndex =
                0x68 + $variant * 4 + $direction + $phase * 0x2c
            Add-LinkGraphicRow $linkGraphicRows 'shield' $variant $phase (
                $direction) $graphicsIndex
        }
    }
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\link_item_graphics.tsv'),
    $linkGraphicRows)

$linkOffsetRows = [Collections.Generic.List[string]]::new()
$linkOffsetRows.Add(
    '# kind`tindex`tsubindex`toffset-y`toffset-x`tradius-y`tradius-x`tsource')
for ($direction = 0; $direction -lt 4; $direction++) {
    $oamIndex = 0x08 + $direction
    $firstPart = (Read-SpecialObjectOamComposition $oamIndex).Split(';')[0].Split(',')
    $offsetY = (Convert-SignedLinkItemByte ([int]$firstPart[0])) - 8
    $offsetX = Convert-SignedLinkItemByte ([int]$firstPart[1])
    $linkOffsetRows.Add(
        "attack-pose`t$direction`t0`t$offsetY`t$offsetX`t0`t0`tdata/ages/specialObjectOamData.s:$($specialObjectOamLabels[$oamIndex])")
}
$shovelOffsetBlock = [regex]::Match(
    $shovelParentSource,
    '(?ms)^@offsets:(?<body>(?:\s*\.db \$[0-9a-f]{2} \$[0-9a-f]{2}[^\r\n]*\r?\n){4})')
$shovelOffsetBytes = if ($shovelOffsetBlock.Success) {
    @(Read-HexBytes $shovelOffsetBlock.Groups['body'].Value)
} else { @() }
if ($shovelOffsetBytes.Count -ne 8) {
    throw "Expected eight ITEM_SHOVEL offset bytes, parsed $($shovelOffsetBytes.Count)."
}
for ($direction = 0; $direction -lt 4; $direction++) {
    $y = Convert-SignedLinkItemByte $shovelOffsetBytes[$direction * 2]
    $x = Convert-SignedLinkItemByte $shovelOffsetBytes[$direction * 2 + 1]
    $linkOffsetRows.Add(
        "shovel-child`t$direction`t0`t$y`t$x`t0`t0`tobject_code/common/itemParents/shovelParent.s:@offsets")
}
$shieldOffsetBlock = [regex]::Match(
    $collisionEffectsSource,
    '(?ms)^@shieldPositionOffsets:(?<body>(?:\s*\.db \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2}[^\r\n]*\r?\n){4})')
$shieldOffsetBytes = if ($shieldOffsetBlock.Success) {
    @(Read-HexBytes $shieldOffsetBlock.Groups['body'].Value)
} else { @() }
if ($shieldOffsetBytes.Count -ne 16) {
    throw "Expected 16 ITEM_SHIELD collision bytes, parsed $($shieldOffsetBytes.Count)."
}
for ($direction = 0; $direction -lt 4; $direction++) {
    $offset = $direction * 4
    $y = Convert-SignedLinkItemByte $shieldOffsetBytes[$offset]
    $x = Convert-SignedLinkItemByte $shieldOffsetBytes[$offset + 1]
    $radiusY = $shieldOffsetBytes[$offset + 2]
    $radiusX = $shieldOffsetBytes[$offset + 3]
    $linkOffsetRows.Add(
        "shield-collision`t$direction`t0`t$y`t$x`t$radiusY`t$radiusX`tcode/collisionEffects.s:@shieldPositionOffsets")
}
$liftOffsetBlock = [regex]::Match(
    $parentItemCommonSource,
    '(?ms)^@liftedObjectPositions:\s*;\s*Weight 0(?<body>.*?)(?=\s*;\s*Weight 1)')
$liftOffsetBytes = if ($liftOffsetBlock.Success) {
    @(Read-HexBytes $liftOffsetBlock.Groups['body'].Value)
} else { @() }
if ($liftOffsetBytes.Count -ne 32) {
    throw "Expected 32 weight-0 lifted-object offset bytes, parsed $($liftOffsetBytes.Count)."
}
for ($frame = 0; $frame -lt 4; $frame++) {
    for ($direction = 0; $direction -lt 4; $direction++) {
        $offset = $frame * 8 + $direction * 2
        $z = Convert-SignedLinkItemByte $liftOffsetBytes[$offset]
        $x = Convert-SignedLinkItemByte $liftOffsetBytes[$offset + 1]
        $linkOffsetRows.Add(
            "bracelet-lift`t$frame`t$direction`t$z`t$x`t0`t0`tobject_code/common/itemParents/commonCode.s:@liftedObjectPositions")
    }
}
$swordTileOffsetBlock = [regex]::Match(
    $itemCommonCode2Source,
    '(?ms)^@linkOffsets:(?<body>(?:\s*\.db \$[0-9a-f]{2} \$[0-9a-f]{2}[^\r\n]*\r?\n){9})')
$swordTileOffsetBytes = if ($swordTileOffsetBlock.Success) {
    @(Read-HexBytes $swordTileOffsetBlock.Groups['body'].Value)
} else { @() }
if ($swordTileOffsetBytes.Count -ne 18) {
    throw "Expected 18 sword-tile Link offset bytes, parsed $($swordTileOffsetBytes.Count)."
}
for ($direction = 0; $direction -lt 9; $direction++) {
    $y = Convert-SignedLinkItemByte $swordTileOffsetBytes[$direction * 2]
    $x = Convert-SignedLinkItemByte $swordTileOffsetBytes[$direction * 2 + 1]
    $linkOffsetRows.Add(
        "sword-tile`t$direction`t0`t$y`t$x`t0`t0`tobject_code/common/items/commonCode2.s:@linkOffsets")
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\link_item_offsets.tsv'),
    $linkOffsetRows)

$swordPresentationRows = [Collections.Generic.List[string]]::new()
$swordPresentationRows.Add(
    '# kind`tindex`tsubindex`tvalue-a`tvalue-b`tvalue-c`tvalue-d`tsource')
$swordAnimationIndices = @{
    0 = @(2, 1, 0, 0); 1 = @(0, 1, 2, 2)
    2 = @(6, 5, 4, 4); 3 = @(0, 7, 6, 6)
}
for ($direction = 0; $direction -lt 4; $direction++) {
    for ($phase = 0; $phase -lt 4; $phase++) {
        $swordPresentationRows.Add(
            "animation`t$direction`t$phase`t$($swordAnimationIndices[$direction][$phase])`t0`t0`t0`tobject_code/common/items/postUpdate.s:updateSwingableItemAnimation.@data")
    }
}
$swordArcBlock = [regex]::Match(
    $itemPostUpdateSource,
    '(?ms)^swordArcData:(?<body>.*?)(?=^biggoronSwordArcData:)')
$swordArcRows = if ($swordArcBlock.Success) {
    @([regex]::Matches(
        $swordArcBlock.Groups['body'].Value,
        '(?m)^\s*\.db\s+\$(?<ry>[0-9a-f]{2})\s+\$(?<rx>[0-9a-f]{2})\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})'))
} else { @() }
if ($swordArcRows.Count -ne 28) {
    throw "Expected 28 swordArcData rows, parsed $($swordArcRows.Count)."
}
for ($index = 0; $index -lt $swordArcRows.Count; $index++) {
    $arc = $swordArcRows[$index]
    $radiusY = [Convert]::ToInt32($arc.Groups['ry'].Value, 16)
    $radiusX = [Convert]::ToInt32($arc.Groups['rx'].Value, 16)
    $y = Convert-SignedLinkItemByte (
        [Convert]::ToInt32($arc.Groups['y'].Value, 16))
    $x = Convert-SignedLinkItemByte (
        [Convert]::ToInt32($arc.Groups['x'].Value, 16))
    $swordPresentationRows.Add(
        "arc`t$index`t0`t$radiusY`t$radiusX`t$y`t$x`tobject_code/common/items/postUpdate.s:swordArcData")
}
$swordSoundBlock = [regex]::Match(
    $swordItemSource,
    '(?ms)^@swordSounds:(?<body>.*?)(?=^@state0:)')
$swordSoundNames = if ($swordSoundBlock.Success) {
    @([regex]::Matches(
        $swordSoundBlock.Groups['body'].Value,
        '(?m)^\s*\.db\s+(?<sound>SND_[A-Z0-9_]+)') |
        ForEach-Object { $_.Groups['sound'].Value })
} else { @() }
if ($swordSoundNames.Count -ne 8) {
    throw "Expected eight ITEM_SWORD slash sounds, parsed $($swordSoundNames.Count)."
}
for ($index = 0; $index -lt $swordSoundNames.Count; $index++) {
    $name = $swordSoundNames[$index]
    if (-not $soundIds.ContainsKey($name)) {
        throw "Could not resolve ITEM_SWORD sound $name."
    }
    $swordPresentationRows.Add(
        "sound`t$index`t0`t$($soundIds[$name].ToString('x2'))`t0`t0`t0`tobject_code/common/items/sword.s:@swordSounds")
}
$swordOamPointerBlock = [regex]::Match(
    $itemAnimationsSource,
    '(?ms)^item05OamDataPointers:.*?(?<body>(?:\s*\.dw\s+itemOamData[0-9a-f]+\s*\r?\n){8})')
$swordOamLabels = if ($swordOamPointerBlock.Success) {
    @([regex]::Matches(
        $swordOamPointerBlock.Groups['body'].Value,
        '(?m)^\s*\.dw\s+(?<label>itemOamData[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
} else { @() }
if ($swordOamLabels.Count -ne 8) {
    throw "Expected eight ITEM_SWORD OAM pointers, parsed $($swordOamLabels.Count)."
}
for ($animation = 0; $animation -lt $swordOamLabels.Count; $animation++) {
    $label = $swordOamLabels[$animation]
    $block = [regex]::Match(
        $itemOamDataSource,
        "(?ms)^${label}:\s*(?<body>.*?)(?=^itemOamData[0-9a-f]+:|\z)")
    if (-not $block.Success) { throw "Could not resolve sword OAM label $label." }
    $bytes = @(Read-HexBytes $block.Groups['body'].Value)
    if ($bytes.Count -lt 1 -or $bytes.Count -ne 1 + $bytes[0] * 4) {
        throw "Malformed sword OAM composition $label."
    }
    for ($part = 0; $part -lt $bytes[0]; $part++) {
        $offset = 1 + $part * 4
        $flags = $bytes[$offset + 3]
        $swordPresentationRows.Add(
            "oam`t$animation`t$part`t$(Convert-SignedLinkItemByte $bytes[$offset])`t$(Convert-SignedLinkItemByte $bytes[$offset + 1])`t$($bytes[$offset + 2])`t$($flags.ToString('x2'))`tdata/itemOamData.s:$label")
    }
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\sword_presentation.tsv'),
    $swordPresentationRows)

if ($clinkSoundSource -notmatch
    '(?ms)^clinkSoundTable:\s*\.dw @overworld\s*\.dw @indoors\s*\.dw @dungeons\s*\.dw @sidescrolling\s*\.dw @underwater\s*\.dw @five\s*^@overworld:\s*^@underwater:\s*\.db \$c1 \$c2 \$c4 \$d1 \$cf\s*\.db \$00\s*\.db \$fd \$fe \$ff\s*\.db \$00\s*\.db \$00\s*^@indoors:\s*^@dungeons:\s*^@five:\s*\.db \$1f \$30 \$31 \$32 \$33 \$38 \$39 \$3a \$3b \$68 \$69\s*\.db \$00\s*\.db \$0a \$0b\s*\.db \$00\s*^@sidescrolling:\s*\.db \$12\s*\.db \$00\s*\.db \$00') {
    throw 'Aliased or zero-terminated Ages clinkSoundTable changed.'
}
$clinkListIds = @(
    'overworld', 'indoors', 'indoors',
    'sidescrolling', 'overworld', 'indoors')
$bombableClinkLists = @{
    0 = @(0xc1, 0xc2, 0xc4, 0xd1, 0xcf)
    1 = @(0x1f, 0x30, 0x31, 0x32, 0x33, 0x38, 0x39, 0x3a, 0x3b, 0x68, 0x69)
    2 = @(0x1f, 0x30, 0x31, 0x32, 0x33, 0x38, 0x39, 0x3a, 0x3b, 0x68, 0x69)
    3 = @(0x12)
    4 = @(0xc1, 0xc2, 0xc4, 0xd1, 0xcf)
    5 = @(0x1f, 0x30, 0x31, 0x32, 0x33, 0x38, 0x39, 0x3a, 0x3b, 0x68, 0x69)
}
$silentClinkLists = @{
    0 = @(0xfd, 0xfe, 0xff); 1 = @(0x0a, 0x0b); 2 = @(0x0a, 0x0b)
    3 = @(); 4 = @(0xfd, 0xfe, 0xff); 5 = @(0x0a, 0x0b)
}
$clinkRows = [Collections.Generic.List[string]]::new()
$clinkRows.Add(
    '# collision-set`tkind`tlist-id`torder`ttile`tterminal`tsource')
for ($collisionSet = 0; $collisionSet -lt 6; $collisionSet++) {
    foreach ($kind in @('bombable', 'silent')) {
        $tiles = if ($kind -eq 'bombable') {
            $bombableClinkLists[$collisionSet]
        } else {
            $silentClinkLists[$collisionSet]
        }
        for ($order = 0; $order -le $tiles.Count; $order++) {
            $terminal = if ($order -eq $tiles.Count) { 1 } else { 0 }
            $tile = if ($terminal) { 0 } else { $tiles[$order] }
            $clinkRows.Add(
                "$collisionSet`t$kind`t$($clinkListIds[$collisionSet])`t$order`t$($tile.ToString('x2'))`t$terminal`tdata/ages/tile_properties/clinkSounds.s:clinkSoundTable")
        }
    }
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\sword_clink_tiles.tsv'),
    $clinkRows)

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
Write-GeneratedTable(
    (Join-Path $destination 'metadata\sword_beam.tsv'),
    $swordBeamRows)

# Preserve the common giveTreasure lookup data so the runtime can update
# inventory variables from original treasure IDs and parameters.
$behaviourRows = [Collections.Generic.List[string]]::new()
$behaviourRows.Add("# treasure-id`tvariable`tmode`tsound")
$behaviourSource = Read-ImportLines (Join-Path $Disassembly "data\ages\treasureCollectionBehaviours.s")
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
Write-GeneratedTable(
    (Join-Path $destination "metadata\treasure_behaviours.tsv"),
    $behaviourRows)

# Treasure objects encode the object subid found in chestData.s and the exact
# b/c values passed to giveTreasure.
$treasureObjectRows = [Collections.Generic.List[string]]::new()
$treasureObjectRows.Add("# treasure-object`ttreasure-id`tsubid`tparameter`ttext-id`tgraphic`tmessage-base64")
$treasureObjectRecords = @{}
$treasureObjectSource = Read-ImportLines (Join-Path $Disassembly "data\ages\treasureObjectData.s")
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
Write-GeneratedTable(
    (Join-Path $destination "metadata\treasure_objects.tsv"),
    $treasureObjectRows)

# Export the item icon rows used by loadTreasureDisplayData. Runtime code only
# consumes a subset today, but keeping all rows makes the inventory foundation
# data-driven for later menu/equipment slices.
$displayRows = [Collections.Generic.List[string]]::new()
$displayRows.Add("# table`tindex`ttreasure-id`tleft-sprite`tleft-palette`tright-sprite`tright-palette`textra-mode`ttext-low")
$displaySource = Read-ImportLines (Join-Path $Disassembly "data\ages\treasureDisplayData.s")
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
Write-GeneratedTable(
    (Join-Path $destination "metadata\treasure_display.tsv"),
    $displayRows)

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
Write-GeneratedTable(
    (Join-Path $destination "metadata\inventory_text.tsv"),
    $inventoryTextRows)

# Export the breakable tile tables used by tryToBreakTile. The source masks
# retain the disassembly's left-to-right bit order from breakableTileSources.s.
# Effect bit 7 calls updateRoomFlagsForBrokenTile, whose collision-indexed
# room-flag and Gasha-maturity tables are retained on each applicable row.
$breakableSource = Read-ImportLines (Join-Path $Disassembly "data\ages\tile_properties\breakableTiles.s")
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
    foreach ($line in Read-ImportLines $path) {
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
Write-GeneratedTable(
    (Join-Path $destination "metadata\breakable_tiles.tsv"),
    $breakableRows)

# Export LINK_STATE_JUMPING_DOWN_LEDGE's collision-set-specific cliff and
# landing tables together with the exact edge probes, length speeds, physics,
# animation timing, and sounds consumed by checkLinkJumpingOffCliff/linkState12.
$linkSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\link.s')
$cliffTileSource = Read-ImportLines (
    Join-Path $Disassembly 'data\ages\tile_properties\cliffTiles.s')
$landableTileSource = Read-ImportLines (
    Join-Path $Disassembly 'data\ages\tile_properties\landableTilesFromCliffs.s')
$tileIndexSource = Read-ImportLines (
    Join-Path $Disassembly 'constants\common\tileIndices.s')
$objectSpeedSource = Read-ImportLines (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$soundSource = Read-ImportLines (
    Join-Path $Disassembly 'constants\common\music.s')
$linkAnimationSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')

# Export the bitwise tile types and fixed 8.8 Link physics used by
# linkState01_sidescroll. These are not ordinary TerrainType values: a tile can
# combine ladder, water, ice, and ladder-top behavior in one byte.
$sideTileTypeSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tile_properties\tileTypeMappings.s')
$tileTypeConstantSource = Read-ImportLines (
    Join-Path $Disassembly 'constants\common\tileTypes.s')
$sideCommonSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\commonCode.s')
$featherParentSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\featherParent.s')
$sidePlatformSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\circularSidescrollPlatform.s')

$objectSpeeds = @{}
foreach ($line in $objectSpeedSource) {
    if ($line -match '^\s*(?<name>SPEED_[A-Za-z0-9]+)\s+dsb\s+5\s*;\s*0x(?<value>[0-9a-f]{2})') {
        $objectSpeeds[$Matches['name']] =
            [Convert]::ToInt32($Matches['value'], 16)
    }
}
foreach ($line in $objectSpeedSource) {
    if ($line -match '^\s*\.define\s+(?<alias>SPEED_[A-Za-z0-9]+)\s+(?<name>SPEED_[A-Za-z0-9]+)\s*$') {
        if (-not $objectSpeeds.ContainsKey($Matches['name'])) {
            throw "Unknown object-speed alias target '$($Matches['name'])'."
        }
        $objectSpeeds[$Matches['alias']] = $objectSpeeds[$Matches['name']]
    }
}

$sideTileTypeConstants = @{}
foreach ($line in $tileTypeConstantSource) {
    if ($line -match '^\s*\.define\s+(?<name>TILETYPE_SS_[A-Za-z0-9_]+)\s+\$(?<value>[0-9a-f]{2})') {
        $sideTileTypeConstants[$Matches['name']] =
            [Convert]::ToInt32($Matches['value'], 16)
    }
}
if ($sideTileTypeConstants.Count -ne 6) {
    throw "Expected six side-scrolling tile-type constants, parsed $($sideTileTypeConstants.Count)."
}

$sideTileBlock = [regex]::Match(
    $sideTileTypeSource,
    '(?ms)^@sidescrolling:\s*(?<body>.*?)(?=^\s*\.db\s+\$00\s*$)')
if (-not $sideTileBlock.Success) {
    throw 'Could not parse tileTypesTable@sidescrolling.'
}
$sideTileRows = [Collections.Generic.List[string]]::new()
$sideTileRows.Add("# tile`tflags`tsource")
foreach ($line in ($sideTileBlock.Groups['body'].Value -split "`r?`n")) {
    if ($line -notmatch '^\s*\.db\s+\$(?<tile>[0-9a-f]{2})\s+(?<expression>[^;]+?)\s*$') {
        continue
    }
    $sideTile = $Matches['tile']
    $sideExpression = $Matches['expression']
    $flags = 0
    foreach ($token in ($sideExpression -split '\|')) {
        $name = $token.Trim()
        if ($name -match '^\$(?<value>[0-9a-f]{2})$') {
            $flags = $flags -bor [Convert]::ToInt32($Matches['value'], 16)
        }
        elseif ($sideTileTypeConstants.ContainsKey($name)) {
            $flags = $flags -bor $sideTileTypeConstants[$name]
        }
        else {
            throw "Unknown side-scrolling tile-type expression '$name'."
        }
    }
    $sideTileRows.Add(
        "$sideTile`t$($flags.ToString('x2'))`ttileTypeMappings.s:@sidescrolling")
}
if ($sideTileRows.Count -ne 17 -or
    ($sideTileRows | Where-Object { $_ -eq "18`t10`ttileTypeMappings.s:@sidescrolling" }).Count -ne 1 -or
    ($sideTileRows | Where-Object { $_ -eq "17`t90`ttileTypeMappings.s:@sidescrolling" }).Count -ne 1 -or
    ($sideTileRows | Where-Object { $_ -eq "1a`t30`ttileTypeMappings.s:@sidescrolling" }).Count -ne 1 -or
    ($sideTileRows | Where-Object { $_ -eq "f4`t01`ttileTypeMappings.s:@sidescrolling" }).Count -ne 1) {
    throw 'Could not export all 16 side-scrolling tile-type rows.'
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\side_scroll_tiles.tsv'),
    $sideTileRows)

$sidePhysicsBlock = [regex]::Match(
    $linkSource,
    '(?ms)^linkUpdateInAir_sidescroll:.*?(?=^initLinkState:)')
$sideActiveTileMatch = [regex]::Match(
    $sideCommonSource,
    '(?ms)^sidescrollUpdateActiveTile:.*?ld bc,\$(?<below>[0-9a-f]{4}).*?(?=^\s*;;)')
$sideJumpMatch = [regex]::Match(
    $featherParentSource,
    '(?ms)^\s*; Jump higher in sidescrolling rooms.*?ld bc,\$fe20.*?cp FIRST_SIDESCROLL_GROUP.*?ld bc,\$(?<speed>[0-9a-f]{4})')
$sideJumpAnimationMatch = [regex]::Match(
    $linkAnimationSource,
    '(?ms)^animationData19f78:\s+\.db \$(?<d0>[0-9a-f]{2}) \$e4 \$00\s+\.db \$(?<d1>[0-9a-f]{2}) \$e8 \$00\s+\.db \$(?<d2>[0-9a-f]{2}) \$ec \$00\s+\.db \$7f \$80 \$ff')
if (-not $sidePhysicsBlock.Success -or
    -not $sideActiveTileMatch.Success -or
    -not $sideJumpMatch.Success -or
    -not $sideJumpAnimationMatch.Success -or
    $sidePhysicsBlock.Groups[0].Value -notmatch
        '(?ms)ld c,\$(?<gravity>[0-9a-f]{2}).*?bit 5,a.*?ld c,\$(?<reduced>[0-9a-f]{2})' -or
    $sidePhysicsBlock.Groups[0].Value -notmatch
        '(?ms)and \$(?<snapMask>[0-9a-f]{2})\s+add \$(?<snapOffset>[0-9a-f]{2})' -or
    $sidePhysicsBlock.Groups[0].Value -notmatch
        '(?ms)If speedZ is negative.*?and \$(?<ceilingMask>[0-9a-f]{2}).*?@positiveSpeedZ:.*?and \$(?<groundMask>[0-9a-f]{2})' -or
    $sidePhysicsBlock.Groups[0].Value -notmatch
        '(?ms)Cap Link''s speedZ to \$(?<maximum>[0-9a-f]{4}).*?cp \$(?<maximumHigh>[0-9a-f]{2})' -or
    $sidePhysicsBlock.Groups[0].Value -notmatch
        '(?ms)reached the bottom boundary.*?cp \$(?<bottom>[0-9a-f]{2})') {
    throw 'Could not verify linkUpdateInAir_sidescroll physics constants.'
}

function Resolve-SideScrollSound([string]$name) {
    foreach ($line in $soundSource) {
        if ($line -match "^\s*$name\s+db\s*;\s*\`$(?<value>[0-9a-f]{2})") {
            return [Convert]::ToInt32($Matches['value'], 16)
        }
    }
    throw "Could not resolve side-scrolling sound constant $name."
}

$sidePhysicsText = $sidePhysicsBlock.Groups[0].Value
$null = $sidePhysicsText -match
    '(?ms)ld c,\$(?<gravity>[0-9a-f]{2}).*?bit 5,a.*?ld c,\$(?<reduced>[0-9a-f]{2})'
$sideGravity = [Convert]::ToInt32($Matches['gravity'], 16)
$sideReducedGravity = [Convert]::ToInt32($Matches['reduced'], 16)
$null = $sidePhysicsText -match
    '(?ms)and \$(?<snapMask>[0-9a-f]{2})\s+add \$(?<snapOffset>[0-9a-f]{2})'
$sideSnapMask = [Convert]::ToInt32($Matches['snapMask'], 16)
$sideSnapOffset = [Convert]::ToInt32($Matches['snapOffset'], 16)
$null = $sidePhysicsText -match
    '(?ms)If speedZ is negative.*?and \$(?<ceilingMask>[0-9a-f]{2}).*?@positiveSpeedZ:.*?and \$(?<groundMask>[0-9a-f]{2})'
$sideCeilingMask = [Convert]::ToInt32($Matches['ceilingMask'], 16)
$sideGroundMask = [Convert]::ToInt32($Matches['groundMask'], 16)
$null = $sidePhysicsText -match
    '(?ms)Cap Link''s speedZ to \$(?<maximum>[0-9a-f]{4}).*?cp \$(?<maximumHigh>[0-9a-f]{2})'
$sideMaximumSpeed = [Convert]::ToInt32($Matches['maximum'], 16)
if (($sideMaximumSpeed -shr 8) -ne
    [Convert]::ToInt32($Matches['maximumHigh'], 16)) {
    throw 'Side-scrolling maximum fall speed disagrees with its high-byte cap.'
}
$null = $sidePhysicsText -match
    '(?ms)reached the bottom boundary.*?cp \$(?<bottom>[0-9a-f]{2})'
$sideBottomBoundary = [Convert]::ToInt32($Matches['bottom'], 16)
$belowOffset = [Convert]::ToInt32(
    $sideActiveTileMatch.Groups['below'].Value, 16) -shr 8
$jumpSpeedUnsigned = [Convert]::ToInt32(
    $sideJumpMatch.Groups['speed'].Value, 16)
$jumpSpeed = if ($jumpSpeedUnsigned -ge 0x8000) {
    $jumpSpeedUnsigned - 0x10000
} else {
    $jumpSpeedUnsigned
}
$spikeTileMatch = [regex]::Match(
    ($tileIndexSource -join "`n"),
    '(?m)^\s*\.define\s+TILEINDEX_SS_SPIKE\s+\$(?<value>[0-9a-f]{2})')
if (-not $spikeTileMatch.Success) {
    throw 'Could not resolve TILEINDEX_SS_SPIKE.'
}

function Resolve-SideObjectSpeed([string]$name) {
    if (-not $objectSpeeds.ContainsKey($name)) {
        throw "Could not resolve side-scrolling object speed $name."
    }
    return $objectSpeeds[$name]
}

$sideWaterExitMatch = [regex]::Match(
    $linkSource,
    '(?ms)Make him "hop out" of the water.*?ld bc,-\$(?<speed>[0-9a-f]{2,4})')
$sideCapeMatch = [regex]::Match(
    $featherParentSource,
    '(?ms)^@state1:.*?ld \(hl\),<\(-\$(?<speed>[0-9a-f]{2,4})\)')
$sideIceIntervalMatch = [regex]::Match(
    $linkSource,
    '(?ms)^@speedTable:.*?; Slippery\s+\.db SPEED_000, \$(?<interval>[0-9a-f]{2})')
if (-not $sideWaterExitMatch.Success -or
    -not $sideCapeMatch.Success -or
    -not $sideIceIntervalMatch.Success -or
    $linkSource -notmatch '(?ms)^linkSetSwimmingSpeed:.*?ld a,SPEED_e0.*?ld a,SPEED_80' -or
    $linkSource -notmatch '(?ms)^@mermaidSuit:.*?ld a,SPEED_160' -or
    $linkSource -notmatch '(?ms)^@speedTable:.*?; Normal\s+\.db SPEED_100, \$00, SPEED_0c0, SPEED_080, SPEED_100' -or
    $linkSource -notmatch '(?ms); Mermaid suit movement\s+\.db SPEED_000, \$05, SPEED_120, SPEED_120, SPEED_120' -or
    $linkSource -notmatch '(?ms)^linkUpdateKnockback:.*?ld b,SPEED_140' -or
    $sidePlatformSource -notmatch '(?ms)^@moveLinkAtAngle:.*?ld b,SPEED_80') {
    throw 'Could not verify the complete side-scrolling Link speed contract.'
}
$sideWaterExitSpeed =
    -[Convert]::ToInt32($sideWaterExitMatch.Groups['speed'].Value, 16)
$sideCapeSpeed =
    -[Convert]::ToInt32($sideCapeMatch.Groups['speed'].Value, 16)
$sideIceInterval =
    [Convert]::ToInt32($sideIceIntervalMatch.Groups['interval'].Value, 16)

$sideConstantRows = @(
    "# key`tvalue`tsource",
    "gravity`t$sideGravity`tlink.s:linkUpdateInAir_sidescroll",
    "reduced-gravity`t$sideReducedGravity`tlink.s:linkUpdateInAir_sidescroll",
    "maximum-fall-speed`t$sideMaximumSpeed`tlink.s:linkUpdateInAir_sidescroll",
    "jump-speed-z`t$jumpSpeed`tfeatherParent.s:parentItemCode_feather",
    "water-exit-speed-z`t$sideWaterExitSpeed`tlink.s:linkState01_sidescroll",
    "rocs-cape-speed-z`t$sideCapeSpeed`tfeatherParent.s:parentItemCode_feather",
    "normal-speed`t$(Resolve-SideObjectSpeed 'SPEED_100')`tlink.s:updateLinkSpeed_withParam@speedTable",
    "platform-push-speed`t$(Resolve-SideObjectSpeed 'SPEED_80')`tcircularSidescrollPlatform.s:sidescrollingPlatformCommon",
    "knockback-speed`t$(Resolve-SideObjectSpeed 'SPEED_140')`tlink.s:linkUpdateKnockback",
    "ice-velocity-interval`t$sideIceInterval`tlink.s:updateLinkSpeed_withParam@speedTable",
    "swim-speed`t$(Resolve-SideObjectSpeed 'SPEED_80')`tlink.s:linkSetSwimmingSpeed",
    "fast-swim-speed`t$(Resolve-SideObjectSpeed 'SPEED_e0')`tlink.s:linkSetSwimmingSpeed",
    "mermaid-target-speed`t$(Resolve-SideObjectSpeed 'SPEED_120')`tlink.s:updateLinkSpeed_withParam@speedTable",
    "fast-mermaid-target-speed`t$(Resolve-SideObjectSpeed 'SPEED_160')`tlink.s:linkUpdateVelocity@mermaidSuit",
    "ground-wall-mask`t$sideGroundMask`tlink.s:linkUpdateInAir_sidescroll",
    "ceiling-wall-mask`t$sideCeilingMask`tlink.s:linkUpdateInAir_sidescroll",
    "landing-high-mask`t$sideSnapMask`tlink.s:linkUpdateInAir_sidescroll",
    "landing-high-offset`t$sideSnapOffset`tlink.s:linkUpdateInAir_sidescroll",
    "below-tile-offset`t$belowOffset`tcommonCode.s:sidescrollUpdateActiveTile",
    "bottom-boundary`t$sideBottomBoundary`tlink.s:linkUpdateInAir_sidescroll",
    "spike-tile`t$([Convert]::ToInt32($spikeTileMatch.Groups['value'].Value, 16))`ttileIndices.s:TILEINDEX_SS_SPIKE",
    "jump-sound`t$(Resolve-SideScrollSound 'SND_JUMP')`tlink.s:linkUpdateInAir_sidescroll",
    "land-sound`t$(Resolve-SideScrollSound 'SND_LAND')`tlink.s:linkUpdateInAir_sidescroll",
    "animation-phase-0`t$([Convert]::ToInt32($sideJumpAnimationMatch.Groups['d0'].Value, 16))`tspecialObjectAnimationData.s:animationData19f78",
    "animation-phase-1`t$([Convert]::ToInt32($sideJumpAnimationMatch.Groups['d1'].Value, 16))`tspecialObjectAnimationData.s:animationData19f78",
    "animation-phase-2`t$([Convert]::ToInt32($sideJumpAnimationMatch.Groups['d2'].Value, 16))`tspecialObjectAnimationData.s:animationData19f78"
)
if ($sideConstantRows.Count -ne 27) {
    throw 'Side-scrolling player constants lost an expected row.'
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\side_scroll_constants.tsv'),
    $sideConstantRows)

$topDownPhysicsBlock = [regex]::Match(
    $linkSource,
    '(?ms)^linkUpdateInAir:.*?(?=^linkUpdateInAir_sidescroll:)')
$topDownJumpMatch = [regex]::Match(
    $featherParentSource,
    '(?ms)^\s*; Jump higher in sidescrolling rooms\s+ld bc,\$(?<speed>[0-9a-f]{4})\s+ld a,\(wActiveGroup\)')
if (-not $topDownPhysicsBlock.Success -or
    -not $topDownJumpMatch.Success -or
    $topDownPhysicsBlock.Groups[0].Value -notmatch
        '(?ms)bit 5,\(hl\)\s+ld c,\$(?<gravity>[0-9a-f]{2})\s+jr z,\+\s+ld c,\$(?<reduced>[0-9a-f]{2})' -or
    $topDownPhysicsBlock.Groups[0].Value -notmatch
        '(?ms)Return if speedZ < \$(?<maximum>[0-9a-f]{4})\s+cp \$(?<maximumHigh>[0-9a-f]{2}).*?Cap speedZ to \$\k<maximum>' -or
    $topDownPhysicsBlock.Groups[0].Value -notmatch
        '(?ms)ld a,\$(?<holeCounter>[0-9a-f]{2})\s+ld \(wStandingOnTileCounter\),a') {
    throw 'Could not verify top-down linkUpdateInAir physics constants.'
}
$topDownPhysicsText = $topDownPhysicsBlock.Groups[0].Value
$null = $topDownPhysicsText -match
    '(?ms)bit 5,\(hl\)\s+ld c,\$(?<gravity>[0-9a-f]{2})\s+jr z,\+\s+ld c,\$(?<reduced>[0-9a-f]{2})'
$topDownGravity = [Convert]::ToInt32($Matches['gravity'], 16)
$topDownReducedGravity = [Convert]::ToInt32($Matches['reduced'], 16)
$null = $topDownPhysicsText -match
    '(?ms)Return if speedZ < \$(?<maximum>[0-9a-f]{4})\s+cp \$(?<maximumHigh>[0-9a-f]{2}).*?Cap speedZ to \$\k<maximum>'
$topDownMaximumSpeed = [Convert]::ToInt32($Matches['maximum'], 16)
if (($topDownMaximumSpeed -shr 8) -ne
    [Convert]::ToInt32($Matches['maximumHigh'], 16)) {
    throw 'Top-down maximum fall speed disagrees with its high-byte cap.'
}
$null = $topDownPhysicsText -match
    '(?ms)ld a,\$(?<holeCounter>[0-9a-f]{2})\s+ld \(wStandingOnTileCounter\),a'
$topDownJumpUnsigned = [Convert]::ToInt32(
    $topDownJumpMatch.Groups['speed'].Value, 16)
$topDownJumpSpeed = if ($topDownJumpUnsigned -ge 0x8000) {
    $topDownJumpUnsigned - 0x10000
} else {
    $topDownJumpUnsigned
}
$topDownAirRows = @(
    "# key`tvalue`tsource",
    "gravity`t$topDownGravity`tlink.s:linkUpdateInAir",
    "reduced-gravity`t$topDownReducedGravity`tlink.s:linkUpdateInAir",
    "maximum-fall-speed`t$topDownMaximumSpeed`tlink.s:linkUpdateInAir",
    "jump-speed-z`t$topDownJumpSpeed`tfeatherParent.s:parentItemCode_feather",
    "hole-standing-counter`t$([Convert]::ToInt32($Matches['holeCounter'], 16))`tlink.s:linkUpdateInAir",
    "jump-sound`t$(Resolve-SideScrollSound 'SND_JUMP')`tlink.s:linkUpdateInAir",
    "land-sound`t$(Resolve-SideScrollSound 'SND_LAND')`tlink.s:linkUpdateInAir",
    "animation-phase-0`t$([Convert]::ToInt32($sideJumpAnimationMatch.Groups['d0'].Value, 16))`tspecialObjectAnimationData.s:animationData19f78",
    "animation-phase-1`t$([Convert]::ToInt32($sideJumpAnimationMatch.Groups['d1'].Value, 16))`tspecialObjectAnimationData.s:animationData19f78",
    "animation-phase-2`t$([Convert]::ToInt32($sideJumpAnimationMatch.Groups['d2'].Value, 16))`tspecialObjectAnimationData.s:animationData19f78"
)
if ($topDownAirRows.Count -ne 11 -or
    $topDownJumpSpeed -ne -0x1e0 -or
    $topDownGravity -ne 0x20 -or
    $topDownReducedGravity -ne 0x0a -or
    $topDownMaximumSpeed -ne 0x0300) {
    throw 'Top-down Link air constants lost an expected value.'
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\top_down_air_constants.tsv'),
    $topDownAirRows)

$ledgeCollisionModes = @{
    overworld = 0
    indoors = 1
    dungeons = 2
    sidescrolling = 3
    underwater = 4
    five = 5
}
$ledgeAngles = @{
    UP = 0x00
    RIGHT = 0x08
    DOWN = 0x10
    LEFT = 0x18
}

$ledgeCliffRows = [Collections.Generic.List[string]]::new()
$ledgeCliffRows.Add("# active-collisions`ttile`tangle")
$activeLabels = [Collections.Generic.List[string]]::new()
foreach ($line in $cliffTileSource) {
    if ($line -match '^\s*@(?<label>[A-Za-z0-9_]+):') {
        $label = $Matches['label']
        if ($ledgeCollisionModes.ContainsKey($label)) {
            $activeLabels.Add($label)
        }
        continue
    }
    if ($activeLabels.Count -eq 0) { continue }
    if ($line -match '^\s*\.db\s+\$00\s*$') {
        $activeLabels.Clear()
        continue
    }
    if ($line -notmatch '^\s*\.db\s+\$(?<tile>[0-9a-f]{2})\s*,\s*ANGLE_(?<direction>UP|RIGHT|DOWN|LEFT)\s*$') {
        continue
    }
    $tile = [Convert]::ToInt32($Matches['tile'], 16)
    $angle = $ledgeAngles[$Matches['direction']]
    foreach ($label in $activeLabels) {
        $ledgeCliffRows.Add(
            "$($ledgeCollisionModes[$label])`t$($tile.ToString('x2'))`t$($angle.ToString('x2'))")
    }
}
if ($ledgeCliffRows.Count -ne 39 -or
    ($ledgeCliffRows | Where-Object { $_ -eq "0`t05`t10" }).Count -ne 1 -or
    ($ledgeCliffRows | Where-Object { $_ -eq "4`tff`t10" }).Count -ne 1 -or
    ($ledgeCliffRows | Where-Object { $_ -eq "2`tc4`t08" }).Count -ne 1) {
    throw 'Could not export all 38 collision-set-specific Ages cliff tile rows.'
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\ledge_cliff_tiles.tsv'),
    $ledgeCliffRows)

$tileIndices = @{}
foreach ($line in $tileIndexSource) {
    if ($line -match '^\s*\.define\s+(?<name>[A-Za-z0-9_]+)\s+\$(?<value>[0-9a-f]{2})') {
        $tileIndices[$Matches['name']] =
            [Convert]::ToInt32($Matches['value'], 16)
    }
}
$ledgeLandableRows = [Collections.Generic.List[string]]::new()
$ledgeLandableRows.Add("# active-collisions`ttile")
$activeLabels = [Collections.Generic.List[string]]::new()
foreach ($line in $landableTileSource) {
    if ($line -match '^\s*@(?<label>[A-Za-z0-9_]+):') {
        $label = $Matches['label']
        if ($ledgeCollisionModes.ContainsKey($label)) {
            $activeLabels.Add($label)
        }
        continue
    }
    if ($activeLabels.Count -eq 0) { continue }
    if ($line -match '^\s*\.db\s+\$00\s*$') {
        $activeLabels.Clear()
        continue
    }
    if ($line -notmatch '^\s*\.db\s+(?<tiles>[A-Za-z0-9_\s]+)\s*$') {
        continue
    }
    $tokens = @($Matches['tiles'] -split '\s+' |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    foreach ($token in $tokens) {
        if (-not $tileIndices.ContainsKey($token)) {
            throw "Unknown landable-from-cliff tile constant '$token'."
        }
        $tile = $tileIndices[$token]
        foreach ($label in $activeLabels) {
            $ledgeLandableRows.Add(
                "$($ledgeCollisionModes[$label])`t$($tile.ToString('x2'))")
        }
    }
}
if ($ledgeLandableRows.Count -ne 7 -or
    ($ledgeLandableRows | Where-Object { $_ -eq "1`t0e" }).Count -ne 1 -or
    ($ledgeLandableRows | Where-Object { $_ -eq "5`t0f" }).Count -ne 1) {
    throw 'Could not export the six raisable-floor cliff landing exceptions.'
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\ledge_landable_tiles.tsv'),
    $ledgeLandableRows)

$wallDirectionMatch = [regex]::Match(
    $linkSource,
    '(?ms)^@wallDirections:\s*(?<body>(?:\s*\.db\s+\$[0-9a-f]{2}(?:\s+\$[0-9a-f]{2}){4}\s*;\s*DIR_(?:UP|RIGHT|DOWN|LEFT)\s*){4})')
if (-not $wallDirectionMatch.Success) {
    throw 'Could not parse checkLinkJumpingOffCliff@wallDirections.'
}
$ledgeDirectionRows = [Collections.Generic.List[string]]::new()
$ledgeDirectionRows.Add(
    "# direction`tangle`twall-mask`tprobe1-y`tprobe1-x`tprobe2-y`tprobe2-x")
$directionIndex = 0
foreach ($line in ($wallDirectionMatch.Groups['body'].Value -split "`r?`n")) {
    if ($line -notmatch '^\s*\.db\s+\$(?<mask>[0-9a-f]{2})\s+\$(?<y1>[0-9a-f]{2})\s+\$(?<x1>[0-9a-f]{2})\s+\$(?<y2>[0-9a-f]{2})\s+\$(?<x2>[0-9a-f]{2})\s*;\s*DIR_(?<direction>UP|RIGHT|DOWN|LEFT)\s*$') {
        continue
    }
    if ($directionIndex -ne @('UP', 'RIGHT', 'DOWN', 'LEFT').IndexOf(
            $Matches['direction'])) {
        throw 'Ledge wall-direction rows were not in source direction order.'
    }
    $signed = foreach ($field in @('y1', 'x1', 'y2', 'x2')) {
        $value = [Convert]::ToInt32($Matches[$field], 16)
        if ($value -ge 0x80) { $value - 0x100 } else { $value }
    }
    $ledgeDirectionRows.Add(
        "$directionIndex`t$($ledgeAngles[$Matches['direction']].ToString('x2'))" +
        "`t$($Matches['mask'])`t$($signed[0])`t$($signed[1])`t$($signed[2])`t$($signed[3])")
    $directionIndex++
}
if ($directionIndex -ne 4) {
    throw 'Could not export all four ledge wall-direction probe rows.'
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\ledge_jump_directions.tsv'),
    $ledgeDirectionRows)

$cliffSpeedMatch = [regex]::Match(
    $linkSource,
    '(?ms)^@cliffSpeedTable:\s*(?<body>.*?)(?=^\s*; In the process of falling down the cliff)')
if (-not $cliffSpeedMatch.Success) {
    throw 'Could not parse linkState12@cliffSpeedTable.'
}
$speedNames = [regex]::Matches(
    $cliffSpeedMatch.Groups['body'].Value,
    'SPEED_[0-9a-f]+') | ForEach-Object { $_.Value }
if ($speedNames.Count -ne 11) {
    throw "Expected 11 ledge cliff speeds, got $($speedNames.Count)."
}
$ledgeSpeedRows = [Collections.Generic.List[string]]::new()
$ledgeSpeedRows.Add("# length`tspeed-raw")
for ($index = 0; $index -lt $speedNames.Count; $index++) {
    $name = $speedNames[$index]
    if (-not $objectSpeeds.ContainsKey($name)) {
        throw "Unknown ledge object speed '$name'."
    }
    $ledgeSpeedRows.Add(
        "$($index + 1)`t$($objectSpeeds[$name].ToString('x2'))")
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\ledge_jump_speeds.tsv'),
    $ledgeSpeedRows)

function Resolve-SoundValue([string]$name) {
    foreach ($line in $soundSource) {
        if ($line -match "^\s*$name\s+db\s*;\s*\`$(?<value>[0-9a-f]{2})") {
            return [Convert]::ToInt32($Matches['value'], 16)
        }
    }
    throw "Could not resolve sound constant $name."
}

$initialSpeedMatch = [regex]::Match(
    $linkSource,
    '(?ms)^checkLinkJumpingOffCliff:.*?ld bc,-\$(?<value>[0-9a-f]{3})\s+call objectSetSpeedZ')
$scanMatch = [regex]::Match(
    $linkSource,
    '(?ms)^@getLengthOfCliff:.*?ldi a,\(hl\)\s+add \$(?<feet>[0-9a-f]{2}).*?cp \$(?<max>[0-9a-f]{2}).*?@offsets:\s+\.db \$(?<up>[0-9a-f]{2}) \$00')
if (-not $initialSpeedMatch.Success -or
    $linkSource -notmatch '(?ms)^@willTransition:.*?ld l,SpecialObject\.speedZ\s+ldi \(hl\),a\s+ld \(hl\),\$ff' -or
    $linkSource -notmatch '(?ms)^@substate1:.*?ld c,\$20\s+call objectUpdateSpeedZ_paramC' -or
    $linkSource -notmatch '(?ms)^@substate2:.*?ld c,\$20\s+call objectUpdateSpeedZ_paramC' -or
    -not $scanMatch.Success -or
    [Convert]::ToInt32($scanMatch.Groups['up'].Value, 16) -ne 0xf8) {
    throw 'Could not verify linkState12 ledge Z physics and eight-pixel landing scan.'
}
$jumpAnimationMatch = [regex]::Match(
    $linkAnimationSource,
    '(?ms)^animationData19f78:\s+\.db \$(?<d0>[0-9a-f]{2}) \$e4 \$00\s+\.db \$(?<d1>[0-9a-f]{2}) \$e8 \$00\s+\.db \$(?<d2>[0-9a-f]{2}) \$ec \$00\s+\.db \$7f \$80 \$ff')
if (-not $jumpAnimationMatch.Success) {
    throw 'Could not verify LINK_ANIM_MODE_JUMP animationData19f78.'
}
$ledgeConstantRows = @(
    "# key`tvalue",
    "initial-speed-z`t-$([Convert]::ToInt32($initialSpeedMatch.Groups['value'].Value, 16))",
    "transition-speed-z`t-256",
    "gravity`t32",
    "jump-sound`t$(Resolve-SoundValue 'SND_JUMP')",
    "land-sound`t$(Resolve-SoundValue 'SND_LAND')",
    "feet-offset`t$([Convert]::ToInt32($scanMatch.Groups['feet'].Value, 16))",
    "scan-step`t8",
    "max-speed-length`t$([Convert]::ToInt32($scanMatch.Groups['max'].Value, 16))",
    "animation-phase-0`t$([Convert]::ToInt32($jumpAnimationMatch.Groups['d0'].Value, 16))",
    "animation-phase-1`t$([Convert]::ToInt32($jumpAnimationMatch.Groups['d1'].Value, 16))",
    "animation-phase-2`t$([Convert]::ToInt32($jumpAnimationMatch.Groups['d2'].Value, 16))"
)
Write-GeneratedTable(
    (Join-Path $destination 'metadata\ledge_jump_constants.tsv'),
    $ledgeConstantRows)

# Preserve checkTileValidForEnemySpawn's collision-mode-specific exceptions.
# The routine rejects every nonzero collision byte first, then consults this
# table for metatiles which remain forbidden despite having collision $00.
$enemyUnspawnableSource = Read-ImportLines (
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
Write-GeneratedBytes(
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
$treasureObjectSource = Read-ImportText (Join-Path $Disassembly "data\ages\treasureObjectData.s")
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

# Common tile interactions have three dialogue fallbacks and one missing-chest
# data fallback outside the room-specific sign/chest tables. Preserve the
# handler identities and resolve getChestData's raw $2800 return through the
# corresponding generated TREASURE_OBJECT_RUPEES_00 record.
$interactableTileSource = Read-ImportText (
    Join-Path $Disassembly 'code\interactableTiles.s')
$chestWrongSide = [regex]::Match(
    $interactableTileSource,
    '(?ms)^nextToChestTile:.*?call checkFacingBottomOfTileAndPressedA\s+jr z,\+\+.*?ld bc,TX_(?<text>[0-9a-f]{4})')
$signBlock = [regex]::Match(
    $interactableTileSource,
    '(?ms)^nextToSignTile:(?<body>.*?)(?=^;;)')
$signWrongSide = [regex]::Match(
    $signBlock.Groups['body'].Value,
    'ld bc,TX_(?<text>[0-9a-f]{4})\s+jr nz,@showText')
$signNoMatch = [regex]::Match(
    $signBlock.Groups['body'].Value,
    '(?m)^@noMatch:\s*\r?\n\s*ld bc,TX_(?<text>[0-9a-f]{4})')
$chestLookupSource = Read-ImportText (
    Join-Path $Disassembly 'code\bank0.s')
$missingChest = [regex]::Match(
    $chestLookupSource,
    '(?ms)^getChestData:.*?^@chestNotFound:\s*\r?\n\s*ld bc,\$(?<contents>[0-9a-f]{4})')
if (-not $chestWrongSide.Success -or
    -not $signBlock.Success -or
    -not $signWrongSide.Success -or
    -not $signNoMatch.Success -or
    -not $missingChest.Success -or
    $chestWrongSide.Groups['text'].Value -ne '510d' -or
    $signWrongSide.Groups['text'].Value -ne '510e' -or
    $signNoMatch.Groups['text'].Value -ne '0901' -or
    $missingChest.Groups['contents'].Value -ne '2800') {
    throw 'Common chest/sign fallback handlers no longer select TX_510d, TX_510e, TX_0901, and chest contents $2800.'
}

$tileInteractionFallbackRows = [Collections.Generic.List[string]]::new()
$tileInteractionFallbackRows.Add(
    "# kind`ttext-id`ttreasure-object`ttreasure-id`tsubid`tparameter`tgraphic`tamount`tmessage-base64`tsource")
foreach ($fallback in @(
    @{
        Kind = 'chest-wrong-side'
        Text = $chestWrongSide.Groups['text'].Value
        Source = 'code/interactableTiles.s:nextToChestTile/TX_510d'
    },
    @{
        Kind = 'sign-wrong-side'
        Text = $signWrongSide.Groups['text'].Value
        Source = 'code/interactableTiles.s:nextToSignTile/TX_510e'
    },
    @{
        Kind = 'sign-no-match'
        Text = $signNoMatch.Groups['text'].Value
        Source = 'code/interactableTiles.s:nextToSignTile@noMatch/TX_0901'
    }
)) {
    $textId = [Convert]::ToInt32($fallback.Text, 16)
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve common tile-interaction text TX_$($fallback.Text)."
    }
    $encoded = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $tileInteractionFallbackRows.Add([string]::Join(
        "`t",
        @(
            $fallback.Kind,
            $fallback.Text,
            '',
            '',
            '',
            '',
            '',
            '',
            $encoded,
            $fallback.Source)))
}

$missingChestObject = 'TREASURE_OBJECT_RUPEES_00'
if (-not $treasureObjectRecords.ContainsKey($missingChestObject)) {
    throw "Could not resolve getChestData's default $missingChestObject."
}
$missingChestRecord = $treasureObjectRecords[$missingChestObject]
$missingChestContents = [Convert]::ToInt32(
    $missingChest.Groups['contents'].Value, 16)
if ((($missingChestRecord.Treasure -shl 8) -bor
        $missingChestRecord.Subid) -ne $missingChestContents -or
    $missingChestRecord.Parameter -ge $rupeeValues.Count) {
    throw "$missingChestObject no longer resolves getChestData's `$2800 default."
}
$missingChestAmount = $rupeeValues[$missingChestRecord.Parameter]
$missingChestMessage = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($missingChestRecord.Message))
$tileInteractionFallbackRows.Add([string]::Join(
    "`t",
    @(
        'chest-no-match',
        $missingChestRecord.TextId.ToString('x4'),
        $missingChestObject,
        $missingChestRecord.Treasure.ToString('x2'),
        $missingChestRecord.Subid.ToString('x2'),
        $missingChestRecord.Parameter.ToString('x2'),
        $missingChestRecord.Graphic.ToString('x2'),
        $missingChestAmount,
        $missingChestMessage,
        'code/bank0.s:getChestData@chestNotFound+data/ages/treasureObjectData.s:TREASURE_OBJECT_RUPEES_00')))
if ($tileInteractionFallbackRows.Count -ne 5) {
    throw "Expected four common tile-interaction fallback rows, got $($tileInteractionFallbackRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\tile_interaction_fallbacks.tsv'),
    $tileInteractionFallbackRows)

$chestRows = [Collections.Generic.List[string]]::new()
$chestRows.Add("# group`troom`tposition`ttreasure-object`ttreasure-id`tsubid`tparameter`ttext-id`tgraphic`tamount`tutf8-base64")
$currentChestGroup = -1
foreach ($line in Read-ImportLines (Join-Path $Disassembly "data\ages\chestData.s")) {
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
Write-GeneratedTable($chestPath, $chestRows)
