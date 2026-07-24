# Maple's recurring encounter is assembled from common special-object and part
# code plus Ages-specific locations and dialogue. Export the traced tables so
# the runtime never parses disassembly source while playing.
$mapleSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\specialObjects\maple.s')
$mapleItemSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\parts\itemFromMaple.s')
$mapleLocationSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\mapleLocations.s')

if ($mapleSource -notmatch '(?ms)^mapleSpawnItemDrops:.*?^mapleShadowPathsTable:' -or
    $mapleItemSource -notmatch '(?ms)^partCode14:.*?^@obtainedValue:' -or
    $mapleLocationSource -notmatch '(?m)^maplePresentLocationsTable:') {
    throw 'Maple encounter sources no longer expose the traced tables.'
}

# dbrev reverses each source bit-string before emission. A zero bit allows
# Maple in the room corresponding to that byte/bit position.
function Get-MapleLocationBytes([string]$label, [string]$nextLabel) {
    $start = $mapleLocationSource.IndexOf(
        "${label}:", [StringComparison]::Ordinal)
    $end = $mapleLocationSource.IndexOf(
        "${nextLabel}:", [StringComparison]::Ordinal)
    if ($start -lt 0 -or $end -le $start) {
        throw "Could not isolate Maple location table $label."
    }
    $values = [Collections.Generic.List[int]]::new()
    foreach ($match in [regex]::Matches(
        $mapleLocationSource.Substring($start, $end - $start),
        'dbrev\s+%(?<a>[01]{8})\s+%(?<b>[01]{8})')) {
        foreach ($name in @('a', 'b')) {
            $bits = $match.Groups[$name].Value
            $reversed = -join $bits.ToCharArray()[($bits.Length - 1)..0]
            $values.Add([Convert]::ToInt32($reversed, 2))
        }
    }
    if ($values.Count -ne 32) {
        throw "$label should emit 32 bytes, parsed $($values.Count)."
    }
    return @($values)
}

$presentRicky = Get-MapleLocationBytes `
    'maplePresentLocationsRickyCompanion' `
    'maplePresentLocationsDimitriCompanion'
$presentDimitri = Get-MapleLocationBytes `
    'maplePresentLocationsDimitriCompanion' `
    'maplePresentLocationsMooshCompanion'
$presentMoosh = Get-MapleLocationBytes `
    'maplePresentLocationsMooshCompanion' `
    'maplePastLocations'
$pastStart = $mapleLocationSource.IndexOf(
    'maplePastLocations:', [StringComparison]::Ordinal)
if ($pastStart -lt 0) { throw 'Could not isolate maplePastLocations.' }
$pastTail = $mapleLocationSource.Substring($pastStart)
$pastValues = [Collections.Generic.List[int]]::new()
foreach ($match in [regex]::Matches(
    $pastTail, 'dbrev\s+%(?<a>[01]{8})\s+%(?<b>[01]{8})')) {
    foreach ($name in @('a', 'b')) {
        $bits = $match.Groups[$name].Value
        $reversed = -join $bits.ToCharArray()[($bits.Length - 1)..0]
        $pastValues.Add([Convert]::ToInt32($reversed, 2))
    }
}
if ($pastValues.Count -ne 32) {
    throw "maplePastLocations should emit 32 bytes, parsed $($pastValues.Count)."
}
if (($presentRicky -join ',') -ne ($presentDimitri -join ',') -or
    ($presentRicky -join ',') -ne ($presentMoosh -join ',')) {
    throw 'The three Ages present Maple location tables are no longer identical.'
}

$mapleLocationRows = [Collections.Generic.List[string]]::new()
$mapleLocationRows.Add("# group`tcompanion`troom`tsource")
foreach ($spec in @(
    @{ Group = 0; Companion = 0; Values = $presentRicky; Source = 'maplePresentLocationsRickyCompanion' },
    @{ Group = 0; Companion = 1; Values = $presentDimitri; Source = 'maplePresentLocationsDimitriCompanion' },
    @{ Group = 0; Companion = 2; Values = $presentMoosh; Source = 'maplePresentLocationsMooshCompanion' },
    @{ Group = 1; Companion = -1; Values = @($pastValues); Source = 'maplePastLocations' }
)) {
    for ($room = 0; $room -lt 0x100; $room++) {
        $blocked = ($spec.Values[[int]($room / 8)] -band
            (1 -shl ($room -band 7))) -ne 0
        if (-not $blocked) {
            $mapleLocationRows.Add(
                "$($spec.Group)`t$($spec.Companion)`t$($room.ToString('x2'))`tmapleLocations.s:$($spec.Source)")
        }
    }
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\maple_locations.tsv'),
    $mapleLocationRows,
    [Text.UTF8Encoding]::new($false))

# Preserve the source path streams and their exact step ordering.
$maplePaths = @(
    @{ Kind = 'shadow'; Index = 0; Y = 0x10; X = 0xb8; Delay = 2;
       Steps = @(@(0x18,0x64),@(0x10,0x02),@(0x08,0x1e),@(0x10,0x02),@(0x18,0x7a)) },
    @{ Kind = 'shadow'; Index = 1; Y = 0x10; X = 0xb8; Delay = 4;
       Steps = @(@(0x18,0x64),@(0x10,0x04),@(0x08,0x64)) },
    @{ Kind = 'movement'; Index = 0; Y = 0x18; X = 0xb8; Delay = 2;
       Steps = @(@(0x18,0x4b),@(0x10,0x01),@(0x08,0x32),@(0x10,0x01),@(0x18,0x46)) },
    @{ Kind = 'movement'; Index = 1; Y = 0x70; X = 0xb8; Delay = 2;
       Steps = @(@(0x18,0x4b),@(0x00,0x01),@(0x08,0x32),@(0x00,0x01),@(0x18,0x46)) },
    @{ Kind = 'movement'; Index = 2; Y = 0x18; X = 0xf0; Delay = 2;
       Steps = @(@(0x08,0x46),@(0x10,0x19),@(0x18,0x28),@(0x00,0x14),@(0x08,0x19),@(0x10,0x0f),@(0x18,0x14),@(0x00,0x0a),@(0x08,0x0f),@(0x10,0x32)) },
    @{ Kind = 'movement'; Index = 3; Y = 0xa0; X = 0x90; Delay = 2;
       Steps = @(@(0x00,0x37),@(0x18,0x01),@(0x10,0x19),@(0x18,0x01),@(0x00,0x19),@(0x18,0x01),@(0x10,0x3c)) },
    @{ Kind = 'movement'; Index = 4; Y = 0xa0; X = 0x10; Delay = 2;
       Steps = @(@(0x00,0x37),@(0x08,0x01),@(0x10,0x19),@(0x08,0x01),@(0x00,0x19),@(0x08,0x01),@(0x10,0x3c)) },
    @{ Kind = 'movement'; Index = 5; Y = 0x18; X = 0xf0; Delay = 1;
       Steps = @(@(0x08,0x28),@(0x16,0x0f),@(0x08,0x2d),@(0x16,0x0a),@(0x08,0x37)) },
    @{ Kind = 'movement'; Index = 6; Y = 0xf0; X = 0x30; Delay = 2;
       Steps = @(@(0x14,0x19),@(0x05,0x11),@(0x14,0x0a),@(0x17,0x05),@(0x10,0x01),@(0x05,0x1e),@(0x14,0x1e)) },
    @{ Kind = 'movement'; Index = 7; Y = 0xf0; X = 0x70; Delay = 2;
       Steps = @(@(0x0c,0x19),@(0x1b,0x11),@(0x0c,0x08),@(0x0a,0x02),@(0x10,0x01),@(0x1b,0x0f),@(0x0c,0x1e)) }
)
function Get-MapleHexBytes([string]$source, [string]$label) {
    $body = Get-AssemblyLabelBody $source $label
    $values = [Collections.Generic.List[int]]::new()
    foreach ($row in [regex]::Matches(
        $body, '(?m)^\s*\.db\s+(?<values>[^;\r\n]+)')) {
        foreach ($value in [regex]::Matches(
            $row.Groups['values'].Value,
            '\$(?<value>[0-9a-f]{2})(?![0-9a-f])')) {
            $values.Add([Convert]::ToInt32(
                $value.Groups['value'].Value, 16))
        }
    }
    return @($values)
}
$pathLabels = @(
    '@rareItemDrops', '@standardItemDrops',
    '@pattern0', '@pattern1', '@pattern2', '@pattern3',
    '@pattern4', '@pattern5', '@pattern6', '@pattern7'
)
for ($pathIndex = 0; $pathIndex -lt $maplePaths.Count; $pathIndex++) {
    $path = $maplePaths[$pathIndex]
    $expected = [Collections.Generic.List[int]]::new()
    if ($path.Kind -eq 'movement') {
        $expected.Add([int]$path.Y)
        $expected.Add([int]$path.X)
    }
    $expected.Add([int]$path.Delay)
    foreach ($step in $path.Steps) {
        $expected.Add([int]$step[0])
        $expected.Add([int]$step[1])
    }
    $expected.Add(0xff)
    $expected.Add(0xff)
    $parsed = Get-MapleHexBytes $mapleSource $pathLabels[$pathIndex]
    if (($expected -join ',') -ne ($parsed -join ',')) {
        throw "Maple path $($pathLabels[$pathIndex]) no longer matches its traced record."
    }
}

$movementPatternIndices =
    Get-MapleHexBytes $mapleSource 'mapleMovementPatternIndices'
if ($movementPatternIndices.Count -ne 16 -or
    $movementPatternIndices.Where({ $_ -lt 0 -or $_ -gt 7 }).Count -ne 0) {
    throw 'mapleMovementPatternIndices should contain 16 path IDs in range 0-7.'
}
$mapleMovementRows = [Collections.Generic.List[string]]::new()
$mapleMovementRows.Add("# slot`tpath")
for ($slot = 0; $slot -lt $movementPatternIndices.Count; $slot++) {
    $mapleMovementRows.Add("$slot`t$($movementPatternIndices[$slot])")
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\maple_movement_selection.tsv'),
    $mapleMovementRows,
    [Text.UTF8Encoding]::new($false))

$maplePathRows = [Collections.Generic.List[string]]::new()
$maplePathRows.Add(
    "# kind`tindex`tstart-y`tstart-x`tturn-delay`tstep`tangle`tduration")
foreach ($path in $maplePaths) {
    for ($step = 0; $step -lt $path.Steps.Count; $step++) {
        $entry = $path.Steps[$step]
        $maplePathRows.Add(
            "$($path.Kind)`t$($path.Index)`t$($path.Y.ToString('x2'))`t$($path.X.ToString('x2'))`t$($path.Delay)`t$step`t$(([int]$entry[0]).ToString('x2'))`t$([int]$entry[1])")
    }
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\maple_paths.tsv'),
    $maplePathRows,
    [Text.UTF8Encoding]::new($false))

# Resolve all 32 special-object animations. Each graphics pointer replaces the
# specified number of 8x8 tiles starting at Maple's first OBJ tile; the other
# OBJ tiles retain the preceding frame's contents. Resolve that virtual VRAM
# state per tile before rendering directly from the complete PNG. A
# two-argument graphics pointer loads nothing and retains every current tile.
$specialAnimationSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')
$specialOamSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\specialObjectOamData.s')
$gfxBodyStart = $specialAnimationSource.IndexOf(
    'specialObject0eGfxPointers:', [StringComparison]::Ordinal)
$animationTableStart = $specialAnimationSource.IndexOf(
    'specialObject0eAnimationDataPointers:', [StringComparison]::Ordinal)
$oamTableStart = $specialAnimationSource.IndexOf(
    'specialObject0eOamDataPointers:', [StringComparison]::Ordinal)
if ($gfxBodyStart -lt 0 -or $animationTableStart -le $gfxBodyStart -or
    $oamTableStart -le $animationTableStart) {
    throw 'Could not isolate Maple special-object visual tables.'
}
$gfxTileOffsets = @{}
$gfxTileCounts = @{}
foreach ($line in ($specialAnimationSource.Substring(
    $gfxBodyStart, $animationTableStart - $gfxBodyStart) -split '\r?\n')) {
    if ($line -match 'm_SpecialObjectGfxPointer\s+\$(?<index>[0-9a-f]{2})\s+spr_maple\s+\$(?<offset>[0-9a-f]{4})\s+\$(?<size>[0-9a-f]{2})') {
        $index = [Convert]::ToInt32($Matches['index'], 16)
        $gfxTileOffsets[$index] =
            [Convert]::ToInt32($Matches['offset'], 16) / 16
        $gfxTileCounts[$index] =
            [Convert]::ToInt32($Matches['size'], 16)
    } elseif ($line -match 'm_SpecialObjectGfxPointer\s+\$(?<index>[0-9a-f]{2})\s+\$0000') {
        $index = [Convert]::ToInt32($Matches['index'], 16)
        $gfxTileOffsets[$index] = 0
        $gfxTileCounts[$index] = 0
    }
}
$mapleAnimationLabels = @(
    [regex]::Matches(
        $specialAnimationSource.Substring(
            $animationTableStart, $oamTableStart - $animationTableStart),
        '(?m)^\s*\.dw\s+(?<label>animationData[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
$nextSpecialTable = $specialAnimationSource.IndexOf(
    'specialObject0bGfxPointers:', $oamTableStart,
    [StringComparison]::Ordinal)
$mapleOamLabels = @(
    [regex]::Matches(
        $specialAnimationSource.Substring(
            $oamTableStart, $nextSpecialTable - $oamTableStart),
        '(?m)^\s*\.dw\s+(?<label>oamData[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
if ($mapleAnimationLabels.Count -ne 32 -or $mapleOamLabels.Count -ne 57 -or
    $gfxTileOffsets.Count -ne 57 -or $gfxTileCounts.Count -ne 57) {
    throw "Expected 32 Maple animations, 57 OAM pointers, and 57 gfx pointers; got $($mapleAnimationLabels.Count), $($mapleOamLabels.Count), $($gfxTileOffsets.Count)/$($gfxTileCounts.Count)."
}
function Resolve-MapleOamTiles(
    [string]$encoded,
    [int[]]$vramTiles,
    [string]$animationLabel,
    [int]$gfxIndex) {
    if ([string]::IsNullOrEmpty($encoded)) { return '' }
    return (@($encoded -split ';' | ForEach-Object {
        $fields = $_ -split ','
        if ($fields.Count -ne 4) { throw "Malformed Maple OAM block: $_" }
        $tile = [int]$fields[2]
        if ($tile -lt 0 -or $tile -ge 0xff -or
            $vramTiles[$tile] -lt 0 -or
            $vramTiles[$tile + 1] -ne $vramTiles[$tile] + 1) {
            throw "$animationLabel graphic `$$($gfxIndex.ToString('x2')) references unresolved Maple VRAM tile `$$($tile.ToString('x2'))."
        }
        "$($fields[0]),$($fields[1]),$($vramTiles[$tile]),$($fields[3])"
    }) -join ';')
}
function Resolve-MapleSpecialAnimation([string]$label) {
    $body = Get-AssemblyLabelBody $specialAnimationSource $label
    $frames = [Collections.Generic.List[string]]::new()
    $vramTiles = [int[]]::new(0x100)
    for ($tile = 0; $tile -lt $vramTiles.Length; $tile++) {
        $vramTiles[$tile] = -1
    }
    foreach ($frame in [regex]::Matches(
        $body,
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')) {
        $duration = [Convert]::ToInt32(
            $frame.Groups['duration'].Value, 16)
        $gfx = [Convert]::ToInt32($frame.Groups['gfx'].Value, 16)
        $parameter = [Convert]::ToInt32(
            $frame.Groups['parameter'].Value, 16)
        if (-not $gfxTileOffsets.ContainsKey($gfx) -or
            $gfx -ge $mapleOamLabels.Count) {
            throw "$label references missing Maple graphic/OAM index `$$($gfx.ToString('x2'))."
        }
        $loadedTileOffset = [int]$gfxTileOffsets[$gfx]
        $loadedTileCount = [int]$gfxTileCounts[$gfx]
        for ($tile = 0; $tile -lt $loadedTileCount; $tile++) {
            $vramTiles[$tile] = $loadedTileOffset + $tile
        }
        $rawOam = Resolve-Oam $specialOamSource $mapleOamLabels[$gfx]
        $oam = Resolve-MapleOamTiles `
            $rawOam $vramTiles $label $gfx
        $metadata = if ($parameter -eq 0) {
            "$duration"
        } else {
            "$duration,$parameter"
        }
        $frames.Add("$metadata@$oam")
    }
    if ($frames.Count -eq 0) {
        throw "Maple animation $label has no frames."
    }
    return $frames -join '|'
}
$mapleAnimations = @($mapleAnimationLabels | ForEach-Object {
    Resolve-MapleSpecialAnimation $_
})
$mapleVisualRows = @(
    "# sprite`ttile-base`tpalette`tanimations-base64`tsource",
    "spr_maple`t0`t0`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($mapleAnimations -join "`n")))`tspecialObjectAnimationData.s:specialObject0e"
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\maple_visual.tsv'),
    $mapleVisualRows,
    [Text.UTF8Encoding]::new($false))
Copy-GeneratedFile 'gfx\common\spr_maple.png' 'gfx\spr_maple.png'

# PART_ITEM_FROM_MAPLE uses the first 17 entries of the shared part animation
# table. Resolve the selected static frame plus the per-item tile/palette
# adjustment applied by @setOamData.
$part14AnimationStart = $partAnimationSource.IndexOf(
    'part14Animations:', [StringComparison]::Ordinal)
$part02AnimationStart = $partAnimationSource.IndexOf(
    'part02Animations:', $part14AnimationStart, [StringComparison]::Ordinal)
$part14AnimationLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring(
            $part14AnimationStart,
            $part02AnimationStart - $part14AnimationStart),
        '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
$part14OamStart = $partAnimationSource.IndexOf(
    'part14OamDataPointers:', [StringComparison]::Ordinal)
$part02OamStart = $partAnimationSource.IndexOf(
    'part02OamDataPointers:', $part14OamStart, [StringComparison]::Ordinal)
$part14OamLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring(
            $part14OamStart, $part02OamStart - $part14OamStart),
        '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
if ($part14AnimationLabels.Count -ne 17 -or $part14OamLabels.Count -ne 4) {
    throw 'Maple item part animation/OAM tables no longer contain 17/4 entries.'
}
function Resolve-MapleItemAnimation([int]$animationIndex) {
    $body = Get-AssemblyLabelBody `
        $partAnimationSource $part14AnimationLabels[$animationIndex]
    $frame = [regex]::Match(
        $body,
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<offset>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')
    if (-not $frame.Success) {
        throw "Maple item animation $animationIndex has no frame."
    }
    $pointerIndex =
        [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
    $duration = [Convert]::ToInt32(
        $frame.Groups['duration'].Value, 16)
    return "$duration@$(Resolve-Oam $partOamSource $part14OamLabels[$pointerIndex])"
}
$rareWeights = Get-MapleHexBytes $mapleSource '@rareItems'
$standardWeights = Get-MapleHexBytes $mapleSource '@standardItems'
$linkWeights = Get-MapleHexBytes $mapleSource 'maple_linkItemDropDistribution'
$mapleItemValues = Get-MapleHexBytes $mapleSource 'mapleItemValues'
$uniqueMasks = @(0) + @(Get-MapleHexBytes $mapleSource '@itemBitmasks') +
    @(0,0,0,0,0,0,0,0,0)
if ($rareWeights.Count -ne 14 -or $standardWeights.Count -ne 14 -or
    $linkWeights.Count -ne 14 -or $mapleItemValues.Count -ne 15 -or
    $uniqueMasks.Count -ne 14 -or
    ($rareWeights | Measure-Object -Sum).Sum -ne 0x100 -or
    ($standardWeights | Measure-Object -Sum).Sum -ne 0x100 -or
    ($linkWeights | Measure-Object -Sum).Sum -ne 0x100) {
    throw 'Maple item weights, values, or unique masks no longer match the 14-item contract.'
}
$mapleItemSpecs = @(
    # index, sprite, tile base, palette, animation, value, treasure, normal, boosted, boost ring
    @(0,'spr_quest_items_5',0x10,2,0x10,0x3c,0x2b,2,2,-1),
    @(1,'spr_quest_items_5',0x0a,1,0x00,0x0f,0x34,1,1,-1),
    @(2,'spr_quest_items_5',0x08,0,0x00,0x0a,0x2d,1,1,-1),
    @(3,'spr_quest_items_5',0x08,0,0x00,0x08,0x2d,2,2,-1),
    @(4,'spr_quest_items_5',0x00,2,0x0f,0x06,0x2f,1,1,-1),
    @(5,'spr_common_items',0x12,2,0x05,0x05,0x20,5,10,-1),
    @(6,'spr_common_items',0x14,3,0x06,0x05,0x21,5,10,-1),
    @(7,'spr_common_items',0x16,1,0x07,0x05,0x22,5,10,-1),
    @(8,'spr_common_items',0x18,1,0x08,0x05,0x23,5,10,-1),
    @(9,'spr_common_items',0x1a,0,0x08,0x05,0x24,5,10,-1),
    @(10,'spr_common_items',0x10,4,0x04,0x04,0x03,4,8,-1),
    @(11,'spr_common_items',0x02,5,0x01,0x03,0x29,4,8,0x25),
    @(12,'spr_common_items',0x06,5,0x03,0x02,0x28,3,4,0x24),
    @(13,'spr_common_items',0x04,0,0x02,0x01,0x28,1,2,0x24)
)
$mapleItemRows = [Collections.Generic.List[string]]::new()
$mapleItemRows.Add(
    "# index`tsprite`ttile-base`tpalette`tanimation`tvalue`ttreasure`tnormal-parameter`tboosted-parameter`tboost-ring`trare-weight`tstandard-weight`tlink-weight`tunique-mask`tsource")
foreach ($spec in $mapleItemSpecs) {
    $index = [int]$spec[0]
    if ([int]$spec[5] -ne $mapleItemValues[$index]) {
        throw "Maple item `$$($index.ToString('x2')) value no longer matches mapleItemValues."
    }
    $mapleItemRows.Add(
        "$index`t$($spec[1])`t$($spec[2])`t$($spec[3])`t$(Resolve-MapleItemAnimation $spec[4])`t$($mapleItemValues[$index])`t$($spec[6])`t$($spec[7])`t$($spec[8])`t$($spec[9])`t$($rareWeights[$index])`t$($standardWeights[$index])`t$($linkWeights[$index])`t$($uniqueMasks[$index])`titemFromMaple.s:@oamData/@itemDropTreasureTable")
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\maple_items.tsv'),
    $mapleItemRows,
    [Text.UTF8Encoding]::new($false))
Copy-GeneratedFile `
    'gfx_compressible\common\spr_quest_items_5.png' `
    'gfx\spr_quest_items_5.png'

# INTERAC_TOUCHING_BOOK $a5:$00 supplies the book actor used by the trade
# branch. Npc-data import already resolved its shared animation/OAM tables.
$bookGraphic = $interactionGraphics['165:0']
$bookAnimation = Resolve-NpcAnimation 0xa5 $bookGraphic.DefaultAnimation
$bookSprite = $gfxNames[$bookGraphic.Gfx]
if ($bookSprite -ne 'spr_quest_items_1' -or
    [string]::IsNullOrWhiteSpace($bookAnimation)) {
    throw 'INTERAC_TOUCHING_BOOK visual no longer resolves to spr_quest_items_1.'
}
$bookSource = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object {
        Get-ChildItem $_.FullName -Recurse -File -Filter "$bookSprite.png"
    } | Select-Object -First 1
if ($null -eq $bookSource) { throw "Touching Book sprite not found: $bookSprite.png" }
Copy-Item -LiteralPath $bookSource.FullName `
    -Destination (Join-Path $destination "gfx\$bookSprite.png") -Force
$mapleBookRows = @(
    "# sprite`ttile-base`tpalette`tanimation`tsource",
    "$bookSprite`t$($bookGraphic.TileBase)`t$($bookGraphic.Palette)`t$bookAnimation`tinteractionData.s:interactiona5SubidData"
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\maple_book.tsv'),
    $mapleBookRows,
    [Text.UTF8Encoding]::new($false))

$mapleTextRows = [Collections.Generic.List[string]]::new()
$mapleTextRows.Add("# text-id`tmessage-base64")
foreach ($textId in 0x0700..0x0713) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Maple text TX_$($textId.ToString('x4')) was not decoded."
    }
    $mapleTextRows.Add(
        "$($textId.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$textId])))")
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'metadata\maple_text.tsv'),
    $mapleTextRows,
    [Text.UTF8Encoding]::new($false))

$mapleConstantRows = @(
    "# key`tvalue`tsource",
    "kill-counter-address`t50753`troomInitialization.s:wMapleKillCounter",
    "state-address`t50756`tmaple.s:wMapleState",
    "normal-kill-threshold`t30`troomInitialization.s:checkAndSpawnMaple",
    "ring-kill-threshold`t15`troomInitialization.s:checkAndSpawnMaple",
    "initial-y`t16`tmaple.s:mapleState0",
    "initial-x`t184`tmaple.s:mapleState0",
    "initial-z`t-120`tmaple.s:mapleState0",
    "entry-delay`t3`tmaple.s:mapleState0/mapleState1",
    "race-speed-raw`t80`tmaple.s:@label_05_262",
    "departure-speed-raw`t120`tmaple.s:mapleState9",
    "horizontal-shake-updates`t15`tmaple.s:mapleCollideWithLink"
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'metadata\maple_constants.tsv'),
    $mapleConstantRows,
    [Text.UTF8Encoding]::new($false))
