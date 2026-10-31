# loadTreasureDisplayData consumes a source-ordered selector stream and a
# game-specific pointer table. Preserve both; item IDs do not imply a table.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Destination = $destination; Texts = $allTexts; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Destination = (Split-Path $previewDestination -Parent); Texts = $seasonInventoryTexts; Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $hostConnection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        function Read-InventoryNodes([string]$path, [string]$label, [string]$directive) {
            $full = Join-Path $Disassembly $path
            $json = Invoke-AssemblySourceHost $hostConnection 'DATA_DIRECTIVES' "$full`0$label`0$directive"
            return @(($json | ConvertFrom-Json) | Where-Object IsActive)
        }
        $source = "data/$($pack.Name)/treasureDisplayData.s"
        $pointers = @(Read-InventoryNodes $source 'treasureDisplayData2' '.dw')
        $compiledPointers = Read-MenuRomBytes $pack 'treasureDisplayData2' ($pointers.Count * 2)
        $tables = [Collections.Generic.List[object]]::new()
        $displayRows = [Collections.Generic.List[string]]::new()
        $displayRows.Add("# table`tindex`ttreasure-id`tleft-sprite`tleft-palette`tright-sprite`tright-palette`textra-mode`ttext-low")
        $prefixRows = [Collections.Generic.List[string]]::new()
        $prefixRows.Add("# table`ttreasure-id`tleft-sprite`tleft-palette`tright-sprite`tright-palette`textra-mode`ttext-low`tsource")
        $exported = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        for ($index = 0; $index -lt $pointers.Count; $index++) {
            $expression = $pointers[$index].OperandText.Trim()
            if ($expression -notmatch '^(?<label>\w+)(?:\s*-\s*(?<bias>7))?$') {
                throw "${source}: unsupported display pointer '$expression'."
            }
            $label = $Matches['label']; $bias = if ($Matches['bias']) { -1 } else { 0 }
            $target = [int]$pack.Symbols[$label]
            $pointer = [BitConverter]::ToUInt16($compiledPointers, $index * 2)
            if (($target -band 0x3fff) + 0x4000 + $bias * 7 -ne $pointer) {
                throw "${source}: compiled display pointer $index differs from $expression."
            }
            $tables.Add(@{ Label = $label; Bias = $bias })
            if ($bias -eq -1) {
                $fields = @($pack.Bytes[($target - 7)..($target - 1)] | ForEach-Object { $_.ToString('x2') }) -join "`t"
                $prefixRows.Add("$label`t$fields`t${source}:$label-7")
            }
            if (-not $exported.Add($label)) { continue }
            $records = @(Read-InventoryNodes $source $label '.db')
            if ($records.Count -eq 0) { throw "${source}:${label}: empty display table." }
            foreach ($record in $records) {
                if ($record.Operands.Count -ne 7) { throw "${source}:${label}: expected seven-byte display rows." }
            }
            $bytes = Read-MenuRomBytes $pack $label ($records.Count * 7)
            for ($row = 0; $row -lt $records.Count; $row++) {
                $fields = @($bytes[($row * 7)..($row * 7 + 6)] | ForEach-Object { $_.ToString('x2') }) -join "`t"
                $displayRows.Add("$label`t$row`t$fields")
            }
        }
        Write-GeneratedTable((Join-Path $pack.Destination 'metadata/treasure_display.tsv'), $displayRows)
        Write-GeneratedTable((Join-Path $pack.Destination 'metadata/treasure_display_prefixes.tsv'), $prefixRows)
        $selectors = @(Read-InventoryNodes $source 'treasureDisplayData1' '.db')
        $compiledSelectors = Read-MenuRomBytes $pack 'treasureDisplayData1' ($selectors.Count * 3)
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# index`ttreasure-id`tvariable`ttable`tindex-bias`tsource")
        $terminated = $false
        for ($index = 0; $index -lt $selectors.Count; $index++) {
            $fields = $selectors[$index].Operands
            if ($fields.Count -ne 3 -or $terminated) { throw "${source}: malformed display selector stream." }
            $treasure = [int]$compiledSelectors[$index * 3]
            $variable = $fields[1].TrimStart('<')
            $table = [int]$compiledSelectors[$index * 3 + 2]
            if ($treasure -eq 0) {
                if ($compiledSelectors[$index * 3 + 1] -ne 0 -or $table -ne 0) { throw "${source}: invalid display-selector terminator." }
                $terminated = $true; continue
            }
            if (-not $pack.Symbols.ContainsKey($variable) -or
                $pack.Symbols[$variable] -ne 0xc600 + $compiledSelectors[$index * 3 + 1] -or
                $table -ge $tables.Count) { throw "${source}: unresolved selector $index variable/table." }
            $entry = $tables[$table]
            $rows.Add("$index`t$($treasure.ToString('x2'))`t$variable`t$($entry.Label)`t$($entry.Bias)`t${source}:treasureDisplayData1+$((3 * $index).ToString('x2'))")
        }
        if (-not $terminated) { throw "${source}: missing display-selector terminator." }
        Write-GeneratedTable((Join-Path $pack.Destination 'metadata/treasure_display_selectors.tsv'), $rows)

        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# kind`tindex`tname-text-id`tdescription-text-id`tmessage-base64")
        foreach ($id in @($pack.Texts.Keys | Where-Object { $_ -ge 0x0900 -and $_ -lt 0x0a00 } | Sort-Object)) {
            $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pack.Texts[$id]))
            $rows.Add("item`t$(($id -band 0xff).ToString('x2'))`t$($id.ToString('x4'))`tffff`t$encoded")
        }
        foreach ($ring in 0..0x3f) {
            $name = 0x3040 + $ring; $description = 0x3080 + $ring
            if (-not $pack.Texts.ContainsKey($name) -or -not $pack.Texts.ContainsKey($description)) {
                throw "$($pack.Name): missing inventory ring TX_$($name.ToString('x4')) / TX_$($description.ToString('x4'))."
            }
            $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$($pack.Texts[$name])`n$($pack.Texts[$description])"))
            $rows.Add("ring`t$($ring.ToString('x2'))`t$($name.ToString('x4'))`t$($description.ToString('x4'))`t$encoded")
        }
        Write-GeneratedTable((Join-Path $pack.Destination 'metadata/inventory_text.tsv'), $rows)

        $bank2 = 'code/bank2.s'
        $blurbCount = if ($pack.Name -eq 'ages') { 2 } else { 4 }
        $bytes = Read-MenuRomBytes $pack 'itemSubmenu2BlurbDisplayData' ($blurbCount * 10)
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# index`tsprite-0`tflags-0`tsprite-1`tflags-1`tsprite-2`tflags-2`tsprite-3`tflags-3`tsource")
        for ($index = 0; $index -lt $blurbCount; $index++) {
            $at = $index * 10
            if ($bytes[$at + 4] -ne 0xff -or $bytes[$at + 9] -ne 0xff) { throw "$($pack.Name) itemSubmenu2BlurbDisplayData: expected two non-quantity records." }
            $fields = @(@($at, ($at + 1), ($at + 2), ($at + 3), ($at + 5), ($at + 6), ($at + 7), ($at + 8)) |
                ForEach-Object { $bytes[$_].ToString('x2') }) -join "`t"
            $rows.Add("$index`t$fields`t${bank2}:itemSubmenu2BlurbDisplayData+$($at.ToString('x2'))")
        }
        Write-GeneratedTable((Join-Path $pack.Destination 'menu/inventory_blurbs.tsv'), $rows)
        $bytes = Read-MenuRomBytes $pack 'itemSubmenu2TextIndices' 11
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# index`ttext-low`tsource")
        for ($index = 0; $index -lt 11; $index++) {
            $rows.Add("$index`t$($bytes[$index].ToString('x2'))`t${bank2}:itemSubmenu2TextIndices+$($index.ToString('x2'))")
        }
        Write-GeneratedTable((Join-Path $pack.Destination 'menu/inventory_essence_texts.tsv'), $rows)

        $source = "data/$($pack.Name)/gfxHeaders.s"
        $headers = Read-ImportText (Join-Path $Disassembly $source)
        $body = [regex]::Match($headers, '(?ms)^m_GfxHeaderStart\s+\$08,\s*GFXH_INVENTORY_SCREEN\s*\r?\n(?<body>.*?)(?=^m_GfxHeaderStart|\z)')
        if (-not $body.Success) { throw "${source}: missing native inventory graphics header." }
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# index`tbank`ttile`tasset`tinterleaved`tinverted`tsource")
        foreach ($line in $body.Groups['body'].Value -split '\r?\n') {
            $code = ($line -split ';', 2)[0].Trim()
            if (-not $code) { continue }
            if ($code -match '^m_GfxHeader\s+\w+,\s*w4(?:TileMap|AttributeMap)\+\$1e0$') { continue }
            if ($code -notmatch '^m_GfxHeader\s+(?<name>\w+),\s*\$(?<address>8[0-9a-f]{3})$') { throw "${source}: unsupported inventory upload '$code'." }
            $name = $Matches['name']; $address = [Convert]::ToInt32($Matches['address'], 16)
            $paths = @(foreach ($kind in @('gfx', 'gfx_compressible')) {
                foreach ($game in @($pack.Name, 'common')) {
                    $relative = "$kind/$game/$name.png"
                    if (Test-Path -LiteralPath (Join-Path $Disassembly $relative)) { $relative }
                }
            })
            if ($paths.Count -ne 1) { throw "${source}: missing or ambiguous source $name.png." }
            $interleaved = $name.StartsWith('spr_'); $inverted = $interleaved
            $properties = [IO.Path]::ChangeExtension((Join-Path $Disassembly $paths[0]), '.properties')
            if (Test-Path -LiteralPath $properties) {
                foreach ($property in [regex]::Matches((Read-ImportText $properties), '(?im)^\s*(?<key>interleave|invert):\s*(?<value>\S+)\s*$')) {
                    $value = $false
                    if (-not [bool]::TryParse($property.Groups['value'].Value, [ref]$value)) { throw "${properties}: invalid boolean graphics property." }
                    if ($property.Groups['key'].Value -eq 'interleave') { $interleaved = $value } else { $inverted = $value }
                }
            }
            $asset = if ($name.StartsWith('spr_item_icons_')) { "gfx/$name.png" } else { "inventory/$name.png" }
            # Ages already produces these exact files; Seasons' base OBJ item
            # sheets are supplied by its room/Link graphics producer.
            if ($pack.Name -eq 'seasons' -and $name -notin @('spr_item_icons_2', 'spr_item_icons_3')) {
                Copy-GeneratedFile $paths[0] $asset $pack.Destination
            }
            $index = $rows.Count - 1
            $rows.Add("$index`t$($address -band 1)`t$((($address -band 0xfff0) - 0x8000 -shr 4).ToString('x2'))`t$asset`t$([int]$interleaved)`t$([int]$inverted)`t${source}:GFXH_INVENTORY_SCREEN+$($index.ToString('x2'))")
        }
        if ($rows.Count -ne 17) { throw "${source}: expected 16 native inventory VRAM uploads." }
        Write-GeneratedTable((Join-Path $pack.Destination 'menu/inventory_graphics.tsv'), $rows)

        if ($pack.Name -eq 'seasons') {
            foreach ($name in @('map_inventory_screen_1', 'flg_inventory_screen_1', 'map_inventory_screen_2', 'flg_inventory_screen_2',
                    'map_inventory_screen_3', 'flg_inventory_screen_3', 'map_inventory_textbar', 'flg_inventory_textbar',
                    'map_appraised_ring_list', 'flg_appraised_ring_list', 'map_unappraised_ring_list', 'flg_unappraised_ring_list')) {
                $paths = @(foreach ($game in @('seasons', 'common')) {
                    $relative = "gfx_compressible/$game/$name.bin"
                    if (Test-Path -LiteralPath (Join-Path $Disassembly $relative)) { $relative }
                })
                if ($paths.Count -ne 1) { throw "Seasons inventory: missing or ambiguous $name.bin." }
                Copy-GeneratedFile $paths[0] "inventory/$name.bin" $pack.Destination
            }
            Copy-GeneratedFile 'gfx/common/map_rings.bin' 'inventory/map_rings.bin' $pack.Destination
        }
        $source = "data/$($pack.Name)/paletteHeaders.s"
        $body = [regex]::Match((Read-ImportText (Join-Path $Disassembly $source)), '(?ms)^m_PaletteHeaderStart\s+\$0a,\s*PALH_0a\s*\r?\n(?<body>.*?)(?=^m_PaletteHeaderStart|\z)')
        if (-not $body.Success) { throw "${source}: missing inventory PALH_0a." }
        $background = [byte[]]::new(96)
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# palette`tshade`tred`tgreen`tblue`tsource")
        foreach ($entry in [regex]::Matches($body.Groups['body'].Value, 'm_PaletteHeader(?<kind>Bg|Spr)\s+(?<first>\d+),\s*(?<count>\d+),\s*(?<label>\w+)')) {
            $first = [int]$entry.Groups['first'].Value; $count = [int]$entry.Groups['count'].Value; $label = $entry.Groups['label'].Value
            if ($first + $count -gt 8) { throw "${source}: inventory palette exceeds its eight slots." }
            $colors = Read-MenuRomBytes $pack $label ($count * 8)
            for ($color = 0; $color -lt $count * 4; $color++) {
                $rgb = [BitConverter]::ToUInt16($colors, $color * 2)
                $r = $rgb -band 31; $g = ($rgb -shr 5) -band 31; $b = ($rgb -shr 10) -band 31
                if ($entry.Groups['kind'].Value -eq 'Bg') {
                    $at = ($first * 4 + $color) * 3
                    $background[$at] = $r; $background[$at + 1] = $g; $background[$at + 2] = $b
                } else {
                    $rows.Add("$($first + [int][Math]::Floor($color / 4))`t$($color % 4)`t$r`t$g`t$b`t${source}:PALH_0a/$label+$((2 * $color).ToString('x2'))")
                }
            }
        }
        if ($pack.Name -eq 'seasons') { Write-GeneratedBytes((Join-Path $pack.Destination 'inventory/palette_bg.bin'), $background) }
        Write-GeneratedTable((Join-Path $pack.Destination 'menu/inventory_sprite_palettes.tsv'), $rows)
    }
    finally {
        [void](Invoke-AssemblySourceHost $hostConnection 'QUIT' '')
        $hostConnection.WaitForExit(); $hostConnection.Dispose()
    }
}
