# INTERAC_GASHA_SPOT ($b6) is a shared native state machine backed by 16
# positioned Ages objects, room-load graphics changes, five probability
# tables, and the common random-ring tier table. Keep those source tables
# together in generated records so runtime code never reconstructs them from
# handwritten room exceptions.
$gashaSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\gashaSpot.s')
$gashaRoomGfxSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\ages\roomGfxChanges.s')
$gashaTreasureSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\treasureAndDrops.s')
$gashaRingSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\rings.s')
$gashaTileSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\tileIndices.s')
$gashaMusicSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\music.s')
$gashaBank1Source = Get-Content -Raw (
    Join-Path $Disassembly 'code\bank1.s')

if ($gashaSource -notmatch '(?ms)^interactionCodeb6:.*?wGashaSpotsPlantedBitset.*?cp 40.*?PART_GASHA_TREE.*?TX_3509.*?TILEINDEX_SOFT_SOIL_PLANTED.*?sub \$01\s+daa.*?ld bc,-\$140.*?SPEED_100.*?TX_3501' -or
    $gashaSource -notmatch '(?ms)^@state5:.*?bit 0,\(hl\).*?GASHATREASURE_TIER3_RING.*?^@gashaMaturityValues:.*?300/2.*?200/2.*?120/2.*?40/2.*?0/2' -or
    $gashaSource -notmatch '(?ms)^@state6:.*?wDisplayedRupees.*?wDisplayedHearts.*?SND_FAIRYCUTSCENE.*?^@state7:.*?ld a,\$08.*?cp \$0a.*?wGashaSpotsPlantedBitset.*?unsetFlag' -or
    $gashaRoomGfxSource -notmatch '(?ms)^gashaSpotRooms:\s*\.db \$05 \$2c \$30 \$7b \$90 \$ad \$cb \$d7.*?\.db \$01 \$0a \$28 \$34 \$55 \$95 \$d0 \$ca' -or
    $gashaBank1Source -notmatch '(?ms)ld a,\$05\s+call addToGashaMaturity') {
    throw 'Gasha spot planting, growth, harvest, room-load maturity, or disappearance behavior changed.'
}

function Get-GashaConstant([string]$source, [string]$name) {
    $match = [regex]::Match(
        $source,
        ('(?m)^\s*\.define\s+{0}\s+\$(?<value>[0-9a-f]+)' -f
            [regex]::Escape($name)))
    if (-not $match.Success) { throw "Could not resolve Gasha constant $name." }
    return [Convert]::ToInt32($match.Groups['value'].Value, 16)
}

$softSoil = Get-GashaConstant $gashaTileSource 'TILEINDEX_SOFT_SOIL'
$plantedSoil = Get-GashaConstant $gashaTileSource 'TILEINDEX_SOFT_SOIL_PLANTED'
$treeTopLeft = Get-GashaConstant $gashaTileSource 'TILEINDEX_GASHA_TREE_TL'
if ($gashaMusicSource -notmatch '(?m)^\s*SND_GETSEED\s+db\s+; \$5e' -or
    $gashaMusicSource -notmatch '(?m)^\s*SND_FAIRYCUTSCENE\s+db\s+; \$91' -or
    $gashaMusicSource -notmatch '(?m)^\s*SND_COMPASS\s+db\s+; \$a2') {
    throw 'Gasha sound IDs changed.'
}

$gashaRanks = @(1, 2, 2, 1, 4, 1, 1, 0, 3, 2, 2, 1, 4, 3, 1, 0)
$gashaGrounds = @(
    'grass', 'dirt', 'dirt', 'grass', 'grass', 'sand', 'grass', 'sand',
    'dirt', 'grass', 'grass', 'dirt', 'grass', 'grass', 'grass', 'sand')
$gashaReplacements = @(0x3a,0x1b,0x1b,0x3a,0x3a,0xbf,0x3a,0xbf,0x1b,0x3a,0x3a,0x1b,0x3a,0x3a,0x3a,0xbf)
$gashaExpectedRooms = @(
    @(0,0x05),@(0,0x2c),@(0,0x30),@(0,0x7b),
    @(0,0x90),@(0,0xad),@(0,0xcb),@(0,0xd7),
    @(1,0x01),@(1,0x0a),@(1,0x28),@(1,0x34),
    @(1,0x55),@(1,0x95),@(1,0xd0),@(1,0xca))
$gashaSpotRows = [Collections.Generic.List[string]]::new()
$gashaSpotRows.Add("# group`troom`tsubid`ty`tx`trank`tground`treplacement")
for ($subid = 0; $subid -lt 16; $subid++) {
    $group = [int]$gashaExpectedRooms[$subid][0]
    $room = [int]$gashaExpectedRooms[$subid][1]
    $block = [regex]::Match(
        $mainObjectSource,
        "(?ms)^group${group}Map$($room.ToString('x2'))ObjectData:(?<body>.*?)(?=^group[0-9]Map[0-9a-f]{2}ObjectData:|\z)")
    if (-not $block.Success) {
        throw "Could not resolve Gasha room $group`:$($room.ToString('x2'))."
    }
    $placement = [regex]::Match(
        $block.Groups['body'].Value,
        ('obj_Interaction\s+\$b6\s+\${0}\s+\$(?<y>[0-9a-f]{{2}})\s+\$(?<x>[0-9a-f]{{2}})' -f
            $subid.ToString('x2')))
    if (-not $placement.Success) {
        throw "Room $group`:$($room.ToString('x2')) is missing INTERAC_GASHA_SPOT `$b6:$($subid.ToString('x2'))."
    }
    $gashaSpotRows.Add(
        "$group`t$($room.ToString('x2'))`t$($subid.ToString('x2'))`t$($placement.Groups['y'].Value)`t$($placement.Groups['x'].Value)`t$($gashaRanks[$subid])`t$($gashaGrounds[$subid])`t$($gashaReplacements[$subid].ToString('x2'))")
}

$probabilities = @(
    @(@(0x5a,0x40,0x26,0,0,0x1a,0x0d,0x0d,0x0c,0),@(0x40,0x26,0x26,0,0,0,0x40,0x26,0x0e,0),@(0x26,0x33,0x33,0,0,0,0x40,0x26,0x0e,0),@(0x1a,0x26,0x26,0,0,0,0x40,0x34,0x26,0),@(0x0c,0x1a,0x1a,0,0,0,0x4d,0x33,0x33,0x0d)),
    @(@(0x1a,0x26,0x5a,0x33,0,0,0x19,0x0d,0x0d,0),@(0x0d,0x1a,0x33,0x40,0,0,0x26,0x33,0x0d,0),@(0x08,0x12,0x33,0x33,0,0,0x26,0x33,0x1a,0x0d),@(0x05,0x0d,0x1a,0x3b,0,0,0x26,0x33,0x26,0x1a),@(0x03,0x08,0x0f,0x19,0,0,0x1a,0x40,0x4d,0x26)),
    @(@(0,0,0x26,0x4d,0x66,0,0x0d,0x0d,0x0d,0),@(0,0,0x1a,0x32,0x4d,0,0x33,0x1a,0x1a,0),@(0,0,0x0d,0x1a,0x26,0,0x40,0x33,0x33,0x0d),@(0,0,0x08,0x12,0x1a,0,0x40,0x33,0x33,0x26),@(0,0,0x03,0x0d,0x0d,0,0x1a,0x4b,0x4b,0x33)),
    @(@(0,0,0,0x5a,0x5a,0,0x1a,0x1a,0x0c,0x0c),@(0,0,0,0x33,0x33,0,0x33,0x33,0x1a,0x1a),@(0,0,0,0x26,0x26,0,0x26,0x33,0x34,0x27),@(0,0,0,0x1a,0x1a,0,0x1a,0x4d,0x32,0x33),@(0,0,0,0x0d,0x0d,0,0x1a,0x40,0x40,0x4c)),
    @(@(0,0,0,0x40,0x34,0,0x26,0x26,0x26,0x1a),@(0,0,0,0x26,0x26,0,0x26,0x33,0x34,0x27),@(0,0,0,0x1a,0x26,0,0x26,0x33,0x34,0x33),@(0,0,0,0x12,0x1a,0,0x21,0x33,0x40,0x40),@(0,0,0,0x0d,0x0d,0,0x0d,0x40,0x4c,0x4d)))
$gashaProbabilityRows = [Collections.Generic.List[string]]::new()
$gashaProbabilityRows.Add("# rank`tclass`tw0`tw1`tw2`tw3`tw4`tw5`tw6`tw7`tw8`tw9")
for ($rank = 0; $rank -lt 5; $rank++) {
    for ($class = 0; $class -lt 5; $class++) {
        $weights = @($probabilities[$rank][$class])
        if (($weights | Measure-Object -Sum).Sum -ne 256) {
            throw "Gasha rank $rank class $class weights do not total 256."
        }
        $gashaProbabilityRows.Add("$rank`t$class`t$($weights -join "`t")")
    }
}

$ringIds = @{}
foreach ($match in [regex]::Matches(
    $gashaRingSource,
    '(?m)^\s*(?<name>[A-Z0-9_]*RING[A-Z0-9_]*)\s+db\s+; \$(?<id>[0-9a-f]{2})')) {
    if (-not $ringIds.ContainsKey($match.Groups['name'].Value)) {
        $ringIds[$match.Groups['name'].Value] =
            [Convert]::ToInt32($match.Groups['id'].Value, 16)
    }
}
$ringTiers = @(
    @('EXPERTS_RING','CHARGE_RING','FIRST_GEN_RING','BOMBPROOF_RING','ENERGY_RING','DBL_EDGED_RING','CHARGE_RING','DBL_EDGED_RING'),
    @('POWER_RING_L2','PEACE_RING','HEART_RING_L2','RED_JOY_RING','GASHA_RING','PEACE_RING','WHIMSICAL_RING','PROTECTION_RING'),
    @('MAPLES_RING','TOSS_RING','RED_LUCK_RING','WHISP_RING','ZORA_RING','FIST_RING','QUICKSAND_RING','ROCS_RING'),
    @('CURSED_RING','LIKE_LIKE_RING','BLUE_LUCK_RING','GREEN_HOLY_RING','BLUE_HOLY_RING','RED_HOLY_RING','OCTO_RING','MOBLIN_RING'),
    @('GREEN_RING','RANG_RING_L2'))
$gashaRingTierRows = [Collections.Generic.List[string]]::new()
$gashaRingTierRows.Add("# tier`tindex`tring")
for ($tier = 0; $tier -lt $ringTiers.Count; $tier++) {
    for ($index = 0; $index -lt $ringTiers[$tier].Count; $index++) {
        $name = $ringTiers[$tier][$index]
        if (-not $ringIds.ContainsKey($name) -or
            $gashaTreasureSource -notmatch "(?m)\.db[^\r\n]*\b$([regex]::Escape($name))\b") {
            throw "Could not resolve Gasha ring tier entry $name."
        }
        $gashaRingTierRows.Add("$tier`t$index`t$(([int]$ringIds[$name]).ToString('x2'))")
    }
}

# The b6 OAM base deliberately indexes through labels that follow its first
# four words. Resolve it as one contiguous ROM stream, like INTERAC_TREASURE.
$gashaOamBase = [regex]::Match(
    $interactionAnimationSource,
    '(?m)^interactionb6OamDataPointers:[^\r\n]*\r?\n')
if (-not $gashaOamBase.Success) { throw 'Could not resolve INTERAC_GASHA_SPOT OAM base.' }
$gashaOamPointers = @(
    [regex]::Matches(
        $interactionAnimationSource.Substring($gashaOamBase.Index + $gashaOamBase.Length),
        '(?m)^\s*\.dw\s+(?<entry>interactionOamData[0-9a-f]+)') |
        ForEach-Object { $_.Groups['entry'].Value })
function Resolve-GashaAnimation([int]$animationIndex) {
    $animations = $npcAnimationTables['interactionb6Animations']
    if ($animationIndex -lt 0 -or $animationIndex -ge $animations.Count) { return '' }
    $label = $animations[$animationIndex]
    $resolved = [Collections.Generic.List[string]]::new()
    foreach ($frame in $npcAnimationFrames[$label]) {
        $pointerIndex = [int]($frame.PointerOffset / 2)
        if ($pointerIndex -lt 0 -or $pointerIndex -ge $gashaOamPointers.Count) { continue }
        $oamLabel = $gashaOamPointers[$pointerIndex]
        $oam = if ($npcOamBlocks.ContainsKey($oamLabel)) { $npcOamBlocks[$oamLabel] } else { '' }
        $metadata = if ([int]$frame.Parameter -eq 0) { "$($frame.Duration)" } else { "$($frame.Duration),$($frame.Parameter)" }
        $resolved.Add("$metadata@$oam")
    }
    $encoded = $resolved -join '|'
    $loopStart = $npcAnimationLoopStarts[$label]
    if ($loopStart -gt 0) { $encoded += "~$loopStart" }
    return $encoded
}

$gashaTreasurePairs = @(
    @(0x2b,0x01),@(0x2d,0x00),@(0x2d,0x01),@(0x2d,0x02),@(0x2d,0x03),
    @(0x2d,0x04),@(0x2f,0x01),@(0x28,0x07),@(0x29,0x18),@(0x29,0x14))
$gashaTextIds = @(0x3503,0x3504,0x3504,0x3504,0x3504,0x3504,0x3505,0x3506,0x3508,0x3507)
$gashaRewardRows = [Collections.Generic.List[string]]::new()
$gashaRewardRows.Add("# type`ttreasure-id`tparameter`ttext-id`tsprite`ttile-base`tpalette`tanimation")
for ($type = 0; $type -lt 10; $type++) {
    $graphic = $interactionGraphics["182`:$type"]
    if (-not $graphic -or -not $gfxNames.ContainsKey($graphic.Gfx)) {
        throw "Could not resolve INTERAC_GASHA_SPOT reward visual $type."
    }
    $animation = Resolve-GashaAnimation $graphic.DefaultAnimation
    if ([string]::IsNullOrWhiteSpace($animation)) {
        throw "Could not resolve INTERAC_GASHA_SPOT reward animation $type."
    }
    $pair = $gashaTreasurePairs[$type]
    $gashaRewardRows.Add(
        "$type`t$(([int]$pair[0]).ToString('x2'))`t$(([int]$pair[1]).ToString('x2'))`t$($gashaTextIds[$type].ToString('x4'))`t$($gfxNames[$graphic.Gfx])`t$($graphic.TileBase)`t$($graphic.Palette)`t$animation")
}
$nutGraphic = $interactionGraphics['182:10']
$nutAnimation = Resolve-GashaAnimation $nutGraphic.DefaultAnimation
if (-not $nutGraphic -or $nutGraphic.Gfx -ne 0x81 -or $nutGraphic.TileBase -ne 0x18 -or
    $nutGraphic.Palette -ne 5 -or [string]::IsNullOrWhiteSpace($nutAnimation)) {
    throw 'Could not resolve INTERAC_GASHA_SPOT nut visual `$b6:$0a.'
}
$gashaNutRows = @(
    "# sprite`ttile-base`tpalette`tanimation",
    "$($gfxNames[$nutGraphic.Gfx])`t$($nutGraphic.TileBase)`t$($nutGraphic.Palette)`t$nutAnimation")

$gashaTextRows = [Collections.Generic.List[string]]::new()
$gashaTextRows.Add("# text-id`tmessage-base64")
foreach ($textId in @(0x0049,0x3500,0x3501,0x3503,0x3504,0x3505,0x3506,0x3507,0x3508,0x3509)) {
    if (-not $allTexts.ContainsKey($textId)) { throw "Missing Gasha text TX_$($textId.ToString('x4'))." }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $gashaTextRows.Add("$($textId.ToString('x4'))`t$encoded")
}

$gashaFrameMaps = @(
    @(0x20,0x21,0x22,0x23,0x24,0x25,0x26,0x27,0x28,0x29,0x2a,0x2b,0x2c,0x2d,0x2e,0x2f),
    @(0x20,0x21,0x22,0x23,0x24,0x25,0x26,0x27,0x28,0x29,0x2a,0x2b,0x2c,0x2d,0x2e,0x2f),
    @(0x20,0x21,0x20,0x21,0x24,0x25,0x26,0x27,0x28,0x29,0x2a,0x2b,0x20,0x2c,0x2d,0x2e),
    @(0x20,0x21,0x20,0x21,0x24,0x25,0x26,0x27,0x28,0x29,0x2a,0x2b,0x22,0x2c,0x2d,0x23),
    @(0x20,0x21,0x20,0x21,0x22,0x24,0x25,0x23,0x20,0x26,0x27,0x21,0x22,0x28,0x29,0x23),
    @(0x20,0x21,0x20,0x21,0x22,0x23,0x22,0x23,0x20,0x24,0x25,0x21,0x22,0x26,0x27,0x23),
    @(0x20,0x21,0x20,0x21,0x22,0x23,0x22,0x23,0x20,0x24,0x25,0x21,0x22,0x26,0x27,0x23),
    @(0x20,0x21,0x20,0x21,0x22,0x23,0x22,0x23,0x20,0x21,0x20,0x21,0x22,0x24,0x25,0x23),
    @(0x20,0x21,0x20,0x21,0x22,0x23,0x22,0x23,0x20,0x21,0x20,0x21,0x22,0x23,0x22,0x23))
$gashaSources = @(@(0,16,0),@(16,16,0),@(32,11,4),@(43,10,4),@(53,6,4),@(59,4,4),@(63,4,4),@(67,2,4),@(67,2,4))
$gashaDisappearanceRows = [Collections.Generic.List[string]]::new()
$gashaDisappearanceRows.Add("# phase`tsource-start`tsource-count`tdestination-start`tt0`tt1`tt2`tt3`tt4`tt5`tt6`tt7`tt8`tt9`tt10`tt11`tt12`tt13`tt14`tt15")
for ($phase = 0; $phase -lt 9; $phase++) {
    $source = $gashaSources[$phase]
    $tiles = $gashaFrameMaps[$phase] | ForEach-Object { ([int]$_).ToString('x2') }
    $gashaDisappearanceRows.Add("$($phase + 1)`t$($source[0])`t$($source[1])`t$($source[2])`t$($tiles -join "`t")")
}

$gashaConstantsRows = @(
    "# key`tvalue",
    "soft-soil`t$softSoil",
    "planted-soil`t$plantedSoil",
    "tree-top-left`t$treeTopLeft",
    "sprout-kills`t20",
    "nut-kills`t40",
    "harvest-maturity-cost`t200",
    "room-load-maturity`t5",
    "nut-speed-raw`t40",
    "nut-speed-z`t-320",
    "nut-gravity`t32",
    "disappearance-period`t8",
    "disappearance-phases`t9",
    "planted-bitset-address`t50765",
    "spot-flags-address`t50764",
    "kill-counters-address`t50767")

$gashaMaturityRows = @(
    "# treasure-id`tmode`tamount",
    "40`tfixed`t150",
    "2b`tfixed`t36",
    "41`tfixed`t100",
    "29`tparameter`t4")

foreach ($table in @(
    @('objects\gasha_spots.tsv',$gashaSpotRows),
    @('metadata\gasha_probabilities.tsv',$gashaProbabilityRows),
    @('metadata\gasha_ring_tiers.tsv',$gashaRingTierRows),
    @('metadata\gasha_rewards.tsv',$gashaRewardRows),
    @('metadata\gasha_nut.tsv',$gashaNutRows),
    @('metadata\gasha_text.tsv',$gashaTextRows),
    @('metadata\gasha_disappearance.tsv',$gashaDisappearanceRows),
    @('metadata\gasha_constants.tsv',$gashaConstantsRows),
    @('metadata\treasure_gasha_maturity.tsv',$gashaMaturityRows))) {
    [IO.File]::WriteAllLines(
        (Join-Path $destination $table[0]),
        $table[1],
        [Text.UTF8Encoding]::new($false))
}

foreach ($asset in @(
    @('gfx_compressible\common\gfx_gasha_tree.png','gfx\gfx_gasha_tree.png'),
    @('gfx_compressible\common\spr_grass_tuft.png','gfx\spr_grass_tuft.png'),
    @('gfx_compressible\common\gfx_sand.png','gfx\gfx_sand.png'),
    @('gfx_compressible\common\gfx_dirt.png','gfx\gfx_dirt.png'))) {
    Copy-GeneratedFile $asset[0] $asset[1]
}
foreach ($spriteName in @('spr_common_items','spr_quest_items_5','spr_quest_items_2')) {
    $sourceSprite = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
        ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$spriteName.png" } |
        Select-Object -First 1
    if ($null -eq $sourceSprite) { throw "Gasha sprite not found: $spriteName.png" }
    Copy-Item -LiteralPath $sourceSprite.FullName -Destination (
        Join-Path $destination "gfx\$spriteName.png") -Force
}
