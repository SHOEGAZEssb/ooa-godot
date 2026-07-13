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

$metadata = [byte[]]::new(128 * 6)
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
    $metadata[$id * 6] = [byte]$layout
    $metadata[$id * 6 + 1] = [byte]$layoutGroup
    $metadata[$id * 6 + 2] = [byte]$paletteHeader.Id
    $metadata[$id * 6 + 3] = 1
    $metadata[$id * 6 + 4] = [byte]$animation
    $metadata[$id * 6 + 5] = if (($flags -band 0x08) -ne 0) {
        [byte]($properties -band 0x0f)
    } else {
        [byte]0xff
    }
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
Copy-GeneratedFile "gfx_compressible\ages\spr_syrup_teenager.png" "gfx\spr_syrup_teenager.png"
Copy-GeneratedFile "gfx_compressible\common\gfx_hud.png" "gfx\gfx_hud.png"
Copy-GeneratedFile "gfx\common\spr_item_icons_2.png" "gfx\spr_item_icons_2.png"
Copy-GeneratedFile "gfx_compressible\ages\spr_common_items.png" "gfx\spr_common_items.png"
Copy-GeneratedFile "gfx\common\gfx_partial_hearts.png" "gfx\gfx_partial_hearts.png"
Copy-GeneratedFile "gfx\common\gfx_font.png" "gfx\gfx_font.png"
foreach ($animationSheet in 1..3) {
    Copy-GeneratedFile "gfx\ages\gfx_animations_${animationSheet}.png" "gfx\gfx_animations_${animationSheet}.png"
}
Copy-GeneratedFile "gfx_compressible\common\map_hud_normal.bin" "hud\map_hud_normal.bin"
Copy-GeneratedFile "gfx_compressible\common\flg_hud_normal.bin" "hud\flg_hud_normal.bin"

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
    return [regex]::Replace($text, '\\(?:stop|pos\([^)]*\)|col\([^)]*\))', '')
}

# Resolve all text blocks once. This also handles the low-index generic-NPC
# commands, whose source still spells the complete TX_XXXX symbol.
$allTexts = @{}
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
    $allTexts[[Convert]::ToInt32($match.Groups['id'].Value, 16)] = Normalize-NpcText ($lines -join "`n")
}

# Chests are interactable $f1 metatiles whose room/position and treasure
# records live in chestData.s. Preserve every record now, while marking the
# rupee rewards supported by the first inventory slice with their original
# amount and TX_00XX pickup text.
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
$chestRows.Add("# group`troom`tposition`ttreasure-object`tsupported`tamount`ttext-id`tutf8-base64")
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
    $supported = 0
    $amount = 0
    $textId = 0
    $message = ''
    if ($treasure -match '^TREASURE_OBJECT_RUPEES_(?<subid>[0-9a-f]{2})$' -and
        $rupeeRewards.ContainsKey($Matches['subid'])) {
        $reward = $rupeeRewards[$Matches['subid']]
        $supported = 1
        $amount = $reward.Amount
        $textId = $reward.TextId
        $message = $reward.Message
    }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message))
    $chestRows.Add(
        "$currentChestGroup`t$room`t$position`t$treasure`t$supported`t$amount`t$($textId.ToString('x4'))`t$encoded")
}
if ($chestRows.Count -ne 134) {
    throw "Expected 133 chest records, parsed $($chestRows.Count - 1)."
}
$testChest = $chestRows | Where-Object { $_ -match '^0\t49\t51\tTREASURE_OBJECT_RUPEES_04\t1\t30\t0005\t' } | Select-Object -First 1
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
Write-Host "Imported $($tilesets.Count) tilesets, 1536 rooms, 42 signs, $($npcRows.Count - 1) NPCs, 133 chests, 529 warps, and 22 animation groups into $destination"
