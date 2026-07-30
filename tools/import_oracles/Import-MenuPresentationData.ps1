# Export the source-ordered, table-shaped presentation records used by the
# map, inventory, file-select, save/quit, and ring menus. State transitions
# and cursor movement remain runtime controller concerns; this stage owns only
# source layout bytes and their label/alias identity.
$menuBank2Path = Join-Path $Disassembly 'code\bank2.s'
$menuGfxHeadersPath = Join-Path $Disassembly 'data\ages\gfxHeaders.s'
$menuTreasureConstantsPath =
    Join-Path $Disassembly 'constants\common\treasure.s'

function Get-MenuLocalBlock(
    [string]$path,
    [string]$globalLabel,
    [string]$localLabel
) {
    $nodes = @(Read-AssemblyLabelNodes $path $globalLabel)
    $start = -1
    for ($index = 0; $index -lt $nodes.Count; $index++) {
        if ($nodes[$index].Kind -eq 'Label' -and
            $nodes[$index].Name -ceq $localLabel) {
            if ($start -ge 0) {
                throw "$path`:$globalLabel contains duplicate local label $localLabel."
            }
            $start = $index
        }
    }
    if ($start -lt 0) {
        throw "$path`:$globalLabel is missing local label $localLabel."
    }
    $result = [Collections.Generic.List[object]]::new()
    for ($index = $start + 1; $index -lt $nodes.Count; $index++) {
        if ($nodes[$index].Kind -eq 'Label') { break }
        $result.Add($nodes[$index])
    }
    return @($result)
}

function Get-MenuLocalData(
    [string]$path,
    [string]$globalLabel,
    [string]$localLabel,
    [string]$directive
) {
    return @(Get-MenuLocalBlock $path $globalLabel $localLabel |
        Where-Object {
            $_.Kind -eq 'Data' -and $_.Name -ieq $directive
        })
}

function Read-MenuOamParts(
    [string]$path,
    [string]$globalLabel,
    [string]$localLabel
) {
    $data = @(Get-MenuLocalData $path $globalLabel $localLabel '.db')
    if ($data.Count -eq 0 -or $data[0].Operands.Count -ne 1) {
        throw "$path`:$globalLabel$localLabel has no single-byte OAM count."
    }
    $count = Convert-AssemblyInteger $data[0].Operands[0]
    $parts = [Collections.Generic.List[object]]::new()
    foreach ($node in $data | Select-Object -Skip 1) {
        if ($node.Operands.Count -ne 4) {
            throw "$($node.Path):$($node.Line): malformed OAM part in " +
                "$globalLabel$localLabel."
        }
        $values = @($node.Operands | ForEach-Object {
            Convert-AssemblyInteger $_
        })
        $parts.Add(@{
            Y = $values[0]
            X = $values[1]
            Tile = $values[2]
            Attributes = $values[3]
        })
    }
    if ($parts.Count -ne $count) {
        throw "$path`:$globalLabel$localLabel declares $count OAM parts but " +
            "contains $($parts.Count)."
    }
    return @($parts)
}

function Get-MenuTreasureIds {
    $ids = @{}
    foreach ($node in Read-AssemblyMacroInvocations $menuTreasureConstantsPath) {
        if ($node.Name -match '^TREASURE_[A-Z0-9_]+$' -and
            $node.Operands.Count -eq 1 -and
            $node.Operands[0] -ieq 'db' -and
            $node.Comment -match '^\s*(?:0x|\$)(?<id>[0-9a-f]{2})') {
            $ids[$node.Name] = [Convert]::ToInt32($Matches['id'], 16)
        }
    }
    return $ids
}

function Convert-MenuTilemapPointer([string]$operand, [string]$context) {
    if ($operand.Trim() -notmatch '^w4TileMap\+\$(?<offset>[0-9a-f]{1,4})$') {
        throw "$context has unsupported tilemap pointer '$operand'."
    }
    return [Convert]::ToInt32($Matches['offset'], 16)
}

function Add-MenuOamRows(
    [Collections.Generic.List[string]]$rows,
    [string]$layout,
    [string]$globalLabel,
    [string]$localLabel
) {
    $parts = @(Read-MenuOamParts `
        $menuBank2Path $globalLabel $localLabel)
    for ($part = 0; $part -lt $parts.Count; $part++) {
        $value = $parts[$part]
        $rows.Add([string]::Join(
            "`t",
            @(
                $layout,
                $part,
                $value.Y.ToString('x2'),
                $value.X.ToString('x2'),
                $value.Tile.ToString('x2'),
                $value.Attributes.ToString('x2'),
                "$globalLabel$localLabel",
                '',
                "code/bank2.s:$globalLabel$localLabel")))
    }
}

# mapIconOamTable is a dbrel pointer table followed by 26 local OAM lists.
$mapIconNodes = @(Read-AssemblyLabelNodes $menuBank2Path 'mapIconOamTable')
$mapIconPointers = @($mapIconNodes | Where-Object {
    $_.Kind -eq 'MacroInvocation' -and $_.Name -ieq 'dbrel'
})
if ($mapIconPointers.Count -ne 26) {
    throw "Expected 26 mapIconOamTable pointers, got $($mapIconPointers.Count)."
}
$mapIconRows = [Collections.Generic.List[string]]::new()
$mapIconRows.Add(
    '# index`tlabel`talias-of`tsprite-count`tleft-y`tleft-x`tleft-tile' +
    '`tleft-attributes`tright-y`tright-x`tright-tile`tright-attributes`tsource')
$mapIconSignatures = @{}
for ($index = 0; $index -lt $mapIconPointers.Count; $index++) {
    $label = $mapIconPointers[$index].Operands[0]
    $parts = @(Read-MenuOamParts $menuBank2Path 'mapIconOamTable' $label)
    if ($parts.Count -notin @(0, 2)) {
        throw "mapIconOamTable $label has $($parts.Count) parts; expected 0 or 2."
    }
    $signature = ($parts | ForEach-Object {
        "$($_.Y),$($_.X),$($_.Tile),$($_.Attributes)"
    }) -join ';'
    $alias = if ($mapIconSignatures.ContainsKey($signature)) {
        $mapIconSignatures[$signature]
    } else {
        $mapIconSignatures[$signature] = $label
        ''
    }
    $left = if ($parts.Count -eq 2) { $parts[0] } else {
        @{ Y = 0; X = 0; Tile = 0; Attributes = 0 }
    }
    $right = if ($parts.Count -eq 2) { $parts[1] } else {
        @{ Y = 0; X = 0; Tile = 0; Attributes = 0 }
    }
    $mapIconRows.Add([string]::Join(
        "`t",
        @(
            $index,
            $label,
            $alias,
            $parts.Count,
            $left.Y.ToString('x2'),
            $left.X.ToString('x2'),
            $left.Tile.ToString('x2'),
            $left.Attributes.ToString('x2'),
            $right.Y.ToString('x2'),
            $right.X.ToString('x2'),
            $right.Tile.ToString('x2'),
            $right.Attributes.ToString('x2'),
            "code/bank2.s:mapIconOamTable/$label")))
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\map_icons.tsv'),
    $mapIconRows)

$floorOffsets = @(Read-AssemblyLiteralValues `
    $menuBank2Path 'dungeonMapFloorListStartPositions')
if ($floorOffsets.Count -ne 14) {
    throw "Expected 14 Ages dungeon floor-list offsets, got $($floorOffsets.Count)."
}
$floorRows = [Collections.Generic.List[string]]::new()
$floorRows.Add('# dungeon`toffset`tsource-label`tsource')
for ($index = 0; $index -lt $floorOffsets.Count; $index++) {
    $floorRows.Add(
        "$index`t$($floorOffsets[$index].ToString('x2'))`t" +
        "dungeonMapFloorListStartPositions`t" +
        "code/bank2.s:dungeonMapFloorListStartPositions+$($index.ToString('x2'))")
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\dungeon_floor_list.tsv'),
    $floorRows)

# GFXH_DUNGEON_0_BLURB through GFXH_DUNGEON_F_BLURB are the source
# selector table. Repeated graphics are retained as explicit aliases.
$gfxHeaderMacros = @(Read-AssemblyMacroInvocations $menuGfxHeadersPath)
$blurbRows = [Collections.Generic.List[string]]::new()
$blurbRows.Add(
    '# dungeon`tgfx-header`tgraphic`tasset`talias-of`tsource')
$firstBlurbHeaderByGraphic = @{}
for ($macroIndex = 0; $macroIndex -lt $gfxHeaderMacros.Count; $macroIndex++) {
    $start = $gfxHeaderMacros[$macroIndex]
    if ($start.Name -ine 'm_GfxHeaderStart' -or
        $start.Operands.Count -lt 2 -or
        $start.Operands[1] -notmatch '^GFXH_DUNGEON_[0-9A-F]_BLURB$') {
        continue
    }
    $headerIndex = Convert-AssemblyInteger $start.Operands[0]
    if ($headerIndex -lt 0x10 -or $headerIndex -gt 0x1f) { continue }
    $graphicNode = $null
    for ($nextIndex = $macroIndex + 1;
        $nextIndex -lt $gfxHeaderMacros.Count;
        $nextIndex++) {
        $candidate = $gfxHeaderMacros[$nextIndex]
        if ($candidate.Name -ieq 'm_GfxHeaderStart') { break }
        if ($candidate.Name -ieq 'm_GfxHeader') {
            $graphicNode = $candidate
            break
        }
    }
    if ($null -eq $graphicNode -or $graphicNode.Operands.Count -lt 1 -or
        $graphicNode.Operands[0] -notmatch '^gfx_blurb_(?<asset>[a-z0-9]+)$') {
        throw "$($start.Path):$($start.Line): $($start.Operands[1]) has no " +
            'gfx_blurb_* record.'
    }
    $graphic = $graphicNode.Operands[0]
    $asset = $Matches['asset']
    $alias = if ($firstBlurbHeaderByGraphic.ContainsKey($graphic)) {
        $firstBlurbHeaderByGraphic[$graphic]
    } else {
        $firstBlurbHeaderByGraphic[$graphic] = $start.Operands[1]
        ''
    }
    $dungeon = $headerIndex - 0x10
    $blurbRows.Add([string]::Join(
        "`t",
        @(
            $dungeon,
            $start.Operands[1],
            $graphic,
            $asset,
            $alias,
            "data/ages/gfxHeaders.s:$($start.Operands[1])/$graphic")))
}
if ($blurbRows.Count -ne 17) {
    throw "Expected 16 dungeon blurb selectors, got $($blurbRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\dungeon_blurbs.tsv'),
    $blurbRows)

$itemSlotRows = [Collections.Generic.List[string]]::new()
$itemSlotRows.Add('# index`ttilemap-offset`tsource-label`tsource')
$itemSlotIndex = 0
foreach ($node in Get-MenuLocalData `
    $menuBank2Path 'inventorySubscreen0_drawStoredItems' '@itemPositions' '.dw') {
    foreach ($operand in $node.Operands) {
        $offset = Convert-MenuTilemapPointer `
            $operand 'inventorySubscreen0_drawStoredItems@itemPositions'
        $itemSlotRows.Add(
            "$itemSlotIndex`t$($offset.ToString('x3'))`t" +
            "inventorySubscreen0_drawStoredItems@itemPositions`t" +
            "code/bank2.s:inventorySubscreen0_drawStoredItems@itemPositions+" +
            $itemSlotIndex.ToString('x2'))
        $itemSlotIndex++
    }
}
if ($itemSlotIndex -ne 16) {
    throw "Expected 16 inventory item positions, got $itemSlotIndex."
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\inventory_item_slots.tsv'),
    $itemSlotRows)

# inventoryMenuState2 shares these widths and source-ordered X positions
# between the Satchel/seed weapons and the Harp. table_5ae5 deliberately
# overlaps its @data2-@data5 byte sequences, so resolve its pointer labels
# against one flattened byte stream instead of treating each local label as an
# independent block.
$itemSubmenuWidths = [Collections.Generic.List[int]]::new()
foreach ($node in Get-MenuLocalData `
    $menuBank2Path 'inventoryMenuState2' '@itemSubmenuWidths' '.db') {
    foreach ($operand in $node.Operands) {
        $itemSubmenuWidths.Add((Convert-AssemblyInteger $operand))
    }
}
if ($itemSubmenuWidths.Count -ne 4) {
    throw "Expected four inventoryMenuState2 submenu widths, got " +
        "$($itemSubmenuWidths.Count)."
}

$itemSubmenuPositionNodes = @(
    Read-AssemblyLabelNodes $menuBank2Path 'table_5ae5')
$itemSubmenuPositionPointers = [Collections.Generic.List[string]]::new()
$itemSubmenuPositionBytes = [Collections.Generic.List[int]]::new()
$itemSubmenuPositionOffsets =
    [Collections.Generic.Dictionary[string, int]]::new(
        [StringComparer]::Ordinal)
foreach ($node in $itemSubmenuPositionNodes) {
    if ($node.Kind -eq 'Label') {
        $itemSubmenuPositionOffsets[$node.Name] =
            $itemSubmenuPositionBytes.Count
        continue
    }
    if ($node.Kind -ne 'Data') { continue }
    if ($node.Name -ieq '.dw') {
        foreach ($operand in $node.Operands) {
            $itemSubmenuPositionPointers.Add($operand)
        }
        continue
    }
    if ($node.Name -ieq '.db') {
        foreach ($operand in $node.Operands) {
            $itemSubmenuPositionBytes.Add(
                (Convert-AssemblyInteger $operand))
        }
    }
}
if ($itemSubmenuPositionPointers.Count -ne 4) {
    throw "Expected four table_5ae5 submenu-position pointers, got " +
        "$($itemSubmenuPositionPointers.Count)."
}

$itemSubmenuLayoutRows = [Collections.Generic.List[string]]::new()
$itemSubmenuLayoutRows.Add(
    '# option-count`tindex`tmax-width`tx-nibble`tsource-label`tsource')
for ($optionCount = 2; $optionCount -le 5; $optionCount++) {
    $pointer = $itemSubmenuPositionPointers[$optionCount - 2]
    if (-not $itemSubmenuPositionOffsets.ContainsKey($pointer)) {
        throw "table_5ae5 pointer '$pointer' does not resolve to a local label."
    }
    $start = $itemSubmenuPositionOffsets[$pointer]
    if ($start + $optionCount -gt $itemSubmenuPositionBytes.Count) {
        throw "table_5ae5 pointer '$pointer' does not contain $optionCount " +
            'position bytes.'
    }
    $width = $itemSubmenuWidths[$optionCount - 2]
    for ($index = 0; $index -lt $optionCount; $index++) {
        $itemSubmenuLayoutRows.Add([string]::Join(
            "`t",
            @(
                $optionCount,
                $index,
                $width.ToString('x2'),
                $itemSubmenuPositionBytes[$start + $index].ToString('x2'),
                "table_5ae5$pointer",
                "code/bank2.s:table_5ae5/$pointer+$($index.ToString('x2'))")))
    }
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\inventory_item_submenu_layout.tsv'),
    $itemSubmenuLayoutRows)

$seedSubmenuPointers = @(Read-AssemblyLabelNodes `
    $menuBank2Path 'seedAndHarpSpriteTable' |
    Where-Object {
        $_.Kind -eq 'MacroInvocation' -and $_.Name -ieq 'dbrel'
    })
if ($seedSubmenuPointers.Count -ne 8) {
    throw "Expected eight seedAndHarpSpriteTable pointers, got " +
        "$($seedSubmenuPointers.Count)."
}
$seedSubmenuRows = [Collections.Generic.List[string]]::new()
$seedSubmenuRows.Add(
    '# seed-type`ty-offset`tx-offset`ttile`tattributes`tsource-label`tsource')
for ($seedType = 0; $seedType -lt 5; $seedType++) {
    $label = $seedSubmenuPointers[$seedType].Operands[0]
    $parts = @(Read-MenuOamParts `
        $menuBank2Path 'seedAndHarpSpriteTable' $label)
    if ($parts.Count -ne 1) {
        throw "seedAndHarpSpriteTable $label has $($parts.Count) OAM parts; " +
            'expected one for a seed.'
    }
    $part = $parts[0]
    $seedSubmenuRows.Add([string]::Join(
        "`t",
        @(
            $seedType,
            $part.Y.ToString('x2'),
            $part.X.ToString('x2'),
            $part.Tile.ToString('x2'),
            $part.Attributes.ToString('x2'),
            "seedAndHarpSpriteTable$label",
            "code/bank2.s:seedAndHarpSpriteTable/$label")))
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\inventory_seed_submenu_oam.tsv'),
    $seedSubmenuRows)

$menuTreasureIds = Get-MenuTreasureIds
$passiveRows = [Collections.Generic.List[string]]::new()
$passiveRows.Add(
    '# index`ttreasure`ttreasure-id`tposition`tslot`tsource-label`tsource')
$passiveIndex = 0
foreach ($node in Read-AssemblyDataDirectives `
    $menuBank2Path 'subscreen1TreasureData' '.db') {
    if ($node.Operands.Count -eq 1 -and
        (Convert-AssemblyInteger $node.Operands[0]) -eq 0) {
        break
    }
    if ($node.Operands.Count -ne 3) {
        throw "$($node.Path):$($node.Line): malformed subscreen1TreasureData row."
    }
    $treasure = $node.Operands[0]
    if (-not $menuTreasureIds.ContainsKey($treasure)) {
        throw "$($node.Path):$($node.Line): unresolved passive treasure $treasure."
    }
    $position = Convert-AssemblyInteger $node.Operands[1]
    $slot = Convert-AssemblyInteger $node.Operands[2]
    $passiveRows.Add([string]::Join(
        "`t",
        @(
            $passiveIndex,
            $treasure,
            $menuTreasureIds[$treasure].ToString('x2'),
            $position.ToString('x2'),
            $slot,
            'subscreen1TreasureData',
            "code/bank2.s:subscreen1TreasureData+$($passiveIndex.ToString('x2'))")))
    $passiveIndex++
}
if ($passiveIndex -ne 32) {
    throw "Expected 32 Ages passive-treasure rows, got $passiveIndex."
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\inventory_passive_treasures.tsv'),
    $passiveRows)

$secondaryValues = [Collections.Generic.List[int]]::new()
foreach ($node in Get-MenuLocalData `
    $menuBank2Path 'inventorySubmenu1_drawCursor' '@data' '.db') {
    foreach ($operand in $node.Operands) {
        $secondaryValues.Add((Convert-AssemblyInteger $operand))
    }
}
if ($secondaryValues.Count -ne 21) {
    throw "Expected 21 secondary cursor bytes, got $($secondaryValues.Count)."
}
$secondaryRows = [Collections.Generic.List[string]]::new()
$secondaryRows.Add('# index`tpacked`tsource-label`tsource')
for ($index = 0; $index -lt $secondaryValues.Count; $index++) {
    $secondaryRows.Add(
        "$index`t$($secondaryValues[$index].ToString('x2'))`t" +
        "inventorySubmenu1_drawCursor@data`t" +
        "code/bank2.s:inventorySubmenu1_drawCursor@data+" +
        $index.ToString('x2'))
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\inventory_secondary_cursors.tsv'),
    $secondaryRows)

$essenceTileRows = [Collections.Generic.List[string]]::new()
$essenceTileRows.Add('# index`ttilemap-offset`tsource-label`tsource')
$essenceTileIndex = 0
foreach ($node in Read-AssemblyDataDirectives `
    $menuBank2Path 'itemSubmenu2EssencePositions' '.dw') {
    foreach ($operand in $node.Operands) {
        $offset = Convert-MenuTilemapPointer `
            $operand 'itemSubmenu2EssencePositions'
        $essenceTileRows.Add(
            "$essenceTileIndex`t$($offset.ToString('x3'))`t" +
            "itemSubmenu2EssencePositions`t" +
            "code/bank2.s:itemSubmenu2EssencePositions+" +
            $essenceTileIndex.ToString('x2'))
        $essenceTileIndex++
    }
}
if ($essenceTileIndex -ne 8) {
    throw "Expected eight essence tilemap positions, got $essenceTileIndex."
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\inventory_essence_tiles.tsv'),
    $essenceTileRows)

$essenceCursorBytes = [Collections.Generic.List[int]]::new()
foreach ($node in Get-MenuLocalData `
    $menuBank2Path 'inventorySubmenu2_drawCursor' '@offsets' '.db') {
    foreach ($operand in $node.Operands) {
        $essenceCursorBytes.Add((Convert-AssemblyInteger $operand))
    }
}
if ($essenceCursorBytes.Count -ne 22) {
    throw "Expected eleven essence cursor coordinate pairs, got " +
        "$($essenceCursorBytes.Count / 2)."
}
$essenceCursorRows = [Collections.Generic.List[string]]::new()
$essenceCursorRows.Add('# index`traw-y`traw-x`tsource-label`tsource')
for ($index = 0; $index -lt 11; $index++) {
    $essenceCursorRows.Add(
        "$index`t$($essenceCursorBytes[$index * 2].ToString('x2'))`t" +
        "$($essenceCursorBytes[$index * 2 + 1].ToString('x2'))`t" +
        "inventorySubmenu2_drawCursor@offsets`t" +
        "code/bank2.s:inventorySubmenu2_drawCursor@offsets+" +
        $index.ToString('x2'))
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\inventory_essence_cursors.tsv'),
    $essenceCursorRows)

$fileOamRows = [Collections.Generic.List[string]]::new()
$fileOamRows.Add(
    '# layout`tpart`ty`tx`ttile`tattributes`tsource-label`talias-of`tsource')
Add-MenuOamRows $fileOamRows 'decorations' `
    'fileSelect_redrawDecorationsAndSetWramBank4' '@sprites'
Add-MenuOamRows $fileOamRows 'acorn-cursor' `
    'fileSelectDrawAcornCursor' '@sprite'
Add-MenuOamRows $fileOamRows 'text-speed-cursor' `
    'fileSelectMode1' '@data'
Add-MenuOamRows $fileOamRows 'name-character-cursor' `
    'drawNameInputCursors' '@cursorOnCharacterSprites'
Add-MenuOamRows $fileOamRows 'name-lower-option-cursor' `
    'drawNameInputCursors' '@lowerOptionCursorSprites'
Add-MenuOamRows $fileOamRows 'name-entry-cursor' `
    'drawNameInputCursors' '@textInputCursorSprite'
Add-MenuOamRows $fileOamRows 'save-quit-acorn' `
    'saveQuitMenu_drawSprites' '@acornSprite'
if ($fileOamRows.Count -ne 25) {
    throw "Expected 24 file/save OAM parts, got $($fileOamRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\file_oam.tsv'),
    $fileOamRows)

$ringOamRows = [Collections.Generic.List[string]]::new()
$ringOamRows.Add(
    '# layout`tpart`ty`tx`ttile`tattributes`tsource-label`talias-of`tsource')
Add-MenuOamRows $ringOamRows 'list-cursor' `
    'ringMenu_drawSprites' '@cursorSprite'
Add-MenuOamRows $ringOamRows 'page-arrows' `
    'ringMenu_drawSprites' '@arrowSprites'
Add-MenuOamRows $ringOamRows 'equipped-marker' `
    'ringMenu_drawEquippedRingSprite' '@equippedSprite'
Add-MenuOamRows $ringOamRows 'box-cursor' `
    'ringMenu_drawRingBoxCursor' '@ringBoxCursor'
Add-MenuOamRows $ringOamRows 'list-box-marker' `
    'ringMenu_drawSpritesForRingsInBox' '@sprite'
if ($ringOamRows.Count -ne 7) {
    throw "Expected six ring-menu OAM parts, got $($ringOamRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\ring_oam.tsv'),
    $ringOamRows)

$ringBoxOffsetValues = [Collections.Generic.List[int]]::new()
foreach ($node in Get-MenuLocalData `
    $menuBank2Path 'ringMenu_getSpriteOffsetForRingBoxPosition' '@offsets' '.db') {
    foreach ($operand in $node.Operands) {
        $ringBoxOffsetValues.Add((Convert-AssemblyInteger $operand))
    }
}
if ($ringBoxOffsetValues.Count -ne 5) {
    throw "Expected five ring-box OAM offsets, got $($ringBoxOffsetValues.Count)."
}
$ringBoxOffsetRows = [Collections.Generic.List[string]]::new()
$ringBoxOffsetRows.Add('# slot`tx-offset`tsource-label`tsource')
for ($index = 0; $index -lt $ringBoxOffsetValues.Count; $index++) {
    $ringBoxOffsetRows.Add(
        "$index`t$($ringBoxOffsetValues[$index].ToString('x2'))`t" +
        "ringMenu_getSpriteOffsetForRingBoxPosition@offsets`t" +
        "code/bank2.s:ringMenu_getSpriteOffsetForRingBoxPosition@offsets+" +
        $index.ToString('x2'))
}
Write-GeneratedTable(
    (Join-Path $destination 'menu\ring_box_oam_offsets.tsv'),
    $ringBoxOffsetRows)
