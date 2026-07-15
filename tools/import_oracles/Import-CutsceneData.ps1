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

# CUTSCENE_TIMEWARP uses INTERAC_TIMEWARP ($dd), PART_TIMEWARP_ANIMATION
# ($2b), and INTERAC_SPARKLE ($84:$01) after a portal spawner transfers Link
# to its center. Export the complete source/destination sprite records and the
# two PALH_c1/PALH_c2 beam palettes; runtime should not approximate the effect
# with a full-screen color fade.
$timeWarpSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\timewarp.s')
$timeWarpCutsceneSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$linkWarpSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\specialObjects\link.s')
$partDataSourceForTimeWarp = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\partData.s')
$timeWarpPartSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\parts\timewarpAnimation.s')
$sparkleSourceForTimeWarp = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\sparkle.s')

$timeWarpGraphics = @($interactionGraphics['221:0'], $interactionGraphics['221:1'])
$timeWarpTrailGraphic = $interactionGraphics['221:2']
$timeWarpBeamGraphic = $interactionGraphics['221:3']
$sparkleGraphic = $interactionGraphics['132:1']
if ($timeWarpGraphics.Count -ne 2 -or
    $timeWarpGraphics[0].Gfx -ne 0x6a -or $timeWarpGraphics[1].Gfx -ne 0x6a -or
    $timeWarpGraphics[0].TileBase -ne 0 -or $timeWarpGraphics[0].Palette -ne 0 -or
    $timeWarpTrailGraphic.Gfx -ne 0 -or $timeWarpTrailGraphic.TileBase -ne 0x10 -or
    $timeWarpTrailGraphic.Palette -ne 3 -or
    $timeWarpBeamGraphic.Gfx -ne 0x6a -or $timeWarpBeamGraphic.Palette -ne 7 -or
    $sparkleGraphic.Gfx -ne 0x6b -or $sparkleGraphic.TileBase -ne 0x0a -or
    $sparkleGraphic.Palette -ne 2) {
    throw 'INTERAC_TIMEWARP / INTERAC_SPARKLE graphics no longer match the time-portal effect.'
}

$timeWarpAnimations = @(0..5 | ForEach-Object { Resolve-NpcAnimation 0xdd $_ })
$sparkleAnimation = Resolve-NpcAnimation 0x84 $sparkleGraphic.DefaultAnimation
if (($timeWarpAnimations | Where-Object { -not $_ }).Count -ne 0 -or
    -not $sparkleAnimation) {
    throw 'Could not resolve all six INTERAC_TIMEWARP animations and sparkle animation $01.'
}

$timeWarpPart = [regex]::Match(
    $partDataSourceForTimeWarp,
    '(?m)^\s*\.db \$(?<gfx>[0-9a-f]{2}) \$00 \$00 \$00 \$40 \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2}) \$00\s*; \$2b')
if (-not $timeWarpPart.Success -or
    [Convert]::ToInt32($timeWarpPart.Groups['gfx'].Value, 16) -ne 0x6a -or
    [Convert]::ToInt32($timeWarpPart.Groups['tile'].Value, 16) -ne 0x1e -or
    [Convert]::ToInt32($timeWarpPart.Groups['flags'].Value, 16) -ne 0x04) {
    throw 'PART_TIMEWARP_ANIMATION no longer resolves to gfx $6a, tile base $1e, palette $04.'
}

# The original Object.visible low bits place the circular $dd:$00/$01 object
# and $2b particles below Link, while the purple $dd:$03/$04 beam, rising
# $dd:$02 trail, and its $84:$01 sparkles are drawn in front of him.
$timeWarpPriorityMatches = @(
    [regex]::Match($timeWarpSource,
        '(?ms)^timewarp_common_state0:.*?objectSetVisible8(?<priority>[0-3])'),
    [regex]::Match($timeWarpSource,
        '(?ms)^itemwarp_subid3Or4_state0:.*?objectSetVisible8(?<priority>[0-3])'),
    [regex]::Match($timeWarpSource,
        '(?ms)^timewarp_subid2:.*?@state0:.*?objectSetVisible8(?<priority>[0-3])'),
    [regex]::Match($timeWarpPartSource,
        '(?ms)^partCode2b:.*?objectSetVisible8(?<priority>[0-3])'),
    [regex]::Match($sparkleSourceForTimeWarp,
        '(?ms)^@initSubid00:\s*^@initSubid01:.*?objectSetVisible8(?<priority>[0-3])')
)
if (($timeWarpPriorityMatches | Where-Object { -not $_.Success }).Count -ne 0) {
    throw 'Could not resolve all time-warp Object.visible draw priorities.'
}
$timeWarpPriorities = @($timeWarpPriorityMatches | ForEach-Object {
    [Convert]::ToInt32($_.Groups['priority'].Value, 16)
})
if (($timeWarpPriorities -join ',') -ne '3,2,1,3,1') {
    throw "Time-warp ground/beam/trail/particle/sparkle priorities changed from 3,2,1,3,1."
}

$particleBlock = [regex]::Match(
    $timeWarpSource,
    '(?ms)^@data:\s*(?<body>.*?)^timewarp_animateUntilFinished:')
$particleRows = @(
    [regex]::Matches(
        $particleBlock.Groups['body'].Value,
        '(?m)^\s*\.db SPEED_(?<speed>[0-9a-f]+), \$(?<x>[0-9a-f]{2}), \$(?<subid>[0-9a-f]{2}), \$00') |
        ForEach-Object {
            $x = [Convert]::ToInt32($_.Groups['x'].Value, 16)
            if ($x -ge 0x80) { $x -= 0x100 }
            $speedFixed = [Convert]::ToInt32($_.Groups['speed'].Value, 16)
            $subid = [Convert]::ToInt32($_.Groups['subid'].Value, 16)
            "$speedFixed,$x,$subid"
        }
)
if (-not $particleBlock.Success -or $particleRows.Count -ne 8 -or
    ($particleRows -join '|') -ne
        '640,-4,0|704,9,3|576,-9,2|704,4,1|576,-4,0|640,4,1|704,-9,2|576,9,3') {
    throw 'INTERAC_TIMEWARP particle speed/offset/subid table no longer matches its eight records.'
}

# State 1 performs six queued graphics-buffer writes for each of eight masks.
# State 2 then owns independent 120 and 60 update counters. Destination
# transition $06 waits 30, creates the effect, waits 16, and flickers for 30.
if ($timeWarpCutsceneSource -notmatch '(?ms)^func_03_7244:.*?ld a,\$08\s+ld \(\$cbb7\),a.*?@@cbb3_00:.*?@@cbb3_05:.*?ld a,120.*?ld \(wTmpcbb4\),a.*?ld \(hl\),\$3c' -or
    $linkWarpSource -notmatch '(?ms)^warpTransition6:.*?ld \(hl\),\$1e.*?ld \(hl\),\$10.*?SND_TIMEWARP_COMPLETED.*?ld \(hl\),\$1e') {
    throw 'CUTSCENE_TIMEWARP or TRANSITION_DEST_TIMEWARP timing no longer matches 8x6, 120/60, and 30/16/30.'
}
if ($timeWarpCutsceneSource -notmatch '(?ms)^func_03_7244:.*?ld a,\(wTilesetFlags\)\s+and \$80\s+ld a,\$02\s+jr nz,\+\s+dec a\s+\+\s+ld l,Interaction.var03\s+ld \(hl\),a\s+ld \(wcc50\),a' -or
    $linkWarpSource -notmatch '(?ms)^@createDestinationTimewarpAnimation:.*?ld a,\(wcc50\)\s+inc l\s+ld \(hl\),a') {
    throw 'Time-warp PALH_c1/PALH_c2 selection no longer carries the source tileset flag through wcc50.'
}

$timeWarpPalette = [byte[]]::new(24)
$timeWarpOutdoorPalette = Read-PaletteBytes 'paletteData5928' 4
$timeWarpIndoorPalette = Read-PaletteBytes 'paletteData5930' 4
[Array]::Copy($timeWarpOutdoorPalette, 0, $timeWarpPalette, 0, 12)
[Array]::Copy($timeWarpIndoorPalette, 0, $timeWarpPalette, 12, 12)
$timeWarpPalettePath = Join-Path $destination 'metadata\time_warp_palettes.bin'
[IO.File]::WriteAllBytes($timeWarpPalettePath, $timeWarpPalette)

$timeWarpSprite = $gfxNames[0x6a]
$sparkleSprite = $gfxNames[0x6b]
Copy-GeneratedFile "gfx_compressible\ages\$timeWarpSprite.png" "gfx\$timeWarpSprite.png"
Copy-GeneratedFile "gfx_compressible\ages\$sparkleSprite.png" "gfx\$sparkleSprite.png"
$timeWarpRows = @(
    "# timewarp-sprite`tcommon-sprite`tsparkle-sprite`tprimary-tile-base`tprimary-palette`tbeam-palette`ttrail-tile-base`ttrail-palette`tparticle-tile-base`tparticle-palette`tsparkle-tile-base`tsparkle-palette`tprimary-priority`tbeam-priority`ttrail-priority`tparticle-priority`tsparkle-priority`tdissolve-frames`tsource-effect-frames`tsource-trail-frames`tarrival-wait-frames`tarrival-effect-frames`tarrival-flicker-frames`texpand-animation`tcontract-animation`tbeam-intro-animation`tbeam-loop-animation`tbeam-contract-animation`ttrail-animation`tsparkle-animation`tparticles",
    "$timeWarpSprite`tspr_common_sprites`t$sparkleSprite`t$($timeWarpGraphics[0].TileBase)`t$($timeWarpGraphics[0].Palette)`t$($timeWarpBeamGraphic.Palette)`t$($timeWarpTrailGraphic.TileBase)`t$($timeWarpTrailGraphic.Palette)`t$([Convert]::ToInt32($timeWarpPart.Groups['tile'].Value, 16))`t$([Convert]::ToInt32($timeWarpPart.Groups['flags'].Value, 16) -band 7)`t$($sparkleGraphic.TileBase)`t$($sparkleGraphic.Palette)`t$($timeWarpPriorities -join "`t")`t48`t120`t60`t30`t16`t30`t$($timeWarpAnimations[0])`t$($timeWarpAnimations[1])`t$($timeWarpAnimations[2])`t$($timeWarpAnimations[3])`t$($timeWarpAnimations[4])`t$($timeWarpAnimations[5])`t$sparkleAnimation`t$($particleRows -join '|')"
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\timeWarpEffects.tsv'),
    $timeWarpRows,
    [Text.UTF8Encoding]::new($false))

# The first present Maku Tree visit is interaction $87 subid $01, selected
# from room 0:38's $87:$00 object while wMakuTreeState and GLOBALFLAG_0c are
# both clear. Export its complete simulated-input/script timing, all five tree
# animations, text, hardcoded destination, initial PALH_8f load, and four
# cycling background-palette states instead of encoding disassembly-only
# details in runtime code.
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
$makuInitialPaletteMatch = [regex]::Match(
    $makuTreeSource,
    '(?ms)Subid 1 only:.*?ld a,(?<palette>PALH_[A-Za-z0-9_]+)\s+call loadPaletteHeader\s+ld hl,@simulatedInput')
if (-not $makuInitialPaletteMatch.Success -or
    $makuInitialPaletteMatch.Groups['palette'].Value -ne 'PALH_8f') {
    throw 'Could not resolve the Maku Tree disappearance initial PALH_8f load.'
}
$makuPaletteSymbols = @('PALH_9a', 'PALH_c4', 'PALH_8f', 'PALH_c5')
$makuPaletteTableMatch = [regex]::Match(
    $makuCutsceneSource,
    '(?ms)@paletteHeaders:\s*\.db\s+\$9a\s+\$c4\s+\$8f\s+\$c5')
if (-not $makuPaletteTableMatch.Success) {
    throw 'Could not resolve the Maku Tree $9a/$c4/$8f/$c5 palette cycle.'
}
$makuInitialPaletteIndex = [Array]::IndexOf(
    $makuPaletteSymbols, $makuInitialPaletteMatch.Groups['palette'].Value)
if ($makuInitialPaletteIndex -lt 0) {
    throw 'The initial Maku Tree palette is absent from its cycling palette table.'
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
    $makuInitialPaletteIndex.ToString(),
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
    "# group`troom`tid`tsubid`tinitial-palette`tinput-idle`tinput-right`tinput-stop`tinput-up`tinput-tail`tintro-delay`tpost-intro`tfrown-delay`tdisappearance`tpost-ahh`tfinish-delay`tsource-transition`tdestination-group`tdestination-room`tdestination-position`tdestination-parameter`tdestination-transition`tanimation0`tanimation1`tanimation2`tanimation3`tanimation4`textra-sprite`ttextbox-position`tintro-base64`tahh-base64`thelp-base64",
    ($makuColumns -join "`t")
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\maku_tree_cutscene.tsv'),
    $makuEventRows,
    [Text.UTF8Encoding]::new($false))

$makuPaletteLabels = [Collections.Generic.List[string]]::new()
foreach ($symbol in $makuPaletteSymbols) {
    $headerMatch = [regex]::Match(
        $paletteHeaderSource,
        "(?ms)^m_PaletteHeaderStart\s+\`$[0-9a-f]{2},[ \t]*$([regex]::Escape($symbol))(?<body>.*?)(?=^m_PaletteHeaderStart|\z)")
    if (-not $headerMatch.Success) {
        throw "Maku Tree palette header not found: $symbol"
    }
    $background = [regex]::Match(
        $headerMatch.Groups['body'].Value,
        'm_PaletteHeaderBg\s+2,\s*(?<count>[46]),\s*(?<label>paletteData[0-9a-f]+)')
    $expectedPaletteCount = if ($symbol -eq 'PALH_8f') { 6 } else { 4 }
    if (-not $background.Success -or
        [int]$background.Groups['count'].Value -ne $expectedPaletteCount) {
        throw "$symbol did not load the expected $expectedPaletteCount Maku Tree BG palettes."
    }
    $makuPaletteLabels.Add($background.Groups['label'].Value)
}
$makuBasePaletteLabel = $makuPaletteLabels[$makuInitialPaletteIndex]
$makuPaletteColors = @{}
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
    $expectedColors = if ($label -eq $makuBasePaletteLabel) { 24 } else { 16 }
    if ($colors.Count -lt $expectedColors) {
        throw "$label contains fewer than $expectedColors Maku Tree background colors."
    }
    $makuPaletteColors[$label] = $colors
}
$makuPaletteBytes = [Collections.Generic.List[byte]]::new()
foreach ($label in $makuPaletteLabels) {
    for ($color = 0; $color -lt 24; $color++) {
        # PALH_9a/PALH_c4/PALH_c5 replace BG palettes 2-5 only. Palettes
        # 6-7 retain the values installed by the initial PALH_8f load.
        $sourceLabel = if ($color -lt 16) { $label } else { $makuBasePaletteLabel }
        $sourceColor = $makuPaletteColors[$sourceLabel][$color]
        $makuPaletteBytes.Add([Convert]::ToByte($sourceColor.Groups['r'].Value, 16))
        $makuPaletteBytes.Add([Convert]::ToByte($sourceColor.Groups['g'].Value, 16))
        $makuPaletteBytes.Add([Convert]::ToByte($sourceColor.Groups['b'].Value, 16))
    }
}
if ($makuPaletteBytes.Count -ne 288) {
    throw "Expected 288 Maku Tree disappearance palette bytes, got $($makuPaletteBytes.Count)."
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
    (Join-Path $destination 'cutscenes\ralph_portal_event.tsv'),
    $ralphEventRows,
    [Text.UTF8Encoding]::new($false))

# The first arrival in the past is INTERAC_MALE_VILLAGER ($3a:$0d) in room
# 1:39. Its leading wait advances while TRANSITION_DEST_TIMEWARP finishes, so
# export the script counters, jump physics, speeds, path, animations, text,
# sound, completion flag, and expected arrival overlap as one checked record.
$enterPastVillagerSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\villager.s')
$enterPastScriptMatch = [regex]::Match(
    $ralphScriptSource,
    '(?ms)^villagerSubid0dScript:(?<body>.*?)(?=^; =+\s*^; INTERAC_FEMALE_VILLAGER)')
if (-not $enterPastScriptMatch.Success) {
    throw 'Could not parse villagerSubid0dScript.'
}
$enterPastBody = $enterPastScriptMatch.Groups['body'].Value
$enterPastCommands = [regex]::Match(
    $enterPastBody,
    '(?ms)^\s*jumpifglobalflagset\s+(?<guard>[A-Z0-9_]+),\s*stubScript\s+setdisabledobjectsto11\s+wait\s+(?<intro>\d+)\s+disableinput\s+wait\s+(?<preJump>\d+)\s+callscript\s+jumpAndWaitUntilLanded\s+wait\s+(?<postJump>\d+)\s+showtext\s+TX_(?<text>[0-9a-f]{4})\s+wait\s+(?<postText>\d+)\s+setspeed\s+(?<fast1>[A-Z0-9_]+)\s+movedown\s+\$(?<firstDown>[0-9a-f]{2})\s+moveright\s+\$(?<right>[0-9a-f]{2})\s+movedown\s+\$(?<secondDown>[0-9a-f]{2})\s+setspeed\s+(?<slow>[A-Z0-9_]+)\s+applyspeed\s+\$(?<slowDown>[0-9a-f]{2})\s+setspeed\s+(?<fast2>[A-Z0-9_]+)\s+applyspeed\s+\$(?<finalDown>[0-9a-f]{2})\s+setglobalflag\s+(?<finish>[A-Z0-9_]+)\s+enableinput\s+scriptend\s*$')
if (-not $enterPastCommands.Success -or
    $enterPastCommands.Groups['guard'].Value -ne 'GLOBALFLAG_ENTER_PAST_CUTSCENE_DONE' -or
    $enterPastCommands.Groups['finish'].Value -ne 'GLOBALFLAG_ENTER_PAST_CUTSCENE_DONE' -or
    $enterPastCommands.Groups['fast1'].Value -ne 'SPEED_100' -or
    $enterPastCommands.Groups['fast2'].Value -ne 'SPEED_100' -or
    $enterPastCommands.Groups['slow'].Value -ne 'SPEED_080') {
    throw 'Could not parse the first-past-arrival script command sequence.'
}
if ($enterPastVillagerSource -notmatch
        '(?ms)^@initSubid0d:\s*call @loadScript\s+jr @state1' -or
    $enterPastVillagerSource -notmatch
        '(?ms)^@runSubid0d:\s*call interactionRunScript\s+jp c,interactionDelete\s+call interactionAnimateBasedOnSpeed\s+jp interactionPushLinkAwayAndUpdateDrawPriority') {
    throw 'INTERAC_MALE_VILLAGER $3a:$0d no longer runs, animates, pushes Link, and deletes in the expected order.'
}

$enterPastNpcRow = $npcRows | Where-Object { $_ -match '^1\t39\t3a\t0d\t' } |
    Select-Object -First 1
if (-not $enterPastNpcRow) {
    throw 'The positioned INTERAC_MALE_VILLAGER $3a:$0d record in room 1:39 was not extracted.'
}
$enterPastNpcColumns = $enterPastNpcRow -split "`t"
if ($enterPastNpcColumns[4] -ne '28' -or $enterPastNpcColumns[5] -ne '18') {
    throw 'INTERAC_MALE_VILLAGER $3a:$0d moved from original position $28/$18.'
}

$enterPastFlagMatch = [regex]::Match(
    $globalFlagSource,
    '(?m)^\s*GLOBALFLAG_ENTER_PAST_CUTSCENE_DONE\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $enterPastFlagMatch.Success -or
    $enterPastFlagMatch.Groups['value'].Value -ne '41') {
    throw 'GLOBALFLAG_ENTER_PAST_CUTSCENE_DONE no longer resolves to $41.'
}
$enterPastSlowSpeedMatch = [regex]::Match(
    $speedSource,
    '(?m)^\s*SPEED_80\s+dsb\s+\d+\s*;\s*0x(?<value>[0-9a-f]{2})')
if (-not $enterPastSlowSpeedMatch.Success -or
    $enterPastSlowSpeedMatch.Groups['value'].Value -ne '14' -or
    $speedSource -notmatch '(?m)^\s*\.define\s+SPEED_080\s+SPEED_80\s*$') {
    throw 'SPEED_080 no longer aliases original object speed $14.'
}

$enterPastHelperSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$enterPastJumpMatch = [regex]::Match(
    $enterPastHelperSource,
    '(?ms)^beginJump:\s*ld h,d\s*ld l,Interaction\.speedZ\s*ld \(hl\),\$(?<low>[0-9a-f]{2})\s*inc hl\s*ld \(hl\),\$(?<high>[0-9a-f]{2})\s*ld a,(?<sound>[A-Z0-9_]+)\s*jp playSound.*?^updateGravity:\s*ld c,\$(?<gravity>[0-9a-f]{2})\s*call objectUpdateSpeedZ_paramC')
if (-not $enterPastJumpMatch.Success -or
    $enterPastJumpMatch.Groups['sound'].Value -ne 'SND_JUMP') {
    throw 'Could not resolve beginJump/updateGravity for the first-past-arrival event.'
}
$enterPastJumpRaw =
    ([Convert]::ToInt32($enterPastJumpMatch.Groups['high'].Value, 16) -shl 8) -bor
    [Convert]::ToInt32($enterPastJumpMatch.Groups['low'].Value, 16)
if ($enterPastJumpRaw -ge 0x8000) { $enterPastJumpRaw -= 0x10000 }
$enterPastGravity = [Convert]::ToInt32(
    $enterPastJumpMatch.Groups['gravity'].Value, 16)
if ($enterPastJumpRaw -ne -0x200 -or $enterPastGravity -ne 0x30) {
    throw 'The first-past-arrival jump changed from speedZ -$0200 and gravity $30.'
}
$enterPastMusicSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\music.s')
$enterPastSoundMatch = [regex]::Match(
    $enterPastMusicSource,
    '(?m)^\s*SND_JUMP\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $enterPastSoundMatch.Success -or
    $enterPastSoundMatch.Groups['value'].Value -ne '53') {
    throw 'SND_JUMP no longer resolves to $53.'
}

$enterPastTextId = [Convert]::ToInt32(
    $enterPastCommands.Groups['text'].Value, 16)
if ($enterPastTextId -ne 0x1622 -or
    -not $allTexts.ContainsKey($enterPastTextId) -or
    $allTextPositions.ContainsKey($enterPastTextId)) {
    throw 'Expected first-past-arrival dialogue TX_1622 without a fixed textbox position.'
}
$enterPastRightAnimation = Resolve-NpcAnimation 0x3a 1
$enterPastDownAnimation = Resolve-NpcAnimation 0x3a 2
if (-not $enterPastRightAnimation -or -not $enterPastDownAnimation) {
    throw 'Could not resolve male villager right/down animations $01/$02.'
}

# Destination room loading performs the script's first update. The remaining
# 32+30+16+30 transition updates install/count wait 100, leaving wait 40 at 33.
$enterPastExpectedArrivalCounter = 33
$enterPastEventColumns = @(
    '1', '39', '3a', '0d',
    $enterPastCommands.Groups['intro'].Value,
    $enterPastCommands.Groups['preJump'].Value,
    $enterPastCommands.Groups['postJump'].Value,
    $enterPastCommands.Groups['postText'].Value,
    $enterPastJumpRaw.ToString(), $enterPastGravity.ToString(),
    $speedMatch.Groups['value'].Value,
    $enterPastSlowSpeedMatch.Groups['value'].Value,
    [Convert]::ToInt32($enterPastCommands.Groups['firstDown'].Value, 16).ToString(),
    [Convert]::ToInt32($enterPastCommands.Groups['right'].Value, 16).ToString(),
    [Convert]::ToInt32($enterPastCommands.Groups['secondDown'].Value, 16).ToString(),
    [Convert]::ToInt32($enterPastCommands.Groups['slowDown'].Value, 16).ToString(),
    [Convert]::ToInt32($enterPastCommands.Groups['finalDown'].Value, 16).ToString(),
    $enterPastFlagMatch.Groups['value'].Value, $enterPastTextId.ToString('x4'),
    $enterPastRightAnimation, $enterPastDownAnimation,
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$enterPastTextId])),
    $enterPastSoundMatch.Groups['value'].Value,
    $enterPastExpectedArrivalCounter.ToString()
)
$enterPastEventRows = @(
    "# group`troom`tid`tsubid`tintro-wait`tpre-jump-wait`tpost-jump-wait`tpost-text-wait`tjump-speed-z`tjump-gravity`tfast-speed`tslow-speed`tfirst-down-counter`tright-counter`tsecond-down-counter`tslow-down-counter`tfinal-down-counter`tglobal-flag`ttext-id`tright-animation`tdown-animation`ttext-base64`tjump-sound`texpected-arrival-counter",
    ($enterPastEventColumns -join "`t")
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\enter_past_event.tsv'),
    $enterPastEventRows,
    [Text.UTF8Encoding]::new($false))

# The first Impa encounter is INTERAC_IMPA_IN_CUTSCENE ($31:$00) in present
# room $6a. It creates three fake Octoroks from extra object data, replaces
# Link with linkCutscene1, runs impaScript0, and finally installs Impa as the
# 16-entry delayed follower. Export every event counter, actor record,
# animation, text, and possessed PALH_97 sprite color used by that slice.
$impaSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\impaInCutscene.s')
$impaFakeSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\fakeOctorok.s')
$impaLinkSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\specialObjects\linkInCutscene.s')
$impaScriptSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$impaExtraObjects = Get-Content -Raw (
    Join-Path $Disassembly 'objects\ages\extraData3.s')

$impaRoomRow = $npcRows | Where-Object { $_ -match '^0\t6a\t31\t00\t' } |
    Select-Object -First 1
if (-not $impaRoomRow) {
    throw 'The positioned INTERAC_IMPA_IN_CUTSCENE $31:$00 record in room 0:6a was not extracted.'
}
$impaRoomColumns = $impaRoomRow -split "`t"
if ($impaRoomColumns[4] -ne '38' -or $impaRoomColumns[5] -ne '48') {
    throw 'INTERAC_IMPA_IN_CUTSCENE $31:$00 moved from original position $38/$48.'
}
$impaInitMatch = [regex]::Match(
    $impaSource,
    '(?ms)@init0:.*?bit 6,a.*?ld a,PALH_(?<palette>[0-9a-f]{2}).*?ld e,Interaction\.oamFlags\s+ld a,\$(?<flags>[0-9a-f]{2}).*?ld hl,objectData\.(?<objects>[A-Za-z0-9_]+).*?ld \(hl\),\$(?<linkSubid>[0-9a-f]{2})')
if (-not $impaInitMatch.Success -or
    $impaInitMatch.Groups['palette'].Value -ne '97' -or
    $impaInitMatch.Groups['flags'].Value -ne '07' -or
    $impaInitMatch.Groups['objects'].Value -ne 'impaOctoroks' -or
    $impaInitMatch.Groups['linkSubid'].Value -ne '01') {
    throw 'Could not parse Impa $31:$00 PALH_97, OAM flags $07, fake Octoroks, and Link subid $01.'
}

$impaLinkBlock = [regex]::Match(
    $impaLinkSource,
    '(?ms)^linkCutscene1:(?<body>.*?)(?=^linkCutscene2:)')
if (-not $impaLinkBlock.Success) { throw 'Could not parse linkCutscene1.' }
$impaLinkMatch = [regex]::Match(
    $impaLinkBlock.Groups['body'].Value,
    '(?ms)ld a,\$(?<initialWait>[0-9a-f]{2}).*?ld \(hl\),SPEED_(?<speed>[0-9a-fA-F_]+).*?cp \$(?<targetX>[0-9a-f]{2}).*?ld \(hl\),\$(?<centerWait>[0-9a-f]{2}).*?ld \(hl\),\$(?<approach>[0-9a-f]{2}).*?ld \(hl\),\$01')
if (-not $impaLinkMatch.Success -or
    $impaLinkMatch.Groups['initialWait'].Value -ne '78' -or
    $impaLinkMatch.Groups['speed'].Value -ne '100' -or
    $impaLinkMatch.Groups['targetX'].Value -ne '48' -or
    $impaLinkMatch.Groups['centerWait'].Value -ne '04' -or
    $impaLinkMatch.Groups['approach'].Value -ne '2e') {
    throw 'linkCutscene1 no longer matches its $78/$48/$04/$2e SPEED_100 entrance.'
}

$impaScriptMatch = [regex]::Match(
    $impaScriptSource,
    '(?ms)^impaScript0:(?<body>.*?)(?=^impaScript_moveAwayFromRock:)')
if (-not $impaScriptMatch.Success) { throw 'Could not parse impaScript0.' }
$impaScriptBody = $impaScriptMatch.Groups['body'].Value
$impaScriptCommand = [regex]::Match(
    $impaScriptBody,
    '(?ms)checkmemoryeq .*?, \$(?<signal>[0-9a-f]{2})\s+wait (?<introWait>\d+)\s+showtextdifferentforlinked TX_(?<text>[0-9a-f]{4}), TX_[0-9a-f]{4}\s+wait (?<postText>\d+)\s+setspeed SPEED_(?<speed>[0-9a-fA-F_]+)\s+movedown \$(?<moveFrames>[0-9a-f]{2})\s+orroomflag \$(?<roomFlag>[0-9a-f]{2})')
if (-not $impaScriptCommand.Success -or
    $impaScriptCommand.Groups['signal'].Value -ne '01' -or
    $impaScriptCommand.Groups['introWait'].Value -ne '210' -or
    $impaScriptCommand.Groups['text'].Value -ne '0102' -or
    $impaScriptCommand.Groups['postText'].Value -ne '30' -or
    $impaScriptCommand.Groups['speed'].Value -ne '080' -or
    $impaScriptCommand.Groups['moveFrames'].Value -ne '20' -or
    $impaScriptCommand.Groups['roomFlag'].Value -ne '40') {
    throw 'impaScript0 no longer matches signal $01, waits 210/30, TX_0102, SPEED_080, movedown $20, and room flag $40.'
}

$impaSpeed80Match = [regex]::Match(
    $speedSource,
    '(?m)^\s*SPEED_80\s+dsb\s+\d+\s*;\s*0x(?<value>[0-9a-f]{2})')
$impaSpeed300Match = [regex]::Match(
    $speedSource,
    '(?m)^\s*SPEED_300\s+dsb\s+\d+\s*;\s*0x(?<value>[0-9a-f]{2})')
if (-not $impaSpeed80Match.Success -or $impaSpeed80Match.Groups['value'].Value -ne '14' -or
    -not $impaSpeed300Match.Success -or $impaSpeed300Match.Groups['value'].Value -ne '78') {
    throw 'SPEED_080/SPEED_300 no longer resolve to original object speeds $14/$78.'
}

$impaTextId = [Convert]::ToInt32($impaScriptCommand.Groups['text'].Value, 16)
if (-not $allTexts.ContainsKey(0x0101) -or -not $allTexts.ContainsKey($impaTextId)) {
    throw 'Could not resolve Impa encounter text TX_0101/TX_0102.'
}
# TX_0102 begins with a text-engine call to TX_0101. Expand it for the runtime
# textbox, which consumes the already-resolved final string rather than text
# bytecode pointers.
$impaText = $allTexts[$impaTextId] -replace '^\\call\(TX_0101\)\r?\n?',
    "$($allTexts[0x0101])`n"
$impaText = $impaText.Replace('\sym(0x57)', [string][char]0x25b2)

# INTERAC_IMPA_IN_CUTSCENE selects animation indices $00-$03 directly from
# Interaction.direction while following Link. The generic room-NPC importer
# deliberately does not infer facings for this scripted, non-talkable actor.
$impaFollowerAnimations = @(0..3 | ForEach-Object {
    Resolve-NpcAnimation 0x31 $_
})
if ($impaFollowerAnimations.Count -ne 4 -or
    $impaFollowerAnimations.Where({ [string]::IsNullOrWhiteSpace($_) }).Count -ne 0) {
    throw 'Could not resolve Impa follower animations $00-$03.'
}

$impaEventColumns = @(
    '0', '6a', '31', '00',
    [Convert]::ToInt32($impaScriptCommand.Groups['roomFlag'].Value, 16).ToString('x2'),
    [Convert]::ToInt32($impaLinkMatch.Groups['initialWait'].Value, 16).ToString(),
    [Convert]::ToInt32($impaLinkMatch.Groups['targetX'].Value, 16).ToString(),
    [Convert]::ToInt32($impaLinkMatch.Groups['centerWait'].Value, 16).ToString(),
    [Convert]::ToInt32($impaLinkMatch.Groups['approach'].Value, 16).ToString(),
    '28',
    $impaScriptCommand.Groups['introWait'].Value,
    $impaTextId.ToString('x4'),
    $impaScriptCommand.Groups['postText'].Value,
    $impaSpeed80Match.Groups['value'].Value,
    [Convert]::ToInt32($impaScriptCommand.Groups['moveFrames'].Value, 16).ToString(),
    '16',
    $impaFollowerAnimations[0],
    $impaFollowerAnimations[1],
    $impaFollowerAnimations[2],
    $impaFollowerAnimations[3],
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($impaText))
)
$impaEventRows = @(
    "# group`troom`tid`tsubid`troom-flag`tlink-wait`ttarget-x`tcenter-wait`tapproach-frames`tlink-speed`timpa-wait`ttext-id`tpost-text`timpa-speed`timpa-move-frames`tfollow-lag`tup-animation`tright-animation`tdown-animation`tleft-animation`ttext-base64",
    ($impaEventColumns -join "`t")
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\impa_intro_event.tsv'),
    $impaEventRows,
    [Text.UTF8Encoding]::new($false))

# Room 0:59 continues the same retained INTERAC_IMPA_IN_CUTSCENE object.
# Export the complete two-object handshake: Impa subid $00/linkCutscene2,
# INTERAC_TRIFORCE_STONE ($34:$00), the post-move PART_TRIFORCE_STONE
# ($5a:$5a), and both Impa scripts on either side of the push.
$impaStoneSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\triforceStone.s')
$impaStonePartSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\parts\triforceStone.s')
$impaScriptHelperSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$musicConstantSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\music.s')

$impaStoneRoomBlock = [regex]::Match(
    ($mainObjectLines -join "`n"),
    '(?ms)^group0Map59ObjectData:(?<body>.*?)(?=^group0Map5aObjectData:)')
$impaStoneRoomInteraction = [regex]::Match(
    $impaStoneRoomBlock.Groups['body'].Value,
    'obj_Interaction \$(?<id>[0-9a-f]{2}) \$(?<subid>[0-9a-f]{2}) \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})')
$impaStoneRoomPart = [regex]::Match(
    $impaStoneRoomBlock.Groups['body'].Value,
    'obj_Part \$(?<id>[0-9a-f]{2}) \$(?<subid>[0-9a-f]{2}) \$(?<position>[0-9a-f]{2})')
if (-not $impaStoneRoomBlock.Success -or -not $impaStoneRoomInteraction.Success -or
    -not $impaStoneRoomPart.Success -or
    $impaStoneRoomInteraction.Groups['id'].Value -ne '34' -or
    $impaStoneRoomInteraction.Groups['subid'].Value -ne '00' -or
    $impaStoneRoomInteraction.Groups['y'].Value -ne '26' -or
    $impaStoneRoomInteraction.Groups['x'].Value -ne '38' -or
    $impaStoneRoomPart.Groups['id'].Value -ne '5a' -or
    $impaStoneRoomPart.Groups['subid'].Value -ne '5a' -or
    $impaStoneRoomPart.Groups['position'].Value -ne '23') {
    throw 'Room 0:59 no longer contains INTERAC_TRIFORCE_STONE $34:$00 at $26/$38 and PART_TRIFORCE_STONE $5a:$5a at $23.'
}

$impaStoneInit = [regex]::Match(
    $impaStoneSource,
    '(?ms)and \$(?<deleteMask>[0-9a-f]{2}).*?Interaction\.collisionRadiusY\s+ld \(hl\),\$(?<radiusY>[0-9a-f]{2})\s+inc l\s+ld \(hl\),\$(?<radiusX>[0-9a-f]{2}).*?ld a,PALH_(?<palette>[0-9a-f]{2})')
$impaStonePush = [regex]::Match(
    $impaStoneSource,
    '(?ms)ld \(hl\),SPEED_40.*?ld \(hl\),\$(?<moveFrames>[0-9a-f]{2}).*?ld \(hl\),\$(?<linkSubid>[0-9a-f]{2}).*?ld \(hl\),SPEED_80.*?ld \(hl\),\$(?<signal>[0-9a-f]{2}).*?ld a,SND_(?<pushSound>[A-Z0-9_]+)')
$impaStoneHold = [regex]::Match(
    $impaStoneSource,
    '(?ms)ld a,\$01\s+ld \(wForceLinkPushAnimation\),a.*?call interactionDecCounter1.*?ld \(wForceLinkPushAnimation\),a\s+ld a,\$(?<frames>[0-9a-f]{2})')
$impaStoneFinish = [regex]::Match(
    $impaStoneSource,
    '(?ms)ld b,\$(?<rightX>[0-9a-f]{2}).*?and \$10.*?ld b,\$(?<leftX>[0-9a-f]{2}).*?ld b,\$(?<leftFlag>[0-9a-f]{2}).*?ld b,\$(?<rightFlag>[0-9a-f]{2}).*?ld a,SNDCTRL_(?<stopSound>[A-Z0-9_]+).*?ld a,SND_(?<solveSound>[A-Z0-9_]+)')
$impaStoneFinalTile = [regex]::Match(
    $impaStoneSource,
    '(?ms)@setSolidTile:.*?ld a,\$(?<tile>[0-9a-f]{2})\s+ld \(bc\),a.*?ld a,\$(?<collision>[0-9a-f]{2})')
if (-not $impaStoneInit.Success -or -not $impaStonePush.Success -or
    -not $impaStoneHold.Success -or -not $impaStoneFinish.Success -or
    -not $impaStoneFinalTile.Success -or
    $impaStoneInit.Groups['deleteMask'].Value -ne 'c0' -or
    $impaStoneInit.Groups['radiusY'].Value -ne '03' -or
    $impaStoneInit.Groups['radiusX'].Value -ne '0a' -or
    $impaStoneInit.Groups['palette'].Value -ne '98' -or
    $impaStonePush.Groups['moveFrames'].Value -ne '40' -or
    $impaStonePush.Groups['linkSubid'].Value -ne '06' -or
    $impaStonePush.Groups['signal'].Value -ne '06' -or
    $impaStonePush.Groups['pushSound'].Value -ne 'MAKUDISAPPEAR' -or
    $impaStoneHold.Groups['frames'].Value -ne '14' -or
    $impaStoneFinish.Groups['rightX'].Value -ne '48' -or
    $impaStoneFinish.Groups['leftX'].Value -ne '28' -or
    $impaStoneFinish.Groups['leftFlag'].Value -ne '40' -or
    $impaStoneFinish.Groups['rightFlag'].Value -ne '80' -or
    $impaStoneFinish.Groups['stopSound'].Value -ne 'STOPSFX' -or
    $impaStoneFinish.Groups['solveSound'].Value -ne 'SOLVEPUZZLE_2' -or
    $impaStoneFinalTile.Groups['tile'].Value -ne '00' -or
    $impaStoneFinalTile.Groups['collision'].Value -ne '0f') {
    throw 'Could not parse the original Triforce-stone radii, 20/64-update push, positions, flags, sounds, or final solid tile.'
}

$impaApproach = [regex]::Match(
    $impaSource,
    '(?ms)^impaCheckApproachedStone:.*?cp \$(?<room>[0-9a-f]{2}).*?cp \$(?<y>[0-9a-f]{2}).*?cp \$(?<x>[0-9a-f]{2})')
$impaStoneSequence = [regex]::Match(
    $impaSource,
    '(?ms); Link has approached the stone; trigger cutscene\..*?ld l,Interaction\.counter1\s+ld \(hl\),\$(?<spotHold>[0-9a-f]{2}).*?ld bc,-\$(?<spotSpeedZ>[0-9a-f]{3}).*?ld c,\$(?<gravity>[0-9a-f]{2}).*?ld \(hl\),\$(?<firstLanding>[0-9a-f]{2}).*?ld \(hl\),\$(?<firstPost>[0-9a-f]{2}).*?TX_(?<firstText>[0-9a-f]{4}).*?ld \(hl\),SPEED_300.*?ldh \(<hFF8B\),a\s+ldbc \$(?<targetY>[0-9a-f]{2}),\$(?<targetX>[0-9a-f]{2}).*?ld \(hl\),\$(?<stoneWait>[0-9a-f]{2}).*?; Start a jump\s+ld \(hl\),\$(?<secondHold>[0-9a-f]{2})\s+ld bc,-\$(?<secondSpeedZ>[0-9a-f]{3}).*?ld c,\$(?<gravity2>[0-9a-f]{2}).*?ld \(hl\),\$(?<secondLanding>[0-9a-f]{2}).*?ld \(hl\),\$(?<signPost>[0-9a-f]{2}).*?TX_(?<signText>[0-9a-f]{4})')
if (-not $impaApproach.Success -or -not $impaStoneSequence.Success -or
    $impaApproach.Groups['room'].Value -ne '59' -or
    $impaApproach.Groups['y'].Value -ne '58' -or
    $impaApproach.Groups['x'].Value -ne '78' -or
    $impaStoneSequence.Groups['spotHold'].Value -ne '1e' -or
    $impaStoneSequence.Groups['spotSpeedZ'].Value -ne '1c0' -or
    $impaStoneSequence.Groups['gravity'].Value -ne '20' -or
    $impaStoneSequence.Groups['firstLanding'].Value -ne '0a' -or
    $impaStoneSequence.Groups['firstPost'].Value -ne '14' -or
    $impaStoneSequence.Groups['firstText'].Value -ne '0104' -or
    $impaStoneSequence.Groups['targetY'].Value -ne '38' -or
    $impaStoneSequence.Groups['targetX'].Value -ne '38' -or
    $impaStoneSequence.Groups['stoneWait'].Value -ne '1e' -or
    $impaStoneSequence.Groups['secondHold'].Value -ne '1e' -or
    $impaStoneSequence.Groups['secondSpeedZ'].Value -ne '180' -or
    $impaStoneSequence.Groups['gravity2'].Value -ne '20' -or
    $impaStoneSequence.Groups['secondLanding'].Value -ne '0a' -or
    $impaStoneSequence.Groups['signPost'].Value -ne '1e' -or
    $impaStoneSequence.Groups['signText'].Value -ne '0105') {
    throw "Could not parse Impa's room `$59 approach, two jumps, target, waits, or TX_0104/TX_0105 (approach=$($impaApproach.Success):$($impaApproach.Groups['room'].Value)/$($impaApproach.Groups['y'].Value)/$($impaApproach.Groups['x'].Value), sequence=$($impaStoneSequence.Success):$($impaStoneSequence.Groups['spotHold'].Value)/$($impaStoneSequence.Groups['spotSpeedZ'].Value)/$($impaStoneSequence.Groups['gravity'].Value)/$($impaStoneSequence.Groups['firstLanding'].Value)/$($impaStoneSequence.Groups['firstPost'].Value)/$($impaStoneSequence.Groups['firstText'].Value)/$($impaStoneSequence.Groups['targetY'].Value)/$($impaStoneSequence.Groups['targetX'].Value)/$($impaStoneSequence.Groups['stoneWait'].Value)/$($impaStoneSequence.Groups['secondHold'].Value)/$($impaStoneSequence.Groups['secondSpeedZ'].Value)/$($impaStoneSequence.Groups['gravity2'].Value)/$($impaStoneSequence.Groups['secondLanding'].Value)/$($impaStoneSequence.Groups['signPost'].Value)/$($impaStoneSequence.Groups['signText'].Value))."
}

$impaMoveAwayBlock = [regex]::Match(
    $impaScriptSource,
    '(?ms)^impaScript_moveAwayFromRock:(?<body>.*?)(?=^impaScript_waitForRockToBeMoved:)')
$impaMoveAway = [regex]::Match(
    $impaMoveAwayBlock.Groups['body'].Value,
    '(?ms)checkmemoryeq .*?, \$(?<signal>[0-9a-f]{2}).*?wait (?<lead>\d+).*?showtext TX_(?<request>[0-9a-f]{4})\s+wait (?<post1>\d+).*?setanimation \$(?<backAnimation>[0-9a-f]{2}).*?setangle \$(?<backAngle>[0-9a-f]{2}).*?setspeed SPEED_(?<backSpeed>[0-9a-fA-F_]+).*?applyspeed \$(?<backFrames1>[0-9a-f]{2})\s+wait (?<between1>\d+)\s+showtext TX_(?<hesitation>[0-9a-f]{4})\s+wait (?<post2>\d+)\s+applyspeed \$(?<backFrames2>[0-9a-f]{2})\s+wait (?<between2>\d+)\s+showtext TX_(?<failure>[0-9a-f]{4})\s+wait (?<post3>\d+).*?\$(?<doneSignal>[0-9a-f]{2})')
if (-not $impaMoveAwayBlock.Success -or -not $impaMoveAway.Success -or
    $impaMoveAway.Groups['signal'].Value -ne '03' -or
    $impaMoveAway.Groups['lead'].Value -ne '10' -or
    $impaMoveAway.Groups['request'].Value -ne '0106' -or
    $impaMoveAway.Groups['post1'].Value -ne '30' -or
    $impaMoveAway.Groups['backAnimation'].Value -ne '01' -or
    $impaMoveAway.Groups['backAngle'].Value -ne '18' -or
    $impaMoveAway.Groups['backSpeed'].Value -ne '080' -or
    $impaMoveAway.Groups['backFrames1'].Value -ne '21' -or
    $impaMoveAway.Groups['between1'].Value -ne '30' -or
    $impaMoveAway.Groups['hesitation'].Value -ne '0107' -or
    $impaMoveAway.Groups['post2'].Value -ne '30' -or
    $impaMoveAway.Groups['backFrames2'].Value -ne '21' -or
    $impaMoveAway.Groups['between2'].Value -ne '30' -or
    $impaMoveAway.Groups['failure'].Value -ne '0108' -or
    $impaMoveAway.Groups['post3'].Value -ne '30' -or
    $impaMoveAway.Groups['doneSignal'].Value -ne '04') {
    throw 'Could not parse impaScript_moveAwayFromRock and its TX_0106/TX_0107/TX_0108 cadence.'
}

$impaRockMovedBlock = [regex]::Match(
    $impaScriptHelperSource,
    '(?ms)^impaScript_rockJustMoved:(?<body>.*?)(?=^; Subid 4:)')
$impaRockMoved = [regex]::Match(
    $impaRockMovedBlock.Groups['body'].Value,
    '(?ms)wait (?<lead>\d+).*?w1Link\.angle, \$(?<rightAngle>[0-9a-f]{2}).*?setangle \$(?<downAngle>[0-9a-f]{2})\s+setspeed SPEED_(?<correctSpeed>[0-9a-fA-F_]+)\s+applyspeed (?<leftCorrect>\d+).*?wait (?<rightWait>\d+).*?wait (?<commonWait>\d+)\s+setangle \$(?<rightMoveAngle>[0-9a-f]{2})\s+setspeed SPEED_(?<rightSpeed>[0-9a-fA-F_]+)\s+applyspeed \$(?<rightFrames>[0-9a-f]{2})\s+wait (?<wait1>\d+).*?moveup \$(?<upFrames>[0-9a-f]{2})\s+wait (?<wait2>\d+).*?\$(?<signal>[0-9a-f]{2})\s+setanimation \$(?<animation>[0-9a-f]{2})\s+wait (?<poseWait>\d+)\s+showtext TX_(?<thanks>[0-9a-f]{4})\s+wait (?<thanksPost>\d+)\s+setspeed SPEED_(?<finalSpeed>[0-9a-fA-F_]+)\s+moveup \$(?<finalFrames>[0-9a-f]{2})')
if (-not $impaRockMovedBlock.Success -or -not $impaRockMoved.Success -or
    $impaRockMoved.Groups['lead'].Value -ne '4' -or
    $impaRockMoved.Groups['rightAngle'].Value -ne '08' -or
    $impaRockMoved.Groups['downAngle'].Value -ne '10' -or
    $impaRockMoved.Groups['correctSpeed'].Value -ne '040' -or
    $impaRockMoved.Groups['leftCorrect'].Value -ne '65' -or
    $impaRockMoved.Groups['rightWait'].Value -ne '65' -or
    $impaRockMoved.Groups['commonWait'].Value -ne '120' -or
    $impaRockMoved.Groups['rightMoveAngle'].Value -ne '08' -or
    $impaRockMoved.Groups['rightSpeed'].Value -ne '100' -or
    $impaRockMoved.Groups['rightFrames'].Value -ne '21' -or
    $impaRockMoved.Groups['wait1'].Value -ne '8' -or
    $impaRockMoved.Groups['upFrames'].Value -ne '11' -or
    $impaRockMoved.Groups['wait2'].Value -ne '8' -or
    $impaRockMoved.Groups['signal'].Value -ne '07' -or
    $impaRockMoved.Groups['animation'].Value -ne '00' -or
    $impaRockMoved.Groups['poseWait'].Value -ne '30' -or
    $impaRockMoved.Groups['thanks'].Value -ne '0109' -or
    $impaRockMoved.Groups['thanksPost'].Value -ne '30' -or
    $impaRockMoved.Groups['finalSpeed'].Value -ne '080' -or
    $impaRockMoved.Groups['finalFrames'].Value -ne '20') {
    throw 'Could not parse scriptHelp.impaScript_rockJustMoved and its direction-dependent response.'
}

$impaLeaveGuard = [regex]::Match(
    $impaSource,
    '(?ms)^impaPreventLinkFromLeavingStoneScreen:.*?ld b,\$(?<y>[0-9a-f]{2}).*?BTN_DOWN.*?ld b,\$(?<x>[0-9a-f]{2}).*?BTN_RIGHT.*?TX_(?<text>[0-9a-f]{4})')
$linkCutscene2Block = [regex]::Match(
    $impaLinkSource,
    '(?ms)^linkCutscene2:(?<body>.*?)(?=^linkCutscene3:)')
$linkCutscene2 = [regex]::Match(
    $linkCutscene2Block.Groups['body'].Value,
    '(?ms)ld bc,\$(?<target>[0-9a-f]{4}).*?^@substate0:.*?ld l,SpecialObject\.yh\s+ldi a,\(hl\)\s+cp \$(?<targetY>[0-9a-f]{2}).*?ld \(hl\),\$(?<axisWait>[0-9a-f]{2}).*?^@gotoState7:.*?ld l,SpecialObject\.counter1\s+ld \(hl\),\$(?<targetWait>[0-9a-f]{2}).*?^@substate7:.*?ld \(hl\),\$(?<faceWait>[0-9a-f]{2}).*?ld hl,\$cfd0\s+ld \(hl\),\$(?<signal>[0-9a-f]{2})')
if (-not $impaLeaveGuard.Success -or -not $linkCutscene2Block.Success -or
    -not $linkCutscene2.Success -or
    $impaLeaveGuard.Groups['y'].Value -ne '76' -or
    $impaLeaveGuard.Groups['x'].Value -ne '96' -or
    $impaLeaveGuard.Groups['text'].Value -ne '010a' -or
    $linkCutscene2.Groups['target'].Value -ne '3838' -or
    $linkCutscene2.Groups['targetY'].Value -ne '48' -or
    $linkCutscene2.Groups['axisWait'].Value -ne '08' -or
    $linkCutscene2.Groups['targetWait'].Value -ne '3c' -or
    $linkCutscene2.Groups['faceWait'].Value -ne '10' -or
    $linkCutscene2.Groups['signal'].Value -ne '03') {
    throw "Could not parse linkCutscene2 target `$38/`$48, 8/60/16 waits, or room-exit guard (guard=$($impaLeaveGuard.Success):$($impaLeaveGuard.Groups['y'].Value)/$($impaLeaveGuard.Groups['x'].Value)/$($impaLeaveGuard.Groups['text'].Value), link=$($linkCutscene2.Success):$($linkCutscene2.Groups['target'].Value)/$($linkCutscene2.Groups['targetY'].Value)/$($linkCutscene2.Groups['axisWait'].Value)/$($linkCutscene2.Groups['targetWait'].Value)/$($linkCutscene2.Groups['faceWait'].Value)/$($linkCutscene2.Groups['signal'].Value))."
}

$impaStonePart = [regex]::Match(
    $impaStonePartSource,
    '(?ms)and \$(?<flags>[0-9a-f]{2}).*?and \$(?<leftMask>[0-9a-f]{2})\s+ld a,\$(?<leftX>[0-9a-f]{2}).*?ld a,\$(?<rightX>[0-9a-f]{2}).*?ld a,PALH_(?<palette>[0-9a-f]{2})')
if (-not $impaStonePart.Success -or
    $impaStonePart.Groups['flags'].Value -ne 'c0' -or
    $impaStonePart.Groups['leftMask'].Value -ne '40' -or
    $impaStonePart.Groups['leftX'].Value -ne '28' -or
    $impaStonePart.Groups['rightX'].Value -ne '48' -or
    $impaStonePart.Groups['palette'].Value -ne '98') {
    throw 'Could not parse PART_TRIFORCE_STONE completed-room position and PALH_98.'
}

function Resolve-ObjectSpeed([string]$name) {
    $match = [regex]::Match(
        $speedSource,
        "(?m)^\s*SPEED_$([regex]::Escape($name))\s+dsb\s+\d+\s*;\s*0x(?<value>[0-9a-f]{2})")
    if (-not $match.Success) { throw "Could not resolve SPEED_$name." }
    return [Convert]::ToInt32($match.Groups['value'].Value, 16)
}
function Resolve-SoundConstant([string]$name) {
    $match = [regex]::Match(
        $musicConstantSource,
        "(?m)^\s*$([regex]::Escape($name))\s+db\s*;\s*\`$(?<value>[0-9a-f]{2})")
    if (-not $match.Success) { throw "Could not resolve sound constant $name." }
    return [Convert]::ToInt32($match.Groups['value'].Value, 16)
}

$stoneGraphic = $interactionGraphics['52:0']
if ($null -eq $stoneGraphic -or $stoneGraphic.Gfx -ne 0x3d -or
    -not $gfxNames.ContainsKey($stoneGraphic.Gfx) -or
    $gfxNames[$stoneGraphic.Gfx] -ne 'spr_triforcestone' -or
    $stoneGraphic.TileBase -ne 0 -or $stoneGraphic.Palette -ne 6 -or
    $stoneGraphic.DefaultAnimation -ne 0) {
    throw 'INTERAC_TRIFORCE_STONE $34:$00 no longer resolves to spr_triforcestone, tile base 0, palette 6, animation 0.'
}
$stoneAnimation = Resolve-NpcAnimation 0x34 0
if (-not $stoneAnimation) { throw 'Could not resolve INTERAC_TRIFORCE_STONE animation $00.' }

$impaStoneSpriteSource = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter 'spr_triforcestone.png' } |
    Select-Object -First 1
if ($null -eq $impaStoneSpriteSource) {
    throw 'Triforce-stone sprite not found: spr_triforcestone.png'
}
$impaStoneSpriteProperties = [IO.Path]::ChangeExtension(
    $impaStoneSpriteSource.FullName, '.properties')
if (-not (Test-Path -LiteralPath $impaStoneSpriteProperties)) {
    throw 'Triforce-stone sprite properties not found: spr_triforcestone.properties'
}
$stoneSourceInverted = [regex]::Match(
    (Get-Content -Raw -LiteralPath $impaStoneSpriteProperties),
    '(?m)^invert:\s*(?<value>true|false)\s*$')
if (-not $stoneSourceInverted.Success -or
    $stoneSourceInverted.Groups['value'].Value -ne 'false') {
    throw 'spr_triforcestone.properties no longer selects non-inverted source grayscale.'
}

$stoneTextIds = @(
    [Convert]::ToInt32($impaStoneSequence.Groups['firstText'].Value, 16),
    [Convert]::ToInt32($impaStoneSequence.Groups['signText'].Value, 16),
    [Convert]::ToInt32($impaMoveAway.Groups['request'].Value, 16),
    [Convert]::ToInt32($impaMoveAway.Groups['hesitation'].Value, 16),
    [Convert]::ToInt32($impaMoveAway.Groups['failure'].Value, 16),
    [Convert]::ToInt32($impaRockMoved.Groups['thanks'].Value, 16),
    [Convert]::ToInt32($impaLeaveGuard.Groups['text'].Value, 16),
    0x010b)
foreach ($textId in $stoneTextIds) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Impa stone-event text TX_$($textId.ToString('x4'))."
    }
}
if (-not $allTexts.ContainsKey(0x010c)) {
    throw 'Could not resolve the TX_010a jump target TX_010c.'
}
$stoneMessages = @($stoneTextIds | ForEach-Object { $allTexts[$_] })
$stoneMessages[1] = $stoneMessages[1].Replace('\sym(0x57)', [string][char]0x25b2)
$stoneMessages[6] = $stoneMessages[6].Replace(
    '\jump(TX_010c)', $allTexts[0x010c])
$stoneMessages[7] = $stoneMessages[7].Replace('\n', '')

$partPosition = [Convert]::ToInt32($impaStoneRoomPart.Groups['position'].Value, 16)
$partY = (($partPosition -shr 4) * 16) + 8
$stoneColumns = @(
    '0', '59', '34', '00',
    [Convert]::ToInt32($impaStoneRoomInteraction.Groups['y'].Value, 16).ToString(),
    [Convert]::ToInt32($impaStoneRoomInteraction.Groups['x'].Value, 16).ToString(),
    $partY.ToString(),
    [Convert]::ToInt32($impaStoneFinish.Groups['leftX'].Value, 16).ToString(),
    [Convert]::ToInt32($impaStoneFinish.Groups['rightX'].Value, 16).ToString(),
    [Convert]::ToInt32($impaStoneInit.Groups['radiusY'].Value, 16).ToString(),
    [Convert]::ToInt32($impaStoneInit.Groups['radiusX'].Value, 16).ToString(),
    $impaStoneFinish.Groups['leftFlag'].Value,
    $impaStoneFinish.Groups['rightFlag'].Value,
    [Convert]::ToInt32($impaApproach.Groups['y'].Value, 16).ToString(),
    [Convert]::ToInt32($impaApproach.Groups['x'].Value, 16).ToString(),
    [Convert]::ToInt32($impaStoneSequence.Groups['targetY'].Value, 16).ToString(),
    [Convert]::ToInt32($impaStoneSequence.Groups['targetX'].Value, 16).ToString(),
    '2',
    [Convert]::ToInt32($impaStoneSequence.Groups['spotHold'].Value, 16).ToString(),
    (-[Convert]::ToInt32($impaStoneSequence.Groups['spotSpeedZ'].Value, 16)).ToString(),
    [Convert]::ToInt32($impaStoneSequence.Groups['gravity'].Value, 16).ToString(),
    [Convert]::ToInt32($impaStoneSequence.Groups['firstLanding'].Value, 16).ToString(),
    $stoneTextIds[0].ToString('x4'),
    [Convert]::ToInt32($impaStoneSequence.Groups['firstPost'].Value, 16).ToString(),
    (Resolve-ObjectSpeed '300').ToString(),
    [Convert]::ToInt32($impaStoneSequence.Groups['stoneWait'].Value, 16).ToString(),
    [Convert]::ToInt32($impaStoneSequence.Groups['secondHold'].Value, 16).ToString(),
    (-[Convert]::ToInt32($impaStoneSequence.Groups['secondSpeedZ'].Value, 16)).ToString(),
    [Convert]::ToInt32($impaStoneSequence.Groups['secondLanding'].Value, 16).ToString(),
    $stoneTextIds[1].ToString('x4'),
    [Convert]::ToInt32($impaStoneSequence.Groups['signPost'].Value, 16).ToString(),
    [Convert]::ToInt32($linkCutscene2.Groups['axisWait'].Value, 16).ToString(),
    [Convert]::ToInt32($linkCutscene2.Groups['targetWait'].Value, 16).ToString(),
    [Convert]::ToInt32($linkCutscene2.Groups['faceWait'].Value, 16).ToString(),
    (Resolve-ObjectSpeed '100').ToString(),
    $impaMoveAway.Groups['lead'].Value,
    $stoneTextIds[2].ToString('x4'),
    $impaMoveAway.Groups['post1'].Value,
    (Resolve-ObjectSpeed '80').ToString(),
    [Convert]::ToInt32($impaMoveAway.Groups['backFrames1'].Value, 16).ToString(),
    $impaMoveAway.Groups['between1'].Value,
    $stoneTextIds[3].ToString('x4'),
    $impaMoveAway.Groups['post2'].Value,
    [Convert]::ToInt32($impaMoveAway.Groups['backFrames2'].Value, 16).ToString(),
    $impaMoveAway.Groups['between2'].Value,
    $stoneTextIds[4].ToString('x4'),
    $impaMoveAway.Groups['post3'].Value,
    [Convert]::ToInt32($impaStoneHold.Groups['frames'].Value, 16).ToString(),
    [Convert]::ToInt32($impaStonePush.Groups['moveFrames'].Value, 16).ToString(),
    (Resolve-ObjectSpeed '40').ToString(),
    (Resolve-ObjectSpeed '80').ToString(),
    $impaRockMoved.Groups['lead'].Value,
    $impaRockMoved.Groups['leftCorrect'].Value,
    (Resolve-ObjectSpeed '40').ToString(),
    $impaRockMoved.Groups['rightWait'].Value,
    $impaRockMoved.Groups['commonWait'].Value,
    [Convert]::ToInt32($impaRockMoved.Groups['rightFrames'].Value, 16).ToString(),
    (Resolve-ObjectSpeed '100').ToString(),
    $impaRockMoved.Groups['wait1'].Value,
    [Convert]::ToInt32($impaRockMoved.Groups['upFrames'].Value, 16).ToString(),
    $impaRockMoved.Groups['wait2'].Value,
    $impaRockMoved.Groups['poseWait'].Value,
    $stoneTextIds[5].ToString('x4'),
    $impaRockMoved.Groups['thanksPost'].Value,
    (Resolve-ObjectSpeed '80').ToString(),
    [Convert]::ToInt32($impaRockMoved.Groups['finalFrames'].Value, 16).ToString(),
    [Convert]::ToInt32($impaLeaveGuard.Groups['y'].Value, 16).ToString(),
    [Convert]::ToInt32($impaLeaveGuard.Groups['x'].Value, 16).ToString(),
    $stoneTextIds[6].ToString('x4'),
    $stoneTextIds[7].ToString('x4'),
    (Resolve-SoundConstant 'SND_CLINK').ToString('x2'),
    (Resolve-SoundConstant 'SND_MAKUDISAPPEAR').ToString('x2'),
    'f1',
    (Resolve-SoundConstant 'SND_SOLVEPUZZLE_2').ToString('x2'),
    $gfxNames[$stoneGraphic.Gfx],
    $stoneGraphic.TileBase.ToString(),
    $stoneGraphic.Palette.ToString(),
    $stoneAnimation,
    $impaStoneFinalTile.Groups['tile'].Value,
    $impaStoneFinalTile.Groups['collision'].Value,
    [Convert]::ToInt32($linkCutscene2.Groups['targetY'].Value, 16).ToString(),
    '56'
)
foreach ($message in $stoneMessages) {
    $stoneColumns += [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($message))
}
$stoneColumns += '0'
$stoneHeader = @(
    'group','room','id','subid','initial-y','initial-x','moved-y','left-x','right-x',
    'radius-y','radius-x','left-flag','right-flag','approach-y','approach-x','target-y','target-x','close-radius',
    'spot-hold','spot-speed-z','gravity','first-land-wait','first-text','first-post','approach-speed','stone-wait',
    'second-hold','second-speed-z','second-land-wait','sign-text','sign-post','link-axis-wait','link-target-wait',
    'link-face-wait','link-speed','request-lead','request-text','request-post','back-speed','back-frames-1',
    'between-back-1','hesitation-text','hesitation-post','back-frames-2','between-back-2','failure-text','failure-post',
    'push-hold','stone-move-frames','stone-speed','link-push-speed','reaction-lead','left-correct-frames',
    'left-correct-speed','right-branch-wait','common-wait','response-right-frames','response-right-speed','response-wait-1',
    'response-up-frames','response-wait-2','pose-wait','thanks-text','thanks-post','final-speed','final-frames',
    'leave-y','leave-x','leave-text','talk-text','spot-sound','push-sound','stop-sound','solve-sound',
    'stone-sprite','stone-tile-base','stone-palette','stone-animation','final-layout-tile','final-collision',
    'link-target-y','link-target-x',
    'first-text-base64','sign-text-base64','request-text-base64','hesitation-text-base64','failure-text-base64',
    'thanks-text-base64','leave-text-base64','talk-text-base64','stone-source-inverted') -join "`t"
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\impa_stone_event.tsv'),
    @("# $stoneHeader", ($stoneColumns -join "`t")),
    [Text.UTF8Encoding]::new($false))

Export-PaletteBlock 'paletteData4428' 4 'metadata\impa_stone_palette.bin'
Copy-Item -LiteralPath $impaStoneSpriteSource.FullName -Destination (
    Join-Path $destination 'gfx\spr_triforcestone.png') -Force

# Room $7a's unpositioned INTERAC_MISCELLANEOUS_1 ($6b:$00) owns the
# "HELLLLP!!!" edge trigger immediately before the Impa encounter. Export its
# edge check, textbox gate, post-text counter, simulated input, and separate
# room flag instead of folding them into room $6a's interaction.
$impaHelpSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous1.s')
$impaHelpBlock = [regex]::Match(
    $impaHelpSource,
    '(?ms)^interaction6b_subid00:(?<body>.*?)(?=^interaction6b_subid01:)')
$impaHelpEdge = [regex]::Match(
    $impaHelpSource,
    '(?ms)^interaction6b_checkLinkPressedUpAtScreenEdge:.*?ld hl,w1Link\.yh.*?cp \$(?<edgeY>[0-9a-f]{2}).*?and BTN_UP')
if (-not $impaHelpBlock.Success -or -not $impaHelpEdge.Success) {
    throw 'Could not parse INTERAC_MISCELLANEOUS_1 $6b:$00 or its Up-at-screen-edge check.'
}
$impaHelpCommand = [regex]::Match(
    $impaHelpBlock.Groups['body'].Value,
    '(?ms)bit 6,a.*?ld a,(?<postText>\d+)\s+ld \(de\),a\s+ld bc,TX_(?<text>[0-9a-f]{4}).*?@simulatedInput:\s*dwb (?<inputFrames>\d+), BTN_UP')
if (-not $impaHelpCommand.Success -or
    $impaHelpEdge.Groups['edgeY'].Value -ne '07' -or
    $impaHelpCommand.Groups['postText'].Value -ne '30' -or
    $impaHelpCommand.Groups['text'].Value -ne '0100' -or
    $impaHelpCommand.Groups['inputFrames'].Value -ne '8') {
    throw 'Impa help trigger no longer matches y<$07, TX_0100, 30 updates, and 8 BTN_UP updates.'
}
$impaHelpRoomBlock = [regex]::Match(
    ($mainObjectLines -join "`n"),
    '(?ms)^group0Map7aObjectData:(?<body>.*?)(?=^group0Map7bObjectData:)')
if (-not $impaHelpRoomBlock.Success -or
    $impaHelpRoomBlock.Groups['body'].Value -notmatch 'obj_Interaction \$6b \$00') {
    throw 'Room 0:7a no longer contains unpositioned INTERAC_MISCELLANEOUS_1 $6b:$00.'
}
$impaHelpTextId = [Convert]::ToInt32($impaHelpCommand.Groups['text'].Value, 16)
if (-not $allTexts.ContainsKey($impaHelpTextId) -or
    -not $allTextPositions.ContainsKey($impaHelpTextId) -or
    $allTextPositions[$impaHelpTextId] -ne 2) {
    throw 'Expected TX_0100 with fixed-bottom \\pos(2).'
}
$impaHelpRows = @(
    "# group`troom`tid`tsubid`troom-flag`tedge-y`tpost-text`tinput-up`ttext-id`ttextbox-position`ttext-base64",
    (@(
        '0', '7a', '6b', '00', '40',
        [Convert]::ToInt32($impaHelpEdge.Groups['edgeY'].Value, 16).ToString(),
        $impaHelpCommand.Groups['postText'].Value,
        $impaHelpCommand.Groups['inputFrames'].Value,
        $impaHelpCommand.Groups['text'].Value,
        $allTextPositions[$impaHelpTextId].ToString(),
        [Convert]::ToBase64String(
            [Text.Encoding]::UTF8.GetBytes($allTexts[$impaHelpTextId]))
    ) -join "`t")
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\impa_help_event.tsv'),
    $impaHelpRows,
    [Text.UTF8Encoding]::new($false))

$impaFakeAnimations = [regex]::Match(
    $impaFakeSource,
    '(?ms)@animations:\s*\.db \$(?<a>[0-9a-f]{2}) \$(?<b>[0-9a-f]{2}) \$(?<c>[0-9a-f]{2})')
$impaFakeCounters = [regex]::Match(
    $impaFakeSource,
    '(?ms)@countersAndAngles:\s*\.db \$(?<counter0>[0-9a-f]{2}) \$(?<angle0>[0-9a-f]{2})\s*\.db \$(?<counter1>[0-9a-f]{2}) \$(?<angle1>[0-9a-f]{2})\s*\.db \$(?<counter2>[0-9a-f]{2}) \$(?<angle2>[0-9a-f]{2})')
$impaFakeWait = [regex]::Match(
    $impaFakeSource,
    '(?ms)cp \$01.*?ld \(hl\),\$(?<wait>[0-9a-f]{2}).*?ld \(hl\),SPEED_300')
$impaFakeObjectBlock = [regex]::Match(
    $impaExtraObjects,
    '(?ms)^impaOctoroks:(?<body>.*?)(?=^\S|\z)')
if (-not $impaFakeAnimations.Success -or -not $impaFakeCounters.Success -or
    -not $impaFakeWait.Success -or $impaFakeWait.Groups['wait'].Value -ne '14' -or
    -not $impaFakeObjectBlock.Success) {
    throw 'Could not parse the fake Octorok animations, signal wait, counters, angles, or object data.'
}
$impaFakeObjects = [regex]::Matches(
    $impaFakeObjectBlock.Groups['body'].Value,
    'obj_Interaction \$(?<id>[0-9a-f]{2}) \$(?<subid>[0-9a-f]{2}) \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<var03>[0-9a-f]{2})')
if ($impaFakeObjects.Count -ne 3) {
    throw "Expected three fake Octoroks in objectData.impaOctoroks, got $($impaFakeObjects.Count)."
}
$impaFakeGraphic = $interactionGraphics['50:0']
if ($null -eq $impaFakeGraphic -or -not $gfxNames.ContainsKey($impaFakeGraphic.Gfx)) {
    throw 'Could not resolve INTERAC_FAKE_OCTOROK $32:$00 graphics.'
}
$impaFakeSprite = $gfxNames[$impaFakeGraphic.Gfx]
$impaInitialIndices = @(
    [Convert]::ToInt32($impaFakeAnimations.Groups['a'].Value, 16),
    [Convert]::ToInt32($impaFakeAnimations.Groups['b'].Value, 16),
    [Convert]::ToInt32($impaFakeAnimations.Groups['c'].Value, 16))
$impaFakeRows = [Collections.Generic.List[string]]::new()
$impaFakeRows.Add("# index`tid`tsubid`ty`tx`tvar03`tsprite`ttile-base`tpalette`tinitial-animation`tflee-animation`tsignal-wait`tflee-counter`tangle`tspeed")
for ($index = 0; $index -lt 3; $index++) {
    $object = $impaFakeObjects[$index]
    $var03 = [Convert]::ToInt32($object.Groups['var03'].Value, 16)
    if ($var03 -ne $index -or $object.Groups['id'].Value -ne '32' -or
        $object.Groups['subid'].Value -ne '00') {
        throw "Unexpected fake Octorok record at objectData.impaOctoroks index $index."
    }
    $counter = [Convert]::ToInt32(
        $impaFakeCounters.Groups["counter$index"].Value, 16)
    $angle = [Convert]::ToInt32(
        $impaFakeCounters.Groups["angle$index"].Value, 16)
    $initialAnimation = Resolve-NpcAnimation 0x32 $impaInitialIndices[$index]
    $fleeAnimation = Resolve-NpcAnimation 0x32 ([int]($angle / 8))
    if (-not $initialAnimation -or -not $fleeAnimation) {
        throw "Could not resolve fake Octorok animations for var03 $index."
    }
    $impaFakeRows.Add((@(
        $index.ToString(), '32', '00',
        $object.Groups['y'].Value, $object.Groups['x'].Value,
        $object.Groups['var03'].Value, $impaFakeSprite,
        $impaFakeGraphic.TileBase.ToString(), $impaFakeGraphic.Palette.ToString(),
        $initialAnimation, $fleeAnimation,
        [Convert]::ToInt32($impaFakeWait.Groups['wait'].Value, 16).ToString(),
        $counter.ToString(), $angle.ToString('x2'),
        $impaSpeed300Match.Groups['value'].Value
    ) -join "`t"))
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\impa_intro_octoroks.tsv'),
    $impaFakeRows,
    [Text.UTF8Encoding]::new($false))

$impaPaletteIndex = $paletteDataSource.IndexOf(
    'paletteData44d8:', [StringComparison]::Ordinal)
$impaPaletteEnd = $paletteDataSource.IndexOf(
    'paletteData44e8:', $impaPaletteIndex, [StringComparison]::Ordinal)
if ($impaPaletteIndex -lt 0 -or $impaPaletteEnd -lt 0) {
    throw 'Could not locate PALH_97 paletteData44d8.'
}
$impaPaletteBlock = $paletteDataSource.Substring(
    $impaPaletteIndex, $impaPaletteEnd - $impaPaletteIndex)
$impaPaletteColors = [regex]::Matches(
    $impaPaletteBlock,
    'm_RGB16 \$(?<r>[0-9a-f]{2}) \$(?<g>[0-9a-f]{2}) \$(?<b>[0-9a-f]{2})')
if ($impaPaletteColors.Count -ne 8) {
    throw "PALH_97 paletteData44d8 should contain two sprite palettes, got $($impaPaletteColors.Count)."
}
$impaPaletteBytes = [Collections.Generic.List[byte]]::new()
# Impa sets oamFlags=$07, selecting the second PALH_97 palette loaded into
# slot 7. Slot 6 is intentionally not emitted for this actor.
for ($color = 4; $color -lt 8; $color++) {
    $impaPaletteBytes.Add([Convert]::ToByte($impaPaletteColors[$color].Groups['r'].Value, 16))
    $impaPaletteBytes.Add([Convert]::ToByte($impaPaletteColors[$color].Groups['g'].Value, 16))
    $impaPaletteBytes.Add([Convert]::ToByte($impaPaletteColors[$color].Groups['b'].Value, 16))
}
if ($impaPaletteBytes.Count -ne 12) {
    throw "Expected 12 possessed-Impa sprite palette bytes, got $($impaPaletteBytes.Count)."
}
[IO.File]::WriteAllBytes(
    (Join-Path $destination 'metadata\impa_possessed_palette.bin'),
    $impaPaletteBytes.ToArray())

$impaFakeSpriteSource = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$impaFakeSprite.png" } |
    Select-Object -First 1
if ($null -eq $impaFakeSpriteSource) {
    throw "Fake Octorok sprite not found: $impaFakeSprite.png"
}
Copy-Item -LiteralPath $impaFakeSpriteSource.FullName -Destination (
    Join-Path $destination "gfx\$impaFakeSprite.png") -Force
