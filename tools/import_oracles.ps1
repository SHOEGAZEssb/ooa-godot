param(
    [string]$Disassembly = "C:\msys64\home\timst\oracles-disasm",
    [string]$Rom = (Join-Path $PSScriptRoot "..\Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc")
)

$ErrorActionPreference = "Stop"
$project = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $project "assets\oracle"

# Remove the eight flat files produced by the original four-room prototype.
# All generated data now lives in purpose-specific subdirectories.
foreach ($legacyName in @(
    'gfx_tileset08.png', 'spr_link.png',
    'tilesetMappings06.bin', 'tilesetCollisions06.bin',
    'room0000.bin', 'room0001.bin', 'room0010.bin', 'room0011.bin'
)) {
    $legacyPath = Join-Path $destination $legacyName
    if (Test-Path -LiteralPath $legacyPath) {
        Remove-Item -LiteralPath $legacyPath -Force
    }
    if (Test-Path -LiteralPath "${legacyPath}.import") {
        Remove-Item -LiteralPath "${legacyPath}.import" -Force
    }
}

if (-not (Test-Path -LiteralPath $Rom)) {
    throw "ROM not found: $Rom"
}

$romBytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Rom))
if ($romBytes.Length -ne 1048576) {
    throw "Expected the 1 MiB US Oracle of Ages ROM, got $($romBytes.Length) bytes."
}

$hash = (Get-FileHash -LiteralPath $Rom -Algorithm MD5).Hash
$cleanUsHash = "C4639CC61C049E5A085526BB6CAC03BB"
if ($hash -ne $cleanUsHash) {
    throw "ROM hash $hash is not the supported clean US Oracle of Ages hash $cleanUsHash."
}

function Copy-GeneratedFile([string]$relativeSource, [string]$relativeDestination) {
    $source = Join-Path $Disassembly $relativeSource
    $target = Join-Path $destination $relativeDestination
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Disassembly asset not found: $source"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
}

# Preserve the complete Ages branch of constants/common/globalFlags.s for the
# editable debug flag menu. Within WLA-DX enums, "db" advances the value while
# ".db" is used here for range aliases, so only the former emits a flag row.
$globalFlagRows = [Collections.Generic.List[string]]::new()
$globalFlagValue = 0
$inGlobalFlagEnum = $false
$includeGlobalFlagBranch = $true
$globalFlagBranch = ''
foreach ($line in Get-Content (Join-Path $Disassembly 'constants\common\globalFlags.s')) {
    if ($line -match '^\s*\.ENUM\s+\$0') {
        $inGlobalFlagEnum = $true
        continue
    }
    if (-not $inGlobalFlagEnum) { continue }
    if ($line -match '^\s*\.ENDE') { break }
    if ($line -match '^\s*\.ifdef\s+ROM_(AGES|SEASONS)') {
        $globalFlagBranch = $Matches[1]
        $includeGlobalFlagBranch = $globalFlagBranch -eq 'AGES'
        continue
    }
    if ($line -match '^\s*\.else') {
        $includeGlobalFlagBranch = $globalFlagBranch -eq 'SEASONS'
        continue
    }
    if ($line -match '^\s*\.endif') {
        $globalFlagBranch = ''
        $includeGlobalFlagBranch = $true
        continue
    }
    if ($includeGlobalFlagBranch -and
        $line -match '^\s*(GLOBALFLAG_[A-Za-z0-9_]+)\s+db(?:\s|;)') {
        $globalFlagRows.Add("$($globalFlagValue.ToString('x2'))`t$($Matches[1])")
        $globalFlagValue++
    }
}
if ($globalFlagRows.Count -ne 0x80) {
    throw "Expected 128 Ages global flags, parsed $($globalFlagRows.Count)."
}
$globalFlagPath = Join-Path $destination 'metadata\global_flags.tsv'
New-Item -ItemType Directory -Force -Path (Split-Path $globalFlagPath -Parent) | Out-Null
[IO.File]::WriteAllLines($globalFlagPath, $globalFlagRows, [Text.UTF8Encoding]::new($false))

# Parse the 103 non-stub tileset records. The runtime needs the room-layout
# group and resolved six-palette block; the original shared mapping index is
# retained in metadata for provenance, but expanded mappings use tileset IDs.
$tilesetSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\tilesets.s")
$tilesetPattern = '(?ms);\s*0x(?<id>[0-9a-f]{2})\s*\r?\n' +
    '\s*\.db\s+\$(?<properties>[0-9a-f]{2}),\s*\$(?<flags>[0-9a-f]{2})[^\r\n]*\r?\n' +
    '\s*\.db[^\r\n]+\r?\n' +
    '\s*\.db[^\r\n]+\r?\n' +
    '\s*\.db\s+(?<palette>[A-Za-z0-9_]+)[^\r\n]*\r?\n' +
    '\s*\.db\s+\$(?<layout>[0-9a-f]{2}),\s*\$(?<group>[0-9a-f]{2}),\s*\$(?<animation>[0-9a-f]{2})' 
$tilesets = [regex]::Matches($tilesetSource, $tilesetPattern)
if ($tilesets.Count -ne 103) {
    throw "Expected 103 concrete tileset records, parsed $($tilesets.Count)."
}

$paletteHeaderSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\paletteHeaders.s")
$paletteBlocks = [regex]::Matches(
    $paletteHeaderSource,
    '(?ms)^m_PaletteHeaderStart\s+\$(?<id>[0-9a-f]{2}),[ \t]*(?<symbol>[A-Za-z0-9_]+)(?<body>.*?)(?=^m_PaletteHeaderStart|\z)'
)
$paletteHeaders = @{}
foreach ($block in $paletteBlocks) {
    $background = [regex]::Match(
        $block.Groups['body'].Value,
        'm_PaletteHeaderBg\s+2,\s*6,\s*(?<label>paletteData[0-9a-f]+)'
    )
    if ($background.Success) {
        $paletteHeaders[$block.Groups['symbol'].Value] = @{
            Id = [Convert]::ToByte($block.Groups['id'].Value, 16)
            Label = $background.Groups['label'].Value
        }
    }
}

$paletteDataSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\paletteData.s")

# initializeGame loads PALH_0f before every gameplay room. Besides the standard
# sprite palettes, that header installs paletteData48e0 as background palette
# 0. Special metatiles such as the closed/open chest ($f1/$f0) select that
# palette directly instead of one of the tileset-specific palettes 2-7.
$commonBgPaletteLabel = 'paletteData48e0'
$commonBgPaletteIndex = $paletteDataSource.IndexOf(
    "${commonBgPaletteLabel}:", [StringComparison]::Ordinal)
if ($commonBgPaletteIndex -lt 0) {
    throw "Common gameplay background palette not found: $commonBgPaletteLabel"
}
$commonBgPaletteEnd = $paletteDataSource.IndexOf(
    'paletteData', $commonBgPaletteIndex + $commonBgPaletteLabel.Length,
    [StringComparison]::Ordinal)
if ($commonBgPaletteEnd -lt 0) { $commonBgPaletteEnd = $paletteDataSource.Length }
$commonBgPaletteBlock = $paletteDataSource.Substring(
    $commonBgPaletteIndex, $commonBgPaletteEnd - $commonBgPaletteIndex)
$commonBgColors = [regex]::Matches(
    $commonBgPaletteBlock,
    'm_RGB16\s+\$(?<r>[0-9a-f]{2})\s+\$(?<g>[0-9a-f]{2})\s+\$(?<b>[0-9a-f]{2})')
if ($commonBgColors.Count -lt 4) {
    throw "$commonBgPaletteLabel contains $($commonBgColors.Count) colors; expected at least 4."
}
$commonBgPaletteBytes = [byte[]]::new(4 * 3)
for ($color = 0; $color -lt 4; $color++) {
    $commonBgPaletteBytes[$color * 3] = [Convert]::ToByte(
        $commonBgColors[$color].Groups['r'].Value, 16)
    $commonBgPaletteBytes[$color * 3 + 1] = [Convert]::ToByte(
        $commonBgColors[$color].Groups['g'].Value, 16)
    $commonBgPaletteBytes[$color * 3 + 2] = [Convert]::ToByte(
        $commonBgColors[$color].Groups['b'].Value, 16)
}
$commonBgPalettePath = Join-Path $destination 'metadata\commonBgPalette0.bin'
New-Item -ItemType Directory -Force -Path (Split-Path $commonBgPalettePath -Parent) | Out-Null
[IO.File]::WriteAllBytes($commonBgPalettePath, $commonBgPaletteBytes)

$tilesetRecordSize = 8
$metadata = [byte[]]::new(128 * $tilesetRecordSize)
$usedTilesets = [Collections.Generic.HashSet[int]]::new()

foreach ($tileset in $tilesets) {
    $id = [Convert]::ToInt32($tileset.Groups['id'].Value, 16)
    $layout = [Convert]::ToInt32($tileset.Groups['layout'].Value, 16)
    $layoutGroup = [Convert]::ToInt32($tileset.Groups['group'].Value, 16)
    $animation = [Convert]::ToInt32($tileset.Groups['animation'].Value, 16)
    $properties = [Convert]::ToInt32($tileset.Groups['properties'].Value, 16)
    $flags = [Convert]::ToInt32($tileset.Groups['flags'].Value, 16)
    $paletteSymbol = $tileset.Groups['palette'].Value
    if (-not $paletteHeaders.ContainsKey($paletteSymbol)) {
        throw "Could not resolve background palette for tileset $($id.ToString('x2')) ($paletteSymbol)."
    }

    $paletteHeader = $paletteHeaders[$paletteSymbol]
    $metadata[$id * $tilesetRecordSize] = [byte]$layout
    $metadata[$id * $tilesetRecordSize + 1] = [byte]$layoutGroup
    $metadata[$id * $tilesetRecordSize + 2] = [byte]$paletteHeader.Id
    $metadata[$id * $tilesetRecordSize + 3] = 1
    $metadata[$id * $tilesetRecordSize + 4] = [byte]$animation
    $metadata[$id * $tilesetRecordSize + 5] = if (($flags -band 0x08) -ne 0) {
        [byte]($properties -band 0x0f)
    } else {
        [byte]0xff
    }
    $metadata[$id * $tilesetRecordSize + 6] = [byte](($properties -shr 4) -band 0x07)
    $metadata[$id * $tilesetRecordSize + 7] = [byte]$flags
    [void]$usedTilesets.Add($id)

    $label = $paletteHeader.Label
    $labelIndex = $paletteDataSource.IndexOf("${label}:", [StringComparison]::Ordinal)
    if ($labelIndex -lt 0) {
        throw "Palette data label not found: $label"
    }
    $nextLabel = $paletteDataSource.IndexOf("paletteData", $labelIndex + $label.Length, [StringComparison]::Ordinal)
    if ($nextLabel -lt 0) { $nextLabel = $paletteDataSource.Length }
    $paletteBlock = $paletteDataSource.Substring($labelIndex, $nextLabel - $labelIndex)
    $colors = [regex]::Matches($paletteBlock, 'm_RGB16\s+\$(?<r>[0-9a-f]{2})\s+\$(?<g>[0-9a-f]{2})\s+\$(?<b>[0-9a-f]{2})')
    if ($colors.Count -lt 24) {
        throw "Palette $label contains $($colors.Count) colors; expected at least 24."
    }
    $paletteBytes = [byte[]]::new(24 * 3)
    for ($color = 0; $color -lt 24; $color++) {
        $paletteBytes[$color * 3] = [Convert]::ToByte($colors[$color].Groups['r'].Value, 16)
        $paletteBytes[$color * 3 + 1] = [Convert]::ToByte($colors[$color].Groups['g'].Value, 16)
        $paletteBytes[$color * 3 + 2] = [Convert]::ToByte($colors[$color].Groups['b'].Value, 16)
    }
    $palettePath = Join-Path $destination "metadata\palette$($id.ToString('x2')).bin"
    New-Item -ItemType Directory -Force -Path (Split-Path $palettePath -Parent) | Out-Null
    [IO.File]::WriteAllBytes($palettePath, $paletteBytes)

    Copy-GeneratedFile "gfx\ages\gfx_tileset$($id.ToString('x2')).png" "gfx\gfx_tileset$($id.ToString('x2')).png"
}

$metadataPath = Join-Path $destination "metadata\tilesets.bin"
[IO.File]::WriteAllBytes($metadataPath, $metadata)

# Join the two original collision-mode-indexed tables used by
# interactWithTileBeforeLink and INTERAC_PUSHBLOCK. Each generated record is
# four bytes: interactable parameter, source replacement tile, destination
# tile, and property flags. $ff in the first byte means that the metatile does
# not use the ordinary pushblock interaction in that collision mode.
function Read-LocalHexByteTable([string]$path, [string]$tableLabel, [string]$pointerDirective) {
    $bytes = [Collections.Generic.List[byte]]::new()
    $labels = @{}
    $pointers = [Collections.Generic.List[string]]::new()
    $reading = $false
    foreach ($line in Get-Content $path) {
        if (-not $reading) {
            if ($line -match "^$([regex]::Escape($tableLabel))\s*:") { $reading = $true }
            continue
        }
        if ($line -match '^\s*(?<label>@[A-Za-z0-9_]+):') {
            $labels[$Matches['label']] = $bytes.Count
        }
        if ($line -match "^\s*$pointerDirective\s+(?<label>@[A-Za-z0-9_]+)") {
            $pointers.Add($Matches['label'])
            continue
        }
        if ($line -match '^\s*\.db\s+(?<values>[^;]+)') {
            foreach ($value in [regex]::Matches($Matches['values'], '\$(?<value>[0-9a-fA-F]{2})')) {
                $bytes.Add([Convert]::ToByte($value.Groups['value'].Value, 16))
            }
        }
    }
    if ($pointers.Count -ne 6) {
        throw "$tableLabel should have 6 collision-mode pointers, got $($pointers.Count)."
    }
    return @{ Bytes = $bytes; Labels = $labels; Pointers = $pointers }
}

$interactableTable = Read-LocalHexByteTable `
    (Join-Path $Disassembly 'data\ages\tile_properties\interactableTiles.s') `
    'interactableTilesTable' '\.dw'
$pushableTable = Read-LocalHexByteTable `
    (Join-Path $Disassembly 'data\ages\tile_properties\pushableTiles.s') `
    'pushableTilePropertiesTable' 'dbrel'
$pushableBytes = [byte[]]::new(6 * 256 * 4)
for ($i = 0; $i -lt $pushableBytes.Length; $i++) { $pushableBytes[$i] = 0xff }
$joinedPushableRecords = 0
for ($mode = 0; $mode -lt 6; $mode++) {
    $interactable = @{}
    $offset = $interactableTable.Labels[$interactableTable.Pointers[$mode]]
    while ($interactableTable.Bytes[$offset] -ne 0) {
        $tile = $interactableTable.Bytes[$offset]
        $parameter = $interactableTable.Bytes[$offset + 1]
        if (($parameter -band 0x0f) -eq 0) { $interactable[$tile] = $parameter }
        $offset += 2
    }

    $properties = @{}
    $offset = $pushableTable.Labels[$pushableTable.Pointers[$mode]]
    while ($pushableTable.Bytes[$offset] -ne 0) {
        $tile = $pushableTable.Bytes[$offset]
        $properties[$tile] = @(
            $pushableTable.Bytes[$offset + 1],
            $pushableTable.Bytes[$offset + 2],
            $pushableTable.Bytes[$offset + 3])
        $offset += 4
    }

    foreach ($tile in $interactable.Keys) {
        # The Somaria block ($da) uses its dedicated item object and therefore
        # intentionally has no INTERAC_PUSHBLOCK property record.
        if (-not $properties.ContainsKey($tile)) { continue }
        $recordOffset = ($mode * 256 + $tile) * 4
        $pushableBytes[$recordOffset] = $interactable[$tile]
        $pushableBytes[$recordOffset + 1] = $properties[$tile][0]
        $pushableBytes[$recordOffset + 2] = $properties[$tile][1]
        $pushableBytes[$recordOffset + 3] = $properties[$tile][2]
        $joinedPushableRecords++
    }
}
if ($joinedPushableRecords -ne 33) {
    throw "Expected 33 collision-mode pushblock records, joined $joinedPushableRecords."
}
$pushablePath = Join-Path $destination 'metadata\pushableTiles.bin'
[IO.File]::WriteAllBytes($pushablePath, $pushableBytes)

# The expanded tileset table is indexed by tileset ID, even though byte 5 in
# tilesets.s still records the original/shared mapping index. Copy the expanded
# mapping and collision pair for every concrete tileset.
foreach ($tilesetId in $usedTilesets) {
    $hex = $tilesetId.ToString('x2')
    Copy-GeneratedFile "tileset_layouts_expanded\ages\tilesetMappings${hex}.bin" "layouts\tilesetMappings${hex}.bin"
    Copy-GeneratedFile "tileset_layouts_expanded\ages\tilesetCollisions${hex}.bin" "layouts\tilesetCollisions${hex}.bin"
}

foreach ($group in 0..5) {
    Copy-GeneratedFile "rooms\ages\group${group}Tilesets.bin" "groups\group${group}Tilesets.bin"
}
Copy-GeneratedFile "rooms\ages\roomPacksPresent.bin" "groups\roomPacksPresent.bin"
Copy-GeneratedFile "rooms\ages\roomPacksPast.bin" "groups\roomPacksPast.bin"
Copy-GeneratedFile "gfx\common\spr_link.png" "gfx\spr_link.png"
Copy-GeneratedFile "gfx\common\spr_swords.png" "gfx\spr_swords.png"
Copy-GeneratedFile "gfx_compressible\common\spr_common_sprites.png" "gfx\spr_common_sprites.png"
Copy-GeneratedFile "gfx_compressible\ages\spr_syrup_teenager.png" "gfx\spr_syrup_teenager.png"
Copy-GeneratedFile "gfx_compressible\common\gfx_hud.png" "gfx\gfx_hud.png"
Copy-GeneratedFile "gfx\common\spr_item_icons_1.png" "gfx\spr_item_icons_1.png"
Copy-GeneratedFile "gfx\common\spr_item_icons_2.png" "gfx\spr_item_icons_2.png"
Copy-GeneratedFile "gfx\common\spr_item_icons_3.png" "gfx\spr_item_icons_3.png"
Copy-GeneratedFile "gfx_compressible\ages\spr_quest_items_4.png" "gfx\spr_quest_items_4.png"
Copy-GeneratedFile "gfx_compressible\ages\spr_common_items.png" "gfx\spr_common_items.png"
Copy-GeneratedFile "gfx\common\gfx_partial_hearts.png" "gfx\gfx_partial_hearts.png"
Copy-GeneratedFile "gfx\common\gfx_font.png" "gfx\gfx_font.png"
Copy-GeneratedFile "gfx_compressible\ages\gfx_inventory_hud_1.png" "inventory\gfx_inventory_hud_1.png"
Copy-GeneratedFile "gfx_compressible\ages\spr_present_past_symbols.png" "inventory\spr_present_past_symbols.png"
Copy-GeneratedFile "gfx_compressible\ages\gfx_inventory_hud_2.png" "inventory\gfx_inventory_hud_2.png"
Copy-GeneratedFile "gfx_compressible\common\map_inventory_screen_1.bin" "inventory\map_inventory_screen_1.bin"
Copy-GeneratedFile "gfx_compressible\common\flg_inventory_screen_1.bin" "inventory\flg_inventory_screen_1.bin"
Copy-GeneratedFile "gfx_compressible\common\map_inventory_textbar.bin" "inventory\map_inventory_textbar.bin"
Copy-GeneratedFile "gfx_compressible\common\flg_inventory_textbar.bin" "inventory\flg_inventory_textbar.bin"
foreach ($animationSheet in 1..3) {
    Copy-GeneratedFile "gfx\ages\gfx_animations_${animationSheet}.png" "gfx\gfx_animations_${animationSheet}.png"
}
Copy-GeneratedFile "gfx_compressible\common\map_hud_normal.bin" "hud\map_hud_normal.bin"
Copy-GeneratedFile "gfx_compressible\common\flg_hud_normal.bin" "hud\flg_hud_normal.bin"

# The map menu swaps in a complete 20x18 background tilemap. Preserve the
# original VRAM-source pieces separately: the runtime composes them at their
# GFXH_OVERWORLD_MAP / GFXH_PAST_MAP / GFXH_DUNGEON_MAP destinations, then
# applies the tile attributes and map-menu palettes.
foreach ($mapAsset in @(
    @{ Source = 'gfx_compressible\ages\map_present_minimap.bin'; Destination = 'map\map_present.bin' },
    @{ Source = 'gfx_compressible\ages\flg_present_minimap.bin'; Destination = 'map\flags_present.bin' },
    @{ Source = 'gfx_compressible\ages\map_past_minimap.bin'; Destination = 'map\map_past.bin' },
    @{ Source = 'gfx_compressible\ages\flg_past_minimap.bin'; Destination = 'map\flags_past.bin' },
    @{ Source = 'gfx_compressible\common\map_dungeon_minimap.bin'; Destination = 'map\map_dungeon.bin' },
    @{ Source = 'gfx_compressible\common\flg_dungeon_minimap.bin'; Destination = 'map\flags_dungeon.bin' },
    @{ Source = 'gfx_compressible\ages\gfx_minimap_tiles_common.png'; Destination = 'map\tiles_common.png' },
    @{ Source = 'gfx_compressible\ages\gfx_minimap_tiles_present_1.png'; Destination = 'map\tiles_present_1.png' },
    @{ Source = 'gfx_compressible\ages\gfx_minimap_tiles_present_2.png'; Destination = 'map\tiles_present_2.png' },
    @{ Source = 'gfx_compressible\ages\gfx_minimap_tiles_past_1.png'; Destination = 'map\tiles_past_1.png' },
    @{ Source = 'gfx_compressible\ages\gfx_minimap_tiles_past_2.png'; Destination = 'map\tiles_past_2.png' },
    @{ Source = 'gfx_compressible\common\gfx_minimap_tiles_dungeon.png'; Destination = 'map\tiles_dungeon.png' },
    @{ Source = 'gfx_compressible\ages\spr_minimap_icons.png'; Destination = 'map\sprites.png' })) {
    Copy-GeneratedFile $mapAsset.Source $mapAsset.Destination
}
foreach ($blurb in @(
    'makupath', 'd1', 'd2', 'd3', 'd4', 'd5', 'd6', 'd7', 'd8',
    'blacktowerturret', 'roomofrites', 'heroscave')) {
    $scope = if ($blurb -eq 'roomofrites') { 'common' } else { 'ages' }
    Copy-GeneratedFile "gfx_compressible\$scope\gfx_blurb_$blurb.png" "map\blurb_$blurb.png"
}

function Export-PaletteBlock(
    [string]$label,
    [int]$colorCount,
    [string]$relativeDestination
) {
    $bytes = Read-PaletteBytes $label $colorCount
    $target = Join-Path $destination $relativeDestination
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    [IO.File]::WriteAllBytes($target, $bytes)
}

function Read-PaletteBytes(
    [string]$label,
    [int]$colorCount
) {
    $labelIndex = $paletteDataSource.IndexOf("${label}:", [StringComparison]::Ordinal)
    if ($labelIndex -lt 0) { throw "Map palette data label not found: $label" }
    $nextLabel = $paletteDataSource.IndexOf(
        'paletteData', $labelIndex + $label.Length, [StringComparison]::Ordinal)
    if ($nextLabel -lt 0) { $nextLabel = $paletteDataSource.Length }
    $block = $paletteDataSource.Substring($labelIndex, $nextLabel - $labelIndex)
    $colors = [regex]::Matches(
        $block,
        'm_RGB16\s+\$(?<r>[0-9a-f]{2})\s+\$(?<g>[0-9a-f]{2})\s+\$(?<b>[0-9a-f]{2})')
    if ($colors.Count -lt $colorCount) {
        throw "$label contains $($colors.Count) colors; expected $colorCount."
    }
    $bytes = [byte[]]::new($colorCount * 3)
    for ($color = 0; $color -lt $colorCount; $color++) {
        $bytes[$color * 3] = [Convert]::ToByte($colors[$color].Groups['r'].Value, 16)
        $bytes[$color * 3 + 1] = [Convert]::ToByte($colors[$color].Groups['g'].Value, 16)
        $bytes[$color * 3 + 2] = [Convert]::ToByte($colors[$color].Groups['b'].Value, 16)
    }
    return $bytes
}

# PALH_07, PALH_08, PALH_09, plus the common sprite palettes used by
# spr_minimap_icons. PALH_09 installs its four palettes into BG slots 2-5.
Export-PaletteBlock 'paletteData4098' 32 'map\palette_present.bin'
Export-PaletteBlock 'paletteData40d8' 32 'map\palette_past.bin'
Export-PaletteBlock 'paletteData4118' 16 'map\palette_dungeon.bin'
Export-PaletteBlock 'paletteData4138' 32 'map\palette_sprites.bin'

# PALH_0a installs paletteData48e0 into BG palettes 0-1 and the first six
# standard sprite palettes into BG palettes 2-7. The cursor and equipped item
# OAM use the same standard sprite palette block in sprite palette slots 0-5.
$inventoryBgPalette = [byte[]]::new(8 * 4 * 3)
$inventoryBg01 = Read-PaletteBytes 'paletteData48e0' 8
$inventoryBg27 = Read-PaletteBytes 'standardSpritePaletteData' 24
[Array]::Copy($inventoryBg01, 0, $inventoryBgPalette, 0, $inventoryBg01.Length)
[Array]::Copy($inventoryBg27, 0, $inventoryBgPalette, $inventoryBg01.Length, $inventoryBg27.Length)
$inventoryPalettePath = Join-Path $destination 'inventory\palette_bg.bin'
New-Item -ItemType Directory -Force -Path (Split-Path $inventoryPalettePath -Parent) | Out-Null
[IO.File]::WriteAllBytes($inventoryPalettePath, $inventoryBgPalette)
Export-PaletteBlock 'standardSpritePaletteData' 24 'inventory\palette_sprites.bin'

# Signs are map metatile $f2 rather than ordinary room objects. Preserve the
# original group/room/position lookup table and resolve its TX_2eXX strings to
# UTF-8 here, while the human-readable disassembly sources are available.
$textYaml = Get-Content -Raw (Join-Path $Disassembly "text\ages\text.yaml")
$signTexts = @{}
$textMatches = [regex]::Matches(
    $textYaml,
    '(?ms)^  - name: TX_2e(?<id>[0-9a-f]{2})\r?\n    index: 0x[0-9a-f]{2}\r?\n    text: \|-\r?\n(?<body>(?:      [^\r\n]*(?:\r?\n|\z))+)'
)
foreach ($match in $textMatches) {
    $lines = $match.Groups['body'].Value -split '\r?\n' | ForEach-Object {
        if ($_.Length -ge 6) { $_.Substring(6) } else { '' }
    }
    while ($lines.Count -gt 0 -and $lines[-1] -eq '') {
        $lines = $lines[0..($lines.Count - 2)]
    }
    $text = $lines -join "`n"
    $text = $text.Replace('\left', [string][char]0x2190)
    $text = $text.Replace('\right', [string][char]0x2192)
    $text = $text.Replace('\up', [string][char]0x2191)
    $text = $text.Replace('\down', [string][char]0x2193)
    $text = [regex]::Replace($text, '\\(?:stop|pos\([^)]*\)|col\([^)]*\))', '')
    $signTexts[[Convert]::ToInt32($match.Groups['id'].Value, 16)] = $text
}

$signSource = Get-Content (Join-Path $Disassembly "data\ages\signText.s")
$signRows = [Collections.Generic.List[string]]::new()
$signRows.Add("# group`troom`tposition`ttext-id`tutf8-base64")
$currentSignGroup = -1
foreach ($line in $signSource) {
    if ($line -match '^signTextGroup(?<group>[0-7])Data:') {
        $currentSignGroup = [int]$Matches['group']
        continue
    }
    if ($currentSignGroup -lt 0 -or $line -notmatch '\.db\s+\$(?<position>[0-9a-f]{2}),\s*\$(?<room>[0-9a-f]{2}),\s*<TX_2e(?<text>[0-9a-f]{2})') {
        continue
    }
    $textId = [Convert]::ToInt32($Matches['text'], 16)
    if (-not $signTexts.ContainsKey($textId)) {
        throw "Could not resolve sign text TX_2e$($textId.ToString('x2'))."
    }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($signTexts[$textId]))
    $signRows.Add("$currentSignGroup`t$($Matches['room'])`t$($Matches['position'])`t$($Matches['text'])`t$encoded")
}
if ($signRows.Count -ne 43) {
    throw "Expected 42 sign records, parsed $($signRows.Count - 1)."
}
$signPath = Join-Path $destination "objects\signs.tsv"
New-Item -ItemType Directory -Force -Path (Split-Path $signPath -Parent) | Out-Null
[IO.File]::WriteAllLines($signPath, $signRows, [Text.UTF8Encoding]::new($false))

# NPC extraction. Interaction objects are split between the room object table,
# interactionData.s (graphics), and the script/text tables. Keep the list of
# character interaction codes here: other interaction codes are scenery,
# triggers, enemies, or cutscene-only helpers even when they have text.
$npcInteractionIds = [Collections.Generic.HashSet[int]]::new()
foreach ($id in @(
    0x10, 0x28, 0x29, 0x2a, 0x2e, 0x30, 0x31, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x3b, 0x3c, 0x3d,
    0x3f, 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x48, 0x49,
    0x4b, 0x4c, 0x4d, 0x4e, 0x4f, 0x50, 0x51, 0x52, 0x53, 0x54,
    0x55, 0x57, 0x58, 0x59, 0x5a, 0x5b, 0x5c, 0x5d, 0x5f, 0x65, 0x66, 0x68,
    0x69, 0x6a, 0x6d, 0x72, 0x83, 0x87, 0x88, 0x89, 0x8b, 0x94, 0x9a,
    0x9c, 0x9d, 0xab, 0xad, 0xba, 0xbf, 0xc3, 0xc4, 0xc8, 0xca,
    0xcb, 0xcc, 0xcd, 0xce, 0xd5, 0xd6, 0xe3
)) { [void]$npcInteractionIds.Add($id) }

function Normalize-NpcText([string]$text) {
    $text = $text.Replace('\left', [string][char]0x2190)
    $text = $text.Replace('\right', [string][char]0x2192)
    $text = $text.Replace('\up', [string][char]0x2191)
    $text = $text.Replace('\down', [string][char]0x2193)
    $text = $text.Replace('\Link', 'Link')
    return [regex]::Replace($text, '\\(?:stop|pos\([^)]*\)|col\([^)]*\))', '')
}

# Resolve all text blocks once. This also handles the low-index generic-NPC
# commands, whose source still spells the complete TX_XXXX symbol.
$allTexts = @{}
$allTextPositions = @{}
$allTextMatches = [regex]::Matches(
    $textYaml,
    '(?ms)^  - name: TX_(?<id>[0-9a-f]{4})\r?\n    index: 0x[0-9a-f]{2}\r?\n    text: \|-\r?\n(?<body>(?:      [^\r\n]*(?:\r?\n|\z))+)'
)
foreach ($match in $allTextMatches) {
    $lines = $match.Groups['body'].Value -split '\r?\n' | ForEach-Object {
        if ($_.Length -ge 6) { $_.Substring(6) } else { '' }
    }
    while ($lines.Count -gt 0 -and $lines[-1] -eq '') {
        $lines = $lines[0..($lines.Count - 2)]
    }
    $textId = [Convert]::ToInt32($match.Groups['id'].Value, 16)
    $rawText = $lines -join "`n"
    $allTexts[$textId] = Normalize-NpcText $rawText
    $positionMatch = [regex]::Match($rawText, '\\pos\((?<position>\d+)\)')
    if ($positionMatch.Success) { $allTextPositions[$textId] = [int]$positionMatch.Groups['position'].Value }
}
# Shared text bodies use a YAML name/index list. Resolve every alias as well;
# cutscene scripts refer to the individual TX_* names even when several IDs
# intentionally share one body.
foreach ($match in [regex]::Matches(
    $textYaml,
    '(?ms)^  - name:\r?\n(?<names>(?:    - TX_[0-9a-f]{4}\r?\n)+)    index:\r?\n(?:    - 0x[0-9a-f]{2}\r?\n)+    text: \|-\r?\n(?<body>(?:      [^\r\n]*(?:\r?\n|\z))+)'
)) {
    $lines = $match.Groups['body'].Value -split '\r?\n' | ForEach-Object {
        if ($_.Length -ge 6) { $_.Substring(6) } else { '' }
    }
    while ($lines.Count -gt 0 -and $lines[-1] -eq '') {
        $lines = $lines[0..($lines.Count - 2)]
    }
    $rawText = $lines -join "`n"
    $message = Normalize-NpcText $rawText
    $positionMatch = [regex]::Match($rawText, '\\pos\((?<position>\d+)\)')
    foreach ($name in [regex]::Matches($match.Groups['names'].Value, 'TX_(?<id>[0-9a-f]{4})')) {
        $textId = [Convert]::ToInt32($name.Groups['id'].Value, 16)
        $allTexts[$textId] = $message
        if ($positionMatch.Success) { $allTextPositions[$textId] = [int]$positionMatch.Groups['position'].Value }
    }
}

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

# Resolve the first dialogue command reached by a script label, following the
# common scriptjump indirection used by generic NPCs.
$scriptSources = @(
    (Join-Path $Disassembly "scripts\ages\scripts.s"),
    (Join-Path $Disassembly "scripts\ages\scriptHelper.s")
)
$scriptBodies = @{}
foreach ($scriptSourcePath in $scriptSources) {
    $scriptSource = Get-Content -Raw $scriptSourcePath
    foreach ($labelMatch in [regex]::Matches($scriptSource, '(?ms)^(?<label>[A-Za-z0-9_@]+):\r?\n(?<body>.*?)(?=^[A-Za-z0-9_@]+:|\z)')) {
        $scriptBodies[$labelMatch.Groups['label'].Value] = $labelMatch.Groups['body'].Value
    }
}
$scriptTextCache = @{}
function Resolve-ScriptTextId([string]$label, [Collections.Generic.HashSet[string]]$visited) {
    if ($scriptTextCache.ContainsKey($label)) { return $scriptTextCache[$label] }
    if ($visited.Contains($label) -or -not $scriptBodies.ContainsKey($label)) { return -1 }
    [void]$visited.Add($label)
    $body = $scriptBodies[$label]
    $textMatch = [regex]::Match($body, '(?:rungenericnpc|rungenericnpclowindex|showtext|showtextlowindex|settextid)\s+(?:<)?TX_(?<id>[0-9a-f]{4})')
    if ($textMatch.Success) {
        $value = [Convert]::ToInt32($textMatch.Groups['id'].Value, 16)
        $scriptTextCache[$label] = $value
        return $value
    }
    $jumpMatch = [regex]::Match($body, 'scriptjump\s+(?:mainScripts\.)?(?<label>[A-Za-z0-9_@]+)')
    if ($jumpMatch.Success) {
        $value = Resolve-ScriptTextId $jumpMatch.Groups['label'].Value $visited
        $scriptTextCache[$label] = $value
        return $value
    }
    $scriptTextCache[$label] = -1
    return -1
}

# Map interaction subids to the first script entry in each original script
# table. This preserves the important subid-specific dialogue without trying
# to evaluate story-state branches during import.
$npcTextBySubid = @{}
$npcFacingIds = [Collections.Generic.HashSet[int]]::new()
$npcInteractionSourcePaths = @()
$npcInteractionSourcePaths += Get-ChildItem (Join-Path $Disassembly "object_code\ages\interactions") -File -Filter '*.s'
$npcInteractionSourcePaths += Get-ChildItem (Join-Path $Disassembly "object_code\common\interactions") -File -Filter '*.s'
foreach ($interactionSourcePath in $npcInteractionSourcePaths) {
    $interactionSource = Get-Content -Raw $interactionSourcePath.FullName
    $codeMatch = [regex]::Match($interactionSource, '(?m)^interactionCode(?<id>[0-9a-f]{2}):')
    if (-not $codeMatch.Success) { continue }
    $interactionId = [Convert]::ToInt32($codeMatch.Groups['id'].Value, 16)
    if (-not $npcInteractionIds.Contains($interactionId)) { continue }
    if ($interactionSource -match 'npcFaceLinkAndAnimate') { [void]$npcFacingIds.Add($interactionId) }
    $tableName = ''
    $tableIndex = 0
    foreach ($line in ($interactionSource -split '\r?\n')) {
        if ($line -match '^@(?<table>[A-Za-z0-9_]+ScriptTable):') {
            $tableName = $Matches['table']
            $tableIndex = 0
            continue
        }
        if (-not $tableName) { continue }
        if ($line -match '^[^\t ;@].*:') { $tableName = ''; continue }
        if ($line -notmatch '^\s*\.dw\s+mainScripts\.(?<label>[A-Za-z0-9_@]+)') { continue }
        $textId = Resolve-ScriptTextId $Matches['label'] ([Collections.Generic.HashSet[string]]::new())
        if ($textId -ge 0) {
            $subids = @()
            if ($tableName -match '^subid(?<a>[0-9a-f])And(?<b>[0-9a-f])') {
                $subids = @([Convert]::ToInt32($Matches['a'], 16), [Convert]::ToInt32($Matches['b'], 16))
            } elseif ($tableName -match '^subid(?<a>[0-9a-f])(?<b>[0-9a-f])') {
                $subids = @([Convert]::ToInt32("$($Matches['a'])$($Matches['b'])", 16))
            } elseif ($tableName -eq 'scriptTable') {
                $subids = @($tableIndex)
            }
            foreach ($subid in $subids) {
                $key = "$interactionId`:$subid"
                if (-not $npcTextBySubid.ContainsKey($key)) { $npcTextBySubid[$key] = $textId }
            }
        }
        $tableIndex++
    }
    # Some interactions select scripts in assembly rather than through a .dw
    # table. Only accept references whose labels identify their subid; never
    # assign an unrelated "first text" to every instance of the interaction.
    foreach ($scriptReference in [regex]::Matches($interactionSource, 'mainScripts\.(?<label>[A-Za-z0-9_@]+)')) {
        $label = $scriptReference.Groups['label'].Value
        $textId = Resolve-ScriptTextId $label ([Collections.Generic.HashSet[string]]::new())
        if ($textId -lt 0) { continue }
        $subids = @()
        if ($label -match '(?i)Subid(?<a>[0-9a-f])And(?<b>[0-9a-f])') {
            $subids = @([Convert]::ToInt32($Matches['a'], 16), [Convert]::ToInt32($Matches['b'], 16))
        } elseif ($label -match '(?i)Subid(?<subid>[0-9a-f]{2})') {
            $subids = @([Convert]::ToInt32($Matches['subid'], 16))
        } elseif ($label -match '(?i)Script(?<subid>[0-9a-f]{2})(?:_|$)') {
            $subids = @([Convert]::ToInt32($Matches['subid'], 16))
        }
        foreach ($subid in $subids) {
            $key = "$interactionId`:$subid"
            if (-not $npcTextBySubid.ContainsKey($key)) { $npcTextBySubid[$key] = $textId }
        }
    }
}

# Parse the interaction graphics table, including pointer-backed subid data.
$interactionDataSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\interactionData.s")
$interactionGraphics = @{}
$interactionPointers = @{}
foreach ($match in [regex]::Matches($interactionDataSource, '(?m)^\s*/\* \$(?<id>[0-9a-f]{2}) \*/ m_InteractionData\s+(?<gfx>\$[0-9a-f]{2}|[A-Za-z0-9_]+)(?:\s+(?<base>\$[0-9a-f]{2})\s+(?<flags>\$[0-9a-f]{2}))?')) {
    $id = [Convert]::ToInt32($match.Groups['id'].Value, 16)
    if ($match.Groups['gfx'].Value.StartsWith('$')) {
        $interactionGraphics["$id`:0"] = @{
            Gfx = [Convert]::ToInt32($match.Groups['gfx'].Value.Substring(1), 16)
            TileBase = [Convert]::ToInt32($match.Groups['base'].Value.Substring(1), 16)
            Palette = ([Convert]::ToInt32($match.Groups['flags'].Value.Substring(1), 16) -shr 4) -band 7
            DefaultAnimation = [Convert]::ToInt32($match.Groups['flags'].Value.Substring(1), 16) -band 15
        }
    } else { $interactionPointers[$id] = $match.Groups['gfx'].Value }
}
foreach ($block in [regex]::Matches($interactionDataSource, '(?ms)^interaction(?<id>[0-9a-f]{2})SubidData:\r?\n(?<body>.*?)(?=^interaction[0-9a-f]{2}SubidData:|\z)')) {
    $id = [Convert]::ToInt32($block.Groups['id'].Value, 16)
    $subid = 0
    foreach ($entry in [regex]::Matches($block.Groups['body'].Value, 'm_InteractionSubidData\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<base>[0-9a-f]{2})\s+\$(?<flags>[0-9a-f]{2})')) {
        $flags = [Convert]::ToInt32($entry.Groups['flags'].Value, 16)
        $interactionGraphics["$id`:$subid"] = @{
            Gfx = [Convert]::ToInt32($entry.Groups['gfx'].Value, 16)
            TileBase = [Convert]::ToInt32($entry.Groups['base'].Value, 16)
            Palette = ($flags -shr 4) -band 7
            DefaultAnimation = $flags -band 15
        }
        $subid++
    }
}
# Repeat the subid pass with alias awareness. The source intentionally stacks
# labels such as interaction6dSubidData / interaction6eSubidData over one
# shared sequence; treating each label as an independent regex block drops the
# first interaction entirely.
$subidAliases = [Collections.Generic.List[int]]::new()
$subidEntries = [Collections.Generic.List[object]]::new()
function Complete-InteractionSubidAliases {
    if ($script:subidEntries.Count -eq 0) { return }
    foreach ($aliasId in $script:subidAliases) {
        for ($index = 0; $index -lt $script:subidEntries.Count; $index++) {
            $entry = $script:subidEntries[$index]
            $script:interactionGraphics["$aliasId`:$index"] = $entry
        }
    }
    $script:subidAliases.Clear()
    $script:subidEntries.Clear()
}
foreach ($line in ($interactionDataSource -split '\r?\n')) {
    if ($line -match '^interaction(?<id>[0-9a-f]{2})SubidData:') {
        if ($subidEntries.Count -gt 0) { Complete-InteractionSubidAliases }
        $subidAliases.Add([Convert]::ToInt32($Matches['id'], 16))
        continue
    }
    if ($subidAliases.Count -eq 0) { continue }
    if ($line -match 'm_InteractionSubidData\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<base>[0-9a-f]{2})\s+\$(?<flags>[0-9a-f]{2})') {
        $flags = [Convert]::ToInt32($Matches['flags'], 16)
        $subidEntries.Add(@{
            Gfx = [Convert]::ToInt32($Matches['gfx'], 16)
            TileBase = [Convert]::ToInt32($Matches['base'], 16)
            Palette = ($flags -shr 4) -band 7
            DefaultAnimation = $flags -band 15
        })
        continue
    }
    if ($line -match '^[A-Za-z0-9_@]+:') {
        Complete-InteractionSubidAliases
        $subidAliases.Clear()
    }
}
Complete-InteractionSubidAliases
$gfxNames = @{}
foreach ($line in Get-Content (Join-Path $Disassembly "data\ages\objectGfxHeaders.s")) {
    if ($line -match '/\* \$(?<id>[0-9a-f]{2}) \*/ m_ObjectGfxHeader (?<name>[A-Za-z0-9_]+)') {
        $gfxNames[[Convert]::ToInt32($Matches['id'], 16)] = $Matches['name']
    }
}

# Resolve animation indices through the original pointer tables. Animation
# frame byte 1 is a byte offset into the interaction's OAM pointer table (the
# engine adds it directly before reading a word), not a sprite-sheet column.
$interactionAnimationSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\interactionAnimations.s")
function Read-NpcDwTables([string]$source, [string]$tableLabelPattern, [string]$entryPattern) {
    $result = @{}
    $aliases = [Collections.Generic.List[string]]::new()
    $entries = [Collections.Generic.List[string]]::new()
    foreach ($line in ($source -split '\r?\n')) {
        $labelMatch = [regex]::Match($line, "^(?<label>$tableLabelPattern):")
        if ($labelMatch.Success) {
            if ($entries.Count -gt 0) {
                foreach ($alias in $aliases) { $result[$alias] = @($entries) }
                $aliases.Clear()
                $entries.Clear()
            }
            $aliases.Add($labelMatch.Groups['label'].Value)
            continue
        }
        if ($aliases.Count -eq 0) { continue }
        $entryMatch = [regex]::Match($line, "^\s*\.dw\s+(?<entry>$entryPattern)")
        if ($entryMatch.Success) {
            $entries.Add($entryMatch.Groups['entry'].Value)
            continue
        }
        if ($line -match '^[A-Za-z0-9_@]+:') {
            if ($entries.Count -gt 0) {
                foreach ($alias in $aliases) { $result[$alias] = @($entries) }
            }
            $aliases.Clear()
            $entries.Clear()
        }
    }
    if ($entries.Count -gt 0) {
        foreach ($alias in $aliases) { $result[$alias] = @($entries) }
    }
    return $result
}

$npcAnimationTables = Read-NpcDwTables $interactionAnimationSource 'interaction[0-9a-f]{2}Animations' 'interactionAnimation[0-9a-f]+'
$npcOamPointerTables = Read-NpcDwTables $interactionAnimationSource 'interaction[0-9a-f]{2}OamDataPointers' 'interactionOamData[0-9a-f]+'
$npcAnimationFrames = @{}
foreach ($animation in [regex]::Matches($interactionAnimationSource, '(?ms)^(?<label>interactionAnimation[0-9a-f]+):\r?\n(?<body>.*?)(?=^interactionAnimation[0-9a-f]+:|\z)')) {
    $frames = [Collections.Generic.List[object]]::new()
    foreach ($frame in [regex]::Matches($animation.Groups['body'].Value, '\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<frame>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')) {
        $frames.Add(@{
            Duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
            PointerOffset = [Convert]::ToInt32($frame.Groups['frame'].Value, 16)
        })
    }
    if ($frames.Count -gt 0) {
        $npcAnimationFrames[$animation.Groups['label'].Value] = @($frames)
    }
}

$npcOamBlocks = @{}
$interactionOamSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\interactionOamData.s")
foreach ($oam in [regex]::Matches($interactionOamSource, '(?ms)^(?<label>interactionOamData[0-9a-f]+):[^\r\n]*\r?\n(?<body>.*?)(?=^interactionOamData[0-9a-f]+:|\z)')) {
    $dataLines = [regex]::Matches($oam.Groups['body'].Value, '(?m)^\s*\.db\s+(?<bytes>[^;\r\n]+)')
    if ($dataLines.Count -eq 0) { continue }
    $countMatch = [regex]::Match($dataLines[0].Groups['bytes'].Value, '\$(?<count>[0-9a-f]{2})')
    if (-not $countMatch.Success) { continue }
    $count = [Convert]::ToInt32($countMatch.Groups['count'].Value, 16)
    $blocks = [Collections.Generic.List[string]]::new()
    for ($index = 1; $index -le $count -and $index -lt $dataLines.Count; $index++) {
        $values = [regex]::Matches($dataLines[$index].Groups['bytes'].Value, '\$(?<value>[0-9a-f]{2})')
        if ($values.Count -lt 4) { continue }
        $blocks.Add(($values | Select-Object -First 4 | ForEach-Object {
            [Convert]::ToInt32($_.Groups['value'].Value, 16)
        }) -join ',')
    }
    $npcOamBlocks[$oam.Groups['label'].Value] = $blocks -join ';'
}

function Resolve-NpcAnimation([int]$interactionId, [int]$animationIndex) {
    $hex = $interactionId.ToString('x2')
    $animationKey = "interaction${hex}Animations"
    $pointerKey = "interaction${hex}OamDataPointers"
    if (-not $npcAnimationTables.ContainsKey($animationKey) -or -not $npcOamPointerTables.ContainsKey($pointerKey)) { return '' }
    $animations = $npcAnimationTables[$animationKey]
    if ($animationIndex -lt 0 -or $animationIndex -ge $animations.Count) { return '' }
    $animationLabel = $animations[$animationIndex]
    if (-not $npcAnimationFrames.ContainsKey($animationLabel)) { return '' }
    $pointers = $npcOamPointerTables[$pointerKey]
    $resolvedFrames = [Collections.Generic.List[string]]::new()
    foreach ($frame in $npcAnimationFrames[$animationLabel]) {
        $pointerIndex = [int]($frame.PointerOffset / 2)
        if ($pointerIndex -lt 0 -or $pointerIndex -ge $pointers.Count) { continue }
        $oamLabel = $pointers[$pointerIndex]
        $oam = if ($npcOamBlocks.ContainsKey($oamLabel)) { $npcOamBlocks[$oamLabel] } else { '' }
        $resolvedFrames.Add("$($frame.Duration)@$oam")
    }
    return $resolvedFrames -join '|'
}

# Room object data is grouped by room label. Only positioned interaction
# objects are emitted; state-only two-byte interaction records cannot spawn a
# visible room NPC without a position and are intentionally left to the later
# object-system slice.
$npcRows = [Collections.Generic.List[string]]::new()
$npcRows.Add("# group`troom`tid`tsubid`ty`tx`tvar03`ttext-id`tsprite`ttile-base`tpalette`tdefault-animation`tcan-face`tup-animation`tright-animation`tdown-animation`tleft-animation`tutf8-base64")
$mainObjectLines = Get-Content (Join-Path $Disassembly "objects\ages\mainData.s")
$currentGroup = -1
$currentRoom = -1
$npcSpriteNames = [Collections.Generic.HashSet[string]]::new()
foreach ($line in $mainObjectLines) {
    if ($line -match '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $currentGroup = [Convert]::ToInt32($Matches['group'], 10)
        $currentRoom = [Convert]::ToInt32($Matches['room'], 16)
        continue
    }
    if ($currentGroup -lt 0 -or $line -notmatch 'obj_Interaction\s+\$(?<id>[0-9a-f]{2})\s+\$(?<subid>[0-9a-f]{2})\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})(?:\s+\$(?<var03>[0-9a-f]{2}))?') { continue }
    $id = [Convert]::ToInt32($Matches['id'], 16)
    if (-not $npcInteractionIds.Contains($id)) { continue }
    $subid = [Convert]::ToInt32($Matches['subid'], 16)
    # INTERAC_SHOOTING_GALLERY subids 0-2 are the human, goron, and elder
    # attendants; subid 3 is the invisible minigame controller.
    if ($id -eq 0x30 -and $subid -eq 0x03) { continue }
    $y = $Matches['y']
    $x = $Matches['x']
    $var03 = if ($Matches['var03']) { $Matches['var03'] } else { '00' }
    $graphic = $interactionGraphics["$id`:$subid"]
    if ($null -eq $graphic) { $graphic = $interactionGraphics["$id`:0"] }
    if ($null -eq $graphic -or -not $gfxNames.ContainsKey($graphic.Gfx)) { continue }
    $spriteName = $gfxNames[$graphic.Gfx]
    [void]$npcSpriteNames.Add($spriteName)
    $textId = if ($npcTextBySubid.ContainsKey("$id`:$subid")) { $npcTextBySubid["$id`:$subid"] } else { 0 }
    $message = if ($allTexts.ContainsKey($textId)) { $allTexts[$textId] } else { '' }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message))
    # Only enable autonomous facing for interactions whose talk script is
    # currently resolved. Many IDs reuse the same code for both ordinary NPC
    # and cutscene subids; applying the helper to the whole ID makes actors in
    # scripted scenes turn toward Link when the original subid would not.
    $canFace = $textId -ne 0 -and $npcFacingIds.Contains($id) -and $graphic.DefaultAnimation -ge 2
    $downOam = Resolve-NpcAnimation $id $graphic.DefaultAnimation
    if ($canFace) {
        $upOam = Resolve-NpcAnimation $id ($graphic.DefaultAnimation - 2)
        $rightOam = Resolve-NpcAnimation $id ($graphic.DefaultAnimation - 1)
        $leftOam = Resolve-NpcAnimation $id ($graphic.DefaultAnimation + 1)
    } else {
        $upOam = $downOam
        $rightOam = $downOam
        $leftOam = $downOam
    }
    if (-not $upOam) { $upOam = $downOam }
    if (-not $rightOam) { $rightOam = $downOam }
    if (-not $leftOam) { $leftOam = $downOam }
    $npcRows.Add("$currentGroup`t$($currentRoom.ToString('x2'))`t$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$y`t$x`t$var03`t$($textId.ToString('x4'))`t$spriteName`t$($graphic.TileBase)`t$($graphic.Palette)`t$($graphic.DefaultAnimation)`t$([int]$canFace)`t$upOam`t$rightOam`t$downOam`t$leftOam`t$encoded")
}
if ($npcRows.Count -ne 378) {
    throw "Expected 377 positioned NPC/character records from Ages mainData.s, parsed $($npcRows.Count - 1)."
}
$villagerRow = $npcRows | Where-Object { $_ -match '^0\t48\t3a\t03\t' } | Select-Object -First 1
if (-not $villagerRow) { throw "The canonical room 0:48 villager record was not extracted." }
$villagerColumns = $villagerRow -split "`t"
if ($villagerColumns[7] -ne '1420' -or $villagerColumns[9] -ne '16' -or
    $villagerColumns[10] -ne '1' -or $villagerColumns[11] -ne '2' -or
    $villagerColumns[13] -ne '16@8,0,4,0;8,8,6,0|16@8,0,6,32;8,8,4,32' -or
    $villagerColumns[14] -ne '16@8,0,10,32;8,8,8,32|16@8,0,14,32;8,8,12,32' -or
    $villagerColumns[15] -ne '16@8,0,0,0;8,8,2,0|16@8,0,2,32;8,8,0,32' -or
    $villagerColumns[16] -ne '16@8,0,8,0;8,8,10,0|16@8,0,12,0;8,8,14,0') {
    throw "The room 0:48 villager no longer matches interaction3a animation/OAM data."
}

# Copy every sprite sheet referenced by the extracted NPC records. The source
# keeps common and Ages graphics in separate directories, so search both.
foreach ($spriteName in $npcSpriteNames) {
    $sourceSprite = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
        ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$spriteName.png" } |
        Select-Object -First 1
    if ($null -eq $sourceSprite) { throw "NPC sprite not found in disassembly: $spriteName.png" }
    $targetSprite = Join-Path $destination "gfx\$spriteName.png"
    Copy-Item -LiteralPath $sourceSprite.FullName -Destination $targetSprite -Force
}
$npcPath = Join-Path $destination "objects\npcs.tsv"
[IO.File]::WriteAllLines($npcPath, $npcRows, [Text.UTF8Encoding]::new($false))

# INTERAC_TIMEPORTAL_SPAWNER ($e1) is a scenery interaction rather than an
# NPC, but it uses the same interaction graphics, animation, and OAM tables.
# Export every placed portal spot so runtime activation stays data-driven.
$portalGraphic = $interactionGraphics['225:0']
if ($null -eq $portalGraphic) {
    throw 'Could not resolve INTERAC_TIMEPORTAL_SPAWNER graphics.'
}
$portalAnimation = Resolve-NpcAnimation 0xe1 $portalGraphic.DefaultAnimation
$portalAnimationLabel = $npcAnimationTables['interactione1Animations'][$portalGraphic.DefaultAnimation]
$portalAnimationBlock = [regex]::Match(
    $interactionAnimationSource,
    "(?ms)^$portalAnimationLabel`:(?<intro>.*?)(?:^${portalAnimationLabel}Loop:)(?<loop>.*?)(?=^interactionAnimation[0-9a-f]+:|\z)")
if (-not $portalAnimation -or -not $portalAnimationBlock.Success) {
    throw 'Could not resolve INTERAC_TIMEPORTAL_SPAWNER graphics and animation.'
}
$portalLoopStart = [regex]::Matches(
    $portalAnimationBlock.Groups['intro'].Value,
    '\.db\s+\$[0-9a-f]{2}\s+\$[0-9a-f]{2}\s+\$[0-9a-f]{2}').Count
$portalRows = [Collections.Generic.List[string]]::new()
$portalRows.Add("# group`troom`tsubid`ty`tx`tsprite`ttile-base`tpalette`tloop-start`tanimation")
$currentGroup = -1
$currentRoom = -1
foreach ($line in $mainObjectLines) {
    if ($line -match '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $currentGroup = [Convert]::ToInt32($Matches['group'], 10)
        $currentRoom = [Convert]::ToInt32($Matches['room'], 16)
        continue
    }
    if ($currentGroup -lt 0 -or
        $line -notmatch 'obj_Interaction\s+\$e1\s+\$(?<subid>[0-9a-f]{2})\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})') {
        continue
    }
    $portalRows.Add("$currentGroup`t$($currentRoom.ToString('x2'))`t$($Matches['subid'])`t$($Matches['y'])`t$($Matches['x'])`tspr_makuflower_book_seedling_weirdswirl_block`t$($portalGraphic.TileBase)`t$($portalGraphic.Palette)`t$portalLoopStart`t$portalAnimation")
}
if ($portalRows.Count -ne 22) {
    throw "Expected 21 positioned time-portal spawners, parsed $($portalRows.Count - 1)."
}
if ($portalLoopStart -ne 3) {
    throw "INTERAC_TIMEPORTAL_SPAWNER animation loop moved from frame 3 to $portalLoopStart."
}
$initialPortal = $portalRows | Where-Object { $_ -match '^0\t39\t01\t28\t28\t' }
if (-not $initialPortal) {
    throw 'The initial active portal in room 0:39 was not extracted.'
}
Copy-GeneratedFile `
    'gfx_compressible\ages\spr_makuflower_book_seedling_weirdswirl_block.png' `
    'gfx\spr_makuflower_book_seedling_weirdswirl_block.png'
$portalPath = Join-Path $destination 'objects\timePortals.tsv'
[IO.File]::WriteAllLines($portalPath, $portalRows, [Text.UTF8Encoding]::new($false))

# The first present Maku Tree visit is interaction $87 subid $01, selected
# from room 0:38's $87:$00 object while wMakuTreeState and GLOBALFLAG_0c are
# both clear. Export its complete simulated-input/script timing, all five tree
# animations, text, hardcoded destination, and four cycling background
# palettes instead of encoding disassembly-only details in runtime code.
$makuTreeSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\makuTree.s')
$makuScriptSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$makuCutsceneSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$makuInputMatch = [regex]::Match(
    $makuTreeSource,
    '(?ms)@simulatedInput:\s*dwb\s+(?<idle>\d+)\s+\$00\s+dwb\s+(?<right>\d+)\s+BTN_RIGHT\s+dwb\s+(?<stop>\d+)\s+\$00\s+dwb\s+(?<up>\d+)\s+BTN_UP\s+dwb\s+(?<tail>\d+)\s+\$00')
if (-not $makuInputMatch.Success) {
    throw 'Could not parse the Maku Tree disappearance simulated-input record.'
}
$makuScriptMatch = [regex]::Match(
    $makuScriptSource,
    '(?ms)makuTree_subid01Script_body:(?<body>.*?)(?=^makuTree_subid02Script_body:)')
if (-not $makuScriptMatch.Success) {
    throw 'Could not parse makuTree_subid01Script_body.'
}
$makuWaits = @([regex]::Matches($makuScriptMatch.Groups['body'].Value, '(?m)^\s*wait\s+(?<frames>\d+)') |
    ForEach-Object { [int]$_.Groups['frames'].Value })
if ($makuWaits.Count -ne 6 -or ($makuWaits -join ',') -ne '210,60,60,210,210,150') {
    throw "Unexpected Maku Tree disappearance waits: $($makuWaits -join ',')."
}
$makuWarpMatch = [regex]::Match(
    $makuCutsceneSource,
    'm_HardcodedWarpA\s+ROOM_AGES_(?<room>[0-9a-f]{3}),\s*\$(?<source>[0-9a-f]{2}),\s*\$(?<position>[0-9a-f]{2}),\s*\$(?<transition2>[0-9a-f]{2})')
if (-not $makuWarpMatch.Success -or $makuWarpMatch.Groups['room'].Value -ne '038') {
    throw 'Could not parse the Maku Tree disappearance hardcoded warp.'
}
$makuAnimations = @(0..4 | ForEach-Object { Resolve-NpcAnimation 0x87 $_ })
if (($makuAnimations | Where-Object { -not $_ }).Count -ne 0) {
    throw 'Could not resolve all five INTERAC_MAKU_TREE animations.'
}
# interactionLoadExtraGraphics follows object graphics header $04 until the
# stop bit on $05, appending the second 16-tile sheet after the first.
$makuGfxIndex = $interactionGraphics['135:0'].Gfx
$makuExtraSprite = $gfxNames[$makuGfxIndex + 1]
$objectGfxSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\objectGfxHeaders.s')
if ($makuGfxIndex -ne 0x04 -or $makuExtraSprite -ne 'spr_makuadultsprites_2' -or
    $objectGfxSource -notmatch '/\* \$05 \*/ m_ObjectGfxHeader spr_makuadultsprites_2, 1') {
    throw 'Could not resolve the Maku Tree extra object-graphics header chain $04-$05.'
}
$makuExtraSource = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$makuExtraSprite.png" } |
    Select-Object -First 1
if ($null -eq $makuExtraSource) { throw "Maku Tree extra sprite not found: $makuExtraSprite.png" }
Copy-Item -LiteralPath $makuExtraSource.FullName -Destination (
    Join-Path $destination "gfx\$makuExtraSprite.png") -Force
foreach ($textId in @(0x0564, 0x0540, 0x0541)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Maku Tree cutscene text TX_$($textId.ToString('x4'))."
    }
    if (-not $allTextPositions.ContainsKey($textId) -or $allTextPositions[$textId] -ne 2) {
        throw "Expected Maku Tree cutscene text TX_$($textId.ToString('x4')) to use \\pos(2)."
    }
}
$makuColumns = [Collections.Generic.List[string]]::new()
$makuColumns.AddRange([string[]]@(
    '0', '38', '87', '00',
    $makuInputMatch.Groups['idle'].Value,
    $makuInputMatch.Groups['right'].Value,
    $makuInputMatch.Groups['stop'].Value,
    $makuInputMatch.Groups['up'].Value,
    $makuInputMatch.Groups['tail'].Value
))
foreach ($wait in $makuWaits) { $makuColumns.Add($wait.ToString()) }
$transition2 = [Convert]::ToInt32($makuWarpMatch.Groups['transition2'].Value, 16)
$makuColumns.AddRange([string[]]@(
    [Convert]::ToInt32($makuWarpMatch.Groups['source'].Value, 16).ToString(),
    '0',
    $makuWarpMatch.Groups['room'].Value.Substring(1),
    $makuWarpMatch.Groups['position'].Value,
    (($transition2 -shr 4) -band 0x07).ToString(),
    ($transition2 -band 0x03).ToString()
))
$makuColumns.AddRange([string[]]$makuAnimations)
$makuColumns.Add($makuExtraSprite)
$makuColumns.Add('2')
foreach ($textId in @(0x0564, 0x0540, 0x0541)) {
    $makuColumns.Add([Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId])))
}
$makuEventRows = @(
    "# group`troom`tid`tsubid`tinput-idle`tinput-right`tinput-stop`tinput-up`tinput-tail`tintro-delay`tpost-intro`tfrown-delay`tdisappearance`tpost-ahh`tfinish-delay`tsource-transition`tdestination-group`tdestination-room`tdestination-position`tdestination-parameter`tdestination-transition`tanimation0`tanimation1`tanimation2`tanimation3`tanimation4`textra-sprite`ttextbox-position`tintro-base64`tahh-base64`thelp-base64",
    ($makuColumns -join "`t")
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\maku_tree_cutscene.tsv'),
    $makuEventRows,
    [Text.UTF8Encoding]::new($false))

$makuPaletteLabels = @('paletteData4530', 'paletteData4550', 'paletteData4500', 'paletteData4570')
$makuPaletteBytes = [Collections.Generic.List[byte]]::new()
foreach ($label in $makuPaletteLabels) {
    $labelIndex = $paletteDataSource.IndexOf("${label}:", [StringComparison]::Ordinal)
    if ($labelIndex -lt 0) { throw "Maku Tree palette data not found: $label" }
    $nextLabel = $paletteDataSource.IndexOf(
        'paletteData', $labelIndex + $label.Length, [StringComparison]::Ordinal)
    if ($nextLabel -lt 0) { $nextLabel = $paletteDataSource.Length }
    $block = $paletteDataSource.Substring($labelIndex, $nextLabel - $labelIndex)
    $colors = [regex]::Matches(
        $block,
        'm_RGB16\s+\$(?<r>[0-9a-f]{2})\s+\$(?<g>[0-9a-f]{2})\s+\$(?<b>[0-9a-f]{2})')
    if ($colors.Count -lt 16) { throw "$label contains fewer than four background palettes." }
    for ($color = 0; $color -lt 16; $color++) {
        $makuPaletteBytes.Add([Convert]::ToByte($colors[$color].Groups['r'].Value, 16))
        $makuPaletteBytes.Add([Convert]::ToByte($colors[$color].Groups['g'].Value, 16))
        $makuPaletteBytes.Add([Convert]::ToByte($colors[$color].Groups['b'].Value, 16))
    }
}
if ($makuPaletteBytes.Count -ne 192) {
    throw "Expected 192 Maku Tree disappearance palette bytes, got $($makuPaletteBytes.Count)."
}
[IO.File]::WriteAllBytes(
    (Join-Path $destination 'metadata\maku_tree_disappear_palettes.bin'),
    $makuPaletteBytes.ToArray())

# Ralph's first portal departure is INTERAC_RALPH ($37) subid $0d in room
# 0:39. Export the entry-direction guard, complete script timing/movement,
# animations, one-shot global flag, and text from their original records.
$ralphSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\ralph.s')
$ralphScriptSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$ralphInitMatch = [regex]::Match(
    $ralphSource,
    '(?ms)@initSubid0d:\s*ld a,\(wScreenTransitionDirection\)\s*cp \$(?<direction>[0-9a-f]{2})\s*jp nz,interactionDelete.*?ld hl,mainScripts\.ralphSubid0dScript')
if (-not $ralphInitMatch.Success) {
    throw 'Could not parse the room-entry direction guard for Ralph subid $0d.'
}
$ralphScriptMatch = [regex]::Match(
    $ralphScriptSource,
    '(?ms)^ralphSubid0dScript:(?<body>.*?)(?=^ralphSubid0eScript:)')
if (-not $ralphScriptMatch.Success) {
    throw 'Could not parse ralphSubid0dScript.'
}
$ralphBody = $ralphScriptMatch.Groups['body'].Value
$ralphWaits = @([regex]::Matches($ralphBody, '(?m)^\s*wait\s+(?<frames>\d+)') |
    ForEach-Object { [int]$_.Groups['frames'].Value })
if ($ralphWaits.Count -ne 2 -or ($ralphWaits -join ',') -ne '40,30') {
    throw "Unexpected Ralph portal event waits: $($ralphWaits -join ',')."
}
$ralphCommandMatch = [regex]::Match(
    $ralphBody,
    '(?ms)showtext\s+TX_(?<text>[0-9a-f]{4}).*?setanimation\s+\$(?<moveAnimation>[0-9a-f]{2})\s+setspeed\s+(?<speed>[A-Z0-9_]+)\s+setangle\s+\$(?<angle>[0-9a-f]{2})\s+applyspeed\s+\$(?<moveFrames>[0-9a-f]{2})\s+setanimation\s+\$(?<portalAnimation>[0-9a-f]{2})\s+writeobjectbyte\s+Interaction\.var3f,\s*\$(?<flickerFrames>[0-9a-f]{2}).*?setglobalflag\s+(?<flag>[A-Z0-9_]+)')
if (-not $ralphCommandMatch.Success -or
    $ralphCommandMatch.Groups['speed'].Value -ne 'SPEED_100' -or
    $ralphCommandMatch.Groups['flag'].Value -ne 'GLOBALFLAG_RALPH_ENTERED_PORTAL') {
    throw 'Could not parse the Ralph portal movement, flicker, and flag commands.'
}
$speedSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$speedMatch = [regex]::Match(
    $speedSource,
    '(?m)^\s*SPEED_100\s+dsb\s+(?<count>\d+)\s*;\s*0x(?<value>[0-9a-f]{2})')
if (-not $speedMatch.Success -or $speedMatch.Groups['value'].Value -ne '28') {
    throw 'SPEED_100 no longer resolves to original object speed $28.'
}
$globalFlagSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\globalFlags.s')
$flagMatch = [regex]::Match(
    $globalFlagSource,
    '(?m)^\s*GLOBALFLAG_RALPH_ENTERED_PORTAL\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $flagMatch.Success -or $flagMatch.Groups['value'].Value -ne '40') {
    throw 'GLOBALFLAG_RALPH_ENTERED_PORTAL no longer resolves to $40.'
}
$ralphNpcRow = $npcRows | Where-Object { $_ -match '^0\t39\t37\t0d\t' } |
    Select-Object -First 1
if (-not $ralphNpcRow) {
    throw 'The positioned INTERAC_RALPH $37:$0d record in room 0:39 was not extracted.'
}
$ralphNpcColumns = $ralphNpcRow -split "`t"
if ($ralphNpcColumns[4] -ne '28' -or $ralphNpcColumns[5] -ne '18') {
    throw 'INTERAC_RALPH $37:$0d moved from original position $28/$18.'
}
$ralphTextId = [Convert]::ToInt32($ralphCommandMatch.Groups['text'].Value, 16)
if ($ralphTextId -ne 0x2a1e -or -not $allTexts.ContainsKey($ralphTextId) -or
    $allTextPositions.ContainsKey($ralphTextId)) {
    throw 'Expected Ralph portal dialogue TX_2a1e without a fixed textbox position.'
}
$ralphMoveAnimationIndex = [Convert]::ToInt32(
    $ralphCommandMatch.Groups['moveAnimation'].Value, 16)
$ralphPortalAnimationIndex = [Convert]::ToInt32(
    $ralphCommandMatch.Groups['portalAnimation'].Value, 16)
$ralphMoveAnimation = Resolve-NpcAnimation 0x37 $ralphMoveAnimationIndex
$ralphPortalAnimation = Resolve-NpcAnimation 0x37 $ralphPortalAnimationIndex
if (-not $ralphMoveAnimation -or -not $ralphPortalAnimation) {
    throw 'Could not resolve Ralph portal event animations $01 and $09.'
}
$ralphEventColumns = @(
    '0', '39', '37', '0d', $ralphInitMatch.Groups['direction'].Value,
    $ralphWaits[0].ToString(), $ralphWaits[1].ToString(),
    [Convert]::ToInt32($ralphCommandMatch.Groups['moveFrames'].Value, 16).ToString(),
    [Convert]::ToInt32($ralphCommandMatch.Groups['flickerFrames'].Value, 16).ToString(),
    $speedMatch.Groups['value'].Value, $ralphCommandMatch.Groups['angle'].Value,
    $flagMatch.Groups['value'].Value, $ralphTextId.ToString('x4'),
    $ralphMoveAnimation, $ralphPortalAnimation,
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$ralphTextId]))
)
$ralphEventRows = @(
    "# group`troom`tid`tsubid`tentry-direction`tintro-delay`tpost-text`tapplyspeed-counter`tflicker-frames`tspeed`tangle`tglobal-flag`ttext-id`tmove-animation`tportal-animation`ttext-base64",
    ($ralphEventColumns -join "`t")
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\ralph_portal_event.tsv'),
    $ralphEventRows,
    [Text.UTF8Encoding]::new($false))

# Keese are the first supported enemy. Their room records use random-position
# enemy opcodes, while their attributes, animations, OAM, and graphics are in
# the shared enemy tables. Export the resolved values so runtime code never
# reparses assembly source.
$enemyDataSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\enemyData.s")
$keeseDataMatch = [regex]::Match(
    $enemyDataSource,
    '(?m)^\s*/\* 0x32 \*/ m_EnemyData \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) \$(?<extra>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
)
if (-not $keeseDataMatch.Success) { throw "Could not resolve ENEMY_KEESE (`$32) data." }
$keeseGfx = [Convert]::ToInt32($keeseDataMatch.Groups['gfx'].Value, 16)
$keeseExtraIndex = [Convert]::ToInt32($keeseDataMatch.Groups['extra'].Value, 16) -band 0x7f
$keeseGraphicFlags = [Convert]::ToInt32($keeseDataMatch.Groups['flags'].Value, 16)
if ($keeseGfx -ne 0x9d -or
    ([Convert]::ToInt32($keeseDataMatch.Groups['collision'].Value, 16) -band 0x7f) -ne 0x1f -or
    $keeseExtraIndex -ne 0x07) {
    throw "ENEMY_KEESE no longer resolves to gfx `$9d, collision mode `$1f, extra data `$07."
}

$extraEnemyBody = [regex]::Match(
    $enemyDataSource,
    '(?ms)^extraEnemyData:\r?\n(?<body>.*)'
).Groups['body'].Value
$extraEnemyRows = [regex]::Matches(
    $extraEnemyBody,
    '(?m)^\s*\.db \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<damage>[0-9a-f]{2}) \$(?<health>[0-9a-f]{2})'
)
if ($extraEnemyRows.Count -le $keeseExtraIndex) { throw "Keese extra-enemy record `$07 is missing." }
$keeseExtra = $extraEnemyRows[$keeseExtraIndex]
$keeseRadiusY = [Convert]::ToInt32($keeseExtra.Groups['y'].Value, 16)
$keeseRadiusX = [Convert]::ToInt32($keeseExtra.Groups['x'].Value, 16)
$keeseDamageByte = [Convert]::ToInt32($keeseExtra.Groups['damage'].Value, 16)
$keeseDamageQuarters = (0x100 - $keeseDamageByte) / 2
$keeseHealth = [Convert]::ToInt32($keeseExtra.Groups['health'].Value, 16)
if ($keeseRadiusY -ne 4 -or $keeseRadiusX -ne 6 -or
    $keeseDamageQuarters -ne 2 -or $keeseHealth -ne 1) {
    throw "ENEMY_KEESE extra data no longer matches radii 4x6, half-heart damage, and 1 health."
}

function Get-AssemblyLabelBody([string]$source, [string]$label) {
    $escaped = [regex]::Escape($label)
    $match = [regex]::Match(
        $source,
        "(?ms)^${escaped}:[^\r\n]*\r?\n(?<body>.*?)(?=^[A-Za-z0-9_@]+:|\z)"
    )
    if (-not $match.Success) { throw "Assembly label not found: $label" }
    return $match.Groups['body'].Value
}

$enemyAnimationSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\enemyAnimations.s")
$enemyOamSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\enemyOamData.s")
$keeseAnimationLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyAnimationSource 'enemy32Animations'),
        '(?m)^\s*\.dw\s+(?<label>enemyAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$keeseOamLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyAnimationSource 'enemy32OamDataPointers'),
        '(?m)^\s*\.dw\s+(?<label>enemyOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($keeseAnimationLabels.Count -ne 2 -or $keeseOamLabels.Count -ne 2) {
    throw "Expected two Keese animations and two Keese OAM pointers."
}

function Resolve-EnemyOam([string]$label) {
    $body = Get-AssemblyLabelBody $script:enemyOamSource $label
    $countMatch = [regex]::Match($body, '(?m)^\s*\.db\s+\$(?<count>[0-9a-f]{2})')
    if (-not $countMatch.Success) { throw "OAM count missing for $label." }
    $count = [Convert]::ToInt32($countMatch.Groups['count'].Value, 16)
    $parts = @(
        [regex]::Matches(
            $body,
            '(?m)^\s*\.db\s+\$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
        ) | ForEach-Object {
            "$([Convert]::ToInt32($_.Groups['y'].Value, 16)),$([Convert]::ToInt32($_.Groups['x'].Value, 16)),$([Convert]::ToInt32($_.Groups['tile'].Value, 16)),$([Convert]::ToInt32($_.Groups['flags'].Value, 16))"
        }
    )
    if ($parts.Count -ne $count) { throw "$label declares $count OAM parts but contains $($parts.Count)." }
    return $parts -join ';'
}

function Resolve-KeeseAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:enemyAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:keeseOamLabels.Count) {
            throw "$label references missing OAM pointer byte offset $($frame.Groups['offset'].Value)."
        }
        $frames.Add("$duration@$(Resolve-EnemyOam $script:keeseOamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}

$keeseIdleAnimation = Resolve-KeeseAnimation $keeseAnimationLabels[0]
$keeseFlyAnimation = Resolve-KeeseAnimation $keeseAnimationLabels[1]
if ($keeseIdleAnimation -ne '127@8,4,2,0' -or
    $keeseFlyAnimation -ne '4@8,0,0,0;8,8,0,32|4@8,4,2,0') {
    throw "ENEMY_KEESE animation/OAM data no longer matches the folded/flying records."
}

$keeseRows = [Collections.Generic.List[string]]::new()
$keeseRows.Add("# group`troom`tid`tsubid`tflags`tcount`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tidle-animation`tfly-animation")
$keeseAliases = [Collections.Generic.List[object]]::new()
foreach ($line in Get-Content (Join-Path $Disassembly "objects\ages\enemyData.s")) {
    if ($line -match '^group(?<group>[0-5])Map(?<room>[0-9a-f]{2})EnemyObjectData:') {
        $keeseAliases.Add(@{
            Group = [int]$Matches['group']
            Room = $Matches['room']
        })
        continue
    }
    if ($keeseAliases.Count -eq 0) { continue }
    if ($line -match '^\s*obj_RandomEnemy\s+\$(?<flags>[0-9a-f]{2})\s+\$32\s+\$(?<subid>[0-9a-f]{2})') {
        $flags = [Convert]::ToInt32($Matches['flags'], 16)
        $count = ($flags -shr 5) -band 7
        foreach ($alias in $keeseAliases) {
            $keeseRows.Add(
                "$($alias.Group)`t$($alias.Room)`t32`t$($Matches['subid'])`t$($Matches['flags'])`t$count`t$($gfxNames[$keeseGfx])`t$(($keeseGraphicFlags -band 0x0f) * 2)`t$(($keeseGraphicFlags -shr 4) -band 7)`t$keeseRadiusY`t$keeseRadiusX`t$keeseDamageQuarters`t$keeseHealth`t$keeseIdleAnimation`t$keeseFlyAnimation")
        }
        continue
    }
    if ($line -match '^\s*obj_EndPointer') {
        $keeseAliases.Clear()
        continue
    }
    if ($line -match '^[A-Za-z0-9_@]+:') { $keeseAliases.Clear() }
}
$keeseInstanceCount = ($keeseRows | Select-Object -Skip 1 | ForEach-Object {
    [int](($_ -split "`t")[5])
} | Measure-Object -Sum).Sum
if ($keeseRows.Count -ne 54 -or $keeseInstanceCount -ne 158) {
    throw "Expected 53 Keese room records / 158 instances, parsed $($keeseRows.Count - 1) / $keeseInstanceCount."
}
if (-not ($keeseRows | Where-Object { $_ -match '^4\t39\t32\t01\t40\t2\t' })) {
    throw "Canonical subid-1 Keese room 4:39 was not extracted."
}

$keeseSpriteName = $gfxNames[$keeseGfx]
$keeseSourceSprite = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$keeseSpriteName.png" } |
    Select-Object -First 1
if ($null -eq $keeseSourceSprite) { throw "Keese sprite not found in disassembly: $keeseSpriteName.png" }
Copy-Item -LiteralPath $keeseSourceSprite.FullName -Destination (Join-Path $destination "gfx\$keeseSpriteName.png") -Force
$keesePath = Join-Path $destination "objects\keese.tsv"
[IO.File]::WriteAllLines($keesePath, $keeseRows, [Text.UTF8Encoding]::new($false))

# Octoroks (`$09) use both random-position and fixed-position enemy opcodes.
# Ages room data instantiates subids `$00, `$01, and `$02: normal red, fast
# red, and blue. Export resolved per-subid attributes and all four cardinal
# animations alongside the original room-object order.
$octorokDataMatch = [regex]::Match(
    $enemyDataSource,
    '(?m)^\s*/\* 0x09 \*/ m_EnemyData \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) enemy09SubidData'
)
if (-not $octorokDataMatch.Success -or
    [Convert]::ToInt32($octorokDataMatch.Groups['gfx'].Value, 16) -ne 0x8f -or
    [Convert]::ToInt32($octorokDataMatch.Groups['collision'].Value, 16) -ne 0x90) {
    throw 'ENEMY_OCTOROK no longer resolves to gfx `$8f / standard collision mode `$10.'
}
$octorokGfx = [Convert]::ToInt32($octorokDataMatch.Groups['gfx'].Value, 16)
$octorokSubidRows = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyDataSource 'enemy09SubidData'),
        '(?m)^\s*m_EnemySubidData \$(?<extra>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
    )
)
if ($octorokSubidRows.Count -ne 5) {
    throw "Expected five ENEMY_OCTOROK subid records, got $($octorokSubidRows.Count)."
}

$enemy09AnimationStart = $enemyAnimationSource.IndexOf('enemy09Animations:', [StringComparison]::Ordinal)
$enemy0aAnimationStart = $enemyAnimationSource.IndexOf('enemy0aAnimations:', [StringComparison]::Ordinal)
$octorokAnimationLabels = @(
    [regex]::Matches(
        $enemyAnimationSource.Substring(
            $enemy09AnimationStart, $enemy0aAnimationStart - $enemy09AnimationStart),
        '(?m)^\s*\.dw\s+(?<label>enemyAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$enemy09OamStart = $enemyAnimationSource.IndexOf('enemy09OamDataPointers:', [StringComparison]::Ordinal)
$enemy0aOamStart = $enemyAnimationSource.IndexOf('enemy0aOamDataPointers:', [StringComparison]::Ordinal)
$octorokOamLabels = @(
    [regex]::Matches(
        $enemyAnimationSource.Substring($enemy09OamStart, $enemy0aOamStart - $enemy09OamStart),
        '(?m)^\s*\.dw\s+(?<label>enemyOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($octorokAnimationLabels.Count -ne 4 -or $octorokOamLabels.Count -ne 8) {
    throw 'Expected four Octorok animations and eight Octorok OAM pointers.'
}

function Resolve-OctorokAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:enemyAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:octorokOamLabels.Count) {
            throw "$label references missing Octorok OAM pointer $pointerIndex."
        }
        $frames.Add("$duration@$(Resolve-EnemyOam $script:octorokOamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}

$octorokAnimations = @($octorokAnimationLabels | ForEach-Object {
    Resolve-OctorokAnimation $_
})
if ($octorokAnimations[0] -ne '8@8,0,0,64;8,8,0,96|8@8,0,2,64;8,8,2,96' -or
    $octorokAnimations[1] -ne '8@8,0,6,32;8,8,4,32|8@8,0,10,32;8,8,8,32' -or
    $octorokAnimations[2] -ne '8@8,0,0,0;8,8,0,32|8@8,0,2,0;8,8,2,32' -or
    $octorokAnimations[3] -ne '8@8,0,4,0;8,8,6,0|8@8,0,8,0;8,8,10,0') {
    throw 'ENEMY_OCTOROK cardinal animation/OAM data no longer matches the original records.'
}

$octorokDefinitions = @{}
foreach ($subid in 0..2) {
    $subidRow = $octorokSubidRows[$subid]
    $extraIndex = [Convert]::ToInt32($subidRow.Groups['extra'].Value, 16)
    $graphicFlags = [Convert]::ToInt32($subidRow.Groups['flags'].Value, 16)
    $extra = $extraEnemyRows[$extraIndex]
    $damageByte = [Convert]::ToInt32($extra.Groups['damage'].Value, 16)
    $octorokDefinitions[$subid] = @{
        TileBase = ($graphicFlags -band 0x0f) * 2
        Palette = ($graphicFlags -shr 4) -band 7
        RadiusY = [Convert]::ToInt32($extra.Groups['y'].Value, 16)
        RadiusX = [Convert]::ToInt32($extra.Groups['x'].Value, 16)
        DamageQuarters = (0x100 - $damageByte) / 2
        Health = [Convert]::ToInt32($extra.Groups['health'].Value, 16)
        SpeedRaw = if (($subid -band 1) -ne 0) { 0x1e } else { 0x14 }
        CounterMask = if ($subid -lt 2) { 7 } else { 3 }
    }
}
if ($octorokDefinitions[0].Health -ne 2 -or
    $octorokDefinitions[0].DamageQuarters -ne 1 -or
    $octorokDefinitions[1].SpeedRaw -ne 0x1e -or
    $octorokDefinitions[2].Health -ne 3 -or
    $octorokDefinitions[2].DamageQuarters -ne 2 -or
    $octorokDefinitions[2].CounterMask -ne 3) {
    throw 'ENEMY_OCTOROK subid attributes no longer match red/fast-red/blue behavior.'
}

$octorokRows = [Collections.Generic.List[string]]::new()
$octorokRows.Add("# group`troom`tid`tsubid`tflags`tcount`tposition-mode`ty`tx`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tspeed-raw`tcounter-mask`tup-animation`tright-animation`tdown-animation`tleft-animation")
$octorokAliases = [Collections.Generic.List[object]]::new()
$octorokLastSpecificFlags = '00'
foreach ($line in Get-Content (Join-Path $Disassembly 'objects\ages\enemyData.s')) {
    if ($line -match '^group(?<group>[0-5])Map(?<room>[0-9a-f]{2})EnemyObjectData:') {
        $octorokAliases.Add(@{ Group = [int]$Matches['group']; Room = $Matches['room'] })
        continue
    }
    if ($octorokAliases.Count -eq 0) { continue }

    if ($line -match '^\s*obj_RandomEnemy\s+\$(?<flags>[0-9a-f]{2})\s+\$09\s+\$(?<subid>[0-9a-f]{2})') {
        $subid = [Convert]::ToInt32($Matches['subid'], 16)
        if (-not $octorokDefinitions.ContainsKey($subid)) {
            throw "Room data uses unsupported ENEMY_OCTOROK subid `$($subid.ToString('x2'))."
        }
        $definition = $octorokDefinitions[$subid]
        $flags = [Convert]::ToInt32($Matches['flags'], 16)
        $count = ($flags -shr 5) -band 7
        foreach ($alias in $octorokAliases) {
            $octorokRows.Add("$($alias.Group)`t$($alias.Room)`t09`t$($Matches['subid'])`t$($Matches['flags'])`t$count`tR`t-1`t-1`t$($gfxNames[$octorokGfx])`t$($definition.TileBase)`t$($definition.Palette)`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.DamageQuarters)`t$($definition.Health)`t$($definition.SpeedRaw)`t$($definition.CounterMask)`t$($octorokAnimations[0])`t$($octorokAnimations[1])`t$($octorokAnimations[2])`t$($octorokAnimations[3])")
        }
        continue
    }

    if ($line -match '^\s*obj_SpecificEnemyA\s+(?<values>(?:\$[0-9a-f]{2}\s*)+)$') {
        $values = @([regex]::Matches($Matches['values'], '\$(?<value>[0-9a-f]{2})') |
            ForEach-Object { $_.Groups['value'].Value })
        if ($values.Count -eq 5) {
            $octorokLastSpecificFlags = $values[0]
            $id, $subidHex, $y, $x = $values[1..4]
        } else {
            $id, $subidHex, $y, $x = $values
        }
        if ($id -eq '09') {
            $subid = [Convert]::ToInt32($subidHex, 16)
            if (-not $octorokDefinitions.ContainsKey($subid)) {
                throw "Room data uses unsupported fixed ENEMY_OCTOROK subid `$subidHex."
            }
            $definition = $octorokDefinitions[$subid]
            foreach ($alias in $octorokAliases) {
                $octorokRows.Add("$($alias.Group)`t$($alias.Room)`t09`t$subidHex`t$octorokLastSpecificFlags`t1`tF`t$y`t$x`t$($gfxNames[$octorokGfx])`t$($definition.TileBase)`t$($definition.Palette)`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.DamageQuarters)`t$($definition.Health)`t$($definition.SpeedRaw)`t$($definition.CounterMask)`t$($octorokAnimations[0])`t$($octorokAnimations[1])`t$($octorokAnimations[2])`t$($octorokAnimations[3])")
            }
        }
        continue
    }

    if ($line -match '^\s*obj_EndPointer' -or $line -match '^[A-Za-z0-9_@]+:') {
        $octorokAliases.Clear()
    }
}
$octorokInstanceCount = ($octorokRows | Select-Object -Skip 1 | ForEach-Object {
    [int](($_ -split "`t")[5])
} | Measure-Object -Sum).Sum
if ($octorokRows.Count -ne 34 -or $octorokInstanceCount -ne 48) {
    throw "Expected 33 Octorok room records / 48 instances, parsed $($octorokRows.Count - 1) / $octorokInstanceCount."
}
if (-not ($octorokRows | Where-Object { $_ -match '^0\t74\t09\t00\t20\t1\tR\t' }) -or
    -not ($octorokRows | Where-Object { $_ -match '^0\t74\t09\t01\t20\t1\tR\t' }) -or
    -not ($octorokRows | Where-Object { $_ -match '^1\tbc\t09\t02\t00\t1\tF\t48\t48\t' })) {
    throw 'Canonical Octorok records in rooms 0:74 and 1:bc were not extracted.'
}

$octorokSpriteName = $gfxNames[$octorokGfx]
$octorokSourceSprite = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$octorokSpriteName.png" } |
    Select-Object -First 1
if ($null -eq $octorokSourceSprite) { throw "Octorok sprite not found: $octorokSpriteName.png" }
Copy-Item -LiteralPath $octorokSourceSprite.FullName -Destination (Join-Path $destination "gfx\$octorokSpriteName.png") -Force
$octorokPath = Join-Path $destination 'objects\octoroks.tsv'
[IO.File]::WriteAllLines($octorokPath, $octorokRows, [Text.UTF8Encoding]::new($false))

# Zols (`$34) are instantiated with both random and fixed-position enemy
# opcodes. Red Zols split into ENEMY_GEL (`$43), which also has one direct
# random-position room record. Export both definitions with animation
# parameters intact: the terminal parameters on Zol animations 0 and 3 drive
# the emerge/disappear state changes.
$zolDataMatch = [regex]::Match(
    $enemyDataSource,
    '(?m)^\s*/\* 0x34 \*/ m_EnemyData \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) enemy34SubidData'
)
if (-not $zolDataMatch.Success -or
    [Convert]::ToInt32($zolDataMatch.Groups['gfx'].Value, 16) -ne 0x97 -or
    [Convert]::ToInt32($zolDataMatch.Groups['collision'].Value, 16) -ne 0x29) {
    throw 'ENEMY_ZOL no longer resolves to gfx `$97 / collision mode `$29.'
}
$zolGfx = [Convert]::ToInt32($zolDataMatch.Groups['gfx'].Value, 16)
$zolSubidRows = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyDataSource 'enemy34SubidData'),
        '(?m)^\s*m_EnemySubidData \$(?<extra>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
    ) | Select-Object -First 2
)
if ($zolSubidRows.Count -ne 2) {
    throw "Expected two ENEMY_ZOL subid records, got $($zolSubidRows.Count)."
}

$gelDataMatch = [regex]::Match(
    $enemyDataSource,
    '(?m)^\s*/\* 0x43 \*/ m_EnemyData \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) \$(?<extra>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
)
if (-not $gelDataMatch.Success -or
    [Convert]::ToInt32($gelDataMatch.Groups['gfx'].Value, 16) -ne 0x97 -or
    [Convert]::ToInt32($gelDataMatch.Groups['collision'].Value, 16) -ne 0xb3 -or
    [Convert]::ToInt32($gelDataMatch.Groups['extra'].Value, 16) -ne 0x06 -or
    [Convert]::ToInt32($gelDataMatch.Groups['flags'].Value, 16) -ne 0x20) {
    throw 'ENEMY_GEL no longer resolves to gfx `$97 / collision `$b3 / extra `$06 / flags `$20.'
}

$zolAnimationLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyAnimationSource 'enemy34Animations'),
        '(?m)^\s*\.dw\s+(?<label>enemyAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$zolOamLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyAnimationSource 'enemy34OamDataPointers'),
        '(?m)^\s*\.dw\s+(?<label>enemyOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$gelAnimationLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyAnimationSource 'enemy43Animations'),
        '(?m)^\s*\.dw\s+(?<label>enemyAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$gelOamLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyAnimationSource 'enemy43OamDataPointers'),
        '(?m)^\s*\.dw\s+(?<label>enemyOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($zolAnimationLabels.Count -ne 6 -or $zolOamLabels.Count -ne 5 -or
    $gelAnimationLabels.Count -ne 3 -or $gelOamLabels.Count -ne 7) {
    throw 'Expected 6/5 Zol and 3/7 Gel animation/OAM records.'
}

function Resolve-ParameterizedEnemyAnimation(
    [string]$label,
    [string[]]$oamLabels
) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:enemyAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $oamLabels.Count) {
            throw "$label references missing enemy OAM pointer $pointerIndex."
        }
        $frames.Add("$duration,$parameter@$(Resolve-EnemyOam $oamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}

$zolAnimations = @($zolAnimationLabels | ForEach-Object {
    Resolve-ParameterizedEnemyAnimation $_ $zolOamLabels
})
$gelAnimations = @($gelAnimationLabels | ForEach-Object {
    Resolve-ParameterizedEnemyAnimation $_ $gelOamLabels
})
if ($zolAnimations[0] -ne '16,0@12,4,0,0|16,0@8,0,4,0;8,8,4,32|127,1@8,0,2,0;8,8,2,32' -or
    $zolAnimations[3] -ne '8,0@8,0,2,0;8,8,2,32|16,0@8,0,4,0;8,8,4,32|16,0@12,4,0,0|127,1@12,4,0,0' -or
    $gelAnimations[1] -ne '4,0@6,2,0,0|4,0@10,6,0,0|4,0@6,6,0,0|4,0@10,2,0,0') {
    throw "ENEMY_ZOL/ENEMY_GEL animation data no longer matches the original records: z0=$($zolAnimations[0]); z3=$($zolAnimations[3]); g1=$($gelAnimations[1])"
}

$zolDefinitions = @{}
foreach ($subid in 0..1) {
    $subidRow = $zolSubidRows[$subid]
    $extraIndex = [Convert]::ToInt32($subidRow.Groups['extra'].Value, 16)
    $graphicFlags = [Convert]::ToInt32($subidRow.Groups['flags'].Value, 16)
    $extra = $extraEnemyRows[$extraIndex]
    $damageByte = [Convert]::ToInt32($extra.Groups['damage'].Value, 16)
    $zolDefinitions[$subid] = @{
        TileBase = ($graphicFlags -band 0x0f) * 2
        Palette = ($graphicFlags -shr 4) -band 7
        RadiusY = [Convert]::ToInt32($extra.Groups['y'].Value, 16)
        RadiusX = [Convert]::ToInt32($extra.Groups['x'].Value, 16)
        DamageQuarters = (0x100 - $damageByte) / 2
        Health = [Convert]::ToInt32($extra.Groups['health'].Value, 16)
    }
}
if ($zolDefinitions[0].Health -ne 2 -or $zolDefinitions[0].Palette -ne 0 -or
    $zolDefinitions[1].Health -ne 3 -or $zolDefinitions[1].Palette -ne 2 -or
    $zolDefinitions[0].DamageQuarters -ne 2 -or
    $zolDefinitions[0].RadiusY -ne 6 -or $zolDefinitions[0].RadiusX -ne 6) {
    throw 'ENEMY_ZOL subid attributes no longer match green/red behavior.'
}

$gelExtraIndex = [Convert]::ToInt32($gelDataMatch.Groups['extra'].Value, 16)
$gelGraphicFlags = [Convert]::ToInt32($gelDataMatch.Groups['flags'].Value, 16)
$gelExtra = $extraEnemyRows[$gelExtraIndex]
$gelDamageByte = [Convert]::ToInt32($gelExtra.Groups['damage'].Value, 16)
$gelDefinition = @{
    TileBase = ($gelGraphicFlags -band 0x0f) * 2
    Palette = ($gelGraphicFlags -shr 4) -band 7
    RadiusY = [Convert]::ToInt32($gelExtra.Groups['y'].Value, 16)
    RadiusX = [Convert]::ToInt32($gelExtra.Groups['x'].Value, 16)
    DamageQuarters = (0x100 - $gelDamageByte) / 2
    Health = [Convert]::ToInt32($gelExtra.Groups['health'].Value, 16)
}
if ($gelDefinition.TileBase -ne 0 -or $gelDefinition.Palette -ne 2 -or
    $gelDefinition.RadiusY -ne 2 -or $gelDefinition.RadiusX -ne 2 -or
    $gelDefinition.DamageQuarters -ne 2 -or $gelDefinition.Health -ne 1) {
    throw 'ENEMY_GEL attributes no longer match radius 2x2, half-heart damage, and one health.'
}

$zolRows = [Collections.Generic.List[string]]::new()
$zolRows.Add("# group`troom`tid`tsubid`tflags`tcount`tposition-mode`ty`tx`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tanimation-0`tanimation-1`tanimation-2`tanimation-3`tanimation-4`tanimation-5")
$gelRows = [Collections.Generic.List[string]]::new()
$gelRows.Add("# group`troom`tid`tsubid`tflags`tcount`tposition-mode`ty`tx`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tanimation-0`tanimation-1`tanimation-2")
$zolAliases = [Collections.Generic.List[object]]::new()
$zolLastSpecificFlags = '00'
foreach ($line in Get-Content (Join-Path $Disassembly 'objects\ages\enemyData.s')) {
    if ($line -match '^group(?<group>[0-5])Map(?<room>[0-9a-f]{2})EnemyObjectData:') {
        $zolAliases.Add(@{ Group = [int]$Matches['group']; Room = $Matches['room'] })
        continue
    }
    if ($zolAliases.Count -eq 0) { continue }

    if ($line -match '^\s*obj_RandomEnemy\s+\$(?<flags>[0-9a-f]{2})\s+\$(?<id>34|43)\s+\$(?<subid>[0-9a-f]{2})') {
        $id = $Matches['id']
        $subid = [Convert]::ToInt32($Matches['subid'], 16)
        $flags = [Convert]::ToInt32($Matches['flags'], 16)
        $count = ($flags -shr 5) -band 7
        foreach ($alias in $zolAliases) {
            if ($id -eq '34') {
                if (-not $zolDefinitions.ContainsKey($subid)) {
                    throw "Room data uses unsupported ENEMY_ZOL subid `$($subid.ToString('x2'))."
                }
                $definition = $zolDefinitions[$subid]
                $zolRows.Add("$($alias.Group)`t$($alias.Room)`t34`t$($Matches['subid'])`t$($Matches['flags'])`t$count`tR`t-1`t-1`t$($gfxNames[$zolGfx])`t$($definition.TileBase)`t$($definition.Palette)`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.DamageQuarters)`t$($definition.Health)`t$($zolAnimations -join "`t")")
            } else {
                if ($subid -ne 0) { throw "Room data uses unsupported ENEMY_GEL subid `$($Matches['subid'])." }
                $gelRows.Add("$($alias.Group)`t$($alias.Room)`t43`t00`t$($Matches['flags'])`t$count`tR`t-1`t-1`t$($gfxNames[$zolGfx])`t$($gelDefinition.TileBase)`t$($gelDefinition.Palette)`t$($gelDefinition.RadiusY)`t$($gelDefinition.RadiusX)`t$($gelDefinition.DamageQuarters)`t$($gelDefinition.Health)`t$($gelAnimations -join "`t")")
            }
        }
        continue
    }

    if ($line -match '^\s*obj_SpecificEnemyA\s+(?<values>(?:\$[0-9a-f]{2}\s*)+)$') {
        $values = @([regex]::Matches($Matches['values'], '\$(?<value>[0-9a-f]{2})') |
            ForEach-Object { $_.Groups['value'].Value })
        if ($values.Count -eq 5) {
            $zolLastSpecificFlags = $values[0]
            $id, $subidHex, $y, $x = $values[1..4]
        } else {
            $id, $subidHex, $y, $x = $values
        }
        if ($id -eq '34') {
            $subid = [Convert]::ToInt32($subidHex, 16)
            if (-not $zolDefinitions.ContainsKey($subid)) {
                throw "Room data uses unsupported fixed ENEMY_ZOL subid `$subidHex."
            }
            $definition = $zolDefinitions[$subid]
            foreach ($alias in $zolAliases) {
                $zolRows.Add("$($alias.Group)`t$($alias.Room)`t34`t$subidHex`t$zolLastSpecificFlags`t1`tF`t$y`t$x`t$($gfxNames[$zolGfx])`t$($definition.TileBase)`t$($definition.Palette)`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.DamageQuarters)`t$($definition.Health)`t$($zolAnimations -join "`t")")
            }
        } elseif ($id -eq '43') {
            if ($subidHex -ne '00') { throw "Room data uses unsupported fixed ENEMY_GEL subid `$subidHex." }
            foreach ($alias in $zolAliases) {
                $gelRows.Add("$($alias.Group)`t$($alias.Room)`t43`t00`t$zolLastSpecificFlags`t1`tF`t$y`t$x`t$($gfxNames[$zolGfx])`t$($gelDefinition.TileBase)`t$($gelDefinition.Palette)`t$($gelDefinition.RadiusY)`t$($gelDefinition.RadiusX)`t$($gelDefinition.DamageQuarters)`t$($gelDefinition.Health)`t$($gelAnimations -join "`t")")
            }
        }
        continue
    }

    if ($line -match '^\s*obj_EndPointer' -or $line -match '^[A-Za-z0-9_@]+:') {
        $zolAliases.Clear()
    }
}
$zolInstanceCount = ($zolRows | Select-Object -Skip 1 | ForEach-Object {
    [int](($_ -split "`t")[5])
} | Measure-Object -Sum).Sum
$gelInstanceCount = ($gelRows | Select-Object -Skip 1 | ForEach-Object {
    [int](($_ -split "`t")[5])
} | Measure-Object -Sum).Sum
if ($zolRows.Count -ne 62 -or $zolInstanceCount -ne 79) {
    throw "Expected 61 Zol room records / 79 instances, parsed $($zolRows.Count - 1) / $zolInstanceCount."
}
if ($gelRows.Count -ne 2 -or $gelInstanceCount -ne 3) {
    throw "Expected one direct Gel room record / 3 instances, parsed $($gelRows.Count - 1) / $gelInstanceCount."
}
if (($zolRows | Where-Object { $_ -match '^4\tcc\t34\t00\t00\t1\tF\t78\t58\t' }).Count -ne 1 -or
    ($zolRows | Where-Object { $_ -match '^4\tcc\t34\t01\t00\t1\tF\t98\t48\t' }).Count -ne 1) {
    throw 'Canonical room 4:cc green/red Zol records were not extracted.'
}

$zolSpriteName = $gfxNames[$zolGfx]
$zolSourceSprite = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$zolSpriteName.png" } |
    Select-Object -First 1
if ($null -eq $zolSourceSprite) { throw "Zol/Gel sprite not found: $zolSpriteName.png" }
Copy-Item -LiteralPath $zolSourceSprite.FullName -Destination (Join-Path $destination "gfx\$zolSpriteName.png") -Force
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\zols.tsv'), $zolRows, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\gels.tsv'), $gelRows, [Text.UTF8Encoding]::new($false))

# PART_ENEMY_DESTROYED (`$02) is the common enemy death puff. Export both
# animations: animation 0 is the ordinary 20-update puff, while animation 1
# inserts the 8-update high-knockback burst selected by bit 7 of the defeated
# enemy's knockback counter.
$partDataSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\partData.s")
$deathPuffData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$00 \$00 \$00 \$00 \$40 \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2}) \$00\s*; \$02'
)
if (-not $deathPuffData.Success) { throw "Could not resolve PART_ENEMY_DESTROYED (`$02) data." }
$deathPuffTileBase = [Convert]::ToInt32($deathPuffData.Groups['tile'].Value, 16)
$deathPuffOamFlags = [Convert]::ToInt32($deathPuffData.Groups['flags'].Value, 16)
if ($deathPuffTileBase -ne 0x0c -or $deathPuffOamFlags -ne 0x0a) {
    throw "PART_ENEMY_DESTROYED no longer resolves to tile base `$0c / OAM flags `$0a."
}

$partAnimationSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\partAnimations.s")
$partOamSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\partOamData.s")
$deathPuffAnimationLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $partAnimationSource 'part02Animations'),
        '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$deathPuffOamLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $partAnimationSource 'part02OamDataPointers'),
        '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($deathPuffAnimationLabels.Count -ne 2 -or $deathPuffOamLabels.Count -ne 7) {
    throw "Expected two death-puff animations and seven death-puff OAM pointers."
}

function Resolve-PartOam([string]$label) {
    $body = Get-AssemblyLabelBody $script:partOamSource $label
    $countMatch = [regex]::Match($body, '(?m)^\s*\.db\s+\$(?<count>[0-9a-f]{2})')
    if (-not $countMatch.Success) { throw "OAM count missing for $label." }
    $count = [Convert]::ToInt32($countMatch.Groups['count'].Value, 16)
    $parts = @(
        [regex]::Matches(
            $body,
            '(?m)^\s*\.db\s+\$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
        ) | ForEach-Object {
            "$([Convert]::ToInt32($_.Groups['y'].Value, 16)),$([Convert]::ToInt32($_.Groups['x'].Value, 16)),$([Convert]::ToInt32($_.Groups['tile'].Value, 16)),$([Convert]::ToInt32($_.Groups['flags'].Value, 16))"
        }
    )
    if ($parts.Count -ne $count) { throw "$label declares $count OAM parts but contains $($parts.Count)." }
    return $parts -join ';'
}

function Resolve-DeathPuffAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:partAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
        if ($parameter -ne 0) { break }
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:deathPuffOamLabels.Count) {
            throw "$label references missing OAM pointer byte offset $($frame.Groups['offset'].Value)."
        }
        $frames.Add("$duration@$(Resolve-PartOam $script:deathPuffOamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}

$deathPuffNormalAnimation = Resolve-DeathPuffAnimation $deathPuffAnimationLabels[0]
$deathPuffKnockbackAnimation = Resolve-DeathPuffAnimation $deathPuffAnimationLabels[1]
$deathPuffNormalDurations = @($deathPuffNormalAnimation.Split('|') | ForEach-Object { [int]($_.Split('@')[0]) })
$deathPuffKnockbackDurations = @($deathPuffKnockbackAnimation.Split('|') | ForEach-Object { [int]($_.Split('@')[0]) })
if ($deathPuffNormalDurations.Count -ne 7 -or
    ($deathPuffNormalDurations | Measure-Object -Sum).Sum -ne 20 -or
    $deathPuffKnockbackDurations.Count -ne 8 -or
    ($deathPuffKnockbackDurations | Measure-Object -Sum).Sum -ne 28 -or
    $deathPuffKnockbackDurations[3] -ne 8) {
    throw "PART_ENEMY_DESTROYED animations no longer match the 20/28-update records."
}

$deathPuffRows = @(
    "# tile-base`tpalette-a`tpalette-b`tnormal-animation`thigh-knockback-animation",
    "$deathPuffTileBase`t$($deathPuffOamFlags -band 7)`t$(($deathPuffOamFlags -bxor 1) -band 7)`t$deathPuffNormalAnimation`t$deathPuffKnockbackAnimation"
)
$deathPuffPath = Join-Path $destination "effects\enemy_death_puff.tsv"
New-Item -ItemType Directory -Force -Path (Split-Path $deathPuffPath -Parent) | Out-Null
[IO.File]::WriteAllLines($deathPuffPath, $deathPuffRows, [Text.UTF8Encoding]::new($false))

# INTERAC_KILLENEMYPUFF (`$08) is the non-dropping burst used when a red Zol
# splits. It is visually and semantically distinct from PART_ENEMY_DESTROYED.
$interactionDataSource = Get-Content -Raw (Join-Path $Disassembly 'data\ages\interactionData.s')
$killPuffData = [regex]::Match(
    $interactionDataSource,
    '(?m)^\s*/\* \$08 \*/ m_InteractionData \$(?<gfx>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
)
if (-not $killPuffData.Success -or
    [Convert]::ToInt32($killPuffData.Groups['gfx'].Value, 16) -ne 0 -or
    [Convert]::ToInt32($killPuffData.Groups['tile'].Value, 16) -ne 0x10 -or
    [Convert]::ToInt32($killPuffData.Groups['flags'].Value, 16) -ne 0xb0) {
    throw 'INTERAC_KILLENEMYPUFF no longer resolves to gfx `$00 / tile `$10 / flags `$b0.'
}
$interactionAnimationSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\interactionAnimations.s')
$interactionOamSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\interactionOamData.s')
$killPuffAnimationLabel = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $interactionAnimationSource 'interaction08Animations'),
        '(?m)^\s*\.dw\s+(?<label>interactionAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$killPuffOamLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $interactionAnimationSource 'interaction08OamDataPointers'),
        '(?m)^\s*\.dw\s+(?<label>interactionOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($killPuffAnimationLabel.Count -ne 1 -or $killPuffOamLabels.Count -ne 6) {
    throw "Expected one INTERAC_KILLENEMYPUFF animation and six OAM pointers, got $($killPuffAnimationLabel.Count) / $($killPuffOamLabels.Count)."
}
function Resolve-InteractionOam([string]$label) {
    $body = Get-AssemblyLabelBody $script:interactionOamSource $label
    $countMatch = [regex]::Match($body, '(?m)^\s*\.db\s+\$(?<count>[0-9a-f]{2})')
    if (-not $countMatch.Success) { throw "OAM count missing for $label." }
    $count = [Convert]::ToInt32($countMatch.Groups['count'].Value, 16)
    $parts = @(
        [regex]::Matches(
            $body,
            '(?m)^\s*\.db\s+\$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
        ) | ForEach-Object {
            "$([Convert]::ToInt32($_.Groups['y'].Value, 16)),$([Convert]::ToInt32($_.Groups['x'].Value, 16)),$([Convert]::ToInt32($_.Groups['tile'].Value, 16)),$([Convert]::ToInt32($_.Groups['flags'].Value, 16))"
        }
    )
    if ($parts.Count -ne $count) { throw "$label declares $count OAM parts but contains $($parts.Count)." }
    return $parts -join ';'
}
$killPuffFrames = [Collections.Generic.List[string]]::new()
foreach ($frame in [regex]::Matches(
    (Get-AssemblyLabelBody $interactionAnimationSource $killPuffAnimationLabel[0]),
    '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
)) {
    $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
    if (($parameter -band 0x80) -ne 0) { break }
    $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
    if ($pointerIndex -ge $killPuffOamLabels.Count) {
        throw "INTERAC_KILLENEMYPUFF references missing OAM pointer $pointerIndex."
    }
    $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
    $killPuffFrames.Add("$duration@$(Resolve-InteractionOam $killPuffOamLabels[$pointerIndex])")
}
$killPuffAnimation = $killPuffFrames -join '|'
$killPuffDuration = ($killPuffFrames | ForEach-Object {
    [int](($_ -split '@')[0])
} | Measure-Object -Sum).Sum
if ($killPuffFrames.Count -ne 7 -or $killPuffDuration -ne 20) {
    throw 'INTERAC_KILLENEMYPUFF no longer has its original 7-frame / 20-update animation.'
}
$killPuffRows = @(
    "# tile-base`tpalette`tanimation",
    "$([Convert]::ToInt32($killPuffData.Groups['tile'].Value, 16))`t$([Convert]::ToInt32($killPuffData.Groups['flags'].Value, 16) -band 7)`t$killPuffAnimation"
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'effects\kill_enemy_puff.tsv'),
    $killPuffRows,
    [Text.UTF8Encoding]::new($false))

# PART_OCTOROK_PROJECTILE (`$18) uses the Octorok sprite sheet with a
# directionless flying-rock cell. On a solid-tile or sword collision it
# switches to animation 3, reverses direction, and bounces for `$20 updates.
$octorokProjectileData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) \$(?<radius>[0-9a-f]{2}) \$(?<damage>[0-9a-f]{2}) \$40 \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2}) \$00\s*; \$18'
)
if (-not $octorokProjectileData.Success -or
    [Convert]::ToInt32($octorokProjectileData.Groups['gfx'].Value, 16) -ne 0x8f -or
    [Convert]::ToInt32($octorokProjectileData.Groups['collision'].Value, 16) -ne 0x87 -or
    [Convert]::ToInt32($octorokProjectileData.Groups['radius'].Value, 16) -ne 0x22 -or
    [Convert]::ToInt32($octorokProjectileData.Groups['damage'].Value, 16) -ne 0xfc) {
    throw 'PART_OCTOROK_PROJECTILE no longer matches gfx `$8f, collision `$07, radius 2x2, and half-heart damage.'
}
$part18AnimationStart = $partAnimationSource.IndexOf('part18Animations:', [StringComparison]::Ordinal)
$part13AnimationStart = $partAnimationSource.IndexOf('part13Animations:', [StringComparison]::Ordinal)
$part18AnimationLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring(
            $part18AnimationStart, $part13AnimationStart - $part18AnimationStart),
        '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$part18OamStart = $partAnimationSource.IndexOf('part18OamDataPointers:', [StringComparison]::Ordinal)
$part0eOamStart = $partAnimationSource.IndexOf('part0eOamDataPointers:', [StringComparison]::Ordinal)
$part18OamLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring($part18OamStart, $part0eOamStart - $part18OamStart),
        '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($part18AnimationLabels.Count -ne 5 -or $part18OamLabels.Count -ne 6) {
    throw 'PART_OCTOROK_PROJECTILE animation/OAM pointer tables are incomplete.'
}
function Resolve-OctorokProjectileAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:partAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:part18OamLabels.Count) {
            throw "$label references missing Octorok-projectile OAM pointer $pointerIndex."
        }
        $frames.Add("$duration@$(Resolve-PartOam $script:part18OamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}
$octorokProjectileNormal = Resolve-OctorokProjectileAnimation $part18AnimationLabels[0]
$octorokProjectileBounce = Resolve-OctorokProjectileAnimation $part18AnimationLabels[3]
if ($octorokProjectileNormal -ne '127@8,0,0,0;8,8,0,32' -or
    $octorokProjectileBounce -ne '127@8,0,2,0;8,8,2,32') {
    throw 'PART_OCTOROK_PROJECTILE flying/bounced visuals changed from the expected OAM records.'
}
$octorokProjectileTileBase = [Convert]::ToInt32(
    $octorokProjectileData.Groups['tile'].Value, 16)
$octorokProjectilePalette = [Convert]::ToInt32(
    $octorokProjectileData.Groups['flags'].Value, 16) -band 7
$octorokProjectileRows = @(
    "# sprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`tspeed-raw`tnormal-animation`tbounce-animation",
    "$($gfxNames[0x8f])`t$octorokProjectileTileBase`t$octorokProjectilePalette`t2`t2`t2`t80`t$octorokProjectileNormal`t$octorokProjectileBounce"
)
$octorokProjectilePath = Join-Path $destination 'effects\octorok_projectile.tsv'
[IO.File]::WriteAllLines(
    $octorokProjectilePath, $octorokProjectileRows, [Text.UTF8Encoding]::new($false))

# Preserve the complete Ages enemy item-drop selection data used by
# decideItemDrop. The fixed binary layout is 144 enemy records, eight 8-byte
# probability masks, and sixteen 32-byte item sets (720 bytes total).
$treasureDropSource = Get-Content -Raw (Join-Path $Disassembly 'code\treasureAndDrops.s')
function Get-HexBytes([string]$body) {
    $result = [Collections.Generic.List[byte]]::new()
    foreach ($dataLine in [regex]::Matches($body, '(?m)^\s*\.db\s+(?<values>[^;\r\n]+)')) {
        foreach ($value in [regex]::Matches(
            $dataLine.Groups['values'].Value, '\$(?<value>[0-9a-fA-F]{2})')) {
            $result.Add([Convert]::ToByte($value.Groups['value'].Value, 16))
        }
    }
    return $result.ToArray()
}

$itemDropTableMatch = [regex]::Match(
    $treasureDropSource,
    '(?ms)^itemDropTables:\r?\n\.ifdef ROM_AGES\r?\n(?<body>.*?)(?=^\.else)'
)
if (-not $itemDropTableMatch.Success) { throw 'Ages itemDropTables block was not found.' }
$itemDropEnemyTable = @(Get-HexBytes $itemDropTableMatch.Groups['body'].Value)
if ($itemDropEnemyTable.Count -ne 144 -or
    $itemDropEnemyTable[0x09] -ne 0x8e -or
    $itemDropEnemyTable[0x32] -ne 0xae) {
    throw "Expected 144 Ages enemy item-drop records with ENEMY_OCTOROK (`$09) = `$8e and ENEMY_KEESE (`$32) = `$ae."
}

$itemDropProbabilityBytes = [Collections.Generic.List[byte]]::new()
foreach ($probability in 0..7) {
    # Probability 0 deliberately aliases probability 1 in the original table.
    $sourceProbability = if ($probability -eq 0) { 1 } else { $probability }
    $label = "@probability${sourceProbability}:"
    $start = $treasureDropSource.IndexOf($label, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Item-drop probability label not found: $label" }
    $endLabel = if ($sourceProbability -lt 7) {
        "@probability$($sourceProbability + 1):"
    } else {
        '.ifdef ROM_SEASONS'
    }
    $end = $treasureDropSource.IndexOf(
        $endLabel, $start + $label.Length, [StringComparison]::Ordinal)
    if ($end -lt 0) { throw "End of item-drop probability $sourceProbability was not found." }
    $bytes = @(Get-HexBytes $treasureDropSource.Substring($start, $end - $start))
    if ($bytes.Count -ne 8) {
        throw "Item-drop probability $sourceProbability contains $($bytes.Count) bytes; expected 8."
    }
    foreach ($value in $bytes) { $itemDropProbabilityBytes.Add($value) }
}

$itemDropSetBytes = [Collections.Generic.List[byte]]::new()
foreach ($setIndex in 0..15) {
    $setLabel = "itemDropSet$($setIndex.ToString('X'))"
    $bytes = @(Get-HexBytes (Get-AssemblyLabelBody $treasureDropSource $setLabel))
    if ($bytes.Count -ne 32) {
        throw "$setLabel contains $($bytes.Count) bytes; expected 32."
    }
    foreach ($value in $bytes) { $itemDropSetBytes.Add($value) }
}

$itemDropSelectionBytes = [Collections.Generic.List[byte]]::new()
foreach ($value in $itemDropEnemyTable) { $itemDropSelectionBytes.Add($value) }
foreach ($value in $itemDropProbabilityBytes) { $itemDropSelectionBytes.Add($value) }
foreach ($value in $itemDropSetBytes) { $itemDropSelectionBytes.Add($value) }
if ($itemDropSelectionBytes.Count -ne 720) {
    throw "Generated item-drop selection data is $($itemDropSelectionBytes.Count) bytes; expected 720."
}
$itemDropSelectionPath = Join-Path $destination 'metadata\itemDrops.bin'
[IO.File]::WriteAllBytes($itemDropSelectionPath, $itemDropSelectionBytes.ToArray())

# Export the PART_ITEM_DROP (`$01) visual records. Its subid selects one of the
# sixteen sprite-data rows and one of the first sixteen part animations.
$itemDropPartData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) \$(?<radius>[0-9a-f]{2}) \$00 \$01 \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2}) \$00\s*; \$01'
)
if (-not $itemDropPartData.Success -or
    [Convert]::ToInt32($itemDropPartData.Groups['gfx'].Value, 16) -ne 0x78 -or
    [Convert]::ToInt32($itemDropPartData.Groups['collision'].Value, 16) -ne 0x01 -or
    [Convert]::ToInt32($itemDropPartData.Groups['radius'].Value, 16) -ne 0x44) {
    throw 'PART_ITEM_DROP no longer resolves to gfx `$78, collision `$01, and radius `$44.'
}
$itemDropBaseTile = [Convert]::ToInt32($itemDropPartData.Groups['tile'].Value, 16)
$itemDropCodeSource = Get-Content -Raw (Join-Path $Disassembly 'object_code\common\parts\itemDrop.s')
$itemDropSpriteBlock = [regex]::Match(
    $itemDropCodeSource,
    '(?ms)^@spriteData:\r?\n(?<body>.*?)(?=^;;)'
)
$itemDropSpriteRows = @(
    [regex]::Matches(
        $itemDropSpriteBlock.Groups['body'].Value,
        '(?m)^\s*\.db \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
    )
)
if ($itemDropSpriteRows.Count -ne 16) {
    throw "PART_ITEM_DROP spriteData contains $($itemDropSpriteRows.Count) rows; expected 16."
}

$part01AnimationStart = $partAnimationSource.IndexOf('part01Animations:', [StringComparison]::Ordinal)
$part02AnimationStart = $partAnimationSource.IndexOf('part02Animations:', [StringComparison]::Ordinal)
$part01AnimationLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring(
            $part01AnimationStart, $part02AnimationStart - $part01AnimationStart),
        '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$part01OamStart = $partAnimationSource.IndexOf('part01OamDataPointers:', [StringComparison]::Ordinal)
$part02OamStart = $partAnimationSource.IndexOf('part02OamDataPointers:', [StringComparison]::Ordinal)
$part01OamLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring($part01OamStart, $part02OamStart - $part01OamStart),
        '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($part01AnimationLabels.Count -lt 16 -or $part01OamLabels.Count -ne 4) {
    throw 'PART_ITEM_DROP animation/OAM pointer tables are incomplete.'
}

function Resolve-ItemDropAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:partAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
        if ($parameter -ne 0) { break }
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:part01OamLabels.Count) {
            throw "$label references missing PART_ITEM_DROP OAM pointer $pointerIndex."
        }
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $frames.Add("$duration@$(Resolve-PartOam $script:part01OamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}

$itemDropVisualRows = [Collections.Generic.List[string]]::new()
$itemDropVisualRows.Add("# subid`ttile-base`tpalette`tanimation")
foreach ($subid in 0..15) {
    $spriteRow = $itemDropSpriteRows[$subid]
    $tileBase = $itemDropBaseTile +
        [Convert]::ToInt32($spriteRow.Groups['tile'].Value, 16)
    $palette = [Convert]::ToInt32($spriteRow.Groups['flags'].Value, 16) -band 7
    $animation = Resolve-ItemDropAnimation $part01AnimationLabels[$subid]
    if (-not $animation) { throw "PART_ITEM_DROP subid `$($subid.ToString('x2')) has no animation." }
    $itemDropVisualRows.Add("$subid`t$tileBase`t$palette`t$animation")
}
if ($itemDropVisualRows[2] -ne "1`t2`t5`t127@11,4,0,0" -or
    $itemDropVisualRows[3] -ne "2`t4`t0`t127@8,4,0,0" -or
    $itemDropVisualRows[4] -ne "3`t6`t5`t127@8,4,0,0") {
    throw 'Heart and rupee PART_ITEM_DROP visual records no longer match the original data.'
}
$itemDropVisualPath = Join-Path $destination 'effects\item_drops.tsv'
[IO.File]::WriteAllLines(
    $itemDropVisualPath, $itemDropVisualRows, [Text.UTF8Encoding]::new($false))

# Resolve warp source indices to their destination records. A source position
# of '*' is a standard whole-room tile warp; nonzero edge masks are the four
# screen corners described by m_StandardWarp's first parameter.
$warpDestinationLines = Get-Content (Join-Path $Disassembly "data\ages\warpDestinations.s")
$warpDestinations = @{}
$currentWarpGroup = -1
foreach ($line in $warpDestinationLines) {
    if ($line -match '^group(?<group>[0-7])WarpDestTable:') {
        $currentWarpGroup = [int]$Matches['group']
        $warpDestinations[$currentWarpGroup] = [Collections.Generic.List[object]]::new()
        continue
    }
    if ($currentWarpGroup -ge 0 -and $line -match 'm_WarpDest\s+\$(?<room>[0-9a-f]{2})\s+\$(?<position>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f])\s+\$(?<type>[0-9a-f])') {
        $warpDestinations[$currentWarpGroup].Add(@{
            Room = [Convert]::ToInt32($Matches['room'], 16)
            Position = [Convert]::ToInt32($Matches['position'], 16)
            Parameter = [Convert]::ToInt32($Matches['parameter'], 16)
            Type = [Convert]::ToInt32($Matches['type'], 16)
        })
    }
}

$warpSourceLines = Get-Content (Join-Path $Disassembly "data\ages\warpSources.s")
$pointerWarpOwners = @{}
$currentWarpGroup = -1
foreach ($line in $warpSourceLines) {
    if ($line -match '^group(?<group>[0-7])WarpSources:') {
        $currentWarpGroup = [int]$Matches['group']
        continue
    }
    if ($currentWarpGroup -ge 0 -and $line -match 'm_PointerWarp\s+\$(?<room>[0-9a-f]{2})\s+(?<label>warpSource[0-9a-f]+)') {
        $pointerWarpOwners[$Matches['label']] = @{
            Group = $currentWarpGroup
            Room = [Convert]::ToInt32($Matches['room'], 16)
        }
    }
}

$warpRows = [Collections.Generic.List[string]]::new()
$warpRows.Add("# source-group`tsource-room`tsource-position`tedge-mask`tsource-transition`tdest-group`tdest-room`tdest-position`tdest-parameter`tdest-transition")
function Add-ResolvedWarp(
    [int]$sourceGroup, [int]$sourceRoom, [string]$sourcePosition,
    [int]$edgeMask, [int]$sourceTransition, [int]$destGroup, [int]$destIndex) {
    if (-not $script:warpDestinations.ContainsKey($destGroup) -or
        $destIndex -ge $script:warpDestinations[$destGroup].Count) {
        throw "Warp destination $destGroup/$($destIndex.ToString('x2')) is not defined."
    }
    $dest = $script:warpDestinations[$destGroup][$destIndex]
    $script:warpRows.Add(
        "$sourceGroup`t$($sourceRoom.ToString('x2'))`t$sourcePosition`t$edgeMask`t$sourceTransition`t$destGroup`t$($dest.Room.ToString('x2'))`t$($dest.Position.ToString('x2'))`t$($dest.Parameter)`t$($dest.Type)")
}

$currentWarpGroup = -1
$currentPointerLabel = ''
foreach ($line in $warpSourceLines) {
    if ($line -match '^group(?<group>[0-7])WarpSources:') {
        $currentWarpGroup = [int]$Matches['group']
        $currentPointerLabel = ''
        continue
    }
    if ($line -match '^(?<label>warpSource[0-9a-f]+):') {
        $currentPointerLabel = $Matches['label']
        continue
    }
    if ($currentWarpGroup -ge 0 -and $currentPointerLabel -eq '' -and
        $line -match 'm_StandardWarp\s+\$(?<edge>[0-9a-f])\s+\$(?<room>[0-9a-f]{2})\s+\$(?<dest>[0-9a-f]{2})\s+\$(?<group>[0-9a-f])\s+\$(?<transition>[0-9a-f])') {
        Add-ResolvedWarp $currentWarpGroup ([Convert]::ToInt32($Matches['room'], 16)) '*' ([Convert]::ToInt32($Matches['edge'], 16)) ([Convert]::ToInt32($Matches['transition'], 16)) ([Convert]::ToInt32($Matches['group'], 16)) ([Convert]::ToInt32($Matches['dest'], 16))
        continue
    }
    if ($currentPointerLabel -ne '' -and $pointerWarpOwners.ContainsKey($currentPointerLabel) -and
        $line -match 'm_PositionWarp\s+\$(?<position>[0-9a-f]{2})\s+\$(?<dest>[0-9a-f]{2})\s+\$(?<group>[0-9a-f])\s+\$(?<transition>[0-9a-f])') {
        $owner = $pointerWarpOwners[$currentPointerLabel]
        Add-ResolvedWarp $owner.Group $owner.Room $Matches['position'] 0 ([Convert]::ToInt32($Matches['transition'], 16)) ([Convert]::ToInt32($Matches['group'], 16)) ([Convert]::ToInt32($Matches['dest'], 16))
    }
}
if ($warpRows.Count -ne 530) {
    throw "Expected 529 resolved warp records, parsed $($warpRows.Count - 1)."
}
$warpPath = Join-Path $destination "objects\warps.tsv"
[IO.File]::WriteAllLines($warpPath, $warpRows, [Text.UTF8Encoding]::new($false))

# Dungeon rooms occupy arbitrary cells in one or more 8x8 floor maps. Screen
# transitions use these cells rather than the overworld's hexadecimal room
# coordinates (for example, dungeon00 room $03 is directly above room $04).
$dungeonLayoutSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\dungeonLayouts.s")
$dungeonBlocks = [regex]::Matches(
    $dungeonLayoutSource,
    '(?ms)^dungeon(?<index>[0-9a-f]{2})Layout:\s*(?<body>.*?)(?=^dungeon[0-9a-f]{2}Layout:|\z)')
$dungeonRows = [Collections.Generic.List[string]]::new()
$dungeonRows.Add("# dungeon`troom`tdirection`tneighbor")
foreach ($block in $dungeonBlocks) {
    $dungeon = [Convert]::ToInt32($block.Groups['index'].Value, 16)
    $cells = [Collections.Generic.List[int]]::new()
    foreach ($dataLine in [regex]::Matches($block.Groups['body'].Value, '(?m)^\s*\.db\s+(?<values>[^;\r\n]+)')) {
        foreach ($value in [regex]::Matches($dataLine.Groups['values'].Value, '\$(?<value>[0-9a-f]{2})')) {
            $cells.Add([Convert]::ToInt32($value.Groups['value'].Value, 16))
        }
    }
    if (($cells.Count % 64) -ne 0) {
        throw "Dungeon $($dungeon.ToString('x2')) layout has $($cells.Count) cells; expected complete 8x8 floors."
    }
    for ($cell = 0; $cell -lt $cells.Count; $cell++) {
        $room = $cells[$cell]
        if ($room -eq 0) { continue }
        $floorCell = $cell % 64
        $x = $floorCell % 8
        $y = [Math]::Floor($floorCell / 8)
        foreach ($edge in @(
            @{ Name = 'up'; Dx = 0; Dy = -1 },
            @{ Name = 'right'; Dx = 1; Dy = 0 },
            @{ Name = 'down'; Dx = 0; Dy = 1 },
            @{ Name = 'left'; Dx = -1; Dy = 0 })) {
            $nx = $x + $edge.Dx
            $ny = $y + $edge.Dy
            if ($nx -lt 0 -or $nx -ge 8 -or $ny -lt 0 -or $ny -ge 8) { continue }
            $neighbor = $cells[$cell - $floorCell + $ny * 8 + $nx]
            if ($neighbor -eq 0) { continue }
            $dungeonRows.Add(
                "$dungeon`t$($room.ToString('x2'))`t$($edge.Name)`t$($neighbor.ToString('x2'))")
        }
    }
}
$dungeonPath = Join-Path $destination "objects\dungeon_adjacency.tsv"
[IO.File]::WriteAllLines($dungeonPath, $dungeonRows, [Text.UTF8Encoding]::new($false))

# The full dungeon map screen needs floor/cell positions and the original room
# property bits, not only the neighbor pairs used by screen transitions.
$dungeonDataSource = Get-Content -Raw (Join-Path $Disassembly 'data\ages\dungeonData.s')
$dungeonMetadata = @{}
foreach ($record in [regex]::Matches(
    $dungeonDataSource,
    '(?ms)^dungeonData(?<index>[0-9a-f]{2}):\s*\r?\n\s*m_DungeonData\s+>wGroup(?<group>[45])RoomFlags,\s*\$[0-9a-f]{2},\s*dungeon[0-9a-f]{2}Layout,\s*\$(?<floors>[0-9a-f]{2}),\s*\$(?<base>[0-9a-f]{2})')) {
    $index = [Convert]::ToInt32($record.Groups['index'].Value, 16)
    $dungeonMetadata[$index] = @{
        Group = [Convert]::ToInt32($record.Groups['group'].Value, 16)
        Floors = [Convert]::ToInt32($record.Groups['floors'].Value, 16)
        BaseFloor = [Convert]::ToInt32($record.Groups['base'].Value, 16)
    }
}
if ($dungeonMetadata.Count -ne 16) {
    throw "Expected 16 dungeon metadata records, parsed $($dungeonMetadata.Count)."
}
$dungeonProperties = @{
    4 = [IO.File]::ReadAllBytes((Join-Path $Disassembly 'rooms\ages\group4DungeonProperties.bin'))
    5 = [IO.File]::ReadAllBytes((Join-Path $Disassembly 'rooms\ages\group5DungeonProperties.bin'))
}
$dungeonMapRows = [Collections.Generic.List[string]]::new()
$dungeonMapRows.Add('# dungeon`tgroup`tfloors`tbase-floor`tfloor`tx`ty`troom`tproperties')
foreach ($block in $dungeonBlocks) {
    $dungeon = [Convert]::ToInt32($block.Groups['index'].Value, 16)
    $metadataRecord = $dungeonMetadata[$dungeon]
    $cells = [Collections.Generic.List[int]]::new()
    foreach ($dataLine in [regex]::Matches($block.Groups['body'].Value, '(?m)^\s*\.db\s+(?<values>[^;\r\n]+)')) {
        foreach ($value in [regex]::Matches($dataLine.Groups['values'].Value, '\$(?<value>[0-9a-f]{2})')) {
            $cells.Add([Convert]::ToInt32($value.Groups['value'].Value, 16))
        }
    }
    if ($cells.Count -ne $metadataRecord.Floors * 64) {
        throw "Dungeon $($dungeon.ToString('x2')) has $($cells.Count) layout cells; expected $($metadataRecord.Floors * 64)."
    }
    for ($cell = 0; $cell -lt $cells.Count; $cell++) {
        $room = $cells[$cell]
        if ($room -eq 0) { continue }
        $floorCell = $cell % 64
        $properties = $dungeonProperties[$metadataRecord.Group][$room]
        $dungeonMapRows.Add(
            "$dungeon`t$($metadataRecord.Group)`t$($metadataRecord.Floors)`t$($metadataRecord.BaseFloor)`t$([Math]::Floor($cell / 64))`t$($floorCell % 8)`t$([Math]::Floor($floorCell / 8))`t$($room.ToString('x2'))`t$($properties.ToString('x2'))")
    }
}
$dungeonMapPath = Join-Path $destination 'objects\dungeon_maps.tsv'
[IO.File]::WriteAllLines($dungeonMapPath, $dungeonMapRows, [Text.UTF8Encoding]::new($false))

# Expand the animation engine's three linked tables into address-independent
# records. Destinations are converted from VRAM addresses to the same signed
# tile indices used by OracleRoomData's 128x128 tileset source images.
$animationHeaderSource = Get-Content (Join-Path $Disassembly "data\ages\animationGfxHeaders.s")
$animationHeaderRows = [Collections.Generic.List[string]]::new()
$animationHeaderRows.Add("# index`tsheet`tdestination-tile`ttile-count`tsource-tile")
$animationHeaderIndex = 0
foreach ($line in $animationHeaderSource) {
    if ($line -notmatch 'm_GfxHeaderAnim\s+gfx_animations_(?<sheet>[1-3]),\s*\$(?<destination>[0-9a-f]{4}),\s*\$(?<count>[0-9a-f]{2}),\s*\$(?<source>[0-9a-f]{3})') {
        continue
    }
    $destinationAddress = [Convert]::ToInt32($Matches['destination'], 16) -band 0xfff0
    if ($destinationAddress -ge 0x9000) {
        $destinationTile = 128 + (($destinationAddress - 0x9000) / 16)
    } else {
        $destinationTile = ($destinationAddress - 0x8800) / 16
    }
    $sourceTile = [Convert]::ToInt32($Matches['source'], 16) / 16
    $animationHeaderRows.Add(
        "$animationHeaderIndex`t$($Matches['sheet'])`t$destinationTile`t$([Convert]::ToInt32($Matches['count'], 16))`t$sourceTile")
    $animationHeaderIndex++
}
if ($animationHeaderIndex -ne 112) {
    throw "Expected 112 animation graphics headers, parsed $animationHeaderIndex."
}

$animationDataSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\animationData.s")
$animationSequences = @{}
$animationBlocks = [regex]::Matches(
    $animationDataSource,
    '(?ms)^(?<label>animationData[A-Za-z0-9]+):[^\r\n]*\r?\n(?<body>.*?)(?=^animationData[A-Za-z0-9]+:|\z)'
)
foreach ($block in $animationBlocks) {
    $frames = [regex]::Matches($block.Groups['body'].Value, '\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<gfx>[0-9a-f]{2})')
    if ($frames.Count -eq 0) { continue }
    $sequence = $frames | ForEach-Object {
        "$([Convert]::ToInt32($_.Groups['duration'].Value, 16)):$([Convert]::ToInt32($_.Groups['gfx'].Value, 16))"
    }
    $animationSequences[$block.Groups['label'].Value] = $sequence -join ','
}

$animationGroupLines = Get-Content (Join-Path $Disassembly "data\ages\animationGroups.s")
$animationGroups = @{}
$pendingGroups = [Collections.Generic.List[int]]::new()
$currentTracks = [Collections.Generic.List[string]]::new()
$readingGroupBody = $false
function Complete-AnimationGroup {
    if (-not $script:readingGroupBody) { return }
    foreach ($groupId in $script:pendingGroups) {
        $script:animationGroups[$groupId] = @($script:currentTracks)
    }
    $script:pendingGroups = [Collections.Generic.List[int]]::new()
    $script:currentTracks = [Collections.Generic.List[string]]::new()
    $script:readingGroupBody = $false
}
foreach ($line in $animationGroupLines) {
    if ($line -match '^animationGroup(?<id>[0-9a-f]{2}):') {
        if ($readingGroupBody) { Complete-AnimationGroup }
        $pendingGroups.Add([Convert]::ToInt32($Matches['id'], 16))
        continue
    }
    if ($pendingGroups.Count -gt 0 -and $line -match '\.db\s+\$8[0-9a-f]') {
        $readingGroupBody = $true
        continue
    }
    if ($readingGroupBody -and $line -match '\.dw\s+(?<label>animationData[A-Za-z0-9]+)') {
        $currentTracks.Add($Matches['label'])
    }
}
Complete-AnimationGroup

$animationTrackRows = [Collections.Generic.List[string]]::new()
$animationTrackRows.Add("# group`ttrack`tframes(duration:gfx-index)")
foreach ($groupId in 0..21) {
    if (-not $animationGroups.ContainsKey($groupId)) {
        throw "Animation group $($groupId.ToString('x2')) was not resolved."
    }
    $track = 0
    foreach ($label in $animationGroups[$groupId]) {
        if (-not $animationSequences.ContainsKey($label)) {
            throw "Animation sequence not found: $label"
        }
        $animationTrackRows.Add("$groupId`t$track`t$($animationSequences[$label])")
        $track++
    }
}

$animationDestination = Join-Path $destination "animations"
New-Item -ItemType Directory -Force -Path $animationDestination | Out-Null
[IO.File]::WriteAllLines(
    (Join-Path $animationDestination "headers.tsv"), $animationHeaderRows, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllLines(
    (Join-Path $animationDestination "tracks.tsv"), $animationTrackRows, [Text.UTF8Encoding]::new($false))

# Room files are already expanded and address-independent. Preserve their
# group-prefixed names so metadata can select the correct layout group.
foreach ($kind in 'small', 'large') {
    $roomSource = Join-Path $Disassembly "rooms\ages\$kind"
    $roomDestination = Join-Path $destination "rooms\$kind"
    New-Item -ItemType Directory -Force -Path $roomDestination | Out-Null
    Copy-Item -Path (Join-Path $roomSource '*.bin') -Destination $roomDestination -Force
}

$importedRoomCount = (Get-ChildItem (Join-Path $destination 'rooms') -Recurse -File -Filter '*.bin').Count
if ($importedRoomCount -ne 1536) {
    throw "Expected 1536 expanded room layouts, imported $importedRoomCount."
}

Write-Host "Validated clean US ROM: $hash"
Write-Host "Imported $($tilesets.Count) tilesets, 1536 rooms, 42 signs, $($npcRows.Count - 1) NPCs, $keeseInstanceCount Keese, $octorokInstanceCount Octoroks, $zolInstanceCount Zols, $gelInstanceCount direct Gels, 133 chests, 529 warps, and 22 animation groups into $destination"
