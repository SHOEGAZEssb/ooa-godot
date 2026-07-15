# Preserve the complete Ages branch of constants/common/globalFlags.s for the
# editable debug flag menu. Within WLA-DX enums, "db" advances the value while
# ".db" is used here for range aliases, so only the former emits a flag row.
$globalFlagRows = [Collections.Generic.List[string]]::new()
$globalFlagValues = @{}
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
        $globalFlagValues[$Matches[1]] = $globalFlagValue
        $globalFlagValue++
    }
}
if ($globalFlagRows.Count -ne 0x80) {
    throw "Expected 128 Ages global flags, parsed $($globalFlagRows.Count)."
}
$globalFlagPath = Join-Path $destination 'metadata\global_flags.tsv'
New-Item -ItemType Directory -Force -Path (Split-Path $globalFlagPath -Parent) | Out-Null
[IO.File]::WriteAllLines($globalFlagPath, $globalFlagRows, [Text.UTF8Encoding]::new($false))

# Import the save-backed subset of applyRoomSpecificTileChanges as declarative
# conditions and layout operations. The dispatcher is parsed rather than
# repeating group/room IDs, so shared routines automatically expand to every
# room that calls them. Transient switch, water, vine, and encounter state is
# kept out until its owning runtime systems exist.
$roomTileChangeSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\ages\roomSpecificTileChanges.s')
$tileChangeJumpBlock = [regex]::Match(
    $roomTileChangeSource,
    '(?ms)^\s*rst_jumpTable\s*(?<body>.*?)(?=^roomTileChangerCodeGroupTable:)')
if (-not $tileChangeJumpBlock.Success) {
    throw 'Could not parse the room tile-changer jump table.'
}
$tileChangeCodeLabels = @{}
foreach ($entry in [regex]::Matches(
    $tileChangeJumpBlock.Groups['body'].Value,
    '(?m)^\s*\.dw\s+(?<label>tileReplacement_[A-Za-z0-9]+)\s*;\s*\$(?<code>[0-9a-f]{2})')) {
    $code = [Convert]::ToInt32($entry.Groups['code'].Value, 16)
    $tileChangeCodeLabels[$code] = $entry.Groups['label'].Value
}
if ($tileChangeCodeLabels.Count -ne 56) {
    throw "Expected 56 room tile-changer routines, parsed $($tileChangeCodeLabels.Count)."
}

$tileChangeDispatch = @{}
$tileChangeDispatchCount = 0
for ($group = 0; $group -lt 8; $group++) {
    $groupBlock = [regex]::Match(
        $roomTileChangeSource,
        "(?ms)^roomTileChangerCodeGroup${group}Data:\s*(?<body>.*?)(?=^roomTileChangerCodeGroup|^;;)")
    if (-not $groupBlock.Success) {
        throw "Could not parse room tile-changer group $group data."
    }
    foreach ($entry in [regex]::Matches(
        $groupBlock.Groups['body'].Value,
        '(?m)^\s*\.db\s+\$(?<room>[0-9a-f]{2})\s+\$(?<code>[0-9a-f]{2})')) {
        $code = [Convert]::ToInt32($entry.Groups['code'].Value, 16)
        if (-not $tileChangeCodeLabels.ContainsKey($code)) {
            throw "Room tile-changer group $group references unknown code `$$($code.ToString('x2'))."
        }
        $label = $tileChangeCodeLabels[$code]
        if (-not $tileChangeDispatch.ContainsKey($label)) {
            $tileChangeDispatch[$label] = [Collections.Generic.List[object]]::new()
        }
        $tileChangeDispatch[$label].Add([pscustomobject]@{
            Group = $group
            Room = $entry.Groups['room'].Value.ToLowerInvariant()
        })
        $tileChangeDispatchCount++
    }
}
if ($tileChangeDispatchCount -ne 58) {
    throw "Expected 58 room tile-changer dispatch entries, parsed $tileChangeDispatchCount."
}

$roomTileChangeRows = [Collections.Generic.List[string]]::new()
$roomTileChangeRows.Add("# group`troom`tconditions`toperations`tsource")
$supportedTileChangeLabels = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::Ordinal)
function Add-RoomTileChangeRule(
    [string]$label,
    [string]$conditions,
    [string]$operations) {
    if (-not $tileChangeDispatch.ContainsKey($label)) {
        throw "Room tile-change rule references undispatched routine $label."
    }
    foreach ($location in $tileChangeDispatch[$label]) {
        $roomTileChangeRows.Add(
            "$($location.Group)`t$($location.Room)`t$conditions`t$operations`t$label")
    }
    [void]$supportedTileChangeLabels.Add($label)
}

$flagD3Crystals = $globalFlagValues['GLOBALFLAG_D3_CRYSTALS'].ToString('x2')
$flagMakuSaved = $globalFlagValues['GLOBALFLAG_MAKU_TREE_SAVED'].ToString('x2')
$flagSymmetryBridge = $globalFlagValues['GLOBALFLAG_SYMMETRY_BRIDGE_BUILT'].ToString('x2')
$flagIntroDone = $globalFlagValues['GLOBALFLAG_INTRO_DONE'].ToString('x2')

# CGB/secret-shop routines, including the original D1 fallthrough bug.
Add-RoomTileChangeRule 'tileReplacement_group1Map58' 'always' 'set:35:de'
Add-RoomTileChangeRule 'tileReplacement_group2Map7e' 'wram_mask_eq:c642:0f:0f' `
    'fill:13:03:06:a0|set:25:f1,27:f1,32:a0'
Add-RoomTileChangeRule 'tileReplacement_group4Map1b' 'current_room_set:80' `
    'set:1a:09,1c:09'
Add-RoomTileChangeRule 'tileReplacement_group4Map1b' `
    'current_room_set:80,wram_mask_eq:c642:0f:0f' `
    'fill:13:03:06:a0|set:25:f1,27:f1,32:a0'

# Current-room flag changes.
Add-RoomTileChangeRule 'tileReplacement_group4Mapc9' 'current_room_set:40' `
    'fill:27:01:04:6d'
Add-RoomTileChangeRule 'tileReplacement_group4Map59' 'current_room_set:80' 'replace:09:08'
Add-RoomTileChangeRule 'tileReplacement_group5Map38' 'current_room_set:40' `
    'set:39:6a,49:6a,59:6a,69:6a'
Add-RoomTileChangeRule 'tileReplacement_group5Map25' 'current_room_clear:40' `
    'fill:17:09:04:a6|fill:1b:09:01:b3|fill:16:09:01:b1'
Add-RoomTileChangeRule 'tileReplacement_group5Map43' 'current_room_clear:40' `
    'fill:17:09:04:a7|fill:1b:09:01:b3|fill:16:09:01:b1'
Add-RoomTileChangeRule 'tileReplacement_group5Map43' 'current_room_set:40' 'replace:09:08'
Add-RoomTileChangeRule 'tileReplacement_group5Map95' 'current_room_clear:40' `
    'set:4d:b4,4e:b2|fill:5e:05:01:a7|fill:5d:05:01:b1'
Add-RoomTileChangeRule 'tileReplacement_group5Mapc3' 'current_room_set:40' `
    'set:31:a2,32:a1,33:a2,34:a1,35:a2,41:a1,42:a2,43:a1,44:a2,45:a1,51:a2,52:a1,53:a2,54:a1,55:a2'
Add-RoomTileChangeRule 'tileReplacement_group7Map4a' 'current_room_set:80' `
    'fill:0d:0a:01:18'
Add-RoomTileChangeRule 'tileReplacement_group0Map5c' 'current_room_set:80' `
    'set:34:3a,43:3a,44:3a,45:3a'
Add-RoomTileChangeRule 'tileReplacement_group0Map73' 'current_room_set:80' `
    'set:73:3a,74:10,75:11,76:12,77:3a'
Add-RoomTileChangeRule 'tileReplacement_group0Mapac' 'current_room_clear:80' `
    'set:33:af,34:af,43:af,44:af'
Add-RoomTileChangeRule 'tileReplacement_group0Map54' 'current_room_set:40' `
    'set:43:1d,44:1d,45:1d,53:1e,54:1e,55:1e,68:9e'
Add-RoomTileChangeRule 'tileReplacement_group5Mapc2' 'current_room_set:80' `
    'fill:56:01:04:6d'
Add-RoomTileChangeRule 'tileReplacement_group5Mape3' 'current_room_set:80' `
    'fill:26:01:03:6d'
Add-RoomTileChangeRule 'tileReplacement_group2Map90' 'current_room_set:02' `
    'draw:42:02:06:dd,de,df,ed,ee,ef,b9,ba,bb,bc,bd,be'
Add-RoomTileChangeRule 'tileReplacement_group1Map8c' 'current_room_set:80' `
    'set:04:30,05:32,14:3a,15:3a,34:02,35:3a'
Add-RoomTileChangeRule 'tileReplacement_group2Map9e' 'current_room_set:40' `
    'fill:13:01:06:6d'
Add-RoomTileChangeRule 'tileReplacement_group4Mapea' 'current_room_set:40' `
    'fill:33:01:03:a3|fill:39:01:03:a3|fill:43:01:03:b7|fill:49:01:03:b7|fill:53:01:03:88|fill:59:01:03:88'

# Global-flag changes.
Add-RoomTileChangeRule 'tileReplacement_group4Map60' "global_set:$flagD3Crystals" `
    'fill:34:05:07:a0|set:34:1d,3a:1d,74:1d,7a:1d'
Add-RoomTileChangeRule 'tileReplacement_group4Map60' `
    "global_set:$flagD3Crystals,current_room_clear:20" 'set:57:f1'
Add-RoomTileChangeRule 'tileReplacement_group4Map60' `
    "global_set:$flagD3Crystals,current_room_set:20" 'set:57:f0'
Add-RoomTileChangeRule 'tileReplacement_group4Map52' "global_set:$flagD3Crystals" `
    'copy_original:60'
Add-RoomTileChangeRule 'tileReplacement_group0Map38' "global_set:$flagMakuSaved" `
    'fill:73:01:04:f9'
Add-RoomTileChangeRule 'tileReplacement_group1Map38' 'current_room_set:80' `
    'fill:73:01:04:f9'
Add-RoomTileChangeRule 'tileReplacement_group0Map48' "global_set:$flagMakuSaved" `
    'fill:03:01:04:3a'
Add-RoomTileChangeRule 'tileReplacement_group0Map25' "global_set:$flagSymmetryBridge" `
    'set:50:1d,51:1d,52:1d,60:1e,61:1e,62:1e'
Add-RoomTileChangeRule 'tileReplacement_group0Map3a' "global_set:$flagIntroDone" `
    'set:23:ee'

# Flags belonging to another room table.
Add-RoomTileChangeRule 'tileReplacement_group0Map0b' 'room_set:0:0a:40' 'set:43:dd'
Add-RoomTileChangeRule 'tileReplacement_group1Map27' 'room_set:1:15:80' `
    'set:33:3a,43:02'
Add-RoomTileChangeRule 'tileReplacement_group1Map27' 'room_set:1:17:80' `
    'set:34:3a,24:02'
Add-RoomTileChangeRule 'tileReplacement_group1Map27' 'room_set:1:35:80' `
    'set:35:3a,45:02'
Add-RoomTileChangeRule 'tileReplacement_group1Map27' 'room_set:1:37:80' `
    'set:36:3a,26:02'
Add-RoomTileChangeRule 'tileReplacement_group0Mapa5' 'room_set:1:a5:80' `
    'set:22:ee,23:ef'

# Essence-backed changes.
Add-RoomTileChangeRule 'tileReplacement_group5Mapc3' 'essence_set:4' `
    'set:06:b0,07:b0,08:b0,09:b0,16:ef,19:ef,26:ef,29:ef,36:b4,37:b2,38:b2,39:b2'
Add-RoomTileChangeRule 'tileReplacement_group5Mapb9' 'essence_set:3' `
    'set:41:a1,42:a1,43:a1,44:ef,45:a1,51:a2,52:ef,53:a2,54:a2,55:a2'
Add-RoomTileChangeRule 'tileReplacement_group0Mape0' 'essence_set:4' 'set:46:dd'
Add-RoomTileChangeRule 'tileReplacement_group0Mape1' 'essence_set:0' 'set:26:dd'
Add-RoomTileChangeRule 'tileReplacement_group0Mape1' 'essence_set:1' 'set:53:dd'
Add-RoomTileChangeRule 'tileReplacement_group0Mape2' 'essence_set:2' 'set:54:dd'

# Every flag/essence-backed routine must be imported above or explicitly
# deferred here. Room 2:f7 also reads transient wSeedTreeRefilledBitset, which
# has no owning gameplay system in the port yet.
$deferredFlagTileChangeLabels = @('tileReplacement_group2Mapf7')
$flagTileChangeBlocks = [regex]::Matches(
    $roomTileChangeSource,
    '(?ms)^(?<label>tileReplacement_[A-Za-z0-9]+):(?<body>.*?)(?=^tileReplacement_|\z)')
$flagTileChangeCount = 0
foreach ($block in $flagTileChangeBlocks) {
    if ($block.Groups['body'].Value -notmatch
        'checkGlobalFlag|getThisRoomFlags|w(?:Present|Past|Group[0-9])RoomFlags|wEssencesObtained') {
        continue
    }
    $flagTileChangeCount++
    $label = $block.Groups['label'].Value
    if (-not $supportedTileChangeLabels.Contains($label) -and
        $deferredFlagTileChangeLabels -notcontains $label) {
        throw "Flag-backed room tile-change routine $label was neither imported nor deferred."
    }
}
if ($flagTileChangeCount -ne 34 -or $supportedTileChangeLabels.Count -ne 35) {
    throw "Expected 34 flag-backed and 35 total supported tile-change routines; " +
        "found $flagTileChangeCount and $($supportedTileChangeLabels.Count)."
}
$roomTileChangePath = Join-Path $destination 'metadata\room_tile_changes.tsv'
[IO.File]::WriteAllLines(
    $roomTileChangePath, $roomTileChangeRows, [Text.UTF8Encoding]::new($false))

# roomSpecificCode index $06 calls setDeathRespawnPoint every update. Preserve
# that table instead of embedding the two Ages rooms in runtime code.
$roomSpecificCodeSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\ages\roomSpecificCode.s')
$continuousRespawnRows = [Collections.Generic.List[string]]::new()
$continuousRespawnRows.Add("# group`troom")
for ($group = 0; $group -lt 8; $group++) {
    $block = [regex]::Match(
        $roomSpecificCodeSource,
        "(?ms)^roomSpecificCodeGroup${group}Table:\s*(?<body>.*?)(?=^roomSpecificCodeGroup[0-7]Table:|^;;)")
    if (-not $block.Success) {
        throw "Could not parse roomSpecificCode group $group table."
    }
    foreach ($entry in [regex]::Matches(
        $block.Groups['body'].Value,
        '(?m)^\s*\.db\s+\$(?<room>[0-9a-f]{2})\s+\$(?<code>[0-9a-f]{2})')) {
        if ([Convert]::ToInt32($entry.Groups['code'].Value, 16) -eq 0x06) {
            $continuousRespawnRows.Add(
                "$group`t$($entry.Groups['room'].Value.ToLowerInvariant())")
        }
    }
}
if ($continuousRespawnRows.Count -ne 3) {
    throw "Expected 2 continuous death-respawn rooms, parsed $($continuousRespawnRows.Count - 1)."
}
$continuousRespawnPath = Join-Path $destination 'metadata\continuous_death_respawn_rooms.tsv'
[IO.File]::WriteAllLines(
    $continuousRespawnPath, $continuousRespawnRows, [Text.UTF8Encoding]::new($false))

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
Copy-GeneratedFile "gfx\common\gfx_font_jp.png" "gfx\gfx_font_jp.png"
