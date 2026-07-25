# ENEMY_SEEDS_ON_TREE ($5a) locates the room's mystical-tree top-left
# metatile, creates three PART_SEED_ON_TREE ($10) children, and consumes one
# of sixteen session-local refill bits when any child is collected. Export
# every Ages placement and the common refill/part tables so the runtime does
# not special-case room 0:78.
$seedTreeEnemySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\seedsOnTree.s')
$seedTreePartSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\parts\seedOnTree.s')
$seedTreeRefillSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\seedTreeRefillData.s')
$seedTreeBank1Source = Read-ImportText (
    Join-Path $Disassembly 'code\bank1.s')
$seedTreeTileSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\tileIndices.s')
$seedTreeSpeedSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$seedTreeMusicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')

if ($seedTreeEnemySource -notmatch
        '(?ms)^enemyCode5a:.*?TILEINDEX_MYSTICAL_TREE_TL.*?wSeedTreeRefilledBitset.*?PART_SEED_ON_TREE.*?@positionOffsets:\s+\.db \$f8 \$00\s+\.db \$00 \$f8\s+\.db \$00 \$08.*?unsetFlag' -or
    $seedTreePartSource -notmatch
        '(?ms)^partCode10:.*?@oamData:\s+\.db \$12 \$02\s+\.db \$14 \$03\s+\.db \$16 \$01\s+\.db \$18 \$01\s+\.db \$1a \$00.*?objectUpdateSpeedZAndBounce.*?TREASURE_EMBER_SEEDS.*?@textIndices:\s+\.db <TX_0029\s+\.db <TX_0029\s+\.db <TX_002b\s+\.db <TX_002c\s+\.db <TX_002a.*?TREASURE_SEED_SATCHEL.*?TX_0035.*?-\$140.*?SPEED_100' -or
    $seedTreeBank1Source -notmatch
        '(?ms)^updateSeedTreeRefillData:.*?TILESETFLAG_OUTDOORS.*?seedTreeRefillLocations.*?NUM_SEED_TREES.*?^@treeScreen:.*?ld c,\$08.*?setFlag.*?clearMemory.*?^initializeSeedTreeRefillData:.*?ld \(hl\),\$f0.*?ld \(hl\),\$ff' -or
    $seedTreeTileSource -notmatch
        '(?m)^\.define TILEINDEX_MYSTICAL_TREE_TL\s+\$6e' -or
    $seedTreeSpeedSource -notmatch
        '(?m)^\s*SPEED_100\s+dsb\s+5\s*;\s*0x28' -or
    $seedTreeMusicSource -notmatch
        '(?m)^\s*SND_GETSEED\s+db\s+;\s*\$5e') {
    throw 'Seed-tree controller, collectible part, refill, tile, speed, or sound behavior changed.'
}

$seedTreePartData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) \$(?<radius>[0-9a-f]{2}) \$00 \$01 \$(?<tile>[0-9a-f]{2}) \$00 \$00\s*; \$10')
if (-not $seedTreePartData.Success -or
    [Convert]::ToInt32($seedTreePartData.Groups['gfx'].Value, 16) -ne 0x78 -or
    [Convert]::ToInt32($seedTreePartData.Groups['collision'].Value, 16) -ne 0x81 -or
    [Convert]::ToInt32($seedTreePartData.Groups['radius'].Value, 16) -ne 0x33) {
    throw 'PART_SEED_ON_TREE no longer resolves to gfx `$78, collision `$81, and radii `$33.'
}

$part10AnimationStart = $partAnimationSource.IndexOf(
    'part10Animations:', [StringComparison]::Ordinal)
$part03AnimationStart = $partAnimationSource.IndexOf(
    'part03Animations:', $part10AnimationStart, [StringComparison]::Ordinal)
$part10AnimationLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring(
            $part10AnimationStart,
            $part03AnimationStart - $part10AnimationStart),
        '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
$part10OamStart = $partAnimationSource.IndexOf(
    'part10OamDataPointers:', [StringComparison]::Ordinal)
$part02OamStart = $partAnimationSource.IndexOf(
    'part02OamDataPointers:', $part10OamStart, [StringComparison]::Ordinal)
$part10OamLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring(
            $part10OamStart,
            $part02OamStart - $part10OamStart),
        '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
if ($part10AnimationLabels.Count -lt 2 -or $part10OamLabels.Count -ne 4) {
    throw 'PART_SEED_ON_TREE animation or OAM pointer table is incomplete.'
}
$seedTreeAnimationLabel = $part10AnimationLabels[1]
$seedTreeAnimationFrame = [regex]::Match(
    (Get-AssemblyLabelBody $partAnimationSource $seedTreeAnimationLabel),
    '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$00')
if (-not $seedTreeAnimationFrame.Success) {
    throw 'PART_SEED_ON_TREE animation 1 has no ordinary frame.'
}
$seedTreePointerIndex = [Convert]::ToInt32(
    $seedTreeAnimationFrame.Groups['offset'].Value, 16) / 2
if ($seedTreePointerIndex -ge $part10OamLabels.Count) {
    throw 'PART_SEED_ON_TREE animation 1 references a missing OAM pointer.'
}
$seedTreeAnimation =
    "$([Convert]::ToInt32($seedTreeAnimationFrame.Groups['duration'].Value, 16))@" +
    (Resolve-Oam $partOamSource $part10OamLabels[$seedTreePointerIndex])
if ($seedTreeAnimation -ne '127@11,4,0,0') {
    throw "PART_SEED_ON_TREE animation changed: $seedTreeAnimation"
}

$seedTreePlacementRows = [Collections.Generic.List[string]]::new()
$seedTreePlacementRows.Add(
    "# group`troom`torder`tid`tsubid`tseed-type`trefill-index`tsource")
$seedTreeGroup = -1
$seedTreeRoom = -1
$seedTreeOrder = 0
foreach ($line in $mainObjectLines) {
    if ($line -match
        '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $seedTreeGroup = [Convert]::ToInt32($Matches['group'], 10)
        $seedTreeRoom = [Convert]::ToInt32($Matches['room'], 16)
        $seedTreeOrder = 0
        continue
    }
    if ($seedTreeGroup -lt 0 -or $line -notmatch '^\s+obj_(?!End)') {
        continue
    }
    if ($line -match
        'obj_SpecificEnemyA\s+\$00\s+\$5a\s+\$(?<subid>[0-9a-f]{2})\s+\$00\s+\$00') {
        $subid = [Convert]::ToInt32($Matches['subid'], 16)
        $seedTreePlacementRows.Add(
            "$seedTreeGroup`t$($seedTreeRoom.ToString('x2'))`t$seedTreeOrder`t5a`t$($subid.ToString('x2'))`t$(($subid -shr 4) -band 0x0f)`t$($subid -band 0x0f)`tmainData.s:group${seedTreeGroup}Map$($seedTreeRoom.ToString('x2'))ObjectData")
    }
    $seedTreeOrder++
}
if ($seedTreePlacementRows.Count -ne 11 -or
    -not ($seedTreePlacementRows | Where-Object {
        $_ -eq "0`t78`t0`t5a`t06`t0`t6`tmainData.s:group0Map78ObjectData"
    })) {
    throw "Expected ten seed-tree placements including canonical Ember tree 0:78, parsed $($seedTreePlacementRows.Count - 1)."
}

$seedTreeRefillMatches = @([regex]::Matches(
    $seedTreeRefillSource,
    '(?m)^\s*m_TreeRefillData \$(?<location>[0-9a-f]{3}), \(<wxSeedTreeRefillData\+\$(?<offset>[0-9a-f]{2})\)'))
if ($seedTreeRefillMatches.Count -ne 16) {
    throw "Expected sixteen Ages seed-tree refill locations, parsed $($seedTreeRefillMatches.Count)."
}
$seedTreeRefillRows = [Collections.Generic.List[string]]::new()
$seedTreeRefillRows.Add("# index`tgroup`troom")
for ($index = 0; $index -lt $seedTreeRefillMatches.Count; $index++) {
    $match = $seedTreeRefillMatches[$index]
    $location = [Convert]::ToInt32($match.Groups['location'].Value, 16)
    $offset = [Convert]::ToInt32($match.Groups['offset'].Value, 16)
    if ($offset -ne $index * 8) {
        throw "Seed-tree refill index $index uses unexpected buffer offset `$$($offset.ToString('x2'))."
    }
    $seedTreeRefillRows.Add(
        "$index`t$(($location -shr 8) -band 1)`t$(($location -band 0xff).ToString('x2'))")
}

$seedTreeOamData = @(
    @(0x12, 0x02, 0x0029),
    @(0x14, 0x03, 0x0029),
    @(0x16, 0x01, 0x002b),
    @(0x18, 0x01, 0x002c),
    @(0x1a, 0x00, 0x002a))
$seedTreeTypeRows = [Collections.Generic.List[string]]::new()
$seedTreeTypeRows.Add(
    "# type`ttreasure-id`ttile-base`tpalette`tintro-text-id`tintro-message-base64")
$seedTreeBaseTile = [Convert]::ToInt32(
    $seedTreePartData.Groups['tile'].Value, 16)
for ($type = 0; $type -lt $seedTreeOamData.Count; $type++) {
    $row = $seedTreeOamData[$type]
    $textId = [int]$row[2]
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Missing seed-tree intro text TX_$($textId.ToString('x4'))."
    }
    $message = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $seedTreeTypeRows.Add(
        "$type`t$((0x20 + $type).ToString('x2'))`t$($seedTreeBaseTile + [int]$row[0])`t$([int]$row[1])`t$($textId.ToString('x4'))`t$message")
}
if (-not $allTexts.ContainsKey(0x0035)) {
    throw 'Missing seed-tree no-satchel text TX_0035.'
}
$seedTreeNoSatchelMessage = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($allTexts[0x0035]))
$seedTreeConstantsRows = @(
    "# key`tvalue",
    "tree-top-left-tile`t110",
    "seed-count`t3",
    "collision-radius-y`t3",
    "collision-radius-x`t3",
    "link-radius`t6",
    "initial-speed-z`t-320",
    "speed-raw`t40",
    "gravity`t32",
    "collision-delay`t2",
    "treasure-parameter`t6",
    "collection-sound`t94",
    "no-satchel-text-id`t53",
    "initial-refill-byte-0`t240",
    "initial-refill-byte-1`t255")
$seedTreeVisualRows = @(
    "# sprite`tanimation`tno-satchel-message-base64",
    "$($gfxNames[0x78])`t$seedTreeAnimation`t$seedTreeNoSatchelMessage")

foreach ($table in @(
    @('objects\seed_trees.tsv', $seedTreePlacementRows),
    @('metadata\seed_tree_refills.tsv', $seedTreeRefillRows),
    @('metadata\seed_tree_types.tsv', $seedTreeTypeRows),
    @('metadata\seed_tree_constants.tsv', $seedTreeConstantsRows),
    @('metadata\seed_tree_visual.tsv', $seedTreeVisualRows)
)) {
    [IO.File]::WriteAllLines(
        (Join-Path $destination $table[0]),
        $table[1],
        [Text.UTF8Encoding]::new($false))
}
Copy-EnemySprite $gfxNames[0x78]
