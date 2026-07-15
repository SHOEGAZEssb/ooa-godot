# Title and file-select screens use the same split VRAM layout as the original
# GFXH_TITLESCREEN / GFXH_FILE_MENU_* headers. Preserve each source piece at
# its header destination instead of baking a replacement menu image.
foreach ($menuAsset in @(
    @{ Source = 'gfx_compressible\ages\gfx_titlescreen_1.png'; Destination = 'menu\gfx_titlescreen_1.png' },
    @{ Source = 'gfx_compressible\common\gfx_titlescreen_2.png'; Destination = 'menu\gfx_titlescreen_2.png' },
    @{ Source = 'gfx_compressible\common\gfx_titlescreen_3.png'; Destination = 'menu\gfx_titlescreen_3.png' },
    @{ Source = 'gfx_compressible\common\gfx_titlescreen_4.png'; Destination = 'menu\gfx_titlescreen_4.png' },
    @{ Source = 'gfx_compressible\common\gfx_titlescreen_5.png'; Destination = 'menu\gfx_titlescreen_5.png' },
    @{ Source = 'gfx_compressible\common\gfx_titlescreen_6.png'; Destination = 'menu\gfx_titlescreen_6.png' },
    @{ Source = 'gfx_compressible\common\spr_titlescreen_sprites.png'; Destination = 'menu\spr_titlescreen_sprites.png' },
    @{ Source = 'gfx_compressible\ages\map_titlescreen.bin'; Destination = 'menu\map_titlescreen.bin' },
    @{ Source = 'gfx_compressible\ages\flg_titlescreen.bin'; Destination = 'menu\flags_titlescreen.bin' },
    @{ Source = 'gfx_compressible\common\gfx_fileselect.png'; Destination = 'menu\gfx_fileselect.png' },
    @{ Source = 'gfx_compressible\common\gfx_messagespeed.png'; Destination = 'menu\gfx_messagespeed.png' },
    @{ Source = 'gfx_compressible\common\gfx_pickafile_2.png'; Destination = 'menu\gfx_pickafile_2.png' },
    @{ Source = 'gfx_compressible\common\gfx_pickafile.png'; Destination = 'menu\gfx_pickafile.png' },
    @{ Source = 'gfx_compressible\common\gfx_copywhatwhere.png'; Destination = 'menu\gfx_copywhatwhere.png' },
    @{ Source = 'gfx_compressible\common\gfx_quit_2.png'; Destination = 'menu\gfx_quit_2.png' },
    @{ Source = 'gfx_compressible\common\gfx_newfilescreen.png'; Destination = 'menu\gfx_newfilescreen.png' },
    @{ Source = 'gfx_compressible\common\gfx_name.png'; Destination = 'menu\gfx_name.png' },
    @{ Source = 'gfx_compressible\common\gfx_copy.png'; Destination = 'menu\gfx_copy.png' },
    @{ Source = 'gfx_compressible\common\gfx_erase.png'; Destination = 'menu\gfx_erase.png' },
    @{ Source = 'gfx_compressible\common\gfx_savescreen.png'; Destination = 'menu\gfx_savescreen.png' },
    @{ Source = 'gfx_compressible\common\spr_fileselect_decorations.png'; Destination = 'menu\spr_fileselect_decorations.png' },
    @{ Source = 'gfx_compressible\ages\spr_nayru_1.png'; Destination = 'menu\spr_nayru_1.png' },
    @{ Source = 'gfx_compressible\common\map_file_menu_top.bin'; Destination = 'menu\map_file_menu_top.bin' },
    @{ Source = 'gfx_compressible\common\flg_file_menu_top.bin'; Destination = 'menu\flags_file_menu_top.bin' },
    @{ Source = 'gfx_compressible\common\map_file_menu_middle.bin'; Destination = 'menu\map_file_menu_middle.bin' },
    @{ Source = 'gfx_compressible\common\flg_file_menu_middle.bin'; Destination = 'menu\flags_file_menu_middle.bin' },
    @{ Source = 'gfx_compressible\common\map_file_menu_bottom.bin'; Destination = 'menu\map_file_menu_bottom.bin' },
    @{ Source = 'gfx_compressible\common\flg_file_menu_bottom.bin'; Destination = 'menu\flags_file_menu_bottom.bin' },
    @{ Source = 'gfx_compressible\common\map_file_menu_copy.bin'; Destination = 'menu\map_file_menu_copy.bin' },
    @{ Source = 'gfx_compressible\common\flg_file_menu_copy.bin'; Destination = 'menu\flags_file_menu_copy.bin' },
    @{ Source = 'gfx_compressible\common\map_save_menu_middle.bin'; Destination = 'menu\map_save_menu_middle.bin' },
    @{ Source = 'gfx_compressible\common\flg_save_menu_middle.bin'; Destination = 'menu\flags_save_menu_middle.bin' },
    @{ Source = 'gfx_compressible\common\map_save_menu_bottom.bin'; Destination = 'menu\map_save_menu_bottom.bin' },
    @{ Source = 'gfx_compressible\common\flg_save_menu_bottom.bin'; Destination = 'menu\flags_save_menu_bottom.bin' },
    @{ Source = 'gfx_compressible\common\map_file_menu_message_speed.bin'; Destination = 'menu\map_file_menu_message_speed.bin' },
    @{ Source = 'gfx_compressible\common\flg_file_menu_message_speed.bin'; Destination = 'menu\flags_file_menu_message_speed.bin' },
    @{ Source = 'gfx_compressible\common\map_name_entry_top.bin'; Destination = 'menu\map_name_entry_top.bin' },
    @{ Source = 'gfx_compressible\common\flg_name_entry_top.bin'; Destination = 'menu\flags_name_entry_top.bin' },
    @{ Source = 'gfx_compressible\common\map_name_entry_middle.bin'; Destination = 'menu\map_name_entry_middle.bin' },
    @{ Source = 'gfx_compressible\common\flg_name_entry_middle.bin'; Destination = 'menu\flags_name_entry_middle.bin' },
    @{ Source = 'gfx_compressible\common\map_name_entry_bottom.bin'; Destination = 'menu\map_name_entry_bottom.bin' },
    @{ Source = 'gfx_compressible\common\flg_name_entry_bottom.bin'; Destination = 'menu\flags_name_entry_bottom.bin' })) {
    Copy-GeneratedFile $menuAsset.Source $menuAsset.Destination
}
Copy-GeneratedFile "gfx_compressible\ages\gfx_inventory_hud_1.png" "inventory\gfx_inventory_hud_1.png"
Copy-GeneratedFile "gfx_compressible\ages\spr_present_past_symbols.png" "inventory\spr_present_past_symbols.png"
Copy-GeneratedFile "gfx_compressible\ages\gfx_inventory_hud_2.png" "inventory\gfx_inventory_hud_2.png"
Copy-GeneratedFile "gfx_compressible\common\spr_quest_items_5.png" "inventory\spr_quest_items_5.png"
Copy-GeneratedFile "gfx_compressible\ages\spr_map_compass_keys_bookofseals.png" "inventory\spr_map_compass_keys_bookofseals.png"
Copy-GeneratedFile "gfx_compressible\common\gfx_save.png" "inventory\gfx_save.png"
Copy-GeneratedFile "gfx_compressible\common\gfx_blank.png" "inventory\gfx_blank.png"
Copy-GeneratedFile "gfx_compressible\common\gfx_rings.png" "inventory\gfx_rings.png"
Copy-GeneratedFile "gfx_compressible\ages\spr_essences.png" "inventory\spr_essences.png"
foreach ($questSheet in 1..4) {
    Copy-GeneratedFile "gfx_compressible\ages\spr_quest_items_${questSheet}.png" "inventory\spr_quest_items_${questSheet}.png"
}
Copy-GeneratedFile "gfx\common\map_rings.bin" "inventory\map_rings.bin"
Copy-GeneratedFile "gfx_compressible\common\map_inventory_screen_1.bin" "inventory\map_inventory_screen_1.bin"
Copy-GeneratedFile "gfx_compressible\common\flg_inventory_screen_1.bin" "inventory\flg_inventory_screen_1.bin"
Copy-GeneratedFile "gfx_compressible\common\map_inventory_screen_2.bin" "inventory\map_inventory_screen_2.bin"
Copy-GeneratedFile "gfx_compressible\common\flg_inventory_screen_2.bin" "inventory\flg_inventory_screen_2.bin"
Copy-GeneratedFile "gfx_compressible\ages\map_inventory_screen_3.bin" "inventory\map_inventory_screen_3.bin"
Copy-GeneratedFile "gfx_compressible\ages\flg_inventory_screen_3.bin" "inventory\flg_inventory_screen_3.bin"
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

# PALH_03 supplies all title palettes. PALH_05 supplies file-select BG palette
# 0, BG palettes 2-6, standard sprite palettes 0-3, and sprite palettes 4-6.
Export-PaletteBlock 'paletteData4018' 32 'menu\palette_title_bg.bin'
Export-PaletteBlock 'paletteData4058' 32 'menu\palette_title_sprites.bin'
$fileMenuBgPalette = [byte[]]::new(8 * 4 * 3)
$fileMenuBg0 = Read-PaletteBytes 'paletteData48e0' 4
$fileMenuBg26 = Read-PaletteBytes 'paletteData5878' 20
[Array]::Copy($fileMenuBg0, 0, $fileMenuBgPalette, 0, $fileMenuBg0.Length)
[Array]::Copy($fileMenuBg26, 0, $fileMenuBgPalette, 2 * 4 * 3, $fileMenuBg26.Length)
[IO.File]::WriteAllBytes(
    (Join-Path $destination 'menu\palette_file_bg.bin'), $fileMenuBgPalette)
$fileMenuSpritePalette = [byte[]]::new(8 * 4 * 3)
$fileMenuSprite03 = Read-PaletteBytes 'standardSpritePaletteData' 16
$fileMenuSprite46 = Read-PaletteBytes 'paletteData5858' 12
[Array]::Copy($fileMenuSprite03, 0, $fileMenuSpritePalette, 0, $fileMenuSprite03.Length)
[Array]::Copy($fileMenuSprite46, 0, $fileMenuSpritePalette, 4 * 4 * 3, $fileMenuSprite46.Length)
[IO.File]::WriteAllBytes(
    (Join-Path $destination 'menu\palette_file_sprites.bin'), $fileMenuSpritePalette)

# Preserve visible symbols as UTF-8 while retaining commands whose behavior is
# owned by DialogueBox. In particular, \col and \stop must survive import so
# the runtime can apply their original inline palette and page-break behavior.
function Normalize-DialogueText([string]$text) {
    $text = $text.Replace('\left', [string][char]0x2190)
    $text = $text.Replace('\right', [string][char]0x2192)
    $text = $text.Replace('\up', [string][char]0x2191)
    $text = $text.Replace('\down', [string][char]0x2193)
    $text = $text.Replace('\sym(0x1c)', [string][char]0x266a)
    $text = $text.Replace('\sym(0x57)', [string][char]0x25b2)
    $text = $text.Replace('\Link', 'Link')
    return [regex]::Replace($text, '\\pos\([^)]*\)', '')
}

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
    $text = Normalize-DialogueText ($lines -join "`n")
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

