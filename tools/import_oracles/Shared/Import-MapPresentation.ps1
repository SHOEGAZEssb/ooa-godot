# MENU_MAP shares bank-2 code, with native per-game dimensions and tables.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Destination = $destination; Defines = 'ROM_AGES;AGES_ENGINE'; Floors = 14; Entrances = 16; Substitutions = 7 },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Destination = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE'; Floors = 12; Entrances = 10; Substitutions = 8 }
)) {
    $hostConnection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        function Read-MapNodes([string]$path, [string]$label, [string]$query = 'DATA_DIRECTIVES') {
            $full = Join-Path $Disassembly $path
            return @((Invoke-AssemblySourceHost $hostConnection $query "$full`0$label`0" | ConvertFrom-Json) | Where-Object IsActive)
        }
        $geometry = [Collections.Generic.List[string]]::new()
        $geometry.Add("# group`twidth`theight`tstart-x`tstart-y`tpopup-shift-x`tpopup-shift-y`tsource")
        $cells = [Collections.Generic.List[string]]::new(); $cells.Add("# group`troom`ttext`tpopup`tsource")
        $constants = @(Read-MapNodes 'constants/common/other.s' '' 'NODES')
        foreach ($group in 0..1) {
            $prefix = if ($pack.Name -eq 'seasons' -and $group -eq 1) { 'SUBROSIA' } else { 'OVERWORLD' }
            $names = @("${prefix}_WIDTH", "${prefix}_HEIGHT", "${prefix}_MAP_START_X", "${prefix}_MAP_START_Y")
            $shiftPrefix = if ($prefix -eq 'OVERWORLD') { 'OVERWORD' } else { $prefix }
            $names += @("${shiftPrefix}_MAP_POPUP_SHIFT_INDEX_X", "${shiftPrefix}_MAP_POPUP_SHIFT_INDEX_Y")
            $values = @(foreach ($name in $names) {
                $nodes = @($constants | Where-Object { $_.Kind -eq 'Constant' -and $_.Name -ceq $name -and $_.Operands.Count -eq 1 })
                if ($nodes.Count -ne 1) { throw "$($pack.Name): missing or ambiguous native map constant $name." }
                Convert-AssemblyInteger $nodes[0].Operands[0]
            })
            $geometry.Add("$group`t$($values -join "`t")`tconstants/common/other.s:$prefix")
            $label = if ($group -eq 0) { 'presentMapTextIndices' } else { 'pastMapTextIndices' }
            $length = $values[0] * $values[1]
            $textBytes = Read-MenuRomBytes $pack $label $length
            $sourceRows = @(Read-MapNodes "data/$($pack.Name)/mapTextAndPopups.s" $label)
            if (@($sourceRows | ForEach-Object Operands).Count -ne $length) { throw "$($pack.Name) ${label}: map text extent differs from native dimensions." }
            $popupLabel = if ($group -eq 0) { 'presentMinimapPopups' } else { 'pastMinimapPopups' }
            $popups = @{}; $terminated = $false
            foreach ($node in Read-MapNodes "data/$($pack.Name)/mapTextAndPopups.s" $popupLabel) {
                if ($terminated) { throw "$($pack.Name) ${popupLabel}: data after terminator." }
                $room = Convert-AssemblyInteger $node.Operands[0]
                if ($room -eq 0xff -and $node.Operands.Count -eq 1) { $terminated = $true; continue }
                if ($node.Operands.Count -ne 2) { throw "$($pack.Name) ${popupLabel}: expected room/popup pair." }
                if (-not $popups.ContainsKey($room)) { $popups[$room] = Convert-AssemblyInteger $node.Operands[1] }
            }
            if (-not $terminated) { throw "$($pack.Name) ${popupLabel}: missing terminator." }
            for ($y = 0; $y -lt $values[1]; $y++) {
                for ($x = 0; $x -lt $values[0]; $x++) {
                    $room = $y * 16 + $x; $popup = if ($popups.ContainsKey($room)) { $popups[$room] } else { 0 }
                    $cells.Add(("{0}`t{1:x2}`t{2:x2}`t{3:x2}`tdata/{4}/mapTextAndPopups.s:{5}" -f $group, $room, $textBytes[$y * $values[0] + $x], $popup, $pack.Name, $label))
                }
            }
        }
        Write-GeneratedTable((Join-Path $pack.Destination 'map/geometry.tsv'), $geometry)
        Write-GeneratedTable((Join-Path $pack.Destination 'map/cells.tsv'), $cells)

        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# group`troom`tpopup")
        $treeRows = [Collections.Generic.List[string]]::new(); $treeRows.Add('# era`tindex`troom`tposition`tpopup`tsource')
        $groups = if ($pack.Name -eq 'ages') { @(0, 1) } else { @(0) }
        foreach ($group in $groups) {
            $label = if ($pack.Name -eq 'seasons') { 'treeWarps' } elseif ($group -eq 0) { 'presentTreeWarps' } else { 'pastTreeWarps' }
            $records = @(Read-MapNodes "data/$($pack.Name)/treeWarps.s" $label); $index = 0
            $expected = if ($pack.Name -eq 'ages' -and $group -eq 0) { 9 } else { 8 }
            if ($records.Count -ne $expected) { throw "$($pack.Name) ${label}: unexpected native tree extent." }
            foreach ($node in $records) {
                if ($node.Operands.Count -ne 3) { throw "$($pack.Name) ${label}: expected three-byte tree record." }
                $values = @($node.Operands | ForEach-Object { (Convert-AssemblyInteger $_).ToString('x2') }) -join "`t"
                $treeRows.Add("$group`t$index`t$values`tdata/$($pack.Name)/treeWarps.s:${label}+$index"); $index++
            }
            foreach ($node in $records) {
                if ($node.Operands.Count -ne 3) { throw "$($pack.Name) ${label}: expected three-byte tree record." }
                $room = Convert-AssemblyInteger $node.Operands[0]
                if ($room -eq 0) { break }
                $popup = Convert-AssemblyInteger $node.Operands[2]
                $rows.Add(("{0}`t{1:x2}`t{2:x2}" -f $group, $room, $popup))
            }
        }
        Write-GeneratedTable((Join-Path $pack.Destination 'map/tree_warps.tsv'), $rows)
        Write-GeneratedTable((Join-Path $pack.Destination 'metadata/gale_tree_warps.tsv'), $treeRows)
        $bytes = Read-MenuRomBytes $pack 'mapMenu_dungeonEntranceText' ($pack.Entrances * 2)
        if (@(Read-MapNodes 'code/bank2.s' 'mapMenu_dungeonEntranceText').Count -ne $pack.Entrances) { throw "$($pack.Name): entrance source extent changed." }
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# dungeon`tgroup`troom`tfallback-text")
        for ($index = 0; $index -lt $pack.Entrances; $index++) {
            $group = if ($bytes[$index * 2 + 1] -band 0x80) { 4 } else { 5 }
            $rows.Add(("{0}`t{1}`t{2:x2}`t{3:x2}" -f $index, $group, $bytes[$index * 2], ($bytes[$index * 2 + 1] -band 0x7f)))
        }
        Write-GeneratedTable((Join-Path $pack.Destination 'map/dungeon_entrances.tsv'), $rows)
        Write-GeneratedBytes((Join-Path $pack.Destination 'map/gasha_rooms.bin'), (Read-MenuRomBytes $pack 'gashaSpotRooms' 16))

        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# selector`torder`theight`twidth`tdestination`tsource-offset`tsource")
        $label = 'mapMenu_tileSubstitutionTable'; $start = [int]$pack.Symbols[$label]
        $pointers = Read-MenuRomBytes $pack $label $pack.Substitutions
        for ($selector = 0; $selector -lt $pack.Substitutions; $selector++) {
            $at = $start + $selector + $pointers[$selector]
            if ($at -ne $pack.Symbols["$label@subst$selector"]) { throw "$($pack.Name) ${label}: invalid relative pointer $selector." }
            $order = 0
            while ($pack.Bytes[$at] -ne 0) {
                if ($order -ge 8) { throw "$($pack.Name) ${label}: unterminated selector $selector." }
                $height = $pack.Bytes[$at] -shr 4; $width = $pack.Bytes[$at] -band 15
                $to = [BitConverter]::ToUInt16($pack.Bytes, $at + 1) - [int]$pack.Symbols['w4TileMap']
                $from = [BitConverter]::ToUInt16($pack.Bytes, $at + 3) - [int]$pack.Symbols['w4TileMap']
                if ($height -eq 0 -or $width -eq 0 -or $to -lt 0 -or $from -lt 0 -or [Math]::Max($to, $from) + ($height - 1) * 32 + $width -gt 0x400) { throw "$($pack.Name) ${label}: substitution outside native tilemap." }
                $rows.Add(("{0}`t{1}`t{2}`t{3}`t{4:x3}`t{5:x3}`tcode/bank2.s:{6}@subst{0}" -f $selector, $order, $height, $width, $to, $from, $label))
                $at += 5; $order++
            }
        }
        Write-GeneratedTable((Join-Path $pack.Destination 'map/substitutions.tsv'), $rows)

        $rows = [Collections.Generic.List[string]]::new(); $rows.Add('# index`tlabel`talias-of`tsprite-count`tleft-y`tleft-x`tleft-tile`tleft-attributes`tright-y`tright-x`tright-tile`tright-attributes`tsource')
        $pointers = Read-MenuRomBytes $pack 'mapIconOamTable' 26; $aliases = @{}
        $sourcePointers = @(Read-MapNodes 'code/bank2.s' 'mapIconOamTable' 'MACRO_INVOCATIONS' | Where-Object Name -eq 'dbrel')
        if ($sourcePointers.Count -ne 26) { throw "$($pack.Name) mapIconOamTable: expected 26 native relative pointers." }
        foreach ($index in 0..25) {
            $local = $sourcePointers[$index].Operands[0]; $label = "mapIconOamTable$local"
            $at = [int]$pack.Symbols['mapIconOamTable'] + $index + $pointers[$index]
            if ($at -ne $pack.Symbols[$label]) { throw "$($pack.Name) ${label}: source and compiled OAM pointers differ." }
            $count = [int]$pack.Bytes[$at]
            if ($count -notin @(0, 2)) { throw "$($pack.Name) mapIconOamTable: invalid OAM count $index." }
            $data = if ($count -eq 0) { [byte[]]::new(8) } else { [byte[]]$pack.Bytes[($at + 1)..($at + 8)] }
            $signature = [Convert]::ToBase64String($data) + "/$count"
            $alias = if ($aliases.ContainsKey($signature)) { $aliases[$signature] } else { $aliases[$signature] = $local; '' }
            $fields = @($data | ForEach-Object { $_.ToString('x2') }) -join "`t"
            $rows.Add("$index`t$local`t$alias`t$count`t$fields`tcode/bank2.s:$label")
        }
        Write-GeneratedTable((Join-Path $pack.Destination 'menu/map_icons.tsv'), $rows)
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add('# layout`tpart`ty`tx`ttile`tattributes`tsource-label`talias-of`tsource')
        foreach ($spec in @(
            @('arrow', 'mapMenu_drawArrow@sprite', 1), @('cursor', 'mapMenu_drawCursor@sprite', 2),
            @('warp', 'mapMenu_drawWarpSites@spriteData', 1), @('floor-cursor', 'dungeonMap_drawFloorCursor@cursorOamData', 1),
            @('boss-floor', 'dungeonMap_drawBossSymbolForFloor@bossSymbolOamData', 1), @('link-map', 'dungeonMap_drawLinkIcons@linkOnMapOamData', 1),
            @('link-floor', 'dungeonMap_drawLinkIcons@linkOnFloorListOamData', 1), @('dungeon-cursor', 'dungeonMap_drawCursor@cursorSprites', 2),
            @('up', 'dungeonMap_drawArrows@upArrow', 1), @('down', 'dungeonMap_drawArrows@downArrow', 1),
            @('map', 'dungeonMap_drawItemSprites@mapSprite', 2), @('compass', 'dungeonMap_drawItemSprites@compassSprite', 2),
            @('boss-key', 'dungeonMap_drawItemSprites@bossKeySprite', 2), @('small-key', 'dungeonMap_drawItemSprites@smallKeySprite', 1),
            @('border1', 'mapIconBorderOamTable@entry1', 1), @('border2', 'mapIconBorderOamTable@entry2', 2),
            @('border3', 'mapIconBorderOamTable@entry3', 8), @('border4', 'mapIconBorderOamTable@entry4', 8)
        )) { Add-MenuRomOam $pack $rows $spec[0] $spec[1] $spec[2] }
        if ($pack.Name -eq 'ages') { Add-MenuRomOam $pack $rows 'portal' 'mapMenu_drawTimePortal@portalSprite' 1 }
        else { Add-MenuRomOam $pack $rows 'jewel' 'mapMenu_drawJewelLocations@sprite' 1 }
        Write-GeneratedTable((Join-Path $pack.Destination 'menu/map_oam.tsv'), $rows)
        Write-GeneratedBytes((Join-Path $pack.Destination 'menu/dungeon_symbols.bin'), (Read-MenuRomBytes $pack 'dungeonMapSymbolPositions' ($pack.Floors * 2)))
        Write-GeneratedBytes((Join-Path $pack.Destination 'menu/dungeon_floor_names.bin'), (Read-MenuRomBytes $pack 'dungeonMapFloorNameTiles' 22))
        $offsets = Read-MenuRomBytes $pack 'dungeonMapFloorListStartPositions' $pack.Floors
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add('# dungeon`toffset`tsource-label`tsource')
        for ($index = 0; $index -lt $pack.Floors; $index++) {
            $rows.Add("$index`t$($offsets[$index].ToString('x2'))`tdungeonMapFloorListStartPositions`tcode/bank2.s:dungeonMapFloorListStartPositions+$($index.ToString('x2'))")
        }
        Write-GeneratedTable((Join-Path $pack.Destination 'menu/dungeon_floor_list.tsv'), $rows)
    } finally {
        [void](Invoke-AssemblySourceHost $hostConnection 'QUIT' '')
        $hostConnection.WaitForExit(); $hostConnection.Dispose()
    }
}
