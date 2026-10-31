# The supported unlinked opening ends with Impa releasing Link in room $00:$b6.
$seasonDestination = Split-Path $previewDestination -Parent
$seasonPalettes = Read-ImportText (Join-Path $Disassembly 'data/seasons/paletteData.s')
$seasonHeaders = Read-ImportText (Join-Path $Disassembly 'data/seasons/paletteHeaders.s')
$seasonGfx = Read-ImportText (Join-Path $Disassembly 'data/seasons/gfxHeaders.s')
$seasonUnique = Read-ImportText (Join-Path $Disassembly 'data/seasons/uniqueGfxHeaders.s')
$copied = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
function Copy-SeasonGraphic([string]$name) {
    if (-not $copied.Add($name)) { return }
    $matches = @(foreach ($kind in @('gfx', 'gfx_compressible')) {
        foreach ($game in @('seasons', 'common')) {
            $relative = "$kind/$game/$name.png"
            if (Test-Path -LiteralPath (Join-Path $Disassembly $relative)) { $relative }
        }
    })
    if ($matches.Count -ne 1) { throw "Seasons graphic ${name}: expected one source, found $($matches.Count)." }
    Copy-GeneratedFile $matches[0] "gfx/$name.png" $seasonDestination
}
function Read-SeasonHeader([string]$source, [string]$label, [string]$macro) {
    $match = [regex]::Match($source, '(?ms)^m_' + $macro + 'Start\s+\$[0-9a-f]+,\s*' + [regex]::Escape($label) + '\s*\r?\n(?<body>.*?)(?=^m_\w+Start|\z)')
    if (-not $match.Success) { throw "data/seasons: missing $macro $label." }
    return $match.Groups['body'].Value
}
function Export-SeasonPalette([string[]]$headers, [string]$path) {
    $background = [byte[]]::new(96)
    $sprites = [byte[]]::new(96)
    foreach ($header in $headers) {
        $body = Read-SeasonHeader $seasonHeaders $header 'PaletteHeader'
        foreach ($entry in [regex]::Matches($body, 'm_PaletteHeader(?<kind>Bg|Spr)\s+(?<first>\d+),\s*(?<count>\d+),\s*(?<label>\w+)')) {
            $first = [int]$entry.Groups['first'].Value; $count = [int]$entry.Groups['count'].Value
            if ($first + $count -gt 8) { throw "Seasons palette $header exceeds eight slots." }
            $bytes = Read-PaletteBytes $entry.Groups['label'].Value ($count * 4) $seasonPalettes
            $target = $background
            if ($entry.Groups['kind'].Value -eq 'Spr') { $target = $sprites }
            [Array]::Copy($bytes, 0, $target, $first * 12, $bytes.Length)
        }
    }
    Write-GeneratedBytes((Join-Path $seasonDestination "$path-bg.bin"), $background)
    Write-GeneratedBytes((Join-Path $seasonDestination "$path-spr.bin"), $sprites)
}
$graphicsRows = [Collections.Generic.List[string]]::new()
$graphicsRows.Add("# header`torder`tasset`taddress`tsource-offset`ttile-count")
function Export-SeasonGfxHeader([string]$label, [bool]$unique = $false) {
    $source = if ($unique) { $seasonUnique } else { $seasonGfx }
    $macro = if ($unique) { 'UniqueGfxHeader' } else { 'GfxHeader' }
    $body = Read-SeasonHeader $source $label $macro
    foreach ($line in $body -split '\r?\n') {
        if ($line -notmatch '^\s*m_GfxHeader\s+(?<name>\w+),\s*(?<destination>[^;]+)') { continue }
        $address = $Matches.destination.Trim()
        if ($address.StartsWith('$')) { continue }
        # These source uploads mirror an identical VRAM upload for UNCMP_GFXH_0e.
        $expected = if ($address -eq 'w3VramTiles') { '9800' } elseif ($address -eq 'w3VramAttributes') { '9801' } else { $null }
        if (-not $expected -or $body -notmatch ('m_GfxHeader\s+' + [regex]::Escape($Matches.name) + ',\s*\$' + $expected)) {
            throw "Seasons $label has unsupported graphics destination '$address'."
        }
    }
    $order = 0
    foreach ($node in [regex]::Matches($body, '(?m)^\s*m_GfxHeader\s+(?<name>\w+),\s*\$(?<address>[0-9a-f]{4})(?:,\s*\$(?<count>[0-9a-f]+),\s*\$(?<offset>[0-9a-f]+))?\s*$')) {
        $name = $node.Groups['name'].Value
        $asset = if ($name.StartsWith('map_') -or $name.StartsWith('flg_')) {
            $relative = "gfx_compressible/seasons/$name.bin"
            if (-not (Test-Path -LiteralPath (Join-Path $Disassembly $relative))) { $relative = "gfx_compressible/common/$name.bin" }
            Copy-GeneratedFile $relative "opening/$name.bin" $seasonDestination
            "opening/$name.bin"
        } else { Copy-SeasonGraphic $name; "gfx/$name.png" }
        $offset = if ($node.Groups['offset'].Success) { $node.Groups['offset'].Value } else { '0000' }
        $count = if ($node.Groups['count'].Success) { $node.Groups['count'].Value } else { '00' }
        $graphicsRows.Add("$label`t$order`t$asset`t$($node.Groups['address'].Value)`t$offset`t$count")
        $order++
    }
    if ($order -eq 0 -and $label -ne 'UNIQUE_GFXH_NONE') { throw "Seasons ${label}: no supported graphics uploads." }
}

# Overworld navigation and native side-view groups share one world catalog.
$tilesetSource = Read-ImportText (Join-Path $Disassembly 'data/seasons/tilesets.s')
$roomRows = [Collections.Generic.List[string]]::new()
$roomRows.Add("# room`tseason`ttileset`tlayout-group`tunique-header`tgraphics-header`tpalette-header`tanimation`tactive-collisions`tflags`tlayout")
$roomAliases = @{}
$pending = [Collections.Generic.List[string]]::new()
foreach ($node in Read-AssemblyNodes (Join-Path $Disassembly 'data/seasons/roomLayoutData.s')) {
    if ($node.Kind -eq 'Label' -and $node.Name -match '^room0[0-6][0-9a-f]{2}$') { $pending.Add($node.Name) }
    if ($node.Kind -eq 'MacroInvocation' -and $node.Name -eq 'm_RoomLayoutData') {
        $pending.Add($node.Operands[0])
        foreach ($alias in $pending) { $roomAliases[$alias] = $node.Operands[0] }
        $pending.Clear()
    }
}
$exportedHeaders = [Collections.Generic.HashSet[string]]::new()
$unsupportedHeaders = @{}
$mappingSources = @{}
foreach ($group in @(0,1,4,5,6,7)) {
    # tilesetAssignments.s aliases group6/group4 and group7/group5.
    $assignment = if ($group -lt 6) { $group } else { $group - 2 }
    $groupTilesets = [IO.File]::ReadAllBytes((Join-Path $Disassembly "rooms/seasons/group${assignment}Tilesets.bin"))
    # Import native geometry for shared placed spinners and the sliding-block puzzle.
    $selectedRooms = if ($group -eq 4) { @(0x26, 0xb1) } elseif ($group -eq 5) { @(0x6f, 0x86) } else { 0..255 }
foreach ($room in $selectedRooms) {
    $tileset = $groupTilesets[$room] -band 0x7f
    $block = [regex]::Match($tilesetSource, '(?ms)^tileset' + $tileset.ToString('x2') + 'Seasons:\s*(?<body>.*?)(?=^\w+:|\z)')
    if (-not $block.Success) {
        $block = [regex]::Match($tilesetSource, '(?ms)^\s*; 0x' + $tileset.ToString('x2') + '\s*\r?\n(?<body>.*?)(?=^\s*; 0x|^tileset\w+:|\z)')
    }
    if (-not $block.Success) { throw "Seasons room $($room.ToString('x2')): missing tileset $($tileset.ToString('x2'))." }
    $fields = @([regex]::Matches(($block.Groups['body'].Value -replace '(?m);[^\r\n]*',''), '\.db\s+([^\r\n]+)') | ForEach-Object { $_.Groups[1].Value.Split(',').Trim() })
    if ($fields.Count -eq 8) { $fields = @($fields * 4) }
    if ($fields.Count -ne 32) { throw "Seasons tileset ${tileset}: expected one or four eight-byte records." }
    foreach ($season in 0..3) {
        $record = $fields[($season * 8)..($season * 8 + 7)]
        if ($group -ge 6 -and ((Convert-AssemblyInteger $record[1]) -band 0x20) -eq 0) { continue }
        $layoutGroup = Convert-AssemblyInteger $record[6]
        $layoutId = 'room0' + $layoutGroup.ToString('x1') + $room.ToString('x2')
        if (-not $roomAliases.ContainsKey($layoutId)) { throw "Seasons $layoutId has no resolved layout source." }
        $size = if ($layoutGroup -lt 5) { 'small' } else { 'large' }
        Copy-GeneratedFile "rooms/seasons/$size/$($roomAliases[$layoutId]).bin" "world/rooms/$layoutId.bin" $seasonDestination
        foreach ($header in @($record[2], $record[3])) {
            if ($exportedHeaders.Add($header)) {
                $body = Read-SeasonHeader $(if ($header.StartsWith('UNIQUE_')) { $seasonUnique } else { $seasonGfx }) $header $(if ($header.StartsWith('UNIQUE_')) { 'UniqueGfxHeader' } else { 'GfxHeader' })
                $custom = @([regex]::Matches($body,'(?m)^\s*m_GfxHeader\s+\w+,\s*(?<destination>w6\w+)') | ForEach-Object { $_.Groups['destination'].Value })
                if ($custom.Count -gt 0) {
                    $unsupportedHeaders[$header] = "data/seasons/gfxHeaders.s:${header}: custom WRAM renderer destinations $($custom -join ',') are not implemented"
                } else { Export-SeasonGfxHeader $header ($header.StartsWith('UNIQUE_')) }
            }
        }
        Export-SeasonPalette @('PALH_0f', $record[4]) "world/rooms/$layoutId-$season"
        $mapping = (Convert-AssemblyInteger $record[5]).ToString('x2')
        if (-not $mappingSources.ContainsKey($mapping)) {
            $layoutHeaders = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'data/seasons/tilesetHeaders.s') "tilesetLayoutGroup$mapping" 'm_TilesetLayoutHeader')
            if ($layoutHeaders.Count -ne 2) { throw "Seasons layout $mapping must resolve mapping and collision headers." }
            $mappingSources[$mapping] = @($layoutHeaders[0].Operands[1], $layoutHeaders[1].Operands[1])
        }
        Copy-GeneratedFile "tileset_layouts/seasons/$($mappingSources[$mapping][0]).bin" "world/rooms/mappings-$mapping.bin" $seasonDestination
        Copy-GeneratedFile "tileset_layouts/seasons/$($mappingSources[$mapping][1]).bin" "world/rooms/collisions-$mapping.bin" $seasonDestination
        $activeCollisions = (Convert-AssemblyInteger $record[0]) -shr 4
        $dungeon = (Convert-AssemblyInteger $record[0]) -band 15
        if ($dungeon -eq 15) { $dungeon = 255 }
        $unsupported = @($record[2],$record[3] | Where-Object { $unsupportedHeaders.ContainsKey($_) } | ForEach-Object { $unsupportedHeaders[$_] })
        $support = if ($unsupported.Count -eq 0) { 'navigation' } else { $unsupported -join '; ' }
        $roomRows.Add((@($room.ToString('x2'), $season, $tileset.ToString('x2'), $layoutGroup, $record[2], $record[3], $record[4], $record[7].TrimStart('$'), $activeCollisions.ToString('x2'), $record[1].TrimStart('$'), $layoutId, $mapping, ($groupTilesets[$room] -shr 7),$group,$dungeon.ToString('x2'),"data/seasons/tilesetAssignments.s:group${group}Tilesets/tileset`$$($tileset.ToString('x2'))",$support) -join "`t"))
    }
}
}
# The mapping selector is retained separately from the room identity.
$roomRows[0] += "`tmapping`tpost-scroll-gfx`tgroup`tdungeon`tsource`tsupport"
Write-GeneratedTable((Join-Path $seasonDestination 'world/rooms.tsv'), $roomRows)
Copy-GeneratedFile 'rooms/seasons/roomPacks.bin' 'world/room_packs.bin' $seasonDestination
$packSeasons = @(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/seasons/roomPackSeasonTable.s') 'roomPackSeasonTable' '.db' | ForEach-Object { $_.Operands | ForEach-Object { Convert-AssemblyInteger $_ } })
Write-GeneratedBytes((Join-Path $seasonDestination 'world/pack_seasons.bin'), [byte[]]$packSeasons)
foreach ($header in @('GFXH_SCENE_OUTSIDE_ONOX_CASTLE', 'GFXH_SCENE_INSIDE_ONOX_CASTLE', 'GFXH_TEMPLEFALL_SCENE1',
        'GFXH_TEMPLEFALL_SCENE2', 'GFXH_TEMPLEFALL_SCENE3', 'GFXH_TEMPLEFALL_SCENE4', 'GFXH_TEMPLEFALL_SCENE5', 'GFXH_TEMPLEFALL_SCENE6', 'GFXH_TEMPLEFALL_SCENE7')) {
    Export-SeasonGfxHeader $header
}
Write-GeneratedTable((Join-Path $seasonDestination 'world/graphics.tsv'), $graphicsRows)
foreach ($palette in @('PALH_SEASONS_97', 'PALH_SEASONS_98', 'PALH_TILESET_ONOX_CASTLE_OUTSIDE_WINTER',
        'PALH_TILESET_SUBROSIA_TEMPLE_VARIANT_UNUSED', 'PALH_TILESET_OVERWORLD_SUMMER_A', 'PALH_TILESET_OVERWORLD_WINTER_A', 'PALH_TILESET_OVERWORLD_AUTUMN_A', 'PALH_SEASONS_99', 'PALH_SEASONS_9a')) {
    $headers = @('PALH_0f', $palette)
    if ($palette -in @('PALH_SEASONS_99', 'PALH_SEASONS_9a')) { $headers = @('PALH_0f', 'PALH_TILESET_ONOX_CASTLE_OUTSIDE_WINTER', $palette) }
    Export-SeasonPalette $headers "opening/$palette"
}
$castleLightning = [Collections.Generic.List[string]]::new()
$castleLightning.Add("# duration`tpalette`tsource")
foreach ($entry in @(Read-AssemblyDataDirectives (Join-Path $Disassembly 'object_code/seasons/interactions/88.s') 'seasonsTable_09_7f33' '.db')) {
    if ($entry.Operands[0] -eq '$ff') { break }
    if ($entry.Operands.Count -ne 2) { throw 'INTERAC_88: malformed castle lightning row.' }
    $castleLightning.Add("$(Convert-AssemblyInteger $entry.Operands[0])`t$(Convert-AssemblyInteger $entry.Operands[1])`tobject_code/seasons/interactions/88.s:seasonsTable_09_7f33")
}
Write-GeneratedTable((Join-Path $seasonDestination 'opening/castle_lightning.tsv'), $castleLightning)
foreach ($address in @(0x44e8, 0x49b0, 0x4a10, 0x49e0, 0x4a40)) {
    $colors = [byte[]]::new(72)
    for ($index = 0; $index -lt 24; $index++) {
        $offset = $seasonsSymbols['paletteDataStart'] + ($address - 0x4000) + $index * 2
        $value = [int]$seasonsRomBytes[$offset] -bor ([int]$seasonsRomBytes[$offset + 1] -shl 8)
        foreach ($component in 0..2) { $colors[$index * 3 + $component] = ($value -shr ($component * 5)) -band 31 }
    }
    Write-GeneratedBytes((Join-Path $seasonDestination ('opening/palette-' + $address.ToString('x4') + '.bin')), $colors)
}
foreach ($name in @('spr_link', 'gfx_hud', 'gfx_font', 'gfx_font_jp', 'gfx_font_tradeitems', 'gfx_font_heartpiece', 'spr_item_icons_1', 'spr_item_icons_2', 'spr_item_icons_3', 'spr_din_1', 'spr_din_2', 'spr_impa', 'spr_onox_1', 'spr_onox_2')) { Copy-SeasonGraphic $name }
Write-GeneratedBytes((Join-Path $seasonDestination 'opening/hud_palette.bin'), (Read-PaletteBytes 'paletteData4830' 4 $seasonPalettes))
Write-GeneratedBytes((Join-Path $seasonDestination 'metadata/commonBgPalette0.bin'), (Read-PaletteBytes 'paletteData4830' 4 $seasonPalettes))
Write-GeneratedBytes((Join-Path $seasonDestination 'inventory/palette_sprites.bin'), (Read-PaletteBytes 'standardSpritePaletteData' 24 $seasonPalettes))
foreach ($layout in @('normal', 'extra_hearts', 'biggoron_sword')) {
    foreach ($kind in @('map', 'flg')) { Copy-GeneratedFile "gfx_compressible/common/${kind}_hud_${layout}.bin" "hud/${kind}_hud_${layout}.bin" $seasonDestination }
}
Copy-GeneratedFile 'gfx_compressible/common/spr_biggoron_sword_icon.png' 'hud/spr_biggoron_sword_icon.png' $seasonDestination
Copy-SeasonGraphic 'spr_common_sprites'
$terrainPath = Join-Path $Disassembly 'data/terrainEffects.s'
function Read-SeasonTerrainOam([string]$label) {
    return ((Read-IntroOamParts $terrainPath $label).Split(';') | ForEach-Object {
        (($_.Split(',') | ForEach-Object { [Convert]::ToInt32($_, 16) }) -join ',')
    }) -join ';'
}
Write-GeneratedTable((Join-Path $seasonDestination 'effects/terrain_shadow.tsv'), @(
    "# sprite`ttile-base`tpalette`toam`tsource",
    "spr_common_sprites`t0`t0`t$(Read-SeasonTerrainOam 'shadowAnimation')`tdata/terrainEffects.s:shadowAnimation"))
$terrainRows = [Collections.Generic.List[string]]::new()
$terrainRows.Add("# kind`ttile`tframe`tduration`tsound`tsound-start`tsound-period`tsound-duration`tsprite`ttile-base`tpalette`toam`tsource")
foreach ($frame in 0..1) {
    $label = "greenGrassAnimationFrame$frame"
    $terrainRows.Add("grass`tf8,f9`t$frame`t0`t00`t0`t0`t0`tspr_common_sprites`t0`t0`t$(Read-SeasonTerrainOam $label)`tdata/terrainEffects.s:$label")
}
foreach ($frame in 0..3) {
    $label = "puddleAnimationFrame$frame"
    $terrainRows.Add("puddle`tfa,fb,fc`t$frame`t8`t87`t3`t18`t6`tspr_common_sprites`t0`t0`t$(Read-SeasonTerrainOam $label)`tdata/terrainEffects.s:$label")
}
Write-GeneratedTable((Join-Path $seasonDestination 'effects/link_terrain_effects.tsv'), $terrainRows)
$textboxPalettes = [Collections.Generic.List[byte]]::new()
foreach ($header in @('PALH_0e', 'PALH_0d', 'PALH_0e')) {
    $body = Read-SeasonHeader $seasonHeaders $header 'PaletteHeader'
    $match = [regex]::Match($body, 'm_PaletteHeaderBg\s+1,\s*1,\s*(?<label>\w+)')
    if (-not $match.Success) { throw "Seasons $header does not supply textbox BG palette $01." }
    $textboxPalettes.AddRange([byte[]](Read-PaletteBytes $match.Groups['label'].Value 4 $seasonPalettes))
}
Write-GeneratedBytes((Join-Path $seasonDestination 'metadata/textboxBgPalette1.bin'), $textboxPalettes.ToArray())

# Full text control streams are resolved before presentation, including aliases,
# calls, jumps and physical fallthrough to the next YAML record.
$yaml = Read-ImportText (Join-Path $Disassembly 'text/seasons/text.yaml')
$rawTexts = @{}; $textOrder = [Collections.Generic.List[int]]::new(); $textEntries = @{}; $fallthroughs = @{}
foreach ($entry in [regex]::Matches($yaml, '(?ms)^  - name:(?<names>[^\r\n]*\r?\n(?:    - TX_[0-9a-f]{4}\r?\n)*)(?<rest>.*?)(?=^  - name:|\z)')) {
    $names = @([regex]::Matches($entry.Groups['names'].Value, 'TX_([0-9a-f]{4})') | ForEach-Object { [Convert]::ToInt32($_.Groups[1].Value, 16) })
    $body = [regex]::Match($entry.Groups['rest'].Value, '(?ms)^    text: \|(?:\d+)?-\r?\n(?<body>(?:      [^\r\n]*(?:\r?\n|\z))+)')
    $raw = if ($body.Success) { (($body.Groups['body'].Value.TrimEnd("`r", "`n") -split '\r?\n') | ForEach-Object { $_.Substring(6) }) -join "`n" } else { '' }
    foreach ($id in $names) {
        $rawTexts[$id] = $raw
        $textEntries[$id] = $textOrder.Count
        $fallthroughs[$id] = $entry.Groups['rest'].Value -match '(?m)^    null_terminator: False\s*$'
    }
    if ($names.Count -gt 0) { $textOrder.Add($names[0]) }
}
function Resolve-SeasonText([int]$id, [Collections.Generic.HashSet[int]]$visiting) {
    if (-not $rawTexts.ContainsKey($id) -or -not $visiting.Add($id)) { throw "Seasons unresolved/recursive TX_$($id.ToString('x4'))." }
    $raw = $rawTexts[$id]
    $message = $raw
    $remaining = $message; $message = ''; $jumped = $false
    while ($true) {
        $match = [regex]::Match($remaining, '\\(?<kind>call|jump)\(TX_(?<target>[0-9a-f]{4})\)')
        if (-not $match.Success) { $message += $remaining; break }
        $message += $remaining.Substring(0, $match.Index) + (Resolve-SeasonText ([Convert]::ToInt32($match.Groups['target'].Value, 16)) $visiting)
        if ($match.Groups['kind'].Value -eq 'jump') { $jumped = $true; break }
        $remaining = $remaining.Substring($match.Index + $match.Length)
    }
    if (-not $jumped -and $fallthroughs[$id]) {
        $next = $textEntries[$id] + 1
        if ($next -ge $textOrder.Count) { throw "Seasons TX_$($id.ToString('x4')) falls outside the text table." }
        $message += Resolve-SeasonText $textOrder[$next] $visiting
    }
    [void]$visiting.Remove($id)
    return Normalize-DialogueText $message -preservePosition $true
}
$texts = @{}; $textRows = [Collections.Generic.List[string]]::new(); $textRows.Add("# id`tmessage-base64`tsource")
foreach ($id in @((0x0c00..0x0c18) + (0x1e00..0x1e06) + (0x2500..0x2505))) {
    if (-not $rawTexts.ContainsKey($id)) { continue }
    $texts[$id] = Resolve-SeasonText $id ([Collections.Generic.HashSet[int]]::new())
    $textRows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($texts[$id])))`ttext/seasons/text.yaml:TX_$($id.ToString('x4'))")
}
Write-GeneratedTable((Join-Path $seasonDestination 'opening/text.tsv'), $textRows)

$seasonTreasureTexts = @{}
$seasonFluteTexts = @{}
foreach ($id in @(0x510c,0x510f)) {
    $seasonFluteTexts[$id] = Resolve-SeasonText $id ([Collections.Generic.HashSet[int]]::new())
}
foreach ($id in @($rawTexts.Keys | Where-Object { $_ -lt 0x100 })) {
    $seasonTreasureTexts[$id] = Resolve-SeasonText $id ([Collections.Generic.HashSet[int]]::new())
}

$seasonTileInteractionTexts = @{}
foreach ($id in @($rawTexts.Keys | Where-Object { ($_ -ge 0x2e00 -and $_ -lt 0x2f00) -or ($_ -ge 0x5100 -and $_ -le 0x510a) -or $_ -in @(0x510d, 0x510e, 0x0901) })) {
    $seasonTileInteractionTexts[$id] = Resolve-SeasonText $id ([Collections.Generic.HashSet[int]]::new())
}

$seasonInventoryTexts = @{}
foreach ($id in @($rawTexts.Keys | Where-Object { ($_ -ge 0x0900 -and $_ -lt 0x0a00) -or ($_ -ge 0x3040 -and $_ -lt 0x30c0) })) {
    $seasonInventoryTexts[$id] = Resolve-SeasonText $id ([Collections.Generic.HashSet[int]]::new())
}
$seasonMapTexts = @{}
foreach ($id in @($rawTexts.Keys | Where-Object { ($_ -ge 0x0200 -and $_ -lt 0x0400) -or ($_ -ge 0x1700 -and $_ -lt 0x1800) })) {
    $seasonMapTexts[$id] = Resolve-SeasonText $id ([Collections.Generic.HashSet[int]]::new())
}

# The driver code is shared; its game-specific song pointers/data are not.
Write-GeneratedBytes((Join-Path $seasonDestination 'audio/sound_data.bin'), [byte[]]$seasonsRomBytes[0xe4000..0xfbfff])
foreach ($name in @('waveforms', 'noise_frequencies', 'frequencies', 'envelope_delays')) {
    Write-GeneratedBytes((Join-Path $seasonDestination "audio/$name.bin"), [IO.File]::ReadAllBytes((Join-Path $destination "audio/$name.bin")))
}
$roomMusic = [byte[]]::new(1536)
foreach ($group in 0..5) {
    $sourceGroup = if ($group -in @(2, 3)) { 1 } else { $group }
    $bytes = [IO.File]::ReadAllBytes((Join-Path $Disassembly "audio/seasons/group${sourceGroup}IDs.bin"))
    if ($bytes.Length -ne 256) { throw "Seasons music group ${group}: expected 256 room assignments." }
    [Array]::Copy($bytes, 0, $roomMusic, $group * 256, 256)
}
Write-GeneratedBytes((Join-Path $seasonDestination 'audio/room_music.bin'), $roomMusic)

# Actors retain the original graphics, complete chained uploads, animation
# parameters, loop anchors and contiguous OAM pointer domains.
$presentation = Read-InteractionPresentation 'seasons'
$interactionPath = Join-Path $Disassembly 'data/seasons/interactionData.s'
$gfxPath = Join-Path $Disassembly 'data/seasons/objectGfxHeaders.s'
$gfxNodes = @(Read-AssemblyMacroInvocations $gfxPath '' 'm_ObjectGfxHeader')
$actorRows = [Collections.Generic.List[string]]::new()
$actorRows.Add("# id`tsubid`tsprite`ttile-base`tpalette`tdefault-animation`tappend-sprites`tsource-offset`tsource`tsource-inverted")
$animationRows = [Collections.Generic.List[string]]::new()
$animationRows.Add("# id`tindex`tframes`tsource")
foreach ($id in @(0x4e, 0x4f, 0x9d, 0x9f, 0xa5, 0x77, 0x86, 0x88, 0x05, 0x08, 0x84)) {
    $hex = $id.ToString('x2')
    $entry = @(Read-AssemblyMacroInvocations $interactionPath '' 'm_InteractionData' | Where-Object Comment -eq "`$$hex")
    if ($entry.Count -ne 1) { throw "Seasons interaction `$$hex has no unique graphics record." }
    $records = @(if ($entry[0].Operands[0].StartsWith('$')) { $entry } else {
        @(Read-AssemblyMacroInvocations $interactionPath $entry[0].Operands[0] 'm_InteractionSubidData')
    })
    for ($subid = 0; $subid -lt $records.Count; $subid++) {
        $fields = $records[$subid].Operands
        $gfx = Convert-AssemblyInteger $fields[0]
        $tileBase = (Convert-AssemblyInteger $fields[1]) -band 0x7f
        $flags = Convert-AssemblyInteger $fields[2]
        $sprite = if ($gfx -ne 0) { $gfxNodes[$gfx].Operands[0] }
            elseif (($flags -band 0x80) -ne 0) { 'spr_common_sprites' }
            elseif ($id -eq 0x77) { 'spr_templefall' }
            else { throw "Seasons interaction `$$hex has unbound fixed OBJ graphics." }
        Copy-SeasonGraphic $sprite
        $append = [Collections.Generic.List[string]]::new()
        # $4e:$06 selects Din's extra graphics through $cc1d/$cc1e.
        if ($sprite -eq 'spr_din_1') { Copy-SeasonGraphic 'spr_din_2'; $append.Add('spr_din_2') }
        if ($id -eq 0x4f -and $subid -eq 0) { Copy-SeasonGraphic 'spr_din_crystal'; $append.Add('spr_din_crystal') }
        $sourceOffset = if ($gfxNodes[$gfx].Operands.Count -ge 3) { $gfxNodes[$gfx].Operands[2].TrimStart('$') } else { '0000' }
        $appendSources = @($append | ForEach-Object { "${_}:$([int](Get-ImportSpriteSourceGrayscaleInverted $_))" }) -join ','
        $actorRows.Add("$hex`t$($subid.ToString('x2'))`t$sprite`t$tileBase`t$(($flags -shr 4) -band 7)`t$($flags -band 15)`t$appendSources`t$sourceOffset`tdata/seasons/interactionData.s:`$$hex`t$([int](Get-ImportSpriteSourceGrayscaleInverted $sprite))")
    }
    $table = $presentation.Tables["interaction${hex}Animations"]
    if ($null -eq $table) { throw "Seasons interaction `$$hex has no animation table." }
    $animationCount = if ($id -eq 0x9f) { 1 } else { $table.Count }
    for ($index = 0; $index -lt $animationCount; $index++) {
        $animation = Resolve-InteractionPresentation $presentation $id $index
        if (-not $animation -or $animation -match '@(?:\||$)') { throw "Seasons interaction `$$hex animation `$$($index.ToString('x2')) has missing OAM." }
        $animationRows.Add("$hex`t$($index.ToString('x2'))`t$animation`tdata/seasons/interactionAnimations.s:$($table[$index])")
    }
}
$partAnimationPath = Join-Path $Disassembly 'data/seasons/partAnimations.s'
$partOamPath = Join-Path $Disassembly 'data/seasons/partOamData.s'
$partLabel = @(Read-AssemblyDataDirectives $partAnimationPath 'part27Animations' '.dw')[0].Operands[0]
$partDefinitions = Read-AssemblyAnimationDefinitions $partAnimationPath 'partAnimation[0-9a-f]+(?:Loop)?'
$lightningFrames = $partDefinitions[$partLabel].Frames
$partPointers = @(Read-AssemblyDataDirectives $partAnimationPath 'part27OamDataPointers' '.dw' | ForEach-Object { $_.Operands[0] })
$frames = [Collections.Generic.List[string]]::new()
foreach ($frame in $lightningFrames) {
    $oam = Read-IntroOamParts $partOamPath $partPointers[$frame.PointerOffset / 2]
    $decimalOam = (@($oam.Split(';') | ForEach-Object { (@($_.Split(',') | ForEach-Object { [Convert]::ToInt32($_, 16) }) -join ',') }) -join ';')
    $frames.Add((Format-NpcAnimationFrame $frame $decimalOam))
}
# PART $27 has its own dispatch bank; this visual is not an INTERAC $27 record.
Copy-SeasonGraphic 'spr_projectiles_2'
$actorRows.Add("27`t00`tspr_projectiles_2`t14`t4`t0`t`t0000`tdata/seasons/partData.s:PART_LIGHTNING`t$([int](Get-ImportSpriteSourceGrayscaleInverted 'spr_projectiles_2'))")
$animationRows.Add("27`t00`t$(Join-NpcAnimationFrames $frames $partDefinitions[$partLabel].LoopStart)`tdata/seasons/partAnimations.s:$partLabel")
$nativeLightning = @($lightningFrames | ForEach-Object { "$($_.Duration.ToString('x2')):$($_.Parameter.ToString('x2'))" }) -join ','
Write-GeneratedTable((Join-Path $seasonDestination 'effects/lightning_native.tsv'), @('# z-offsets`tframes`tshake`tdebris-offsets', "c0,d0,e0,f0,00`t$nativeLightning`t06`t02:06,00:fb,ff:07,fd:fc,00:05"))
Write-GeneratedTable((Join-Path $seasonDestination 'opening/actors.tsv'), $actorRows)
Write-GeneratedTable((Join-Path $seasonDestination 'opening/animations.tsv'), $animationRows)

# Preserve source object-list order, including the scenery/helper slot.
$placements = [Collections.Generic.List[string]]::new()
$placements.Add("# index`tid`tsubid`ty`tx`tsource")
$nodes = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly 'objects/seasons/extraData3.s') 'objectData7e4e' 'obj_Interaction')
if ($nodes.Count -ne 7) { throw 'objects/seasons/extraData3.s:objectData7e4e: expected seven troupe slots.' }
for ($index = 0; $index -lt $nodes.Count; $index++) {
    $placements.Add("$index`t$(($nodes[$index].Operands -replace '^\$', '') -join "`t")`tobjects/seasons/extraData3.s:objectData7e4e")
}
Write-GeneratedTable((Join-Path $seasonDestination 'opening/troupe.tsv'), $placements)

# roomGfxChanges.s writes vertical tile pairs, then an ordered collision list.
Copy-SeasonGraphic 'spr_caravan'
$scenery = [Collections.Generic.List[string]]::new()
$scenery.Add("# room`tkind`tposition`ttile`tattribute`tsource")
foreach ($spec in @(@(0x98, 'dinsTroupeVramAndCollisions'), @(0x97, 'roomTileChangesAfterLoad0e@vramTilesAndCollisions'))) {
    $cursor = $seasonsSymbols[$spec[1]]
    if ($null -eq $cursor) { throw "roomGfxChanges.s: missing $($spec[1])." }
    while ($seasonsRomBytes[$cursor] -ne 0) {
        $position = [int]$seasonsRomBytes[$cursor] -bor ([int]$seasonsRomBytes[$cursor + 1] -shl 8)
        $cursor += 2
        $position -= $seasonsSymbols['w3VramTiles']
        if ($position -lt 0 -or $position -ge 512) { throw "roomGfxChanges.s: invalid troupe VRAM offset $position." }
        while ($seasonsRomBytes[$cursor] -ne 0) {
            $tile = $seasonsRomBytes[$cursor]; $attribute = $seasonsRomBytes[$cursor + 1]; $cursor += 2
            $scenery.Add("$($spec[0].ToString('x2'))`tmapping`t$($position.ToString('x3'))`t$($tile.ToString('x2'))`t$($attribute.ToString('x2'))`tcode/seasons/roomGfxChanges.s:$($spec[1])")
            $position++
        }
        $cursor++
    }
    $cursor++
    while ($seasonsRomBytes[$cursor] -ne 0) {
        $position = [int]$seasonsRomBytes[$cursor++]; $collision = if (($position -band 0x80) -ne 0) { '05' } else { '0f' }
        $scenery.Add("$($spec[0].ToString('x2'))`tcollision`t$(($position -band 0x7f).ToString('x3'))`t$collision`t00`tcode/seasons/roomGfxChanges.s:$($spec[1])")
    }
}
Write-GeneratedTable((Join-Path $seasonDestination 'world/scenery.tsv'), $scenery)

# Native Link and the pregame orb use the same typed sprite-frame presentation
# as Ages, resolved from Seasons' own pointer/animation/OAM tables.
$linkPath = Join-Path $Disassembly 'data/seasons/specialObjectAnimationData.s'
$linkOamPath = Join-Path $Disassembly 'data/seasons/specialObjectOamData.s'
$linkGfx = @(Read-AssemblyMacroInvocations $linkPath 'specialObject08GfxPointers' 'm_SpecialObjectGfxPointer')
$linkOam = @(Read-AssemblyDataDirectives $linkPath 'specialObject09OamDataPointers' '.dw' | ForEach-Object { $_.Operands[0] })
$linkPointers = @(Read-AssemblyDataDirectives $linkPath 'specialObject08AnimationDataPointers' '.dw' | ForEach-Object { $_.Operands[0] })
$linkDefinitions = Read-AssemblyAnimationDefinitions $linkPath 'animation(?:Data|Loop)[A-Za-z0-9_]+' $false
$linkRows = [Collections.Generic.List[string]]::new(); $linkRows.Add("# kind`tindex`tduration`tsource-offset`tbase-palette`toam-parts")
$linkLoopRows = [Collections.Generic.List[string]]::new(); $linkLoopRows.Add("# animation`tloop-start`tparameters`tsource")
foreach ($animation in 0..($linkPointers.Count - 1)) {
    $definition = $linkDefinitions[$linkPointers[$animation]]
    if ($null -eq $definition) { throw "Seasons Link animation $animation has no resolved stream." }
    foreach ($direction in 0..3) {
    $vram = [int[]]::new(256); for ($tile = 0; $tile -lt 256; $tile++) { $vram[$tile] = -1 }
    $index = 0
    foreach ($frame in $definition.Frames) {
        $pointer = $frame.PointerOffset + $(if ($frame.PointerOffset -ge 0x40) { $direction } else { 0 })
        $gfx = $linkGfx[$pointer]
        $sourceTile = (Convert-AssemblyInteger $gfx.Operands[2]) / 16
        $count = Convert-AssemblyInteger $gfx.Operands[3]
        foreach ($tile in 0..($count - 1)) { $vram[$tile] = $sourceTile + $tile }
        $parts = Read-IntroOamParts $linkOamPath $linkOam[(Convert-AssemblyInteger $gfx.Operands[0])]
        $parts = (@($parts.Split(';') | ForEach-Object {
            $fields = $_.Split(','); $tile = [Convert]::ToInt32($fields[2], 16) -band 0xfe
            if ($vram[$tile] -lt 0 -or $vram[$tile + 1] -ne $vram[$tile] + 1) { throw "Seasons Link $animation frame $index references an unresolved partial tile upload." }
            "$($fields[0]),$($fields[1]),$($vram[$tile].ToString('x2')),$($fields[3])"
        }) -join ';')
        $kind = 'link-native-' + $animation.ToString('x2') + '-' + $direction
        $linkRows.Add("$kind`t$index`t$($frame.Duration)`t0000`t0`t$parts")
        if ($direction -eq 0 -and $animation -in @(8, 5)) {
            $kind = if ($animation -eq 8) { 'link-spin' } else { 'link-vanish' }
            $linkRows.Add("$kind`t$index`t$($frame.Duration)`t0000`t0`t$parts")
        }
        $index++
    }
    }
    $parameters = ($definition.Frames | ForEach-Object { $_.Parameter.ToString('x2') }) -join ','
    $linkLoopRows.Add("$($animation.ToString('x2'))`t$($definition.LoopStart)`t$parameters`tdata/seasons/specialObjectAnimationData.s:$($linkPointers[$animation])")
}
$sparkle = @(Read-AssemblyMacroInvocations $interactionPath 'interaction84SubidData' 'm_InteractionSubidData')
$sparkleOam = @(Read-AssemblyDataDirectives (Join-Path $Disassembly 'data/seasons/interactionAnimations.s') 'interaction84OamDataPointers' '.dw' | ForEach-Object { $_.Operands[0] })
foreach ($subid in @(9, 6)) {
    $fields = $sparkle[$subid].Operands
    $flags = Convert-AssemblyInteger $fields[2]
    $baseTile = (Convert-AssemblyInteger $fields[1]) -band 0x7f
    $gfx = $gfxNodes[(Convert-AssemblyInteger $fields[0])]
    $offset = (Convert-AssemblyInteger $gfx.Operands[2]) + $baseTile * 16
    $label = $presentation.Tables['interaction84Animations'][$flags -band 15]
    $index = 0
    foreach ($frame in $presentation.Definitions[$label].Frames) {
        $parts = Read-IntroOamParts (Join-Path $Disassembly 'data/seasons/interactionOamData.s') $sparkleOam[$frame.PointerOffset / 2]
        $kind = if ($subid -eq 9) { 'orb-descend' } else { 'orb-vanish' }
        $linkRows.Add("$kind`t$index`t$($frame.Duration)`t$($offset.ToString('x4'))`t$(($flags -shr 4) -band 7)`t$parts")
        $index++
    }
}
Write-GeneratedTable((Join-Path $seasonDestination 'cutscenes/new_game_intro_sprites.tsv'), @($linkRows[0]) + @($linkRows | Select-Object -Skip 1 | Sort-Object { $parts = $_.Split("`t"); $parts[0] + ([int]$parts[1]).ToString('d4') }))
Write-GeneratedTable((Join-Path $seasonDestination 'opening/link_loops.tsv'), $linkLoopRows)
$spin = $linkDefinitions[$linkPointers[8]].Frames
$vanish = $linkDefinitions[$linkPointers[5]].Frames
$linkNativeSource = Read-ImportText (Join-Path $Disassembly 'object_code/seasons/specialObjects/linkInCutscene.s')
$initialization = [regex]::Match($linkNativeSource, '(?ms)^linkCutsceneA:.*?ld \(hl\),\$(?<lo>[0-9a-f]{2})\s+inc hl\s+ld \(hl\),\$(?<hi>[0-9a-f]{2}).*?ld l,SpecialObject.yh\s+ld \(hl\),\$(?<y>[0-9a-f]{2}).*?ld l,SpecialObject.xh\s+ld \(hl\),\$(?<x>[0-9a-f]{2})')
if (-not $initialization.Success) { throw 'Seasons linkCutsceneA: unresolved initial counters/position.' }
$introHeader = @(Read-ImportLines (Join-Path $destination 'cutscenes/new_game_intro.tsv'))[0]
$values = @(
    (([Convert]::ToInt32($initialization.Groups['hi'].Value, 16) -shl 8) -bor [Convert]::ToInt32($initialization.Groups['lo'].Value, 16)),
    60, 60, 128, [Convert]::ToInt32($initialization.Groups['x'].Value, 16), [Convert]::ToInt32($initialization.Groups['y'].Value, 16),
    '2a', '21', 2, 0x0c16, 4, (($spin | ForEach-Object { $_.PointerOffset.ToString('x2') }) -join ','),
    (($vanish | ForEach-Object { $_.Duration.ToString('x2') }) -join ','), (($vanish | ForEach-Object { $_.PointerOffset.ToString('x2') }) -join ','),
    '02,03,04,03,02,00,ff,00', 'ff,ff,ff,00,01,01,01,00', [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($texts[0x0c16])))
Write-GeneratedTable((Join-Path $seasonDestination 'cutscenes/new_game_intro.tsv'), @($introHeader, ($values -join "`t")))

# Select the Seasons branch for common WRAM and file-management declarations.
$seasonHost = Start-AssemblySourceHost 'ROM_SEASONS;REGION_US;SEASONS_ENGINE;BUILD_VANILLA;ENABLE_US_BUGFIXES'
try {
    function Read-SeasonNodes([string]$path, [string]$command = 'NODES', [string]$label = '', [string]$name = '') {
        $fullPath = Join-Path $Disassembly $path
        $json = Invoke-AssemblySourceHost $seasonHost $command "$fullPath`0$label`0$name"
        return @(($json | ConvertFrom-Json) | Where-Object IsActive)
    }
    $registers = @(Read-SeasonNodes 'code/bank0.s' 'DATA_DIRECTIVES' 'gfxRegisterStates' '.db' | ForEach-Object { $_.Operands | ForEach-Object { (Convert-AssemblyInteger $_).ToString('x2') } })
    if ($registers.Count -ne 23 * 12) { throw 'Seasons gfxRegisterStates: expected $17 pairs of six hardware register bytes.' }
    $registerRows = [Collections.Generic.List[string]]::new(); $registerRows.Add("# index`tregisters`tsource")
    foreach ($index in 0..22) { $registerRows.Add("$($index.ToString('x2'))`t$($registers[($index * 12)..($index * 12 + 11)] -join ',')`tcode/bank0.s:gfxRegisterStates") }
    Write-GeneratedTable((Join-Path $seasonDestination 'opening/gfx_registers.tsv'), $registerRows)
    $seasonWram = $seasonsSymbols
    $initial = [Collections.Generic.List[string]]::new()
    $respawnStruct = [regex]::Match((Read-ImportText (Join-Path $Disassembly 'include/structs.s')), '(?ms)^\.STRUCT DeathRespawnStruct\s*(?<body>.*?)^\.ENDST')
    $respawnMembers = @([regex]::Matches($respawnStruct.Groups['body'].Value, '(?m)^\s*(\w+)\s+db\b') | ForEach-Object { $_.Groups[1].Value })
    $initial.Add("# variant`tindex`taddress`tvalue`tsource")
    foreach ($variant in @('common', 'standardGame')) {
        $label = if ($variant -eq 'common') { 'initialFileVariables' } else { 'initialFileVariables_standardGame' }
        $nodes = @(Read-SeasonNodes 'code/fileManagement.s' 'DATA_DIRECTIVES' $label '.db')
        if ($variant -eq 'standardGame') { $nodes += @(Read-SeasonNodes 'code/fileManagement.s' 'DATA_DIRECTIVES' 'initialFileVariables_heroGame' '.db') }
        $index = 0
        foreach ($node in $nodes) {
            if ($node.Operands.Count -eq 1 -and $node.Operands[0] -eq '$00') { continue }
            $name = $node.Operands[0].TrimStart('<')
            $offset = 0
            if ($name -match '^wDeathRespawnBuffer\.(?<member>\w+)$') {
                $offset = [Array]::IndexOf($respawnMembers, $Matches['member'])
                if ($offset -lt 0) { throw "Unknown Seasons respawn member $name." }
                $name = 'wDeathRespawnBuffer'
            }
            if ($name -match '^(?<base>\w+)\+(?<offset>\d+)$') { $name = $Matches['base']; $offset = [int]$Matches['offset'] }
            $key = $name
            if (-not $seasonWram.ContainsKey($key)) { throw "Seasons fileManagement.s:$label references unresolved WRAM $key." }
            $address = $seasonWram[$key] + $offset
            $value = switch ($node.Operands[1]) {
                '1<<TREASURE_PUNCH' { 4 }
                'SPECIALOBJECT_RICKY' { 0x0b }
                default { Convert-AssemblyInteger $_ }
            }
            $initial.Add("$variant`t$index`t$($address.ToString('x4'))`t$(([int]$value).ToString('x2'))`tcode/fileManagement.s:$label")
            $index++
        }
        if ($index -ne $(if ($variant -eq 'common') { 13 } else { 5 })) { throw "Seasons $label has an unexpected write count $index." }
    }
    Write-GeneratedTable((Join-Path $seasonDestination 'metadata/new_file_initialization.tsv'), $initial)
    [void](Invoke-AssemblySourceHost $seasonHost 'ASSERT' '')
}
finally {
    [void](Invoke-AssemblySourceHost $seasonHost 'QUIT' '')
    $seasonHost.WaitForExit()
    $seasonHost.Dispose()
}

# Normalize the opening's real interaction scripts into the existing runner.
$symbols = @{}
foreach ($source in @('constants/common/objectSpeeds.s', 'constants/common/music.s', 'constants/common/globalFlags.s')) {
    foreach ($match in [regex]::Matches((Read-ImportText (Join-Path $Disassembly $source)), '(?m)^\s*(?<name>\w+)\s+(?:db|dsb\s+\d+)\s*;\s*(?:\$|0x)(?<value>[0-9a-f]+)')) {
        $symbols[$match.Groups['name'].Value] = [Convert]::ToInt32($match.Groups['value'].Value, 16)
    }
    foreach ($constant in Read-AssemblyConstants (Join-Path $Disassembly $source)) {
        if ($constant.OperandText -match '^\$[0-9a-f]+$') { $symbols[$constant.Name] = Convert-AssemblyInteger $constant.OperandText }
    }
}
$symbols['ANGLE_UP'] = 0; $symbols['ANGLE_RIGHT'] = 8; $symbols['ANGLE_DOWN'] = 16; $symbols['ANGLE_LEFT'] = 24
$symbols['$cfd0'] = 'cfd0'; $symbols['$cfd1'] = 'cfd1'; $symbols['$cfd3'] = 'cfd3'
$symbols['$d00f'] = 'LinkZ'; $symbols['wIsLinkedGame'] = 'IsLinkedGame'
$delayLengths = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'code/scripting.s') '@delayLengths')
if ($delayLengths.Count -ne 13) { throw 'scripting.s:scriptCmd_delay: expected thirteen native delay lengths.' }
$supported = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @('initcollisions','asm15','checkabutton','showloadedtext','ormemory','scriptjump','setdisabledobjectsto11','cplinkx',
        'setanimationfromobjectbyte','enableallobjects','setcollisionradii','makeabuttonsensitive','setanimation','orroomflag',
        'scriptend','checkmemoryeq','delay','setspeed','movedown','writeobjectbyte','callscript','setcounter1','setglobalflag',
        'setzspeed','playsound','wait','jumpifobjectbyteeq','retscript','setcoords','setmusic','setangle','applyspeed','showtext',
        'writememory','disableinput','enableinput','jumpifroomflagset','setangleandanimation','jumpifmemoryeq','jumpifglobalflagset','rungenericnpc')) { [void]$supported.Add($opcode) }
$programs = @(
    @('scripts.s','troupeScript1',0x4e), @('scripts.s','troupeScript2',0x4e), @('scripts.s','troupeScript3',0x4e),
    @('scripts.s','troupeScript4',0x4e), @('scripts.s','troupeScript_Impa',0x4e), @('scripts.s','troupeScript_Din',0x4e),
    @('scripts.s','troupeScript_startDanceScene',0x4e), @('scripts2.s','dinScript_discoverLinkCollapsed_body',0xa5),
    @('scripts2.s','tornadoScript_startDestruction',0x4e), @('scripts2.s','tornadoScript_endDestruction',0x4e),
    @('scripts.s','dinImprisonedScript_setDinCoords',0x4f), @('scripts2.s','dinImprisoned_OnoxExplainsMotive',0x4f),
    @('scripts.s','dinImprisonedScript_OnoxSendsTempleDown',0x4f), @('scripts.s','impaScript_afterOnoxTakesDin',0x9d))
$commandRows = [Collections.Generic.List[string]]::new()
$commandRows.Add("# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
foreach ($spec in $programs) {
    $path = Join-Path $Disassembly "scripts/seasons/$($spec[0])"
    $program = $spec[1]; $id = [int]$spec[2]
    $commands = @(Read-AssemblyCutsceneCommands $path $program $supported)
    if ($program -eq 'dinScript_discoverLinkCollapsed_body') {
        $commands += @(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/seasons/scripts.s') 'piratianScript_jump' $supported)
    }
    $expanded = [Collections.Generic.List[object]]::new()
    foreach ($command in $commands) {
        $entries = ,@($command.Opcode, $command.Operands, $command.Label)
        if ($command.Opcode -eq 'setangleandanimation') {
            $angle = if ($symbols.ContainsKey($command.Operands)) { $symbols[$command.Operands] } else { Convert-AssemblyInteger $command.Operands }
            $entries = @(@('setangle', $command.Operands, $command.Label), @('setanimation', ('$' + ([int]($angle / 8)).ToString('x2')), $command.Label))
        }
        elseif ($command.Opcode -eq 'rungenericnpc') {
            $loop = '__generic_' + $expanded.Count
            $entries = @(@('initcollisions','',$command.Label), @('checkabutton','',$loop), @('showtext',$command.Operands,$loop), @('scriptjump',$loop,$loop))
        }
        foreach ($entry in $entries) {
            $expanded.Add([pscustomobject]@{ Path=$command.Path; Script=$program; Label=$entry[2]; Line=$command.Line;
                Index=$expanded.Count; Opcode=$entry[0]; Operands=$entry[1] })
        }
    }
    $targets = @{}; foreach ($command in $expanded) { if (-not $targets.ContainsKey($command.Label)) { $targets[$command.Label] = $command.Index } }
    $bindings = @{}
    foreach ($command in $expanded) {
        $parts = @($command.Operands -split ',\s*')
        $binding = $null
        switch ($command.Opcode) {
            'delay' { $binding = @{ Opcode='wait'; Arg0=[string]$delayLengths[(Convert-AssemblyInteger $parts[0])] } }
            'setcounter1' { $binding = @{ Opcode='wait'; Arg0=[string](Convert-AssemblyInteger $parts[0]) } }
            'setdisabledobjectsto11' { $binding = @{ Opcode='setdisabledobjects'; Arg0='11' } }
            'enableallobjects' { $binding = @{ Opcode='setdisabledobjects'; Arg0='00' } }
            { $_ -in @('asm15','ormemory','cplinkx','setanimationfromobjectbyte','setzspeed') } { $binding = @{ Opcode='native'; Payload=($command.Opcode + ' ' + $command.Operands) } }
            'callscript' { $command.Operands = $command.Operands.Replace('mainScripts.','') }
            { $_ -in @('playsound','setmusic','setglobalflag','orroomflag') } {
                $value = if ($symbols.ContainsKey($parts[0])) { [int]$symbols[$parts[0]] } else { Convert-AssemblyInteger $parts[0] }
                $binding = @{ Opcode=$command.Opcode; Arg0=$value.ToString('x2') }
            }
            'jumpifmemoryeq' {
                $binding = @{ Opcode='jumpifmemoryeq'; Arg0=(Convert-AssemblyInteger $parts[1]).ToString('x2'); Arg1=[string]$targets[$parts[2]]; Payload=[string]$symbols[$parts[0]] }
            }
            'jumpifglobalflagset' {
                $binding = @{ Opcode='jumpifmemoryeq'; Arg0='01'; Arg1=[string]$targets[$parts[1]]; Payload=('flag:' + ([int]$symbols[$parts[0]]).ToString('x2')) }
            }
            'jumpifobjectbyteeq' {
                $binding = @{ Opcode='jumpifmemoryeq'; Arg0=(Convert-AssemblyInteger $parts[1]).ToString('x2'); Arg1=[string]$targets[$parts[2]]; Payload=('object:' + $parts[0].TrimStart('$')) }
            }
            'writeobjectbyte' {
                $address = switch ($parts[0]) { 'Interaction.var38' { 0x78 }; default { Convert-AssemblyInteger $_ } }
                $binding = @{ Opcode='writeobjectbyte'; Actor='Self'; Arg0=$address.ToString('x2'); Arg1=(Convert-AssemblyInteger $parts[1]).ToString('x2') }
            }
        }
        if ($null -ne $binding) { $bindings[$command.Index.ToString() + '|' + $command.Opcode + '|' + $command.Operands] = $binding }
    }
    $animations = @{}
    for ($index = 0; $index -lt $presentation.Tables[('interaction' + $id.ToString('x2') + 'Animations')].Count; $index++) {
        $animations[$index] = Resolve-InteractionPresentation $presentation $id $index
    }
    $rows = @(ConvertTo-CutsceneCommandRows @($expanded) 'Self' -symbols $symbols -texts $texts -animations $animations -bindings $bindings -yieldOnJump $true)
    foreach ($row in $rows | Select-Object -Skip 1) { $commandRows.Add($row) }
}
Write-GeneratedTable((Join-Path $seasonDestination 'opening/commands.tsv'), $commandRows)
