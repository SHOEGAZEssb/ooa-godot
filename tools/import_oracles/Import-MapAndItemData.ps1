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
    $textLow = if ($values[6] -match '<TX_[A-Z0-9_]+') { -1 } else { Convert-AsmByte $values[6] }
    if ($leftSprite -lt 0 -or $leftPalette -lt 0 -or $rightSprite -lt 0 -or
        $rightPalette -lt 0 -or $extraMode -lt 0) {
        throw "Could not parse treasure display row '$line'."
    }
    $textColumn = if ($textLow -lt 0) { 'ff' } else { $textLow.ToString('x2') }
    $displayRows.Add("$displayTable`t$displayIndex`t$($treasure.ToString('x2'))`t$($leftSprite.ToString('x2'))`t$($leftPalette.ToString('x2'))`t$($rightSprite.ToString('x2'))`t$($rightPalette.ToString('x2'))`t$($extraMode.ToString('x2'))`t$textColumn")
    $displayIndex++
}
if (($displayRows | Where-Object { $_ -match '^treasureDisplayData_sword\t0\t05\t90\t' }).Count -ne 1) {
    throw "Could not export the level-1 sword display icon row."
}
[IO.File]::WriteAllLines(
    (Join-Path $destination "metadata\treasure_display.tsv"),
    $displayRows,
    [Text.UTF8Encoding]::new($false))

# Export the breakable tile tables used by tryToBreakTile. The source masks
# retain the disassembly's left-to-right bit order from breakableTileSources.s.
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
$breakableRows = [Collections.Generic.List[string]]::new()
$breakableRows.Add("# active-collisions`ttile`tmode`tsource-mask`tdrop`teffect`treplacement")
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
        $breakableRows.Add("$collisionMode`t$($tile.ToString('x2'))`t$($modeIndex.ToString('x2'))`t$($mode.SourceMask.ToString('x5'))`t$($mode.Drop.ToString('x1'))`t$($mode.Effect.ToString('x2'))`t$($mode.Replacement.ToString('x2'))")
    }
}
if (($breakableRows | Where-Object { $_ -eq "2`t10`t1d`t00125`t2`t06`ta0" }).Count -ne 1) {
    throw 'Could not export dungeon moving pot tile $10 as bracelet-breakable mode $1d.'
}
[IO.File]::WriteAllLines(
    (Join-Path $destination "metadata\breakable_tiles.tsv"),
    $breakableRows,
    [Text.UTF8Encoding]::new($false))

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

