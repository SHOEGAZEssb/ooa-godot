# Resolve the shared boss and dungeon-interaction graphics closure before the
# Spirit's Grave placement/constants section below. The generated assets use
# global enemy/interaction ownership even though this stage also imports D1's
# first supported placements; runtime code never reads assembly source.

$enemyObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\enemyData.s')
$pumpkinHeadSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\enemies\pumpkinHead.s')

$dungeonBossSpriteSequences = @{
    0x3f = @($gfxNames[0xad], $gfxNames[0xae])
    # Shadow Hag's uncounted $42 bugs use the second boss sheet. The boss
    # itself loads both consecutive $c2/$c3 headers.
    0x42 = @($gfxNames[0xc3])
    0x70 = @($gfxNames[0xad], $gfxNames[0xae])
    # Swoop's $af header is followed by the chained $b0 header. Grounded OAM
    # frames use tiles $20-$2c from spr_pound for the expanding impact arcs.
    0x71 = @($gfxNames[0xaf], $gfxNames[0xb0])
    # Subterror loads the consecutive $b1-$b3 headers. PART_SUBTERROR_DIRT
    # shares the final header while the enemy animations address the complete
    # live extra-GFX closure.
    0x72 = @($gfxNames[0xb1], $gfxNames[0xb2], $gfxNames[0xb3])
    0x78 = @($gfxNames[0xbc], $gfxNames[0xbd], $gfxNames[0xbe])
    0x79 = @($gfxNames[0xbf], $gfxNames[0xc0], $gfxNames[0xc1])
    0x7a = @($gfxNames[0xc2], $gfxNames[0xc3])
}
$dungeonBossSourceGrayscaleInverted = @{
    # Giant Ghini's two source sheets use white as color 0, unlike the
    # ordinary black-background enemy sheets.
    0x3f = $false
    0x42 = $true
    0x70 = $false
    0x71 = $true
    0x72 = $true
    0x78 = $true
    0x79 = $true
    0x7a = $true
}
$dungeonBossRows = [Collections.Generic.List[string]]::new()
$dungeonBossRows.Add('# id`tsubid`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tanimations-base64'.Replace('`t', "`t"))
foreach ($spec in @(
    @(0x3f, 0), @(0x42, 0), @(0x70, 0), @(0x71, 0), @(0x72, 0), @(0x78, 0),
    @(0x79, 0), @(0x7a, 0)
)) {
    $id = [int]$spec[0]
    $subid = [int]$spec[1]
    $definition = Get-EnemyDefinition $id $subid
    $sprites = $dungeonBossSpriteSequences[$id]
    foreach ($sprite in $sprites) { Copy-EnemySprite $sprite }
    $animations = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($definition.Animations -join "`n"))
    $sourceGrayscaleInverted = if ($dungeonBossSourceGrayscaleInverted[$id]) { 1 } else { 0 }
    $dungeonBossRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($sprites -join ',')`t$($definition.TileBase)`t$($definition.Palette)`t$sourceGrayscaleInverted`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.Damage)`t$($definition.Health)`t$animations")
}
if ($dungeonBossRows.Count -ne 9 -or
    -not ($dungeonBossRows | Where-Object { $_ -match '^3f\t00\tspr_giantghini_1,spr_giantghini_2\t0\t5\t0\t2\t2\t128\t2\t' }) -or
    -not ($dungeonBossRows | Where-Object { $_ -match '^42\t00\tspr_shadowhag_2\t20\t2\t1\t6\t6\t1\t2\t' }) -or
    -not ($dungeonBossRows | Where-Object { $_ -match '^70\t00\tspr_giantghini_1,spr_giantghini_2\t0\t5\t0\t10\t10\t1\t12\t' }) -or
    -not ($dungeonBossRows | Where-Object { $_ -match '^71\t00\tspr_swoop,spr_pound\t0\t2\t1\t10\t10\t2\t20\t' }) -or
    -not ($dungeonBossRows | Where-Object { $_ -match '^72\t00\tspr_subterror_1,spr_subterror_2,spr_subterror_3\t0\t1\t1\t6\t6\t2\t20\t' }) -or
    -not ($dungeonBossRows | Where-Object { $_ -match '^78\t00\tspr_pumpkinhead_1,spr_pumpkinhead_2,spr_pumpkinhead_3\t0\t3\t1\t6\t12\t2\t8\t' }) -or
    -not ($dungeonBossRows | Where-Object { $_ -match '^79\t00\tspr_headthwomp_1,spr_headthwomp_2,spr_headthwomp_3\t0\t0\t1\t18\t15\t2\t4\t' }) -or
    -not ($dungeonBossRows | Where-Object { $_ -match '^7a\t00\tspr_shadowhag_1,spr_shadowhag_2\t0\t3\t1\t9\t9\t3\t12\t' })) {
    throw "Shared dungeon boss definitions no longer match the traced records:`n$($dungeonBossRows -join "`n")"
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\dungeon_bosses.tsv'),
    $dungeonBossRows)

# ENEMY_SUBTERROR initializes PALH_be. The header replaces OBJ palette 6 for
# PART_SUBTERROR_DIRT; using the ordinary slot-6 palette makes the dirt blue.
$subterrorSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\enemies\subterror.s')
$subterrorPaletteHeader = [regex]::Match(
    $paletteHeaderSource,
    '(?ms)^m_PaletteHeaderStart\s+\$be,\s*PALH_be(?<body>.*?)(?=^m_PaletteHeaderStart|\z)')
if ($subterrorSource -notmatch
        '(?ms)^subterror_state_uninitialized:.*?' +
        'ld a,ENEMY_SUBTERROR.*?ld b,PALH_be.*?' +
        'call enemyBoss_initializeRoom' -or
    -not $subterrorPaletteHeader.Success -or
    $subterrorPaletteHeader.Groups['body'].Value -notmatch
        'm_PaletteHeaderSpr\s+6,\s*1,\s*paletteData4950') {
    throw 'ENEMY_SUBTERROR no longer loads PALH_be/paletteData4950 into OBJ palette 6.'
}
Write-GeneratedBytes(
    (Join-Path $destination 'objects\dungeon_subterror_palette.bin'),
    (Read-PaletteBytes 'paletteData4950' 4))

# ENEMY_HEAD_THWOMP initializes PALH_81 before becoming visible. The header
# replaces OBJ palette 6 with paletteData4958; the boss's purple-face OAM
# selects that slot while its other cells retain the standard OBJ palettes.
$headThwompSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\enemies\headThwomp.s')
$headThwompPaletteHeader = [regex]::Match(
    $paletteHeaderSource,
    '(?ms)^m_PaletteHeaderStart\s+\$81,\s*PALH_81(?<body>.*?)(?=^m_PaletteHeaderStart|\z)')
if ($headThwompSource -notmatch
        '(?ms)^headThwomp_state_uninitialized:.*?' +
        'ld a,ENEMY_HEAD_THWOMP.*?ld b,PALH_81.*?' +
        'call enemyBoss_initializeRoom' -or
    -not $headThwompPaletteHeader.Success -or
    $headThwompPaletteHeader.Groups['body'].Value -notmatch
        'm_PaletteHeaderSpr\s+6,\s*1,\s*paletteData4958') {
    throw 'ENEMY_HEAD_THWOMP no longer loads PALH_81/paletteData4958 into OBJ palette 6.'
}
Write-GeneratedBytes(
    (Join-Path $destination 'objects\dungeon_head_thwomp_palette.bin'),
    (Read-PaletteBytes 'paletteData4958' 4))

# Resolve native interaction graphics used by the moving platforms, rotating
# cube/flames, and the first essence. The cube state machine selects all 30
# animations in its source table, so retain the complete sequence.
$dungeonVisualRows = [Collections.Generic.List[string]]::new()
$dungeonVisualRows.Add(
    '# key`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tanimations-base64'.Replace(
        '`t', "`t"))

# The broad NPC importer accepts several deliberately shared animation tails.
# These native dungeon objects instead use self-contained records whose final
# parameter byte has bit 7 set (or an explicit m_AnimationLoop). Read exactly
# one such record so a cube animation can never absorb the following labels.
function Resolve-DungeonInteractionAnimation(
    [int]$interactionId,
    [int]$animationIndex) {
    $hex = $interactionId.ToString('x2')
    $animationKey = "interaction${hex}Animations"
    $pointerKey = "interaction${hex}OamDataPointers"
    if (-not $npcAnimationTables.ContainsKey($animationKey) -or
        -not $npcOamPointerTables.ContainsKey($pointerKey)) {
        throw "Interaction `$$hex has no animation/OAM tables."
    }
    $animations = $npcAnimationTables[$animationKey]
    if ($animationIndex -lt 0 -or $animationIndex -ge $animations.Count) {
        throw "Interaction `$$hex animation index $animationIndex is out of range."
    }
    $label = $animations[$animationIndex]
    $bodyMatch = [regex]::Match(
        $interactionAnimationSource,
        "(?ms)^$([regex]::Escape($label)):\s*(?<body>.*?)(?=^interactionAnimation[0-9a-f]+(?:Loop)?:|\z)")
    if (-not $bodyMatch.Success) {
        throw "Interaction `$$hex animation body is missing: $label"
    }
    $pointers = $npcOamPointerTables[$pointerKey]
    $frames = [Collections.Generic.List[string]]::new()
    $terminal = $false
    foreach ($line in ($bodyMatch.Groups['body'].Value -split '\r?\n')) {
        $frame = [regex]::Match(
            $line,
            '^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<offset>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')
        if ($frame.Success) {
            $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
            $offset = [Convert]::ToInt32($frame.Groups['offset'].Value, 16)
            $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
            $pointerIndex = [int]($offset / 2)
            if ($pointerIndex -lt 0 -or $pointerIndex -ge $pointers.Count) {
                throw "Interaction `$$hex animation $animationIndex OAM offset `$$($offset.ToString('x2')) is out of range."
            }
            $oamLabel = $pointers[$pointerIndex]
            if (-not $npcOamBlocks.ContainsKey($oamLabel)) {
                throw "Interaction `$$hex animation $animationIndex OAM body is missing: $oamLabel"
            }
            $metadata = if ($parameter -eq 0) { "$duration" } else { "$duration,$parameter" }
            $frames.Add("$metadata@$($npcOamBlocks[$oamLabel])")
            if (($parameter -band 0x80) -ne 0) {
                $terminal = $true
                break
            }
            continue
        }
        if ($line -match '^\s*m_AnimationLoop\s+') {
            $terminal = $true
            break
        }
    }
    if (-not $terminal -and $frames.Count -eq 1 -and
        $bodyMatch.Groups['body'].Value -match
            '(?m)^\s*\.db\s+\$7f\s+\$[0-9a-f]{2}\s+\$00\s*$') {
        # Static side-platform animations end after one $7f-duration frame.
        $terminal = $true
    }
    if ($frames.Count -eq 0 -or -not $terminal) {
        throw "Interaction `$$hex animation $animationIndex is incomplete: $label"
    }
    return $frames -join '|'
}

function Resolve-MinecartSpecialObjectAnimations {
    $animationSource = Read-ImportText (
        Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')
    $oamSource = Read-ImportText (
        Join-Path $Disassembly 'data\ages\specialObjectOamData.s')

    $gfxBlock = [regex]::Match(
        $animationSource,
        '(?ms)^specialObject0aGfxPointers:(?<body>.*?)(?=^specialObject0aAnimationDataPointers:)')
    $gfxEntries = if ($gfxBlock.Success) {
        @([regex]::Matches(
            $gfxBlock.Groups['body'].Value,
            'm_SpecialObjectGfxPointer \$(?<oam>[0-9a-f]{2}) spr_dungeon_sprites \$(?<offset>[0-9a-f]{4}) \$04'))
    } else { @() }
    if ($gfxEntries.Count -ne 4) {
        throw "SPECIALOBJECT_MINECART must retain four GFX rows; found $($gfxEntries.Count)."
    }

    $animationPointerBlock = [regex]::Match(
        $animationSource,
        '(?ms)^specialObject0aAnimationDataPointers:(?<body>.*?)(?=^animationData1a1c5:)')
    $animationLabels = if ($animationPointerBlock.Success) {
        @([regex]::Matches(
            $animationPointerBlock.Groups['body'].Value,
            '(?m)^\s*\.dw\s+(?<label>animationData[0-9a-f]+)') |
            ForEach-Object { $_.Groups['label'].Value })
    } else { @() }
    if ($animationLabels.Count -ne 4 -or
        $animationLabels[0] -ne $animationLabels[2] -or
        $animationLabels[1] -ne $animationLabels[3]) {
        throw 'SPECIALOBJECT_MINECART must retain its vertical/horizontal animation aliases.'
    }

    $oamPointerBlock = [regex]::Match(
        $animationSource,
        '(?ms)^specialObject0aOamDataPointers:(?<body>.*?)(?=^specialObject13GfxPointers:)')
    $oamLabels = if ($oamPointerBlock.Success) {
        @([regex]::Matches(
            $oamPointerBlock.Groups['body'].Value,
            '(?m)^\s*\.dw\s+(?<label>oamData4c[0-9a-f]+)') |
            ForEach-Object { $_.Groups['label'].Value })
    } else { @() }
    if ($oamLabels.Count -ne 3) {
        throw "SPECIALOBJECT_MINECART must retain three OAM rows; found $($oamLabels.Count)."
    }

    function Resolve-MinecartSpecialObjectOam(
        [int]$gfxIndex) {
        if ($gfxIndex -lt 0 -or $gfxIndex -ge $gfxEntries.Count) {
            throw "SPECIALOBJECT_MINECART GFX index $gfxIndex is out of range."
        }
        $gfx = $gfxEntries[$gfxIndex]
        $oamIndex = [Convert]::ToInt32($gfx.Groups['oam'].Value, 16)
        $tileOffset =
            [Convert]::ToInt32($gfx.Groups['offset'].Value, 16) / 16
        if ($oamIndex -lt 0 -or $oamIndex -ge $oamLabels.Count) {
            throw "SPECIALOBJECT_MINECART OAM index $oamIndex is out of range."
        }
        $label = $oamLabels[$oamIndex]
        $block = [regex]::Match(
            $oamSource,
            ('(?ms)^{0}:\s*\.db \$(?<count>[0-9a-f]{{2}})(?<body>.*?)(?=^oamData4c[0-9a-f]+:|\z)' -f
                [regex]::Escape($label)))
        if (-not $block.Success) {
            throw "SPECIALOBJECT_MINECART OAM body is missing: $label"
        }
        $cells = @([regex]::Matches(
            $block.Groups['body'].Value,
            '(?m)^\s*\.db\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s+\$(?<flags>[0-9a-f]{2})'))
        $count = [Convert]::ToInt32($block.Groups['count'].Value, 16)
        if ($cells.Count -ne $count) {
            throw "SPECIALOBJECT_MINECART OAM $label declares $count cells but contains $($cells.Count)."
        }
        return @($cells | ForEach-Object {
            $y = [Convert]::ToInt32($_.Groups['y'].Value, 16)
            $x = [Convert]::ToInt32($_.Groups['x'].Value, 16)
            $tile =
                [Convert]::ToInt32($_.Groups['tile'].Value, 16) +
                $tileOffset
            $flags = [Convert]::ToInt32($_.Groups['flags'].Value, 16)
            "$y,$x,$tile,$flags"
        }) -join ';'
    }

    $resolved = [Collections.Generic.List[string]]::new()
    foreach ($label in $animationLabels[0..1]) {
        $body = [regex]::Match(
            $animationSource,
            "(?ms)^$([regex]::Escape($label)):(?<body>.*?)(?=^animationData[0-9a-f]+:|^specialObject0aOamDataPointers:)")
        if (-not $body.Success -or
            $body.Groups['body'].Value -notmatch
                "(?m)^\s*m_AnimationLoop\s+$([regex]::Escape($label))\s*$") {
            throw "SPECIALOBJECT_MINECART animation body is incomplete: $label"
        }
        $frames = [Collections.Generic.List[string]]::new()
        foreach ($frame in [regex]::Matches(
            $body.Groups['body'].Value,
            '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<gfx>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')) {
            $duration =
                [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
            $gfxIndex =
                [Convert]::ToInt32($frame.Groups['gfx'].Value, 16)
            $parameter =
                [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
            $metadata = if ($parameter -eq 0) {
                "$duration"
            } else {
                "$duration,$parameter"
            }
            $frames.Add(
                "$metadata@$(Resolve-MinecartSpecialObjectOam $gfxIndex)")
        }
        if ($frames.Count -ne 2) {
            throw "SPECIALOBJECT_MINECART animation $label must contain two frames."
        }
        $resolved.Add($frames -join '|')
    }
    return $resolved.ToArray()
}

function Add-DungeonInteractionVisual(
    [string]$key,
    [int]$id,
    [int]$subid,
    [int[]]$animations,
    [int]$tileBaseOverride = -1,
    [int]$paletteOverride = -1,
    [bool]$sourceGrayscaleInverted = $true,
    [string[]]$additionalAnimations = @()) {
    $graphic = $interactionGraphics["$id`:$subid"]
    if ($null -eq $graphic) { $graphic = $interactionGraphics["$id`:0"] }
    if ($null -eq $graphic -or -not $gfxNames.ContainsKey($graphic.Gfx)) {
        throw "Dungeon interaction visual $key (`$$($id.ToString('x2')):`$$($subid.ToString('x2'))) is missing."
    }
    $sprite = $gfxNames[$graphic.Gfx]
    Copy-EnemySprite $sprite
    $resolved = @(
        @($animations | ForEach-Object {
            Resolve-DungeonInteractionAnimation $id $_
        }) +
        @($additionalAnimations))
    if ($resolved.Count -eq 0 -or ($resolved | Where-Object { -not $_ }).Count -gt 0) {
        throw "Dungeon interaction visual $key has unresolved animations."
    }
    $animationData = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($resolved -join "`n"))
    $tileBase = if ($tileBaseOverride -ge 0) { $tileBaseOverride } else { $graphic.TileBase }
    $palette = if ($paletteOverride -ge 0) { $paletteOverride } else { $graphic.Palette }
    $inverted = if ($sourceGrayscaleInverted) { 1 } else { 0 }
    $dungeonVisualRows.Add("$key`t$sprite`t$tileBase`t$palette`t$inverted`t$animationData")
}
Add-DungeonInteractionVisual 'platform-05' 0x79 5 @(5)
Add-DungeonInteractionVisual 'platform-09' 0x79 1 @(1)
# Unlike the ordinary black-background spr_* sheets, spr_colored_cube is
# authored black-on-white. Retain that source interpretation so color zero,
# not the cube drawing, becomes transparent during OAM composition.
Add-DungeonInteractionVisual 'colored-cube' 0x19 5 (0..29) -sourceGrayscaleInverted $false
Add-DungeonInteractionVisual 'cube-flame' 0x1a 0 @(0)
Add-DungeonInteractionVisual 'moving-side-platform' 0xa1 0 (0..4)
Add-DungeonInteractionVisual 'circular-side-platform' 0xa4 0 @(0)
Add-DungeonInteractionVisual 'minecart' 0x16 0 @(0, 1) `
    -additionalAnimations (Resolve-MinecartSpecialObjectAnimations)
# interactionCode1b rewrites subid to direction $00/$02 before graphics
# initialization, then selects direction|open for the four source animations.
Add-DungeonInteractionVisual 'minecart-gate' 0x1b 0 @(0, 1, 2, 3)
# INTERAC_SPINNER selects animation 0/1 from its angle while the related arrow
# uses animation 2/3. The parent toggles only its oamFlags palette bit, which
# the runtime applies to both objects from the same imported graphics closure.
Add-DungeonInteractionVisual 'spinner' 0x7d 0 @(0, 1, 2, 3)

# interactionCode19 loads PALH_89, which replaces OBJ palettes 6 and 7 with
# the two color-pair palettes used by the rotating cube. Its OAM records mix
# these with ordinary palette 5, so retain both indexed overrides rather than
# flattening the cube to one approximate palette.
$cubePaletteHeader = [regex]::Match(
    $paletteHeaderSource,
    '(?ms)^m_PaletteHeaderStart\s+\$89,\s*PALH_89(?<body>.*?)(?=^m_PaletteHeaderStart|\z)')
if (-not $cubePaletteHeader.Success -or
    $cubePaletteHeader.Groups['body'].Value -notmatch
        'm_PaletteHeaderSpr\s+6,\s*1,\s*paletteData5908' -or
    $cubePaletteHeader.Groups['body'].Value -notmatch
        'm_PaletteHeaderSpr\s+7,\s*1,\s*paletteData5910') {
    throw 'PALH_89 no longer maps cube OBJ palettes 6/7 to paletteData5908/paletteData5910.'
}
$cubePalette6 = Read-PaletteBytes 'paletteData5908' 4
$cubePalette7 = Read-PaletteBytes 'paletteData5910' 4
$cubePaletteBytes = [byte[]]::new(24)
[Array]::Copy($cubePalette6, 0, $cubePaletteBytes, 0, 12)
[Array]::Copy($cubePalette7, 0, $cubePaletteBytes, 12, 12)
Write-GeneratedBytes(
    (Join-Path $destination 'objects\colored_cube_palettes.bin'),
    $cubePaletteBytes)

# D1's @essenceOamData row adds tile 0, palette 1, and chooses layout/animation 1.
# The separately created pedestal and glow retain their subid-data defaults:
# $76/$00/$40 selects animation 0, while $76/$06/$43 selects the four-frame
# animation 3 glow. Using animation 0 for both draws the pedestal OAM twice.
$essenceSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\essence.s')
$essencePedestalGraphic = $interactionGraphics['127:1']
$essenceGlowGraphic = $interactionGraphics['127:2']
if ($essenceSource -notmatch
        '(?ms)^@essenceOamData:.*?\.db \$00 \$01 \$01\s+' +
        '\.db \$04 \$00 \$02\s+\.db \$06 \$03 \$02' -or
    $null -eq $essencePedestalGraphic -or
    $essencePedestalGraphic.Gfx -ne 0x76 -or
    $essencePedestalGraphic.TileBase -ne 0 -or
    $essencePedestalGraphic.Palette -ne 4 -or
    $essencePedestalGraphic.DefaultAnimation -ne 0 -or
    $null -eq $essenceGlowGraphic -or
    $essenceGlowGraphic.Gfx -ne 0x76 -or
    $essenceGlowGraphic.TileBase -ne 6 -or
    $essenceGlowGraphic.Palette -ne 4 -or
    $essenceGlowGraphic.DefaultAnimation -ne 3) {
    throw 'INTERAC_ESSENCE D1/pedestal/glow graphics initialization changed.'
}
if ($essenceSource -notmatch
        '(?ms)^@state0:.*?ld a,\(wDungeonIndex\)\s+dec a.*?' +
        'ld l,Interaction\.var03\s+ld \(hl\),a' -or
    $essenceSource -notmatch
        '(?ms)^@getEssenceTextTable:\s+\.db <TX_000e\s+' +
        '\.db <TX_000f\s+\.db <TX_0010' -or
    $essenceSource -notmatch
        '(?ms)^@essenceWarps:.*?' +
        '\.db \$80, \$8d, \$26, TRANSITION_DEST_SET_RESPAWN\s+' +
        '\.db \$81, \$83, \$25, TRANSITION_DEST_SET_RESPAWN\s+' +
        '\.db \$80, \$ba, \$55, TRANSITION_DEST_SET_RESPAWN') {
    throw 'INTERAC_ESSENCE D1-D3 index, text, or exit-warp mapping changed.'
}
Add-DungeonInteractionVisual 'eternal-spirit' 0x7f 0 @(1) 0 1
# D2's second @essenceOamData row selects the four-tile Ancient Wood layout
# with tile base $04 and OBJ palette 0.
Add-DungeonInteractionVisual 'ancient-wood' 0x7f 0 @(2) 4 0
# D3's third row keeps the four-tile layout for Echoing Howl while selecting
# tile base $06 and OBJ palette 3.
Add-DungeonInteractionVisual 'echoing-howl' 0x7f 0 @(2) 6 3
Add-DungeonInteractionVisual 'essence-pedestal' 0x7f 1 @(0)
Add-DungeonInteractionVisual 'essence-glow' 0x7f 2 @(3)

# PART_BLUE_ENERGY_BEAD $53 supplies the eight inward-swirl variants used by
# the common essence script. Part data $53 selects gfx $87, tile base 0,
# palette 4; retain its source animation and OAM order.
$energyAnimationStart = $partAnimationSource.IndexOf(
    'part53Animations:', [StringComparison]::Ordinal)
$energyAnimationEnd = $partAnimationSource.IndexOf(
    'part54Animations:', [StringComparison]::Ordinal)
$energyAnimationLabels = @([regex]::Matches(
    $partAnimationSource.Substring(
        $energyAnimationStart, $energyAnimationEnd - $energyAnimationStart),
    '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
$energyOamStart = $partAnimationSource.IndexOf(
    'part53OamDataPointers:', [StringComparison]::Ordinal)
$energyOamEnd = $partAnimationSource.IndexOf(
    'part55OamDataPointers:', [StringComparison]::Ordinal)
$energyOamLabels = @([regex]::Matches(
    $partAnimationSource.Substring(
        $energyOamStart, $energyOamEnd - $energyOamStart),
    '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if ($energyAnimationLabels.Count -ne 16 -or $energyOamLabels.Count -ne 12) {
    throw 'PART_BLUE_ENERGY_BEAD animation/OAM tables changed.'
}
function Resolve-DungeonEnergyAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:partAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<offset>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $offset = [Convert]::ToInt32($frame.Groups['offset'].Value, 16)
        $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
        $pointerIndex = [int]($offset / 2)
        if ($pointerIndex -ge $script:energyOamLabels.Count) {
            throw "$label references missing energy-bead OAM pointer $pointerIndex."
        }
        $frames.Add("$duration,$parameter@$(Resolve-Oam $script:partOamSource $script:energyOamLabels[$pointerIndex])")
        if ($parameter -band 0x80) {
            break
        }
    }
    if ($frames.Count -eq 0) { throw "$label has no energy-bead frames." }
    return $frames -join '|'
}
$energyAnimations = @($energyAnimationLabels[0..7] | ForEach-Object {
    Resolve-DungeonEnergyAnimation $_
})
$energySprite = $gfxNames[0x87]
$energySpritePropertiesPath = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object {
        Get-ChildItem $_.FullName -Recurse -File -Filter "$energySprite.properties"
    } |
    Select-Object -First 1
if ($energySprite -ne 'spr_circlebeads' -or
    $null -eq $energySpritePropertiesPath -or
    (Read-ImportText $energySpritePropertiesPath.FullName) -notmatch
        '(?m)^\s*invert:\s*false\s*$') {
    throw 'PART_BLUE_ENERGY_BEAD source graphics polarity changed.'
}
Copy-EnemySprite $energySprite
$energyAnimationData = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($energyAnimations -join "`n"))
$dungeonVisualRows.Add("energy-bead`t$energySprite`t0`t4`t0`t$energyAnimationData")

# PART_PUMPKIN_HEAD_PROJECTILE $42 uses gfx $a6, tile base $1e, palette 2.
$pumpkinProjectileAnimationLabels = @([regex]::Matches(
    (Get-AssemblyLabelBody $partAnimationSource 'part42Animations'),
    '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
$pumpkinProjectileOamLabels = @([regex]::Matches(
    (Get-AssemblyLabelBody $partAnimationSource 'part42OamDataPointers'),
    '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if ($pumpkinProjectileAnimationLabels.Count -ne 1 -or
    $pumpkinProjectileOamLabels.Count -ne 3) {
    throw 'PART_PUMPKIN_HEAD_PROJECTILE animation/OAM tables changed.'
}
$pumpkinProjectileFrames = [Collections.Generic.List[string]]::new()
foreach ($frame in [regex]::Matches(
    (Get-AssemblyLabelBody $partAnimationSource $pumpkinProjectileAnimationLabels[0]),
    '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<offset>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')) {
    $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
    $pointerIndex = [int]([Convert]::ToInt32(
        $frame.Groups['offset'].Value, 16) / 2)
    if ($pointerIndex -ge $pumpkinProjectileOamLabels.Count) {
        throw "Pumpkin Head projectile OAM pointer $pointerIndex is out of range."
    }
    $pumpkinProjectileFrames.Add(
        "$duration@$(Resolve-Oam $partOamSource $pumpkinProjectileOamLabels[$pointerIndex])")
}
if ($pumpkinProjectileFrames.Count -ne 3) {
    throw 'PART_PUMPKIN_HEAD_PROJECTILE must retain three animation frames.'
}
$pumpkinProjectileSprite = $gfxNames[0xa6]
Copy-EnemySprite $pumpkinProjectileSprite
$pumpkinProjectileAnimationData = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($pumpkinProjectileFrames -join '|'))
$dungeonVisualRows.Add(
    "pumpkin-projectile`t$pumpkinProjectileSprite`t30`t2`t1`t$pumpkinProjectileAnimationData")

# Head Thwomp's two native projectiles use the Ages PART $39/$3c tables.
# Resolve both from their own animation and OAM pointer tables instead of
# borrowing an ordinary enemy frame at runtime. PART $39 changes its graphics
# bank, tile base, and palette when it enters animation 1, so return the source
# animations separately and emit two self-contained runtime records below.
function Get-DungeonPartVisual(
    [int]$partId,
    [bool]$sourceGrayscaleInverted) {
    $hex = $partId.ToString('x2')
    $partDataPath = Join-Path $Disassembly 'data\ages\partData.s'
    $partRows = @(Read-AssemblyDataDirectives `
        $partDataPath 'partData' '.db')
    if ($partId -ge $partRows.Count -or
        $partRows[$partId].Operands.Count -lt 7) {
        throw "PART_`$$hex graphics data is incomplete."
    }
    $partRow = $partRows[$partId]
    $gfx = Convert-AssemblyInteger $partRow.Operands[0]
    $tileBase = Convert-AssemblyInteger $partRow.Operands[5]
    $oamFlags = Convert-AssemblyInteger $partRow.Operands[6]
    $palette = $oamFlags -band 0x07
    # PART $3b/$3c alias PART $45/$42 at consecutive table labels.
    $animationTableHex = if ($partId -eq 0x03) {
        '24'
    } elseif ($partId -eq 0x3b) {
        '45'
    } elseif ($partId -eq 0x3c) {
        '42'
    } elseif ($partId -eq 0x41) {
        '58'
    } else {
        $hex
    }
    $oamTableHex = if ($partId -eq 0x03) {
        '59'
    } elseif ($partId -eq 0x24) {
        '4d'
    } elseif ($partId -eq 0x3b) {
        '54'
    } else {
        $animationTableHex
    }
    if ($partId -eq 0x03 -and
        ($partAnimationSource -notmatch
            '(?m)^part03Animations:\s*\r?\npart0bAnimations:\s*\r?\n' +
            'part24Animations:\s*\r?\n\s*\.dw\s+partAnimation5b8c0' -or
         $partAnimationSource -notmatch
            '(?m)^part03OamDataPointers:[^\r\n]*\r?\n' +
            'part0bOamDataPointers:[^\r\n]*\r?\n' +
            'part12OamDataPointers:[^\r\n]*\r?\n' +
            'part18OamDataPointers:[^\r\n]*\r?\n' +
            'part20OamDataPointers:[^\r\n]*\r?\n' +
            'part23OamDataPointers:[^\r\n]*\r?\n' +
            'part59OamDataPointers:[^\r\n]*\r?\n')) {
        throw 'PART_ORB no longer aliases PART $24 animations.'
    }
    if ($partId -eq 0x3b -and
        ($partAnimationSource -notmatch
            '(?m)^part3bAnimations:\s*\r?\npart45Animations:\s*\r?\n\s*\.dw\s+partAnimation5ba4f' -or
         $partAnimationSource -notmatch
            '(?m)^part3bOamDataPointers:[^\r\n]*\r?\n' +
            'part45OamDataPointers:[^\r\n]*\r?\n' +
            'part54OamDataPointers:[^\r\n]*\r?\n')) {
        throw 'PART_3b no longer aliases PART $45 graphics.'
    }
    if ($partId -eq 0x24 -and
        $partAnimationSource -notmatch
            '(?m)^part24OamDataPointers:[^\r\n]*\r?\n' +
            'part4bOamDataPointers:[^\r\n]*\r?\n' +
            'part4dOamDataPointers:[^\r\n]*\r?\n') {
        throw 'PART_GROTTO_CRYSTAL no longer aliases PART $4d OAM data.'
    }
    if ($partId -eq 0x3c -and
        $partAnimationSource -notmatch
            '(?m)^part3cAnimations:\s*\r?\npart42Animations:\s*\r?\n\s*\.dw\s+partAnimation5ba27' -or
        $partId -eq 0x3c -and
        $partAnimationSource -notmatch
            '(?m)^part3cOamDataPointers:[^\r\n]*\r?\npart42OamDataPointers:[^\r\n]*\r?\n') {
        throw 'PART_HEAD_THWOMP_CIRCULAR_PROJECTILE no longer aliases PART $42 graphics.'
    }
    if ($partId -eq 0x41 -and
        $partAnimationSource -notmatch
            '(?m)^part41Animations:\s*\r?\npart56Animations:\s*\r?\n' +
            'part58Animations:\s*\r?\n\s*\.dw\s+partAnimation5b8c0') {
        throw 'PART_SHADOW_HAG_SHADOW no longer aliases PART $58 animations.'
    }
    $animationLabels = @([regex]::Matches(
        (Get-AssemblyLabelBody $partAnimationSource "part${animationTableHex}Animations"),
        '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
    $oamLabels = @([regex]::Matches(
        (Get-AssemblyLabelBody $partAnimationSource "part${oamTableHex}OamDataPointers"),
        '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
    if ($animationLabels.Count -eq 0 -or $oamLabels.Count -eq 0) {
        throw "PART_`$$hex animation/OAM tables are incomplete."
    }
    $resolvedAnimations = [Collections.Generic.List[string]]::new()
    foreach ($label in $animationLabels) {
        $frames = [Collections.Generic.List[string]]::new()
        foreach ($frame in [regex]::Matches(
            (Get-AssemblyLabelBody $partAnimationSource $label),
            '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<offset>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')) {
            $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
            $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
            $pointerIndex = [int]([Convert]::ToInt32(
                $frame.Groups['offset'].Value, 16) / 2)
            if ($pointerIndex -ge $oamLabels.Count) {
                throw "PART_`$$hex $label OAM pointer $pointerIndex is out of range."
            }
            $metadata = if ($parameter -eq 0) { "$duration" } else { "$duration,$parameter" }
            $frames.Add(
                "$metadata@$(Resolve-Oam $partOamSource $oamLabels[$pointerIndex])")
            # Most native visuals consumed here only need their first
            # parameter-signalled sequence. PART_SUBTERROR_DIRT is different:
            # $83/$82 are visible-frame parameters and its fifth frame writes
            # the terminal zero observed by partCode32.
            if (($parameter -band 0x80) -ne 0 -and $partId -ne 0x32) {
                break
            }
        }
        if ($frames.Count -eq 0) {
            throw "PART_`$$hex $label has no animation frames."
        }
        $resolvedAnimations.Add($frames -join '|')
    }
    $sprite = $gfxNames[$gfx]
    if (-not $sprite) {
        throw "PART_`$$hex gfx `$$($gfx.ToString('x2')) is missing."
    }
    Copy-EnemySprite $sprite
    $inverted = if ($sourceGrayscaleInverted) { 1 } else { 0 }
    return [PSCustomObject]@{
        Sprite = $sprite
        TileBase = $tileBase
        Palette = $palette
        SourceGrayscaleInverted = $inverted
        Animations = $resolvedAnimations.ToArray()
    }
}

function Add-DungeonPartVisualRow(
    [string]$key,
    [string]$sprite,
    [int]$tileBase,
    [int]$palette,
    [int]$sourceGrayscaleInverted,
    [string[]]$animations) {
    if ($animations.Count -eq 0) {
        throw "Dungeon part visual $key has no animations."
    }
    $animationData = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($animations -join "`n"))
    $dungeonVisualRows.Add(
        "$key`t$sprite`t$tileBase`t$palette`t$sourceGrayscaleInverted`t$animationData")
}

$headThwompFireballVisual = Get-DungeonPartVisual 0x39 $true
$headThwompBoulderVisual = Get-DungeonPartVisual 0x3b $true
$headThwompCircularVisual = Get-DungeonPartVisual 0x3c $true
if ($headThwompFireballVisual.Animations.Count -ne 2 -or
    $headThwompBoulderVisual.Animations.Count -ne 2 -or
    $headThwompCircularVisual.Animations.Count -ne 1) {
    throw 'Head Thwomp projectile animation counts no longer match PART $39/$3b/$3c.'
}

# headThwompFireball_state1 writes oamFlagsBackup/oamFlags/oamTileIndexBase
# at object offsets $1b/$1c/$1d before selecting animation 1. OAM flag bit 3
# selects fixed VRAM bank 1, whose GFXH_COMMON_SPRITES header is $83.
$headThwompFireballPartSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\parts\headThwompFireball.s')
$headThwompFireballImpact = [regex]::Match(
    $headThwompFireballPartSource,
    '(?ms)ld l,\$db\s+ld a,\$(?<flags>[0-9a-f]{2})\s+' +
    'ldi \(hl\),a\s+ldi \(hl\),a\s+' +
    'ld \(hl\),\$(?<tile>[0-9a-f]{2})\s+' +
    'ld a,\$01\s+call partSetAnimation')
if (-not $headThwompFireballImpact.Success) {
    throw 'PART_HEAD_THWOMP_FIREBALL no longer switches OAM flags/tile base before animation 1.'
}
$headThwompFireballImpactFlags = [Convert]::ToInt32(
    $headThwompFireballImpact.Groups['flags'].Value, 16)
$headThwompFireballImpactTileBase = [Convert]::ToInt32(
    $headThwompFireballImpact.Groups['tile'].Value, 16)
$agesGfxHeaderSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\gfxHeaders.s')
$headThwompFireballImpactSprite = 'spr_common_sprites'
if (($headThwompFireballImpactFlags -band 0x08) -eq 0 -or
    $agesGfxHeaderSource -notmatch
        '(?ms)^m_GfxHeaderStart \$83, GFXH_COMMON_SPRITES\s+' +
        'm_GfxHeader spr_common_sprites, \$8001\s+' +
        'm_GfxHeaderEnd') {
    throw 'PART_HEAD_THWOMP_FIREBALL impact no longer selects fixed-bank spr_common_sprites.'
}
Copy-EnemySprite $headThwompFireballImpactSprite

# Purple-face PART_3b starts with object-GFX header $96, then makes the same
# fixed-bank transition as the fireball while selecting common-sprite tile $02.
$headThwompBoulderPartSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\parts\3b.s')
$headThwompBoulderImpact = [regex]::Match(
    $headThwompBoulderPartSource,
    '(?ms)@@func_6ebd:.*?ld l,\$db\s+ld a,\$(?<flags>[0-9a-f]{2})\s+' +
    'ldi \(hl\),a\s+ldi \(hl\),a\s+' +
    'ld \(hl\),\$(?<tile>[0-9a-f]{2})\s+' +
    'ld a,\$01\s+call partSetAnimation')
if (-not $headThwompBoulderImpact.Success) {
    throw 'PART_3b no longer switches OAM flags/tile base before animation 1.'
}
$headThwompBoulderImpactFlags = [Convert]::ToInt32(
    $headThwompBoulderImpact.Groups['flags'].Value, 16)
$headThwompBoulderImpactTileBase = [Convert]::ToInt32(
    $headThwompBoulderImpact.Groups['tile'].Value, 16)
if (($headThwompBoulderImpactFlags -band 0x08) -eq 0) {
    throw 'PART_3b impact no longer selects fixed-bank spr_common_sprites.'
}

Add-DungeonPartVisualRow `
    'head-thwomp-fireball' `
    $headThwompFireballVisual.Sprite `
    $headThwompFireballVisual.TileBase `
    $headThwompFireballVisual.Palette `
    $headThwompFireballVisual.SourceGrayscaleInverted `
    @($headThwompFireballVisual.Animations[0])
Add-DungeonPartVisualRow `
    'head-thwomp-fireball-impact' `
    $headThwompFireballImpactSprite `
    $headThwompFireballImpactTileBase `
    ($headThwompFireballImpactFlags -band 0x07) `
    1 `
    @($headThwompFireballVisual.Animations[1])
Add-DungeonPartVisualRow `
    'head-thwomp-circular-projectile' `
    $headThwompCircularVisual.Sprite `
    $headThwompCircularVisual.TileBase `
    $headThwompCircularVisual.Palette `
    $headThwompCircularVisual.SourceGrayscaleInverted `
    $headThwompCircularVisual.Animations
Add-DungeonPartVisualRow `
    'head-thwomp-boulder' `
    $headThwompBoulderVisual.Sprite `
    $headThwompBoulderVisual.TileBase `
    $headThwompBoulderVisual.Palette `
    $headThwompBoulderVisual.SourceGrayscaleInverted `
    @($headThwompBoulderVisual.Animations[0])
Add-DungeonPartVisualRow `
    'head-thwomp-boulder-impact' `
    $headThwompFireballImpactSprite `
    $headThwompBoulderImpactTileBase `
    ($headThwompBoulderImpactFlags -band 0x07) `
    1 `
    @($headThwompBoulderVisual.Animations[1])

# PART_GROTTO_CRYSTAL $24 uses both static frames from object-GFX $76.
# On collision it creates INTERAC_SARCOPHAGUS $82:$80 directly in @break,
# which replaces the ordinary interaction graphics with fixed-bank common
# sprites, tile base $40, and palette 4 before selecting animation 0.
$grottoCrystalVisual = Get-DungeonPartVisual 0x24 $true
if ($grottoCrystalVisual.Sprite -ne 'spr_pedestal_flame_crystal' -or
    $grottoCrystalVisual.TileBase -ne 0x12 -or
    $grottoCrystalVisual.Palette -ne 1 -or
    $grottoCrystalVisual.Animations.Count -ne 2) {
    throw 'PART_GROTTO_CRYSTAL $24 graphics no longer match object-GFX $76.'
}
Add-DungeonPartVisualRow `
    'grotto-crystal' `
    $grottoCrystalVisual.Sprite `
    $grottoCrystalVisual.TileBase `
    $grottoCrystalVisual.Palette `
    $grottoCrystalVisual.SourceGrayscaleInverted `
    $grottoCrystalVisual.Animations
$grottoCrystalBreakSprite = 'spr_common_sprites'
Copy-EnemySprite $grottoCrystalBreakSprite
Add-DungeonPartVisualRow `
    'grotto-crystal-break' `
    $grottoCrystalBreakSprite `
    0x40 `
    4 `
    1 `
    @((Resolve-DungeonInteractionAnimation 0x82 0))

# PART_ORB $03 shares the two static part frames but loads object-GFX $74.
$grottoOrbVisual = Get-DungeonPartVisual 0x03 $true
if ($grottoOrbVisual.Sprite -ne 'spr_roller_owl_barrier_orb' -or
    $grottoOrbVisual.TileBase -ne 0x1e -or
    $grottoOrbVisual.Palette -ne 0 -or
    $grottoOrbVisual.Animations.Count -ne 2) {
    throw 'PART_ORB $03 graphics no longer match object-GFX $74.'
}
Add-DungeonPartVisualRow `
    'grotto-orb' `
    $grottoOrbVisual.Sprite `
    $grottoOrbVisual.TileBase `
    $grottoOrbVisual.Palette `
    $grottoOrbVisual.SourceGrayscaleInverted `
    $grottoOrbVisual.Animations

# PART_SUBTERROR_DIRT $32 uses the third Subterror object-GFX header and its
# one terminal animation. It is globally a part, but only the native $72
# miniboss creates it.
$subterrorDirtVisual = Get-DungeonPartVisual 0x32 $true
$subterrorDirtFrames = @($subterrorDirtVisual.Animations[0] -split '\|')
if ($subterrorDirtVisual.Sprite -ne 'spr_subterror_3' -or
    $subterrorDirtVisual.TileBase -ne 0 -or
    $subterrorDirtVisual.Palette -ne 6 -or
    $subterrorDirtVisual.Animations.Count -ne 1 -or
    $subterrorDirtFrames.Count -ne 5 -or
    $subterrorDirtFrames[0] -notmatch '^3,131@' -or
    $subterrorDirtFrames[1] -notmatch '^6,130@' -or
    $subterrorDirtFrames[2] -notmatch '^6,131@' -or
    $subterrorDirtFrames[3] -notmatch '^6,131@' -or
    $subterrorDirtFrames[4] -notmatch '^1@') {
    throw 'PART_SUBTERROR_DIRT $32 graphics no longer match object-GFX $b3.'
}
Add-DungeonPartVisualRow `
    'subterror-dirt' `
    $subterrorDirtVisual.Sprite `
    $subterrorDirtVisual.TileBase `
    $subterrorDirtVisual.Palette `
    $subterrorDirtVisual.SourceGrayscaleInverted `
    $subterrorDirtVisual.Animations

# PART_SHADOW_HAG_SHADOW $41 uses object-GFX $a7 and selects animation 1.
# Retain its full five-entry source table because the part owns the mapping,
# even though this handler uses only the static second entry.
$shadowHagShadowVisual = Get-DungeonPartVisual 0x41 $true
if ($shadowHagShadowVisual.Sprite -ne 'spr_projectiles_3' -or
    $shadowHagShadowVisual.TileBase -ne 0 -or
    $shadowHagShadowVisual.Palette -ne 0 -or
    $shadowHagShadowVisual.Animations.Count -ne 5) {
    throw 'PART_SHADOW_HAG_SHADOW $41 graphics no longer match object-GFX $a7.'
}
Add-DungeonPartVisualRow `
    'shadow-hag-shadow' `
    $shadowHagShadowVisual.Sprite `
    $shadowHagShadowVisual.TileBase `
    $shadowHagShadowVisual.Palette `
    $shadowHagShadowVisual.SourceGrayscaleInverted `
    $shadowHagShadowVisual.Animations

# PART_ROTATABLE_SEED_THING $33 uses object-GFX $73 and four static
# orientations. Its automatically created, invisible $03 child copies the
# animation parameter and collision radii but is not rendered.
$seedBouncerVisual = Get-DungeonPartVisual 0x33 $true
if ($seedBouncerVisual.Sprite -ne 'spr_spinner_seedbouncer' -or
    $seedBouncerVisual.TileBase -ne 0x1a -or
    $seedBouncerVisual.Palette -ne 2 -or
    $seedBouncerVisual.Animations.Count -ne 4) {
    throw 'PART_ROTATABLE_SEED_THING $33 graphics no longer match object-GFX $73.'
}
Add-DungeonPartVisualRow `
    'rotatable-seed-thing' `
    $seedBouncerVisual.Sprite `
    $seedBouncerVisual.TileBase `
    $seedBouncerVisual.Palette `
    $seedBouncerVisual.SourceGrayscaleInverted `
    $seedBouncerVisual.Animations

if ($dungeonVisualRows.Count -ne 28) {
    throw "Expected twenty-seven imported shared dungeon interaction visuals."
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\dungeon_interaction_visuals.tsv'),
    $dungeonVisualRows)

# Moonlit Grotto's spinner is created by roomSpecificCode rather than its
# empty object stream. Breaking the dungeon crystals moves the room-$60
# layout and the same mask-$01 spinner to room $52. Retain that conditional
# source dispatch as data so runtime code does not invent a room exception.
$roomSpecificCodeSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\roomSpecificCode.s')
$globalFlagSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\globalFlags.s')
if ($globalFlagSource -notmatch
        '(?m)^\s*GLOBALFLAG_D3_CRYSTALS\s+db\s*;\s*\$0f\s*$' -or
    $roomSpecificCodeSource -notmatch
        '(?ms)^roomSpecificCodeGroup4Table:.*?\.db \$60 \$01.*?\.db \$52 \$02.*?\.db \$00' -or
    $roomSpecificCodeSource -notmatch
        '(?ms)^roomSpecificCode1:.*?ld a, GLOBALFLAG_D3_CRYSTALS.*?call checkGlobalFlag.*?ret nz.*?ld \(hl\),\$7d.*?ld \(hl\),\$57.*?ld \(hl\),\$01.*?ret' -or
    $roomSpecificCodeSource -notmatch
        '(?ms)^roomSpecificCode2:.*?ld a,GLOBALFLAG_D3_CRYSTALS.*?call checkGlobalFlag.*?ret z.*?jr ---') {
    throw 'Moonlit Grotto room-specific spinner dispatch changed.'
}
$spinnerRows = [Collections.Generic.List[string]]::new()
$spinnerRows.Add(
    '# group`troom`tposition`tstate-mask`trequired-global-flag`trequired-global-state`tsource'.Replace(
        '`t', "`t"))
$spinnerRows.Add(
    "4`t60`t57`t01`t0f`t0`tcode/ages/roomSpecificCode.s:roomSpecificCode1")
$spinnerRows.Add(
    "4`t52`t57`t01`t0f`t1`tcode/ages/roomSpecificCode.s:roomSpecificCode2")
Write-GeneratedTable(
    (Join-Path $destination 'objects\dungeon_spinners.tsv'),
    $spinnerRows)
foreach ($obsoleteDungeonAsset in @(
    'objects\spirits_grave_enemies.tsv',
    'objects\spirits_grave_head_thwomp_palette.bin',
    'objects\spirits_grave_cube_palettes.bin',
    'objects\spirits_grave_visuals.tsv'
)) {
    [IO.File]::Delete((Join-Path $destination $obsoleteDungeonAsset))
}

# Preserve the native object order. before-event bosses are emitted after the
# ordinary main-room objects and are active only while ROOMFLAG_BIT_80 is clear.
$expectedSpiritsGraveMainData = @'
group4Map10ObjectData:
	obj_Interaction $20 $01 $58 $58
	obj_End

group4Map11ObjectData:
	obj_Interaction $7f $00 $28 $78
	obj_End
'@
if (-not $mainObjectSource.Contains($expectedSpiritsGraveMainData.Replace("`r", ''))) {
    throw "Spirit's Grave bracelet/essence source placements changed."
}
foreach ($required in @(
    '^group4Map13BeforeEventObjectData:\s+obj_SpecificEnemyA \$00 \$78 \$00 \$58 \$78\s+obj_EndPointer',
    '^group4Map18BeforeEventObjectData:\s+obj_SpecificEnemyA \$00 \$70 \$00 \$58 \$78\s+obj_EndPointer'
)) {
    if ($enemyObjectSource -notmatch "(?ms)$required") {
        throw "Spirit's Grave before-event boss placement changed."
    }
}

# Moonlit Grotto room $4d orders its two shutter controllers, dormant portal,
# miniboss-death script, then the BeforeEvent ENEMY_SUBTERROR record. Keep the
# native reward and enemy rows together so the runtime can merge them into
# that existing source stream without treating either as an ordinary pointer.
$expectedMoonlitMinibossMainData = @'
group4Map4dObjectData:
	obj_Interaction $1e $0a $a7 $00
	obj_Interaction $1e $0b $30 $00
	obj_Interaction $7e $00 $58 $78
	obj_Interaction $20 $00 $58 $78
	obj_BeforeEvent group4Map4dBeforeEventObjectData
	obj_End
'@
if (-not $mainObjectSource.Contains(
        $expectedMoonlitMinibossMainData.Replace("`r", '')) -or
    $enemyObjectSource -notmatch
        '(?ms)^group4Map4dBeforeEventObjectData:\s+' +
        'obj_SpecificEnemyA \$00 \$72 \$00 \$18 \$78\s+' +
        'obj_EndPointer') {
    throw 'Moonlit Grotto room 4:4d miniboss object stream changed.'
}
$expectedMoonlitBossMainData = @'
group4Map4aObjectData:
	obj_Interaction $1e $0b $50 $00
	obj_Interaction $1e $09 $5e $00
	obj_Interaction $20 $01 $58 $78
	obj_BeforeEvent group4Map4aBeforeEventObjectData
	obj_End
'@
 $expectedMoonlitEssenceMainData = @'
group4Map49ObjectData:
	obj_Interaction $7f $00 $28 $78
	obj_End
'@
if (-not $mainObjectSource.Contains(
        $expectedMoonlitEssenceMainData.Replace("`r", ''))) {
    throw 'Moonlit Grotto room 4:49 Essence object stream changed.'
}
if (-not $mainObjectSource.Contains(
        $expectedMoonlitBossMainData.Replace("`r", '')) -or
    $enemyObjectSource -notmatch
        '(?ms)^group4Map4aBeforeEventObjectData:\s+' +
        'obj_SpecificEnemyA \$00 \$7a \$00 \$58 \$d8\s+' +
        'obj_EndPointer') {
    throw 'Moonlit Grotto room 4:4a boss object stream changed.'
}
$shadowHagSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\enemies\shadowHag.s')
$shadowHagBugSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\enemies\shadowHagBug.s')
$shadowHagShadowSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\parts\shadowHagShadow.s')
foreach ($contract in @(
    @($shadowHagSource, '(?ms)^shadowHag_stateA:.*?wFrameCounter.*?ecom_decCounter1.*?getRandomNumber_noPreserveVars.*?@targetPositions:\s*\.db \$38 \$48\s*\.db \$38 \$b8\s*\.db \$78 \$48\s*\.db \$78 \$b8'),
    @($shadowHagSource, '(?ms)^shadowHag_stateD:.*?and \$0f.*?cp \$07.*?ENEMY_SHADOW_HAG_BUG.*?inc \(hl\)'),
    @($shadowHagSource, '(?ms)^shadowHag_chooseSpawnPosition:.*?@spawnOffsets:\s*\.db \$40 \$00\s*\.db \$08 \$c0\s*\.db \$c0 \$00\s*\.db \$08 \$40'),
    @($shadowHagBugSource, '(?ms)^shadowHagBug_state8:.*?objectUpdateSpeedZ_paramC.*?getRandomNumber.*?\(hl\),180.*?^shadowHagBug_state9:.*?cp 30.*?ld bc,\$0f0f.*?ecom_randomBitwiseAndBCE'),
    @($shadowHagShadowSource, '(?ms)^@state0:.*?\(hl\),\$08.*?SPEED_100.*?@angles:\s*\.db \$04 \$0c \$14 \$1c.*?^@state1:.*?Object\.counter1.*?objectNudgeAngleTowards.*?objectApplySpeed.*?^@state2:.*?add \$04\s+cp \$09.*?Enemy\.counter2\s+dec \(hl\)')
)) {
    if ($contract[0] -notmatch $contract[1]) {
        throw "Shadow Hag source contract changed: $($contract[1])"
    }
}
foreach ($required in @(
    'ld \(hl\),SPEED_180',
    'ld \(hl\),60',
    'ld \(hl\),90',
    'ld \(hl\),180',
    'subterror_speedVals:[^\r\n]*\r?\n\s*\.db SPEED_80 SPEED_100 SPEED_180',
    'subterror_timeUntilDrillAttack:[^\r\n]*\r?\n\s*\.db 120 90 60',
    'subterror_durationAboveGround:[^\r\n]*\r?\n\s*\.db 60 90 120 180')) {
    if ($subterrorSource -notmatch "(?m)$required") {
        throw "ENEMY_SUBTERROR source contract changed: $required"
    }
}
$moonlitMinibossRows = @(
    "# group`troom`torder`tkind`tid`tsubid`ty`tx`tcondition`tsource"
    "4`t49`t0`tessence`t7f`t00`t28`t78`talways`tmainData.s:group4Map49ObjectData"
    "4`t4a`t2`tboss-reward`t20`t01`t58`t78`titem-clear`tmainData.s:group4Map4aObjectData"
    "4`t4a`t3`tshadow-hag`t7a`t00`t58`td8`tflag80-clear`tenemyData.s:group4Map4aBeforeEventObjectData"
    "4`t4d`t3`tminiboss-reward`t20`t00`t58`t78`tflag80-clear`tmainData.s:group4Map4dObjectData"
    "4`t4d`t4`tsubterror`t72`t00`t18`t78`tflag80-clear`tenemyData.s:group4Map4dBeforeEventObjectData"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\moonlit_grotto_objects.tsv'),
    $moonlitMinibossRows)
if (-not $allTexts.ContainsKey(0x0010) -or
    -not $allTexts.ContainsKey(0x2f03) -or
    -not $allTexts.ContainsKey(0x2f2b)) {
    throw 'Moonlit Grotto text TX_0010/TX_2f03/TX_2f2b was not imported.'
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\moonlit_grotto_text.tsv'),
    @(
        "# text-id`tmessage-base64"
        "0010`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x0010])))"
        "2f03`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x2f03])))"
        "2f2b`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x2f2b])))"
    ))

$sgObjectRows = @(
    "# group`troom`torder`tkind`tid`tsubid`ty`tx`tcondition`t source".Replace("`t source", "`tsource")
    "4`t10`t0`tbracelet-reward`t20`t01`t58`t58`titem-clear`tmainData.s:group4Map10ObjectData"
    # INTERAC_ESSENCE creates its subid-$01 pedestal before testing
    # ROOMFLAG_ITEM and deleting only the essence/glow object.
    "4`t11`t0`tessence`t7f`t00`t28`t78`talways`tmainData.s:group4Map11ObjectData"
    "4`t13`t2`tboss-reward`t20`t03`t58`t78`titem-clear`tmainData.s:group4Map13ObjectData"
    "4`t13`t3`tpumpkin-head`t78`t00`t58`t78`tflag80-clear`tenemyData.s:group4Map13BeforeEventObjectData"
    "4`t15`t1`tmoving-platform`t79`t05`t90`t30`talways`tmainData.s:group4Map15ObjectData"
    "4`t16`t1`tspawn-moving-platform`t20`t05`t00`t00`talways`tmainData.s:group4Map16ObjectData"
    "4`t18`t2`tminiboss-reward`t20`t02`t58`t78`tflag80-clear`tmainData.s:group4Map18ObjectData"
    "4`t18`t4`tgiant-ghini`t70`t00`t58`t78`tflag80-clear`tenemyData.s:group4Map18BeforeEventObjectData"
    "4`t1b`t0`ttorch-stairs`t20`t04`t28`tb8`tflag80-clear`tmainData.s:group4Map1bObjectData"
    "4`t1e`t2`tenemy-small-key`t12`t01`t58`t38`titem-clear`tmainData.s:group4Map1eObjectData"
    "4`t20`t2`tcolored-cube`t19`t05`t78`ta8`talways`tmainData.s:group4Map20ObjectData"
    "4`t20`t3`tcube-flame`t1a`t00`t2e`t98`talways`tmainData.s:group4Map20ObjectData"
    "4`t20`t4`tcube-flame`t1a`t00`t4e`t98`talways`tmainData.s:group4Map20ObjectData"
    "4`t20`t5`tcube-flame`t1a`t00`t2e`tb8`talways`tmainData.s:group4Map20ObjectData"
    "4`t20`t6`tcube-flame`t1a`t00`t4e`tb8`talways`tmainData.s:group4Map20ObjectData"
    "4`t20`t7`tcube-light-sensor`t21`t03`t48`ta8`talways`tmainData.s:group4Map20ObjectData"
    "4`t20`t8`tcube-trigger-sensor`t21`t19`t00`t00`talways`tmainData.s:group4Map20ObjectData"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\spirits_grave_objects.tsv'),
    $sgObjectRows)

$movingPlatformSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\movingPlatform.s')
$linkCommonSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\commonCode.s')
$platformRadiusBlock = [regex]::Match(
    $movingPlatformSource,
    '(?ms)^@collisionRadii:\s*(?<body>(?:\s*\.db\s+\$[0-9a-f]{2}\s+\$[0-9a-f]{2}\s*){6})')
$platformRadiusMatches = @([regex]::Matches(
    $platformRadiusBlock.Groups['body'].Value,
    '\.db\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})'))
if (-not $platformRadiusBlock.Success -or
    $platformRadiusMatches.Count -ne 6 -or
    $movingPlatformSource -notmatch '(?ms)^@state1:.*?wLinkRidingObject.*?@checkLinkTouching.*?add \$05.*?interactionCheckContainsPoint.*?^@substate1:.*?objectApplySpeed.*?wLinkRidingObject.*?w1Link\.state.*?updateLinkPositionGivenVelocity' -or
    $linkCommonSource -notmatch '(?ms)^@tileType_hole:.*?wLinkRidingObject.*?or a\s+jr nz,@tileType_normal') {
    throw 'Moving-platform collision, Link displacement, or riding-object hole suppression changed.'
}

$pumpkinBodyPalette = [regex]::Match(
    $pumpkinHeadSource,
    '(?ms)^pumpkinHead_body_state08:.*?ld l,Enemy\.oamFlags\s+ld a,\$(?<value>[0-9a-f]{2})\s+ldd \(hl\),a\s+ld \(hl\),a')
$pumpkinGhostPalette = [regex]::Match(
    $pumpkinHeadSource,
    '(?ms)^pumpkinHead_ghost_state08:.*?ld l,Enemy\.oamFlags\s+ld a,\$(?<value>[0-9a-f]{2})\s+ldd \(hl\),a\s+ld \(hl\),a')
if (-not $pumpkinBodyPalette.Success -or
    -not $pumpkinGhostPalette.Success) {
    throw 'Pumpkin Head body/ghost OAM palette overrides changed.'
}
$dungeonBossConstantRows = @(
    "# key`tvalue"
    "pumpkin-body-palette`t$([Convert]::ToInt32($pumpkinBodyPalette.Groups['value'].Value, 16) -band 7)"
    "pumpkin-ghost-palette`t$([Convert]::ToInt32($pumpkinGhostPalette.Groups['value'].Value, 16) -band 7)"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\dungeon_boss_constants.tsv'),
    $dungeonBossConstantRows)

$objectSpeedSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
if ($objectSpeedSource -notmatch
        '(?m)^\s*SPEED_80\s+dsb\s+5\s*;\s*0x14\s*$') {
    throw 'SPEED_80 no longer resolves to object-speed index $14.'
}
$dungeonObjectConstantRows = [Collections.Generic.List[string]]::new()
$dungeonObjectConstantRows.Add("# key`tvalue")
foreach ($row in @(
    "platform-speed`t20"
    "platform-wait`t8"
    "cube-push-frames`t20"
    "cube-hole-frames`t10"
    "miniboss-reward-wait`t20"
    "move-block-sound`t127"
)) {
    $dungeonObjectConstantRows.Add($row)
}
for ($size = 0; $size -lt $platformRadiusMatches.Count; $size++) {
    $radius = $platformRadiusMatches[$size]
    $dungeonObjectConstantRows.Add(
        "platform-radius-$size-y`t$([Convert]::ToInt32($radius.Groups['y'].Value, 16))")
    $dungeonObjectConstantRows.Add(
        "platform-radius-$size-x`t$([Convert]::ToInt32($radius.Groups['x'].Value, 16))")
}
if ($dungeonObjectConstantRows.Count -ne 19 -or
    -not $dungeonObjectConstantRows.Contains("platform-radius-1-y`t16") -or
    -not $dungeonObjectConstantRows.Contains("platform-radius-1-x`t8") -or
    -not $dungeonObjectConstantRows.Contains("platform-radius-5-y`t16") -or
    -not $dungeonObjectConstantRows.Contains("platform-radius-5-x`t16")) {
    throw 'Expected all six moving-platform collision-radius pairs.'
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\dungeon_object_behavior_constants.tsv'),
    $dungeonObjectConstantRows)

$sgConstantsRows = @(
    "# key`tvalue"
    "moving-platform-spawn-wait`t30"
    "torch-count`t2"
    "torch-tile`t69"
    "solve-sound`t119"
    "light-torch-sound`t114"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\spirits_grave_constants.tsv'),
    $sgConstantsRows)

if (-not $allTexts.ContainsKey(0x000e)) {
    throw 'Spirit''s Grave Eternal Spirit text TX_000e was not imported.'
}
$sgTextRows = @(
    "# text-id`tmessage-base64"
    "000e`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x000e])))"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\spirits_grave_text.tsv'),
    $sgTextRows)
