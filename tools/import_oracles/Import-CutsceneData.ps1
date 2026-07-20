function Find-CutsceneCommandSourceLine {
    param(
        [string]$source,
        [int]$bodyStart,
        [int]$bodyEnd,
        [string]$pattern,
        [string]$script,
        [int]$occurrence = 0)
    $regex = [regex]::new(
        $pattern, [Text.RegularExpressions.RegexOptions]::Multiline,
        [TimeSpan]::FromSeconds(1))
    $matches = @($regex.Matches($source, $bodyStart) |
        Where-Object { $_.Index -ge $bodyStart -and $_.Index -lt $bodyEnd })
    if ($occurrence -lt 0 -or $occurrence -ge $matches.Count) {
        throw "Could not locate $script command source occurrence $occurrence matching: $pattern"
    }
    $match = $matches[$occurrence]
    $firstToken = [regex]::Match($match.Value, '\S')
    if (-not $firstToken.Success) {
        throw "$script command match contains no opcode token: $pattern"
    }
    $sourceIndex = $match.Index + $firstToken.Index
    return [regex]::Matches(
        $source.Substring(0, $sourceIndex), "`n").Count + 1
}
function ConvertTo-CutsceneCommandPayload {
    param([string]$value)
    if ([string]::IsNullOrEmpty($value)) { return '' }
    return [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($value))
}
function New-CutsceneCommandRow {
    param(
        [string]$script,
        [int]$index,
        [string]$label,
        [int]$line,
        [string]$opcode,
        [string]$actor,
        [string]$arg0,
        [string]$arg1,
        [string]$payload)
    return @(
        $script, $label, $index.ToString(), $line.ToString(),
        $opcode, $actor, $arg0, $arg1,
        (ConvertTo-CutsceneCommandPayload $payload)
    ) -join "`t"
}

function Read-AssemblyCutsceneCommands {
    param(
        [string]$path,
        [string]$source,
        [string]$script,
        [int]$bodyStart,
        [int]$bodyLength,
        [Collections.Generic.HashSet[string]]$supportedOpcodes)

    $body = $source.Substring($bodyStart, $bodyLength)
    $firstLine = [regex]::Matches($source.Substring(0, $bodyStart), "`n").Count + 1
    $label = $script
    $commands = [Collections.Generic.List[object]]::new()
    $lines = $body -split "`r?`n"
    for ($offset = 0; $offset -lt $lines.Count; $offset++) {
        $lineNumber = $firstLine + $offset
        $line = ($lines[$offset] -replace ';.*$', '').Trim()
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -match '^(?<label>@?[A-Za-z_][A-Za-z0-9_@]*):$') {
            $label = $Matches['label']
            continue
        }
        if ($line.StartsWith('.')) { continue }
        if ($line -notmatch '^(?<opcode>[a-zA-Z0-9_]+)(?:\s+(?<operands>.*))?$') {
            throw "$path`:$lineNumber`: malformed $script assembly command '$line'."
        }
        $opcode = $Matches['opcode'].ToLowerInvariant()
        if (-not $supportedOpcodes.Contains($opcode)) {
            throw "$path`:$lineNumber`: unsupported $script opcode '$opcode' at label '$label'."
        }
        $commands.Add([pscustomobject]@{
            Script = $script
            Label = $label
            Index = $commands.Count
            Line = $lineNumber
            Opcode = $opcode
            Operands = $Matches['operands']
        })
    }
    if ($commands.Count -eq 0) {
        throw "$path`:$firstLine`: $script contains no commands."
    }
    return $commands
}

function Get-AssemblySourceLine {
    param([string]$source, [string]$pattern, [string]$description)
    $match = [regex]::Match(
        $source, $pattern, [Text.RegularExpressions.RegexOptions]::Multiline,
        [TimeSpan]::FromSeconds(1))
    if (-not $match.Success) { throw "Could not locate $description source label." }
    return [regex]::Matches($source.Substring(0, $match.Index), "`n").Count + 1
}

# INTERAC_TIMEPORTAL_SPAWNER ($e1) is a scenery interaction rather than an
# NPC, but it uses the same interaction graphics, animation, and OAM tables.
# Export every placed portal spot so runtime activation stays data-driven.
$portalSpawnerSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\timeportalSpawner.s')
if ($portalSpawnerSource -notmatch '(?ms)^@subid1Init:.*?GLOBALFLAG_MAKU_TREE_SAVED.*?jr nz,@commonInit\s+jr @setSubidBit7\s+^@subid2Init:.*?TREASURE_SEED_SATCHEL.*?jr c,@commonInit\s+^@setSubidBit7:.*?set 7,\(hl\)') {
    throw 'INTERAC_TIMEPORTAL_SPAWNER subtype $01/$02 activation predicates changed.'
}
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
$makuReturnPortal = $portalRows | Where-Object { $_ -match '^1\t48\t02\t48\t58\t' }
if (-not $initialPortal -or -not $makuReturnPortal) {
    throw 'The initial 0:39 or post-rescue 1:48 active portal was not extracted.'
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
if ($timeWarpCutsceneSource -notmatch '(?ms)^@@cbb3_03:\s+call timewarpCutscene_decCBB4\s+ret nz\s+call fastFadeinFromBlack\s+jp timewarpCutscene_incCBB3\s+@@cbb3_04:\s+ld a,\(wPaletteThread_mode\)\s+or a\s+ret nz\s+call fadeoutToWhite') {
    throw 'CUTSCENE_TIMEWARP no longer hands fastFadeinFromBlack directly to fadeoutToWhite.'
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

$makuMusicSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\music.s')
$makuStopSound = [regex]::Match(
    $makuMusicSource,
    '(?m)^\.define\s+SNDCTRL_STOPMUSIC\s+\$(?<value>[0-9a-f]{2})')
$makuDisappearSound = [regex]::Match(
    $makuMusicSource,
    '(?m)^\s*SND_MAKUDISAPPEAR\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
$makuCutsceneConstants = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\cutsceneIndices.s')
$makuCutsceneIndex = [regex]::Match(
    $makuCutsceneConstants,
    '(?m)^\s*CUTSCENE_MAKU_TREE_DISAPPEARING\s+db\s*;\s*0x(?<value>[0-9a-f]{2})')
if (-not $makuStopSound.Success -or $makuStopSound.Groups['value'].Value -ne 'f0' -or
    -not $makuDisappearSound.Success -or $makuDisappearSound.Groups['value'].Value -ne 'b2' -or
    -not $makuCutsceneIndex.Success -or $makuCutsceneIndex.Groups['value'].Value -ne '07') {
    throw 'Could not resolve Maku Tree STOPMUSIC $f0, disappearance sound $b2, or cutscene $07.'
}

$makuBodyStart = $makuScriptMatch.Groups['body'].Index
$makuBodyEnd = $makuBodyStart + $makuScriptMatch.Groups['body'].Length
$findMakuSourceLine = {
    param([string]$pattern, [int]$occurrence = 0)
    return Find-CutsceneCommandSourceLine `
        $makuScriptSource $makuBodyStart $makuBodyEnd $pattern `
        'makuTree_subid01Script_body' $occurrence
}
$newMakuCommandRow = {
    param(
        [int]$index,
        [int]$line,
        [string]$opcode,
        [string]$actor,
        [string]$arg0,
        [string]$arg1,
        [string]$payload)
    return New-CutsceneCommandRow `
        'makuTree_subid01Script_body' $index 'makuTree_subid01Script_body' `
        $line $opcode $actor $arg0 $arg1 $payload
}
$makuCommandRows = @(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64",
    (& $newMakuCommandRow 0 (& $findMakuSourceLine '(?m)^\s*disablemenu\s*$') 'disablemenu' '' '' '' ''),
    (& $newMakuCommandRow 1 (& $findMakuSourceLine '(?m)^\s*asm15\s+makuTree_setAnimation,\s*\$00\s*$') 'setanimationcontinue' 'MakuTree' '00' '' $makuAnimations[0]),
    (& $newMakuCommandRow 2 (& $findMakuSourceLine '(?m)^\s*setcollisionradii\s+\$08,\s*\$08\s*$') 'setcollisionradii' 'MakuTree' '08' '08' ''),
    (& $newMakuCommandRow 3 (& $findMakuSourceLine '(?m)^\s*makeabuttonsensitive\s*$') 'makeabuttonsensitive' 'MakuTree' '' '' ''),
    (& $newMakuCommandRow 4 (& $findMakuSourceLine '(?m)^\s*checkpalettefadedone\s*$') 'gate' '' '' '' 'palette-fade-done'),
    (& $newMakuCommandRow 5 (& $findMakuSourceLine '(?m)^\s*wait\s+210\s*$') 'wait' '' '210' '' ''),
    (& $newMakuCommandRow 6 (& $findMakuSourceLine '(?m)^\s*showtextlowindex\s+<TX_0564\s*$') 'showtext' '' '0564' '' $allTexts[0x0564]),
    (& $newMakuCommandRow 7 (& $findMakuSourceLine '(?m)^\s*wait\s+60\s*$') 'wait' '' '60' '' ''),
    (& $newMakuCommandRow 8 (& $findMakuSourceLine '(?m)^\s*playsound\s+SNDCTRL_STOPMUSIC\s*$') 'playsound' '' $makuStopSound.Groups['value'].Value '' ''),
    (& $newMakuCommandRow 9 (& $findMakuSourceLine '(?m)^\s*asm15\s+makuTree_setAnimation,\s*\$04\s*$') 'setanimationcontinue' 'MakuTree' '04' '' $makuAnimations[4]),
    (& $newMakuCommandRow 10 (& $findMakuSourceLine '(?m)^\s*wait\s+60\s*$' 1) 'wait' '' '60' '' ''),
    (& $newMakuCommandRow 11 (& $findMakuSourceLine '(?m)^\s*playsound\s+SND_MAKUDISAPPEAR\s*$') 'playsound' '' $makuDisappearSound.Groups['value'].Value '' ''),
    (& $newMakuCommandRow 12 (& $findMakuSourceLine '(?m)^\s*writememory\s+wCutsceneTrigger,\s*CUTSCENE_MAKU_TREE_DISAPPEARING\s*$') 'writememory' '' $makuCutsceneIndex.Groups['value'].Value '' 'wCutsceneTrigger'),
    (& $newMakuCommandRow 13 (& $findMakuSourceLine '(?m)^\s*wait\s+210\s*$' 1) 'wait' '' '210' '' ''),
    (& $newMakuCommandRow 14 (& $findMakuSourceLine '(?m)^\s*showtextlowindex\s+<TX_0540\s*$') 'showtext' '' '0540' '' $allTexts[0x0540]),
    (& $newMakuCommandRow 15 (& $findMakuSourceLine '(?m)^\s*playsound\s+SND_MAKUDISAPPEAR\s*$' 1) 'playsound' '' $makuDisappearSound.Groups['value'].Value '' ''),
    (& $newMakuCommandRow 16 (& $findMakuSourceLine '(?m)^\s*wait\s+210\s*$' 2) 'wait' '' '210' '' ''),
    (& $newMakuCommandRow 17 (& $findMakuSourceLine '(?m)^\s*showtextlowindex\s+<TX_0541\s*$') 'showtext' '' '0541' '' $allTexts[0x0541]),
    (& $newMakuCommandRow 18 (& $findMakuSourceLine '(?m)^\s*playsound\s+SND_MAKUDISAPPEAR\s*$' 2) 'playsound' '' $makuDisappearSound.Groups['value'].Value '' ''),
    (& $newMakuCommandRow 19 (& $findMakuSourceLine '(?m)^\s*wait\s+150\s*$') 'wait' '' '150' '' ''),
    (& $newMakuCommandRow 20 (& $findMakuSourceLine '(?m)^\s*writememory\s+wTmpcfc0\.genericCutscene\.state,\s*\$01\s*$') 'writememory' '' '01' '' 'wTmpcfc0.genericCutscene.state'),
    (& $newMakuCommandRow 21 (& $findMakuSourceLine '(?m)^\s*asm15\s+incMakuTreeState\s*$') 'native' '' '' '' 'incMakuTreeState'),
    (& $newMakuCommandRow 22 (& $findMakuSourceLine '(?m)^\s*scriptend\s*$') 'scriptend' '' '' '' '')
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\maku_tree_commands.tsv'),
    $makuCommandRows,
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

# Emit the active path as typed command records. Command rows retain the
# assembly script/label, normalized command index, and physical source line.
# The recognized flicker loop remains one native-effect command, while its
# counter byte and frame mask stay explicit operands.
$ralphMusicSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\music.s')
$ralphSoundMatch = [regex]::Match(
    $ralphMusicSource,
    '(?m)^\s*SND_MYSTERY_SEED\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $ralphSoundMatch.Success -or $ralphSoundMatch.Groups['value'].Value -ne '7b') {
    throw 'SND_MYSTERY_SEED no longer resolves to $7b.'
}

$ralphBodyStart = $ralphScriptMatch.Groups['body'].Index
$ralphBodyEnd = $ralphBodyStart + $ralphScriptMatch.Groups['body'].Length
$findRalphSourceLine = {
    param([string]$pattern)
    return Find-CutsceneCommandSourceLine `
        $ralphScriptSource $ralphBodyStart $ralphBodyEnd $pattern 'ralphSubid0dScript'
}
$newRalphCommandRow = {
    param(
        [int]$index,
        [string]$label,
        [int]$line,
        [string]$opcode,
        [string]$actor,
        [string]$arg0,
        [string]$arg1,
        [string]$payload)
    return New-CutsceneCommandRow `
        'ralphSubid0dScript' $index $label $line $opcode $actor $arg0 $arg1 $payload
}

$ralphCommandRows = @(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64",
    (& $newRalphCommandRow 0 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*disableinput\s*$') 'disableinput' '' '' '' ''),
    (& $newRalphCommandRow 1 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*wait\s+40\s*$') 'wait' '' '40' '' ''),
    (& $newRalphCommandRow 2 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*showtext\s+TX_2a1e\s*$') 'showtext' '' '2a1e' '' $allTexts[$ralphTextId]),
    (& $newRalphCommandRow 3 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*wait\s+30\s*$') 'wait' '' '30' '' ''),
    (& $newRalphCommandRow 4 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*setanimation\s+\$01\s*$') 'setanimation' 'Ralph' '01' '' $ralphMoveAnimation),
    (& $newRalphCommandRow 5 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*setspeed\s+SPEED_100\s*$') 'setspeed' 'Ralph' '28' '' ''),
    (& $newRalphCommandRow 6 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*setangle\s+\$08\s*$') 'setangle' 'Ralph' '08' '' ''),
    (& $newRalphCommandRow 7 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*applyspeed\s+\$11\s*$') 'applyspeed' 'Ralph' '11' '' ''),
    (& $newRalphCommandRow 8 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*setanimation\s+\$09\s*$') 'setanimation' 'Ralph' '09' '' $ralphPortalAnimation),
    (& $newRalphCommandRow 9 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*writeobjectbyte\s+Interaction\.var3f,\s*\$2d\s*$') 'writeobjectbyte' 'Ralph' '3f' '2d' ''),
    (& $newRalphCommandRow 10 'ralphSubid0dScript' (& $findRalphSourceLine '(?m)^\s*playsound\s+SND_MYSTERY_SEED\s*$') 'playsound' '' $ralphSoundMatch.Groups['value'].Value '' ''),
    (& $newRalphCommandRow 11 '@flickerVisibility' (& $findRalphSourceLine '(?m)^\s*asm15\s+scriptHelp\.ralph_flickerVisibility\s*$') 'flicker' 'Ralph' '3f' '01' ''),
    (& $newRalphCommandRow 12 '@done' (& $findRalphSourceLine '(?m)^\s*setglobalflag\s+GLOBALFLAG_RALPH_ENTERED_PORTAL\s*$') 'setglobalflag' '' $flagMatch.Groups['value'].Value '' ''),
    (& $newRalphCommandRow 13 '@done' (& $findRalphSourceLine '(?m)^\s*asm15\s+scriptHelp\.ralph_restoreMusic\s*$') 'native' '' '' '' 'ralph_restoreMusic'),
    (& $newRalphCommandRow 14 '@done' (& $findRalphSourceLine '(?m)^\s*enableinput\s*$') 'enableinput' '' '' '' ''),
    (& $newRalphCommandRow 15 '@done' (& $findRalphSourceLine '(?m)^\s*scriptend\s*$') 'scriptend' '' '' '' '')
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\ralph_portal_commands.tsv'),
    $ralphCommandRows,
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

# The second shared-runner slice preserves the active path's actual command
# boundaries. jumpAndWaitUntilLanded remains one typed composite command, but
# retains callscript's setup-only update before beginJump/updateGravity.
$enterPastBodyStart = $enterPastScriptMatch.Groups['body'].Index
$enterPastBodyEnd = $enterPastBodyStart + $enterPastScriptMatch.Groups['body'].Length
$findEnterPastSourceLine = {
    param([string]$pattern, [int]$occurrence = 0)
    return Find-CutsceneCommandSourceLine `
        $ralphScriptSource $enterPastBodyStart $enterPastBodyEnd $pattern `
        'villagerSubid0dScript' $occurrence
}
$newEnterPastCommandRow = {
    param(
        [int]$index,
        [int]$line,
        [string]$opcode,
        [string]$actor,
        [string]$arg0,
        [string]$arg1,
        [string]$payload)
    return New-CutsceneCommandRow `
        'villagerSubid0dScript' $index 'villagerSubid0dScript' $line `
        $opcode $actor $arg0 $arg1 $payload
}

$enterPastCommandRows = @(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64",
    (& $newEnterPastCommandRow 0 (& $findEnterPastSourceLine '(?m)^\s*setdisabledobjectsto11\s*$') 'setdisabledobjects' '' '11' '' ''),
    (& $newEnterPastCommandRow 1 (& $findEnterPastSourceLine '(?m)^\s*wait\s+100\s*$') 'wait' '' '100' '' ''),
    (& $newEnterPastCommandRow 2 (& $findEnterPastSourceLine '(?m)^\s*disableinput\s*$') 'disableinput' '' '' '' ''),
    (& $newEnterPastCommandRow 3 (& $findEnterPastSourceLine '(?m)^\s*wait\s+40\s*$') 'wait' '' '40' '' ''),
    (& $newEnterPastCommandRow 4 (& $findEnterPastSourceLine '(?m)^\s*callscript\s+jumpAndWaitUntilLanded\s*$') 'jump' 'Villager' $enterPastJumpRaw.ToString() $enterPastGravity.ToString('x2') $enterPastSoundMatch.Groups['value'].Value),
    (& $newEnterPastCommandRow 5 (& $findEnterPastSourceLine '(?m)^\s*wait\s+30\s*$') 'wait' '' '30' '' ''),
    (& $newEnterPastCommandRow 6 (& $findEnterPastSourceLine '(?m)^\s*showtext\s+TX_1622\s*$') 'showtext' '' '1622' '' $allTexts[$enterPastTextId]),
    (& $newEnterPastCommandRow 7 (& $findEnterPastSourceLine '(?m)^\s*wait\s+30\s*$' 1) 'wait' '' '30' '' ''),
    (& $newEnterPastCommandRow 8 (& $findEnterPastSourceLine '(?m)^\s*setspeed\s+SPEED_100\s*$') 'setspeed' 'Villager' $speedMatch.Groups['value'].Value '' ''),
    (& $newEnterPastCommandRow 9 (& $findEnterPastSourceLine '(?m)^\s*movedown\s+\$11\s*$') 'move' 'Villager' '10' '11' $enterPastDownAnimation),
    (& $newEnterPastCommandRow 10 (& $findEnterPastSourceLine '(?m)^\s*moveright\s+\$11\s*$') 'move' 'Villager' '08' '11' $enterPastRightAnimation),
    (& $newEnterPastCommandRow 11 (& $findEnterPastSourceLine '(?m)^\s*movedown\s+\$09\s*$') 'move' 'Villager' '10' '09' $enterPastDownAnimation),
    (& $newEnterPastCommandRow 12 (& $findEnterPastSourceLine '(?m)^\s*setspeed\s+SPEED_080\s*$') 'setspeed' 'Villager' $enterPastSlowSpeedMatch.Groups['value'].Value '' ''),
    (& $newEnterPastCommandRow 13 (& $findEnterPastSourceLine '(?m)^\s*applyspeed\s+\$21\s*$') 'applyspeed' 'Villager' '21' '' ''),
    (& $newEnterPastCommandRow 14 (& $findEnterPastSourceLine '(?m)^\s*setspeed\s+SPEED_100\s*$' 1) 'setspeed' 'Villager' $speedMatch.Groups['value'].Value '' ''),
    (& $newEnterPastCommandRow 15 (& $findEnterPastSourceLine '(?m)^\s*applyspeed\s+\$39\s*$') 'applyspeed' 'Villager' '39' '' ''),
    (& $newEnterPastCommandRow 16 (& $findEnterPastSourceLine '(?m)^\s*setglobalflag\s+GLOBALFLAG_ENTER_PAST_CUTSCENE_DONE\s*$') 'setglobalflag' '' $enterPastFlagMatch.Groups['value'].Value '' ''),
    (& $newEnterPastCommandRow 17 (& $findEnterPastSourceLine '(?m)^\s*enableinput\s*$') 'enableinput' '' '' '' ''),
    (& $newEnterPastCommandRow 18 (& $findEnterPastSourceLine '(?m)^\s*scriptend\s*$') 'scriptend' '' '' '' '')
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\enter_past_commands.tsv'),
    $enterPastCommandRows,
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
    '(?ms)checkmemoryeq .*?, \$(?<signal>[0-9a-f]{2})\s+wait (?<introWait>\d+)\s+showtextdifferentforlinked TX_(?<text>[0-9a-f]{4}), TX_(?<linkedText>[0-9a-f]{4})\s+wait (?<postText>\d+)\s+setspeed SPEED_(?<speed>[0-9a-fA-F_]+)\s+movedown \$(?<moveFrames>[0-9a-f]{2})\s+orroomflag \$(?<roomFlag>[0-9a-f]{2})')
if (-not $impaScriptCommand.Success -or
    $impaScriptCommand.Groups['signal'].Value -ne '01' -or
    $impaScriptCommand.Groups['introWait'].Value -ne '210' -or
    $impaScriptCommand.Groups['text'].Value -ne '0102' -or
    $impaScriptCommand.Groups['linkedText'].Value -ne '0103' -or
    $impaScriptCommand.Groups['postText'].Value -ne '30' -or
    $impaScriptCommand.Groups['speed'].Value -ne '080' -or
    $impaScriptCommand.Groups['moveFrames'].Value -ne '20' -or
    $impaScriptCommand.Groups['roomFlag'].Value -ne '40') {
    throw 'impaScript0 no longer matches signal $01, waits 210/30, TX_0102/TX_0103, SPEED_080, movedown $20, and room flag $40.'
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
$impaLinkedTextId = [Convert]::ToInt32(
    $impaScriptCommand.Groups['linkedText'].Value, 16)
if (-not $allTexts.ContainsKey(0x0101) -or
    -not $allTexts.ContainsKey($impaTextId) -or
    -not $allTexts.ContainsKey($impaLinkedTextId)) {
    throw 'Could not resolve Impa encounter text TX_0101/TX_0102/TX_0103.'
}
# TX_0102 begins with a text-engine call to TX_0101. Expand it for the runtime
# textbox, which consumes the already-resolved final string rather than text
# bytecode pointers.
$impaText = $allTexts[$impaTextId] -replace '^\\call\(TX_0101\)\r?\n?',
    "$($allTexts[0x0101])`n"
$impaText = $impaText.Replace('\sym(0x57)', [string][char]0x25b2)
$impaLinkedText = $allTexts[$impaLinkedTextId] -replace '^\\call\(TX_0101\)\r?\n?',
    "$($allTexts[0x0101])`n"
$impaLinkedText = $impaLinkedText.Replace('\sym(0x57)', [string][char]0x25b2)

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
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($impaText)),
    $impaLinkedTextId.ToString('x4'),
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($impaLinkedText))
)
$impaEventRows = @(
    "# group`troom`tid`tsubid`troom-flag`tlink-wait`ttarget-x`tcenter-wait`tapproach-frames`tlink-speed`timpa-wait`ttext-id`tpost-text`timpa-speed`timpa-move-frames`tfollow-lag`tup-animation`tright-animation`tdown-animation`tleft-animation`ttext-base64`tlinked-text-id`tlinked-text-base64",
    ($impaEventColumns -join "`t")
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\impa_intro_event.tsv'),
    $impaEventRows,
    [Text.UTF8Encoding]::new($false))

$impaCommandDefinitions = @(
    @('^\s*checkmemoryeq\s+wTmpcfc0\.genericCutscene\.cfd0,\s*\$01', 'checkmemoryeq', '', '01', '', 'wTmpcfc0.genericCutscene.cfd0'),
    @('^\s*wait\s+210', 'wait', '', '210', '', ''),
    @('^\s*showtextdifferentforlinked\s+TX_0102,\s*TX_0103', 'showtextdifferentforlinked', '', '0102', '0103', [string]::Concat($impaText, [char]0, $impaLinkedText)),
    @('^\s*wait\s+30', 'wait', '', '30', '', ''),
    @('^\s*setspeed\s+SPEED_080', 'setspeed', 'Impa', $impaSpeed80Match.Groups['value'].Value, '', ''),
    @('^\s*movedown\s+\$20', 'move', 'Impa', '10', '20', $impaFollowerAnimations[2]),
    @('^\s*orroomflag\s+\$40', 'orroomflag', '', '40', '', ''),
    @('^\s*scriptend', 'scriptend', '', '', '', '')
)
$impaCommandRows = [Collections.Generic.List[string]]::new()
$impaCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
$impaScriptBodyStart = $impaScriptMatch.Groups['body'].Index
$impaScriptBodyEnd = $impaScriptBodyStart + $impaScriptMatch.Groups['body'].Length
for ($index = 0; $index -lt $impaCommandDefinitions.Count; $index++) {
    $definition = $impaCommandDefinitions[$index]
    $sourceLine = Find-CutsceneCommandSourceLine `
        $impaScriptSource $impaScriptBodyStart $impaScriptBodyEnd `
        $definition[0] 'impaScript0'
    $impaCommandRows.Add((New-CutsceneCommandRow `
        'impaScript0' $index 'impaScript0' $sourceLine `
        $definition[1] $definition[2] $definition[3] $definition[4] $definition[5]))
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\impa_intro_commands.tsv'),
    $impaCommandRows,
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

# The approach, jumps, and linkCutscene2 positioning handshake are native
# interaction/special-object code. The retreat after linkCutscene2 writes
# cfd0=$03 is the actual interaction-script stream; export it without folding
# those parallel native handlers into event-specific timing stages.
$impaMoveAwayBodyStart = $impaMoveAwayBlock.Groups['body'].Index
$impaMoveAwayBodyEnd =
    $impaMoveAwayBodyStart + $impaMoveAwayBlock.Groups['body'].Length
$findImpaMoveAwaySourceLine = {
    param([string]$pattern, [int]$occurrence = 0)
    return Find-CutsceneCommandSourceLine `
        $impaScriptSource $impaMoveAwayBodyStart $impaMoveAwayBodyEnd `
        $pattern 'impaScript_moveAwayFromRock' $occurrence
}
$newImpaMoveAwayCommandRow = {
    param(
        [int]$index,
        [int]$line,
        [string]$opcode,
        [string]$actor = '',
        [string]$arg0 = '',
        [string]$arg1 = '',
        [string]$payload = '')
    return New-CutsceneCommandRow `
        'impaScript_moveAwayFromRock' $index 'impaScript_moveAwayFromRock' $line `
        $opcode $actor $arg0 $arg1 $payload
}
$impaMoveAwayCommandRows = @(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64",
    (& $newImpaMoveAwayCommandRow 0 (& $findImpaMoveAwaySourceLine '^\s*checkmemoryeq\s+wTmpcfc0\.genericCutscene\.cfd0,\s*\$03\s*$') 'checkmemoryeq' '' '03' '' 'wTmpcfc0.genericCutscene.cfd0'),
    (& $newImpaMoveAwayCommandRow 1 (& $findImpaMoveAwaySourceLine '^\s*setanimation\s+\$02\s*$') 'setanimation' 'Impa' '02' '' $impaFollowerAnimations[2]),
    (& $newImpaMoveAwayCommandRow 2 (& $findImpaMoveAwaySourceLine '^\s*wait\s+10\s*$') 'wait' '' '10'),
    (& $newImpaMoveAwayCommandRow 3 (& $findImpaMoveAwaySourceLine '^\s*showtext\s+TX_0106\s*$') 'showtext' '' '0106' '' $stoneMessages[2]),
    (& $newImpaMoveAwayCommandRow 4 (& $findImpaMoveAwaySourceLine '^\s*wait\s+30\s*$' 0) 'wait' '' '30'),
    (& $newImpaMoveAwayCommandRow 5 (& $findImpaMoveAwaySourceLine '^\s*setanimation\s+\$01\s*$') 'setanimation' 'Impa' '01' '' $impaFollowerAnimations[1]),
    (& $newImpaMoveAwayCommandRow 6 (& $findImpaMoveAwaySourceLine '^\s*setangle\s+\$18\s*$') 'setangle' 'Impa' '18'),
    (& $newImpaMoveAwayCommandRow 7 (& $findImpaMoveAwaySourceLine '^\s*setspeed\s+SPEED_080\s*$') 'setspeed' 'Impa' $impaSpeed80Match.Groups['value'].Value),
    (& $newImpaMoveAwayCommandRow 8 (& $findImpaMoveAwaySourceLine '^\s*applyspeed\s+\$21\s*$' 0) 'applyspeed' 'Impa' '21'),
    (& $newImpaMoveAwayCommandRow 9 (& $findImpaMoveAwaySourceLine '^\s*wait\s+30\s*$' 1) 'wait' '' '30'),
    (& $newImpaMoveAwayCommandRow 10 (& $findImpaMoveAwaySourceLine '^\s*showtext\s+TX_0107\s*$') 'showtext' '' '0107' '' $stoneMessages[3]),
    (& $newImpaMoveAwayCommandRow 11 (& $findImpaMoveAwaySourceLine '^\s*wait\s+30\s*$' 2) 'wait' '' '30'),
    (& $newImpaMoveAwayCommandRow 12 (& $findImpaMoveAwaySourceLine '^\s*applyspeed\s+\$21\s*$' 1) 'applyspeed' 'Impa' '21'),
    (& $newImpaMoveAwayCommandRow 13 (& $findImpaMoveAwaySourceLine '^\s*wait\s+30\s*$' 3) 'wait' '' '30'),
    (& $newImpaMoveAwayCommandRow 14 (& $findImpaMoveAwaySourceLine '^\s*showtext\s+TX_0108\s*$') 'showtext' '' '0108' '' $stoneMessages[4]),
    (& $newImpaMoveAwayCommandRow 15 (& $findImpaMoveAwaySourceLine '^\s*wait\s+30\s*$' 4) 'wait' '' '30'),
    (& $newImpaMoveAwayCommandRow 16 (& $findImpaMoveAwaySourceLine '^\s*writememory\s+wTmpcfc0\.genericCutscene\.cfd0,\s*\$04\s*$') 'writememory' '' '04' '' 'wTmpcfc0.genericCutscene.cfd0'),
    (& $newImpaMoveAwayCommandRow 17 (& $findImpaMoveAwaySourceLine '^\s*scriptend\s*$') 'scriptend')
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\impa_stone_prepush_commands.tsv'),
    $impaMoveAwayCommandRows,
    [Text.UTF8Encoding]::new($false))

$impaRockMovedBodyStart = $impaRockMovedBlock.Groups['body'].Index
$impaRockMovedBodyEnd =
    $impaRockMovedBodyStart + $impaRockMovedBlock.Groups['body'].Length
$findImpaRockMovedSourceLine = {
    param([string]$pattern, [int]$occurrence = 0)
    return Find-CutsceneCommandSourceLine `
        $impaScriptHelperSource $impaRockMovedBodyStart $impaRockMovedBodyEnd `
        $pattern 'impaScript_rockJustMoved' $occurrence
}
$newImpaRockMovedCommandRow = {
    param(
        [int]$index,
        [string]$label,
        [int]$line,
        [string]$opcode,
        [string]$actor = '',
        [string]$arg0 = '',
        [string]$arg1 = '',
        [string]$payload = '')
    return New-CutsceneCommandRow `
        'impaScript_rockJustMoved' $index $label $line `
        $opcode $actor $arg0 $arg1 $payload
}
$impaRockMovedCommandRows = @(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64",
    (& $newImpaRockMovedCommandRow 0 'impaScript_rockJustMoved' (& $findImpaRockMovedSourceLine '^\s*wait\s+4\s*$') 'wait' '' '4'),
    (& $newImpaRockMovedCommandRow 1 'impaScript_rockJustMoved' (& $findImpaRockMovedSourceLine '^\s*jumpifmemoryeq\s+w1Link\.angle,\s*\$08,\s*@pushedRight\s*$') 'jumpifmemoryeq' '' '08' '6' 'w1Link.angle'),
    (& $newImpaRockMovedCommandRow 2 'impaScript_rockJustMoved' (& $findImpaRockMovedSourceLine '^\s*setangle\s+\$10\s*$') 'setangle' 'Impa' '10'),
    (& $newImpaRockMovedCommandRow 3 'impaScript_rockJustMoved' (& $findImpaRockMovedSourceLine '^\s*setspeed\s+SPEED_040\s*$') 'setspeed' 'Impa' ((Resolve-ObjectSpeed '40').ToString('x2'))),
    (& $newImpaRockMovedCommandRow 4 'impaScript_rockJustMoved' (& $findImpaRockMovedSourceLine '^\s*applyspeed\s+65\s*$') 'applyspeed' 'Impa' ([Convert]::ToInt32($impaRockMoved.Groups['leftCorrect'].Value, 10).ToString('x2'))),
    (& $newImpaRockMovedCommandRow 5 'impaScript_rockJustMoved' (& $findImpaRockMovedSourceLine '^\s*scriptjump\s+\+\+\s*$') 'scriptjump' '' '7'),
    (& $newImpaRockMovedCommandRow 6 '@pushedRight' (& $findImpaRockMovedSourceLine '^\s*wait\s+65\s*$') 'wait' '' '65'),
    (& $newImpaRockMovedCommandRow 7 '++[1]' (& $findImpaRockMovedSourceLine '^\s*wait\s+120\s*$') 'wait' '' '120'),
    (& $newImpaRockMovedCommandRow 8 '++[1]' (& $findImpaRockMovedSourceLine '^\s*setangle\s+\$08\s*$') 'setangle' 'Impa' '08'),
    (& $newImpaRockMovedCommandRow 9 '++[1]' (& $findImpaRockMovedSourceLine '^\s*setspeed\s+SPEED_100\s*$') 'setspeed' 'Impa' ((Resolve-ObjectSpeed '100').ToString('x2'))),
    (& $newImpaRockMovedCommandRow 10 '++[1]' (& $findImpaRockMovedSourceLine '^\s*applyspeed\s+\$21\s*$') 'applyspeed' 'Impa' '21'),
    (& $newImpaRockMovedCommandRow 11 '++[1]' (& $findImpaRockMovedSourceLine '^\s*wait\s+8\s*$' 0) 'wait' '' '8'),
    (& $newImpaRockMovedCommandRow 12 '++[1]' (& $findImpaRockMovedSourceLine '^\s*jumpifmemoryeq\s+w1Link\.angle,\s*\$08\s+\+\+\s*$') 'jumpifmemoryeq' '' '08' '15' 'w1Link.angle'),
    (& $newImpaRockMovedCommandRow 13 '++[1]' (& $findImpaRockMovedSourceLine '^\s*moveup\s+\$11\s*$') 'move' 'Impa' '00' '11' $impaFollowerAnimations[0]),
    (& $newImpaRockMovedCommandRow 14 '++[1]' (& $findImpaRockMovedSourceLine '^\s*wait\s+8\s*$' 1) 'wait' '' '8'),
    (& $newImpaRockMovedCommandRow 15 '++[2]' (& $findImpaRockMovedSourceLine '^\s*writememory\s+wTmpcfc0\.genericCutscene\.cfd0,\s*\$07\s*$') 'writememory' '' '07' '' 'wTmpcfc0.genericCutscene.cfd0'),
    (& $newImpaRockMovedCommandRow 16 '++[2]' (& $findImpaRockMovedSourceLine '^\s*setanimation\s+\$00\s*$') 'setanimation' 'Impa' '00' '' $impaFollowerAnimations[0]),
    (& $newImpaRockMovedCommandRow 17 '++[2]' (& $findImpaRockMovedSourceLine '^\s*wait\s+30\s*$' 0) 'wait' '' '30'),
    (& $newImpaRockMovedCommandRow 18 '++[2]' (& $findImpaRockMovedSourceLine '^\s*showtext\s+TX_0109\s*$') 'showtext' '' '0109' '' $stoneMessages[5]),
    (& $newImpaRockMovedCommandRow 19 '++[2]' (& $findImpaRockMovedSourceLine '^\s*wait\s+30\s*$' 1) 'wait' '' '30'),
    (& $newImpaRockMovedCommandRow 20 '++[2]' (& $findImpaRockMovedSourceLine '^\s*setspeed\s+SPEED_080\s*$') 'setspeed' 'Impa' $impaSpeed80Match.Groups['value'].Value),
    (& $newImpaRockMovedCommandRow 21 '++[2]' (& $findImpaRockMovedSourceLine '^\s*moveup\s+\$20\s*$') 'move' 'Impa' '00' '20' $impaFollowerAnimations[0]),
    (& $newImpaRockMovedCommandRow 22 '++[2]' (& $findImpaRockMovedSourceLine '^\s*scriptend\s*$') 'scriptend')
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\impa_stone_postpush_commands.tsv'),
    $impaRockMovedCommandRows,
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

# interaction6b_subid00 is native object code rather than interaction-script
# bytecode. Export its linear active path through the same typed catalog while
# retaining the edge predicate and simulated-input playback as native runtime
# handoffs. In particular, counter1 is installed before TX_0100 and its first
# decrement occurs on the first object update after the textbox closes.
$impaHelpBodyStart = $impaHelpBlock.Groups['body'].Index
$impaHelpBodyEnd = $impaHelpBodyStart + $impaHelpBlock.Groups['body'].Length
$findImpaHelpSourceLine = {
    param([string]$pattern, [int]$occurrence = 0)
    return Find-CutsceneCommandSourceLine `
        $impaHelpSource $impaHelpBodyStart $impaHelpBodyEnd `
        $pattern 'interaction6b_subid00' $occurrence
}
$newImpaHelpCommandRow = {
    param(
        [int]$index,
        [int]$line,
        [string]$opcode,
        [string]$arg0 = '',
        [string]$payload = '')
    return New-CutsceneCommandRow `
        'interaction6b_subid00' $index 'interaction6b_subid00' $line `
        $opcode '' $arg0 '' $payload
}
$impaHelpCommandRows = @(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64",
    (& $newImpaHelpCommandRow 0 (& $findImpaHelpSourceLine '^\s*ld\s+\(wMenuDisabled\),a\s*$') 'disablemenu'),
    (& $newImpaHelpCommandRow 1 (& $findImpaHelpSourceLine '^\s*ld\s+\(wDisabledObjects\),a\s*$' 0) 'setdisabledobjectscontinue' '01'),
    (& $newImpaHelpCommandRow 2 (& $findImpaHelpSourceLine '^\s*ld\s+a,30\s*$') 'setcounter' $impaHelpCommand.Groups['postText'].Value),
    (& $newImpaHelpCommandRow 3 (& $findImpaHelpSourceLine '^\s*call\s+showText\s*$') 'showtext' $impaHelpCommand.Groups['text'].Value $allTexts[$impaHelpTextId]),
    (& $newImpaHelpCommandRow 4 (& $findImpaHelpSourceLine '^\s*call\s+@decCounter1IfTextNotActive\s*$') 'waitpreloadedcounter'),
    (& $newImpaHelpCommandRow 5 (& $findImpaHelpSourceLine '^\s*ld\s+\(wDisabledObjects\),a\s*$' 1) 'setdisabledobjectscontinue' '00'),
    (& $newImpaHelpCommandRow 6 (& $findImpaHelpSourceLine '^\s*call\s+setSimulatedInputAddress\s*$') 'native' '' 'installHelpSimulatedInput'),
    (& $newImpaHelpCommandRow 7 (& $findImpaHelpSourceLine '^\s*set\s+6,\(hl\)\s*$') 'orroomflagcontinue' '40'),
    (& $newImpaHelpCommandRow 8 (& $findImpaHelpSourceLine '^\s*jp\s+interactionDelete\s*$') 'scriptend')
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\impa_help_commands.tsv'),
    $impaHelpCommandRows,
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

# Keep the command vocabulary used by implemented and near-term events tied to
# the actual script macros/handlers. A no-carry handler yields for the current
# object update; a carry handler immediately dispatches the next command.
$cutsceneVocabularyRows = @(
    "# opcode`tbytes`trunner-result`tdescription",
    "scriptend`t1`tend`tEnd the interaction script.",
    "scriptjump`t2`tcontinue`tJump and continue dispatch in the same update.",
    "setcoords`t3`tyield`tWrite the actor Y/X bytes.",
    "setangle`t2`tyield`tWrite Interaction.angle.",
    "setspeed`t2`tyield`tWrite Interaction.speed.",
    "applyspeed`t1-or-2`tblock`tWait for counter2; apply speed while nonzero.",
    "setcollisionradii`t3`tyield`tWrite collision radius Y/X.",
    "writeobjectbyte`t3`tyield`tWrite an Interaction byte.",
    "setanimation`t2-or-3`tyield`tSelect a literal, angle, or object-byte animation.",
    "writememory`t4`tcontinue`tWrite one WRAM byte and continue dispatch.",
    "showtext`t2-or-3`tyield`tOpen text; interactionRunScript then waits globally.",
    "showtextdifferentforlinked`t4`tyield`tSelect linked or unlinked text.",
    "orroomflag`t2`tyield`tOR the current room flags.",
    "disablemenu`t1`tcontinue`tDisable the menu.",
    "disableinput`t1`tcontinue`tDisable Link and the menu.",
    "enableinput`t1`tcontinue`tEnable Link and the menu.",
    "callscript`t3`tyield`tStore return address and transfer on the next update.",
    "retscript`t1`tyield`tRestore return address on the next update.",
    "jumpifmemoryeq`t6`tcontinue`tConditionally branch and continue dispatch.",
    "checkmemoryeq`t4`tgate`tHold until the WRAM byte equals the operand.",
    "playsound`t2`tyield`tQueue a sound effect.",
    "moveup/moveright/movedown/moveleft`t2`tblock`tSet direction/animation and install counter2.",
    "wait`t1-or-more`tblock`tPseudo-op selecting delay or setcounter1 records.",
    "asm15`t3-or-4`tcontinue`tRun an object-code handler; carry is forced on return."
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\script_command_vocabulary.tsv'),
    $cutsceneVocabularyRows,
    [Text.UTF8Encoding]::new($false))

# Parse the active Nayru/Ralph/Ghost script lanes with one source-aware reader.
# This is intentionally done before emitting the merged controller stream so a
# newly introduced opcode fails import at its exact source file, line and label.
$supportedNayruOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'setanimation', 'checkmemoryeq', 'wait', 'setspeed', 'moveup',
    'moveright', 'movedown', 'moveleft', 'showtext', 'writememory',
    'asm15', 'setangle', 'applyspeed', 'setcoords', 'writeobjectbyte',
    'playsound', 'orroomflag', 'scriptend', 'callscript',
    'jumpifmemoryeq', 'scriptjump')) {
    [void]$supportedNayruOpcodes.Add($opcode)
}
$nayruScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$nayruLaneSpecs = @(
    @('nayruScript00_part1', '(?ms)^nayruScript00_part1:(?<body>.*?)(?=^nayruScript00_part2:)'),
    @('nayruScript00_part2', '(?ms)^nayruScript00_part2:(?<body>.*?)(?=^nayruScript01:)'),
    @('ralphSubid00Script', '(?ms)^ralphSubid00Script:(?<body>.*?)(?=^ralphSubid02Script:)'),
    @('ghostVeranSubid1Script_part2', '(?ms)^ghostVeranSubid1Script_part2:(?<body>.*?)(?=^ghostVeranSubid1Script:)')
)
foreach ($laneSpec in $nayruLaneSpecs) {
    $laneMatch = [regex]::Match($nayruScriptSource, $laneSpec[1])
    if (-not $laneMatch.Success) {
        throw "$nayruScriptPath`: could not locate $($laneSpec[0]) for typed parsing."
    }
    $parsedLane = @(Read-AssemblyCutsceneCommands `
        $nayruScriptPath $nayruScriptSource $laneSpec[0] `
        $laneMatch.Groups['body'].Index $laneMatch.Groups['body'].Length `
        $supportedNayruOpcodes)
    if ($parsedLane[-1].Opcode -ne 'scriptend') {
        throw "$nayruScriptPath`:$($parsedLane[-1].Line): $($laneSpec[0]) does not terminate in scriptend."
    }
}

# The intro is a multi-object controller: independent interaction scripts,
# Link object code, and native palette/room handlers synchronize through cfd0.
# Export the already validated active-path orchestration as typed records while
# retaining native handlers only for the non-script object code.
$nayruControllerLine = Get-AssemblySourceLine `
    $nayruCutsceneSource '^nayruSingingCutsceneHandler:' 'nayruSingingCutsceneHandler'
$nayruPart1Line = Get-AssemblySourceLine `
    $nayruScriptSource '^nayruScript00_part1:' 'nayruScript00_part1'
$nayruPart2Line = Get-AssemblySourceLine `
    $nayruScriptSource '^nayruScript00_part2:' 'nayruScript00_part2'
$nayruRalphLine = Get-AssemblySourceLine `
    $nayruScriptSource '^ralphSubid00Script:' 'ralphSubid00Script'
$nayruGhostLine = Get-AssemblySourceLine `
    $nayruScriptSource '^ghostVeranSubid1Script_part2:' 'ghostVeranSubid1Script_part2'

$nayruCommandRows = [Collections.Generic.List[string]]::new()
$nayruCommandRows.Add(
    '# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
$addNayruCommand = {
    param(
        [string]$opcode,
        [string]$actor = '',
        [string]$arg0 = '',
        [string]$arg1 = '',
        [string]$payload = '',
        [string]$script = 'nayruSingingCutsceneHandler',
        [int]$line = $nayruControllerLine)
    $nayruCommandRows.Add((New-CutsceneCommandRow `
        $script ($nayruCommandRows.Count - 1) $script $line `
        $opcode $actor $arg0 $arg1 $payload))
}
$nayruWait = { param([int]$frames) & $addNayruCommand 'waitframes' '' $frames '' '' }
$nayruText = { param([string]$id, [string]$script = 'nayruSingingCutsceneHandler', [int]$line = $nayruControllerLine)
    & $addNayruCommand 'dialogue' '' $id '' '' $script $line }
$nayruAnimation = { param([string]$actor, [int]$animation, [string]$script = 'nayruSingingCutsceneHandler', [int]$line = $nayruControllerLine)
    & $addNayruCommand 'setanimation' $actor $animation.ToString('x2') '' '' $script $line }
$nayruMove = { param([string]$actor, [double]$dx, [double]$dy, [int]$frames, [int]$animation = -1, [bool]$setAnimation = $false, [string]$script = 'nayruSingingCutsceneHandler', [int]$line = $nayruControllerLine)
    $payload = @(
        $dx.ToString([Globalization.CultureInfo]::InvariantCulture),
        $dy.ToString([Globalization.CultureInfo]::InvariantCulture),
        $(if ($setAnimation) { '1' } else { '0' })) -join ','
    & $addNayruCommand 'translate' $actor $frames $animation $payload $script $line }
$nayruParallelMove = { param([string]$actor, [double]$dx, [double]$dy, [int]$frames, [string]$actor2, [double]$dx2, [double]$dy2, [int]$frames2)
    $first = $dx.ToString([Globalization.CultureInfo]::InvariantCulture) + ',' +
        $dy.ToString([Globalization.CultureInfo]::InvariantCulture)
    $second = $dx2.ToString([Globalization.CultureInfo]::InvariantCulture) + ',' +
        $dy2.ToString([Globalization.CultureInfo]::InvariantCulture)
    & $addNayruCommand 'paralleltranslate' $actor $frames $frames2 "$first|$actor2|$second" }
$nayruNative = { param([string]$handler)
    & $addNayruCommand 'nativeyield' '' '' '' $handler }
$nayruBlock = { param([string]$handler, [int]$frames, [string]$actor = '', [string]$arguments = '')
    $payload = if ([string]::IsNullOrEmpty($arguments)) { $handler } else { "$handler`0$arguments" }
    & $addNayruCommand 'nativeblock' $actor $frames '' $payload }
$nayruSound = { param([string]$sound)
    & $addNayruCommand 'playsound' '' $sound '' '' }

& $nayruNative 'SetupNayruPossessionScene'
& $nayruBlock 'Fade' 11 '' 'in'
& $nayruWait 30; & $nayruBlock 'Jump' 1 'Ralph'; & $nayruWait 30
& $nayruText '2a00' 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 30
& $nayruNative 'FacePlayerUp'; & $nayruAnimation 'Nayru' 2 'nayruScript00_part1' $nayruPart1Line; & $nayruWait 10
& $nayruMove 'Nayru' 0 8 32 2 $true 'nayruScript00_part1' $nayruPart1Line
& $nayruWait 30; & $nayruText '1d00' 'nayruScript00_part1' $nayruPart1Line; & $nayruWait 30
& $nayruNative 'FacePlayerRight'; & $nayruBlock 'Jump' 1 'Ralph'; & $nayruWait 10
& $nayruText '2a22' 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 30
& $nayruWait 40; & $nayruNative 'FacePlayerUp'; & $nayruText '1d22' 'nayruScript00_part1' $nayruPart1Line; & $nayruWait 30
& $nayruAnimation 'Impa' 2; & $nayruWait 30; & $nayruNative 'FastMusicFadeOut'; & $nayruWait 30
& $nayruMove 'Impa' 32 0 32 1 $true; & $nayruWait 8
& $nayruMove 'Impa' 0 -16 16 0 $true; & $nayruWait 30
& $nayruNative 'PlaySideviewMusic'; & $nayruAnimation 'Impa' 4; & $nayruWait 240
& $nayruText '5600'; & $nayruNative 'FacePlayerDown'; & $nayruNative 'AlarmNayruAudience'
& $nayruWait 60; & $nayruAnimation 'Impa' 0; & $nayruWait 60; & $nayruText '5606'; & $nayruWait 10
& $nayruAnimation 'Impa' 7
& $nayruMove 'Impa' -33.259663 13.776604 72 7 $false
& $nayruNative 'SpawnGhostVeran'; & $nayruBlock 'RoomPalette' 32
& $nayruNative 'BeginNayruAudienceEscape'; & $nayruWait 58
& $nayruMove 'GhostVeran' 0 -22.5 90; & $nayruWait 60
& $nayruAnimation 'Ralph' 2 'ralphSubid00Script' $nayruRalphLine
& $nayruNative 'PlayDoubleUnknown5'
& $nayruParallelMove 'Player' -33 0 22 'Ralph' 0 33 22
& $nayruSound '75'; & $nayruWait 6; & $nayruMove 'Player' 0 12 8
& $nayruSound '75'; & $nayruWait 84
& $nayruSound '6b'; & $nayruMove 'GhostVeran' -48.08326 -48.08326 17; & $nayruWait 8
& $nayruSound '6b'; & $nayruMove 'GhostVeran' 123.0575 82.224396 37; & $nayruWait 8
& $nayruSound '6b'; & $nayruMove 'GhostVeran' -76 0 19; & $nayruWait 8
& $nayruSound '6b'; & $nayruMove 'GhostVeran' 38.26834 -92.38795 25; & $nayruWait 8
& $nayruSound '6b'; & $nayruMove 'GhostVeran' 44.346214 18.368805 12; & $nayruWait 8
& $nayruSound '6b'; & $nayruMove 'GhostVeran' -48.08326 48.08326 17; & $nayruWait 30
& $nayruNative 'SpawnHumanVeran'; & $nayruBlock 'Flicker' 120 'GhostVeran'; & $nayruWait 120
& $nayruAnimation 'HumanVeran' 1; & $nayruWait 30; & $nayruText '5601'; & $nayruWait 30
& $nayruAnimation 'HumanVeran' 0; & $nayruWait 60; & $nayruSound '8d'
& $nayruBlock 'Flicker' 120 'GhostVeran' 'PlaySwordObtained'
& $nayruNative 'HideHumanVeran'; & $nayruWait 30
& $nayruMove 'GhostVeran' 33.258785 22.222809 80; & $nayruWait 30
& $nayruText '5602'; & $nayruWait 30; & $nayruNative 'BeginGhostRumble'; & $nayruWait 120
& $nayruMove 'GhostVeran' 0 10.25 41; & $nayruWait 60
& $nayruNative 'BeginGhostCharge'; & $nayruParallelMove 'GhostVeran' 0 -102 34 'Nayru' 0 -8 32
& $nayruNative 'FinishGhostCharge'; & $nayruBlock 'Fade' 32 '' 'out'
& $nayruWait 60; & $nayruNative 'HideGhostVeranAfterPossession'
& $nayruNative 'BeginNayruPossessionRecovery'; & $nayruBlock 'Fade' 97 '' 'in'
& $nayruWait 452; & $nayruWait 120
& $nayruMove 'Ralph' -16 0 16 3 $true 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 6
& $nayruNative 'SpawnRalphSword'; & $nayruMove 'Ralph' 0 -24 24 0 $true 'ralphSubid00Script' $nayruRalphLine
& $nayruWait 30; & $nayruAnimation 'Ralph' 4 'ralphSubid00Script' $nayruRalphLine
& $nayruSound '74'; & $nayruWait 60; & $nayruText '2a01' 'ralphSubid00Script' $nayruRalphLine
& $nayruWait 30; & $nayruText '5603' 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 60
& $nayruAnimation 'Ralph' 0 'ralphSubid00Script' $nayruRalphLine
& $nayruMove 'Ralph' 0 16 129 0 $false 'ralphSubid00Script' $nayruRalphLine
& $nayruWait 30; & $nayruText '5604' 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 60
& $nayruNative 'SpawnPortalLightning'; & $nayruWait 2; & $nayruNative 'ActivateNayruPortal'; & $nayruWait 1; & $nayruWait 60
& $nayruMove 'GhostVeran' 0 17.5 35 '0' $false 'ghostVeranSubid1Script_part2' $nayruGhostLine
& $nayruWait 10; & $nayruNative 'HideGhostVeran'; & $nayruWait 60
& $nayruBlock 'PortalFlight' 1 'Nayru'; & $nayruWait 20
& $nayruMove 'Ralph' 0 -48 48 0 $true 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 6
& $nayruMove 'Ralph' -49 0 49 3 $true 'ralphSubid00Script' $nayruRalphLine
& $nayruWait 40; & $nayruText '5605' 'nayruScript00_part2' $nayruPart2Line; & $nayruWait 60
& $nayruMove 'Nayru' 0 -17 17 0 $true 'nayruScript00_part2' $nayruPart2Line
& $nayruSound '95'; & $nayruBlock 'Flicker' 120 'Nayru'; & $nayruNative 'HideNayru'; & $nayruWait 120
& $nayruNative 'MediumMusicFadeOut'; & $nayruWait 90; & $nayruText '5607'; & $nayruWait 90
& $nayruBlock 'Fade' 11 '' 'out'; & $nayruNative 'BeginNayruVignette0'; & $nayruBlock 'Fade' 11 '' 'in'; & $nayruWait 926
& $nayruBlock 'Fade' 11 '' 'out'; & $nayruNative 'BeginNayruVignette1'; & $nayruBlock 'Fade' 11 '' 'in'; & $nayruWait 589
& $nayruBlock 'Fade' 11 '' 'out'; & $nayruNative 'BeginNayruVignette2'; & $nayruBlock 'Fade' 11 '' 'in'; & $nayruWait 634
& $nayruBlock 'Fade' 11 '' 'out'; & $nayruNative 'BeginNayruAftermath'; & $nayruBlock 'Fade' 11 '' 'in'
& $nayruWait 120; & $nayruText '2a02'; & $nayruWait 30
& $nayruMove 'AftermathRalph' 16 0 129 9 $false; & $nayruAnimation 'AftermathRalph' 8
& $nayruWait 120; & $nayruText '2a03'; & $nayruWait 120; & $nayruAnimation 'AftermathRalph' 9
& $nayruWait 10; & $nayruAnimation 'AftermathRalph' 10; & $nayruWait 60
& $nayruMove 'AftermathRalph' -17 0 102 10 $false; & $nayruWait 30
& $nayruText '2a04'; & $nayruWait 120; & $nayruWait 60; & $nayruAnimation 'AftermathRalph' 2
& $nayruText '2a05'; & $nayruWait 30; & $nayruMove 'AftermathRalph' 50 0 25 1 $true
& $nayruAnimation 'AftermathRalph' 2; & $nayruSound '78'; & $nayruWait 120
& $nayruText '2a06'; & $nayruWait 30; & $nayruMove 'AftermathRalph' 0 120 40 2 $true
& $nayruWait 60; & $nayruNative 'FinishAftermathRalphDeparture'
& $nayruWait 80; & $nayruMove 'Player' 0 48 48; & $nayruWait 8
& $nayruMove 'Player' -16 0 16; & $nayruWait 60; & $nayruWait 120
& $nayruNative 'RestoreAftermathImpa'; & $nayruWait 60; & $nayruAnimation 'AftermathImpa' 3
& $nayruWait 50; & $nayruAnimation 'AftermathImpa' 1; & $nayruWait 30
& $nayruAnimation 'AftermathImpa' 3; & $nayruWait 10; & $nayruAnimation 'AftermathImpa' 1
& $nayruWait 60; & $nayruText '0110'; & $nayruWait 30; & $nayruAnimation 'AftermathImpa' 3
& $nayruWait 30; & $nayruText '0112'; & $nayruWait 30; & $nayruAnimation 'AftermathImpa' 1
& $nayruText '0115'; & $nayruWait 30; & $nayruNative 'BeginNayruSwordGift'
& $nayruNative 'GrantNayruSword'; & $nayruText '001c'; & $nayruNative 'RemoveNayruSwordEffect'
& $nayruWait 30; & $nayruNative 'FacePlayerLeft'; & $nayruWait 30; & $nayruText '0117'; & $nayruWait 30
& $nayruMove 'AftermathImpa' 65 0 65 1 $true; & $nayruWait 8
& $nayruMove 'AftermathImpa' 0 33 33 2 $true; & $nayruWait 30
& $nayruNative 'RestoreRoomMusic'; & $nayruWait 30
& $addNayruCommand 'scriptend' '' '' '' ''

if ($nayruCommandRows.Count -lt 200) {
    throw "Initial Nayru typed command stream is unexpectedly short ($($nayruCommandRows.Count - 1) records)."
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\nayru_intro_commands.tsv'),
    $nayruCommandRows,
    [Text.UTF8Encoding]::new($false))

# Room 1:75's pre-Black Tower sequence is seven synchronized interaction
# lanes. Export the original per-actor scripts independently; runtime advances
# them in placement order and preserves their cfc0/cfd0 gates.
$preBlackTowerMainScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$preBlackTowerHelperScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$preBlackTowerMainScriptSource = Get-Content -Raw $preBlackTowerMainScriptPath
$preBlackTowerHelperScriptSource = Get-Content -Raw $preBlackTowerHelperScriptPath
$preBlackTowerOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'wait', 'showtext', 'writememory', 'setspeed', 'moveup',
    'moveright', 'movedown', 'moveleft', 'setanimation',
    'checkmemoryeq', 'checkobjectbyteeq', 'applyspeed', 'asm15',
    'xorcfc0bit', 'checkcfc0bit', 'spawninteraction',
    'writeobjectword', 'scriptend')) {
    [void]$preBlackTowerOpcodes.Add($opcode)
}

$preBlackTowerActorIds = @{
    'Ralph' = 0x37
    'Impa' = 0x31
    'Nayru' = 0x36
    'Zelda' = 0xad
}
$preBlackTowerDirection = @{
    'DIR_UP' = 0
    'DIR_RIGHT' = 1
    'DIR_DOWN' = 2
    'DIR_LEFT' = 3
}
$preBlackTowerMovement = @{
    'moveup' = @(0x00, 0)
    'moveright' = @(0x08, 1)
    'movedown' = @(0x10, 2)
    'moveleft' = @(0x18, 3)
}

function Convert-PreBlackTowerHex([string]$value) {
    $trimmed = $value.Trim()
    if ($trimmed -match '^\$(?<hex>[0-9a-f]+)$') {
        return [Convert]::ToInt32($Matches['hex'], 16)
    }
    return [Convert]::ToInt32($trimmed, 10)
}

function Export-PreBlackTowerLane {
    param(
        [string]$script,
        [string]$actor,
        [string]$path,
        [string]$source,
        [string]$nextLabel,
        [string]$outputName)

    $endPattern = if ([string]::IsNullOrEmpty($nextLabel)) {
        '\z'
    } else {
        "^$([regex]::Escape($nextLabel)):"
    }
    $match = [regex]::Match(
        $source,
        "(?ms)^$([regex]::Escape($script)):(?<body>.*?)(?=$endPattern)")
    if (-not $match.Success) {
        throw "$path`: could not locate typed pre-Black Tower lane $script."
    }
    $parsed = @(Read-AssemblyCutsceneCommands `
        $path $source $script $match.Groups['body'].Index `
        $match.Groups['body'].Length $preBlackTowerOpcodes)
    if ($parsed[-1].Opcode -ne 'scriptend') {
        throw "$path`:$($parsed[-1].Line): $script does not terminate in scriptend."
    }

    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add('# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
    foreach ($command in $parsed) {
        $opcode = [string]$command.Opcode
        $operands = [string]$command.Operands
        $runtimeOpcode = $opcode
        $runtimeActor = ''
        $arg0 = ''
        $arg1 = ''
        $payload = ''

        switch ($opcode) {
            'wait' {
                $arg0 = (Convert-PreBlackTowerHex $operands).ToString()
            }
            'showtext' {
                if ($operands -notmatch '^TX_(?<text>[0-9a-f]{4})$') {
                    throw "$path`:$($command.Line): unsupported showtext operand '$operands'."
                }
                $textId = [Convert]::ToInt32($Matches['text'], 16)
                if (-not $allTexts.ContainsKey($textId)) {
                    throw "$path`:$($command.Line): missing TX_$($Matches['text'])."
                }
                $arg0 = $Matches['text']
                $payload = $allTexts[$textId]
            }
            'writememory' {
                $parts = $operands -split '\s*,\s*'
                if ($parts.Count -ne 2) {
                    throw "$path`:$($command.Line): malformed writememory '$operands'."
                }
                $value = if ($preBlackTowerDirection.ContainsKey($parts[1])) {
                    $preBlackTowerDirection[$parts[1]]
                } else { Convert-PreBlackTowerHex $parts[1] }
                $arg0 = ([int]$value).ToString('x2')
                $payload = switch -Regex ($parts[0]) {
                    'wTmpcfc0\.genericCutscene\.cfd0' { 'SharedSignal'; break }
                    'w1Link\.direction' { 'PlayerDirection'; break }
                    default { throw "$path`:$($command.Line): unsupported writememory binding '$($parts[0])'." }
                }
            }
            'setspeed' {
                if ($operands -notmatch '^SPEED_(?<speed>[0-9a-f]+)$') {
                    throw "$path`:$($command.Line): unsupported speed '$operands'."
                }
                $runtimeActor = $actor
                $speedName = $Matches['speed'].TrimStart('0')
                if ([string]::IsNullOrEmpty($speedName)) { $speedName = '0' }
                $arg0 = (Resolve-ObjectSpeed $speedName).ToString('x2')
            }
            { $preBlackTowerMovement.ContainsKey($_) } {
                $movement = $preBlackTowerMovement[$opcode]
                $animation = [int]$movement[1]
                $runtimeOpcode = 'move'
                $runtimeActor = $actor
                $arg0 = ([int]$movement[0]).ToString('x2')
                $arg1 = (Convert-PreBlackTowerHex $operands).ToString('x2')
                $payload = Resolve-NpcAnimation $preBlackTowerActorIds[$actor] $animation
                if (-not $payload) {
                    throw "$path`:$($command.Line): missing $actor movement animation $animation."
                }
            }
            'setanimation' {
                $animation = Convert-PreBlackTowerHex $operands
                $runtimeActor = $actor
                $arg0 = $animation.ToString('x2')
                $payload = Resolve-NpcAnimation $preBlackTowerActorIds[$actor] $animation
                if (-not $payload) {
                    throw "$path`:$($command.Line): missing $actor animation $animation."
                }
            }
            'checkmemoryeq' {
                $parts = $operands -split '\s*,\s*'
                if ($parts.Count -ne 2 -or $parts[0] -ne 'wTmpcfc0.genericCutscene.cfd0') {
                    throw "$path`:$($command.Line): unsupported checkmemoryeq '$operands'."
                }
                $arg0 = (Convert-PreBlackTowerHex $parts[1]).ToString('x2')
                $payload = 'SharedSignal'
            }
            'checkobjectbyteeq' {
                $parts = $operands -split '\s*,\s*'
                if ($parts.Count -ne 2) {
                    throw "$path`:$($command.Line): malformed checkobjectbyteeq '$operands'."
                }
                $runtimeOpcode = 'checkmemoryeq'
                $arg0 = (Convert-PreBlackTowerHex $parts[1]).ToString('x2')
                $payload = switch ($parts[0]) {
                    'Interaction.substate' { "${actor}Substate" }
                    'Interaction.var38' { "${actor}Var38" }
                    default { throw "$path`:$($command.Line): unsupported object binding '$($parts[0])'." }
                }
            }
            'applyspeed' {
                $runtimeActor = $actor
                $arg0 = (Convert-PreBlackTowerHex $operands).ToString('x2')
            }
            'asm15' {
                if ($operands -match '^setGlobalFlag,\s*GLOBALFLAG_RALPH_ENTERED_BLACK_TOWER$') {
                    $runtimeOpcode = 'setglobalflag'
                    $arg0 = '45'
                } elseif ($operands -match '^scriptHelp\.ralph_createExclamationMarkShiftedRight,\s*\$1e$') {
                    $runtimeOpcode = 'native'
                    $payload = 'CreateLinkedExclamation'
                } else {
                    throw "$path`:$($command.Line): unsupported asm15 handler '$operands'."
                }
            }
            'xorcfc0bit' {
                $bit = Convert-PreBlackTowerHex $operands
                $runtimeOpcode = 'writememory'
                $arg0 = (1 -shl $bit).ToString('x2')
                $payload = 'ToggleSharedBit'
            }
            'checkcfc0bit' {
                $bit = Convert-PreBlackTowerHex $operands
                $runtimeOpcode = 'checkmemoryeq'
                $arg0 = '01'
                $payload = "SharedBit$bit"
            }
            'spawninteraction' {
                if ($operands -ne 'INTERAC_NAYRU, $09, $f8, $48') {
                    throw "$path`:$($command.Line): unexpected spawninteraction '$operands'."
                }
                $runtimeOpcode = 'nativeyield'
                $payload = 'SpawnNayru09'
            }
            'writeobjectword' {
                if ($operands -ne 'Interaction.speedZ, -$180') {
                    throw "$path`:$($command.Line): unexpected writeobjectword '$operands'."
                }
                $runtimeOpcode = 'nativeyield'
                $payload = "Begin${actor}Jump"
            }
            'scriptend' { }
            default {
                throw "$path`:$($command.Line): unsupported converted opcode '$opcode'."
            }
        }

        $rows.Add((New-CutsceneCommandRow `
            $script $command.Index $command.Label $command.Line `
            $runtimeOpcode $runtimeActor $arg0 $arg1 $payload))
    }
    [IO.File]::WriteAllLines(
        (Join-Path $destination "cutscenes\$outputName"),
        $rows,
        [Text.UTF8Encoding]::new($false))
}

$preBlackTowerLaneSpecs = @(
    @('ralphSubid0aScript_unlinked', 'Ralph', $preBlackTowerMainScriptPath, $preBlackTowerMainScriptSource, 'ralphSubid0aScript_linked', 'pre_black_tower_ralph_unlinked.tsv'),
    @('ralphSubid0aScript_linked', 'Ralph', $preBlackTowerMainScriptPath, $preBlackTowerMainScriptSource, 'ralphSubid0bScript', 'pre_black_tower_ralph_linked.tsv'),
    @('impaScript4', 'Impa', $preBlackTowerHelperScriptPath, $preBlackTowerHelperScriptSource, 'impaScript5', 'pre_black_tower_impa_unlinked.tsv'),
    @('impaScript5', 'Impa', $preBlackTowerHelperScriptPath, $preBlackTowerHelperScriptSource, 'impaScript7', 'pre_black_tower_impa_linked.tsv'),
    @('nayruScript09', 'Nayru', $preBlackTowerMainScriptPath, $preBlackTowerMainScriptSource, 'nayruScript0a', 'pre_black_tower_nayru_unlinked.tsv'),
    @('nayruScript0a', 'Nayru', $preBlackTowerMainScriptPath, $preBlackTowerMainScriptSource, 'nayruScript10', 'pre_black_tower_nayru_linked.tsv'),
    @('zeldaSubid04Script', 'Zelda', $preBlackTowerMainScriptPath, $preBlackTowerMainScriptSource, 'zeldaSubid05Script', 'pre_black_tower_zelda_linked.tsv')
)
foreach ($lane in $preBlackTowerLaneSpecs) {
    Export-PreBlackTowerLane @lane
}

$preBlackTowerExclamationGraphic = $interactionGraphics['159:0']
$preBlackTowerExclamationAnimation = Resolve-NpcAnimation 0x9f 0
if ($null -eq $preBlackTowerExclamationGraphic -or
    -not $gfxNames.ContainsKey($preBlackTowerExclamationGraphic.Gfx) -or
    -not $preBlackTowerExclamationAnimation) {
    throw 'Could not resolve the pre-Black Tower exclamation effect graphics.'
}
$preBlackTowerEventRows = @(
    "# group`troom`tmaku-seed`tcompletion-flag`tralph-entered-flag`tclink-sound`tgravity`tralph-id`tralph-subid`timpa-id`timpa-unlinked-subid`timpa-linked-subid`tnayru-id`tnayru-linked-subid`tnayru-spawned-subid`tzelda-id`tzelda-subid`teffect-id`teffect-subid`teffect-sprite`teffect-tile-base`teffect-palette`teffect-animation",
    (@(
        '1', '75', '36', '33', '45', '50', '20', '37', '0a', '31', '04', '05',
        '36', '0a', '09', 'ad', '04', '9f', '00',
        $gfxNames[$preBlackTowerExclamationGraphic.Gfx],
        $preBlackTowerExclamationGraphic.TileBase.ToString(),
        $preBlackTowerExclamationGraphic.Palette.ToString(),
        $preBlackTowerExclamationAnimation
    ) -join "`t")
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\pre_black_tower_event.tsv'),
    $preBlackTowerEventRows,
    [Text.UTF8Encoding]::new($false))

# Room 1:86's guard starts stage 0 of CUTSCENE_BLACK_TOWER_EXPLANATION, then
# resumes at @cutsceneAftermath after the cutscene's same-room transition $0c.
$blackTowerScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$blackTowerScriptSource = Get-Content -Raw $blackTowerScriptPath
$blackTowerScriptMatch = [regex]::Match(
    $blackTowerScriptSource,
    '(?ms)^hardhatWorkerSubid02Script:(?<body>.*?)(?=^hardhatWorkerSubid03Script:)')
if (-not $blackTowerScriptMatch.Success) {
    throw 'Could not locate hardhatWorkerSubid02Script for room 1:86.'
}
$blackTowerBodyStart = $blackTowerScriptMatch.Groups['body'].Index
$blackTowerBodyEnd = $blackTowerBodyStart + $blackTowerScriptMatch.Groups['body'].Length
function Get-BlackTowerGuardLine([string]$pattern, [int]$occurrence = 0) {
    return Find-CutsceneCommandSourceLine `
        $blackTowerScriptSource $blackTowerBodyStart $blackTowerBodyEnd `
        $pattern 'hardhatWorkerSubid02Script' $occurrence
}
foreach ($textId in @(0x1003, 0x1004, 0x1005, 0x1006)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Room 1:86 is missing TX_$($textId.ToString('x4'))."
    }
}
$blackTowerRightAnimation = Resolve-NpcAnimation 0x58 1
$blackTowerMoveSpeed = Resolve-ObjectSpeed '80'
if (-not $blackTowerRightAnimation -or $blackTowerMoveSpeed -ne 0x14) {
    throw 'Could not resolve the hardhat worker right-facing animation or SPEED_080 raw value.'
}

$blackTowerFirstRows = [Collections.Generic.List[string]]::new()
$blackTowerFirstRows.Add('# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
$firstSpec = @(
    @('disableinput', '', '', '', '', '^\s*disableinput\s*$', 0),
    @('showtext', '', '1003', '', $allTexts[0x1003], '^\s*showtextlowindex\s+<TX_1003\s*$', 0),
    @('wait', '', '30', '', '', '^\s*wait\s+30\s*$', 0),
    @('orroomflag', '', '40', '', '', '^\s*orroomflag\s+\$40\s*$', 0),
    @('native', '', '', '', 'StoreLink', '^\s*asm15\s+hardhatWorker_storeLinkVarsSomewhere\s*$', 0),
    @('writememory', '', '00', '', 'CutsceneStage', '^\s*writememory\s+wGenericCutscene\.cbb8,\s*\$00\s*$', 0),
    @('writememory', '', '08', '', 'CutsceneTrigger', '^\s*writememory\s+wCutsceneTrigger,\s*CUTSCENE_BLACK_TOWER_EXPLANATION\s*$', 0),
    @('scriptend', '', '', '', '', '^\s*scriptend\s*$', 0)
)
for ($index = 0; $index -lt $firstSpec.Count; $index++) {
    $spec = $firstSpec[$index]
    $blackTowerFirstRows.Add((New-CutsceneCommandRow `
        'hardhatWorkerSubid02Script:first' $index 'hardhatWorkerSubid02Script' `
        (Get-BlackTowerGuardLine $spec[5] ([int]$spec[6])) `
        $spec[0] $spec[1] $spec[2] $spec[3] $spec[4]))
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\black_tower_guard_first.tsv'),
    $blackTowerFirstRows,
    [Text.UTF8Encoding]::new($false))

$blackTowerAfterRows = [Collections.Generic.List[string]]::new()
$blackTowerAfterRows.Add('# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
$afterSpec = @(
    @('disableinput', '', '', '', '', '^\s*disableinput\s*$', 1),
    @('native', '', '', '', 'TurnToFaceLink', '^\s*asm15\s+turnToFaceLink\s*$', 0),
    @('gate', '', '', '', 'palette-fade-done', '^\s*checkpalettefadedone\s*$', 0),
    @('wait', '', '60', '', '', '^\s*wait\s+60\s*$', 0),
    @('showtext', '', '1006', '', $allTexts[0x1006], '^\s*showtextlowindex\s+<TX_1006\s*$', 0),
    @('native', '', '', '', 'MoveLinkAway', '^\s*asm15\s+hardhatWorker_moveLinkAway\s*$', 0),
    @('writeobjectbyte', 'Guard', '38', '01', '', '^\s*writeobjectbyte\s+Interaction\.var38,\s*\$01\s*$', 0),
    @('wait', '', '30', '', '', '^\s*wait\s+30\s*$', 1),
    @('setspeed', 'Guard', ($blackTowerMoveSpeed.ToString('x2')), '', '', '^\s*setspeed\s+SPEED_080\s*$', 0),
    @('move', 'Guard', '08', '21', $blackTowerRightAnimation, '^\s*moveright\s+\$21\s*$', 0),
    @('writeobjectbyte', 'Guard', '38', '00', '', '^\s*writeobjectbyte\s+Interaction\.var38,\s*\$00\s*$', 0),
    @('wait', '', '30', '', '', '^\s*wait\s+30\s*$', 2),
    @('orroomflag', '', '80', '', '', '^\s*orroomflag\s+\$80\s*$', 0),
    @('writememory', '', '00', '', 'SimulatedInput', '^\s*writememory\s+wUseSimulatedInput,\s*\$00\s*$', 0),
    @('enableinput', '', '', '', '', '^\s*enableinput\s*$', 0),
    @('scriptend', '', '', '', '', '^\s*enableinput\s*$', 0)
)
for ($index = 0; $index -lt $afterSpec.Count; $index++) {
    $spec = $afterSpec[$index]
    $blackTowerAfterRows.Add((New-CutsceneCommandRow `
        'hardhatWorkerSubid02Script:aftermath' $index '@cutsceneAftermath' `
        (Get-BlackTowerGuardLine $spec[5] ([int]$spec[6])) `
        $spec[0] $spec[1] $spec[2] $spec[3] $spec[4]))
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\black_tower_guard_aftermath.tsv'),
    $blackTowerAfterRows,
    [Text.UTF8Encoding]::new($false))

$blackTowerCutsceneSource = Get-Content -Raw (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
if ($blackTowerCutsceneSource -notmatch '(?ms)^blackTowerExplanationCutsceneHandler:.*?^@@table_6625:\s+\.db GFXH_BLACK_TOWER_STAGE_1_LAYOUT, GFXH_BLACK_TOWER_BASE' -or
    $blackTowerCutsceneSource -notmatch '(?ms)^func_6ef7:.*?and \$1f.*?call getRandomNumber.*?and \$07.*?SND_LIGHTNING' -or
    $blackTowerCutsceneSource -notmatch '(?ms)^func_6f44:.*?oamData_714c') {
    throw 'Black Tower explanation stage-0 presentation changed.'
}
$blackTowerOamSource = Get-Content -Raw (Join-Path $Disassembly 'ages.s')
$blackTowerOamMatch = [regex]::Match(
    $blackTowerOamSource,
    '(?ms)^oamData_714c:\s+\.db \$10(?<body>.*?)(?=^oamData_718d:)')
$blackTowerOamEntries = [regex]::Matches(
    $blackTowerOamMatch.Groups['body'].Value,
    '(?m)^\s*\.db \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})\s*$')
if (-not $blackTowerOamMatch.Success -or $blackTowerOamEntries.Count -ne 16) {
    throw 'Could not import stage-0 Black Tower OAM data $714c.'
}
$blackTowerOamRows = [Collections.Generic.List[string]]::new()
$blackTowerOamRows.Add("# index`ty`tx`ttile`tflags`tsource")
for ($index = 0; $index -lt $blackTowerOamEntries.Count; $index++) {
    $entry = $blackTowerOamEntries[$index]
    $blackTowerOamRows.Add(
        "$index`t$($entry.Groups['y'].Value)`t$($entry.Groups['x'].Value)`t$($entry.Groups['tile'].Value)`t$($entry.Groups['flags'].Value)`tages.s:oamData_714c")
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\black_tower_stage_0_oam.tsv'),
    $blackTowerOamRows,
    [Text.UTF8Encoding]::new($false))

foreach ($asset in @(
    @('map_black_tower_stage_1.bin', 'map_black_tower_stage_1.bin'),
    @('flg_black_tower_stage_1.bin', 'flags_black_tower_stage_1.bin'),
    @('map_black_tower_base.bin', 'map_black_tower_base.bin'),
    @('flg_black_tower_base.bin', 'flags_black_tower_base.bin'),
    @('gfx_black_tower_scene_1.png', 'gfx_black_tower_scene_1.png'),
    @('gfx_black_tower_scene_2.png', 'gfx_black_tower_scene_2.png'),
    @('gfx_black_tower_scene_3.png', 'gfx_black_tower_scene_3.png'),
    @('gfx_black_tower_scene_4.png', 'gfx_black_tower_scene_4.png'),
    @('spr_black_tower_scene.png', 'spr_black_tower_scene.png'))) {
    Copy-GeneratedFile `
        "gfx_compressible\ages\$($asset[0])" `
        "cutscenes\$($asset[1])"
}
Export-PaletteBlock 'paletteData57e0' 28 'cutscenes\black_tower_bg_palette.bin'
Export-PaletteBlock 'paletteData5818' 32 'cutscenes\black_tower_sprite_palette.bin'

$blackTowerEventRows = @(
    "# group`troom`tguard-id`tguard-subid`tessence-mask`titem-flag`taftermath-flag`tcomplete-flag`tinitial-y`tinitial-x`tcompleted-y`tcompleted-x`tmove-speed`tmove-counter`tscreen-offset-y`tintro-wait`tpost-wait`tsource-transition`tdestination-transition`texplanation-text-id`texplanation-text-base64",
    (@(
        '1', '86', '58', '02', '08', '20', '40', '80', '38', '48', '38', '58',
        $blackTowerMoveSpeed.ToString('x2'), '21', '70', '60', '60', '04', '0c', '1005',
        (ConvertTo-CutsceneCommandPayload $allTexts[0x1005])
    ) -join "`t")
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\black_tower_entrance_event.tsv'),
    $blackTowerEventRows,
    [Text.UTF8Encoding]::new($false))

# Room $1:$76 contains INTERAC_MISCELLANEOUS_2 $dc:$10 rather than a visible
# NPC. It opens the two entrance metatiles and arms a collision rectangle that
# selects one of two hardcoded Black Tower rooms from this room's bit $01.
# Keep the placement, state-machine inputs, flag predicate, raw warp bytes, and
# sound tied to their disassembly definitions instead of encoding them in the
# runtime controller.
$towerDoorObjectSource = Get-Content -Raw (
    Join-Path $Disassembly 'objects\ages\mainData.s')
$towerDoorPlacement = [regex]::Match(
    $towerDoorObjectSource,
    '(?ms)^group(?<group>1)Map(?<room>76)ObjectData:\s*' +
    'obj_Interaction \$(?<id>[0-9a-f]{2}) \$(?<subid>[0-9a-f]{2}) ' +
    '\$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})\s*obj_End')
if (-not $towerDoorPlacement.Success -or
    [Convert]::ToInt32($towerDoorPlacement.Groups['group'].Value, 16) -ne 1 -or
    [Convert]::ToInt32($towerDoorPlacement.Groups['room'].Value, 16) -ne 0x76 -or
    [Convert]::ToInt32($towerDoorPlacement.Groups['id'].Value, 16) -ne 0xdc -or
    [Convert]::ToInt32($towerDoorPlacement.Groups['subid'].Value, 16) -ne 0x10) {
    throw 'Could not resolve room 1:76 INTERAC_MISCELLANEOUS_2 $dc:$10 placement.'
}

$towerDoorSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous2.s')
$towerDoorHandler = [regex]::Match(
    $towerDoorSource,
    '(?ms)^interactiondc_subid10:(?<body>.*?)(?=^interactiondc_subid11:)')
if (-not $towerDoorHandler.Success) {
    throw 'Could not resolve interactiondc_subid10.'
}
$towerDoorBody = $towerDoorHandler.Groups['body'].Value
$towerDoorClear = [regex]::Match(
    $towerDoorBody,
    'ld hl,wRoomLayout\+\$(?<position>[0-9a-f]{2})\s*xor a\s*ldi \(hl\),a\s*ld \(hl\),a')
$towerDoorRadii = [regex]::Match(
    $towerDoorBody, 'ld bc,\$(?<y>[0-9a-f]{2})(?<x>[0-9a-f]{2})\s*call objectSetCollideRadii')
$towerDoorFlag = [regex]::Match(
    $towerDoorBody, 'call getThisRoomFlags\s*and \$(?<mask>[0-9a-f]{2})')
$towerDoorWarps = [regex]::Matches(
    $towerDoorBody,
    '(?m)^@warp[12]:\s*\r?\n\s*m_HardcodedWarpA ROOM_AGES_(?<group>[0-7])(?<room>[0-9a-f]{2}), ' +
    '\$(?<transition>[0-9a-f]{2}), \$(?<position>[0-9a-f]{2}), \$(?<transition2>[0-9a-f]{2})')
if (-not $towerDoorClear.Success -or -not $towerDoorRadii.Success -or
    -not $towerDoorFlag.Success -or $towerDoorWarps.Count -ne 2 -or
    $towerDoorBody -notmatch '(?ms)@state0:.*?call objectCheckCollidedWithLink_notDeadAndNotGrabbing\s*call nc,interactionIncState\s*jp interactionIncState' -or
    $towerDoorBody -notmatch '(?ms)@state1:.*?call objectCheckCollidedWithLink_notDeadAndNotGrabbing\s*ret c\s*jp interactionIncState' -or
    $towerDoorBody -notmatch '(?ms)@state2:.*?call objectCheckCollidedWithLink_notDeadAndNotGrabbing\s*ret nc\s*call checkLinkVulnerable\s*ret nc' -or
    $towerDoorBody -notmatch 'ld a,SND_ENTERCAVE\s*call playSound') {
    throw 'Room 1:76 tower-door collision handler changed.'
}

$linkSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\specialObjects\link.s')
$linkRadii = [regex]::Match(
    $linkSource,
    '(?ms); Set collisionRadiusY,X\s*inc l\s*ld a,\$(?<radius>[0-9a-f]{2})\s*ldi \(hl\),a\s*ldi \(hl\),a')
$musicConstantSource = Get-Content -Raw (
    Join-Path $Disassembly 'constants\common\music.s')
$enterCaveSound = [regex]::Match(
    $musicConstantSource,
    '(?m)^\s*SND_ENTERCAVE\s+db\s*;\s*\$(?<sound>[0-9a-f]{2})')
if (-not $linkRadii.Success -or -not $enterCaveSound.Success) {
    throw 'Could not resolve Link collision radii or SND_ENTERCAVE.'
}

$clearPosition = [Convert]::ToInt32($towerDoorClear.Groups['position'].Value, 16)
$towerDoorWarpRows = @($towerDoorWarps | ForEach-Object {
    [pscustomobject]@{
        Group = [Convert]::ToInt32($_.Groups['group'].Value, 16)
        Room = [Convert]::ToInt32($_.Groups['room'].Value, 16)
        Transition = [Convert]::ToInt32($_.Groups['transition'].Value, 16)
        Position = [Convert]::ToInt32($_.Groups['position'].Value, 16)
        Transition2 = [Convert]::ToInt32($_.Groups['transition2'].Value, 16)
    }
})
if ($clearPosition -ne 0x44 -or
    [Convert]::ToInt32($towerDoorRadii.Groups['y'].Value, 16) -ne 0x04 -or
    [Convert]::ToInt32($towerDoorRadii.Groups['x'].Value, 16) -ne 0x10 -or
    [Convert]::ToInt32($towerDoorFlag.Groups['mask'].Value, 16) -ne 0x01 -or
    $towerDoorWarpRows[0].Transition -ne 0x93 -or
    $towerDoorWarpRows[0].Position -ne 0xff -or
    $towerDoorWarpRows[0].Transition2 -ne 0x01 -or
    $towerDoorWarpRows[1].Transition -ne 0x93 -or
    $towerDoorWarpRows[1].Position -ne 0xff -or
    $towerDoorWarpRows[1].Transition2 -ne 0x01) {
    throw 'Room 1:76 tower-door constants diverged from the supported handler.'
}

$towerDoorRows = @(
    "# group`troom`tid`tsubid`ty`tx`tclear-position-a`tclear-position-b`tobject-radius-y`tobject-radius-x`tlink-radius-y`tlink-radius-x`troom-flag-mask`tclear-dest-group`tclear-dest-room`tset-dest-group`tset-dest-room`twarp-transition`tdest-position`twarp-transition2`tsound`tsource",
    (@(
        $towerDoorPlacement.Groups['group'].Value,
        $towerDoorPlacement.Groups['room'].Value,
        $towerDoorPlacement.Groups['id'].Value,
        $towerDoorPlacement.Groups['subid'].Value,
        $towerDoorPlacement.Groups['y'].Value,
        $towerDoorPlacement.Groups['x'].Value,
        $clearPosition.ToString('x2'),
        ($clearPosition + 1).ToString('x2'),
        $towerDoorRadii.Groups['y'].Value,
        $towerDoorRadii.Groups['x'].Value,
        $linkRadii.Groups['radius'].Value,
        $linkRadii.Groups['radius'].Value,
        $towerDoorFlag.Groups['mask'].Value,
        $towerDoorWarpRows[0].Group.ToString('x1'),
        $towerDoorWarpRows[0].Room.ToString('x2'),
        $towerDoorWarpRows[1].Group.ToString('x1'),
        $towerDoorWarpRows[1].Room.ToString('x2'),
        $towerDoorWarpRows[0].Transition.ToString('x2'),
        $towerDoorWarpRows[0].Position.ToString('x2'),
        $towerDoorWarpRows[0].Transition2.ToString('x2'),
        $enterCaveSound.Groups['sound'].Value,
        'miscellaneous2.s:interactiondc_subid10'
    ) -join "`t")
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\black_tower_doorway_event.tsv'),
    $towerDoorRows,
    [Text.UTF8Encoding]::new($false))

# Room $1:$38 is the first Maku Sprout rescue. Its placed sprout creates a
# native controller, which in turn creates two scripted Moblin interactions;
# those actors replace themselves with ordinary masked-Moblin enemies. Keep
# all four source script lanes distinct so their original object update order
# and shared wTmpcfc0/wccd4 synchronization remain observable at runtime.
$makuObjectSource = Get-Content -Raw (
    Join-Path $Disassembly 'objects\ages\mainData.s')
$makuPlacement = [regex]::Match(
    $makuObjectSource,
    '(?ms)^group1Map38ObjectData:\s*obj_Interaction \$88 \$00 \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})\s*obj_Interaction \$6b \$15')
$makuObjectDataSource = Get-Content -Raw (
    Join-Path $Disassembly 'objects\ages\extraData3.s')
$makuMoblins = [regex]::Match(
    $makuObjectDataSource,
    '(?ms)^moblinsAttackingMakuSprout:\s*obj_Interaction \$96 \$00 \$(?<y>[0-9a-f]{2}) \$(?<leftx>[0-9a-f]{2})\s*obj_Interaction \$96 \$01 \$[0-9a-f]{2} \$(?<rightx>[0-9a-f]{2})')
if (-not $makuMoblins.Success) {
    # Some disassembly revisions keep dynamic lists in mainData.s.
    $makuMoblins = [regex]::Match(
        $makuObjectSource,
        '(?ms)^moblinsAttackingMakuSprout:\s*obj_Interaction \$96 \$00 \$(?<y>[0-9a-f]{2}) \$(?<leftx>[0-9a-f]{2})\s*obj_Interaction \$96 \$01 \$[0-9a-f]{2} \$(?<rightx>[0-9a-f]{2})')
}
if (-not $makuPlacement.Success -or -not $makuMoblins.Success -or
    $makuPlacement.Groups['y'].Value -ne '28' -or
    $makuPlacement.Groups['x'].Value -ne '50' -or
    $makuMoblins.Groups['y'].Value -ne '30' -or
    $makuMoblins.Groups['leftx'].Value -ne '68' -or
    $makuMoblins.Groups['rightx'].Value -ne '38') {
    throw 'Room 1:38 Maku Sprout/Moblin placements changed.'
}

$makuScriptsSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$makuHelperSource = Get-Content -Raw (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$makuInteractionSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\makuSprout.s')
$makuGateSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\interactions\makuGateOpening.s')
if ($makuScriptsSource -notmatch '(?ms)^makuSprout_subid01Script:.*?GLOBALFLAG_MAKU_TREE_SAVED.*?INTERAC_MISCELLANEOUS_1, \$04, \$40, \$50.*?TX_05d5' -or
    $makuScriptsSource -notmatch '(?ms)^moblin_subid00Script:.*?moblin_spawnEnemyHere.*?^moblin_subid01Script:' -or
    $makuHelperSource -notmatch '(?ms)^interaction6b_subid04Script:.*?wDisableScreenTransitions, \$01.*?INTERAC_MAKU_GATE_OPENING.*?GLOBALFLAG_MAKU_TREE_SAVED.*?wDisableScreenTransitions, \$00') {
    throw 'Room 1:38 rescue script ownership or completion predicates changed.'
}

$makuSproutGraphic = $interactionGraphics['136:0']
$makuMoblinGraphic = $interactionGraphics['150:0']
if ($null -eq $makuSproutGraphic -or $null -eq $makuMoblinGraphic -or
    $makuSproutGraphic.Gfx -ne 0x67 -or $makuMoblinGraphic.Gfx -ne 0x90) {
    throw 'Maku Sprout or scripted Moblin graphics changed.'
}
$makuSproutAnimations = @(0..2 | ForEach-Object {
    Resolve-NpcAnimation 0x88 $_
})
$makuMoblinAnimations = @(0..3 | ForEach-Object {
    Resolve-NpcAnimation 0x96 $_
})
if (-not $allTextPositions.ContainsKey(0x05d4) -or
    $allTextPositions[0x05d4] -ne 2) {
    throw 'TX_05d4 no longer explicitly selects textbox position 2.'
}

$makuActorRows = @(
    "# actor`tid`tsubid`ty`tx`tsprite`ttile-base`tpalette`tup-animation`tright-animation`tdown-animation`tleft-animation",
    (@('Sprout','88','00','28','50',$gfxNames[0x67],$makuSproutGraphic.TileBase,$makuSproutGraphic.Palette,
        $makuSproutAnimations[0],$makuSproutAnimations[0],$makuSproutAnimations[0],$makuSproutAnimations[0]) -join "`t"),
    (@('MoblinLeft','96','00','30','68',$gfxNames[0x90],$makuMoblinGraphic.TileBase,$makuMoblinGraphic.Palette,
        $makuMoblinAnimations[0],$makuMoblinAnimations[1],$makuMoblinAnimations[2],$makuMoblinAnimations[3]) -join "`t"),
    (@('MoblinRight','96','01','30','38',$gfxNames[0x90],$makuMoblinGraphic.TileBase,$makuMoblinGraphic.Palette,
        $makuMoblinAnimations[0],$makuMoblinAnimations[1],$makuMoblinAnimations[2],$makuMoblinAnimations[3]) -join "`t")
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\maku_sprout_rescue_actors.tsv'),
    $makuActorRows, [Text.UTF8Encoding]::new($false))

$makuEventRows = @(
    "# group`troom`tsprout-id`tsprout-subid`tcontroller-y`tcontroller-x`tmoblin-id`tmoblin-y`tleft-x`tright-x`tinitial-gate-position`tclear-tile`tgate-left`tgate-inner-left`tgate-inner-right`tgate-right`troom-flag`tadvice-flag`tsaved-flag`tstate-min`tstate-max`tmap-text-low`ttrigger-radius-y`ttrigger-radius-x`tjump-speed-z`tjump-gravity`tjump-sound`tgate-counter`tshake-counter`tfinal-text-position`tpost-text-id`tpost-text-base64",
    (@('1','38','88','00','40','50','96','30','68','38','52','f9','73','74','75','76','80','3f','12','01','02','d6','04','50','-512','30','53','30','06',
        $allTextPositions[0x05d4].ToString(),'05d5',
        (ConvertTo-CutsceneCommandPayload $allTexts[0x05d5])) -join "`t")
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'cutscenes\maku_sprout_rescue_event.tsv'),
    $makuEventRows, [Text.UTF8Encoding]::new($false))

function Write-MakuRescueCommands {
    param(
        [string]$file,
        [string]$script,
        [string]$label,
        [string]$sourceText,
        [object[]]$specs)
    $line = Get-AssemblySourceLine $sourceText "(?m)^$([regex]::Escape($label))\s*:" $label
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
    for ($index = 0; $index -lt $specs.Count; $index++) {
        $spec = $specs[$index]
        $rows.Add((New-CutsceneCommandRow $script $index $label $line `
            $spec[0] $spec[1] $spec[2] $spec[3] $spec[4]))
    }
    [IO.File]::WriteAllLines(
        (Join-Path $destination "cutscenes\$file"),
        $rows, [Text.UTF8Encoding]::new($false))
}
function Maku-Text([int]$id) { return $allTexts[$id] }

$sproutSpecs = @(
    @('nativeyield','','','','SpawnController'),
    @('setanimation','Sprout','02','',$makuSproutAnimations[2]),
    @('setcollisionradii','Sprout','08','08',''),
    @('checkmemoryeq','','09','','CutsceneState'),
    @('wait','','2','',''),
    @('nativeblock','','1','','WaitForAtMostOneEnemy'),
    @('jumpifmemoryeq','','00','12','RoomEnemyCount'),
    @('setanimation','Sprout','01','',$makuSproutAnimations[1]),
    @('wait','','90','',''),
    @('setanimation','Sprout','00','',$makuSproutAnimations[0]),
    @('wait','','60','',''),
    @('checkmemoryeq','','00','','RoomEnemyCount'),
    @('setanimation','Sprout','01','',$makuSproutAnimations[1]),
    @('wait','','90','',''),
    @('setanimation','Sprout','00','',$makuSproutAnimations[0]),
    @('setcollisionradii','Sprout','08','08',''),
    @('makeabuttonsensitive','Sprout','','',''),
    @('native','','','','EnterNpcLoop'),
    @('scriptend','','','','')
)
Write-MakuRescueCommands 'maku_sprout_rescue_sprout.tsv' `
    'makuSprout_subid01Script' 'makuSprout_subid01Script' `
    $makuScriptsSource $sproutSpecs

$controllerSpecs = @(
    @('disableinput','','','',''), @('native','','','','RestartSound'),
    @('native','','','','DisableScreenTransitions'), @('native','','','','LoadMoblins'),
    @('wait','','60','',''), @('nativeyield','','','','SpawnInitialPuff'),
    @('wait','','4','',''), @('native','','','','SetInitialGateTile'),
    @('writememory','','01','','CutsceneState'), @('checkmemoryeq','','02','','CutsceneState'),
    @('wait','','30','',''), @('showtext','','1202','',(Maku-Text 0x1202)),
    @('wait','','30','',''), @('writememory','','03','','CutsceneState'),
    @('checkmemoryeq','','04','','CutsceneState'), @('wait','','30','',''),
    @('showtext','','05d0','',(Maku-Text 0x05d0)), @('wait','','30','',''),
    @('nativeyield','','','','PlayDisasterMusic'), @('writememory','','05','','CutsceneState'),
    @('enableinput','','','',''), @('nativeblock','','1','','WaitForLinkCollision'),
    @('disableinput','','','',''), @('native','','','','SetLinkUp'),
    @('writememory','','06','','CutsceneState'), @('checkmemoryeq','','08','','CutsceneState'),
    @('wait','','30','',''), @('showtext','','1203','',(Maku-Text 0x1203)),
    @('playsound','','c8','',''), @('wait','','40','',''),
    @('writememory','','09','','CutsceneState'), @('wait','','2','',''),
    @('enableinput','','','',''), @('nativeblock','','1','','WaitForAtMostOneEnemy'),
    @('jumpifmemoryeq','','00','39','RoomEnemyCount'), @('wait','','20','',''),
    @('showtext','','05d1','',(Maku-Text 0x05d1)), @('checkmemoryeq','','00','','RoomEnemyCount'),
    @('wait','','20','',''), @('showtext','','05d2','',(Maku-Text 0x05d2)),
    @('wait','','30','',''), @('disableinput','','','',''),
    @('native','','','','RestartSound'), @('wait','','20','',''),
    @('playsound','','c8','',''), @('wait','','20','',''),
    @('playsound','','c8','',''), @('wait','','20','',''),
    @('playsound','','c8','',''), @('wait','','30','',''),
    @('nativeblock','','1','','MoveLinkToPosition'), @('wait','','1','',''),
    @('checkmemoryeq','','01','','PlayerMoveComplete'), @('wait','','30','',''),
    @('showtext','','05d3','',(Maku-Text 0x05d3)), @('wait','','30','',''),
    @('nativeyield','','','','SpawnGateOpening'), @('checkmemoryeq','','01','','RoomGateOpen'),
    @('wait','','40','',''), @('setglobalflag','','3f','',''),
    @('showtext','','05d6','',(Maku-Text 0x05d6)), @('native','','','','WriteMakuMapText'),
    @('setglobalflag','','12','',''), @('native','','','','IncMakuState'),
    @('native','','','','LayoutSwap'), @('native','','','','ResetMusic'),
    @('enableinput','','','',''), @('nativeblock','','1','','WaitForScreenEdge'),
    @('showtext','','05d4','',(Maku-Text 0x05d4)),
    @('native','','','','EnableScreenTransitions'), @('scriptend','','','','')
)
Write-MakuRescueCommands 'maku_sprout_rescue_controller.tsv' `
    'interaction6b_subid04Script' 'interaction6b_subid04Script' `
    $makuHelperSource $controllerSpecs

$leftMoblinSpecs = @(
    @('setanimation','MoblinLeft','03','',$makuMoblinAnimations[3]),
    @('checkmemoryeq','','01','','CutsceneState'),
    @('writeobjectbyte','MoblinLeft','3f','01',''),
    @('jump','MoblinLeft','-512','30','53'),
    @('writeobjectbyte','MoblinLeft','3f','00',''),
    @('writememory','','02','','CutsceneState'),
    @('checkmemoryeq','','05','','CutsceneState'),
    @('writeobjectbyte','MoblinLeft','3f','01',''),
    @('jump','MoblinLeft','-512','30','53'),
    @('writeobjectbyte','MoblinLeft','3f','00',''),
    @('jumpifmemoryeq','','06','13','CutsceneState'),
    @('wait','','30','',''), @('scriptjump','','7','',''),
    @('native','','','','FaceMoblinLeft'), @('native','','','','AddMoblinSync'),
    @('checkmemoryeq','','02','','MoblinSync'), @('native','','','','IncrementCutsceneState'),
    @('checkmemoryeq','','09','','CutsceneState'), @('native','','','','SpawnMaskedMoblinLeft'),
    @('wait','','1','',''), @('scriptend','','','','')
)
Write-MakuRescueCommands 'maku_sprout_rescue_moblin_left.tsv' `
    'moblin_subid00Script' 'moblin_subid00Script' `
    $makuScriptsSource $leftMoblinSpecs

$rightMoblinSpecs = @(
    @('setanimation','MoblinRight','01','',$makuMoblinAnimations[1]),
    @('checkmemoryeq','','03','','CutsceneState'),
    @('writeobjectbyte','MoblinRight','3f','01',''),
    @('jump','MoblinRight','-512','30','53'),
    @('writeobjectbyte','MoblinRight','3f','00',''),
    @('writememory','','04','','CutsceneState'),
    @('checkmemoryeq','','05','','CutsceneState'), @('wait','','30','',''),
    @('writeobjectbyte','MoblinRight','3f','01',''),
    @('jump','MoblinRight','-512','30','53'),
    @('writeobjectbyte','MoblinRight','3f','00',''),
    @('jumpifmemoryeq','','06','14','CutsceneState'),
    @('wait','','30','',''), @('scriptjump','','8','',''),
    @('native','','','','FaceMoblinRight'), @('native','','','','AddMoblinSync'),
    @('checkmemoryeq','','02','','MoblinSync'), @('native','','','','IncrementCutsceneState'),
    @('checkmemoryeq','','09','','CutsceneState'), @('native','','','','SpawnMaskedMoblinRight'),
    @('wait','','1','',''), @('scriptend','','','','')
)
Write-MakuRescueCommands 'maku_sprout_rescue_moblin_right.tsv' `
    'moblin_subid01Script' 'moblin_subid01Script' `
    $makuScriptsSource $rightMoblinSpecs
