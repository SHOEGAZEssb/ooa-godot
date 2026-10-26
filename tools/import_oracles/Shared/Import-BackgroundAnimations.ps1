# Shared background DMA animation format; each game retains its own tracks.
foreach ($game in @('ages', 'seasons')) {
    $gameDestination = if ($game -eq 'ages') { $destination } else { Split-Path $previewDestination -Parent }
    $expectedHeaders = if ($game -eq 'ages') { 112 } else { 98 }
    $lastGroup = if ($game -eq 'ages') { 21 } else { 26 }
# Expand the animation engine's three linked tables into address-independent
# records. Destinations are converted from VRAM addresses to the same signed
# tile indices used by OracleRoomData's 128x128 tileset source images.
$animationHeaderSource = Read-ImportLines (Join-Path $Disassembly "data\$game\animationGfxHeaders.s")
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
if ($animationHeaderIndex -ne $expectedHeaders) {
    throw "Expected $expectedHeaders animation graphics headers, parsed $animationHeaderIndex."
}

$animationDataSource = Read-ImportText (Join-Path $Disassembly "data\$game\animationData.s")
$animationSequences = @{}
$animationBlocks = [regex]::Matches(
    $animationDataSource,
    '(?ms)^(?<label>animationData[A-Za-z0-9_]+):[^\r\n]*\r?\n(?<body>.*?)(?=^animationData[A-Za-z0-9_]+:|\z)'
)
foreach ($block in $animationBlocks) {
    $frames = [regex]::Matches($block.Groups['body'].Value, '\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<gfx>[0-9a-f]{2})')
    if ($frames.Count -eq 0) { continue }
    $sequence = $frames | ForEach-Object {
        "$([Convert]::ToInt32($_.Groups['duration'].Value, 16)):$([Convert]::ToInt32($_.Groups['gfx'].Value, 16))"
    }
    $animationSequences[$block.Groups['label'].Value] = $sequence -join ','
}

$animationGroupLines = Read-ImportLines (Join-Path $Disassembly "data\$game\animationGroups.s")
$animationGroups = @{}
$animationInitialStates=@{}
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
    if ($line -match '^\w+:') {
        if ($readingGroupBody) { Complete-AnimationGroup }
        continue
    }
    if ($pendingGroups.Count -gt 0 -and $line -match '\.db\s+\$(?<state>8[0-9a-f])') {
        foreach($groupId in $pendingGroups){$animationInitialStates[$groupId]=[Convert]::ToInt32($Matches['state'],16)}
        $readingGroupBody = $true
        continue
    }
    if ($readingGroupBody -and $line -match '\.dw\s+(?<label>animationData[A-Za-z0-9_]+)') {
        $currentTracks.Add($Matches['label'])
    }
}
Complete-AnimationGroup

$animationTrackRows = [Collections.Generic.List[string]]::new()
$animationTrackRows.Add("# group`ttrack`tframes(duration:gfx-index)")
foreach ($groupId in 0..$lastGroup) {
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

$animationDestination = Join-Path $gameDestination "animations"
Write-GeneratedTable(
    (Join-Path $animationDestination "headers.tsv"), $animationHeaderRows)
Write-GeneratedTable(
    (Join-Path $animationDestination "tracks.tsv"), $animationTrackRows)
$states=[Collections.Generic.List[string]]::new();$states.Add("# group`tinitial-state`tsource")
foreach($groupId in 0..$lastGroup){$states.Add("$groupId`t$($animationInitialStates[$groupId].ToString('x2'))`tdata/$game/animationGroups.s:animationGroup$($groupId.ToString('x2'))")}
Write-GeneratedTable((Join-Path $animationDestination 'groups.tsv'),$states)

    foreach ($sheet in 1..3) { Copy-GeneratedFile "gfx/$game/gfx_animations_$sheet.png" "gfx/gfx_animations_$sheet.png" $gameDestination }
    if ($game -eq 'seasons') {
        # Header $61 reads $0e tiles at gfx_animations_3+$f40: two tiles
        # beyond that symbol. Preserve the actual adjacent ROM bytes too.
        $pixels = [byte[]]::new(128 * 136)
        for ($tile = 0; $tile -lt 272; $tile++) {
            for ($y = 0; $y -lt 8; $y++) {
                $offset = $seasonsSymbols['gfx_animations_3'] + $tile * 16 + $y * 2
                $low = $seasonsRomBytes[$offset]; $high = $seasonsRomBytes[$offset + 1]
                for ($x = 0; $x -lt 8; $x++) {
                    $shade = (($low -shr (7 - $x)) -band 1) -bor ((($high -shr (7 - $x)) -band 1) -shl 1)
                    $pixels[([int][Math]::Floor($tile / 16) * 8 + $y) * 128 + ($tile % 16) * 8 + $x] = (3 - $shade) * 85
                }
            }
        }
        Write-GeneratedGrayscalePng (Join-Path $gameDestination 'gfx/gfx_animations_3.png') 128 136 $pixels
    }
}
