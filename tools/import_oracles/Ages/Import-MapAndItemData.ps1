# updateTilesetFlagsForIndoorRoomInAltWorld ORs the source bit into
# TILESETFLAG_PAST; the retained minimap group is not an era selector.
$altWorldPath = Join-Path $Disassembly 'data\ages\roomsInAltWorld.s'
$altWorldBytes = [Collections.Generic.List[byte]]::new()
$altWorldLabels = @(Read-AssemblyLabels $altWorldPath | Where-Object { $_.Name -match '^roomsInAltWorldGroup[0-7]$' })
for ($group = 0; $group -lt 8; $group++) {
    $before = $altWorldBytes.Count
    $labelIndex = 0
    while ($labelIndex -lt $altWorldLabels.Count -and $altWorldLabels[$labelIndex].Name -ne "roomsInAltWorldGroup$group") { $labelIndex++ }
    $altRows = @()
    # Adjacent global labels are aliases (groups $00/$01, $04/$06, $05/$07).
    while ($labelIndex -lt $altWorldLabels.Count -and $altRows.Count -eq 0) {
        $altRows = @(Read-AssemblyMacroInvocations $altWorldPath $altWorldLabels[$labelIndex].Name 'dbrev')
        $labelIndex++
    }
    foreach ($node in $altRows) {
        foreach ($operand in $node.Operands) {
            if ($operand -notmatch '^%[01]{8}$') {
                throw "$altWorldPath`:$($node.Line): expected an eight-bit dbrev operand, got '$operand'."
            }
            $value = 0
            for ($bit = 0; $bit -lt 8; $bit++) {
                if ($operand[$bit + 1] -eq '1') { $value = $value -bor (1 -shl $bit) }
            }
            $altWorldBytes.Add([byte]$value)
        }
    }
    if ($altWorldBytes.Count - $before -ne 32) {
        throw "$altWorldPath`:roomsInAltWorldGroup$group did not contain 32 dbrev bytes."
    }
}
Write-GeneratedBytes((Join-Path $destination 'metadata\rooms_in_alt_world.bin'), $altWorldBytes.ToArray())

# checkLinkCanSurface_isUnderwater indexes contiguous words, including the
# unlabeled alternate rows following pollution/Jabu masks. Keep that arena
# intact and resolve consecutive table labels as aliases.
$surfacePath = Join-Path $Disassembly 'data\ages\underwaterSurfaceData.s'
$surfaceNodes = @(Read-AssemblyNodes $surfacePath)
$surfaceOffsets = @{}
$surfaceMasks = [Collections.Generic.List[string]]::new()
$surfaceMasks.Add('# index`tmask`tsource')
$surfaceStarted = $false
$surfaceIndex = 0
foreach ($node in $surfaceNodes) {
    if ($node.Kind -eq 'Label' -and $node.Name -match '^underWaterSurfaceData_[0-9a-f]{4}$') {
        $surfaceStarted = $true
        $surfaceOffsets[$node.Name] = $surfaceIndex
    } elseif ($surfaceStarted -and $node.Kind -eq 'Data') {
        if ($node.Name -ne '.dw' -or $node.Operands.Count -ne 1 -or
            $node.Operands[0] -notmatch '^%[01]{16}$') {
            throw "$surfacePath`:$($node.Line): expected one 16-bit surfacing mask."
        }
        $mask = [Convert]::ToInt32($node.Operands[0].Substring(1), 2)
        $surfaceMasks.Add("$surfaceIndex`t$($mask.ToString('x4'))`tunderwaterSurfaceData.s:$($node.Line)")
        $surfaceIndex++
    } elseif ($surfaceStarted -and $node.Kind -notin @('Blank', 'Comment', 'Directive')) {
        throw "$surfacePath`:$($node.Line): unsupported surfacing data node '$($node.Code)'."
    }
}
if ($surfaceIndex -ne 529) { throw "$surfacePath`: expected 529 contiguous clean-US surfacing words, got $surfaceIndex." }
$surfaceRooms = [Collections.Generic.List[string]]::new()
$surfaceRooms.Add('# group`troom`tmask-index`tsource')
$surfaceGroupLabels = @(Read-AssemblyLabels $surfacePath | Where-Object { $_.Name -match '^underWaterSurfaceTableGroup[0-7]$' })
$surfacePointers = @(Read-AssemblyDataDirectives $surfacePath 'underWaterSurfaceTable' '.dw')
if ($surfacePointers.Count -ne 8) { throw "$surfacePath`: expected eight surfacing group pointers." }
for ($group = 0; $group -lt 8; $group++) {
    if ($surfacePointers[$group].Operands.Count -ne 1 -or
        $surfacePointers[$group].Operands[0] -ne "underWaterSurfaceTableGroup$group") {
        throw "$surfacePath`: unexpected surfacing pointer for group $group."
    }
    $labelIndex = 0
    while ($labelIndex -lt $surfaceGroupLabels.Count -and $surfaceGroupLabels[$labelIndex].Name -ne "underWaterSurfaceTableGroup$group") { $labelIndex++ }
    $rows = @()
    while ($labelIndex -lt $surfaceGroupLabels.Count -and $rows.Count -eq 0) {
        $rows = @(Read-AssemblyDataDirectives $surfacePath $surfaceGroupLabels[$labelIndex].Name)
        $labelIndex++
    }
    $room = -1
    $terminated = $false
    foreach ($node in $rows) {
        if ($terminated -or $node.Operands.Count -ne 1) { throw "$surfacePath`:$($node.Line): malformed surfacing room table." }
        if ($node.Name -eq '.db' -and $room -eq -1) {
            $room = Convert-AssemblyInteger $node.Operands[0]
            if ($room -eq 0) { $terminated = $true; $room = -1 }
        } elseif ($node.Name -eq '.dw' -and $room -gt 0 -and $surfaceOffsets.ContainsKey($node.Operands[0])) {
            $surfaceRooms.Add("$group`t$($room.ToString('x2'))`t$($surfaceOffsets[$node.Operands[0]])`t$($node.Operands[0])")
            $room = -1
        } else { throw "$surfacePath`:$($node.Line): expected room byte followed by a surfacing mask pointer." }
    }
    if (-not $terminated -or $room -ne -1) { throw "$surfacePath`: unterminated surfacing table for group $group." }
}
if ($surfaceRooms.Count -ne 220) { throw "$surfacePath`: expected 219 aliased surfacing room records, got $($surfaceRooms.Count - 1)." }
Write-GeneratedTable((Join-Path $destination 'metadata\underwater_surface_masks.tsv'), $surfaceMasks)
Write-GeneratedTable((Join-Path $destination 'metadata\underwater_surface_rooms.tsv'), $surfaceRooms)

$mapTextRows = [Collections.Generic.List[string]]::new()
$mapTextRows.Add('# text-id`tposition`tmessage-base64')
function Resolve-MapText([int]$textId, [Collections.Generic.HashSet[int]]$visited) {
    if (-not $allTexts.ContainsKey($textId) -or -not $visited.Add($textId)) {
        throw "text/ages/text.yaml: missing or recursive map TX_$($textId.ToString('x4'))."
    }
    try {
        $remaining = [string]$allTexts[$textId]
        $message = ''
        # Resolve commands in execution order. A jump ends the current stream;
        # calls return to its suffix. Dynamic \call(0xfd) remains symbolic for
        # the Gale owner, which supplies the selected area's resolved message.
        while ($true) {
            $command = [regex]::Match($remaining, '\\(?<kind>call|jump)\(TX_(?<id>[0-9a-f]{4})\)')
            if (-not $command.Success) { break }
            $target = [Convert]::ToInt32($command.Groups['id'].Value, 16)
            $message += $remaining.Substring(0, $command.Index) + (Resolve-MapText $target $visited)
            if ($command.Groups['kind'].Value -eq 'jump') { return $message }
            $remaining = $remaining.Substring($command.Index + $command.Length)
        }
        $message += $remaining
        if ($allTextFallthroughIds.ContainsKey($textId)) {
            # The trailing source newline is a text control, not a terminator.
            if ($message.EndsWith('\n', [StringComparison]::Ordinal)) {
                $message = $message.Substring(0, $message.Length - 2) + "`n"
            }
            $message += Resolve-MapText $allTextFallthroughIds[$textId] $visited
        }
        return $message
    } finally {
        [void]$visited.Remove($textId)
    }
}
foreach ($textId in @($allTexts.Keys | Sort-Object)) {
    if ($textId -lt 0x0200 -or $textId -ge 0x0600) { continue }
    $position = if ($allTextPositions.ContainsKey($textId)) { $allTextPositions[$textId] } else { 0 }
    $message = Resolve-MapText $textId ([Collections.Generic.HashSet[int]]::new())
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message))
    $mapTextRows.Add("$($textId.ToString('x4'))`t$position`t$encoded")
}
$mapTextsPath = Join-Path $destination 'map\texts.tsv'
Write-GeneratedTable($mapTextsPath, $mapTextRows)

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
$uncmpGfxHeadersSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\uncmpGfxHeaders.s')
$seedShooterPropertiesSource = Read-ImportText (
    Join-Path $Disassembly 'gfx\ages\spr_seed_shooter.properties')
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
$conveyorItemSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tile_properties\conveyorItemTiles.s')
$itemPassableTilesSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tile_properties\itemPassableTiles.s')
$shieldParentSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\shieldParent.s')
$shovelParentSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\shovelParent.s')
$swordItemSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\sword.s')
$itemPostUpdateSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\postUpdate.s')
$itemCommonCode2Source = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\commonCode2.s')
$itemCommonCode1Source = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\items\commonCode1.s')
$specialObjectOamDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\specialObjectOamData.s')
$braceletParentSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\bombsBraceletParent.s')
$parentItemCommonSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\commonCode.s')
$pushBlockSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\pushblock.s')
$objectSpeedsSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
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
        '(?m)^\s*dbrev %11111111 %10000010 %00000000 %00000000 ; 0x18' -or
    $partActiveCollisionsSource -notmatch
        '(?m)^\s*dbrev %11111111 %10000010 %00000000 %00000000 ; 0x1a') {
    throw 'ITEM_SHIELD or ridden Link animation data, graphics, hitbox, sounds, or supported projectile collisions changed in the disassembly.'
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

$throwSpeeds = @{}
$throwSpeedOffset = 0
foreach ($match in [regex]::Matches($objectSpeedsSource, '(?m)^\s*(SPEED_[0-9a-f]+)\s+dsb (\d+)')) {
    $throwSpeeds[$match.Groups[1].Value] = $throwSpeedOffset
    $throwSpeedOffset += [int]$match.Groups[2].Value
}
foreach ($alias in Read-AssemblyConstants (Join-Path $Disassembly 'constants/common/objectSpeeds.s')) {
    if ($throwSpeeds.ContainsKey($alias.OperandText)) { $throwSpeeds[$alias.Name] = $throwSpeeds[$alias.OperandText] }
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

# ITEM_SWITCH_HOOK owns a weapon, a cycling chain item, and reserved helper $09.
# The directional latch animations are advanced only by state3/substate0;
# their final $80 parameter is a control-flow input, not a deletion marker.
function Export-SwitchHookData {
$hookPath = Join-Path $Disassembly 'object_code\ages\items\switchHook.s'
$hookSource = Read-ImportText $hookPath
$hookParent = Read-ImportText (Join-Path $Disassembly 'object_code\common\itemParents\switchHookParent.s')
$hookTileConstants = Read-ImportText (Join-Path $Disassembly 'constants\common\tileIndices.s')
$hookSpeedConstants = Read-ImportText (Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$hookBreakSources = Read-ImportText (Join-Path $Disassembly 'constants\common\breakableTileSources.s')
$hookOffsets = @(Read-AssemblyLiteralValues $hookPath '@offsetsTable')
$hookPlacementOffsets = @(Read-AssemblyLiteralValues $hookPath '@data')
if ($hookOffsets.Count -ne 12 -or $hookPlacementOffsets.Count -ne 4 -or
    $itemIds['ITEM_SWITCH_HOOK'] -ne 0x0a -or $itemIds['ITEM_SWITCH_HOOK_CHAIN'] -ne 0x0b -or
    $itemIds['ITEM_SWITCH_HOOK_HELPER'] -ne 0x09 -or
    $hookBreakSources -notmatch 'BREAKABLETILESOURCE_SWITCH_HOOK:\s+db ; 0x08' -or
    $hookTileConstants -notmatch 'TILEINDEX_SWITCH_DIAMOND\s+\$db' -or
    $hookTileConstants -notmatch 'TILEINDEX_SOMARIA_BLOCK\s+\$da' -or
    $hookSpeedConstants -notmatch 'SPEED_200\s+dsb 5 ; 0x50' -or
    $hookSpeedConstants -notmatch 'SPEED_300\s+dsb 5 ; 0x78' -or
    $hookSource -notmatch 'ldbc SPEED_200,\$29' -or
    $hookSource -notmatch 'ldbc SPEED_300,\$26' -or
    $hookSource -notmatch '(?ms)^@fullyRetracted:.*?itemDecCounter1.*?itemDelete' -or
    $hookSource -notmatch '(?ms)call itemIncSubstate\s*; Set Item.counter1 to \$03\s*inc l\s*ld \(hl\),\$03' -or
    $hookSource -notmatch '(?ms)^updateSwitchHookSound:.*?and \$03.*?ret z.*?SND_SWITCH_HOOK' -or
    $hookSource -notmatch '(?ms)^@s3subState1:.*?dec \(hl\).*?cp \$f1.*?call c,itemIncSubstate' -or
    $hookSource -notmatch '(?ms)^@s3subState3:.*?inc \(hl\).*?@updateOtherPositions' -or
    $hookParent -notmatch 'parentItemLoadAnimationAndIncState' -or
    $hookParent -notmatch '(?ms)call isLinkUnderwater\s+ret z\s+ld a,LINK_ANIM_MODE_2e\s+jp specialObjectSetAnimationWithLinkData' -or
    @([regex]::Matches([regex]::Match($specialObjectAnimationsSource,
        '(?ms)^specialObject00AnimationDataPointers:\s*specialObject09AnimationDataPointers:\s*(?<body>.*?)(?=^specialObject01AnimationDataPointers:)').Groups['body'].Value,
        '\.dw (\w+)') | ForEach-Object { $_.Groups[1].Value })[0x2e] -ne 'animationData1a063' -or
    $specialObjectAnimationsSource -notmatch '(?ms)^animationData1a063:\s*\.db \$03 \$c0 \$06\s*\.db \$7f \$c0 \$86' -or
    $uncmpGfxHeadersSource -notmatch '(?ms)^uncmpGfxHeader1f:\s*m_GfxHeader spr_switch_hook, \$8521') {
    throw 'Switch Hook source offsets, flight/exchange counters, parent animation or graphics header changed.'
}
$hookRows = [Collections.Generic.List[string]]::new()
$hookRows.Add("# level`titem-id`tchain-id`thelper-id`tspeed-raw`textension-frames`tretracted-frames`tlift-frames`tsound-mask`tflight-sound`texchange-sound`tdiamond-tile`tsomaria-tile`tbreak-source`tsource")
foreach ($level in 1..2) {
    $hookSpeed = if ($level -eq 1) { 80 } else { 120 }
    $hookFrames = if ($level -eq 1) { 41 } else { 38 }
    $hookRows.Add("$level`t0a`t0b`t09`t$hookSpeed`t$hookFrames`t3`t16`t3`t$($soundIds['SND_SWITCH_HOOK'].ToString('x2'))`t$($soundIds['SND_SWITCH2'].ToString('x2'))`tdb`tda`t08`tobject_code/ages/items/switchHook.s:itemCode0a;switchHookState3")
}
Write-GeneratedTable((Join-Path $destination 'metadata\switch_hook.tsv'), $hookRows)
$hookOffsetRows = [Collections.Generic.List[string]]::new()
$hookOffsetRows.Add("# direction`ty`tx`tz`tplacement-offset`tsource")
for ($direction = 0; $direction -lt 4; $direction++) {
    $offset = @($hookOffsets[($direction * 3)..($direction * 3 + 2)] | ForEach-Object {
        if ($_ -ge 128) { $_ - 256 } else { $_ }
    })
    $placement = $hookPlacementOffsets[$direction]
    if ($placement -ge 128) { $placement -= 256 }
    $hookOffsetRows.Add("$direction`t$($offset[0])`t$($offset[1])`t$($offset[2])`t$placement`tobject_code/ages/items/switchHook.s:itemCode0a@offsetsTable;switchHookState3@data")
}
Write-GeneratedTable((Join-Path $destination 'metadata\switch_hook_offsets.tsv'), $hookOffsetRows)
$wallSource = Read-ImportText (Join-Path $Disassembly 'object_code\common\specialObjects\link.s')
$wallBlock = [regex]::Match($wallSource, '(?ms)^calculateAdjacentWallsBitset:.*?^@overworldOffsets:(?<top>.*?)^@sidescrollOffsets:(?<side>.*?)(?=^\.ifdef)')
if (-not $wallBlock.Success) { throw 'Missing Link adjacent-wall probe tables used by Switch Hook.' }
$wallRows = [Collections.Generic.List[string]]::new()
$wallRows.Add("# sidescroll`tindex`ty`tx`tsource")
foreach ($side in 0..1) {
    $group = if ($side -eq 0) { 'top' } else { 'side' }
    $pairs = [regex]::Matches($wallBlock.Groups[$group].Value, '(?m)^\s*\.db\s+(?<y>-?\d+),\s*(?<x>-?\d+)\s*$')
    if ($pairs.Count -ne 8) { throw 'Expected eight cumulative Link adjacent-wall probes.' }
    for ($i = 0; $i -lt 8; $i++) {
        $wallRows.Add("$side`t$i`t$($pairs[$i].Groups['y'].Value)`t$($pairs[$i].Groups['x'].Value)`tobject_code/common/specialObjects/link.s:calculateAdjacentWallsBitset")
    }
}
Write-GeneratedTable((Join-Path $destination 'metadata\switch_hook_wall_probes.tsv'), $wallRows)
$hookAnimationPath = Join-Path $Disassembly 'data\itemAnimations.s'
$hookTables = Read-AssemblyDwTables $hookAnimationPath 'item[0-9a-f]{2}Animations' 'itemAnimation[0-9a-f]+'
$hookPointers = Read-AssemblyDwTables $hookAnimationPath 'item[0-9a-f]{2}OamDataPointers' 'itemOamData[0-9a-f]+'
$hookDefinitions = Read-AssemblyAnimationDefinitions $hookAnimationPath 'itemAnimation[0-9a-f]+(?:Loop)?' $true
$hookAnimationRows = [Collections.Generic.List[string]]::new()
$hookAnimationRows.Add("# item-id`tanimation`tsprite`ttile-base`toam-flags`tcollision`tradius-y`tradius-x`tdamage`thealth`tframes`tsource")
foreach ($item in @('0a', '0b')) {
    $hookGfx = [regex]::Match($itemDataSource,
        "(?m)^\s*\.db \`$00 \`$(?<tile>[0-9a-f]{2}) \`$(?<flags>[0-9a-f]{2}) ; \`$${item}:")
    $hookAttributes = [regex]::Match($itemAttributesSource,
        "(?m)^\s*\.db \`$(?<collision>[0-9a-f]{2}) \`$(?<radius>[0-9a-f]{2}) \`$(?<damage>[0-9a-f]{2}) \`$(?<health>[0-9a-f]{2}) ; \`$${item}:")
    if (-not $hookGfx.Success -or -not $hookAttributes.Success) { throw "Could not read ITEM_`$$item attributes." }
    $radius = [Convert]::ToInt32($hookAttributes.Groups['radius'].Value, 16)
    $count = if ($item -eq '0a') { 6 } else { 1 }
    for ($animation = 0; $animation -lt $count; $animation++) {
        $label = $hookTables["item${item}Animations"][$animation]
        $definition = $hookDefinitions[$label]
        $encoded = @($definition.Frames | ForEach-Object {
            if (($_.PointerOffset -band 1) -ne 0) { throw "$label has an odd OAM pointer offset." }
            $oam = $hookPointers["item${item}OamDataPointers"][[int]($_.PointerOffset / 2)]
            "$($_.Duration),$($_.Parameter)@$(Read-ItemOamComposition $oam)"
        }) -join '|'
        if (($item -eq '0a' -and $animation -ge 2) -and
            ($definition.Frames.Count -ne 6 -or $definition.Frames[-1].Parameter -ne 0x80)) {
            throw "$label must retain all six latch frames and the final exchange parameter `$80."
        }
        # itemLoadGraphics/addIndexToLoadedObjectGfx leaves GFX index $00 in
        # resident VRAM. The chain's flags $09 select bank 1, where
        # GFXH_COMMON_SPRITES loads spr_common_sprites at $8000. Tile $16 on
        # the dynamically loaded spr_common_items sheet is a Pegasus Seed.
        if ($item -eq '0b' -and (
            $hookGfx.Groups['flags'].Value -ne '09' -or
            $gfxHeadersSource -notmatch '(?m)^\s*m_GfxHeader\s+spr_common_sprites,\s*\$8001')) {
            throw 'ITEM_SWITCH_HOOK_CHAIN $0b: expected resident bank-1 graphics from gfxHeaders.s:GFXH_COMMON_SPRITES at $8001.'
        }
        $sprite = if ($item -eq '0a') { 'spr_switch_hook' } else { 'spr_common_sprites' }
        $hookAnimationRows.Add("$item`t$animation`t$sprite`t$($hookGfx.Groups['tile'].Value)`t$($hookGfx.Groups['flags'].Value)`t$($hookAttributes.Groups['collision'].Value)`t$($radius -shr 4)`t$($radius -band 15)`t$($hookAttributes.Groups['damage'].Value)`t$($hookAttributes.Groups['health'].Value)`t$encoded`tdata/itemAnimations.s:$label;data/ages/itemAttributes.s:ITEM_`$$item")
    }
}
Write-GeneratedTable((Join-Path $destination 'metadata\switch_hook_animations.tsv'), $hookAnimationRows)
}
Export-SwitchHookData


# Somaria's creation geometry and raw tile ownership precede its presentation.
$somariaPath = Join-Path $Disassembly 'object_code/ages/items/caneOfSomaria.s'
$somariaSource = Read-ImportText $somariaPath
foreach ($contract in @(
    'w1ParentItem2.animParameter\)\s+cp \$06',
    'findItemWithID.*?Item.var2f\s+set 5,\(hl\).*?getFreeItemSlot',
    '@checkBlockCanAppear:.*?cp >ROOM_AGES_5e8.*?cp <ROOM_AGES_5e8.*?Item.zh.*?dec a\s+cp \$fc',
    '@checkBlockCanAppear:.*?objectGetTileCollisions\s+ret nz.*?TILESETFLAG_BIT_UNDERWATER.*?TILESETFLAG_SIDESCROLL.*?add \$10.*?cp \$0f',
    '@createBlockIfNotOnHazard:.*?@alignOnTile.*?hazardCollisionTable.*?TILEINDEX_SOMARIA_BLOCK.*?wRoomCollisions.*?\$0f.*?setTileInRoomLayoutBuffer',
    '@removeBlock:.*?@checkBlockInPlace\s+ret nz.*?getTileIndexFromRoomLayoutBuffer\s+jp setTile',
    '@alignOnTile:\s+call objectCenterOnTile\s+ld l,Item.yh\s+dec \(hl\)\s+dec \(hl\)'
)) {
    if ($somariaSource -notmatch "(?s)$contract") { throw "caneOfSomaria.s source contract changed: $contract" }
}
$somariaTileConstants = Read-ImportText (Join-Path $Disassembly 'constants/common/tileIndices.s')
if ($somariaTileConstants -notmatch '(?m)^\.define TILEINDEX_SOMARIA_BLOCK\s+\$(?<tile>[0-9a-f]{2})') {
    throw 'Missing TILEINDEX_SOMARIA_BLOCK constant.'
}
$somariaTile = $Matches['tile']
Write-GeneratedTable((Join-Path $destination 'metadata/somaria_placement.tsv'), @(
    "# create-parameter`ttile`tcollision`tz-subtract`tz-boundary`tforbidden-group`tforbidden-room`talign-y`tsource",
    "6`t$somariaTile`t0f`t1`tfc`t5`te8`t-2`tobject_code/ages/items/caneOfSomaria.s:checkBlockCanAppear/createBlockIfNotOnHazard/alignOnTile"
))
$somariaOffsets = @(Read-AssemblyDataDirectives $somariaPath '@somariaCreationOffsets' '.dw')
if ($somariaOffsets.Count -ne 4) { throw 'Somaria creation requires four ordered Y/X words.' }
$somariaRows = [Collections.Generic.List[string]]::new()
$somariaRows.Add("# direction`ty`tx`tsource")
for ($i=0; $i -lt 4; $i++) {
    $word = Convert-AssemblyInteger $somariaOffsets[$i].Operands[0]
    $y = $word -band 255; $x = $word -shr 8
    if ($y -ge 128) { $y -= 256 }; if ($x -ge 128) { $x -= 256 }
    $somariaRows.Add("$i`t$y`t$x`tobject_code/ages/items/caneOfSomaria.s:somariaCreationOffsets+$($i*2)")
}
Write-GeneratedTable((Join-Path $destination 'metadata/somaria_creation_offsets.tsv'), $somariaRows)

# ITEM18 state1 advances through the next label until parameter1; states3
# and2 hold their poses without calling itemAnimate. Keep that executable
# fallthrough, but do not import unrelated following item animations.
function Export-SomariaGraphics {
    $path = Join-Path $Disassembly 'data/itemAnimations.s'
    $tables = Read-AssemblyDwTables $path 'item[0-9a-f]{2}Animations' 'itemAnimation[0-9a-f]+'
    $pointers = Read-AssemblyDwTables $path 'item[0-9a-f]{2}OamDataPointers' 'itemOamData[0-9a-f]+'
    $definitions = Read-AssemblyAnimationDefinitions $path 'itemAnimation[0-9a-f]+(?:Loop)?' $true
    if ($tables['item04Animations'].Count -ne 8 -or $tables['item18Animations'].Count -ne 3 -or
        $uncmpGfxHeadersSource -notmatch '(?ms)^uncmpGfxHeader1c:\s*m_GfxHeader spr_cane_of_somaria, \$8521' -or
        $somariaSource -notmatch '(?ms)Item.oamFlagsBackup\s+ld a,\$0d\s+ldi \(hl\),a\s+ldi \(hl\),a\s+; Item.oamTileIndexBase\s+ld \(hl\),\$36') {
        throw 'Somaria item04/item18 animation tables or weapon/block VRAM mapping changed.'
    }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# item-id`tanimation`tsprite`ttile-base`toam-flags`tcollision`tradius-y`tradius-x`tdamage`thealth`tframes`tsource")
    foreach ($item in @('04','18')) {
        $gfx = [regex]::Match($itemDataSource,
            "(?m)^\s*\.db \`$00 \`$(?<tile>[0-9a-f]{2}) \`$(?<flags>[0-9a-f]{2}) ; \`$${item}:")
        $attributes = [regex]::Match($itemAttributesSource,
            "(?m)^\s*\.db \`$(?<collision>[0-9a-f]{2}) \`$(?<radius>[0-9a-f]{2}) \`$(?<damage>[0-9a-f]{2}) \`$(?<health>[0-9a-f]{2}) ; \`$${item}:")
        if (-not $gfx.Success -or -not $attributes.Success) { throw "Missing Somaria ITEM_`$$item graphics/attributes." }
        $radius = [Convert]::ToInt32($attributes.Groups['radius'].Value,16)
        $labels = $tables["item${item}Animations"]
        for ($animation=0; $animation -lt $labels.Count; $animation++) {
            $label = $labels[$animation]
            $frames = @($definitions[$label].Frames)
            if ($item -eq '18' -and $animation -eq 0) {
                $next = $labels[1]
                if ($frames.Count -ne 3 -or $itemAnimationsSource -notmatch
                    "(?ms)^${label}:\s*(?:\.db[^\r\n]+\s*){3}${next}:") {
                    throw "Somaria phase-in $label must fall through directly to $next."
                }
                $frames += @($definitions[$next].Frames)
                if ($frames.Count -ne 4 -or $frames[-1].Parameter -ne 1) {
                    throw "Somaria phase-in $label must reach placement parameter1."
                }
            }
            $encoded = @($frames | ForEach-Object {
                $index = [int]($_.PointerOffset / 2)
                if (($_.PointerOffset -band 1) -ne 0 -or $index -ge $pointers["item${item}OamDataPointers"].Count) {
                    throw "Somaria $label has invalid OAM offset $($_.PointerOffset)."
                }
                "$($_.Duration),$($_.Parameter)@$(Read-ItemOamComposition $pointers["item${item}OamDataPointers"][$index])"
            }) -join '|'
            $sprite = if ($item -eq '04') { 'spr_cane_of_somaria' } else { 'spr_common_sprites' }
            $tile = $gfx.Groups['tile'].Value
            $flags = $gfx.Groups['flags'].Value
            if ($item -eq '18' -and $animation -gt 0) { $tile='36'; $flags='0d' }
            $rows.Add("$item`t$animation`t$sprite`t$tile`t$flags`t$($attributes.Groups['collision'].Value)`t$($radius -shr 4)`t$($radius -band 15)`t$($attributes.Groups['damage'].Value)`t$($attributes.Groups['health'].Value)`t$encoded`tdata/itemAnimations.s:$label;object_code/ages/items/caneOfSomaria.s")
        }
    }
    Write-GeneratedTable((Join-Path $destination 'metadata/somaria_animations.tsv'), $rows)
}
Export-SomariaGraphics

function Export-SomariaParentAnimations {
    $path = Join-Path $Disassembly 'data/ages/specialObjectAnimationData.s'
    $tables = Read-AssemblyDwTables $path 'specialObject(?:00|09)AnimationDataPointers' 'animationData\w+'
    $nodes = @(Read-AssemblyNodes $path)
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# mode`tframe`tduration`tgraphic`tparameter`tsource")
    foreach ($mode in @(0x22,0x26,0x2d)) {
        $label = $tables['specialObject00AnimationDataPointers'][$mode]
        $start = @($nodes | Where-Object { $_.Kind -eq 'Label' -and $_.Name -eq $label })
        if ($start.Count -ne 1) { throw "Somaria parent mode $mode cannot resolve $label." }
        $frame=0; $ended=$false
        foreach ($node in $nodes) {
            if ($node.Offset -le $start[0].Offset -or $node.Kind -in @('Label','Blank','Comment')) { continue }
            if ($node.Kind -ne 'Data' -or $node.Name -ne '.db' -or $node.Operands.Count -ne 3) {
                throw "Somaria parent animation $label encountered unsupported $($node.Kind)/$($node.Name) before terminal parameter."
            }
            $bytes = @($node.Operands | ForEach-Object {
                if ($_ -notmatch '^\$[0-9a-f]{2}$') { throw "Somaria $label has non-byte animation operand $_." }
                [Convert]::ToInt32($_.Substring(1),16)
            })
            $rows.Add("$($mode.ToString('x2'))`t$frame`t$($bytes[0])`t$($bytes[1].ToString('x2'))`t$($bytes[2].ToString('x2'))`tdata/ages/specialObjectAnimationData.s:$label+$frame")
            $frame++
            if (($bytes[2] -band 0x80) -ne 0) { $ended=$true; break }
        }
        if (-not $ended -or $frame -ne $(if ($mode -eq 0x26) {4} else {5})) {
            throw "Somaria parent $label did not terminate after its expected source frames."
        }
    }
    Write-GeneratedTable((Join-Path $destination 'metadata/somaria_parent_animations.tsv'), $rows)
    $selector = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'object_code/common/items/postUpdate.s') '@data')
    if ($selector.Count -ne 24) { throw 'Somaria/sword swing selector requires24 source bytes.' }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# index`tvalue`tsource")
    for ($i=0;$i -lt 24;$i++) {
        $rows.Add("$i`t$($selector[$i].ToString('x2'))`tobject_code/common/items/postUpdate.s:updateSwingableItemAnimation/@data+$i")
    }
    Write-GeneratedTable((Join-Path $destination 'metadata/somaria_swing_selector.tsv'), $rows)
}
Export-SomariaParentAnimations

# ITEM_BIGGORON_SWORD ($0c) has a distinct eight-sector arc. Both normal
# ($23) and mounted ($27) parents end on bit7 before the next animation step.
# Retain animation aliases and literal tables through the shared source model.
$biggoronParentFrames = [Collections.Generic.List[object]]::new()
function Export-BiggoronSwordData {
    $animationsPath = Join-Path $Disassembly 'data/itemAnimations.s'
    $tables = Read-AssemblyDwTables $animationsPath 'item[0-9a-f]{2}Animations' 'itemAnimation[0-9a-f]+'
    $pointers = Read-AssemblyDwTables $animationsPath 'item[0-9a-f]{2}OamDataPointers' 'itemOamData[0-9a-f]+'
    $definitions = Read-AssemblyAnimationDefinitions $animationsPath 'itemAnimation[0-9a-f]+(?:Loop)?' $true
    $gfx = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/itemData.s') 'itemData')
    $attributes = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'data/ages/itemAttributes.s') 'itemAttributes')
    if ($tables['item0cAnimations'].Count -ne 8 -or $pointers['item0cOamDataPointers'].Count -ne 8 -or
        $gfx.Count -lt 39 -or $attributes.Count -lt 52 -or
        $uncmpGfxHeadersSource -notmatch '(?ms)^uncmpGfxHeader1b:\s*m_GfxHeader spr_swords, \$8521, \$0e, \$a0') {
        throw 'ITEM$0c animation aliases, attributes or weapon graphics extent changed.'
    }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# animation`tsprite`ttile-base`toam-flags`tsource-offset`tcollision`tradius-y`tradius-x`tdamage`thealth`tsound`tframes`tsource")
    for ($animation = 0; $animation -lt 8; $animation++) {
        $label = $tables['item0cAnimations'][$animation]
        $frames = @($definitions[$label].Frames)
        if ($frames.Count -ne 1 -or $frames[0].Duration -ne 0x7f) {
            throw "ITEM`$0c $label must retain its inert one-frame pose."
        }
        $frame = $frames[0]
        $oamIndex = [int]($frame.PointerOffset / 2)
        if (($frame.PointerOffset -band 1) -ne 0 -or $oamIndex -ge 8) {
            throw "ITEM`$0c $label references invalid OAM offset $($frame.PointerOffset)."
        }
        $encoded = "$($frame.Duration),$($frame.Parameter)@$(Read-ItemOamComposition $pointers['item0cOamDataPointers'][$oamIndex])"
        $radius = $attributes[0x0c * 4 + 1]
        $rows.Add("$animation`tspr_swords`t$($gfx[0x0c*3+1].ToString('x2'))`t$($gfx[0x0c*3+2].ToString('x2'))`t160`t$($attributes[0x0c*4].ToString('x2'))`t$($radius -shr 4)`t$($radius -band 15)`t$($attributes[0x0c*4+2].ToString('x2'))`t$($attributes[0x0c*4+3].ToString('x2'))`t$($soundIds['SND_BIGSWORD'].ToString('x2'))`t$encoded`tdata/itemAnimations.s:$label;data/ages/itemAttributes.s:ITEM_BIGGORON_SWORD;data/ages/uncmpGfxHeaders.s:uncmpGfxHeader1b")
    }
    Write-GeneratedTable((Join-Path $destination 'metadata/biggoron_sword_animations.tsv'), $rows)
    $arc = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'object_code/common/items/postUpdate.s') 'biggoronSwordArcData')
    if ($arc.Count -ne 32) { throw 'biggoronSwordArcData requires eight ordered four-byte sectors.' }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# index`tradius-y`tradius-x`toffset-y`toffset-x`tsource")
    for ($index = 0; $index -lt 8; $index++) {
        $offset = $index * 4
        $rows.Add("$index`t$($arc[$offset])`t$($arc[$offset+1])`t$(Convert-SignedLinkItemByte $arc[$offset+2])`t$(Convert-SignedLinkItemByte $arc[$offset+3])`tobject_code/common/items/postUpdate.s:biggoronSwordArcData+$offset")
    }
    Write-GeneratedTable((Join-Path $destination 'metadata/biggoron_sword_arcs.tsv'), $rows)
    $path = Join-Path $Disassembly 'data/ages/specialObjectAnimationData.s'
    $parents = Read-AssemblyDwTables $path 'specialObject(?:00|09)AnimationDataPointers' 'animationData\w+'
    $nodes = @(Read-AssemblyNodes $path)
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# mode`tframe`tduration`tgraphic`tparameter`tsource")
    foreach ($mode in @(0x23, 0x27)) {
        $label = $parents['specialObject00AnimationDataPointers'][$mode]
        $start = @($nodes | Where-Object { $_.Kind -eq 'Label' -and $_.Name -eq $label })
        if ($start.Count -ne 1) { throw "ITEM`$0c parent mode $mode cannot resolve $label." }
        $index = 0; $ended = $false
        foreach ($node in $nodes) {
            if ($node.Offset -le $start[0].Offset -or $node.Kind -in @('Label', 'Blank', 'Comment')) { continue }
            if ($node.Kind -ne 'Data' -or $node.Name -ne '.db' -or $node.Operands.Count -ne 3) {
                throw "ITEM`$0c ${label}: unsupported $($node.Kind)/$($node.Name) before bit7 terminal."
            }
            $bytes = @($node.Operands | ForEach-Object {
                if ($_ -notmatch '^\$[0-9a-f]{2}$') { throw "ITEM`$0c ${label}: non-byte animation operand $_." }
                [Convert]::ToInt32($_.Substring(1), 16)
            })
            $rows.Add("$($mode.ToString('x2'))`t$index`t$($bytes[0])`t$($bytes[1].ToString('x2'))`t$($bytes[2].ToString('x2'))`tdata/ages/specialObjectAnimationData.s:$label+$index")
            $biggoronParentFrames.Add([pscustomobject]@{ Mode=$mode; Frame=$index; Graphic=$bytes[1] })
            $index++
            if (($bytes[2] -band 0x80) -ne 0) { $ended=$true; break }
        }
        if (-not $ended -or $index -ne 6) { throw "ITEM`$0c $label lacks its six-frame terminal stream." }
    }
    Write-GeneratedTable((Join-Path $destination 'metadata/biggoron_sword_parent_animations.tsv'), $rows)
}


$somariaPush = [regex]::Match($somariaSource,
    '(?ms)ldbc SPEED_80, \$(?<normal>[0-9a-f]{2}).*?wBraceletLevel\).*?cp \$02.*?ldbc SPEED_c0, \$(?<glove>[0-9a-f]{2})')
if (-not $somariaPush.Success -or $somariaSource -notmatch 'ld a,SND_MYSTERY_SEED' -or
    $somariaSource -notmatch 'ld a,SND_MOVEBLOCK') { throw 'Somaria block push counters/phase sounds changed.' }
Write-GeneratedTable((Join-Path $destination 'metadata/somaria_lifecycle.tsv'), @(
    "# phase-sound`tmove-sound`tnormal-speed`tnormal-frames`tglove-speed`tglove-frames`tsource",
    "$($soundIds['SND_MYSTERY_SEED'].ToString('x2'))`t$($soundIds['SND_MOVEBLOCK'].ToString('x2'))`t14`t$([Convert]::ToInt32($somariaPush.Groups['normal'].Value,16))`t1e`t$([Convert]::ToInt32($somariaPush.Groups['glove'].Value,16))`tobject_code/ages/items/caneOfSomaria.s:itemCode18"))

$itemPassableTiles = [regex]::Match(
    $itemPassableTilesSource,
    '(?ms)^itemPassableTilesTable:.*?^@overworld:\s*^@underwater:\s*(?<overworld>.*?)^@indoors:\s*(?<indoors>.*?)^@dungeons:\s*^@five:\s*(?<dungeons>.*?)^@sidescrolling:\s*(?<sidescrolling>.*)')
if (-not $itemPassableTiles.Success) {
    throw 'Could not parse itemPassableTilesTable.'
}

function Read-ItemPassableTiles([string]$name) {
    $values = @([regex]::Matches(
        $itemPassableTiles.Groups[$name].Value,
        '\$(?<value>[0-9a-f]{2})') |
        ForEach-Object { [Convert]::ToInt32($_.Groups['value'].Value, 16) })
    # The dungeon/five list deliberately falls through to the following
    # sidescrolling `$00 instead of owning a duplicate terminator.
    if ($name -eq 'dungeons') { return $values }
    if ($values.Count -eq 0 -or $values[-1] -ne 0) {
        throw "Item-passable tile list $name has no `$00 terminator."
    }
    if ($values.Count -eq 1) { return @() }
    return @($values[0..($values.Count - 2)])
}

$passableOverworld = @(Read-ItemPassableTiles 'overworld')
$passableIndoors = @(Read-ItemPassableTiles 'indoors')
$passableDungeons = @(Read-ItemPassableTiles 'dungeons')
$passableSidescrolling = @(Read-ItemPassableTiles 'sidescrolling')
if ($passableOverworld.Count -ne 2 -or $passableIndoors.Count -ne 3 -or
    $passableDungeons.Count -ne 16 -or $passableSidescrolling.Count -ne 0) {
    throw 'Ages item-passable tile lists changed.'
}
$encodedItemPassableTiles = @(
    "0:$($passableOverworld -join ',')"
    "1:$($passableIndoors -join ',')"
    "2:$($passableDungeons -join ',')"
    '3:'
    "4:$($passableOverworld -join ',')"
    "5:$($passableDungeons -join ',')"
) -join ';'

$seedNonBouncePath = Join-Path $Disassembly 'data\ages\tile_properties\seedsDontBounce.s'
$seedNonBounceSource = Read-ImportText $seedNonBouncePath
if ($seedNonBounceSource -notmatch '@indoors:\s*@sidescrolling:\s*@underwater:\s*\.db \$00' -or
    $seedNonBounceSource -notmatch '@dungeons:\s*@five:\s*\.db TILEINDEX_UNLIT_TORCH' -or
    $seedNonBounceSource -notmatch 'seedsDontBounceTilesTable:\s*\.dw @overworld\s*\.dw @indoors\s*\.dw @dungeons\s*\.dw @sidescrolling\s*\.dw @underwater\s*\.dw @five') {
    throw 'seedsDontBounceTilesTable pointer order or aliases changed.'
}
$seedNonBounceOverworld = @(Read-AssemblyLiteralValues $seedNonBouncePath '@overworld')
$seedNonBounceDungeon = @()
foreach ($node in Read-AssemblyDataDirectives $seedNonBouncePath '@five' '.db') {
    foreach ($operand in $node.Operands) {
        if ($operand -eq '$00') { continue }
        $constant = @(Read-AssemblyConstants (Join-Path $Disassembly 'constants\common\tileIndices.s') '' $operand)
        if ($constant.Count -ne 1) { throw "seedsDontBounceTilesTable: unresolved $operand." }
        $seedNonBounceDungeon += Convert-AssemblyInteger $constant[0].OperandText
    }
}
if (($seedNonBounceOverworld -join ',') -ne '206,207,197,197,198,199,200,201,202' -or
    ($seedNonBounceDungeon -join ',') -ne '8,9') {
    throw 'seedsDontBounceTilesTable overworld/dungeon rows changed.'
}
$encodedSeedNonBounceTiles = @(
    "0:$($seedNonBounceOverworld -join ',')", '1:',
    "2:$($seedNonBounceDungeon -join ',')", '3:', '4:',
    "5:$($seedNonBounceDungeon -join ',')"
) -join ';'

# ITEM_SHOOTER ($0f) holds its parent until the assigned button is released,
# rotates through eight directions while held, then creates the selected seed
# with a nonzero subid from the item-use dispatcher. The runtime represents
# the zero/nonzero distinction with SeedLaunchKind, not a fixed source subid.
# The child uses SPEED_300 and activates after its third solid bounce.
# Keep these parent/child boundaries separate from the Satchel rows:
# the seed item IDs and graphics are shared, but their motion is not.
$seedShooterSourceValid =
    $itemIds['ITEM_SHOOTER'] -eq 0x0f -and
    $treasureIds['TREASURE_SHOOTER'] -eq 0x0f -and
    $soundIds['SND_SEEDSHOOTER'] -eq 0xcb -and
    $itemUsageSource -match
        '(?m)^\s*\.db\s+\$43,\s*<wGameKeysJustPressed\s*;\s*ITEM_SHOOTER' -and
    $itemUsageSource -match
        '(?m)^\s*\.db\s+\$c6,\s*LINK_ANIM_MODE_21\s*;\s*ITEM_SHOOTER' -and
    $uncmpGfxHeadersSource -match
        '(?ms)^uncmpGfxHeader1d:\s*m_GfxHeader spr_seed_shooter, \$8521' -and
    $seedShooterPropertiesSource -match '(?m)^invert:\s*false\s*$' -and
    $seedParentSource -match
        '(?ms)^parentItemCode_shooter:.*?^@state0:.*?call clearSelfIfNoSeeds.*?call updateLinkDirectionFromAngle.*?call parentItemLoadAnimationAndIncState.*?call itemCreateChild' -and
    $seedParentSource -match
        '(?ms)^@state1:.*?call parentItemCheckButtonPressed.*?wIsSeedShooterInUse.*?call clearSelfIfNoSeeds\s*;[^\r\n]*\r?\n(?:\s*;[^\r\n]*\r?\n)*\s*push bc\s+ld e,\$01\s+call itemCreateChildWithID.*?ld \(hl\),\$0c.*?SND_SEEDSHOOTER' -and
    $seedParentSource -match
        '(?ms)^@updateAngleFrom5Bit:.*?^@checkUpdateAngle:.*?and \(BTN_RIGHT\|BTN_LEFT\|BTN_UP\|BTN_DOWN\).*?ld \(hl\),\$10.*?^@determineBaseAnimation:.*?ld a,\$48.*?ld a,\$40.*?ld a,\$38.*?ld l,Item\.var31.*?ld \(hl\),a.*?ld l,Item\.var3f.*?ld \(hl\),\$04' -and
    $specialObjectAnimationLogicSource -match
        '(?ms)^func_4553:.*?^--\s*ld l,Item\.var3f\s*ld a,\(hl\)\s*cp c\s*jr c,\+\s*ld c,a\s*ld l,Item\.var31\s*ld b,\(hl\)' -and
    $seedCodeSource -match
        '(?ms)^@shooterPositionOffsets:\s*\.db \$f2 \$fc.*?\.db \$fc \$0b.*?\.db \$05 \$0c.*?\.db \$09 \$0b.*?\.db \$0d \$03.*?\.db \$0a \$f8.*?\.db \$05 \$f3.*?\.db \$f8 \$f8' -and
    $seedCodeSource -match
        '(?ms)^@state0:.*?ld l,Item\.var34\s*ld \(hl\),\$03.*?^@shooter:\s*ld e,Item\.angle.*?SPEED_300' -and
    $seedCodeSource -match
        '(?ms)^seedItemUpdateBouncing:.*?Decrement bounce counter.*?dec \(hl\).*?jr z,@unsetZFlag' -and
    $objectSpeedsSource -match
        '(?m)^\s*SPEED_300\s+dsb 5 ; 0x78$'
if (-not $seedShooterSourceValid) {
    throw 'ITEM_SHOOTER aiming, child allocation, offsets, speed, bounce count, or sound changed in the disassembly.'
}
$shooterItemData = [regex]::Match(
    $itemDataSource,
    '(?m)^\s*\.db\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s+\$(?<palette>[0-9a-f]{2})\s*;\s*\$0f:\s*ITEM_SHOOTER')
$shooterOamPointers = [regex]::Match(
    $itemAnimationsSource,
    '(?ms)^item0fOamDataPointers:.*?\r?\n(?<body>(?:\s*\.dw\s+itemOamData[0-9a-f]+\s*\r?\n){8})')
$shooterAnimationAliases = $itemAnimationsSource -match
    '(?ms)^item0fAnimations:\s*^item13Animations:\s*^item16Animations:\s*^item27Animations:\s*\.dw itemAnimation1e7ad\s*\.dw itemAnimation1e7b0\s*\.dw itemAnimation1e7b3\s*\.dw itemAnimation1e7b6\s*\.dw itemAnimation1e7b9\s*\.dw itemAnimation1e7bc\s*\.dw itemAnimation1e7bf\s*\.dw itemAnimation1e7c2' -and
    $itemAnimationsSource -match
    '(?ms)^itemAnimation1e7ad:\s*\.db \$7f \$00 \$00\s*^itemAnimation1e7b0:\s*\.db \$7f \$02 \$00\s*^itemAnimation1e7b3:\s*\.db \$7f \$04 \$00\s*^itemAnimation1e7b6:\s*\.db \$7f \$06 \$00\s*^itemAnimation1e7b9:\s*\.db \$7f \$08 \$00\s*^itemAnimation1e7bc:\s*\.db \$7f \$0a \$00\s*^itemAnimation1e7bf:\s*\.db \$7f \$0c \$00\s*^itemAnimation1e7c2:\s*\.db \$7f \$0e \$00'
if (-not $shooterItemData.Success -or
    $shooterItemData.Groups['gfx'].Value -ne '00' -or
    $shooterItemData.Groups['tile'].Value -ne '52' -or
    $shooterItemData.Groups['palette'].Value -ne '08' -or
    -not $shooterOamPointers.Success -or -not $shooterAnimationAliases) {
    throw 'ITEM_SHOOTER weapon data, OAM pointers, or eight static animations changed.'
}
$shooterOamLabels = @([regex]::Matches(
    $shooterOamPointers.Groups['body'].Value,
    '(?m)^\s*\.dw\s+(?<label>itemOamData[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if ($shooterOamLabels.Count -ne 8) {
    throw "Expected eight ITEM_SHOOTER OAM pointers, parsed $($shooterOamLabels.Count)."
}
$encodedShooterOam = @($shooterOamLabels |
    ForEach-Object { Read-ItemOamComposition $_ }) -join '|'
$seedShooterRows = @(
    '# item`tspeed-raw`taim-lockout`tpost-shot-wait`tsound`toffsets`tnon-bounce-tiles`titem-passable-tiles`tweapon-sprite`tweapon-vram-tile-base`tweapon-palette`tweapon-source-grayscale-inverted`tweapon-oam`tsource'.Replace('`t', "`t")
    "0f`t78`t16`t12`tcb`t-14,-4;-4,11;5,12;9,11;13,3;10,-8;5,-13;-8,-8`t$encodedSeedNonBounceTiles`t$encodedItemPassableTiles`tspr_seed_shooter`t52`t00`t0`t$encodedShooterOam`tobject_code/common/itemParents/seedsParent.s:parentItemCode_shooter;object_code/ages/items/seedShooter.s:itemCode0fPost;object_code/common/items/seeds.s:@shooterPositionOffsets/seedItemUpdateBouncing;data/ages/tile_properties/seedsDontBounce.s;data/itemAnimations.s:item0fAnimations;gfx/ages/spr_seed_shooter.properties"
)
Write-GeneratedTable(
    (Join-Path $destination 'metadata\seed_shooter.tsv'),
    $seedShooterRows)

# Ages' Biggoron producer still needs Link OAM composition; ordinary Link/item
# tables are owned by Shared/Import-LinkItemData.ps1.
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

Export-BiggoronSwordData
$biggoronLinkRows = [Collections.Generic.List[string]]::new()
$biggoronLinkRows.Add("# kind`tvariant`tphase`tdirection`tgraphics-index`toam-index`tbyte-offset`toam`tsource")
foreach ($frame in $biggoronParentFrames) {
    for ($direction = 0; $direction -lt 4; $direction++) {
        Add-LinkGraphicRow $biggoronLinkRows 'biggoron' $frame.Mode $frame.Frame $direction ($frame.Graphic + $direction)
    }
}
Write-GeneratedTable((Join-Path $destination 'metadata/biggoron_sword_link_graphics.tsv'), $biggoronLinkRows)

# The same native macro stream supplies both games' treasure catalogs.
$catalog = Read-TreasureObjectCatalog 'ages' $romBytes $agesSymbols $allTexts
$treasureObjectRecords = $catalog.Records
$treasureObjectSource = $catalog.Source
Write-GeneratedTable((Join-Path $destination 'metadata/treasure_objects.tsv'), $catalog.Rows)

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
$rupeeValues = @()
$rupeeValueRows = [Collections.Generic.List[string]]::new()
$rupeeValueRows.Add("# index`tamount")
# bank0.getRupeeValue clamps at $14. Preserve all 21 packed-BCD words,
# including shop values and $0999, from the supported clean US ROM.
for ($index = 0; $index -le 0x14; $index++) {
    $low = [int]$romBytes[0x1791 + $index * 2]
    $high = [int]$romBytes[0x1792 + $index * 2]
    if (($low -band 15) -gt 9 -or ($low -shr 4) -gt 9 -or $high -gt 9) {
        throw "getRupeeValue: invalid BCD word at index `$$($index.ToString('x2'))."
    }
    $amount = $high * 100 + ($low -shr 4) * 10 + ($low -band 15)
    $rupeeValues += $amount
    $rupeeValueRows.Add("$($index.ToString('x2'))`t$amount")
}
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

# These native interactions publish a two-byte override one dispatch after
# state-zero initialization. Preserve the shared source table and opposite
# linked-game selectors; the static chestData entries intentionally contain NONE.
$seaChestSource = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/miscellaneous2.s')
$seaChestTable = [regex]::Match($seaChestSource,
    '(?ms)^interactiondc_subid15And16_setChestContents:\s+ld hl,@chestContents\s+rst_addDoubleIndex\s+ldi a,\(hl\)\s+ld \(wChestContentsOverride\),a\s+ld a,\(hl\)\s+ld \(wChestContentsOverride\+1\),a\s+jp interactionDelete\s+@chestContents:\s+dwbe (?<first>TREASURE_OBJECT_[A-Za-z0-9_]+)[^\r\n]*\s+dwbe (?<second>TREASURE_OBJECT_[A-Za-z0-9_]+)')
if (!$seaChestTable.Success -or
    $seaChestSource -notmatch '(?ms)^interactiondc_subid15:\s+call checkInteractionState\s+jr z,interactiondc_subid15And16_state0\s+@state1:\s+call checkIsLinkedGame\s+ld a,\$01\s+jr nz,interactiondc_subid15And16_setChestContents\s+dec a' -or
    $seaChestSource -notmatch '(?ms)^interactiondc_subid16:\s+call checkInteractionState\s+jr z,interactiondc_subid15And16_state0\s+call checkIsLinkedGame\s+ld a,\$00\s+jr nz,interactiondc_subid15And16_setChestContents\s+inc a\s+jr interactiondc_subid15And16_setChestContents' -or
    $seaChestSource -notmatch '(?ms)^interactiondc_subid15And16_state0:\s+call getThisRoomFlags\s+and ROOMFLAG_ITEM\s+jp nz,interactionDelete\s+jp interactionIncState') {
    throw 'miscellaneous2.s:$dc:$15/$16 chest override state, linked selectors, or contents table changed.'
}
$seaChestObjects = @($seaChestTable.Groups['first'].Value, $seaChestTable.Groups['second'].Value)
$seaChestRows = [Collections.Generic.List[string]]::new()
$seaChestRows.Add("# subid`tlinked`ttreasure-object`tsource")
foreach ($subid in @(0x15, 0x16)) {
    for ($linked = 0; $linked -lt 2; $linked++) {
        $index = if ($subid -eq 0x15) { $linked } else { 1 - $linked }
        $name = $seaChestObjects[$index]
        if (!$treasureObjectRecords.ContainsKey($name)) { throw "Unresolved chest override $name." }
        $seaChestRows.Add("$($subid.ToString('x2'))`t$linked`t$name`tobject_code/ages/interactions/miscellaneous2.s:interactiondc_subid$($subid.ToString('x2'))")
    }
}
Write-GeneratedTable((Join-Path $destination 'objects/chest_contents_overrides.tsv'), $seaChestRows)
