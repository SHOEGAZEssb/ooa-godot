# Spirit's Grave is the first dungeon (dungeon index $01, group $04). Its
# ordinary object stream is already imported globally, but several native
# interaction handlers and the two before-event boss streams need typed data
# of their own. Resolve graphics/OAM here while all shared importer tables are
# still in scope; runtime code never reads assembly source.

function Read-SpiritsGraveDwTables(
    [string]$source,
    [string]$labelPattern,
    [string]$entryPattern) {
    $tables = @{}
    $aliases = [Collections.Generic.List[string]]::new()
    $entries = [Collections.Generic.List[string]]::new()
    foreach ($line in ($source -split '\r?\n')) {
        $label = [regex]::Match($line, "^(?<label>$labelPattern):")
        if ($label.Success) {
            if ($entries.Count -gt 0) {
                foreach ($alias in $aliases) { $tables[$alias] = @($entries) }
                $aliases.Clear()
                $entries.Clear()
            }
            $aliases.Add($label.Groups['label'].Value)
            continue
        }
        if ($aliases.Count -eq 0) { continue }
        $entry = [regex]::Match($line, "^\s*\.dw\s+(?<entry>$entryPattern)")
        if ($entry.Success) {
            $entries.Add($entry.Groups['entry'].Value)
            continue
        }
        if ($entries.Count -gt 0 -and $line -match '^[A-Za-z0-9_@]+:') {
            foreach ($alias in $aliases) { $tables[$alias] = @($entries) }
            $aliases.Clear()
            $entries.Clear()
        }
    }
    if ($entries.Count -gt 0) {
        foreach ($alias in $aliases) { $tables[$alias] = @($entries) }
    }
    return $tables
}

function Read-SpiritsGraveEnemyAnimationFrames([string]$source) {
    $script:sgAnimResult = @{}
    $script:sgAnimAliases = [Collections.Generic.List[string]]::new()
    $script:sgAnimFrames = [Collections.Generic.List[object]]::new()
    $script:sgAnimFrameOffsets = @{}
    $script:sgAnimLoopStart = 0

    function Complete-SpiritsGraveEnemyAnimation {
        if ($script:sgAnimAliases.Count -eq 0 -or $script:sgAnimFrames.Count -eq 0) { return }
        $record = @{
            Frames = @($script:sgAnimFrames)
            LoopStart = $script:sgAnimLoopStart
        }
        foreach ($alias in $script:sgAnimAliases) { $script:sgAnimResult[$alias] = $record }
        $script:sgAnimAliases.Clear()
        $script:sgAnimFrames.Clear()
        $script:sgAnimFrameOffsets.Clear()
        $script:sgAnimLoopStart = 0
    }

    foreach ($line in ($source -split '\r?\n')) {
        $label = [regex]::Match($line, '^(?<label>enemyAnimation[0-9a-f]+(?:Loop)?):')
        if ($label.Success) {
            $name = $label.Groups['label'].Value
            if ($name.EndsWith('Loop') -and $script:sgAnimAliases.Count -gt 0) {
                $script:sgAnimFrameOffsets[$name] = $script:sgAnimFrames.Count
            } else {
                if ($script:sgAnimFrames.Count -gt 0) { Complete-SpiritsGraveEnemyAnimation }
                $script:sgAnimAliases.Add($name)
                $script:sgAnimFrameOffsets[$name] = 0
            }
            continue
        }
        if ($script:sgAnimAliases.Count -eq 0) { continue }
        $frame = [regex]::Match(
            $line,
            '^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<offset>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')
        if ($frame.Success) {
            $script:sgAnimFrames.Add(@{
                Duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
                PointerOffset = [Convert]::ToInt32($frame.Groups['offset'].Value, 16)
                Parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
            })
            continue
        }
        $loop = [regex]::Match($line, '^\s*m_AnimationLoop\s+(?<target>enemyAnimation[0-9a-f]+(?:Loop)?)')
        if ($loop.Success) {
            $target = $loop.Groups['target'].Value
            if (-not $script:sgAnimFrameOffsets.ContainsKey($target)) {
                throw "Enemy animation loop target not found: $target"
            }
            $script:sgAnimLoopStart = $script:sgAnimFrameOffsets[$target]
            Complete-SpiritsGraveEnemyAnimation
        }
    }
    Complete-SpiritsGraveEnemyAnimation
    $result = $script:sgAnimResult
    Remove-Variable sgAnimResult,sgAnimAliases,sgAnimFrames,sgAnimFrameOffsets,sgAnimLoopStart `
        -Scope Script
    return $result
}

$sgEnemyAnimationTables = Read-SpiritsGraveDwTables `
    $enemyAnimationSource 'enemy[0-9a-f]{2}Animations' 'enemyAnimation[0-9a-f]+'
$sgEnemyOamTables = Read-SpiritsGraveDwTables `
    $enemyAnimationSource 'enemy[0-9a-f]{2}OamDataPointers' 'enemyOamData[0-9a-f]+'
$sgEnemyAnimationFrames = Read-SpiritsGraveEnemyAnimationFrames $enemyAnimationSource
$enemyObjectSource = Get-Content -Raw (
    Join-Path $Disassembly 'objects\ages\enemyData.s')
$pumpkinHeadSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\ages\enemies\pumpkinHead.s')

function Resolve-SpiritsGraveEnemyAnimations([int]$id) {
    $hex = $id.ToString('x2')
    $animationKey = "enemy${hex}Animations"
    $oamKey = "enemy${hex}OamDataPointers"
    if (-not $sgEnemyAnimationTables.ContainsKey($animationKey) -or
        -not $sgEnemyOamTables.ContainsKey($oamKey)) {
        throw "Enemy `$$hex animation/OAM table is missing."
    }
    $pointers = $sgEnemyOamTables[$oamKey]
    $encoded = [Collections.Generic.List[string]]::new()
    foreach ($animationLabel in $sgEnemyAnimationTables[$animationKey]) {
        if (-not $sgEnemyAnimationFrames.ContainsKey($animationLabel)) {
            throw "Enemy `$$hex animation body is missing: $animationLabel"
        }
        $definition = $sgEnemyAnimationFrames[$animationLabel]
        $frames = [Collections.Generic.List[string]]::new()
        $valid = $true
        foreach ($frame in $definition.Frames) {
            $pointerIndex = [int]($frame.PointerOffset / 2)
            if ($pointerIndex -lt 0 -or $pointerIndex -ge $pointers.Count) {
                # Several alias animation tables intentionally run into the
                # following enemy's entries. The current enemy can only use
                # the prefix addressable by its own OAM pointer table.
                $valid = $false
                break
            }
            $metadata = if ($frame.Parameter -eq 0) {
                "$($frame.Duration)"
            } else {
                "$($frame.Duration),$($frame.Parameter)"
            }
            $frames.Add("$metadata@$(Resolve-Oam $enemyOamSource $pointers[$pointerIndex])")
        }
        if (-not $valid) { break }
        $value = $frames -join '|'
        if ($definition.LoopStart -gt 0) { $value += "~$($definition.LoopStart)" }
        $encoded.Add($value)
    }
    return @($encoded)
}

function Get-SpiritsGraveEnemyDefinition([int]$id, [int]$subid = 0) {
    $hex = $id.ToString('x2')
    $line = [regex]::Match(
        $enemyDataSource,
        ('(?m)^\s*/\* 0x{0} \*/ m_EnemyData \$(?<gfx>[0-9a-f]{{2}}) \$(?<collision>[0-9a-f]{{2}}) (?:(?:\$(?<extra>[0-9a-f]{{2}}) \$(?<flags>[0-9a-f]{{2}}))|(?<subids>enemy[0-9a-f]{{2}}SubidData))' -f $hex))
    if (-not $line.Success) { throw "Enemy data row `$$hex is missing." }
    $extra = 0
    $flags = 0
    if ($line.Groups['subids'].Success) {
        $rows = @([regex]::Matches(
            (Get-AssemblyLabelBody $enemyDataSource $line.Groups['subids'].Value),
            '(?m)^\s*m_EnemySubidData \$(?<extra>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'))
        if ($subid -ge $rows.Count) { throw "Enemy `$$hex subid `$$($subid.ToString('x2')) has no data row." }
        $extra = [Convert]::ToInt32($rows[$subid].Groups['extra'].Value, 16) -band 0x7f
        $flags = [Convert]::ToInt32($rows[$subid].Groups['flags'].Value, 16)
    } else {
        $extra = [Convert]::ToInt32($line.Groups['extra'].Value, 16) -band 0x7f
        $flags = [Convert]::ToInt32($line.Groups['flags'].Value, 16)
    }
    if ($extra -ge $extraEnemyRows.Count) { throw "Enemy `$$hex extra-data index is out of range." }
    $extraRow = $extraEnemyRows[$extra]
    $damageByte = [Convert]::ToInt32($extraRow.Groups['damage'].Value, 16)
    return @{
        Id = $id
        Gfx = [Convert]::ToInt32($line.Groups['gfx'].Value, 16)
        TileBase = ($flags -band 0x0f) * 2
        Palette = ($flags -shr 4) -band 7
        RadiusY = [Convert]::ToInt32($extraRow.Groups['y'].Value, 16)
        RadiusX = [Convert]::ToInt32($extraRow.Groups['x'].Value, 16)
        Damage = (0x100 - $damageByte) / 2
        Health = [Convert]::ToInt32($extraRow.Groups['health'].Value, 16)
        Animations = Resolve-SpiritsGraveEnemyAnimations $id
    }
}

function Copy-SpiritsGraveSprite([string]$name) {
    $source = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
        ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$name.png" } |
        Select-Object -First 1
    if ($null -eq $source) { throw "Spirit's Grave sprite not found: $name.png" }
    Copy-Item -LiteralPath $source.FullName `
        -Destination (Join-Path $destination "gfx\$name.png") -Force
}

$sgEnemySpriteSequences = @{
    0x0a = @($gfxNames[0x91])
    0x10 = @($gfxNames[0x9b])
    0x17 = @($gfxNames[0x90])
    0x28 = @($gfxNames[0xa0])
    0x3f = @($gfxNames[0xad], $gfxNames[0xae])
    0x70 = @($gfxNames[0xad], $gfxNames[0xae])
    0x78 = @($gfxNames[0xbc], $gfxNames[0xbd], $gfxNames[0xbe])
}
$sgEnemySourceGrayscaleInverted = @{
    0x0a = $true
    0x10 = $true
    0x17 = $true
    0x28 = $true
    # Giant Ghini's two source sheets use white as color 0, unlike the
    # ordinary black-background enemy sheets.
    0x3f = $false
    0x70 = $false
    0x78 = $true
}
$sgEnemyRows = [Collections.Generic.List[string]]::new()
$sgEnemyRows.Add('# id`tsubid`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tanimations-base64'.Replace('`t', "`t"))
foreach ($spec in @(
    @(0x0a, 0), @(0x10, 0), @(0x17, 0), @(0x28, 0),
    @(0x3f, 0), @(0x70, 0), @(0x78, 0)
)) {
    $id = [int]$spec[0]
    $subid = [int]$spec[1]
    $definition = Get-SpiritsGraveEnemyDefinition $id $subid
    $sprites = $sgEnemySpriteSequences[$id]
    foreach ($sprite in $sprites) { Copy-SpiritsGraveSprite $sprite }
    $animations = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($definition.Animations -join "`n"))
    $sourceGrayscaleInverted = if ($sgEnemySourceGrayscaleInverted[$id]) { 1 } else { 0 }
    $sgEnemyRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($sprites -join ',')`t$($definition.TileBase)`t$($definition.Palette)`t$sourceGrayscaleInverted`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.Damage)`t$($definition.Health)`t$animations")
}
if ($sgEnemyRows.Count -ne 8 -or
    -not ($sgEnemyRows | Where-Object { $_ -match '^0a\t00\tspr_moblin\t0\t2\t1\t6\t6\t2\t3\t' }) -or
    -not ($sgEnemyRows | Where-Object { $_ -match '^3f\t00\tspr_giantghini_1,spr_giantghini_2\t0\t5\t0\t2\t2\t128\t2\t' }) -or
    -not ($sgEnemyRows | Where-Object { $_ -match '^70\t00\tspr_giantghini_1,spr_giantghini_2\t0\t5\t0\t10\t10\t1\t12\t' }) -or
    -not ($sgEnemyRows | Where-Object { $_ -match '^78\t00\tspr_pumpkinhead_1,spr_pumpkinhead_2,spr_pumpkinhead_3\t0\t3\t1\t6\t12\t2\t8\t' })) {
    throw "Spirit's Grave enemy definitions no longer match the traced ordinary/boss records:`n$($sgEnemyRows -join "`n")"
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\spirits_grave_enemies.tsv'),
    $sgEnemyRows,
    [Text.UTF8Encoding]::new($false))

# Resolve native interaction graphics used by the moving platforms, rotating
# cube/flames, and the first essence. The cube state machine selects all 30
# animations in its source table, so retain the complete sequence.
$sgVisualRows = [Collections.Generic.List[string]]::new()
$sgVisualRows.Add(
    '# key`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tanimations-base64'.Replace(
        '`t', "`t"))

# The broad NPC importer accepts several deliberately shared animation tails.
# These native dungeon objects instead use self-contained records whose final
# parameter byte has bit 7 set (or an explicit m_AnimationLoop). Read exactly
# one such record so a cube animation can never absorb the following labels.
function Resolve-SpiritsGraveInteractionAnimation(
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
    if ($frames.Count -eq 0 -or -not $terminal) {
        throw "Interaction `$$hex animation $animationIndex is incomplete: $label"
    }
    return $frames -join '|'
}

function Add-SpiritsGraveInteractionVisual(
    [string]$key,
    [int]$id,
    [int]$subid,
    [int[]]$animations,
    [int]$tileBaseOverride = -1,
    [int]$paletteOverride = -1,
    [bool]$sourceGrayscaleInverted = $true) {
    $graphic = $interactionGraphics["$id`:$subid"]
    if ($null -eq $graphic) { $graphic = $interactionGraphics["$id`:0"] }
    if ($null -eq $graphic -or -not $gfxNames.ContainsKey($graphic.Gfx)) {
        throw "Spirit's Grave interaction visual $key (`$$($id.ToString('x2')):`$$($subid.ToString('x2'))) is missing."
    }
    $sprite = $gfxNames[$graphic.Gfx]
    Copy-SpiritsGraveSprite $sprite
    $resolved = @($animations | ForEach-Object {
        Resolve-SpiritsGraveInteractionAnimation $id $_
    })
    if ($resolved.Count -eq 0 -or ($resolved | Where-Object { -not $_ }).Count -gt 0) {
        throw "Spirit's Grave interaction visual $key has unresolved animations."
    }
    $animationData = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($resolved -join "`n"))
    $tileBase = if ($tileBaseOverride -ge 0) { $tileBaseOverride } else { $graphic.TileBase }
    $palette = if ($paletteOverride -ge 0) { $paletteOverride } else { $graphic.Palette }
    $inverted = if ($sourceGrayscaleInverted) { 1 } else { 0 }
    $sgVisualRows.Add("$key`t$sprite`t$tileBase`t$palette`t$inverted`t$animationData")
}
Add-SpiritsGraveInteractionVisual 'platform-05' 0x79 5 @(5)
Add-SpiritsGraveInteractionVisual 'platform-09' 0x79 1 @(1)
# Unlike the ordinary black-background spr_* sheets, spr_colored_cube is
# authored black-on-white. Retain that source interpretation so color zero,
# not the cube drawing, becomes transparent during OAM composition.
Add-SpiritsGraveInteractionVisual 'colored-cube' 0x19 5 (0..29) -sourceGrayscaleInverted $false
Add-SpiritsGraveInteractionVisual 'cube-flame' 0x1a 0 @(0)

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
[IO.File]::WriteAllBytes(
    (Join-Path $destination 'objects\spirits_grave_cube_palettes.bin'),
    $cubePaletteBytes)

# D1's @essenceOamData row adds tile 0, palette 1, and chooses layout/animation 1.
Add-SpiritsGraveInteractionVisual 'eternal-spirit' 0x7f 0 @(1) 0 1
Add-SpiritsGraveInteractionVisual 'essence-pedestal' 0x7f 1 @(0)
Add-SpiritsGraveInteractionVisual 'essence-glow' 0x7f 2 @(0)

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
function Resolve-SpiritsGraveEnergyAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:partAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<offset>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $offset = [Convert]::ToInt32($frame.Groups['offset'].Value, 16)
        $pointerIndex = [int]($offset / 2)
        if ($pointerIndex -ge $script:energyOamLabels.Count) {
            throw "$label references missing energy-bead OAM pointer $pointerIndex."
        }
        $frames.Add("$duration@$(Resolve-Oam $script:partOamSource $script:energyOamLabels[$pointerIndex])")
        if ([Convert]::ToInt32($frame.Groups['parameter'].Value, 16) -band 0x80) {
            break
        }
    }
    if ($frames.Count -eq 0) { throw "$label has no energy-bead frames." }
    return $frames -join '|'
}
$energyAnimations = @($energyAnimationLabels[0..7] | ForEach-Object {
    Resolve-SpiritsGraveEnergyAnimation $_
})
$energySprite = $gfxNames[0x87]
Copy-SpiritsGraveSprite $energySprite
$energyAnimationData = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($energyAnimations -join "`n"))
$sgVisualRows.Add("energy-bead`t$energySprite`t0`t4`t1`t$energyAnimationData")

# PART_MOBLIN_BOOMERANG $21 uses gfx $8e with tile base $0a and palette 4.
# Its four OAM frames rotate the projectile; retain the source animation rather
# than drawing a runtime approximation.
$boomerangAnimationLabels = @([regex]::Matches(
    (Get-AssemblyLabelBody $partAnimationSource 'part21Animations'),
    '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
$boomerangOamLabels = @([regex]::Matches(
    (Get-AssemblyLabelBody $partAnimationSource 'part21OamDataPointers'),
    '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if ($boomerangAnimationLabels.Count -ne 1 -or $boomerangOamLabels.Count -ne 4) {
    throw 'PART_MOBLIN_BOOMERANG animation/OAM tables changed.'
}
$boomerangFrames = [Collections.Generic.List[string]]::new()
foreach ($frame in [regex]::Matches(
    (Get-AssemblyLabelBody $partAnimationSource $boomerangAnimationLabels[0]),
    '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<offset>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')) {
    $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
    $offset = [Convert]::ToInt32($frame.Groups['offset'].Value, 16)
    $pointerIndex = [int]($offset / 2)
    if ($pointerIndex -ge $boomerangOamLabels.Count) {
        throw "Moblin boomerang OAM pointer $pointerIndex is out of range."
    }
    $boomerangFrames.Add(
        "$duration@$(Resolve-Oam $partOamSource $boomerangOamLabels[$pointerIndex])")
}
if ($boomerangFrames.Count -ne 4) {
    throw 'PART_MOBLIN_BOOMERANG must retain four animation frames.'
}
$boomerangSprite = $gfxNames[0x8e]
Copy-SpiritsGraveSprite $boomerangSprite
$boomerangAnimationData = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($boomerangFrames -join '|'))
$sgVisualRows.Add("moblin-boomerang`t$boomerangSprite`t10`t4`t1`t$boomerangAnimationData")

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
Copy-SpiritsGraveSprite $pumpkinProjectileSprite
$pumpkinProjectileAnimationData = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($pumpkinProjectileFrames -join '|'))
$sgVisualRows.Add(
    "pumpkin-projectile`t$pumpkinProjectileSprite`t30`t2`t1`t$pumpkinProjectileAnimationData")
if ($sgVisualRows.Count -ne 11) { throw "Expected ten Spirit's Grave interaction visuals." }
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\spirits_grave_visuals.tsv'),
    $sgVisualRows,
    [Text.UTF8Encoding]::new($false))

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
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\spirits_grave_objects.tsv'),
    $sgObjectRows,
    [Text.UTF8Encoding]::new($false))

$movingPlatformSource = Get-Content -Raw (
    Join-Path $Disassembly 'object_code\common\interactions\movingPlatform.s')
$linkCommonSource = Get-Content -Raw (
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

$sgConstantsRows = [Collections.Generic.List[string]]::new()
$sgConstantsRows.Add("# key`tvalue")
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
foreach ($row in @(
    "platform-speed-raw`t80"
    "platform-wait`t8"
    "cube-push-frames`t20"
    "cube-hole-frames`t10"
    "moving-platform-spawn-wait`t30"
    "miniboss-reward-wait`t20"
    "torch-count`t2"
    "torch-tile`t45"
    "solve-sound`t77"
    "move-block-sound`t127"
    "light-torch-sound`t114"
    "pumpkin-body-palette`t$([Convert]::ToInt32($pumpkinBodyPalette.Groups['value'].Value, 16) -band 7)"
    "pumpkin-ghost-palette`t$([Convert]::ToInt32($pumpkinGhostPalette.Groups['value'].Value, 16) -band 7)"
)) {
    $sgConstantsRows.Add($row)
}
for ($size = 0; $size -lt $platformRadiusMatches.Count; $size++) {
    $radius = $platformRadiusMatches[$size]
    $sgConstantsRows.Add(
        "platform-radius-$size-y`t$([Convert]::ToInt32($radius.Groups['y'].Value, 16))")
    $sgConstantsRows.Add(
        "platform-radius-$size-x`t$([Convert]::ToInt32($radius.Groups['x'].Value, 16))")
}
if ($sgConstantsRows.Count -ne 26 -or
    -not $sgConstantsRows.Contains("pumpkin-body-palette`t1") -or
    -not $sgConstantsRows.Contains("pumpkin-ghost-palette`t5") -or
    -not $sgConstantsRows.Contains("platform-radius-1-y`t16") -or
    -not $sgConstantsRows.Contains("platform-radius-1-x`t8") -or
    -not $sgConstantsRows.Contains("platform-radius-5-y`t16") -or
    -not $sgConstantsRows.Contains("platform-radius-5-x`t16")) {
    throw 'Expected all six moving-platform collision-radius pairs.'
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\spirits_grave_constants.tsv'),
    $sgConstantsRows,
    [Text.UTF8Encoding]::new($false))

if (-not $allTexts.ContainsKey(0x000e)) {
    throw 'Spirit''s Grave Eternal Spirit text TX_000e was not imported.'
}
$sgTextRows = @(
    "# text-id`tmessage-base64"
    "000e`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x000e])))"
)
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\spirits_grave_text.tsv'),
    $sgTextRows,
    [Text.UTF8Encoding]::new($false))
