# bank2.s uses the same menu machinery with game-selected pointer streams.
# Read compiled clean-US bytes through source symbols so aliases, fallthrough
# and shared suffixes retain their executed order without a second assembler.
function Read-MenuRomBytes($pack, [string]$label, [int]$count, [string]$bankOwner = '') {
    $key=$label
    if($bankOwner -ne ''){
        if(-not $pack.Symbols.ContainsKey($bankOwner) -or $pack.Symbols.ContainsKey("ambiguous:$bankOwner")){
            throw "$($pack.Name) ${label}: missing or ambiguous bank owner '$bankOwner'."
        }
        $bank=[int][Math]::Floor($pack.Symbols[$bankOwner]/0x4000)
        $key='bank:{0:x2}:{1}' -f $bank,$label
    }elseif($pack.Symbols.ContainsKey("ambiguous:$label")){
        throw "$($pack.Name) ${label}: ambiguous compiled symbol; supply its source bank owner."
    }
    if (-not $pack.Symbols.ContainsKey($key)) { throw "$($pack.Name) ${label}: missing compiled symbol '$key'." }
    if($pack.Symbols.ContainsKey("ambiguous:$key")){throw "$($pack.Name) ${label}: ambiguous compiled symbol '$key' in the requested source bank."}
    $start = [int]$pack.Symbols[$key]
    if ($count -le 0 -or $start + $count -gt $pack.Bytes.Length -or
        [int][Math]::Floor($start / 0x4000) -ne [int][Math]::Floor(($start + $count - 1) / 0x4000)) {
        throw "$($pack.Name) ${label}: invalid table extent $count."
    }
    return ,([byte[]]$pack.Bytes[$start..($start + $count - 1)])
}
function Add-MenuRomOam($pack, $rows, [string]$layout, [string]$label, [int]$expected) {
    $count = (Read-MenuRomBytes $pack $label 1)[0]
    if ($count -ne $expected) { throw "$($pack.Name) ${label}: expected $expected OAM parts, got $count." }
    $bytes = Read-MenuRomBytes $pack $label (1 + $count * 4)
    for ($part = 0; $part -lt $count; $part++) {
        $fields = @($bytes[(1 + $part * 4)..(4 + $part * 4)] | ForEach-Object { $_.ToString('x2') }) -join "`t"
        $rows.Add("$layout`t$part`t$fields`t$label`t`tcode/bank2.s:$label+$((1 + 4 * $part).ToString('x2'))")
    }
}
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Destination = $destination; PassiveCount = 32 },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Destination = (Split-Path $previewDestination -Parent); PassiveCount = 27 }
)) {
    foreach ($spec in @(
        @('inventory_item_slots', 'inventorySubscreen0_drawStoredItems@itemPositions', 16),
        @('inventory_essence_tiles', 'itemSubmenu2EssencePositions', 8)
    )) {
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# index`ttilemap-offset`tsource-label`tsource")
        $bytes = Read-MenuRomBytes $pack $spec[1] ($spec[2] * 2)
        for ($index = 0; $index -lt $spec[2]; $index++) {
            $pointer = [BitConverter]::ToUInt16($bytes, $index * 2)
            $offset = $pointer - [int]$pack.Symbols['w4TileMap']
            if ($offset -lt 0 -or $offset -ge 0x400) { throw "$($pack.Name) $($spec[1]): tilemap pointer outside w4TileMap." }
            $rows.Add("$index`t$($offset.ToString('x3'))`t$($spec[1])`tcode/bank2.s:$($spec[1])+$(( $index * 2).ToString('x2'))")
        }
        Write-GeneratedTable((Join-Path $pack.Destination "menu/$($spec[0]).tsv"), $rows)
    }
    $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# option-count`tindex`tmax-width`tx-nibble`tsource-label`tsource")
    $widths = Read-MenuRomBytes $pack 'inventoryMenuState2@itemSubmenuWidths' 4
    $pointers = Read-MenuRomBytes $pack 'table_5ae5' 8
    for ($count = 2; $count -le 5; $count++) {
        $label = "table_5ae5@data$count"
        $target = [int]$pack.Symbols[$label]
        $pointer = [BitConverter]::ToUInt16($pointers, ($count - 2) * 2)
        if (($target -band 0x3fff) + 0x4000 -ne $pointer) { throw "$($pack.Name) table_5ae5: unresolved $label pointer." }
        $positions = Read-MenuRomBytes $pack $label $count
        for ($index = 0; $index -lt $count; $index++) {
            $rows.Add("$count`t$index`t$($widths[$count - 2].ToString('x2'))`t$($positions[$index].ToString('x2'))`t$label`tcode/bank2.s:$label+$($index.ToString('x2'))")
        }
    }
    Write-GeneratedTable((Join-Path $pack.Destination 'menu/inventory_item_submenu_layout.tsv'), $rows)
    $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# seed-type`ty-offset`tx-offset`ttile`tattributes`tsource-label`tsource")
    $seedPointers = Read-MenuRomBytes $pack 'seedAndHarpSpriteTable' $(if ($pack.Name -eq 'ages') { 8 } else { 5 })
    for ($seed = 0; $seed -lt 5; $seed++) {
        $label = "seedAndHarpSpriteTable@sprite$seed"
        if ($pack.Symbols['seedAndHarpSpriteTable'] + $seed + $seedPointers[$seed] -ne $pack.Symbols[$label]) {
            throw "$($pack.Name) seedAndHarpSpriteTable: relative seed pointer $seed differs from $label."
        }
        $bytes = Read-MenuRomBytes $pack $label 5
        if ($bytes[0] -ne 1) { throw "$($pack.Name) ${label}: expected one seed OAM part." }
        $fields = @($bytes[1..4] | ForEach-Object { $_.ToString('x2') }) -join "`t"
        $rows.Add("$seed`t$fields`t$label`tcode/bank2.s:$label")
    }
    Write-GeneratedTable((Join-Path $pack.Destination 'menu/inventory_seed_submenu_oam.tsv'), $rows)
    $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# index`ttreasure`ttreasure-id`tposition`tslot`tsource-label`tsource")
    $bytes = Read-MenuRomBytes $pack 'subscreen1TreasureData' ($pack.PassiveCount * 3 + 1)
    for ($index = 0; $index -lt $pack.PassiveCount; $index++) {
        $id = $bytes[$index * 3]; $position = $bytes[$index * 3 + 1]; $slot = $bytes[$index * 3 + 2]
        if ($id -eq 0 -or $slot -gt 14) { throw "$($pack.Name) subscreen1TreasureData: invalid passive row $index." }
        $rows.Add("$index`t`$$($id.ToString('x2'))`t$($id.ToString('x2'))`t$($position.ToString('x2'))`t$slot`tsubscreen1TreasureData`tcode/bank2.s:subscreen1TreasureData+$((3 * $index).ToString('x2'))")
    }
    if ($bytes[$pack.PassiveCount * 3] -ne 0) { throw "$($pack.Name) subscreen1TreasureData: missing source terminator." }
    Write-GeneratedTable((Join-Path $pack.Destination 'menu/inventory_passive_treasures.tsv'), $rows)
    $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# index`tpacked`tsource-label`tsource")
    $bytes = Read-MenuRomBytes $pack 'inventorySubmenu1_drawCursor@data' 21
    for ($index = 0; $index -lt 21; $index++) {
        $rows.Add("$index`t$($bytes[$index].ToString('x2'))`tinventorySubmenu1_drawCursor@data`tcode/bank2.s:inventorySubmenu1_drawCursor@data+$($index.ToString('x2'))")
    }
    Write-GeneratedTable((Join-Path $pack.Destination 'menu/inventory_secondary_cursors.tsv'), $rows)
    $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# index`traw-y`traw-x`tsource-label`tsource")
    $bytes = Read-MenuRomBytes $pack 'inventorySubmenu2_drawCursor@offsets' 22
    for ($index = 0; $index -lt 11; $index++) {
        $rows.Add("$index`t$($bytes[2 * $index].ToString('x2'))`t$($bytes[2 * $index + 1].ToString('x2'))`tinventorySubmenu2_drawCursor@offsets`tcode/bank2.s:inventorySubmenu2_drawCursor@offsets+$((2 * $index).ToString('x2'))")
    }
    Write-GeneratedTable((Join-Path $pack.Destination 'menu/inventory_essence_cursors.tsv'), $rows)
    $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# layout`tpart`ty`tx`ttile`tattributes`tsource-label`talias-of`tsource")
    Add-MenuRomOam $pack $rows 'maku-seed' 'inventoryMenuDrawSprites@makuSeedSprite' 4
    if ($pack.Name -eq 'ages') {
        foreach ($song in 0..3) {
            Add-MenuRomOam $pack $rows "harp-$song" "seedAndHarpSpriteTable@sprite$($song + 4)" $(if ($song -eq 0) { 1 } else { 2 })
        }
        foreach ($mask in 0..2) {
            Add-MenuRomOam $pack $rows "submenu-mask-$mask" "createBlankSpritesForItemSubmenu@sprites$mask" (@(8, 2, 6)[$mask])
        }
    }
    Write-GeneratedTable((Join-Path $pack.Destination 'menu/inventory_oam.tsv'), $rows)
    $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# layout`tpart`ty`tx`ttile`tattributes`tsource-label`talias-of`tsource")
    foreach ($spec in @(
        @('list-cursor', 'ringMenu_drawSprites@cursorSprite', 1), @('page-arrows', 'ringMenu_drawSprites@arrowSprites', 2),
        @('equipped-marker', 'ringMenu_drawEquippedRingSprite@equippedSprite', 1),
        @('box-cursor', 'ringMenu_drawRingBoxCursor@ringBoxCursor', 1), @('list-box-marker', 'ringMenu_drawSpritesForRingsInBox@sprite', 1)
    )) { Add-MenuRomOam $pack $rows $spec[0] $spec[1] $spec[2] }
    Write-GeneratedTable((Join-Path $pack.Destination 'menu/ring_oam.tsv'), $rows)
    $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# slot`tx-offset`tsource-label`tsource")
    $bytes = Read-MenuRomBytes $pack 'ringMenu_getSpriteOffsetForRingBoxPosition@offsets' 5
    for ($slot = 0; $slot -lt 5; $slot++) {
        $rows.Add("$slot`t$($bytes[$slot].ToString('x2'))`tringMenu_getSpriteOffsetForRingBoxPosition@offsets`tcode/bank2.s:ringMenu_getSpriteOffsetForRingBoxPosition@offsets+$($slot.ToString('x2'))")
    }
    Write-GeneratedTable((Join-Path $pack.Destination 'menu/ring_box_oam_offsets.tsv'), $rows)
}
