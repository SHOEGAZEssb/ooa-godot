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
    '(?ms)^dungeonData(?<index>[0-9a-f]{2}):\s*\r?\n\s*m_DungeonData\s+>wGroup(?<group>[45])RoomFlags,\s*\$[0-9a-f]{2},\s*dungeon[0-9a-f]{2}Layout,\s*\$(?<floors>[0-9a-f]{2}),\s*\$(?<base>[0-9a-f]{2}),\s*\$(?<compass>[0-9a-f]{2})')) {
    $index = [Convert]::ToInt32($record.Groups['index'].Value, 16)
    $dungeonMetadata[$index] = @{
        Group = [Convert]::ToInt32($record.Groups['group'].Value, 16)
        Floors = [Convert]::ToInt32($record.Groups['floors'].Value, 16)
        BaseFloor = [Convert]::ToInt32($record.Groups['base'].Value, 16)
        CompassFloors = [Convert]::ToInt32($record.Groups['compass'].Value, 16)
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
$dungeonMapRows.Add('# dungeon`tgroup`tfloors`tbase-floor`tcompass-floors`tfloor`tx`ty`troom`tproperties')
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
            "$dungeon`t$($metadataRecord.Group)`t$($metadataRecord.Floors)`t$($metadataRecord.BaseFloor)`t$($metadataRecord.CompassFloors)`t$([Math]::Floor($cell / 64))`t$($floorCell % 8)`t$([Math]::Floor($floorCell / 8))`t$($room.ToString('x2'))`t$($properties.ToString('x2'))")
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

