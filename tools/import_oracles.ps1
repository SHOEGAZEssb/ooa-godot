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
    '\s*\.db[^\r\n]+\r?\n' +
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
$metadata = [byte[]]::new(128 * 5)
$usedTilesets = [Collections.Generic.HashSet[int]]::new()

foreach ($tileset in $tilesets) {
    $id = [Convert]::ToInt32($tileset.Groups['id'].Value, 16)
    $layout = [Convert]::ToInt32($tileset.Groups['layout'].Value, 16)
    $layoutGroup = [Convert]::ToInt32($tileset.Groups['group'].Value, 16)
    $animation = [Convert]::ToInt32($tileset.Groups['animation'].Value, 16)
    $paletteSymbol = $tileset.Groups['palette'].Value
    if (-not $paletteHeaders.ContainsKey($paletteSymbol)) {
        throw "Could not resolve background palette for tileset $($id.ToString('x2')) ($paletteSymbol)."
    }

    $paletteHeader = $paletteHeaders[$paletteSymbol]
    $metadata[$id * 5] = [byte]$layout
    $metadata[$id * 5 + 1] = [byte]$layoutGroup
    $metadata[$id * 5 + 2] = [byte]$paletteHeader.Id
    $metadata[$id * 5 + 3] = 1
    $metadata[$id * 5 + 4] = [byte]$animation
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

# First NPC slice: preserve the original male villager placed by
# group0Map48ObjectData (`obj_Interaction $3a $03 $48 $38`) and resolve its
# generic script text (`villagerSubid03Script_befored3 -> TX_1420`).
$npcTextMatch = [regex]::Match(
    $textYaml,
    '(?ms)^  - name: TX_1420\r?\n    index: 0x20\r?\n    text: \|-\r?\n(?<body>(?:      [^\r\n]*(?:\r?\n|\z))+)'
)
if (-not $npcTextMatch.Success) {
    throw "Could not resolve villager text TX_1420."
}
$npcLines = $npcTextMatch.Groups['body'].Value -split '\r?\n' | ForEach-Object {
    if ($_.Length -ge 6) { $_.Substring(6) } else { '' }
}
while ($npcLines.Count -gt 0 -and $npcLines[-1] -eq '') {
    $npcLines = $npcLines[0..($npcLines.Count - 2)]
}
$npcText = $npcLines -join "`n"
$npcText = $npcText.Replace('\left', [string][char]0x2190)
$npcText = $npcText.Replace('\right', [string][char]0x2192)
$npcText = $npcText.Replace('\up', [string][char]0x2191)
$npcText = $npcText.Replace('\down', [string][char]0x2193)
$npcText = [regex]::Replace($npcText, '\\(?:stop|pos\([^)]*\)|col\([^)]*\))', '')
$npcRows = [Collections.Generic.List[string]]::new()
$npcRows.Add("# group`troom`tid`tsubid`ty`tx`ttext-id`tsprite`tframe-base`tutf8-base64")
$npcRows.Add("0`t48`t3a`t03`t48`t38`t1420`tspr_syrup_teenager`t4`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($npcText)))")
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
Write-Host "Imported $($tilesets.Count) tilesets, 1536 rooms, 42 signs, 1 NPC, 529 warps, and 22 animation groups into $destination"
