# Keese are the first supported enemy. Their room records use random-position
# enemy opcodes, while their attributes, animations, OAM, and graphics are in
# the shared enemy tables. Export the resolved values so runtime code never
# reparses assembly source.
$enemyDataPath = Join-Path $Disassembly "data\ages\enemyData.s"
$enemyDataSource = Read-ImportText $enemyDataPath
$enemyDataRows = @{}
foreach ($node in Read-AssemblyMacroInvocations $enemyDataPath '' 'm_EnemyData') {
    if ($node.Comment -match '^0x(?<id>[0-9a-f]{2})$') {
        $enemyDataRows[[Convert]::ToInt32($Matches['id'], 16)] = $node
    }
}
$extraEnemyRows = @(Read-AssemblyDataDirectives `
    $enemyDataPath 'extraEnemyData' '.db' | ForEach-Object {
        if ($_.Operands.Count -lt 4) { throw 'Malformed extra-enemy data row.' }
        [pscustomobject]@{
            RadiusY = Convert-AssemblyInteger $_.Operands[0]
            RadiusX = Convert-AssemblyInteger $_.Operands[1]
            Damage = Convert-AssemblyInteger $_.Operands[2]
            Health = Convert-AssemblyInteger $_.Operands[3]
        }
    })
if ($enemyDataRows.Count -ne 0x80 -or $extraEnemyRows.Count -eq 0) {
    throw 'Enemy data tables are incomplete.'
}

# Preserve the common side-view terrain-probe stream used by ordinary enemy
# movement, screen-boundary reflection, and every common knockback path. Each
# signed Y/X pair is cumulative: ecom_getAdjacentWallsBitset updates b/c after
# every probe rather than applying four absolute offsets from the enemy.
$enemyCommonCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\commonCode.s')
$sideviewOffsetBody = Get-AssemblyLabelBody `
    $enemyCommonCodeSource 'ecom_sideviewAdjacentWallOffsetTable'
$sideviewOffsetMatches = @([regex]::Matches(
    $sideviewOffsetBody,
    '(?m)^\s*\.db\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})'))
if ($sideviewOffsetMatches.Count -ne 32 -or
    $enemyCommonCodeSource -notmatch
        '(?ms)^ecom_getAdjacentWallsBitset:.*?^@checkCollisionAt:\s*\r?\n\s*ld a,\(de\)\s*\r?\n\s*inc de\s*\r?\n\s*add b\s*\r?\n\s*ld b,a\s*\r?\n\s*ld a,\(de\)\s*\r?\n\s*inc de\s*\r?\n\s*add c\s*\r?\n\s*ld c,a') {
    throw 'ecom_sideviewAdjacentWallOffsetTable or its cumulative probe loop changed.'
}
$sideviewOffsetRows = [Collections.Generic.List[string]]::new()
$sideviewOffsetRows.Add(
    "# octant`tprobe`ty-delta`tx-delta`tsource")
for ($index = 0; $index -lt $sideviewOffsetMatches.Count; $index++) {
    $rawY = [Convert]::ToInt32(
        $sideviewOffsetMatches[$index].Groups['y'].Value, 16)
    $rawX = [Convert]::ToInt32(
        $sideviewOffsetMatches[$index].Groups['x'].Value, 16)
    $y = if ($rawY -ge 0x80) { $rawY - 0x100 } else { $rawY }
    $x = if ($rawX -ge 0x80) { $rawX - 0x100 } else { $rawX }
    $octant = [int][Math]::Floor($index / 4)
    $probe = $index % 4
    $sourceOffset = $index * 2
    $sideviewOffsetRows.Add(
        "$octant`t$probe`t$y`t$x`t" +
        "object_code/common/enemies/commonCode.s:" +
        "ecom_sideviewAdjacentWallOffsetTable+$($sourceOffset.ToString('x2'))")
}
$expectedFirstSideviewOffset =
    "0`t0`t-4`t-5`tobject_code/common/enemies/commonCode.s:" +
    "ecom_sideviewAdjacentWallOffsetTable+00"
$expectedLastSideviewOffset =
    "7`t3`t6`t0`tobject_code/common/enemies/commonCode.s:" +
    "ecom_sideviewAdjacentWallOffsetTable+3e"
if ($sideviewOffsetRows[1] -ne $expectedFirstSideviewOffset -or
    $sideviewOffsetRows[32] -ne $expectedLastSideviewOffset) {
    throw 'Side-view adjacent-wall offset ordering or signed decoding changed.'
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\enemy_adjacent_wall_offsets.tsv'),
    $sideviewOffsetRows)

# Spiny Beetles use the stricter top-down probe table for their covered charge.
# Keep it separate from the side-view table so existing common knockback callers
# cannot silently select the wrong geometry.
$topDownOffsetBody = Get-AssemblyLabelBody `
    $enemyCommonCodeSource 'ecom_topDownAdjacentWallOffsetTable'
$topDownOffsetMatches = @([regex]::Matches(
    $topDownOffsetBody,
    '(?m)^\s*\.db\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})'))
if ($topDownOffsetMatches.Count -ne 32) {
    throw 'ecom_topDownAdjacentWallOffsetTable changed.'
}
$topDownOffsetRows = [Collections.Generic.List[string]]::new()
$topDownOffsetRows.Add(
    "# octant`tprobe`ty-delta`tx-delta`tsource")
for ($index = 0; $index -lt $topDownOffsetMatches.Count; $index++) {
    $rawY = [Convert]::ToInt32(
        $topDownOffsetMatches[$index].Groups['y'].Value, 16)
    $rawX = [Convert]::ToInt32(
        $topDownOffsetMatches[$index].Groups['x'].Value, 16)
    $y = if ($rawY -ge 0x80) { $rawY - 0x100 } else { $rawY }
    $x = if ($rawX -ge 0x80) { $rawX - 0x100 } else { $rawX }
    $octant = [int][Math]::Floor($index / 4)
    $probe = $index % 4
    $sourceOffset = $index * 2
    $topDownOffsetRows.Add(
        "$octant`t$probe`t$y`t$x`t" +
        "object_code/common/enemies/commonCode.s:" +
        "ecom_topDownAdjacentWallOffsetTable+$($sourceOffset.ToString('x2'))")
}
$expectedFirstTopDownOffset =
    "0`t0`t-9`t-6`tobject_code/common/enemies/commonCode.s:" +
    "ecom_topDownAdjacentWallOffsetTable+00"
$expectedLastTopDownOffset =
    "7`t3`t10`t0`tobject_code/common/enemies/commonCode.s:" +
    "ecom_topDownAdjacentWallOffsetTable+3e"
if ($topDownOffsetRows[1] -ne $expectedFirstTopDownOffset -or
    $topDownOffsetRows[32] -ne $expectedLastTopDownOffset) {
    throw 'Top-down adjacent-wall offset ordering or signed decoding changed.'
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\enemy_topdown_adjacent_wall_offsets.tsv'),
    $topDownOffsetRows)

$bounceAngleBlock = [regex]::Match(
    $enemyCommonCodeSource,
    '(?ms)^ecom_bounceOffScreenBoundary:.*?^@angleTable:\s*\r?\n(?<body>.*?)(?=^;;)')
$bounceAngleMatches = @([regex]::Matches(
    $bounceAngleBlock.Groups['body'].Value,
    '\$(?<angle>[0-9a-f]{2})'))
if (-not $bounceAngleBlock.Success -or
    $bounceAngleMatches.Count -ne 48) {
    throw 'ecom_bounceOffScreenBoundary@angleTable no longer contains 48 angles.'
}
$bounceAngleRows = [Collections.Generic.List[string]]::new()
$bounceAngleRows.Add("# index`tangle`tsource")
for ($index = 0; $index -lt $bounceAngleMatches.Count; $index++) {
    $angle = [Convert]::ToInt32(
        $bounceAngleMatches[$index].Groups['angle'].Value, 16)
    $bounceAngleRows.Add(
        "$index`t$($angle.ToString('x2'))`t" +
        "object_code/common/enemies/commonCode.s:" +
        "ecom_bounceOffScreenBoundary@angleTable+$($index.ToString('x2'))")
}
if ($bounceAngleRows[1] -notmatch "^0`t10`t" -or
    $bounceAngleRows[17] -notmatch "^16`t00`t" -or
    $bounceAngleRows[39] -notmatch "^38`t09`t" -or
    $bounceAngleRows[48] -notmatch "^47`t01`t") {
    throw 'Enemy bounce-angle table ordering changed.'
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\enemy_bounce_angles.tsv'),
    $bounceAngleRows)

function Resolve-Oam([string]$source, [string]$label) {
    $path = Resolve-AssemblySourceTextPath $source
    if ($null -eq $path) { throw "OAM source for $label is untracked." }
    $rows = @(Read-AssemblyDataDirectives $path $label '.db')
    if ($rows.Count -eq 0 -or $rows[0].Operands.Count -eq 0) {
        throw "OAM count missing for $label."
    }
    $count = Convert-AssemblyInteger $rows[0].Operands[0]
    $parts = @($rows | Select-Object -Skip 1 | Where-Object {
        $_.Operands.Count -ge 4
    } | ForEach-Object {
        (($_.Operands | Select-Object -First 4 | ForEach-Object {
            Convert-AssemblyInteger $_
        }) -join ',')
    })
    if ($parts.Count -ne $count) {
        throw "$label declares $count OAM parts but contains $($parts.Count)."
    }
    return $parts -join ';'
}

$enemyAnimationPath = Join-Path $Disassembly "data\ages\enemyAnimations.s"
$enemyAnimationSource = Read-ImportText $enemyAnimationPath
$enemyOamSource = Read-ImportText (Join-Path $Disassembly "data\ages\enemyOamData.s")

$enemyAnimationTables = Read-AssemblyDwTables `
    $enemyAnimationPath 'enemy[0-9a-f]{2}Animations' 'enemyAnimation[0-9a-f]+'
$enemyOamTables = Read-AssemblyDwTables `
    $enemyAnimationPath 'enemy[0-9a-f]{2}OamDataPointers' 'enemyOamData[0-9a-f]+'
$enemyAnimationFrames = Read-AssemblyAnimationDefinitions `
    $enemyAnimationPath 'enemyAnimation[0-9a-f]+(?:Loop)?' $true

function Resolve-EnemyAnimations(
    [int]$id,
    [bool]$includeZeroParameters = $false
) {
    $hex = $id.ToString('x2')
    $animationKey = "enemy${hex}Animations"
    $oamKey = "enemy${hex}OamDataPointers"
    if (-not $enemyAnimationTables.ContainsKey($animationKey) -or
        -not $enemyOamTables.ContainsKey($oamKey)) {
        throw "Enemy `$$hex animation/OAM table is missing."
    }
    $pointers = $enemyOamTables[$oamKey]
    $encoded = [Collections.Generic.List[string]]::new()
    foreach ($animationLabel in $enemyAnimationTables[$animationKey]) {
        if (-not $enemyAnimationFrames.ContainsKey($animationLabel)) {
            throw "Enemy `$$hex animation body is missing: $animationLabel"
        }
        $definition = $enemyAnimationFrames[$animationLabel]
        $frames = [Collections.Generic.List[string]]::new()
        $valid = $true
        foreach ($frame in $definition.Frames) {
            $pointerIndex = [int]($frame.PointerOffset / 2)
            if ($pointerIndex -lt 0 -or $pointerIndex -ge $pointers.Count) {
                # Some alias animation tables deliberately continue into the
                # next enemy's entries. Keep only the prefix addressable by
                # this enemy's own OAM pointer table.
                $valid = $false
                break
            }
            $metadata = if ($includeZeroParameters -or $frame.Parameter -ne 0) {
                "$($frame.Duration),$($frame.Parameter)"
            } else {
                "$($frame.Duration)"
            }
            $frames.Add(
                "$metadata@$(Resolve-Oam $enemyOamSource $pointers[$pointerIndex])")
        }
        if (-not $valid) { break }
        $value = $frames -join '|'
        if ($definition.LoopStart -gt 0) {
            $value += "~$($definition.LoopStart)"
        }
        $encoded.Add($value)
    }
    return @($encoded)
}

function Get-EnemyDefinition([int]$id, [int]$subid = 0) {
    $hex = $id.ToString('x2')
    if (-not $enemyDataRows.ContainsKey($id)) {
        throw "Enemy data row `$$hex is missing."
    }
    $row = $enemyDataRows[$id]
    if ($row.Operands.Count -lt 4) {
        $subidTable = $row.Operands[2]
        $rows = @(Read-AssemblyMacroInvocations `
            $enemyDataPath $subidTable 'm_EnemySubidData')
        if ($subid -ge $rows.Count) {
            throw "Enemy `$$hex subid `$$($subid.ToString('x2')) has no data row."
        }
        $extra = (Convert-AssemblyInteger $rows[$subid].Operands[0]) -band 0x7f
        $flags = Convert-AssemblyInteger $rows[$subid].Operands[1]
    } else {
        $extra = (Convert-AssemblyInteger $row.Operands[2]) -band 0x7f
        $flags = Convert-AssemblyInteger $row.Operands[3]
    }
    if ($extra -ge $extraEnemyRows.Count) {
        throw "Enemy `$$hex extra-data index is out of range."
    }
    $extraRow = $extraEnemyRows[$extra]
    $damage = (0x100 - $extraRow.Damage) / 2
    return @{
        Id = $id
        Gfx = Convert-AssemblyInteger $row.Operands[0]
        Collision = Convert-AssemblyInteger $row.Operands[1]
        ExtraIndex = $extra
        GraphicFlags = $flags
        TileBase = ($flags -band 0x0f) * 2
        Palette = ($flags -shr 4) -band 7
        RadiusY = $extraRow.RadiusY
        RadiusX = $extraRow.RadiusX
        Damage = $damage
        DamageQuarters = $damage
        Health = $extraRow.Health
        Animations = Resolve-EnemyAnimations $id
    }
}

$keeseDefinition = Get-EnemyDefinition 0x32
$keeseGfx = $keeseDefinition.Gfx
$keeseGraphicFlags = $keeseDefinition.GraphicFlags
$keeseRadiusY = $keeseDefinition.RadiusY
$keeseRadiusX = $keeseDefinition.RadiusX
$keeseDamageQuarters = $keeseDefinition.Damage
$keeseHealth = $keeseDefinition.Health
if ($keeseGfx -ne 0x9d -or
    ($keeseDefinition.Collision -band 0x7f) -ne 0x1f -or
    $keeseDefinition.ExtraIndex -ne 0x07 -or
    $keeseRadiusY -ne 4 -or $keeseRadiusX -ne 6 -or
    $keeseDamageQuarters -ne 2 -or $keeseHealth -ne 1) {
    throw 'ENEMY_KEESE data no longer matches its traced definition.'
}

function Find-EnemySpriteSource([string]$name) {
    $source = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
        ForEach-Object {
            Get-ChildItem $_.FullName -Recurse -File -Filter "$name.png"
        } |
        Select-Object -First 1
    if ($null -eq $source) {
        throw "Enemy sprite not found: $name.png"
    }
    return $source
}

function Copy-EnemySprite([string]$name) {
    $source = Find-EnemySpriteSource $name
    Copy-Item -LiteralPath $source.FullName `
        -Destination (Join-Path $destination "gfx\$name.png") -Force
}

function Get-EnemySpriteSourceGrayscaleInverted([string]$name) {
    $source = Find-EnemySpriteSource $name
    # tools/gfx/gfx.py defaults spr_* sheets to invert=true, but a sibling
    # .properties file can deliberately reverse the four source color IDs.
    $inverted = $source.BaseName.StartsWith(
        'spr_', [StringComparison]::Ordinal)
    $propertiesPath = [IO.Path]::ChangeExtension(
        $source.FullName, '.properties')
    if (Test-Path -LiteralPath $propertiesPath) {
        $properties = Read-ImportText $propertiesPath
        $invertMatch = [regex]::Match(
            $properties,
            '(?im)^\s*invert:\s*(?<value>\S+)\s*$')
        if ($invertMatch.Success) {
            $value = $invertMatch.Groups['value'].Value
            if (-not [bool]::TryParse(
                    $value, [ref]$inverted)) {
                throw "Enemy sprite $name has invalid invert property: $value"
            }
        }
    }
    return $inverted
}

$commonEnemySprites = @{
    0x0a = @($gfxNames[0x91])
    0x0b = @($gfxNames[0x8f])
    0x0c = @($gfxNames[0x91])
    0x10 = @($gfxNames[0x9b])
    0x14 = @($gfxNames[0x8c])
    0x17 = @($gfxNames[0x90])
    0x1a = @($gfxNames[0x94])
    0x1b = @($gfxNames[0x94])
    0x23 = @($gfxNames[0x8c])
    0x28 = @($gfxNames[0xa0])
    0x4d = @($gfxNames[0x8c])
}
$commonEnemySpecs = @(
    @(0x0a, 0x00), @(0x0b, 0x00), @(0x0c, 0x00),
    @(0x10, 0x00), @(0x13, 0x00),
    @(0x14, 0x00), @(0x17, 0x00), @(0x19, 0x00), @(0x1b, 0x01),
    @(0x1a, 0x00), @(0x22, 0x00), @(0x23, 0x00), @(0x28, 0x00), @(0x33, 0x00),
    @(0x2f, 0x00), @(0x36, 0x00), @(0x3b, 0x00), @(0x3e, 0x00), @(0x47, 0x00), @(0x49, 0x00),
    @(0x4a, 0x01), @(0x4d, 0x00), @(0x4f, 0x00)
)
$commonEnemyRows = [Collections.Generic.List[string]]::new()
$commonEnemyRows.Add(
    '# id`tsubid`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tanimations-base64'.Replace(
        '`t', "`t"))
foreach ($spec in $commonEnemySpecs) {
    $id = [int]$spec[0]
    $subid = [int]$spec[1]
    $definition = Get-EnemyDefinition $id $subid
    $sprites = $commonEnemySprites[$id]
    if ($null -eq $sprites) {
        $sprites = @($gfxNames[$definition.Gfx])
    }
    foreach ($sprite in $sprites) { Copy-EnemySprite $sprite }
    $sourceInversions = @($sprites | ForEach-Object {
        Get-EnemySpriteSourceGrayscaleInverted $_
    } | Select-Object -Unique)
    if ($sourceInversions.Count -ne 1) {
        throw "Enemy `$$($id.ToString('x2')) combines sprite sheets with " +
            "different grayscale inversion properties."
    }
    $sourceGrayscaleInverted = if ($sourceInversions[0]) { 1 } else { 0 }
    $animations = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes(
            $definition.Animations -join "`n"))
    $commonEnemyRows.Add(
        "$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($sprites -join ',')`t$($definition.TileBase)`t$($definition.Palette)`t$sourceGrayscaleInverted`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.Damage)`t$($definition.Health)`t$animations")
}
if ($commonEnemyRows.Count -ne 24 -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^0a\t00\tspr_moblin\t0\t2\t1\t6\t6\t2\t3\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^0c\t00\tspr_moblin\t0\t2\t1\t6\t6\t2\t3\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^0b\t00\tspr_octorok_leever_tektite_zora\t14\t2\t1\t6\t6\t2\t3\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^10\t00\tspr_gibdo_stalfos_rope_whisp_spark_bubble_beetle\t12\t0\t1\t6\t6\t2\t2\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^14\t00\tspr_polsvoice_hardhatbeetle_spikedbeetle_beamon\t8\t1\t1\t6\t6\t2\t2\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^17\t00\tspr_moblin_ghini\t22\t2\t1\t6\t6\t2\t10\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^1b\t01\tspr_crab_fish_goponga_beetle\t24\t2\t1\t6\t6\t2\t2\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^1a\t00\tspr_crab_fish_goponga_beetle\t0\t3\t1\t6\t6\t2\t2\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^23\t00\tspr_polsvoice_hardhatbeetle_spikedbeetle_beamon\t0\t3\t1\t6\t6\t2\t3\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^28\t00\tspr_ironmask\t24\t2\t1\t6\t6\t2\t5\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^33\t00\tspr_chickens_dog_forestfairy_other\t14\t3\t1\t6\t6\t128\t127\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^36\t00\tspr_chickens_dog_forestfairy_other\t0\t2\t1\t6\t6\t128\t32\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^3b\t00\tspr_giantcucco\t0\t2\t1\t7\t12\t2\t2\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^2f\t00\tspr_thwomps\t0\t4\t0\t15\t12\t4\t127\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^4d\t00\tspr_polsvoice_hardhatbeetle_spikedbeetle_beamon\t4\t3\t1\t6\t6\t2\t4\t'
    }) -or
    -not ($commonEnemyRows | Where-Object {
        $_ -match '^4f\t00\tspr_zol_peahat_watertektite_moldorm_gel\t14\t0\t1\t6\t6\t2\t8\t'
    }) -or
    ($commonEnemyRows | Where-Object {
        $_ -match '^(13|19|22|2f|3e|47|49|4a)\t'
    }).Count -ne 8
    ) {
    throw "Common enemy definitions no longer match the traced records:`n$($commonEnemyRows -join "`n")"
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\common_enemies.tsv'),
    $commonEnemyRows)

# ENEMY_VINE_SPROUT (`$62) is a globally dispatched enemy even though its
# five placements live directly in mainData.s rather than enemyData.s. Its
# source coordinates are zero because state 0 loads the live packed position
# from wVinePositions; preserve that six-byte position table and the shared
# enemy visual as one typed contract.
$vineDefinition = Get-EnemyDefinition 0x62 0
$vineSprite = $gfxNames[$vineDefinition.Gfx]
$vineSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\enemies\vineSprout.s')
$vineDefaultSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\defaultVinePositions.s')
$vineDefaults = @([regex]::Matches(
    $vineDefaultSource,
    '\$(?<position>[0-9a-f]{2})') | ForEach-Object {
        $_.Groups['position'].Value
    })
if ($vineDefinition.Gfx -ne 0x6b -or
    $vineDefinition.Collision -ne 0x90 -or
    $vineDefinition.GraphicFlags -ne 0x09 -or
    $vineDefinition.TileBase -ne 0x12 -or
    $vineDefinition.Palette -ne 0 -or
    $vineDefinition.Animations.Count -lt 1 -or
    $vineDefaults.Count -ne 6 -or
    ($vineDefaults -join ' ') -ne '41 22 16 35 18 53' -or
    $vineSource -notmatch
        '(?ms)^vineSprout_state0:.*?ld \(hl\),20.*?ld \(hl\),SPEED_c0.*?vineSprout_getPosition' -or
    $vineSource -notmatch
        '(?ms)^vineSprout_state1:.*?All the above must hold for 20 frames.*?ld \(hl\),\$16.*?SND_MOVEBLOCK' -or
    $vineSource -notmatch
        '(?ms)^vineSprout_linkJumpingDownCliff:.*?vineSprout_restoreTileAtPosition.*?vineSprout_checkLinkInSprout.*?ret nc.*?SpecialObject\.zh.*?add \$03.*?ret nc.*?^vineSprout_destroy:.*?INTERAC_ROCKDEBRIS.*?objectCreateInteractionWithSubid00.*?vineSprout_getDefaultPosition.*?wVinePositions.*?enemyDelete' -or
    $vineSource -notmatch
        '(?ms)^vineSprout_checkLinkInSprout:.*?sub \(hl\)\s+add \$06\s+cp \$0d\s+ret nc.*?sub \(hl\)\s+add \$06\s+cp \$0d\s+ret' -or
    $vineSource -notmatch
        '(?ms)^vineSprout_updateTileAtPosition:.*?ld \(hl\),\$0f.*?ld \(hl\),TILEINDEX_00.*?wVinePositions') {
    throw 'ENEMY_VINE_SPROUT source contract changed.'
}
Copy-EnemySprite $vineSprite
$vineSourceGrayscaleInverted = if (
    Get-EnemySpriteSourceGrayscaleInverted $vineSprite) { 1 } else { 0 }
$vineAnimation = $vineDefinition.Animations[0]
$vineRows = [Collections.Generic.List[string]]::new()
$vineRows.Add(
    '# subid`tdefault-position`tsprite`ttile-base`tpalette`tsource-grayscale-inverted`tanimation`tspeed-raw`tpush-delay`tmove-frames`tcliff-overlap-radius`tcliff-ground-proximity`tcliff-debris-interaction`tsource'.Replace(
        '`t', "`t"))
for ($subid = 0; $subid -lt $vineDefaults.Count; $subid++) {
    $vineRows.Add(
        "$($subid.ToString('x2'))`t$($vineDefaults[$subid])`t$vineSprite`t" +
        "$($vineDefinition.TileBase)`t$($vineDefinition.Palette)`t" +
        "$vineSourceGrayscaleInverted`t$vineAnimation`t" +
        "1e`t20`t22`t6`t3`t06`tobject_code/ages/enemies/vineSprout.s:" +
        "ENEMY_VINE_SPROUT+$($subid.ToString('x2'))")
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\vine_sprouts.tsv'),
    $vineRows)

$wingEnemySources = @{
    spark = Read-ImportText (
        Join-Path $Disassembly 'object_code\common\enemies\spark.s')
    whisp = Read-ImportText (
        Join-Path $Disassembly 'object_code\common\enemies\whisp.s')
    thwomp = Read-ImportText (
        Join-Path $Disassembly 'object_code\common\enemies\thwomp.s')
    peahat = Read-ImportText (
        Join-Path $Disassembly 'object_code\common\enemies\peahat.s')
    sword = Read-ImportText (
        Join-Path $Disassembly 'object_code\common\enemies\swordEnemies.s')
    gel = Read-ImportText (
        Join-Path $Disassembly 'object_code\ages\enemies\colorChangingGel.s')
}
if ($wingEnemySources.spark -notmatch
        '(?ms)spark_state_uninitialized:.*?ld a,SPEED_100.*?spark_state8:.*?spark_updateAngle.*?objectApplySpeed' -or
    $wingEnemySources.whisp -notmatch
        '(?ms)whisp_state_uninitialized:.*?and \$18\s+add \$04.*?ld a,SPEED_c0.*?whisp_state8:.*?ecom_bounceOffWalls' -or
    $wingEnemySources.thwomp -notmatch
        '(?ms)thwomp_state8:.*?add \$14\s+cp \$29.*?thwomp_state9:.*?ld b,\$10\s+ld a,\$30.*?ld \(hl\),60.*?thwomp_stateA:.*?sub \$80.*?ld \(hl\),\$20' -or
    $wingEnemySources.peahat -notmatch
        '(?ms)peahat_state8:.*?ld \(hl\),\$7f.*?SPEED_20.*?peahat_state9:.*?peahat_counter1Vals:.*?\.db 180 180 210 210 240 240 0 0' -or
    $wingEnemySources.sword -notmatch
        '(?ms)swordEnemy_state_uninitialized:.*?SPEED_80.*?swordEnemy_state9:.*?ld \(hl\),\$60.*?SPEED_a0.*?swordEnemy_beginChasingLink:.*?ld \(hl\),\$10.*?@counter2Vals:\s+\.db \$14 \$10 \$0c' -or
    $wingEnemySources.gel -notmatch
        '(?ms)colorChangingGel_state_uninitialized:.*?SPEED_140.*?ld \(hl\),150.*?colorChangingGel_state8:.*?ld \(hl\),60.*?-\$180.*?colorChangingGel_stateA:.*?ld c,\$30.*?ld \(hl\),150.*?ld \(hl\),90') {
    throw 'Wing Dungeon ordinary-enemy handler constants changed.'
}

# ENEMY_COLOR_CHANGING_GEL loads PALH_bf during state 0. That header replaces
# OBJ palette 6 with paletteData4940; red/blue keep the standard OBJ palettes
# 2/1 while the gel writes 6 for its yellow floor color.
$colorChangingGelPaletteHeader = [regex]::Match(
    $paletteHeaderSource,
    '(?ms)^m_PaletteHeaderStart\s+\$bf,\s*PALH_bf(?<body>.*?)(?=^m_PaletteHeaderStart|\z)')
if ($wingEnemySources.gel -notmatch
        '(?ms)^colorChangingGel_state_uninitialized:.*?' +
        'ld a,PALH_bf\s+call loadPaletteHeader' -or
    -not $colorChangingGelPaletteHeader.Success -or
    $colorChangingGelPaletteHeader.Groups['body'].Value -notmatch
        'm_PaletteHeaderSpr\s+6,\s*1,\s*paletteData4940') {
    throw 'ENEMY_COLOR_CHANGING_GEL no longer loads PALH_bf/paletteData4940 into OBJ palette 6.'
}
Write-GeneratedBytes(
    (Join-Path $destination 'objects\color_changing_gel_palette.bin'),
    (Read-PaletteBytes 'paletteData4940' 4))

$keeseAnimations = @($keeseDefinition.Animations)
if ($keeseAnimations.Count -ne 2) {
    throw "Expected two Keese animations, resolved $($keeseAnimations.Count)."
}
$keeseIdleAnimation, $keeseFlyAnimation = $keeseAnimations
if ($keeseIdleAnimation -ne '127@8,4,2,0' -or
    $keeseFlyAnimation -ne '4@8,0,0,0;8,8,0,32|4@8,4,2,0') {
    throw "ENEMY_KEESE animation/OAM data no longer matches the folded/flying records."
}

$keeseRows = [Collections.Generic.List[string]]::new()
$keeseRows.Add("# group`troom`tid`tsubid`tflags`tcount`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tidle-animation`tfly-animation")
$keeseAliases = [Collections.Generic.List[object]]::new()
foreach ($line in Read-ImportLines (Join-Path $Disassembly "objects\ages\enemyData.s")) {
    if ($line -match '^group(?<group>[0-5])Map(?<room>[0-9a-f]{2})EnemyObjectData:') {
        $keeseAliases.Add(@{
            Group = [int]$Matches['group']
            Room = $Matches['room']
        })
        continue
    }
    if ($keeseAliases.Count -eq 0) { continue }
    if ($line -match '^\s*obj_RandomEnemy\s+\$(?<flags>[0-9a-f]{2})\s+\$32\s+\$(?<subid>[0-9a-f]{2})') {
        $flags = [Convert]::ToInt32($Matches['flags'], 16)
        $count = ($flags -shr 5) -band 7
        foreach ($alias in $keeseAliases) {
            $keeseRows.Add(
                "$($alias.Group)`t$($alias.Room)`t32`t$($Matches['subid'])`t$($Matches['flags'])`t$count`t$($gfxNames[$keeseGfx])`t$(($keeseGraphicFlags -band 0x0f) * 2)`t$(($keeseGraphicFlags -shr 4) -band 7)`t$keeseRadiusY`t$keeseRadiusX`t$keeseDamageQuarters`t$keeseHealth`t$keeseIdleAnimation`t$keeseFlyAnimation")
        }
        continue
    }
    if ($line -match '^\s*obj_EndPointer') {
        $keeseAliases.Clear()
        continue
    }
    if ($line -match '^[A-Za-z0-9_@]+:') { $keeseAliases.Clear() }
}
$keeseInstanceCount = ($keeseRows | Select-Object -Skip 1 | ForEach-Object {
    [int](($_ -split "`t")[5])
} | Measure-Object -Sum).Sum
if ($keeseRows.Count -ne 54 -or $keeseInstanceCount -ne 158) {
    throw "Expected 53 Keese room records / 158 instances, parsed $($keeseRows.Count - 1) / $keeseInstanceCount."
}
if (-not ($keeseRows | Where-Object { $_ -match '^4\t39\t32\t01\t40\t2\t' })) {
    throw "Canonical subid-1 Keese room 4:39 was not extracted."
}

$keeseSpriteName = $gfxNames[$keeseGfx]
$keeseSourceSprite = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$keeseSpriteName.png" } |
    Select-Object -First 1
if ($null -eq $keeseSourceSprite) { throw "Keese sprite not found in disassembly: $keeseSpriteName.png" }
Copy-Item -LiteralPath $keeseSourceSprite.FullName -Destination (Join-Path $destination "gfx\$keeseSpriteName.png") -Force
$keeseDefinitionRows = [Collections.Generic.List[string]]::new()
$keeseDefinitionRows.Add(
    "# id`tsubid`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tidle-animation`tfly-animation")
foreach ($subid in @('00', '01')) {
    $keeseDefinitionRows.Add(
        "32`t$subid`t$keeseSpriteName`t$(($keeseGraphicFlags -band 0x0f) * 2)`t$(($keeseGraphicFlags -shr 4) -band 7)`t$keeseRadiusY`t$keeseRadiusX`t$keeseDamageQuarters`t$keeseHealth`t$keeseIdleAnimation`t$keeseFlyAnimation")
}
$keesePath = Join-Path $destination "objects\keese.tsv"

# Octoroks (`$09) use both random-position and fixed-position enemy opcodes.
# Ages room data instantiates subids `$00, `$01, and `$02: normal red, fast
# red, and blue. Export one definition per supported subid with its resolved
# attributes and all four cardinal animations.
$octorokBase = Get-EnemyDefinition 0x09 0
if ($octorokBase.Gfx -ne 0x8f -or $octorokBase.Collision -ne 0x90) {
    throw 'ENEMY_OCTOROK no longer resolves to gfx `$8f / standard collision mode `$10.'
}
$octorokGfx = $octorokBase.Gfx

$octorokAnimations = @(Resolve-EnemyAnimations 0x09)
if ($octorokAnimations.Count -ne 4) {
    throw 'Expected four Octorok animations.'
}
if ($octorokAnimations[0] -ne '8@8,0,0,64;8,8,0,96|8@8,0,2,64;8,8,2,96' -or
    $octorokAnimations[1] -ne '8@8,0,6,32;8,8,4,32|8@8,0,10,32;8,8,8,32' -or
    $octorokAnimations[2] -ne '8@8,0,0,0;8,8,0,32|8@8,0,2,0;8,8,2,32' -or
    $octorokAnimations[3] -ne '8@8,0,4,0;8,8,6,0|8@8,0,8,0;8,8,10,0') {
    throw 'ENEMY_OCTOROK cardinal animation/OAM data no longer matches the original records.'
}

$octorokDefinitions = @{}
foreach ($subid in 0..2) {
    $definition = Get-EnemyDefinition 0x09 $subid
    $definition['SpeedRaw'] =
        if (($subid -band 1) -ne 0) { 0x1e } else { 0x14 }
    $definition['CounterMask'] = if ($subid -lt 2) { 7 } else { 3 }
    $octorokDefinitions[$subid] = $definition
}
if ($octorokDefinitions[0].Health -ne 2 -or
    $octorokDefinitions[0].DamageQuarters -ne 1 -or
    $octorokDefinitions[1].SpeedRaw -ne 0x1e -or
    $octorokDefinitions[2].Health -ne 3 -or
    $octorokDefinitions[2].DamageQuarters -ne 2 -or
    $octorokDefinitions[2].CounterMask -ne 3) {
    throw 'ENEMY_OCTOROK subid attributes no longer match red/fast-red/blue behavior.'
}

$octorokRows = [Collections.Generic.List[string]]::new()
$octorokRows.Add("# group`troom`tid`tsubid`tflags`tcount`tposition-mode`ty`tx`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tspeed-raw`tcounter-mask`tup-animation`tright-animation`tdown-animation`tleft-animation")
$octorokAliases = [Collections.Generic.List[object]]::new()
$octorokLastSpecificFlags = '00'
foreach ($line in Read-ImportLines (Join-Path $Disassembly 'objects\ages\enemyData.s')) {
    if ($line -match '^group(?<group>[0-5])Map(?<room>[0-9a-f]{2})EnemyObjectData:') {
        $octorokAliases.Add(@{ Group = [int]$Matches['group']; Room = $Matches['room'] })
        continue
    }
    if ($octorokAliases.Count -eq 0) { continue }

    if ($line -match '^\s*obj_RandomEnemy\s+\$(?<flags>[0-9a-f]{2})\s+\$09\s+\$(?<subid>[0-9a-f]{2})') {
        $subid = [Convert]::ToInt32($Matches['subid'], 16)
        if (-not $octorokDefinitions.ContainsKey($subid)) {
            throw "Room data uses unsupported ENEMY_OCTOROK subid `$($subid.ToString('x2'))."
        }
        $definition = $octorokDefinitions[$subid]
        $flags = [Convert]::ToInt32($Matches['flags'], 16)
        $count = ($flags -shr 5) -band 7
        foreach ($alias in $octorokAliases) {
            $octorokRows.Add("$($alias.Group)`t$($alias.Room)`t09`t$($Matches['subid'])`t$($Matches['flags'])`t$count`tR`t-1`t-1`t$($gfxNames[$octorokGfx])`t$($definition.TileBase)`t$($definition.Palette)`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.DamageQuarters)`t$($definition.Health)`t$($definition.SpeedRaw)`t$($definition.CounterMask)`t$($octorokAnimations[0])`t$($octorokAnimations[1])`t$($octorokAnimations[2])`t$($octorokAnimations[3])")
        }
        continue
    }

    if ($line -match '^\s*obj_SpecificEnemyA\s+(?<values>(?:\$[0-9a-f]{2}\s*)+)$') {
        $values = @([regex]::Matches($Matches['values'], '\$(?<value>[0-9a-f]{2})') |
            ForEach-Object { $_.Groups['value'].Value })
        if ($values.Count -eq 5) {
            $octorokLastSpecificFlags = $values[0]
            $id, $subidHex, $y, $x = $values[1..4]
        } else {
            $id, $subidHex, $y, $x = $values
        }
        if ($id -eq '09') {
            $subid = [Convert]::ToInt32($subidHex, 16)
            if (-not $octorokDefinitions.ContainsKey($subid)) {
                throw "Room data uses unsupported fixed ENEMY_OCTOROK subid `$subidHex."
            }
            $definition = $octorokDefinitions[$subid]
            foreach ($alias in $octorokAliases) {
                $octorokRows.Add("$($alias.Group)`t$($alias.Room)`t09`t$subidHex`t$octorokLastSpecificFlags`t1`tF`t$y`t$x`t$($gfxNames[$octorokGfx])`t$($definition.TileBase)`t$($definition.Palette)`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.DamageQuarters)`t$($definition.Health)`t$($definition.SpeedRaw)`t$($definition.CounterMask)`t$($octorokAnimations[0])`t$($octorokAnimations[1])`t$($octorokAnimations[2])`t$($octorokAnimations[3])")
            }
        }
        continue
    }

    if ($line -match '^\s*obj_EndPointer' -or $line -match '^[A-Za-z0-9_@]+:') {
        $octorokAliases.Clear()
    }
}
$octorokInstanceCount = ($octorokRows | Select-Object -Skip 1 | ForEach-Object {
    [int](($_ -split "`t")[5])
} | Measure-Object -Sum).Sum
if ($octorokRows.Count -ne 34 -or $octorokInstanceCount -ne 48) {
    throw "Expected 33 Octorok room records / 48 instances, parsed $($octorokRows.Count - 1) / $octorokInstanceCount."
}
if (-not ($octorokRows | Where-Object { $_ -match '^0\t74\t09\t00\t20\t1\tR\t' }) -or
    -not ($octorokRows | Where-Object { $_ -match '^0\t74\t09\t01\t20\t1\tR\t' }) -or
    -not ($octorokRows | Where-Object { $_ -match '^1\tbc\t09\t02\t00\t1\tF\t48\t48\t' })) {
    throw 'Canonical Octorok records in rooms 0:74 and 1:bc were not extracted.'
}

$octorokSpriteName = $gfxNames[$octorokGfx]
$octorokSourceSprite = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$octorokSpriteName.png" } |
    Select-Object -First 1
if ($null -eq $octorokSourceSprite) { throw "Octorok sprite not found: $octorokSpriteName.png" }
Copy-Item -LiteralPath $octorokSourceSprite.FullName -Destination (Join-Path $destination "gfx\$octorokSpriteName.png") -Force
$octorokDefinitionRows = [Collections.Generic.List[string]]::new()
$octorokDefinitionRows.Add(
    "# id`tsubid`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tspeed-raw`tcounter-mask`tup-animation`tright-animation`tdown-animation`tleft-animation")
foreach ($subid in 0..2) {
    $definition = $octorokDefinitions[$subid]
    $octorokDefinitionRows.Add(
        "09`t$($subid.ToString('x2'))`t$octorokSpriteName`t$($definition.TileBase)`t$($definition.Palette)`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.DamageQuarters)`t$($definition.Health)`t$($definition.SpeedRaw)`t$($definition.CounterMask)`t$($octorokAnimations -join "`t")")
}
$octorokPath = Join-Path $destination 'objects\octoroks.tsv'

# ENEMY_STALFOS (`$31) subid `$00 is the ordinary walking Stalfos used by
# room 4:06 and 33 other source records. Other subids add weapon-evasion,
# bone projectiles, or stomp states and remain explicit unsupported variants.
$stalfosDefinition = Get-EnemyDefinition 0x31 0
if ($stalfosDefinition.Gfx -ne 0x9b -or
    ($stalfosDefinition.Collision -band 0x7f) -ne 0x7d) {
    throw 'ENEMY_STALFOS no longer resolves to gfx `$9b / undead collision mode `$7d.'
}
$stalfosGfx = $stalfosDefinition.Gfx

$stalfosAnimations = @(Resolve-EnemyAnimations 0x31)
if ($stalfosAnimations.Count -ne 2) {
    throw 'Expected two Stalfos animations.'
}
if ($stalfosAnimations[0] -ne '4@8,0,0,0;8,8,2,0|4@8,0,2,32;8,8,0,32' -or
    $stalfosAnimations[1] -ne '127@8,0,4,0;8,8,4,32') {
    throw 'ENEMY_STALFOS walk/jump animation OAM no longer matches the original records.'
}

$stalfosDefinition['SpeedRaw'] = 0x14
if ($stalfosDefinition.TileBase -ne 4 -or $stalfosDefinition.Palette -ne 1 -or
    $stalfosDefinition.RadiusY -ne 6 -or $stalfosDefinition.RadiusX -ne 6 -or
    $stalfosDefinition.DamageQuarters -ne 2 -or $stalfosDefinition.Health -ne 2) {
    throw 'ENEMY_STALFOS subid `$00 no longer matches tile base 4, palette 1, radii 6x6, half-heart damage, and two health.'
}

$stalfosRows = [Collections.Generic.List[string]]::new()
$stalfosRows.Add("# group`troom`tid`tsubid`tflags`tcount`tposition-mode`ty`tx`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tspeed-raw`twalk-animation`tjump-animation")
$stalfosAliases = [Collections.Generic.List[object]]::new()
$stalfosLastSpecificFlags = '00'
foreach ($line in Read-ImportLines (Join-Path $Disassembly 'objects\ages\enemyData.s')) {
    if ($line -match '^group(?<group>[0-5])Map(?<room>[0-9a-f]{2})EnemyObjectData:') {
        $stalfosAliases.Add(@{ Group = [int]$Matches['group']; Room = $Matches['room'] })
        continue
    }
    if ($stalfosAliases.Count -eq 0) { continue }

    if ($line -match '^\s*obj_RandomEnemy\s+\$(?<flags>[0-9a-f]{2})\s+\$31\s+\$(?<subid>[0-9a-f]{2})') {
        if ($Matches['subid'] -ne '00') { continue }
        $flags = [Convert]::ToInt32($Matches['flags'], 16)
        $count = ($flags -shr 5) -band 7
        foreach ($alias in $stalfosAliases) {
            $stalfosRows.Add("$($alias.Group)`t$($alias.Room)`t31`t00`t$($Matches['flags'])`t$count`tR`t-1`t-1`t$($gfxNames[$stalfosGfx])`t$($stalfosDefinition.TileBase)`t$($stalfosDefinition.Palette)`t$($stalfosDefinition.RadiusY)`t$($stalfosDefinition.RadiusX)`t$($stalfosDefinition.DamageQuarters)`t$($stalfosDefinition.Health)`t$($stalfosDefinition.SpeedRaw)`t$($stalfosAnimations[0])`t$($stalfosAnimations[1])")
        }
        continue
    }

    if ($line -match '^\s*obj_SpecificEnemyA\s+(?<values>(?:\$[0-9a-f]{2}\s*)+)$') {
        $values = @([regex]::Matches($Matches['values'], '\$(?<value>[0-9a-f]{2})') |
            ForEach-Object { $_.Groups['value'].Value })
        if ($values.Count -eq 5) {
            $stalfosLastSpecificFlags = $values[0]
            $id, $subidHex, $y, $x = $values[1..4]
        } else {
            $id, $subidHex, $y, $x = $values
        }
        if ($id -eq '31' -and $subidHex -eq '00') {
            foreach ($alias in $stalfosAliases) {
                $stalfosRows.Add("$($alias.Group)`t$($alias.Room)`t31`t00`t$stalfosLastSpecificFlags`t1`tF`t$y`t$x`t$($gfxNames[$stalfosGfx])`t$($stalfosDefinition.TileBase)`t$($stalfosDefinition.Palette)`t$($stalfosDefinition.RadiusY)`t$($stalfosDefinition.RadiusX)`t$($stalfosDefinition.DamageQuarters)`t$($stalfosDefinition.Health)`t$($stalfosDefinition.SpeedRaw)`t$($stalfosAnimations[0])`t$($stalfosAnimations[1])")
            }
        }
        continue
    }

    if ($line -match '^\s*obj_EndPointer' -or $line -match '^[A-Za-z0-9_@]+:') {
        $stalfosAliases.Clear()
    }
}
$stalfosInstanceCount = ($stalfosRows | Select-Object -Skip 1 | ForEach-Object {
    [int](($_ -split "`t")[5])
} | Measure-Object -Sum).Sum
if ($stalfosRows.Count -ne 35 -or $stalfosInstanceCount -ne 37) {
    throw "Expected 34 ordinary Stalfos room records / 37 instances, parsed $($stalfosRows.Count - 1) / $stalfosInstanceCount."
}
if (($stalfosRows | Where-Object { $_ -match '^4\t06\t31\t00\t00\t1\tF\t68\t68\t' }).Count -ne 1 -or
    ($stalfosRows | Where-Object { $_ -match '^4\t06\t31\t00\t00\t1\tF\t68\t98\t' }).Count -ne 1) {
    throw 'Canonical room 4:06 Stalfos records at `$68,`$68 and `$68,`$98 were not extracted.'
}

$stalfosSpriteName = $gfxNames[$stalfosGfx]
$stalfosSourceSprite = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$stalfosSpriteName.png" } |
    Select-Object -First 1
if ($null -eq $stalfosSourceSprite) { throw "Stalfos sprite not found: $stalfosSpriteName.png" }
Copy-Item -LiteralPath $stalfosSourceSprite.FullName -Destination (Join-Path $destination "gfx\$stalfosSpriteName.png") -Force
$stalfosDefinitionRows = [Collections.Generic.List[string]]::new()
$stalfosDefinitionRows.Add(
    "# id`tsubid`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tspeed-raw`twalk-animation`tjump-animation")
$stalfosDefinitionRows.Add(
    "31`t00`t$stalfosSpriteName`t$($stalfosDefinition.TileBase)`t$($stalfosDefinition.Palette)`t$($stalfosDefinition.RadiusY)`t$($stalfosDefinition.RadiusX)`t$($stalfosDefinition.DamageQuarters)`t$($stalfosDefinition.Health)`t$($stalfosDefinition.SpeedRaw)`t$($stalfosAnimations -join "`t")")
$stalfosPath = Join-Path $destination 'objects\stalfos.tsv'

# Zols (`$34) are instantiated with both random and fixed-position enemy
# opcodes. Red Zols split into ENEMY_GEL (`$43), which also has one direct
# random-position room record. Export both definitions with animation
# parameters intact: the terminal parameters on Zol animations 0 and 3 drive
# the emerge/disappear state changes.
$zolBase = Get-EnemyDefinition 0x34 0
if ($zolBase.Gfx -ne 0x97 -or $zolBase.Collision -ne 0x29) {
    throw 'ENEMY_ZOL no longer resolves to gfx `$97 / collision mode `$29.'
}
$zolGfx = $zolBase.Gfx
$gelDefinition = Get-EnemyDefinition 0x43 0
if ($gelDefinition.Gfx -ne 0x97 -or $gelDefinition.Collision -ne 0xb3 -or
    $gelDefinition.ExtraIndex -ne 0x06 -or
    $gelDefinition.GraphicFlags -ne 0x20) {
    throw 'ENEMY_GEL no longer resolves to gfx `$97 / collision `$b3 / extra `$06 / flags `$20.'
}

$zolAnimations = @(Resolve-EnemyAnimations 0x34 $true)
$gelAnimations = @(Resolve-EnemyAnimations 0x43 $true)
if ($zolAnimations.Count -ne 6 -or $gelAnimations.Count -ne 3) {
    throw 'Expected six Zol and three Gel animations.'
}
if ($zolAnimations[0] -ne '16,0@12,4,0,0|16,0@8,0,4,0;8,8,4,32|127,1@8,0,2,0;8,8,2,32' -or
    $zolAnimations[3] -ne '8,0@8,0,2,0;8,8,2,32|16,0@8,0,4,0;8,8,4,32|16,0@12,4,0,0|127,1@12,4,0,0' -or
    $gelAnimations[1] -ne '4,0@6,2,0,0|4,0@10,6,0,0|4,0@6,6,0,0|4,0@10,2,0,0') {
    throw "ENEMY_ZOL/ENEMY_GEL animation data no longer matches the original records: z0=$($zolAnimations[0]); z3=$($zolAnimations[3]); g1=$($gelAnimations[1])"
}

$zolDefinitions = @{}
foreach ($subid in 0..1) {
    $zolDefinitions[$subid] = Get-EnemyDefinition 0x34 $subid
}
if ($zolDefinitions[0].Health -ne 2 -or $zolDefinitions[0].Palette -ne 0 -or
    $zolDefinitions[1].Health -ne 3 -or $zolDefinitions[1].Palette -ne 2 -or
    $zolDefinitions[0].DamageQuarters -ne 2 -or
    $zolDefinitions[0].RadiusY -ne 6 -or $zolDefinitions[0].RadiusX -ne 6) {
    throw 'ENEMY_ZOL subid attributes no longer match green/red behavior.'
}

if ($gelDefinition.TileBase -ne 0 -or $gelDefinition.Palette -ne 2 -or
    $gelDefinition.RadiusY -ne 2 -or $gelDefinition.RadiusX -ne 2 -or
    $gelDefinition.DamageQuarters -ne 2 -or $gelDefinition.Health -ne 1) {
    throw 'ENEMY_GEL attributes no longer match radius 2x2, half-heart damage, and one health.'
}

$zolRows = [Collections.Generic.List[string]]::new()
$zolRows.Add("# group`troom`tid`tsubid`tflags`tcount`tposition-mode`ty`tx`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tanimation-0`tanimation-1`tanimation-2`tanimation-3`tanimation-4`tanimation-5")
$gelRows = [Collections.Generic.List[string]]::new()
$gelRows.Add("# group`troom`tid`tsubid`tflags`tcount`tposition-mode`ty`tx`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tanimation-0`tanimation-1`tanimation-2")
$zolAliases = [Collections.Generic.List[object]]::new()
$zolLastSpecificFlags = '00'
foreach ($line in Read-ImportLines (Join-Path $Disassembly 'objects\ages\enemyData.s')) {
    if ($line -match '^group(?<group>[0-5])Map(?<room>[0-9a-f]{2})EnemyObjectData:') {
        $zolAliases.Add(@{ Group = [int]$Matches['group']; Room = $Matches['room'] })
        continue
    }
    if ($zolAliases.Count -eq 0) { continue }

    if ($line -match '^\s*obj_RandomEnemy\s+\$(?<flags>[0-9a-f]{2})\s+\$(?<id>34|43)\s+\$(?<subid>[0-9a-f]{2})') {
        $id = $Matches['id']
        $subid = [Convert]::ToInt32($Matches['subid'], 16)
        $flags = [Convert]::ToInt32($Matches['flags'], 16)
        $count = ($flags -shr 5) -band 7
        foreach ($alias in $zolAliases) {
            if ($id -eq '34') {
                if (-not $zolDefinitions.ContainsKey($subid)) {
                    throw "Room data uses unsupported ENEMY_ZOL subid `$($subid.ToString('x2'))."
                }
                $definition = $zolDefinitions[$subid]
                $zolRows.Add("$($alias.Group)`t$($alias.Room)`t34`t$($Matches['subid'])`t$($Matches['flags'])`t$count`tR`t-1`t-1`t$($gfxNames[$zolGfx])`t$($definition.TileBase)`t$($definition.Palette)`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.DamageQuarters)`t$($definition.Health)`t$($zolAnimations -join "`t")")
            } else {
                if ($subid -ne 0) { throw "Room data uses unsupported ENEMY_GEL subid `$($Matches['subid'])." }
                $gelRows.Add("$($alias.Group)`t$($alias.Room)`t43`t00`t$($Matches['flags'])`t$count`tR`t-1`t-1`t$($gfxNames[$zolGfx])`t$($gelDefinition.TileBase)`t$($gelDefinition.Palette)`t$($gelDefinition.RadiusY)`t$($gelDefinition.RadiusX)`t$($gelDefinition.DamageQuarters)`t$($gelDefinition.Health)`t$($gelAnimations -join "`t")")
            }
        }
        continue
    }

    if ($line -match '^\s*obj_SpecificEnemyA\s+(?<values>(?:\$[0-9a-f]{2}\s*)+)$') {
        $values = @([regex]::Matches($Matches['values'], '\$(?<value>[0-9a-f]{2})') |
            ForEach-Object { $_.Groups['value'].Value })
        if ($values.Count -eq 5) {
            $zolLastSpecificFlags = $values[0]
            $id, $subidHex, $y, $x = $values[1..4]
        } else {
            $id, $subidHex, $y, $x = $values
        }
        if ($id -eq '34') {
            $subid = [Convert]::ToInt32($subidHex, 16)
            if (-not $zolDefinitions.ContainsKey($subid)) {
                throw "Room data uses unsupported fixed ENEMY_ZOL subid `$subidHex."
            }
            $definition = $zolDefinitions[$subid]
            foreach ($alias in $zolAliases) {
                $zolRows.Add("$($alias.Group)`t$($alias.Room)`t34`t$subidHex`t$zolLastSpecificFlags`t1`tF`t$y`t$x`t$($gfxNames[$zolGfx])`t$($definition.TileBase)`t$($definition.Palette)`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.DamageQuarters)`t$($definition.Health)`t$($zolAnimations -join "`t")")
            }
        } elseif ($id -eq '43') {
            if ($subidHex -ne '00') { throw "Room data uses unsupported fixed ENEMY_GEL subid `$subidHex." }
            foreach ($alias in $zolAliases) {
                $gelRows.Add("$($alias.Group)`t$($alias.Room)`t43`t00`t$zolLastSpecificFlags`t1`tF`t$y`t$x`t$($gfxNames[$zolGfx])`t$($gelDefinition.TileBase)`t$($gelDefinition.Palette)`t$($gelDefinition.RadiusY)`t$($gelDefinition.RadiusX)`t$($gelDefinition.DamageQuarters)`t$($gelDefinition.Health)`t$($gelAnimations -join "`t")")
            }
        }
        continue
    }

    if ($line -match '^\s*obj_EndPointer' -or $line -match '^[A-Za-z0-9_@]+:') {
        $zolAliases.Clear()
    }
}
$zolInstanceCount = ($zolRows | Select-Object -Skip 1 | ForEach-Object {
    [int](($_ -split "`t")[5])
} | Measure-Object -Sum).Sum
$gelInstanceCount = ($gelRows | Select-Object -Skip 1 | ForEach-Object {
    [int](($_ -split "`t")[5])
} | Measure-Object -Sum).Sum
if ($zolRows.Count -ne 62 -or $zolInstanceCount -ne 79) {
    throw "Expected 61 Zol room records / 79 instances, parsed $($zolRows.Count - 1) / $zolInstanceCount."
}
if ($gelRows.Count -ne 2 -or $gelInstanceCount -ne 3) {
    throw "Expected one direct Gel room record / 3 instances, parsed $($gelRows.Count - 1) / $gelInstanceCount."
}
if (($zolRows | Where-Object { $_ -match '^4\tcc\t34\t00\t00\t1\tF\t78\t58\t' }).Count -ne 1 -or
    ($zolRows | Where-Object { $_ -match '^4\tcc\t34\t01\t00\t1\tF\t98\t48\t' }).Count -ne 1) {
    throw 'Canonical room 4:cc green/red Zol records were not extracted.'
}

$zolSpriteName = $gfxNames[$zolGfx]
$zolSourceSprite = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$zolSpriteName.png" } |
    Select-Object -First 1
if ($null -eq $zolSourceSprite) { throw "Zol/Gel sprite not found: $zolSpriteName.png" }
Copy-Item -LiteralPath $zolSourceSprite.FullName -Destination (Join-Path $destination "gfx\$zolSpriteName.png") -Force
$zolDefinitionRows = [Collections.Generic.List[string]]::new()
$zolDefinitionRows.Add(
    "# id`tsubid`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tanimation-0`tanimation-1`tanimation-2`tanimation-3`tanimation-4`tanimation-5")
foreach ($subid in 0..1) {
    $definition = $zolDefinitions[$subid]
    $zolDefinitionRows.Add(
        "34`t$($subid.ToString('x2'))`t$zolSpriteName`t$($definition.TileBase)`t$($definition.Palette)`t$($definition.RadiusY)`t$($definition.RadiusX)`t$($definition.DamageQuarters)`t$($definition.Health)`t$($zolAnimations -join "`t")")
}
$gelDefinitionRows = [Collections.Generic.List[string]]::new()
$gelDefinitionRows.Add(
    "# id`tsubid`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tanimation-0`tanimation-1`tanimation-2")
$gelDefinitionRows.Add(
    "43`t00`t$zolSpriteName`t$($gelDefinition.TileBase)`t$($gelDefinition.Palette)`t$($gelDefinition.RadiusY)`t$($gelDefinition.RadiusX)`t$($gelDefinition.DamageQuarters)`t$($gelDefinition.Health)`t$($gelAnimations -join "`t")")

# Perched Crows (`$41:$00) are fixed-position enemies. Their shared graphics
# header, standard attributes, and four directional/flight animations are
# resolved here; the off-screen flock subid `$01 remains outside this slice.
$crowDefinition = Get-EnemyDefinition 0x41 0
if ($crowDefinition.Gfx -ne 0x93 -or $crowDefinition.Collision -ne 0x31 -or
    $crowDefinition.ExtraIndex -ne 0x3d -or
    $crowDefinition.GraphicFlags -ne 0x30) {
    throw 'ENEMY_CROW no longer resolves to gfx `$93 / collision `$31 / extra `$3d / flags `$30.'
}
$crowGfx = $crowDefinition.Gfx
$crowRadiusY = $crowDefinition.RadiusY
$crowRadiusX = $crowDefinition.RadiusX
$crowDamageQuarters = $crowDefinition.Damage
$crowHealth = $crowDefinition.Health
if ($crowRadiusY -ne 6 -or $crowRadiusX -ne 6 -or
    $crowDamageQuarters -ne 2 -or $crowHealth -ne 1) {
    throw 'ENEMY_CROW attributes no longer match radius 6x6, half-heart damage, and one health.'
}
$crowAnimations = @(Resolve-EnemyAnimations 0x41 $true)
if ($crowAnimations.Count -ne 4) {
    throw 'Expected four ENEMY_CROW animations.'
}
if ($crowAnimations[0] -ne '127,0@8,0,0,0;8,8,2,0' -or
    $crowAnimations[1] -ne '127,0@8,0,2,32;8,8,0,32' -or
    $crowAnimations[2] -ne '16,0@8,0,0,0;8,8,2,0|16,1@8,0,4,0;8,8,6,0' -or
    $crowAnimations[3] -ne '16,0@8,0,2,32;8,8,0,32|16,1@8,0,6,32;8,8,4,32') {
    throw "ENEMY_CROW animation/OAM data no longer matches the original records: $($crowAnimations -join '; ')"
}

$crowRows = [Collections.Generic.List[string]]::new()
$crowRows.Add("# group`troom`tid`tsubid`tflags`tcount`tposition-mode`ty`tx`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tspeed-raw`tperched-right`tperched-left`tflight-right`tflight-left")
$crowAliases = [Collections.Generic.List[object]]::new()
$crowLastSpecificFlags = '00'
foreach ($line in Read-ImportLines (Join-Path $Disassembly 'objects\ages\enemyData.s')) {
    if ($line -match '^group(?<group>[0-5])Map(?<room>[0-9a-f]{2})EnemyObjectData:') {
        $crowAliases.Add(@{ Group = [int]$Matches['group']; Room = $Matches['room'] })
        continue
    }
    if ($crowAliases.Count -eq 0) { continue }
    if ($line -match '^\s*obj_SpecificEnemyA\s+(?<values>(?:\$[0-9a-f]{2}\s*)+)$') {
        $values = @([regex]::Matches($Matches['values'], '\$(?<value>[0-9a-f]{2})') |
            ForEach-Object { $_.Groups['value'].Value })
        if ($values.Count -eq 5) {
            $crowLastSpecificFlags = $values[0]
            $id, $subid, $y, $x = $values[1..4]
        } elseif ($values.Count -eq 4) {
            $id, $subid, $y, $x = $values
        } else {
            throw "Malformed Crow obj_SpecificEnemyA row: $line"
        }
        if ($id -eq '41' -and $subid -eq '00') {
            foreach ($alias in $crowAliases) {
                $crowRows.Add("$($alias.Group)`t$($alias.Room)`t41`t00`t$crowLastSpecificFlags`t1`tF`t$y`t$x`t$($gfxNames[$crowGfx])`t0`t3`t$crowRadiusY`t$crowRadiusX`t$crowDamageQuarters`t$crowHealth`t50`t$($crowAnimations -join "`t")")
            }
        }
        continue
    }
    if ($line -match '^\s*obj_EndPointer') { $crowAliases.Clear(); continue }
    if ($line -match '^[A-Za-z0-9_@]+:') { $crowAliases.Clear() }
}
if ($crowRows.Count -ne 4 -or
    ($crowRows | Where-Object { $_ -match '^0\t5d\t41\t00\t00\t1\tF\t78\t78\t' }).Count -ne 1 -or
    ($crowRows | Where-Object { $_ -match '^0\t6d\t41\t00\t00\t1\tF\t38\t(18|88)\t' }).Count -ne 2) {
    throw "Expected the three fixed subid-0 Crows in rooms 0:5d/0:6d, got $($crowRows.Count - 1)."
}
$crowSpriteName = $gfxNames[$crowGfx]
$crowSourceSprite = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$crowSpriteName.png" } |
    Select-Object -First 1
if ($null -eq $crowSourceSprite) { throw "Crow sprite not found: $crowSpriteName.png" }
Copy-Item -LiteralPath $crowSourceSprite.FullName -Destination (Join-Path $destination "gfx\$crowSpriteName.png") -Force
$crowDefinitionRows = [Collections.Generic.List[string]]::new()
$crowDefinitionRows.Add(
    "# id`tsubid`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tspeed-raw`tperched-right`tperched-left`tflight-right`tflight-left")
$crowDefinitionRows.Add(
    "41`t00`t$crowSpriteName`t0`t3`t$crowRadiusY`t$crowRadiusX`t$crowDamageQuarters`t$crowHealth`t50`t$($crowAnimations -join "`t")")

# Preserve parseObjectData order independently of the currently implemented
# enemy species. Random/fixed enemies, reserving parts, and item-drop producers
# all affect wPlacedEnemyPositions; parameterized enemies/parts consume their
# respective object slots without reserving a tile.
$orderedObjectRows = [Collections.Generic.List[string]]::new()
$orderedObjectRows.Add(
    "# group`troom`torder`tkind`tid`tsubid`tflags`tcount`ty`tx`tpacked-position`tcondition-mask")
$orderedAliases = [Collections.Generic.List[object]]::new()
$orderedPendingCondition = 'ff'
$orderedActiveCondition = 'ff'
$orderedActiveOpcode = ''
$orderedSpecificFlags = '00'
$orderedItemFlags = '00'
$orderedIncludeLine = $true
$orderedRegionConditional = ''

foreach ($line in Read-ImportLines (Join-Path $Disassembly 'objects\ages\enemyData.s')) {
    if ($line -match '^\.ifdef\s+(?<symbol>[A-Za-z0-9_]+)\s*$') {
        if ($orderedRegionConditional -ne '' -or
            $Matches['symbol'] -ne 'REGION_JP') {
            throw "Unsupported ordered object conditional: $line"
        }
        $orderedRegionConditional = $Matches['symbol']
        $orderedIncludeLine = $false
        continue
    }
    if ($line -match '^\.else\s*$') {
        if ($orderedRegionConditional -ne 'REGION_JP') {
            throw "Unexpected ordered object .else: $line"
        }
        $orderedIncludeLine = $true
        continue
    }
    if ($line -match '^\.endif\s*$') {
        if ($orderedRegionConditional -ne 'REGION_JP') {
            throw "Unexpected ordered object .endif: $line"
        }
        $orderedRegionConditional = ''
        $orderedIncludeLine = $true
        continue
    }
    if (-not $orderedIncludeLine) { continue }

    if ($line -match '^group(?<group>[0-5])Map(?<room>[0-9a-f]{2})EnemyObjectData:') {
        if ($orderedAliases.Count -eq 0) {
            $orderedPendingCondition = 'ff'
            $orderedActiveCondition = 'ff'
            $orderedActiveOpcode = ''
            $orderedSpecificFlags = '00'
            $orderedItemFlags = '00'
        }
        $orderedAliases.Add(@{
            Group = [int]$Matches['group']
            Room = $Matches['room']
            Order = 0
        })
        continue
    }
    if ($orderedAliases.Count -eq 0) { continue }

    if ($line -match '^\s*obj_Condition\s+\$(?<mask>[0-9a-f]{2})') {
        $orderedPendingCondition = $Matches['mask']
        $orderedActiveOpcode = ''
        continue
    }

    if ($line -match '^\s*obj_RandomEnemy\s+\$(?<flags>[0-9a-f]{2})\s+\$(?<id>[0-9a-f]{2})\s+\$(?<subid>[0-9a-f]{2})') {
        $orderedActiveCondition = $orderedPendingCondition
        $orderedPendingCondition = 'ff'
        $orderedActiveOpcode = 'R'
        $count = ([Convert]::ToInt32($Matches['flags'], 16) -shr 5) -band 7
        foreach ($alias in $orderedAliases) {
            $orderedObjectRows.Add(
                "$($alias.Group)`t$($alias.Room)`t$($alias.Order)`tR`t$($Matches['id'])`t$($Matches['subid'])`t$($Matches['flags'])`t$count`t-1`t-1`t-1`t$orderedActiveCondition")
            $alias.Order = [int]$alias.Order + 1
        }
        continue
    }

    if ($line -match '^\s*obj_SpecificEnemyA\s+(?<values>(?:\$[0-9a-f]{2}\s*)+)$') {
        $values = @([regex]::Matches($Matches['values'], '\$(?<value>[0-9a-f]{2})') |
            ForEach-Object { $_.Groups['value'].Value })
        if ($values.Count -eq 5) {
            $orderedActiveCondition = $orderedPendingCondition
            $orderedPendingCondition = 'ff'
            $orderedActiveOpcode = 'F'
            $orderedSpecificFlags = $values[0]
            $id, $subid, $y, $x = $values[1..4]
        } elseif ($values.Count -eq 4 -and $orderedActiveOpcode -eq 'F') {
            $id, $subid, $y, $x = $values
        } else {
            throw "Malformed ordered obj_SpecificEnemyA row: $line"
        }
        $packed = ([Convert]::ToInt32($y, 16) -band 0xf0) -bor
            (([Convert]::ToInt32($x, 16) -shr 4) -band 0x0f)
        foreach ($alias in $orderedAliases) {
            $orderedObjectRows.Add(
                "$($alias.Group)`t$($alias.Room)`t$($alias.Order)`tF`t$id`t$subid`t$orderedSpecificFlags`t1`t$y`t$x`t$($packed.ToString('x2'))`t$orderedActiveCondition")
            $alias.Order = [int]$alias.Order + 1
        }
        continue
    }

    if ($line -match '^\s*obj_Part\s+(?<values>(?:\$[0-9a-f]{2}\s*)+)$') {
        $values = @([regex]::Matches($Matches['values'], '\$(?<value>[0-9a-f]{2})') |
            ForEach-Object { $_.Groups['value'].Value })
        if ($values.Count -eq 3) {
            if ($orderedActiveOpcode -ne 'P') {
                $orderedActiveCondition = $orderedPendingCondition
                $orderedPendingCondition = 'ff'
            }
            $orderedActiveOpcode = 'P'
            $id, $subid, $packed = $values
            $kind = 'P'
            $y = '-1'
            $x = '-1'
        } elseif ($values.Count -eq 5) {
            if ($orderedActiveOpcode -ne '9') {
                $orderedActiveCondition = $orderedPendingCondition
                $orderedPendingCondition = 'ff'
            }
            $orderedActiveOpcode = '9'
            $id, $subid, $y, $x, $null = $values
            $packedValue = ([Convert]::ToInt32($y, 16) -band 0xf0) -bor
                (([Convert]::ToInt32($x, 16) -shr 4) -band 0x0f)
            $packed = $packedValue.ToString('x2')
            $kind = 'Q'
        } else {
            throw "Malformed ordered obj_Part row: $line"
        }
        foreach ($alias in $orderedAliases) {
            $orderedObjectRows.Add(
                "$($alias.Group)`t$($alias.Room)`t$($alias.Order)`t$kind`t$id`t$subid`t00`t1`t$y`t$x`t$packed`t$orderedActiveCondition")
            $alias.Order = [int]$alias.Order + 1
        }
        continue
    }

    if ($line -match '^\s*obj_SpecificEnemyB\s+\$(?<id>[0-9a-f]{2})\s+\$(?<subid>[0-9a-f]{2})\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})\s+\$(?<var03>[0-9a-f]{2})') {
        if ($orderedActiveOpcode -ne '9') {
            $orderedActiveCondition = $orderedPendingCondition
            $orderedPendingCondition = 'ff'
        }
        $orderedActiveOpcode = '9'
        $packed = ([Convert]::ToInt32($Matches['y'], 16) -band 0xf0) -bor
            (([Convert]::ToInt32($Matches['x'], 16) -shr 4) -band 0x0f)
        foreach ($alias in $orderedAliases) {
            $orderedObjectRows.Add(
                "$($alias.Group)`t$($alias.Room)`t$($alias.Order)`tB`t$($Matches['id'])`t$($Matches['subid'])`t00`t1`t$($Matches['y'])`t$($Matches['x'])`t$($packed.ToString('x2'))`t$orderedActiveCondition")
            $alias.Order = [int]$alias.Order + 1
        }
        continue
    }

    if ($line -match '^\s*obj_ItemDrop\s+(?<values>(?:\$[0-9a-f]{2}\s*)+)$') {
        $values = @([regex]::Matches($Matches['values'], '\$(?<value>[0-9a-f]{2})') |
            ForEach-Object { $_.Groups['value'].Value })
        if ($values.Count -eq 3) {
            $orderedActiveCondition = $orderedPendingCondition
            $orderedPendingCondition = 'ff'
            $orderedActiveOpcode = 'I'
            $orderedItemFlags, $item, $packed = $values
        } elseif ($values.Count -eq 2 -and $orderedActiveOpcode -eq 'I') {
            $item, $packed = $values
        } else {
            throw "Malformed ordered obj_ItemDrop row: $line"
        }
        foreach ($alias in $orderedAliases) {
            $orderedObjectRows.Add(
                "$($alias.Group)`t$($alias.Room)`t$($alias.Order)`tI`t57`t$item`t$orderedItemFlags`t1`t-1`t-1`t$packed`t$orderedActiveCondition")
            $alias.Order = [int]$alias.Order + 1
        }
        continue
    }

    if ($line -match '^\s*obj_EndPointer') {
        $orderedAliases.Clear()
        continue
    }
    if ($line -match '^\s*obj_[A-Za-z0-9_]+') {
        $orderedActiveOpcode = 'X'
        $orderedActiveCondition = $orderedPendingCondition
        $orderedPendingCondition = 'ff'
        continue
    }
    if ($line -match '^[A-Za-z0-9_@]+:') {
        $orderedAliases.Clear()
    }
}

if ($orderedRegionConditional -ne '') {
    throw "Ordered object conditional $orderedRegionConditional was not closed."
}

# mainData.s parses the direct vine enemy before any following enemy pointer.
# Prepend those five records to the same authoritative ordered stream and
# shift pointer-owned orders in the two rooms which contain both forms.
$directVines = @{}
$mainObjectGroup = -1
$mainObjectRoom = -1
foreach ($line in Read-ImportLines (
    Join-Path $Disassembly 'objects\ages\mainData.s')) {
    if ($line -match '^group(?<group>[0-5])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $mainObjectGroup = [int]$Matches['group']
        $mainObjectRoom = [Convert]::ToInt32($Matches['room'], 16)
        continue
    }
    if ($mainObjectGroup -lt 0) { continue }
    if ($line -match
        '^\s*obj_SpecificEnemyA\s+\$00\s+\$62\s+\$(?<subid>[0-9a-f]{2})\s+\$00\s+\$00\s*$') {
        $key = "$mainObjectGroup`:$($mainObjectRoom.ToString('x2'))"
        if ($directVines.ContainsKey($key)) {
            throw "Room $key contains more than one direct ENEMY_VINE_SPROUT."
        }
        $directVines[$key] = $Matches['subid']
        continue
    }
    if ($line -match '^\s*obj_End\s*$' -or
        $line -match '^[A-Za-z0-9_@]+:') {
        $mainObjectGroup = -1
        $mainObjectRoom = -1
    }
}
if ($directVines.Count -ne 5 -or
    $directVines['1:2c'] -ne '00' -or
    $directVines['1:61'] -ne '01' -or
    $directVines['1:ba'] -ne '02' -or
    $directVines['1:cc'] -ne '03' -or
    $directVines['1:da'] -ne '04') {
    throw 'Expected the five direct ENEMY_VINE_SPROUT placements from mainData.s.'
}
$mergedOrderedRows = [Collections.Generic.List[string]]::new()
$mergedOrderedRows.Add($orderedObjectRows[0])
$insertedDirectVines = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::Ordinal)
foreach ($row in $orderedObjectRows | Select-Object -Skip 1) {
    $columns = $row -split "`t"
    $key = "$($columns[0]):$($columns[1])"
    if ($directVines.ContainsKey($key)) {
        if ($insertedDirectVines.Add($key)) {
            $mergedOrderedRows.Add(
                "$($columns[0])`t$($columns[1])`t0`tF`t62`t" +
                "$($directVines[$key])`t00`t1`t00`t00`t00`tff")
        }
        $columns[2] = ([int]$columns[2] + 1).ToString()
    }
    $mergedOrderedRows.Add($columns -join "`t")
}
foreach ($key in $directVines.Keys | Sort-Object) {
    if ($insertedDirectVines.Contains($key)) { continue }
    $group, $room = $key -split ':'
    $mergedOrderedRows.Add(
        "$group`t$room`t0`tF`t62`t$($directVines[$key])`t00`t1`t00`t00`t00`tff")
    [void]$insertedDirectVines.Add($key)
}
$orderedObjectRows = $mergedOrderedRows

if ($orderedObjectRows.Count -ne 1146) {
    throw "Expected 1145 clean-US ordered placement records, parsed $($orderedObjectRows.Count - 1)."
}
if (-not ($orderedObjectRows | Where-Object { $_ -match '^5\tb0\t0\tF\t1b\t01\t00\t1\t68\t38\t63\tff$' }) -or
    -not ($orderedObjectRows | Where-Object { $_ -match '^5\tb0\t1\tF\t34\t00\t00\t1\t78\t58\t75\tff$' }) -or
    -not ($orderedObjectRows | Where-Object { $_ -match '^5\tb0\t2\tR\t32\t00\t40\t2\t-1\t-1\t-1\tff$' }) -or
    -not ($orderedObjectRows | Where-Object { $_ -match '^5\tdb\t0\tI\t57\t01\t00\t1\t-1\t-1\t1d\tff$' }) -or
    -not ($orderedObjectRows | Where-Object { $_ -match '^5\t01\t0\tP\t23\t01\t00\t1\t-1\t-1\t08\tff$' }) -or
    -not ($orderedObjectRows | Where-Object { $_ -match '^1\t0e\t0\tP\t13\t13\t00\t1\t-1\t-1\t23\tff$' }) -or
    ($orderedObjectRows | Where-Object { $_ -match '^1\t0e\t\d+\tP\t13\t09\t' })) {
    throw 'Canonical ordered enemy/fixed-enemy/item-drop/part placement records were not extracted.'
}

# The former per-species room tables are retained only as an importer-local
# migration oracle. Generated runtime species tables contain definitions only,
# so every supported placement must have an exact ordered-stream row.
function Assert-SpeciesPlacementMigration(
    [string]$name,
    [object]$legacyRows,
    [string]$id,
    [string[]]$subids,
    [bool]$hasPosition
) {
    $legacyProjection = @($legacyRows | Select-Object -Skip 1 | ForEach-Object {
        $columns = $_ -split "`t"
        if ($hasPosition) {
            $kind = $columns[6]
            $y = $columns[7]
            $x = $columns[8]
            $packed = if ($kind -eq 'F') {
                $value = ([Convert]::ToInt32($y, 16) -band 0xf0) -bor
                    (([Convert]::ToInt32($x, 16) -shr 4) -band 0x0f)
                $value.ToString('x2')
            } else {
                '-1'
            }
        } else {
            $kind = 'R'
            $y = '-1'
            $x = '-1'
            $packed = '-1'
        }
        "$($columns[0])`t$($columns[1])`t$kind`t$($columns[2])`t$($columns[3])`t$($columns[4])`t$($columns[5])`t$y`t$x`t$packed"
    } | Sort-Object)

    $subidSet = @{}
    foreach ($subid in $subids) { $subidSet[$subid] = $true }
    $orderedProjection = @($orderedObjectRows | Select-Object -Skip 1 |
        ForEach-Object {
            $columns = $_ -split "`t"
            if ($columns[4] -eq $id -and $subidSet.ContainsKey($columns[5])) {
                "$($columns[0])`t$($columns[1])`t$($columns[3])`t$($columns[4])`t$($columns[5])`t$($columns[6])`t$($columns[7])`t$($columns[8])`t$($columns[9])`t$($columns[10])"
            }
        } | Sort-Object)

    if ($legacyProjection.Count -ne $orderedProjection.Count -or
        -not [string]::Equals(
            ($legacyProjection -join "`n"),
            ($orderedProjection -join "`n"),
            [StringComparison]::Ordinal)) {
        $difference = Compare-Object $legacyProjection $orderedProjection |
            Select-Object -First 4 |
            Out-String
        throw "$name placement migration mismatch: legacy=$($legacyProjection.Count), ordered=$($orderedProjection.Count). $difference"
    }
}

Assert-SpeciesPlacementMigration 'Keese' $keeseRows '32' @('00', '01') $false
Assert-SpeciesPlacementMigration 'Octorok' $octorokRows '09' @('00', '01', '02') $true
Assert-SpeciesPlacementMigration 'Stalfos' $stalfosRows '31' @('00') $true
Assert-SpeciesPlacementMigration 'Zol' $zolRows '34' @('00', '01') $true
Assert-SpeciesPlacementMigration 'Gel' $gelRows '43' @('00') $true
Assert-SpeciesPlacementMigration 'Crow' $crowRows '41' @('00') $true

Write-GeneratedTable(
    $keesePath, $keeseDefinitionRows)
Write-GeneratedTable(
    $octorokPath, $octorokDefinitionRows)
Write-GeneratedTable(
    $stalfosPath, $stalfosDefinitionRows)
Write-GeneratedTable(
    (Join-Path $destination 'objects\zols.tsv'),
    $zolDefinitionRows)
Write-GeneratedTable(
    (Join-Path $destination 'objects\gels.tsv'),
    $gelDefinitionRows)
Write-GeneratedTable(
    (Join-Path $destination 'objects\crows.tsv'),
    $crowDefinitionRows)
Write-GeneratedTable(
    (Join-Path $destination 'objects\enemy_object_stream.tsv'),
    $orderedObjectRows)

# Keep ordered enemy construction capability separate from species data and
# from the placement opcode. The registry is keyed only by the original enemy
# ID/subid; each room-object row still owns its source order, count, flags, and
# fixed/random/parameter placement semantics.
$orderedEnemyImplementationHandlers = [ordered]@{
    '09:00' = 'octorok'
    '09:01' = 'octorok'
    '09:02' = 'octorok'
    '0a:00' = 'boomerang-moblin'
    '0b:00' = 'leever'
    '0c:00' = 'arrow-moblin'
    '10:00' = 'rope'
    '13:00' = 'spark'
    '14:00' = 'spiked-beetle'
    '17:00' = 'ghini'
    '19:00' = 'whisp'
    '1a:00' = 'sand-crab'
    '1b:01' = 'spiny-beetle'
    '20:00' = 'masked-moblin'
    '20:01' = 'masked-moblin'
    '22:00' = 'arrow-moblin'
    '23:00' = 'pols-voice'
    '28:00' = 'wallmaster'
    '2f:00' = 'thwomp'
    '31:00' = 'stalfos'
    '32:00' = 'keese'
    '32:01' = 'keese'
    '33:00' = 'baby-cucco'
    '34:00' = 'zol'
    '34:01' = 'zol'
    '36:00' = 'cucco'
    '3e:00' = 'peahat'
    '41:00' = 'crow'
    '43:00' = 'gel'
    '47:00' = 'color-changing-gel'
    '49:00' = 'sword-enemy'
    '4a:01' = 'sword-enemy'
    '4d:00' = 'hardhat-beetle'
    '4f:00' = 'moldorm'
    '62:00' = 'vine-sprout'
    '62:01' = 'vine-sprout'
    '62:02' = 'vine-sprout'
    '62:03' = 'vine-sprout'
    '62:04' = 'vine-sprout'
}
$dynamicEnemyImplementationHandlers = [ordered]@{}
if ($orderedEnemyImplementationHandlers.Count -ne 39 -or
    $dynamicEnemyImplementationHandlers.Count -ne 0) {
    throw 'Enemy implementation registry key counts changed.'
}

$enemyConstantDefinitions = @{}
foreach ($constantSpec in @(
    @{
        Path = 'constants\common\enemies.s'
        Source = 'constants/common/enemies.s'
    },
    @{
        Path = 'constants\ages\enemies.s'
        Source = 'constants/ages/enemies.s'
    }
)) {
    $constantSource = Read-ImportText (
        Join-Path $Disassembly ([string]$constantSpec.Path))
    foreach ($match in [regex]::Matches(
        $constantSource,
        '(?m)^\.define\s+(?<name>ENEMY_[A-Z0-9_]+)\s+\$(?<id>[0-9a-f]{2})\s*$')) {
        $id = [Convert]::ToInt32($match.Groups['id'].Value, 16)
        if ($enemyConstantDefinitions.ContainsKey($id)) {
            throw "Enemy ID `$$($id.ToString('x2')) has more than one constant definition."
        }
        $enemyConstantDefinitions[$id] = @{
            Name = $match.Groups['name'].Value
            Source = "$($constantSpec.Source):$($match.Groups['name'].Value)"
        }
    }
}

$enemyHandlerKeys = @{}
$enemyClassificationCounts = @{
    'ordered-implemented' = 0
    'dynamic-special' = 0
    'deliberately-unsupported' = 0
}
$enemyParameterRows = 0
foreach ($row in $orderedObjectRows | Select-Object -Skip 1) {
    $columns = $row -split "`t"
    $kind = $columns[3]
    if ($kind -notin @('R', 'F', 'B')) {
        continue
    }
    $id = [Convert]::ToInt32($columns[4], 16)
    $subid = [Convert]::ToInt32($columns[5], 16)
    $key = "$($id.ToString('x2'))`:$($subid.ToString('x2'))"
    if (-not $enemyHandlerKeys.ContainsKey($key)) {
        $enemyHandlerKeys[$key] = @{
            Id = $id
            SubId = $subid
        }
    }
    if ($kind -eq 'B') {
        $enemyParameterRows++
        continue
    }
    $classification = if (
        $orderedEnemyImplementationHandlers.Contains($key)) {
        'ordered-implemented'
    } elseif ($dynamicEnemyImplementationHandlers.Contains($key)) {
        'dynamic-special'
    } else {
        'deliberately-unsupported'
    }
    $enemyClassificationCounts[$classification]++
}

if ($enemyHandlerKeys.Count -ne 123 -or
    $enemyParameterRows -ne 12 -or
    $enemyClassificationCounts['ordered-implemented'] -ne 430 -or
    $enemyClassificationCounts['dynamic-special'] -ne 0 -or
    $enemyClassificationCounts['deliberately-unsupported'] -ne 391) {
    throw "Enemy handler classification manifest changed: keys=$($enemyHandlerKeys.Count), " +
        "parameter=$enemyParameterRows, classifications=" +
        "$($enemyClassificationCounts | Out-String)"
}
foreach ($key in @(
    $orderedEnemyImplementationHandlers.Keys +
    $dynamicEnemyImplementationHandlers.Keys
)) {
    if (-not $enemyHandlerKeys.ContainsKey($key)) {
        throw "Enemy implementation key $key has no ordered source placement."
    }
}

$enemyCollisionModes = @{}
foreach ($match in [regex]::Matches(
    $enemyDataSource,
    '(?m)^\s*/\* 0x(?<id>[0-9a-f]{2}) \*/ m_EnemyData \$[0-9a-f]{2} \$(?<collision>[0-9a-f]{2})(?:\s|$)')) {
    $id = [Convert]::ToInt32($match.Groups['id'].Value, 16)
    if ($enemyCollisionModes.ContainsKey($id)) {
        throw "Enemy `$$($id.ToString('x2')) has duplicate enemyData rows."
    }
    $enemyCollisionModes[$id] =
        [Convert]::ToInt32($match.Groups['collision'].Value, 16)
}
if ($enemyCollisionModes.Count -ne 128) {
    throw "Expected 128 source enemy collision modes, got $($enemyCollisionModes.Count)."
}

$enemyCollisionTableSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\objectCollisionTable.s')
$enemyCollisionTableValues = [Collections.Generic.List[int]]::new()
$enemyCollisionRows = [regex]::Matches(
    $enemyCollisionTableSource,
    '(?m)^\s*\.db(?<values>(?:\s+\$[0-9a-f]{2}){16})\s*$')
foreach ($collisionRow in $enemyCollisionRows) {
    foreach ($value in [regex]::Matches(
        $collisionRow.Groups['values'].Value,
        '\$(?<value>[0-9a-f]{2})')) {
        $enemyCollisionTableValues.Add(
            [Convert]::ToInt32($value.Groups['value'].Value, 16))
    }
}
if ($enemyCollisionRows.Count -ne 256 -or
    $enemyCollisionTableValues.Count -ne 0x1000) {
    throw "Expected 256 object-collision rows / 4096 effects, got " +
        "$($enemyCollisionRows.Count) / $($enemyCollisionTableValues.Count)."
}

$enemyHandlerRows = [Collections.Generic.List[string]]::new()
$enemyHandlerRows.Add(
    "# id`tsubid`tcollision-mode`tclassification`thandler`tenemy-name`tsource`t" +
    "shield-l1-effect`tshield-l2-effect`tshield-l3-effect`tshield-source")
foreach ($record in $enemyHandlerKeys.Values |
    Sort-Object @{ Expression = { [int]$_.Id } },
        @{ Expression = { [int]$_.SubId } }) {
    $key = "$(([int]$record.Id).ToString('x2'))`:" +
        "$(([int]$record.SubId).ToString('x2'))"
    $classification = 'deliberately-unsupported'
    $handler = '-'
    if ($orderedEnemyImplementationHandlers.Contains($key)) {
        $classification = 'ordered-implemented'
        $handler = [string]$orderedEnemyImplementationHandlers[$key]
    } elseif ($dynamicEnemyImplementationHandlers.Contains($key)) {
        $classification = 'dynamic-special'
        $handler = [string]$dynamicEnemyImplementationHandlers[$key]
    }
    $definition = $enemyConstantDefinitions[[int]$record.Id]
    if ($null -eq $definition) {
        throw "Enemy handler key $key has no source constant."
    }
    $source = if ($classification -eq 'dynamic-special') {
        'scripts/ages/scriptHelper.s:moblin_spawnEnemyHere'
    } else {
        [string]$definition.Source
    }
    $collisionMode = [int]$enemyCollisionModes[[int]$record.Id]
    $collisionRowOffset = ($collisionMode -band 0x7f) * 0x20
    $shieldSource = 'data/ages/objectCollisionTable.s:' +
        "objectCollisionTable+`$$($collisionRowOffset.ToString('x4'))"
    $enemyHandlerRows.Add(
        "$(([int]$record.Id).ToString('x2'))`t" +
        "$(([int]$record.SubId).ToString('x2'))`t" +
        "$($collisionMode.ToString('x2'))`t" +
        "$classification`t$handler`t$($definition.Name)`t$source`t" +
        "$($enemyCollisionTableValues[$collisionRowOffset + 1].ToString('x2'))`t" +
        "$($enemyCollisionTableValues[$collisionRowOffset + 2].ToString('x2'))`t" +
        "$($enemyCollisionTableValues[$collisionRowOffset + 3].ToString('x2'))`t" +
        $shieldSource)
}
if ($enemyHandlerRows.Count -ne 124 -or
    -not $enemyHandlerRows.Contains((
        "09`t00`t90`tordered-implemented`toctorok`tENEMY_OCTOROK`t" +
        'constants/common/enemies.s:ENEMY_OCTOROK' +
        "`t10`t0f`t0f`tdata/ages/objectCollisionTable.s:" +
        'objectCollisionTable+$0200')) -or
    -not $enemyHandlerRows.Contains((
        "20`t00`t91`tordered-implemented`tmasked-moblin`t" +
        "ENEMY_MASKED_MOBLIN`tconstants/common/enemies.s:ENEMY_MASKED_MOBLIN`t" +
        "10`t0f`t0f`tdata/ages/objectCollisionTable.s:" +
        'objectCollisionTable+$0220')) -or
    -not $enemyHandlerRows.Contains((
        "14`t00`t98`tordered-implemented`tspiked-beetle`tENEMY_SPIKED_BEETLE`t" +
        'constants/common/enemies.s:ENEMY_SPIKED_BEETLE' +
        "`t10`t0f`t0f`tdata/ages/objectCollisionTable.s:" +
        'objectCollisionTable+$0300')) -or
    -not $enemyHandlerRows.Contains((
        "1b`t01`t90`tordered-implemented`tspiny-beetle`tENEMY_SPINY_BEETLE`t" +
        'constants/common/enemies.s:ENEMY_SPINY_BEETLE' +
        "`t10`t0f`t0f`tdata/ages/objectCollisionTable.s:" +
        'objectCollisionTable+$0200'))) {
    $representativeRows = @($enemyHandlerRows | Where-Object {
        $_ -match 'ENEMY_(OCTOROK|MASKED_MOBLIN|SPIKED_BEETLE|SPINY_BEETLE)'
    })
    throw "Enemy handler registry lost its expected source classifications " +
        "(rows=$($enemyHandlerRows.Count)):`n" +
        ($representativeRows -join "`n")
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\enemy_handler_registry.tsv'),
    $enemyHandlerRows)

# PART_ENEMY_DESTROYED (`$02) is the common enemy death puff. Export both
# animations: animation 0 is the ordinary 20-update puff, while animation 1
# inserts the 8-update high-knockback burst selected by bit 7 of the defeated
# enemy's knockback counter.
$partDataSource = Read-ImportText (Join-Path $Disassembly "data\ages\partData.s")
$deathPuffData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$00 \$00 \$00 \$00 \$40 \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2}) \$00\s*; \$02'
)
if (-not $deathPuffData.Success) { throw "Could not resolve PART_ENEMY_DESTROYED (`$02) data." }
$deathPuffTileBase = [Convert]::ToInt32($deathPuffData.Groups['tile'].Value, 16)
$deathPuffOamFlags = [Convert]::ToInt32($deathPuffData.Groups['flags'].Value, 16)
if ($deathPuffTileBase -ne 0x0c -or $deathPuffOamFlags -ne 0x0a) {
    throw "PART_ENEMY_DESTROYED no longer resolves to tile base `$0c / OAM flags `$0a."
}

$partAnimationSource = Read-ImportText (Join-Path $Disassembly "data\ages\partAnimations.s")
$partOamSource = Read-ImportText (Join-Path $Disassembly "data\ages\partOamData.s")

# PART_MOBLIN_BOOMERANG $21 belongs to the common Boomerang Moblin species,
# not to the first dungeon. Retain its four rotating OAM frames in a shared
# projectile record.
$boomerangAnimationLabels = @([regex]::Matches(
    (Get-AssemblyLabelBody $partAnimationSource 'part21Animations'),
    '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
$boomerangOamLabels = @([regex]::Matches(
    (Get-AssemblyLabelBody $partAnimationSource 'part21OamDataPointers'),
    '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if ($boomerangAnimationLabels.Count -ne 1 -or
    $boomerangOamLabels.Count -ne 4) {
    throw 'PART_MOBLIN_BOOMERANG animation/OAM tables changed.'
}
$boomerangFrames = [Collections.Generic.List[string]]::new()
foreach ($frame in [regex]::Matches(
    (Get-AssemblyLabelBody $partAnimationSource $boomerangAnimationLabels[0]),
    '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2})\s+\$(?<offset>[0-9a-f]{2})\s+\$(?<parameter>[0-9a-f]{2})')) {
    $duration = [Convert]::ToInt32(
        $frame.Groups['duration'].Value, 16)
    $pointerIndex = [int](
        [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2)
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
Copy-EnemySprite $boomerangSprite
$boomerangAnimationData = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($boomerangFrames -join '|'))
$boomerangRows = @(
    '# sprites`ttile-base`tpalette`tsource-grayscale-inverted`tanimations-base64'.Replace(
        '`t', "`t")
    "$boomerangSprite`t10`t4`t1`t$boomerangAnimationData"
)
Write-GeneratedTable(
    (Join-Path $destination 'effects\moblin_boomerang.tsv'),
    $boomerangRows)

$deathPuffAnimationLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $partAnimationSource 'part02Animations'),
        '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$deathPuffOamLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $partAnimationSource 'part02OamDataPointers'),
        '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($deathPuffAnimationLabels.Count -ne 2 -or $deathPuffOamLabels.Count -ne 7) {
    throw "Expected two death-puff animations and seven death-puff OAM pointers."
}

function Resolve-DeathPuffAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:partAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
        if ($parameter -ne 0) { break }
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:deathPuffOamLabels.Count) {
            throw "$label references missing OAM pointer byte offset $($frame.Groups['offset'].Value)."
        }
        $frames.Add("$duration@$(Resolve-Oam $script:partOamSource $script:deathPuffOamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}

$deathPuffNormalAnimation = Resolve-DeathPuffAnimation $deathPuffAnimationLabels[0]
$deathPuffKnockbackAnimation = Resolve-DeathPuffAnimation $deathPuffAnimationLabels[1]
$deathPuffNormalDurations = @($deathPuffNormalAnimation.Split('|') | ForEach-Object { [int]($_.Split('@')[0]) })
$deathPuffKnockbackDurations = @($deathPuffKnockbackAnimation.Split('|') | ForEach-Object { [int]($_.Split('@')[0]) })
if ($deathPuffNormalDurations.Count -ne 7 -or
    ($deathPuffNormalDurations | Measure-Object -Sum).Sum -ne 20 -or
    $deathPuffKnockbackDurations.Count -ne 8 -or
    ($deathPuffKnockbackDurations | Measure-Object -Sum).Sum -ne 28 -or
    $deathPuffKnockbackDurations[3] -ne 8) {
    throw "PART_ENEMY_DESTROYED animations no longer match the 20/28-update records."
}

$deathPuffRows = @(
    "# tile-base`tpalette-a`tpalette-b`tnormal-animation`thigh-knockback-animation",
    "$deathPuffTileBase`t$($deathPuffOamFlags -band 7)`t$(($deathPuffOamFlags -bxor 1) -band 7)`t$deathPuffNormalAnimation`t$deathPuffKnockbackAnimation"
)
$deathPuffPath = Join-Path $destination "effects\enemy_death_puff.tsv"
Write-GeneratedTable($deathPuffPath, $deathPuffRows)

# PART_BOSS_DEATH_EXPLOSION (`$04) keeps wNumEnemies occupied until its
# terminal `$ff animation parameter. Boss reward scripts therefore cannot
# advance while the large explosion is still visible.
$bossExplosionData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$00 \$00 \$00 \$00 \$40 \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2}) \$00\s*; \$04'
)
if (-not $bossExplosionData.Success) {
    throw 'Could not resolve PART_BOSS_DEATH_EXPLOSION (`$04) data.'
}
$bossExplosionTileBase = [Convert]::ToInt32(
    $bossExplosionData.Groups['tile'].Value, 16)
$bossExplosionPalette = [Convert]::ToInt32(
    $bossExplosionData.Groups['flags'].Value, 16) -band 7
$bossExplosionAnimationLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $partAnimationSource 'part04Animations'),
        '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$bossExplosionOamLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $partAnimationSource 'part04OamDataPointers'),
        '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($bossExplosionAnimationLabels.Count -ne 1 -or
    $bossExplosionOamLabels.Count -ne 13) {
    throw 'Expected one boss-explosion animation and 13 OAM pointers.'
}
$bossExplosionFrames = [Collections.Generic.List[string]]::new()
foreach ($frame in [regex]::Matches(
    (Get-AssemblyLabelBody $partAnimationSource $bossExplosionAnimationLabels[0]),
    '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
)) {
    $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
    if ($parameter -eq 0xff) { break }
    if ($parameter -ne 0) {
        throw "Boss explosion has unexpected animation parameter `$$($frame.Groups['parameter'].Value)."
    }
    $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
    if ($pointerIndex -ge $bossExplosionOamLabels.Count) {
        throw "Boss explosion references missing OAM pointer $pointerIndex."
    }
    $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
    $bossExplosionFrames.Add(
        "$duration@$(Resolve-Oam $partOamSource $bossExplosionOamLabels[$pointerIndex])")
}
$bossExplosionDuration = ($bossExplosionFrames | ForEach-Object {
    [int](($_ -split '@')[0])
} | Measure-Object -Sum).Sum
if ($bossExplosionTileBase -ne 0x0c -or $bossExplosionPalette -ne 2 -or
    $bossExplosionFrames.Count -ne 13 -or $bossExplosionDuration -ne 78) {
    throw 'PART_BOSS_DEATH_EXPLOSION no longer matches tile `$0c, palette 2, or its 13-frame/78-update animation.'
}
Write-GeneratedTable(
    (Join-Path $destination 'effects\boss_death_explosion.tsv'),
    @(
        "# tile-base`tpalette`tanimation",
        "$bossExplosionTileBase`t$bossExplosionPalette`t$($bossExplosionFrames -join '|')"
    ))

# PART_SHADOW (`$07) is attached to both Spirit's Grave bosses. The source
# selects one of four static OAM records from the parent's Z byte and flickers
# the part every update while the parent is airborne.
$bossShadowData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$(?<gfx>[0-9a-f]{2}) \$00 \$00 \$00 \$40 \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2}) \$00\s*; \$07'
)
if (-not $bossShadowData.Success) {
    throw 'Could not resolve PART_SHADOW (`$07) data.'
}
$bossShadowGfx = [Convert]::ToInt32($bossShadowData.Groups['gfx'].Value, 16)
$bossShadowTileBase = [Convert]::ToInt32($bossShadowData.Groups['tile'].Value, 16)
$bossShadowPalette = [Convert]::ToInt32($bossShadowData.Groups['flags'].Value, 16) -band 7
$bossShadowAnimationStart = $partAnimationSource.IndexOf(
    'part07Animations:', [StringComparison]::Ordinal)
$bossShadowAnimationEnd = $partAnimationSource.IndexOf(
    'part13Animations:', $bossShadowAnimationStart, [StringComparison]::Ordinal)
$bossShadowAnimationLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring(
            $bossShadowAnimationStart,
            $bossShadowAnimationEnd - $bossShadowAnimationStart),
        '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$bossShadowOamStart = $partAnimationSource.IndexOf(
    'part07OamDataPointers:', [StringComparison]::Ordinal)
$bossShadowOamEnd = $partAnimationSource.IndexOf(
    'part11OamDataPointers:', $bossShadowOamStart, [StringComparison]::Ordinal)
$bossShadowOamLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring(
            $bossShadowOamStart, $bossShadowOamEnd - $bossShadowOamStart),
        '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($bossShadowAnimationLabels.Count -lt 4 -or $bossShadowOamLabels.Count -ne 4) {
    throw 'Expected at least four PART_SHADOW animations and exactly four OAM pointers.'
}
$bossShadowFrames = [Collections.Generic.List[string]]::new()
foreach ($index in 0..3) {
    $frame = [regex]::Match(
        (Get-AssemblyLabelBody $partAnimationSource $bossShadowAnimationLabels[$index]),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$00'
    )
    if (-not $frame.Success -or
        [Convert]::ToInt32($frame.Groups['duration'].Value, 16) -ne 0x7f -or
        [Convert]::ToInt32($frame.Groups['offset'].Value, 16) -ne $index * 2) {
        throw "PART_SHADOW animation $index no longer contains its static source frame."
    }
    $bossShadowFrames.Add((
        Resolve-Oam $partOamSource $bossShadowOamLabels[$index]))
}
$bossShadowSprite = $gfxNames[$bossShadowGfx]
if ($bossShadowGfx -ne 0xa7 -or $bossShadowSprite -ne 'spr_projectiles_3' -or
    $bossShadowTileBase -ne 0 -or $bossShadowPalette -ne 0 -or
    $bossShadowFrames[0] -ne '8,4,0,0' -or
    $bossShadowFrames[3] -ne '8,248,4,0;8,0,6,0;8,8,6,32;8,16,4,32') {
    throw 'PART_SHADOW no longer matches gfx `$a7, tile/palette 0, or its small/large OAM records.'
}
Write-GeneratedTable(
    (Join-Path $destination 'effects\boss_shadow.tsv'),
    @(
        "# sprite`ttile-base`tpalette`tanimation-0`tanimation-1`tanimation-2`tanimation-3",
        "$bossShadowSprite`t$bossShadowTileBase`t$bossShadowPalette`t$($bossShadowFrames -join "`t")"
    ))
$bossShadowSpriteSource = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object {
        Get-ChildItem $_.FullName -Recurse -File -Filter "$bossShadowSprite.png"
    } | Select-Object -First 1
if ($null -eq $bossShadowSpriteSource) {
    throw "PART_SHADOW sprite not found: $bossShadowSprite.png"
}
Copy-Item -LiteralPath $bossShadowSpriteSource.FullName `
    -Destination (Join-Path $destination "gfx\$bossShadowSprite.png") -Force

# Objects with visible bit 6 set use the fixed terrain-effect shadow, not
# PART_SHADOW. Its raw OAM selects tile $20 from VRAM bank 1, where the common
# sprite sheet is loaded at tile zero.
$terrainEffectSource = Read-ImportText (
    Join-Path $Disassembly 'data\terrainEffects.s')
$terrainShadowOam = Resolve-Oam $terrainEffectSource 'shadowAnimation'
if ($terrainShadowOam -ne '19,4,32,8') {
    throw "The default terrain shadow no longer matches OAM `$13/`$04, tile `$20, flags `$08."
}
Write-GeneratedTable(
    (Join-Path $destination 'effects\terrain_shadow.tsv'),
    @(
        "# sprite`ttile-base`tpalette`toam`tsource",
        "spr_common_sprites`t0`t0`t$terrainShadowOam`tdata/terrainEffects.s:shadowAnimation"
    ))

# Grounded Link uses the same raw terrain-effect OAM path for grass and
# puddles. Ages checks the exact metatile IDs, selects grass frame 0/1 from bit
# 2 of (xh XOR yh), and selects one of four puddle frames with
# (wFrameCounter >> 3) & 3.
$tileIndexSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\tileIndices.s')
$grassTileMatch = [regex]::Match(
    $tileIndexSource,
    '(?m)^\s*\.define TILEINDEX_GRASS\s+\$(?<value>[0-9a-f]{2})')
$agesPuddleBlock = [regex]::Match(
    $tileIndexSource,
    '(?ms)^\s*\.ifdef ROM_AGES\r?\n(?<body>.*?TILEINDEX_PUDDLE.*?)^\s*\.else ; ROM_SEASONS')
$puddleTileMatch = [regex]::Match(
    $agesPuddleBlock.Groups['body'].Value,
    '(?m)^\s*\.define TILEINDEX_PUDDLE\s+\$(?<value>[0-9a-f]{2})')
if (-not $grassTileMatch.Success -or -not $agesPuddleBlock.Success -or
    -not $puddleTileMatch.Success) {
    throw 'Could not resolve Ages TILEINDEX_GRASS / TILEINDEX_PUDDLE.'
}
$grassTile = [Convert]::ToInt32(
    $grassTileMatch.Groups['value'].Value, 16)
$puddleTile = [Convert]::ToInt32(
    $puddleTileMatch.Groups['value'].Value, 16)

$terrainHandlerSource = Read-ImportText (
    Join-Path $Disassembly 'code\bank0.s')
$usesAgesTerrainTiles = [regex]::IsMatch(
    $terrainHandlerSource,
    '(?ms)\.ifdef ROM_AGES\r?\n\s*cp TILEINDEX_GRASS\r?\n\s*jr z,@walkingInGrass\r?\n\s*cp TILEINDEX_PUDDLE\r?\n\s*jr nz,@end')
$usesPuddleFrameClock = [regex]::IsMatch(
    $terrainHandlerSource,
    '(?ms)ld a,\(wFrameCounter\)\r?\n\s*add a\r?\n\s*swap a\r?\n\s*and \$03\r?\n\s*ld hl,terrainEffects\.puddleAnimationFrames')
$usesGrassPositionFrame = [regex]::IsMatch(
    $terrainHandlerSource,
    '(?ms)ld a,l\r?\n\s*xor b\r?\n\s*ld h,a.*?@walkingInGrass:\r?\n\s*bit 2,h\r?\n\s*ld a,\(wGrassAnimationModifier\)\r?\n\s*jr z,\+\r?\n\s*add \$24')
$grassModifierSource = Read-ImportText (
    Join-Path $Disassembly 'code\bank1.s')
$usesAgesGreenGrass = [regex]::IsMatch(
    $grassModifierSource,
    '(?ms)updateGrassAnimationModifier:\s*\.ifdef ROM_AGES\r?\n\s*ld a,\$00\r?\n\s*ld \(wGrassAnimationModifier\),a\r?\n\s*ret')
$suppressesTerrainEffects = [regex]::IsMatch(
    $terrainHandlerSource,
    '(?ms)_drawObjectTerrainEffects:.*?and TILESETFLAG_SIDESCROLL\r?\n\s*ret nz.*?@onGround:.*?ld a,\(wScrollMode\)\r?\n\s*cp \$08\r?\n\s*ret z')
$terrainEffectsHaveOamPriority = [regex]::IsMatch(
    $terrainHandlerSource,
    '(?ms)_getObjectPositionOnScreen:.*?rlca\r?\n\s*call c,_drawObjectTerrainEffects\r?\n\s*; Account for Z position.*?; Point hl to the Object\.oamFlags variable')
$linkTileTypeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\commonCode.s')
$usesPuddleWalkSound = [regex]::IsMatch(
    $linkTileTypeSource,
    '(?ms)@tileType_puddle:\s*ld h,d\r?\n\s*ld l,SpecialObject\.animParameter\r?\n\s*bit 5,\(hl\)\r?\n\s*jr z,@tileType_normal\r?\n\s*res 5,\(hl\)\r?\n\s*ld a,\(wLinkImmobilized\)\r?\n\s*or a\r?\n\s*ld a,SND_SPLASH\r?\n\s*call z,playSound')
$musicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
$splashSoundMatch = [regex]::Match(
    $musicSource,
    '(?m)^\s*SND_SPLASH\s+db\s+;\s+\$(?<value>[0-9a-f]{2})')
if (-not $splashSoundMatch.Success) {
    throw 'Could not resolve SND_SPLASH.'
}
$splashSound = [Convert]::ToInt32(
    $splashSoundMatch.Groups['value'].Value, 16)

# The puddle handler consumes bit 5 from Link's current animation parameter
# before animateLinkWalking advances the table later in the update. Resolve the
# exact sound phase from the Ages walking animation instead of approximating it
# from the visible two-frame pose.
$linkAnimationSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')
$walkAnimationMatch = [regex]::Match(
    $linkAnimationSource,
    '(?ms)^animationData19f0b:\r?\n(?<body>.*?m_AnimationLoop animationLoop19f0e)')
if (-not $walkAnimationMatch.Success) {
    throw 'Could not resolve Ages LINK_ANIM_MODE_WALK animation data.'
}
$walkAnimationRows = @(
    [regex]::Matches(
        $walkAnimationMatch.Groups['body'].Value,
        '(?m)^\s*\.db \$(?<duration>[0-9a-f]{2}) \$(?<graphic>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})') |
        ForEach-Object {
            [pscustomobject]@{
                Duration = [Convert]::ToInt32(
                    $_.Groups['duration'].Value, 16)
                Parameter = [Convert]::ToInt32(
                    $_.Groups['parameter'].Value, 16)
            }
        }
)
$puddleSoundUpdates = [Collections.Generic.List[int]]::new()
$puddleSoundDurations = [Collections.Generic.List[int]]::new()
$walkElapsed = 0
for ($row = 1; $row -lt $walkAnimationRows.Count; $row++) {
    $walkElapsed += $walkAnimationRows[$row - 1].Duration
    if (($walkAnimationRows[$row].Parameter -band 0x20) -ne 0) {
        # linkApplyTileTypes observes the newly loaded parameter on the next
        # original update.
        $puddleSoundUpdates.Add($walkElapsed + 1)
        $puddleSoundDurations.Add($walkAnimationRows[$row].Duration)
    }
}
$walkLoopDuration = 0
for ($row = 1; $row -lt $walkAnimationRows.Count; $row++) {
    $walkLoopDuration += $walkAnimationRows[$row].Duration
}
$puddleSoundStart = 3
$puddleSoundPeriod = 18
$puddleSoundDuration = 6
if ($grassTile -ne 0xf8 -or $puddleTile -ne 0xf9 -or
    -not $usesAgesTerrainTiles -or -not $usesPuddleFrameClock -or
    -not $usesGrassPositionFrame -or -not $usesAgesGreenGrass -or
    -not $suppressesTerrainEffects -or -not $terrainEffectsHaveOamPriority -or
    -not $usesPuddleWalkSound -or
    $splashSound -ne 0x87 -or $walkAnimationRows.Count -ne 13 -or
    ($puddleSoundUpdates -join ',') -ne '3,21,39,57' -or
    ($puddleSoundDurations -join ',') -ne '6,6,6,6' -or
    $walkLoopDuration -ne 72 -or
    ($puddleSoundUpdates[0] + $walkLoopDuration -
        $puddleSoundUpdates[$puddleSoundUpdates.Count - 1]) -ne
        $puddleSoundPeriod) {
    throw 'Link terrain-effect tile, frame, priority, suppression, or puddle-sound behavior changed.'
}

$grassTerrainLabels = @(
    'greenGrassAnimationFrame0',
    'greenGrassAnimationFrame1'
)
$expectedGrassTerrainOam = @(
    '17,1,36,8;17,7,36,8',
    '17,1,36,40;17,7,36,40'
)
$puddleTerrainLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $terrainEffectSource 'puddleAnimationFrames'),
        '(?m)^\s*\.dw\s+(?<label>puddleAnimationFrame[0-3])\s*$') |
        ForEach-Object { $_.Groups['label'].Value }
)
if (($puddleTerrainLabels -join ',') -ne
        'puddleAnimationFrame0,puddleAnimationFrame1,puddleAnimationFrame2,puddleAnimationFrame3') {
    throw 'Link grass or puddle terrain-effect OAM tables changed.'
}
$expectedPuddleTerrainOam = @(
    '22,3,34,8;22,5,34,40',
    '22,2,34,8;22,6,34,40',
    '23,1,34,8;23,7,34,40',
    '24,0,34,8;24,8,34,40'
)
$linkTerrainEffectRows = [Collections.Generic.List[string]]::new()
for ($frame = 0; $frame -lt $grassTerrainLabels.Count; $frame++) {
    $oam = Resolve-Oam $terrainEffectSource $grassTerrainLabels[$frame]
    if ($oam -ne $expectedGrassTerrainOam[$frame]) {
        throw "Link grass terrain-effect frame $frame changed."
    }
    $linkTerrainEffectRows.Add(
        "grass`t$($grassTile.ToString('x2'))`t$frame`t0`t00`t0`t0`t0`tspr_common_sprites`t0`t0`t$oam`tdata/terrainEffects.s:$($grassTerrainLabels[$frame])+code/bank0.s:_drawObjectTerrainEffects")
}
for ($frame = 0; $frame -lt $puddleTerrainLabels.Count; $frame++) {
    $oam = Resolve-Oam $terrainEffectSource $puddleTerrainLabels[$frame]
    if ($oam -ne $expectedPuddleTerrainOam[$frame]) {
        throw "Link puddle terrain-effect frame $frame changed."
    }
    $linkTerrainEffectRows.Add(
        "puddle`t$($puddleTile.ToString('x2'))`t$frame`t8`t$($splashSound.ToString('x2'))`t$puddleSoundStart`t$puddleSoundPeriod`t$puddleSoundDuration`tspr_common_sprites`t0`t0`t$oam`tdata/terrainEffects.s:$($puddleTerrainLabels[$frame])+object_code/common/specialObjects/commonCode.s:@tileType_puddle")
}
Write-GeneratedTable(
    (Join-Path $destination 'effects\link_terrain_effects.tsv'),
    @(
        "# kind`ttile`tframe`tduration`tsound`tsound-start`tsound-period`tsound-duration`tsprite`ttile-base`tpalette`toam`tsource"
    ) + $linkTerrainEffectRows)

# INTERAC_KILLENEMYPUFF (`$08) is the non-dropping burst used when a red Zol
# splits. It is visually and semantically distinct from PART_ENEMY_DESTROYED.
$interactionDataSource = Read-ImportText (Join-Path $Disassembly 'data\ages\interactionData.s')
$killPuffData = [regex]::Match(
    $interactionDataSource,
    '(?m)^\s*/\* \$08 \*/ m_InteractionData \$(?<gfx>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
)
if (-not $killPuffData.Success -or
    [Convert]::ToInt32($killPuffData.Groups['gfx'].Value, 16) -ne 0 -or
    [Convert]::ToInt32($killPuffData.Groups['tile'].Value, 16) -ne 0x10 -or
    [Convert]::ToInt32($killPuffData.Groups['flags'].Value, 16) -ne 0xb0) {
    throw 'INTERAC_KILLENEMYPUFF no longer resolves to gfx `$00 / tile `$10 / flags `$b0.'
}
$interactionAnimationSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\interactionAnimations.s')
$interactionOamSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\interactionOamData.s')

# PART_OWL_STATUE (`$13) is a reserving enemy-pointer part. Mystery Seeds set
# its collision status to `$9a`; the part then emits six ordinary
# INTERAC_SPARKLE (`$84:$00) children, changes pose, and shows TX_39xx selected
# by its subid. Export one typed row per valid text subid so every source
# placement uses the same runtime state machine.
$owlPartData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) \$(?<radius>[0-9a-f]{2}) \$(?<damage>[0-9a-f]{2}) \$(?<health>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2}) \$00\s*; \$13')
$owlCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\parts\owlStatue.s')
$bank0SourceForOwl = Read-ImportText (
    Join-Path $Disassembly 'code\bank0.s')
$collisionEffectsSourceForOwl = Read-ImportText (
    Join-Path $Disassembly 'code\collisionEffects.s')
if (-not $owlPartData.Success -or
    [Convert]::ToInt32($owlPartData.Groups['gfx'].Value, 16) -ne 0x74 -or
    [Convert]::ToInt32($owlPartData.Groups['collision'].Value, 16) -ne 0x82 -or
    [Convert]::ToInt32($owlPartData.Groups['radius'].Value, 16) -ne 0x77 -or
    [Convert]::ToInt32($owlPartData.Groups['damage'].Value, 16) -ne 0 -or
    [Convert]::ToInt32($owlPartData.Groups['health'].Value, 16) -ne 0x40 -or
    $owlCodeSource -notmatch
        '(?ms)^partCode13:.*?cp \$9a.*?cp \$02.*?ld \(hl\),\$32.*?set 5,\(hl\).*?call objectMakeTileSolid.*?ld \(hl\),\$00.*?ld \(hl\),\$1e.*?cp \$16.*?ld b,\$39\s*jp showText' -or
    $bank0SourceForOwl -notmatch
        '(?ms)^objectMakeTileSolid:.*?ld \(hl\),\$0f' -or
    $collisionEffectsSourceForOwl -notmatch
        '(?ms)^func_07_47b7:.*?cp ITEM_MYSTERY_SEED.*?add Object\.var3f.*?cpl\s*bit 5,a\s*ret nz.*?ld l,Item\.var2a\s*ld \(hl\),\$40.*?ld l,Item\.collisionType\s*res 7,\(hl\).*?add Object\.var2a.*?ld a,\$80\|ITEMCOLLISION_MYSTERY_SEED\s*ld \(de\),a') {
    throw 'PART_OWL_STATUE data, collision, counters, floor replacement, or TX_39 dispatch changed.'
}
$owlAnimationLabels = @([regex]::Matches(
    (Get-AssemblyLabelBody $partAnimationSource 'part13Animations'),
    '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
$owlOamLabels = @([regex]::Matches(
    (Get-AssemblyLabelBody $partAnimationSource 'part13OamDataPointers'),
    '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if ($owlAnimationLabels.Count -ne 2 -or $owlOamLabels.Count -ne 2) {
    throw "Expected two PART_OWL_STATUE animations / OAM pointers, parsed $($owlAnimationLabels.Count) / $($owlOamLabels.Count)."
}
$owlAnimations = [Collections.Generic.List[string]]::new()
for ($animationIndex = 0;
     $animationIndex -lt $owlAnimationLabels.Count;
     $animationIndex++) {
    $owlAnimationBody = Get-AssemblyLabelBody `
        $partAnimationSource $owlAnimationLabels[$animationIndex]
    $frame = [regex]::Match(
        $owlAnimationBody,
        '(?m)^\s*\.db \$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})')
    if (-not $frame.Success) {
        throw "Could not parse PART_OWL_STATUE animation $animationIndex."
    }
    $pointerIndex = [Convert]::ToInt32(
        $frame.Groups['offset'].Value, 16) / 2
    if ($pointerIndex -ne $animationIndex) {
        throw "PART_OWL_STATUE animation $animationIndex changed its OAM pointer."
    }
    $duration = [Convert]::ToInt32(
        $frame.Groups['duration'].Value, 16)
    $parameter = [Convert]::ToInt32(
        $frame.Groups['parameter'].Value, 16)
    $owlAnimations.Add(
        "$duration,$parameter@$(Resolve-Oam $partOamSource $owlOamLabels[$pointerIndex])~0")
}

$owlSparkleData = [regex]::Match(
    (Get-AssemblyLabelBody $interactionDataSource 'interaction84SubidData'),
    '(?m)^\s*m_InteractionSubidData \$(?<gfx>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})')
$owlSparkleAnimationAliases = [regex]::Match(
    $interactionAnimationSource,
    '(?ms)^interaction11Animations:\s*^interaction84Animations:\s*^interactiondeAnimations:\s*(?<body>(?:\s*\.dw\s+interactionAnimation[0-9a-f]+\s*\r?\n){5})')
$owlSparkleOamAliases = [regex]::Match(
    $interactionAnimationSource,
    '(?ms)^interaction11OamDataPointers:[^\r\n]*\r?\ninteraction84OamDataPointers:[^\r\n]*\r?\n(?<body>(?:\s*\.dw\s+interactionOamData[0-9a-f]+\s*\r?\n){11})')
if (-not $owlSparkleData.Success -or
    -not $owlSparkleAnimationAliases.Success -or
    -not $owlSparkleOamAliases.Success -or
    [Convert]::ToInt32($owlSparkleData.Groups['gfx'].Value, 16) -ne 0x6b -or
    [Convert]::ToInt32($owlSparkleData.Groups['tile'].Value, 16) -ne 0x0a -or
    [Convert]::ToInt32($owlSparkleData.Groups['flags'].Value, 16) -ne 0x01) {
    throw 'INTERAC_SPARKLE `$84:$00 data or pointer aliases changed.'
}
$owlSparkleAnimationLabels = @([regex]::Matches(
    $owlSparkleAnimationAliases.Groups['body'].Value,
    '(?m)^\s*\.dw\s+(?<label>interactionAnimation[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
$owlSparkleOamLabels = @([regex]::Matches(
    $owlSparkleOamAliases.Groups['body'].Value,
    '(?m)^\s*\.dw\s+(?<label>interactionOamData[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
$owlSparkleAnimationIndex =
    [Convert]::ToInt32($owlSparkleData.Groups['flags'].Value, 16) -band 0x0f
$owlSparkleFrames = [Collections.Generic.List[string]]::new()
$owlSparkleAnimationBody = Get-AssemblyLabelBody `
    $interactionAnimationSource `
    $owlSparkleAnimationLabels[$owlSparkleAnimationIndex]
$owlSparkleLoopLabel =
    $owlSparkleAnimationLabels[$owlSparkleAnimationIndex] + 'Loop'
if ($interactionAnimationSource -match
    "(?m)^$([regex]::Escape($owlSparkleLoopLabel)):$") {
    $owlSparkleAnimationBody += "`n" + (
        Get-AssemblyLabelBody `
            $interactionAnimationSource `
            $owlSparkleLoopLabel)
}
foreach ($frame in [regex]::Matches(
    $owlSparkleAnimationBody,
    '(?m)^\s*\.db \$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})')) {
    $pointerOffset = [Convert]::ToInt32(
        $frame.Groups['offset'].Value, 16)
    if (($pointerOffset -band 1) -ne 0 -or
        ($pointerOffset / 2) -ge $owlSparkleOamLabels.Count) {
        throw "INTERAC_SPARKLE `$84:$00 references invalid OAM offset `$$($pointerOffset.ToString('x2'))."
    }
    $duration = [Convert]::ToInt32(
        $frame.Groups['duration'].Value, 16)
    $parameter = [Convert]::ToInt32(
        $frame.Groups['parameter'].Value, 16)
    $owlSparkleFrames.Add(
        "$duration,$parameter@$(Resolve-Oam $interactionOamSource $owlSparkleOamLabels[$pointerOffset / 2])")
}
if ($owlSparkleFrames.Count -ne 5 -or
    $owlSparkleFrames[$owlSparkleFrames.Count - 1] -notmatch '^1,255@') {
    throw 'INTERAC_SPARKLE `$84:$00 terminal animation changed.'
}
$owlSparkleAnimation = ($owlSparkleFrames -join '|') + '~4'

$owlOffsetMatches = @([regex]::Matches(
    (Get-AssemblyLabelBody $owlCodeSource '@owlStatueSparkleOffset'),
    '(?m)^\s*\.db \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})'))
if ($owlOffsetMatches.Count -ne 6) {
    throw "Expected six PART_OWL_STATUE sparkle offsets, parsed $($owlOffsetMatches.Count)."
}
$owlSparkleOffsets = @($owlOffsetMatches | ForEach-Object {
    $rawY = [Convert]::ToInt32($_.Groups['y'].Value, 16)
    $rawX = [Convert]::ToInt32($_.Groups['x'].Value, 16)
    $y = if ($rawY -ge 0x80) { $rawY - 0x100 } else { $rawY }
    $x = if ($rawX -ge 0x80) { $rawX - 0x100 } else { $rawX }
    "$x,$y"
}) -join ';'
$owlSprite = $gfxNames[
    [Convert]::ToInt32($owlPartData.Groups['gfx'].Value, 16)]
$owlSparkleSprite = $gfxNames[
    [Convert]::ToInt32($owlSparkleData.Groups['gfx'].Value, 16)]
if ([string]::IsNullOrWhiteSpace($owlSprite) -or
    [string]::IsNullOrWhiteSpace($owlSparkleSprite)) {
    throw 'PART_OWL_STATUE or INTERAC_SPARKLE object GFX header was not resolved.'
}
Copy-EnemySprite $owlSprite
Copy-EnemySprite $owlSparkleSprite

$owlRows = [Collections.Generic.List[string]]::new()
$owlRows.Add(
    '# subid`ttext-id`tutf8-base64`tsprite`ttile-base`tpalette`tcollision-mode`tradius-y`tradius-x`tdamage`thealth`tidle-animation`tspeaking-animation`tfloor-tile`tfloor-collision`tmystery-collision`tactivation-counter`tspeaking-counter`ttext-counter`tsparkle-offsets`tsparkle-sprite`tsparkle-tile-base`tsparkle-palette`tsparkle-animation`tsource')
for ($subid = 0; $subid -lt 0x14; $subid++) {
    $textId = 0x3900 + $subid
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Missing PART_OWL_STATUE text TX_$($textId.ToString('x4'))."
    }
    $message = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    $owlRows.Add(
        "$($subid.ToString('x2'))`t$($textId.ToString('x4'))`t$message`t$owlSprite`t$($owlPartData.Groups['tile'].Value)`t$([Convert]::ToInt32($owlPartData.Groups['flags'].Value, 16) -band 7)`t$($owlPartData.Groups['collision'].Value)`t7`t7`t$($owlPartData.Groups['damage'].Value)`t$($owlPartData.Groups['health'].Value)`t$($owlAnimations[0])`t$($owlAnimations[1])`t00`t0f`t9a`t50`t30`t22`t$owlSparkleOffsets`t$owlSparkleSprite`t$($owlSparkleData.Groups['tile'].Value)`t$([Convert]::ToInt32($owlSparkleData.Groups['flags'].Value, 16) -shr 4 -band 7)`t$owlSparkleAnimation`tobject_code/common/parts/owlStatue.s:partCode13+TX_$($textId.ToString('x4'))")
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\owl_statues.tsv'),
    $owlRows)

$killPuffAnimationLabel = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $interactionAnimationSource 'interaction08Animations'),
        '(?m)^\s*\.dw\s+(?<label>interactionAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$killPuffOamLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $interactionAnimationSource 'interaction08OamDataPointers'),
        '(?m)^\s*\.dw\s+(?<label>interactionOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($killPuffAnimationLabel.Count -ne 1 -or $killPuffOamLabels.Count -ne 6) {
    throw "Expected one INTERAC_KILLENEMYPUFF animation and six OAM pointers, got $($killPuffAnimationLabel.Count) / $($killPuffOamLabels.Count)."
}
$killPuffFrames = [Collections.Generic.List[string]]::new()
foreach ($frame in [regex]::Matches(
    (Get-AssemblyLabelBody $interactionAnimationSource $killPuffAnimationLabel[0]),
    '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
)) {
    $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
    if (($parameter -band 0x80) -ne 0) { break }
    $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
    if ($pointerIndex -ge $killPuffOamLabels.Count) {
        throw "INTERAC_KILLENEMYPUFF references missing OAM pointer $pointerIndex."
    }
    $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
    $killPuffFrames.Add("$duration@$(Resolve-Oam $interactionOamSource $killPuffOamLabels[$pointerIndex])")
}
$killPuffAnimation = $killPuffFrames -join '|'
$killPuffDuration = ($killPuffFrames | ForEach-Object {
    [int](($_ -split '@')[0])
} | Measure-Object -Sum).Sum
if ($killPuffFrames.Count -ne 7 -or $killPuffDuration -ne 20) {
    throw 'INTERAC_KILLENEMYPUFF no longer has its original 7-frame / 20-update animation.'
}
$killPuffRows = @(
    "# tile-base`tpalette`tanimation",
    "$([Convert]::ToInt32($killPuffData.Groups['tile'].Value, 16))`t$([Convert]::ToInt32($killPuffData.Groups['flags'].Value, 16) -band 7)`t$killPuffAnimation"
)
Write-GeneratedTable(
    (Join-Path $destination 'effects\kill_enemy_puff.tsv'),
    $killPuffRows)

# PART_OCTOROK_PROJECTILE (`$18) uses the Octorok sprite sheet with a
# directionless flying-rock cell. On a solid-tile or sword collision it
# switches to animation 3, reverses direction, and bounces for `$20 updates.
$octorokProjectileData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) \$(?<radius>[0-9a-f]{2}) \$(?<damage>[0-9a-f]{2}) \$40 \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2}) \$00\s*; \$18'
)
if (-not $octorokProjectileData.Success -or
    [Convert]::ToInt32($octorokProjectileData.Groups['gfx'].Value, 16) -ne 0x8f -or
    [Convert]::ToInt32($octorokProjectileData.Groups['collision'].Value, 16) -ne 0x87 -or
    [Convert]::ToInt32($octorokProjectileData.Groups['radius'].Value, 16) -ne 0x22 -or
    [Convert]::ToInt32($octorokProjectileData.Groups['damage'].Value, 16) -ne 0xfc) {
    throw 'PART_OCTOROK_PROJECTILE no longer matches gfx `$8f, collision `$07, radius 2x2, and half-heart damage.'
}
$part18AnimationStart = $partAnimationSource.IndexOf('part18Animations:', [StringComparison]::Ordinal)
$part13AnimationStart = $partAnimationSource.IndexOf('part13Animations:', [StringComparison]::Ordinal)
$part18AnimationLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring(
            $part18AnimationStart, $part13AnimationStart - $part18AnimationStart),
        '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$part18OamStart = $partAnimationSource.IndexOf('part18OamDataPointers:', [StringComparison]::Ordinal)
$part0eOamStart = $partAnimationSource.IndexOf('part0eOamDataPointers:', [StringComparison]::Ordinal)
$part18OamLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring($part18OamStart, $part0eOamStart - $part18OamStart),
        '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($part18AnimationLabels.Count -ne 5 -or $part18OamLabels.Count -ne 6) {
    throw 'PART_OCTOROK_PROJECTILE animation/OAM pointer tables are incomplete.'
}
function Resolve-OctorokProjectileAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:partAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:part18OamLabels.Count) {
            throw "$label references missing Octorok-projectile OAM pointer $pointerIndex."
        }
        $frames.Add("$duration@$(Resolve-Oam $script:partOamSource $script:part18OamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}
$octorokProjectileNormal = Resolve-OctorokProjectileAnimation $part18AnimationLabels[0]
$octorokProjectileBounce = Resolve-OctorokProjectileAnimation $part18AnimationLabels[3]
if ($octorokProjectileNormal -ne '127@8,0,0,0;8,8,0,32' -or
    $octorokProjectileBounce -ne '127@8,0,2,0;8,8,2,32') {
    throw 'PART_OCTOROK_PROJECTILE flying/bounced visuals changed from the expected OAM records.'
}
$octorokProjectileTileBase = [Convert]::ToInt32(
    $octorokProjectileData.Groups['tile'].Value, 16)
$octorokProjectilePalette = [Convert]::ToInt32(
    $octorokProjectileData.Groups['flags'].Value, 16) -band 7
$octorokProjectileRows = @(
    "# sprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`tspeed-raw`tnormal-animation`tbounce-animation",
    "$($gfxNames[0x8f])`t$octorokProjectileTileBase`t$octorokProjectilePalette`t2`t2`t2`t80`t$octorokProjectileNormal`t$octorokProjectileBounce"
)
$octorokProjectilePath = Join-Path $destination 'effects\octorok_projectile.tsv'
Write-GeneratedTable(
    $octorokProjectilePath, $octorokProjectileRows)

# ENEMY_MASKED_MOBLIN (`$20:`$00) is created dynamically by the room 1:38
# Maku Sprout rescue script. Export its shared four-direction animation table
# and PART_ENEMY_ARROW (`$1a) here so the cutscene does not need room-local
# approximations of ordinary combat objects.
$maskedMoblinData = [regex]::Match(
    $enemyDataSource,
    '(?m)^\s*/\* 0x20 \*/ m_EnemyData \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) enemy20SubidData')
$maskedSubidStart = $enemyDataSource.IndexOf(
    'enemy20SubidData:', [StringComparison]::Ordinal)
$maskedSubidEnd = $enemyDataSource.IndexOf(
    'enemy21SubidData:', [StringComparison]::Ordinal)
$maskedMoblinSubid = @([regex]::Matches(
    $enemyDataSource.Substring(
        $maskedSubidStart, $maskedSubidEnd - $maskedSubidStart),
    '(?m)^\s*m_EnemySubidData \$(?<extra>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'))[0]
if (-not $maskedMoblinData.Success -or $null -eq $maskedMoblinSubid -or
    [Convert]::ToInt32($maskedMoblinData.Groups['gfx'].Value, 16) -ne 0x90 -or
    ([Convert]::ToInt32($maskedMoblinData.Groups['collision'].Value, 16) -band 0x7f) -ne 0x11 -or
    [Convert]::ToInt32($maskedMoblinSubid.Groups['extra'].Value, 16) -ne 0x0a) {
    throw 'ENEMY_MASKED_MOBLIN `$20:`$00 data changed.'
}
$maskedMoblinExtra = $extraEnemyRows[0x0a]
$maskedMoblinFlags = [Convert]::ToInt32(
    $maskedMoblinSubid.Groups['flags'].Value, 16)
$maskedAnimationStart = $enemyAnimationSource.IndexOf(
    'enemy20Animations:', [StringComparison]::Ordinal)
$maskedAnimationEnd = $enemyAnimationSource.IndexOf(
    'enemy0bAnimations:', [StringComparison]::Ordinal)
$maskedMoblinAnimationLabels = @([regex]::Matches(
    $enemyAnimationSource.Substring(
        $maskedAnimationStart, $maskedAnimationEnd - $maskedAnimationStart),
    '(?m)^\s*\.dw\s+(?<label>enemyAnimation[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
$maskedOamStart = $enemyAnimationSource.IndexOf(
    'enemy20OamDataPointers:', [StringComparison]::Ordinal)
$maskedOamEnd = $enemyAnimationSource.IndexOf(
    'enemy0bOamDataPointers:', [StringComparison]::Ordinal)
$maskedMoblinOamLabels = @([regex]::Matches(
    $enemyAnimationSource.Substring(
        $maskedOamStart, $maskedOamEnd - $maskedOamStart),
    '(?m)^\s*\.dw\s+(?<label>enemyOamData[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if ($maskedMoblinAnimationLabels.Count -lt 4 -or $maskedMoblinOamLabels.Count -lt 8) {
    throw 'ENEMY_MASKED_MOBLIN animation/OAM pointer tables are incomplete.'
}
function Resolve-MaskedMoblinAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:enemyAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})')) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:maskedMoblinOamLabels.Count) {
            throw "$label references missing masked-Moblin OAM pointer $pointerIndex."
        }
        $frames.Add("$duration@$(Resolve-Oam $script:enemyOamSource $script:maskedMoblinOamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}
$maskedMoblinAnimations = @($maskedMoblinAnimationLabels[0..3] |
    ForEach-Object { Resolve-MaskedMoblinAnimation $_ })
$maskedMoblinDamageByte = $maskedMoblinExtra.Damage
$maskedMoblinTileBase = ($maskedMoblinFlags -band 0x0f) * 2
$maskedMoblinPalette = ($maskedMoblinFlags -shr 4) -band 7
$maskedMoblinRadiusY = $maskedMoblinExtra.RadiusY
$maskedMoblinRadiusX = $maskedMoblinExtra.RadiusX
$maskedMoblinDamage = (0x100 - $maskedMoblinDamageByte) / 2
$maskedMoblinHealth = $maskedMoblinExtra.Health
$maskedMoblinRows = @(
    "# id`tsubid`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tspeed-raw`tmove-base`tmove-mask`tturn-wait`tup-animation`tright-animation`tdown-animation`tleft-animation",
    (@(
        '20', '00', $gfxNames[0x90], $maskedMoblinTileBase,
        $maskedMoblinPalette, $maskedMoblinRadiusY, $maskedMoblinRadiusX,
        $maskedMoblinDamage, $maskedMoblinHealth,
        0x14, 0x30, 0x3f, 0x08,
        $maskedMoblinAnimations[0], $maskedMoblinAnimations[1],
        $maskedMoblinAnimations[2], $maskedMoblinAnimations[3]
    ) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\masked_moblin.tsv'),
    $maskedMoblinRows)

$enemyArrowData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) \$(?<radius>[0-9a-f]{2}) \$(?<damage>[0-9a-f]{2}) \$40 \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2}) \$00\s*; \$1a')
if (-not $enemyArrowData.Success -or
    [Convert]::ToInt32($enemyArrowData.Groups['gfx'].Value, 16) -ne 0x8e -or
    [Convert]::ToInt32($enemyArrowData.Groups['collision'].Value, 16) -ne 0x86 -or
    [Convert]::ToInt32($enemyArrowData.Groups['damage'].Value, 16) -ne 0xfc) {
    throw 'PART_ENEMY_ARROW `$1a data changed.'
}
$arrowAnimationStart = $partAnimationSource.IndexOf(
    'part1aAnimations:', [StringComparison]::Ordinal)
$arrowAnimationEnd = $partAnimationSource.IndexOf(
    'part13Animations:', [StringComparison]::Ordinal)
$enemyArrowAnimationLabels = @([regex]::Matches(
    $partAnimationSource.Substring(
        $arrowAnimationStart, $arrowAnimationEnd - $arrowAnimationStart),
    '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
$arrowOamStart = $partAnimationSource.IndexOf(
    'part1aOamDataPointers:', [StringComparison]::Ordinal)
$arrowOamEnd = $partAnimationSource.IndexOf(
    'part19OamDataPointers:', [StringComparison]::Ordinal)
$enemyArrowOamLabels = @([regex]::Matches(
    $partAnimationSource.Substring(
        $arrowOamStart, $arrowOamEnd - $arrowOamStart),
    '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)') |
    ForEach-Object { $_.Groups['label'].Value })
if ($enemyArrowAnimationLabels.Count -lt 4 -or $enemyArrowOamLabels.Count -ne 4) {
    throw 'PART_ENEMY_ARROW animation/OAM pointer tables are incomplete.'
}
function Resolve-EnemyArrowAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:partAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})')) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:enemyArrowOamLabels.Count) {
            throw "$label references missing enemy-arrow OAM pointer $pointerIndex."
        }
        $frames.Add("$duration@$(Resolve-Oam $script:partOamSource $script:enemyArrowOamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}
$enemyArrowAnimations = @($enemyArrowAnimationLabels[0..4] |
    ForEach-Object { Resolve-EnemyArrowAnimation $_ })
$enemyArrowDamageByte = [Convert]::ToInt32(
    $enemyArrowData.Groups['damage'].Value, 16)
$enemyArrowTileBase = [Convert]::ToInt32(
    $enemyArrowData.Groups['tile'].Value, 16)
$enemyArrowPalette = [Convert]::ToInt32(
    $enemyArrowData.Groups['flags'].Value, 16) -band 7
$enemyArrowDamage = (0x100 - $enemyArrowDamageByte) / 2
$enemyArrowRows = @(
    "# sprite`ttile-base`tpalette`tdamage-quarters`tspeed-raw`tup-animation`tright-animation`tdown-animation`tleft-animation`tbounce-animation",
    (@(
        $gfxNames[0x8e],
        $enemyArrowTileBase, $enemyArrowPalette, $enemyArrowDamage, 0x50,
        $enemyArrowAnimations[0], $enemyArrowAnimations[1],
        $enemyArrowAnimations[2], $enemyArrowAnimations[3],
        $enemyArrowAnimations[4]
    ) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'effects\enemy_arrow.tsv'),
    $enemyArrowRows)

foreach ($spriteName in @($gfxNames[0x90], $gfxNames[0x8e])) {
    $sourceSprite = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
        ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$spriteName.png" } |
        Select-Object -First 1
    if ($null -eq $sourceSprite) { throw "Dynamic Maku rescue sprite not found: $spriteName.png" }
    Copy-Item -LiteralPath $sourceSprite.FullName `
        -Destination (Join-Path $destination "gfx\$spriteName.png") -Force
}

# Preserve the complete Ages enemy item-drop selection data used by
# decideItemDrop. The fixed binary layout is 144 enemy records, eight 8-byte
# probability masks, and sixteen 32-byte item sets (720 bytes total).
$treasureDropPath = Join-Path $Disassembly 'code\treasureAndDrops.s'
$treasureDropSource = Read-ImportText $treasureDropPath
$itemDropEnemyTable = @(Read-AssemblyLiteralValues `
    $treasureDropPath 'itemDropTables')
if ($itemDropEnemyTable.Count -ne 144 -or
    $itemDropEnemyTable[0x09] -ne 0x8e -or
    $itemDropEnemyTable[0x32] -ne 0xae) {
    throw "Expected 144 Ages enemy item-drop records with ENEMY_OCTOROK (`$09) = `$8e and ENEMY_KEESE (`$32) = `$ae."
}

$itemDropProbabilityBytes = [Collections.Generic.List[byte]]::new()
$probabilityTables = @{}
$probabilityAliases = [Collections.Generic.List[int]]::new()
$probabilityValues = [Collections.Generic.List[byte]]::new()
foreach ($node in Read-AssemblyLabelNodes $treasureDropPath 'itemDropProbabilityTable') {
    if ($node.Kind -eq 'Label' -and
        $node.Name -match '^@probability(?<index>[0-7])$') {
        if ($probabilityValues.Count -gt 0) {
            foreach ($alias in $probabilityAliases) {
                $probabilityTables[$alias] = $probabilityValues.ToArray()
            }
            $probabilityAliases.Clear()
            $probabilityValues.Clear()
        }
        $probabilityAliases.Add([int]$Matches['index'])
        continue
    }
    if ($probabilityAliases.Count -gt 0 -and
        $node.Kind -eq 'Data' -and $node.Name -ieq '.db') {
        foreach ($operand in $node.Operands) {
            $probabilityValues.Add([byte](Convert-AssemblyInteger $operand))
        }
    }
}
foreach ($alias in $probabilityAliases) {
    $probabilityTables[$alias] = $probabilityValues.ToArray()
}
foreach ($probability in 0..7) {
    $bytes = @($probabilityTables[$probability])
    if ($bytes.Count -ne 8) {
        throw "Item-drop probability $probability contains $($bytes.Count) bytes; expected 8."
    }
    foreach ($value in $bytes) { $itemDropProbabilityBytes.Add($value) }
}

$itemDropSetBytes = [Collections.Generic.List[byte]]::new()
foreach ($setIndex in 0..15) {
    $setLabel = "itemDropSet$($setIndex.ToString('X'))"
    $bytes = @(Read-AssemblyLiteralValues $treasureDropPath $setLabel)
    if ($bytes.Count -ne 32) {
        throw "$setLabel contains $($bytes.Count) bytes; expected 32."
    }
    foreach ($value in $bytes) { $itemDropSetBytes.Add($value) }
}

$itemDropSelectionBytes = [Collections.Generic.List[byte]]::new()
foreach ($value in $itemDropEnemyTable) { $itemDropSelectionBytes.Add($value) }
foreach ($value in $itemDropProbabilityBytes) { $itemDropSelectionBytes.Add($value) }
foreach ($value in $itemDropSetBytes) { $itemDropSelectionBytes.Add($value) }
if ($itemDropSelectionBytes.Count -ne 720) {
    throw "Generated item-drop selection data is $($itemDropSelectionBytes.Count) bytes; expected 720."
}
$itemDropSelectionPath = Join-Path $destination 'metadata\itemDrops.bin'
Write-GeneratedBytes($itemDropSelectionPath, $itemDropSelectionBytes.ToArray())

# Export the PART_ITEM_DROP (`$01) visual records. Its subid selects one of the
# sixteen sprite-data rows and one of the first sixteen part animations.
$itemDropPartData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) \$(?<radius>[0-9a-f]{2}) \$00 \$01 \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2}) \$00\s*; \$01'
)
if (-not $itemDropPartData.Success -or
    [Convert]::ToInt32($itemDropPartData.Groups['gfx'].Value, 16) -ne 0x78 -or
    [Convert]::ToInt32($itemDropPartData.Groups['collision'].Value, 16) -ne 0x01 -or
    [Convert]::ToInt32($itemDropPartData.Groups['radius'].Value, 16) -ne 0x44) {
    throw 'PART_ITEM_DROP no longer resolves to gfx `$78, collision `$01, and radius `$44.'
}
$itemDropBaseTile = [Convert]::ToInt32($itemDropPartData.Groups['tile'].Value, 16)
$itemDropCodeSource = Read-ImportText (Join-Path $Disassembly 'object_code\common\parts\itemDrop.s')
$itemDropMovementSource = Read-ImportText (Join-Path $Disassembly 'code\bank0.s')
$itemDropState0 = [regex]::Match(
    $itemDropCodeSource,
    '(?ms)^@state0:\s*(?<body>.*?)(?=^@state1:)'
).Groups['body'].Value
if (-not $itemDropState0 -or
    $itemDropState0 -notmatch
        '(?ms)ld l,Part\.speedZ\s+ld a,<\(-\$160\)\s+ldi \(hl\),a\s+ld \(hl\),>\(-\$160\)' -or
    $itemDropState0 -notmatch
        '(?ms)and TILESETFLAG_SIDESCROLL\s+jr z,@label_11_008\s+; Sidescrolling only\s+inc \(hl\)' -or
    $itemDropState0 -notmatch
        '(?ms)ld l,Part\.collisionType\s+set 7,\(hl\)\s+ld l,Part\.counter1\s+ld \(hl\),240' -or
    $itemDropState0 -notmatch
        '(?ms)call objectCheckIsOnHazard.*?; On water\s+ld e,Part\.var34\s+ld a,\$01\s+ld \(de\),a') {
    throw 'PART_ITEM_DROP state 0 no longer selects immediate side-scrolling state 2/collision/counter/water setup.'
}
if ($itemDropCodeSource -notmatch
        '(?ms)^itemDrop_checkSidescrollingConditions:.*?ret z ; Return if it''s ITEM_DROP_FAIRY.*?ld a,\$20\s+call objectUpdateSpeedZ_sidescroll\s+jr c,@checkY.*?ld b,\$01.*?ld \(hl\),b.*?ld \(hl\),\$00.*?^@checkY:.*?cp \$b0\s+ret c\s+pop hl\s+jp partDelete') {
    throw 'PART_ITEM_DROP side-scrolling state 2 no longer uses gravity `$20, its water speed clamp, and y `$b0 deletion.'
}
if ($itemDropCodeSource -notmatch
        '(?ms)^itemDrop_checkOnHazard:.*?ld e,Part\.var34.*?ld b,INTERAC_SPLASH\s+xor a\s+jr @onWaterSidescrolling.*?^@onWater:.*?and TILESETFLAG_SIDESCROLL\s+jr z,@replaceWithAnimation.*?ld a,\$01\s+^@onWaterSidescrolling:\s+ld \(de\),a') {
    throw 'PART_ITEM_DROP no longer uses var34 for side-scrolling water entry and exit splashes.'
}
if ($itemDropMovementSource -notmatch
        '(?ms)^objectUpdateSpeedZ_sidescroll:\s+ld b,\$06.*?^objectUpdateSpeedZ_sidescroll_givenYOffset:.*?bit 7,\(hl\)\s+jr nz,@notLanded.*?sub \$04.*?call checkTileCollisionAt_allowHoles\s+ret c.*?add \$07.*?call checkTileCollisionAt_allowHoles\s+ret c.*?^@notLanded:.*?call add16BitRefs.*?ldh a,\(<hFF8B\)\s+add \(hl\)') {
    throw 'objectUpdateSpeedZ_sidescroll no longer uses y+`$06, x-`$04/x+`$03 floor probes before Y/gravity integration.'
}
$itemDropSpriteBlock = [regex]::Match(
    $itemDropCodeSource,
    '(?ms)^@spriteData:\r?\n(?<body>.*?)(?=^;;)'
)
$itemDropSpriteRows = @(
    [regex]::Matches(
        $itemDropSpriteBlock.Groups['body'].Value,
        '(?m)^\s*\.db \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
    )
)
if ($itemDropSpriteRows.Count -ne 16) {
    throw "PART_ITEM_DROP spriteData contains $($itemDropSpriteRows.Count) rows; expected 16."
}

$part01AnimationStart = $partAnimationSource.IndexOf('part01Animations:', [StringComparison]::Ordinal)
$part02AnimationStart = $partAnimationSource.IndexOf('part02Animations:', [StringComparison]::Ordinal)
$part01AnimationLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring(
            $part01AnimationStart, $part02AnimationStart - $part01AnimationStart),
        '(?m)^\s*\.dw\s+(?<label>partAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$part01OamStart = $partAnimationSource.IndexOf('part01OamDataPointers:', [StringComparison]::Ordinal)
$part02OamStart = $partAnimationSource.IndexOf('part02OamDataPointers:', [StringComparison]::Ordinal)
$part01OamLabels = @(
    [regex]::Matches(
        $partAnimationSource.Substring($part01OamStart, $part02OamStart - $part01OamStart),
        '(?m)^\s*\.dw\s+(?<label>partOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($part01AnimationLabels.Count -lt 16 -or $part01OamLabels.Count -ne 4) {
    throw 'PART_ITEM_DROP animation/OAM pointer tables are incomplete.'
}

function Resolve-ItemDropAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:partAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
        if ($parameter -ne 0) { break }
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:part01OamLabels.Count) {
            throw "$label references missing PART_ITEM_DROP OAM pointer $pointerIndex."
        }
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $frames.Add("$duration@$(Resolve-Oam $script:partOamSource $script:part01OamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}

$itemDropVisualRows = [Collections.Generic.List[string]]::new()
$itemDropVisualRows.Add("# subid`ttile-base`tpalette`tanimation")
foreach ($subid in 0..15) {
    $spriteRow = $itemDropSpriteRows[$subid]
    $tileBase = $itemDropBaseTile +
        [Convert]::ToInt32($spriteRow.Groups['tile'].Value, 16)
    $palette = [Convert]::ToInt32($spriteRow.Groups['flags'].Value, 16) -band 7
    $animation = Resolve-ItemDropAnimation $part01AnimationLabels[$subid]
    if (-not $animation) { throw "PART_ITEM_DROP subid `$($subid.ToString('x2')) has no animation." }
    $itemDropVisualRows.Add("$subid`t$tileBase`t$palette`t$animation")
}
if ($itemDropVisualRows[2] -ne "1`t2`t5`t127@11,4,0,0" -or
    $itemDropVisualRows[3] -ne "2`t4`t0`t127@8,4,0,0" -or
    $itemDropVisualRows[4] -ne "3`t6`t5`t127@8,4,0,0") {
    throw 'Heart and rupee PART_ITEM_DROP visual records no longer match the original data.'
}
$itemDropVisualPath = Join-Path $destination 'effects\item_drops.tsv'
Write-GeneratedTable(
    $itemDropVisualPath, $itemDropVisualRows)

# Every object speed is one of 24 multiples of SPEED_20, and every movement
# angle indexes bank3.objectSpeedTable's signed 8.8 Y/X components. Import the
# complete table once so enemy movement and ITEM_DROP_FAIRY do not reconstruct
# any row with host floating-point trigonometry. The clean US ROM places the
# table at file offset $00c09b.
if ($itemDropCodeSource -notmatch
        '(?ms)^itemDrop_chooseRandomFairyMovement:.*?@speedTable:\s*\r?\n\s*\.db SPEED_40, SPEED_80, SPEED_c0, SPEED_100') {
    throw 'ITEM_DROP_FAIRY no longer selects the expected four-speed source table.'
}
$objectSpeedSource = Read-ImportText(
    (Join-Path $Disassembly 'code\bank3.s'))
if ($objectSpeedSource -notmatch
        '(?ms)^objectSpeedTable:\s*\r?\n\s*\.define TMP_SPEED \$20\s*\r?\n\s*\r?\n\s*\.rept 24\s*\r?\n.*?\.dwsin 090 7 11\.25 \(-TMP_SPEED\) 0.*?\.dwcos 090 7 11\.25 \(-TMP_SPEED\) 0.*?\.dwsin 270 7 11\.25 \(-TMP_SPEED\) 0.*?\.dwcos 270 7 11\.25 \(-TMP_SPEED\) 0.*?\.dwsin 090 7 11\.25 \(-TMP_SPEED\) 0.*?\.redefine TMP_SPEED TMP_SPEED\+\$20\s*\r?\n\s*\.endr') {
    throw 'bank3.objectSpeedTable no longer has the expected 24 signed 8.8 rows.'
}
$objectSpeedConstantSource = Read-ImportText(
    (Join-Path $Disassembly 'constants\common\objectSpeeds.s'))
$objectSpeedNames = @(
    '20', '40', '60', '80', 'a0', 'c0', 'e0', '100',
    '120', '140', '160', '180', '1a0', '1c0', '1e0', '200',
    '220', '240', '260', '280', '2a0', '2c0', '2e0', '300')
for ($speedIndex = 0; $speedIndex -lt $objectSpeedNames.Count; $speedIndex++) {
    $speedCode = ($speedIndex + 1) * 5
    $sourcePattern = '(?m)^\s*SPEED_' +
        [regex]::Escape($objectSpeedNames[$speedIndex]) +
        '\s+dsb 5\s*;\s*0x' + $speedCode.ToString('x2') + '\s*$'
    if ($objectSpeedConstantSource -notmatch $sourcePattern) {
        throw "Object speed SPEED_$($objectSpeedNames[$speedIndex]) no longer has code " +
            "`$$($speedCode.ToString('x2'))."
    }
}
$speedTableRomOffset = 0x00c09b
$speedTableSignature = @(
    0xe0, 0xff, 0xe1, 0xff, 0xe3, 0xff, 0xe6, 0xff,
    0xea, 0xff, 0xef, 0xff, 0xf4, 0xff, 0xfa, 0xff)
for ($index = 0; $index -lt $speedTableSignature.Count; $index++) {
    if ($romBytes[$speedTableRomOffset + $index] -ne
        $speedTableSignature[$index]) {
        throw 'Clean-ROM bank3.objectSpeedTable signature changed.'
    }
}
$objectSpeedRows = [Collections.Generic.List[string]]::new()
$objectSpeedRows.Add(
    "# speed-code`tspeed-fixed`tangle`ty-fixed`tx-fixed`tsource")
for ($speedIndex = 0; $speedIndex -lt $objectSpeedNames.Count; $speedIndex++) {
    $speedCode = ($speedIndex + 1) * 5
    $speedFixed = ($speedIndex + 1) * 0x20
    $rowOffset = $speedIndex * 0x50
    foreach ($angle in 0..31) {
        $offset = $speedTableRomOffset + $rowOffset + $angle * 2
        $y = [BitConverter]::ToInt16($romBytes, $offset)
        $x = [BitConverter]::ToInt16($romBytes, $offset + 0x10)
        $objectSpeedRows.Add((
            "$($speedCode.ToString('x2'))`t$speedFixed" +
            "`t$($angle.ToString('x2'))`t$y`t$x" +
            "`tbank3.objectSpeedTable:SPEED_$($objectSpeedNames[$speedIndex])"))
    }
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\object_speed_vectors.tsv'),
    $objectSpeedRows)

# objectGetRelativeAngle reduces the wrapped unsigned byte deltas to one of
# five bands in one of eight octants, then indexes pushDirectionData. Preserve
# that final source table beside objectSpeedTable so the game-wide runtime
# movement owner does not reconstruct angles with host trigonometry.
$bank0Path = Join-Path $Disassembly 'code\bank0.s'
$bank0Source = Read-ImportText $bank0Path
$relativeAngleRoutine = [regex]::Match(
    $bank0Source,
    '(?ms)^objectGetRelativeAngleWithTempVars:.*?(?=^pushDirectionData:)').Value
$relativeAngleBandPath =
    '(?ms)\s+ld c,e\s+ld b,\$00\s+srl a\s+srl a\s+srl a\s+add a\s+' +
    'ld l,a\s+cp h\s+jr nc,\+\+\s+' +
    'inc b\s+add l\s+cp h\s+jr nc,\+\+\s+' +
    'inc b\s+add l\s+cp h\s+jr nc,\+\+\s+' +
    'inc b\s+add l\s+cp h\s+jr nc,\+\+\s+inc b\s+\+\+\s+' +
    'ld a,c\s+add a\s+add a\s+add a\s+add b'
if ([string]::IsNullOrEmpty($relativeAngleRoutine) -or
    $relativeAngleRoutine -notmatch
        '(?ms)^objectGetRelativeAngleWithTempVars:\s*\r?\n\s*ld e,\$08.*?\s+ld hl,pushDirectionData\s*\r?\n\s*add hl,bc\s*\r?\n\s*ld a,\(hl\)\s*\r?\n\s*ret' -or
    $relativeAngleRoutine -notmatch $relativeAngleBandPath) {
    throw 'bank0.objectGetRelativeAngle integer decision path changed.'
}
$pushDirectionRows = @(Read-AssemblyDataDirectives `
    $bank0Path 'pushDirectionData' '.db')
$pushDirectionAngles = @($pushDirectionRows | ForEach-Object {
    $_.Operands | ForEach-Object { Convert-AssemblyInteger $_ }
})
if ($pushDirectionAngles.Count -ne 64) {
    throw "bank0.pushDirectionData has $($pushDirectionAngles.Count) bytes; expected 64."
}
$expectedPushDirections = @(
    0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x00, 0x00, 0x00,
    0x00, 0x1f, 0x1e, 0x1d, 0x1c, 0x00, 0x00, 0x00,
    0x08, 0x07, 0x06, 0x05, 0x04, 0x00, 0x00, 0x00,
    0x00, 0x01, 0x02, 0x03, 0x04, 0x00, 0x00, 0x00,
    0x18, 0x17, 0x16, 0x15, 0x14, 0x00, 0x00, 0x00,
    0x10, 0x11, 0x12, 0x13, 0x14, 0x00, 0x00, 0x00,
    0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x00, 0x00, 0x00,
    0x10, 0x0f, 0x0e, 0x0d, 0x0c, 0x00, 0x00, 0x00)
for ($index = 0; $index -lt $pushDirectionAngles.Count; $index++) {
    if ($pushDirectionAngles[$index] -ne $expectedPushDirections[$index]) {
        throw "bank0.pushDirectionData byte $index changed."
    }
}
$relativeAngleRows = [Collections.Generic.List[string]]::new()
$relativeAngleRows.Add("# octant`tband`tangle`tsource")
for ($index = 0; $index -lt $pushDirectionAngles.Count; $index++) {
    $octant = [int][Math]::Floor($index / 8)
    $band = $index % 8
    $relativeAngleRows.Add(
        "$octant`t$band`t$($pushDirectionAngles[$index].ToString('x2'))`t" +
        "bank0.pushDirectionData+$($index.ToString('x2'))")
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\object_relative_angles.tsv'),
    $relativeAngleRows)

# Retain the remaining lookup tables used by implemented enemy handlers in one
# ordered, source-addressed boundary. These tables are indexed directly by
# state/RNG/direction values in the original code; runtime code must not carry
# private copies whose order can drift from the disassembly.
$enemyBehaviorRows = [Collections.Generic.List[string]]::new()
$enemyBehaviorRows.Add(
    "# owner`ttable`tindex`tvalue-a`tvalue-b`tsource")

$enemySpeedCodes = @{}
for ($speedIndex = 0; $speedIndex -lt $objectSpeedNames.Count; $speedIndex++) {
    $enemySpeedCodes["SPEED_$($objectSpeedNames[$speedIndex])"] =
        ($speedIndex + 1) * 5
}

function Convert-EnemyBehaviorToken(
    [string]$token,
    [bool]$signedByte = $false) {
    if ($script:enemySpeedCodes.ContainsKey($token)) {
        return [int]$script:enemySpeedCodes[$token]
    }
    if ($token -match '^-\$(?<hex>[0-9a-f]+)$') {
        return -[Convert]::ToInt32($Matches['hex'], 16)
    }
    if ($token -match '^\$(?<hex>[0-9a-f]+)$') {
        $value = [Convert]::ToInt32($Matches['hex'], 16)
        if ($signedByte -and $value -ge 0x80) { return $value - 0x100 }
        return $value
    }
    return [int]$token
}

function Read-EnemyBehaviorValues(
    [string]$body,
    [bool]$signedByte = $false) {
    $values = [Collections.Generic.List[int]]::new()
    foreach ($directive in [regex]::Matches(
        $body,
        '(?m)^\s*\.db\s+(?<values>[^;\r\n]+)')) {
        foreach ($token in [regex]::Matches(
            $directive.Groups['values'].Value,
            'SPEED_[0-9a-z]+|-\$[0-9a-f]+|\$[0-9a-f]+|(?<![A-Za-z0-9_])-?\d+')) {
            $values.Add(
                (Convert-EnemyBehaviorToken $token.Value $signedByte))
        }
    }
    return @($values)
}

function Add-EnemyBehaviorValueTable(
    [string]$owner,
    [string]$table,
    [int[]]$values,
    [string]$source) {
    for ($index = 0; $index -lt $values.Count; $index++) {
        $script:enemyBehaviorRows.Add(
            "$owner`t$table`t$index`t$($values[$index])`t0`t" +
            "$source+$($index.ToString('x2'))")
    }
}

function Add-EnemyBehaviorPairTable(
    [string]$owner,
    [string]$table,
    [object[]]$pairs,
    [string[]]$sources) {
    if ($pairs.Count -ne $sources.Count) {
        throw "$owner/$table behavior pair source count changed."
    }
    for ($index = 0; $index -lt $pairs.Count; $index++) {
        $pair = $pairs[$index]
        $script:enemyBehaviorRows.Add(
            "$owner`t$table`t$index`t$($pair[0])`t$($pair[1])`t" +
            $sources[$index])
    }
}

function Add-EnemyBehaviorProfile(
    [string]$owner,
    [string]$table,
    [int[]]$values,
    [string]$source) {
    Add-EnemyBehaviorValueTable $owner $table $values $source
}

$keeseCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\keese.s')
$keeseDeceleration = [regex]::Match(
    $keeseCodeSource,
    '(?ms)^keese_updateDeceleration:.*?^@speeds:\s*(?<speeds>.*?)(?=^@bits:)' +
    '^@bits:\s*(?<bits>.*?)(?=^;;)')
$keeseSpeeds = @(
    Read-EnemyBehaviorValues $keeseDeceleration.Groups['speeds'].Value)
$keeseBits = @(
    Read-EnemyBehaviorValues $keeseDeceleration.Groups['bits'].Value)
if (-not $keeseDeceleration.Success -or
    ($keeseSpeeds -join ',') -ne '30,20,10,10,5,5,5,5' -or
    ($keeseBits -join ',') -ne '0,0,1,1,3,3,7,0') {
    throw 'Keese deceleration speed/animation lookup tables changed.'
}
Add-EnemyBehaviorValueTable 'keese' 'deceleration-speeds' $keeseSpeeds `
    'object_code/common/enemies/keese.s:keese_updateDeceleration@speeds'
Add-EnemyBehaviorValueTable 'keese' 'deceleration-animation-masks' $keeseBits `
    'object_code/common/enemies/keese.s:keese_updateDeceleration@bits'

$octorokCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\octorok.s')
$octorokCounterValues = @(
    Read-EnemyBehaviorValues (
        Get-AssemblyLabelBody $octorokCodeSource 'octorok_counter1Values'))
$octorokWalkValues = @(
    Read-EnemyBehaviorValues (
        Get-AssemblyLabelBody $octorokCodeSource 'octorok_walkCounterValues'))
if (($octorokCounterValues -join ',') -ne '30,45,60,75,45,60,75,90' -or
    ($octorokWalkValues -join ',') -ne '25,33,41,49') {
    throw 'Octorok counter lookup tables changed.'
}
Add-EnemyBehaviorValueTable 'octorok' 'counter-values' $octorokCounterValues `
    'object_code/common/enemies/octorok.s:octorok_counter1Values'
Add-EnemyBehaviorValueTable 'octorok' 'walk-counter-values' $octorokWalkValues `
    'object_code/common/enemies/octorok.s:octorok_walkCounterValues'

$leeverCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\leever.s')
$leeverCounterValues = @(
    Read-EnemyBehaviorValues (
        Get-AssemblyLabelBody $leeverCodeSource '@counter1Vals'))
$leeverSpawnOffsets = @(
    Read-EnemyBehaviorValues (
        Get-AssemblyLabelBody $leeverCodeSource '@@linkRelativeOffsets') $true)
if (($leeverCounterValues -join ',') -ne '16,48,80,112' -or
    ($leeverSpawnOffsets -join ',') -ne
        '-48,-64,-80,-80,3,4,5,5,48,64,80,80,-3,-4,-5,-5' -or
    $leeverCodeSource -notmatch
        '(?ms)^@state9:.*?ld \(hl\),SPEED_80.*?' +
        '^@backIntoGround:.*?ld \(hl\),SPEED_20.*?' +
        '^@setRandomHighCounter1:.*?and \$38\s+add \$70' -or
    $leeverCodeSource -notmatch
        '(?ms)^@chooseSpawnPosition:.*?ld a,\(wFrameCounter\)\s+and \$03.*?' +
        'add \(hl\).*?SMALL_ROOM_HEIGHT<<4.*?SMALL_ROOM_WIDTH') {
    throw 'Leever counters, Link-relative spawn offsets, or state operands changed.'
}
Add-EnemyBehaviorValueTable 'leever' 'underground-counters' `
    $leeverCounterValues `
    'object_code/common/enemies/leever.s:@counter1Vals'
Add-EnemyBehaviorValueTable 'leever' 'link-relative-offsets' `
    $leeverSpawnOffsets `
    'object_code/common/enemies/leever.s:@@linkRelativeOffsets'
Add-EnemyBehaviorProfile 'leever' 'state-profile' `
    @(0x14, 0x05, 0x38, 0x70, 0x04, 0x18) `
    'object_code/common/enemies/leever.s:state-entry-operands'

$sandCrabCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\sandCrab.s')
if ($sandCrabCodeSource -notmatch
        '(?ms)^@state8:.*?ldbc \$18,\$30.*?ld a,\$30\s+add c.*?' +
        'bit 3,b\s+ld a,SPEED_40.*?ld a,SPEED_100.*?' +
        '^@state9:.*?ecom_applyVelocityForSideviewEnemyNoHoles') {
    throw 'Sand Crab RNG masks, counters, directional speeds, or movement helper changed.'
}
Add-EnemyBehaviorProfile 'sand-crab' 'state-profile' `
    @(0x18, 0x30, 0x30, 0x0a, 0x28) `
    'object_code/common/enemies/sandCrab.s:state-entry-operands'

$boomerangMoblinCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\boomerangMoblin.s')
$boomerangMoblinCounterBlock = [regex]::Match(
    $boomerangMoblinCodeSource,
    '(?ms)^@counterVals:\s*(?<body>.*?)(?=\z|^[A-Za-z0-9_]+:)')
$boomerangMoblinCounters = @(
    Read-EnemyBehaviorValues $boomerangMoblinCounterBlock.Groups['body'].Value)
if (-not $boomerangMoblinCounterBlock.Success -or
    ($boomerangMoblinCounters -join ',') -ne '48,64,80,96') {
    throw 'Boomerang Moblin route-counter lookup table changed.'
}
Add-EnemyBehaviorValueTable 'boomerang-moblin' 'route-counters' `
    $boomerangMoblinCounters `
    'object_code/common/enemies/boomerangMoblin.s:@counterVals'

$partCommonCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\parts\commonCode.s')
$enemyArrowDirectionBlock = [regex]::Match(
    $partCommonCodeSource,
    '(?ms)^partCommon_setPositionOffsetAndRadiusFromAngle:.*?^@data:\s*' +
    '(?<body>.*?)(?=^;;)')
$enemyArrowDirectionValues = @(
    Read-EnemyBehaviorValues `
        $enemyArrowDirectionBlock.Groups['body'].Value $true)
if (-not $enemyArrowDirectionBlock.Success -or
    ($enemyArrowDirectionValues -join ',') -ne
        '-8,-5,6,3,2,8,3,6,8,5,6,3,2,-8,3,6') {
    throw 'Enemy-arrow directional offset/radius table changed.'
}
$enemyArrowOffsetPairs = @()
$enemyArrowRadiusPairs = @()
$enemyArrowOffsetSources = @()
$enemyArrowRadiusSources = @()
for ($direction = 0; $direction -lt 4; $direction++) {
    $base = $direction * 4
    $enemyArrowOffsetPairs += ,@(
        $enemyArrowDirectionValues[$base],
        $enemyArrowDirectionValues[$base + 1])
    $enemyArrowRadiusPairs += ,@(
        $enemyArrowDirectionValues[$base + 2],
        $enemyArrowDirectionValues[$base + 3])
    $enemyArrowOffsetSources +=
        "object_code/common/parts/commonCode.s:" +
        "partCommon_setPositionOffsetAndRadiusFromAngle@data+" +
        ($base.ToString('x2'))
    $enemyArrowRadiusSources +=
        "object_code/common/parts/commonCode.s:" +
        "partCommon_setPositionOffsetAndRadiusFromAngle@data+" +
        (($base + 2).ToString('x2'))
}
Add-EnemyBehaviorPairTable 'enemy-arrow' 'spawn-offsets' `
    $enemyArrowOffsetPairs $enemyArrowOffsetSources
Add-EnemyBehaviorPairTable 'enemy-arrow' 'collision-radii' `
    $enemyArrowRadiusPairs $enemyArrowRadiusSources

$giantGhiniChildCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\enemies\giantGhiniChild.s')
$giantGhiniChildOffsets = @(
    Read-EnemyBehaviorValues (
        Get-AssemblyLabelBody `
            $giantGhiniChildCodeSource 'giantGhiniChild_spawnOffsets') $true)
if (($giantGhiniChildOffsets -join ',') -ne '0,-24,-24,0,0,24') {
    throw 'Giant Ghini child spawn-offset table changed.'
}
# The parent allocates subids 3, 2, 1, so runtime spawn index 0 reads the
# source table's final pair, then the middle and first pairs.
$giantGhiniChildPairs = @(
    ,@($giantGhiniChildOffsets[4], $giantGhiniChildOffsets[5])
    ,@($giantGhiniChildOffsets[2], $giantGhiniChildOffsets[3])
    ,@($giantGhiniChildOffsets[0], $giantGhiniChildOffsets[1])
)
$giantGhiniChildSources = @(
    'object_code/ages/enemies/giantGhiniChild.s:giantGhiniChild_spawnOffsets+04',
    'object_code/ages/enemies/giantGhiniChild.s:giantGhiniChild_spawnOffsets+02',
    'object_code/ages/enemies/giantGhiniChild.s:giantGhiniChild_spawnOffsets+00'
)
Add-EnemyBehaviorPairTable 'giant-ghini-child' 'spawn-offsets' `
    $giantGhiniChildPairs $giantGhiniChildSources

$pumpkinHeadCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\enemies\pumpkinHead.s')
$pumpkinWalkDurations = @(
    Read-EnemyBehaviorValues (
        Get-AssemblyLabelBody `
            $pumpkinHeadCodeSource 'pumpkinHead_body_walkDurations'))
$pumpkinStompBlock = [regex]::Match(
    $pumpkinHeadCodeSource,
    '(?ms)^pumpkinHead_body_chooseRandomStompTimerAndCount:.*?' +
    '^@counter2Vals:\s*(?<body>.*?)(?=^;;)')
$pumpkinStompTimers = @(
    Read-EnemyBehaviorValues $pumpkinStompBlock.Groups['body'].Value)
$pumpkinHeadOffsetsBlock = [regex]::Match(
    $pumpkinHeadCodeSource,
    '(?ms)^pumpkinHead_head_state0a:.*?^@headZOffsets:\s*' +
    '(?<body>.*?)(?=^;;)')
$pumpkinHeadOffsetValues = @(
    Read-EnemyBehaviorValues `
        $pumpkinHeadOffsetsBlock.Groups['body'].Value $true)
if (($pumpkinWalkDurations -join ',') -ne
        '30,30,60,60,60,60,60,90,90,90,90,90,90,120,120,120' -or
    -not $pumpkinStompBlock.Success -or
    ($pumpkinStompTimers -join ',') -ne
        '90,120,120,120,150,150,150,180' -or
    -not $pumpkinHeadOffsetsBlock.Success -or
    ($pumpkinHeadOffsetValues -join ',') -ne '0,-16,1,-16,0,-17') {
    throw 'Pumpkin Head walk, stomp, or head-follow lookup table changed.'
}
Add-EnemyBehaviorValueTable 'pumpkin-head' 'walk-durations' `
    $pumpkinWalkDurations `
    'object_code/ages/enemies/pumpkinHead.s:pumpkinHead_body_walkDurations'
Add-EnemyBehaviorValueTable 'pumpkin-head' 'stomp-timers' `
    $pumpkinStompTimers `
    'object_code/ages/enemies/pumpkinHead.s:pumpkinHead_body_chooseRandomStompTimerAndCount@counter2Vals'
$pumpkinHeadOffsetPairs = @(
    ,@($pumpkinHeadOffsetValues[0], $pumpkinHeadOffsetValues[1])
    ,@($pumpkinHeadOffsetValues[2], $pumpkinHeadOffsetValues[3])
    ,@($pumpkinHeadOffsetValues[4], $pumpkinHeadOffsetValues[5])
)
$pumpkinHeadOffsetSources = @(
    'object_code/ages/enemies/pumpkinHead.s:pumpkinHead_head_state0a@headZOffsets+00',
    'object_code/ages/enemies/pumpkinHead.s:pumpkinHead_head_state0a@headZOffsets+02',
    'object_code/ages/enemies/pumpkinHead.s:pumpkinHead_head_state0a@headZOffsets+04'
)
Add-EnemyBehaviorPairTable 'pumpkin-head' 'head-offsets' `
    $pumpkinHeadOffsetPairs $pumpkinHeadOffsetSources

$pumpkinProjectileCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\parts\pumpkinHeadProjectile.s')
$pumpkinProjectileAngleBlock = [regex]::Match(
    $pumpkinProjectileCodeSource,
    '(?ms)^@table_7421:\s*(?<body>.*?)(?=^@table_7424:)')
$pumpkinProjectileAngles = @(
    Read-EnemyBehaviorValues `
        $pumpkinProjectileAngleBlock.Groups['body'].Value $true)
$pumpkinProjectileOriginBlock = [regex]::Match(
    $pumpkinProjectileCodeSource,
    '(?ms)^@table_7424:\s*(?<body>.*?)(?=^@state1:)')
$pumpkinProjectileOrigins = @(
    Read-EnemyBehaviorValues `
        $pumpkinProjectileOriginBlock.Groups['body'].Value $true)
if (-not $pumpkinProjectileAngleBlock.Success -or
    ($pumpkinProjectileAngles -join ',') -ne '0,2,-2' -or
    -not $pumpkinProjectileOriginBlock.Success -or
    ($pumpkinProjectileOrigins -join ',') -ne '-4,0,2,4,4,0,2,-4') {
    throw 'Pumpkin Head projectile angle/origin lookup tables changed.'
}
# The source creates the base projectile first, then subids 2 and 1.
$pumpkinProjectileSpawnAngles = @(
    $pumpkinProjectileAngles[0],
    $pumpkinProjectileAngles[2],
    $pumpkinProjectileAngles[1])
$pumpkinProjectileSpawnAngleSources = @(
    'object_code/ages/parts/pumpkinHeadProjectile.s:@table_7421+00',
    'object_code/ages/parts/pumpkinHeadProjectile.s:@table_7421+02',
    'object_code/ages/parts/pumpkinHeadProjectile.s:@table_7421+01')
for ($index = 0; $index -lt 3; $index++) {
    $enemyBehaviorRows.Add(
        "pumpkin-head`tprojectile-angle-offsets`t$index`t" +
        "$($pumpkinProjectileSpawnAngles[$index])`t0`t" +
        $pumpkinProjectileSpawnAngleSources[$index])
}
$pumpkinProjectileOriginPairs = @()
$pumpkinProjectileOriginSources = @()
for ($direction = 0; $direction -lt 4; $direction++) {
    $base = $direction * 2
    $pumpkinProjectileOriginPairs += ,@(
        $pumpkinProjectileOrigins[$base],
        $pumpkinProjectileOrigins[$base + 1])
    $pumpkinProjectileOriginSources +=
        "object_code/ages/parts/pumpkinHeadProjectile.s:@table_7424+" +
        ($base.ToString('x2'))
}
Add-EnemyBehaviorPairTable 'pumpkin-head' 'projectile-origin-offsets' `
    $pumpkinProjectileOriginPairs $pumpkinProjectileOriginSources

# State-entry operands are data too: handlers write these speeds, counters,
# gravity values, bounds, and radii into object fields, while collisionEffects
# supplies the common sword response counters. Keep them beside the lookup
# streams so C# state machines retain control flow without private data copies.
$collisionEffectsSource = Read-ImportText (
    Join-Path $Disassembly 'code\collisionEffects.s')
$enemySwordDamageBlock = [regex]::Match(
    $collisionEffectsSource,
    '(?ms)^applyDamageToEnemyOrPart:.*?^@damageTypeTable:\s*' +
    '(?<body>.*?)(?=^@soundEffects:)')
$enemySwordDamageValues = @(
    Read-EnemyBehaviorValues $enemySwordDamageBlock.Groups['body'].Value)
if (-not $enemySwordDamageBlock.Success -or
    $enemySwordDamageValues.Count -lt 16 -or
    ($enemySwordDamageValues[0..15] -join ',') -ne
        '241,16,8,0,241,21,11,0,241,26,15,0,241,32,0,0') {
    throw 'collisionEffects enemy sword damage profiles changed.'
}
$enemySwordDamagePairs = @()
$enemySwordDamageSources = @()
for ($index = 0; $index -lt 4; $index++) {
    $base = $index * 4
    $enemySwordDamagePairs += ,@(
        $enemySwordDamageValues[$base + 1],
        $enemySwordDamageValues[$base + 2])
    $enemySwordDamageSources +=
        "code/collisionEffects.s:applyDamageToEnemyOrPart@" +
        "damageTypeTable+$($base.ToString('x2'))"
}
Add-EnemyBehaviorPairTable 'common-enemy' 'sword-damage-profiles' `
    $enemySwordDamagePairs $enemySwordDamageSources

if ($enemyCommonCodeSource -notmatch
        '(?ms)^ecom_updateKnockback_common:.*?' +
        'ld b,SPEED_200.*?ld b,SPEED_300' -or
    $enemyCommonCodeSource -notmatch
        '(?ms)^ecom_checkHazardsCommon:.*?' +
        'ld bc,\$05ff.*?ld bc,\$0501.*?' +
        'ld \(hl\),60.*?^ecom_fallingInHole:.*?' +
        'and \$07.*?ld b,SPEED_80.*?sub \$03') {
    throw 'Common enemy knockback or hazard profile changed.'
}
Add-EnemyBehaviorProfile 'common-enemy' 'knockback-speeds' `
    @(0x50, 0x78) `
    'object_code/common/enemies/commonCode.s:ecom_updateKnockback_common'
Add-EnemyBehaviorProfile 'common-enemy' 'hazard-profile' `
    @(5, -1, 1, 60, 7, 0x14, 3) `
    'object_code/common/enemies/commonCode.s:ecom_checkHazardsCommon'

if ($enemyCommonCodeSource -notmatch
        '(?ms)^ecom_checkScentSeedActive:.*?' +
        'bit 4,a.*?ld e,Enemy\.state.*?and \$f8.*?' +
        'ld a,\$04.*?ld \(de\),a' -or
    $enemyCommonCodeSource -notmatch
        '(?ms)^ecom_updateAngleToScentSeed:.*?' +
        'ld l,Enemy\.var3d.*?dec \(hl\).*?and \$0f.*?' +
        'call objectGetRelativeAngle.*?ld \(de\),a') {
    throw 'Common enemy Scent Seed attraction state/angle cadence changed.'
}
Add-EnemyBehaviorProfile 'common-enemy' 'scent-attraction-profile' `
    @(0x04, 0x0f, 0x04, 0x18) `
    'object_code/common/enemies/commonCode.s:ecom_checkScentSeedActive'

if ($partCommonCodeSource -notmatch
        '(?ms)^partCommon_bounceWhenCollisionsEnabled:.*?' +
        'ld bc,-\$e0.*?ld \(hl\),\$20.*?' +
        'ld \(hl\),SPEED_40.*?^partCommon_updateSpeedAndDeleteWhenCounter1Is0:.*?' +
        'ld c,\$0e') {
    throw 'Common hostile-projectile bounce profile changed.'
}
Add-EnemyBehaviorProfile 'common-projectile' 'bounce-profile' `
    @(0x20, 0x0e, 0x0a, -0xe0) `
    'object_code/common/parts/commonCode.s:partCommon_bounceWhenCollisionsEnabled'

if ($keeseCodeSource -notmatch
        '(?ms)^keese_subid00_state8:.*?' +
        'ld \(hl\),SPEED_c0.*?ld a,\$c0.*?' +
        '^keese_subid00_stateA:.*?cp \$68.*?ld a,\$7f.*?' +
        'and \$7f.*?add \$20' -or
    $keeseCodeSource -notmatch
        '(?ms)^keese_subid01_state8:.*?ld c,\$31.*?' +
        'ld \(hl\),SPEED_100.*?ld \(hl\),12.*?ld \(hl\),12' -or
    $keeseCodeSource -notmatch
        '(?ms)^keese_initializeSubid:.*?ld \(hl\),\$20') {
    throw 'Keese state-entry behavior profile changed.'
}
Add-EnemyBehaviorProfile 'keese' 'state-profile' `
    @(0x1e, 0x28, 0x20, 0x31, 12, 12, 0xc0, 0x3f,
      0x68, 0x7f, 0x20, 0x7f) `
    'object_code/common/enemies/keese.s:state-entry-operands'

$arrowDarknutCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\arrowDarknut.s')
$moblinSharedCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\moblinsAndShroudedStalfos.s')
if ($arrowDarknutCodeSource -notmatch
        '(?ms)^arrowDarknut_state_uninitialized:.*?ld a,SPEED_80' -or
    $arrowDarknutCodeSource -notmatch
        '(?ms)^arrowDarknut_setState8WithRandomAngleAndCounter:.*?' +
        'and \$3f\s+add \$30' -or
    $moblinSharedCodeSource -notmatch
        '(?ms)^moblin_state_8:.*?ld \(hl\),\$08') {
    throw 'Arrow Moblin state-entry behavior profile changed.'
}
Add-EnemyBehaviorProfile 'arrow-moblin' 'state-profile' `
    @(0x14, 0x30, 0x3f, 0x08) `
    'object_code/common/enemies/arrowDarknut.s:state-entry-operands'

$babyCuccoCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\babyCucco.s')
if ($babyCuccoCodeSource -notmatch
        '(?ms)^babyCucco_state_uninitialized:\s+ld a,SPEED_40\s+jp ecom_setSpeedAndState8AndVisible' -or
    $babyCuccoCodeSource -notmatch
        '(?ms)^babyCucco_state8:\s+call objectAddToGrabbableObjectBuffer\s+call objectSetPriorityRelativeToLink_withTerrainEffects\s+call ecom_updateAngleTowardTarget\s+call babyCucco_updateAnimationFromAngle\s+ld c,\$10\s+call objectCheckLinkWithinDistance\s+jr nc,@moveCloserToLink.*?call getRandomNumber_noPreserveVars\s+and \$3f\s+ret nz.*?ld a,<\(\$ff40\).*?^@moveCloserToLink:\s+call ecom_applyVelocityForSideviewEnemyNoHoles.*?enemyAnimate' -or
    $babyCuccoCodeSource -notmatch
        '(?ms)^babyCucco_state9:\s+ld c,\$12\s+call objectUpdateSpeedZ_paramC\s+jr nz,babyCucco_animate.*?dec \(hl\)\s+ret' -or
    $babyCuccoCodeSource -notmatch
        '(?ms)^babyCucco_updateAnimationFromAngle:\s+ld e,Enemy\.angle\s+ld a,\(de\)\s+cp \$10\s+ld a,\$01.*?xor a.*?jp enemySetAnimation' -or
    $babyCuccoCodeSource -notmatch
        '(?ms)^babyCucco_state_grabbed:.*?^@justGrabbed:.*?res 7,\(hl\).*?wLinkGrabState2.*?objectSetVisiblec1.*?^@beingHeld:.*?enemySetAnimation.*?^@released:.*?SMALL_ROOM_HEIGHT<<4.*?SMALL_ROOM_WIDTH<<4.*?^@landed:.*?ld \(hl\),\$08.*?set 7,\(hl\).*?objectSetVisiblec2') {
    throw 'Baby Cucco movement, shared RNG, hopping, or bracelet state path changed.'
}
Add-EnemyBehaviorProfile 'baby-cucco' 'state-profile' `
    @(0x0a, 0x10, 0x3f, -0xc0, 0x12, 0x10) `
    'object_code/common/enemies/babyCucco.s:state-entry-operands'

$cuccoCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\cucco.s')
$giantCuccoCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\giantCucco.s')
$collisionEffectsSource = Read-ImportText (
    Join-Path $Disassembly 'code\collisionEffects.s')
if ($cuccoCodeSource -notmatch
        '(?ms)^cucco_state_uninitialized:.*?ld a,SPEED_80.*?ecom_setSpeedAndState8AndVisible' -or
    $cuccoCodeSource -notmatch
        '(?ms)^cucco_state8:.*?ld bc,\$031f.*?ecom_randomBitwiseAndBCE.*?or e\s+ret nz.*?ld a,\$02\s+add b.*?ld \(hl\),a.*?ld a,c.*?cucco_setAnimationFromAngle' -or
    $cuccoCodeSource -notmatch
        '(?ms)^cucco_state9:.*?and \$0f.*?cucco_zVals.*?ecom_decCounter2.*?ecom_bounceOffWallsAndHoles.*?objectApplySpeed' -or
    $cuccoCodeSource -notmatch
        '(?ms)^cucco_stateA:.*?ecom_updateCardinalAngleAwayFromTarget.*?ecom_applyVelocityForSideviewEnemyNoHoles' -or
    $giantCuccoCodeSource -notmatch
        '(?ms)^cucco_zVals:\s+\.db \$00 \$ff \$ff \$fe \$fe \$fe \$fd \$fd\s+\.db \$fd \$fd \$fe \$fe \$fe \$ff \$ff \$00' -or
    $giantCuccoCodeSource -notmatch
        '(?ms)^cucco_checkSpawnCuccoAttacker:.*?cp \$10.*?@var33Vals:\s+\.db \$1e \$1a \$18 \$16 \$14 \$12 \$10 \$0e\s+\.db \$0c' -or
    $giantCuccoCodeSource -notmatch
        '(?ms)^cucco_attacked:.*?ld \(hl\),SPEED_100.*?bit 5,\(hl\).*?inc \(hl\).*?SND_CHICKEN' -or
    $giantCuccoCodeSource -notmatch
        '(?ms)^cucco_hitWithMysterySeed:.*?cp \$10.*?ld a,ENEMY_GIANT_CUCCO.*?ld a,ENEMY_BABY_CUCCO' -or
    $giantCuccoCodeSource -notmatch
        '(?ms)^enemyCode3b:.*?cp ITEMCOLLISION_L1_SWORD.*?ld l,Enemy\.var30\s+inc \(hl\).*?ld l,Enemy\.health\s+ld \(hl\),\$40' -or
    $collisionEffectsSource -notmatch
        '(?ms)^applyDamageToEnemyOrPart:.*?; If health reaches zero, disable collisions.*?add Object\.collisionType-Object\.health.*?res 7,a' -or
    $giantCuccoCodeSource -notmatch
        '(?ms)^giantCucco_state_uninitialized:.*?ld a,SPEED_c0.*?ld a,\$30.*?setScreenShakeCounter' -or
    $giantCuccoCodeSource -notmatch
        '(?ms)^giantCucco_stateA:.*?cp \$08.*?SND_TELEPORT.*?^@runAway:.*?ecom_updateCardinalAngleAwayFromTarget.*?^giantCucco_stateB:.*?objectGetAngleTowardEnemyTarget.*?objectNudgeAngleTowards.*?objectApplySpeed') {
    throw 'Cucco wandering, hit, or revenge-spawn behavior changed.'
}
Add-EnemyBehaviorProfile 'cucco' 'state-profile' `
    @(0x14, 0x28, 0x3f, 0x02, 0x03, 0x1f, 0x10, 0x33, 0x3b) `
    'object_code/common/enemies/cucco.s:state-operands'
Add-EnemyBehaviorValueTable 'cucco' 'hop-z-values' `
    @(0, -1, -1, -2, -2, -2, -3, -3,
      -3, -3, -2, -2, -2, -1, -1, 0) `
    'object_code/common/enemies/giantCucco.s:cucco_zVals'
Add-EnemyBehaviorValueTable 'cucco' 'revenge-delays' `
    @(0x1e, 0x1a, 0x18, 0x16, 0x14, 0x12, 0x10, 0x0e, 0x0c) `
    'object_code/common/enemies/giantCucco.s:@var33Vals'
Add-EnemyBehaviorProfile 'giant-cucco' 'state-profile' `
    @(0x1e, 0x30, 0x40) `
    'object_code/common/enemies/giantCucco.s:state-operands'
$cuccoAttackerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\parts\cuccoAttacker.s')
if ($cuccoAttackerSource -notmatch
        '(?ms)^@state0:.*?ld \(hl\),\$18.*?ld \(hl\),\$fa.*?@speedVals.*?getRandomNumber_noPreserveVars.*?and \$30.*?swap b.*?and \$10.*?@xOrYVals.*?and \$0f.*?@screenEdgePositions.*?objectGetAngleTowardEnemyTarget' -or
    $cuccoAttackerSource -notmatch
        '(?ms)^@state1:.*?partCommon_decCounter1IfNonzero.*?^@state2:.*?objectCheckWithinScreenBoundary.*?partDelete' -or
    $cuccoAttackerSource -notmatch
        '(?ms)^@screenEdgePositions:\s+\.db \$08 \$98 \$88 \$08\s+^@xOrYVals:\s+\.db \$05 \$0e \$17 \$20 \$29 \$32 \$3b \$44\s+\.db \$4d \$56 \$5f \$68 \$71 \$7a \$83 \$8c\s+\.db \$05 \$0f \$19 \$23 \$2d \$37 \$41 \$4b\s+\.db \$55 \$5f \$69 \$73 \$7d \$87 \$91 \$9b' -or
    $cuccoAttackerSource -notmatch
        '(?ms)^@speedVals:\s+\.db SPEED_140 SPEED_180 SPEED_1c0 SPEED_200\s+\.db SPEED_240 SPEED_240 SPEED_280 SPEED_2c0\s+\.db SPEED_300' -or
    $partAnimationSource -notmatch
        '(?ms)^part22Animations:.*?partAnimation5b98c' ) {
    throw 'PART_CUCCO_ATTACKER movement behavior changed.'
}
Add-EnemyBehaviorProfile 'cucco-attacker' 'state-profile' `
    @(0x18, -6, 0x30, 0x10, 0x0f, 0x11, 4) `
    'object_code/common/parts/cuccoAttacker.s:state-operands'
Add-EnemyBehaviorValueTable 'cucco-attacker' 'screen-edge-positions' `
    @(0x08, 0x98, 0x88, 0x08) `
    'object_code/common/parts/cuccoAttacker.s:@screenEdgePositions'
Add-EnemyBehaviorValueTable 'cucco-attacker' 'edge-axis-values' `
    @(0x05, 0x0e, 0x17, 0x20, 0x29, 0x32, 0x3b, 0x44,
      0x4d, 0x56, 0x5f, 0x68, 0x71, 0x7a, 0x83, 0x8c,
      0x05, 0x0f, 0x19, 0x23, 0x2d, 0x37, 0x41, 0x4b,
      0x55, 0x5f, 0x69, 0x73, 0x7d, 0x87, 0x91, 0x9b) `
    'object_code/common/parts/cuccoAttacker.s:@xOrYVals'
Add-EnemyBehaviorValueTable 'cucco-attacker' 'speeds' `
    @(0x32, 0x3c, 0x46, 0x50, 0x5a, 0x5a, 0x64, 0x6e, 0x78) `
    'object_code/common/parts/cuccoAttacker.s:@speedVals'

$crowCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\crows.s')
if ($crowCodeSource -notmatch
        '(?ms)^crow_subid0_state8:.*?add \$30\s+cp \$61.*?' +
        'add \$18\s+cp \$31.*?ld \(hl\),25' -or
    $crowCodeSource -notmatch
        '(?ms)^crow_subid0_state9:.*?ld \(hl\),90' -or
    $crowCodeSource -notmatch
        '(?ms)^crow_subid0_checkWithinScreenBounds:.*?' +
        'cp \(SMALL_ROOM_HEIGHT<<4\) \+ 8.*?' +
        'cp \(SMALL_ROOM_WIDTH<<4\) \+ 8') {
    throw 'Crow approach, timing, or boundary profile changed.'
}
Add-EnemyBehaviorProfile 'crow' 'state-profile' `
    @(0x30, 0x18, 25, 90, 0x88, 0xa8) `
    'object_code/common/enemies/crows.s:crow_subid0'

$gelCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\gel.s')
if ($gelCodeSource -notmatch
        '(?ms)^gel_state_uninitialized:.*?ld a,\$10' -or
    $gelCodeSource -notmatch
        '(?ms)^gel_state8:.*?ld \(hl\),\$30.*?' +
        'ld \(hl\),\$08.*?ld \(hl\),SPEED_40' -or
    $gelCodeSource -notmatch
        '(?ms)^gel_stateB:.*?ld c,\$28' -or
    $gelCodeSource -notmatch
        '(?ms)^gel_stateC:.*?ld \(hl\),120' -or
    $gelCodeSource -notmatch
        '(?ms)^gel_beginHop:.*?ld bc,-\$200.*?' +
        'ld \(hl\),SPEED_100') {
    throw 'Gel state-entry behavior profile changed.'
}
Add-EnemyBehaviorProfile 'gel' 'state-profile' `
    @(0x10, 0x30, 0x08, 0x0a, 0x28, -0x200, 0x28, 120) `
    'object_code/common/enemies/gel.s:state-entry-operands'

$zolCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\zol.s')
if ($zolCodeSource -notmatch
        '(?ms)^zol_state_uninitialized:.*?ld a,SPEED_c0.*?' +
        'ld \(hl\),\$18' -or
    $zolCodeSource -notmatch
        '(?ms)^zol_subid00_state8:.*?ld c,\$28.*?' +
        'ld bc,-\$200.*?ld \(hl\),\$04' -or
    $zolCodeSource -notmatch
        '(?ms)^zol_subid00_state9:.*?ld c,\$28.*?ld \(hl\),\$30' -or
    $zolCodeSource -notmatch
        '(?ms)^zol_subid00_stateB:.*?ld c,\$28.*?ld \(hl\),\$30' -or
    $zolCodeSource -notmatch
        '(?ms)^zol_subid00_stateC:.*?ld \(hl\),40' -or
    $zolCodeSource -notmatch
        '(?ms)^zol_subid01_state8:.*?ld \(hl\),\$10.*?' +
        'ld \(hl\),SPEED_80.*?ld \(hl\),\$20' -or
    $zolCodeSource -notmatch
        '(?ms)^zol_subid01_stateA:.*?ld \(hl\),<\(-\$200\).*?' +
        'ld \(hl\),SPEED_100' -or
    $zolCodeSource -notmatch
        '(?ms)^zol_subid01_stateB:.*?ld \(hl\),\$18' -or
    $zolCodeSource -notmatch
        '(?ms)^zol_subid01_stateC:.*?ld \(hl\),18') {
    throw 'Zol state-entry behavior profile changed.'
}
Add-EnemyBehaviorProfile 'zol' 'state-profile' `
    @(0x28, -0x200, 0x28, 0x18, 4, 0x30, 0x1e, 40,
      0x10, 0x14, 0x20, 0x28, 0x18, 18) `
    'object_code/common/enemies/zol.s:state-entry-operands'

if ($octorokCodeSource -notmatch
        '(?ms)^octorok_state_08:.*?ld \(hl\),\$10' -or
    $octorokCodeSource -notmatch
        '(?ms)^octorok_state_0b:.*?ld \(hl\),\$20') {
    throw 'Octorok shooting-counter profile changed.'
}
Add-EnemyBehaviorProfile 'octorok' 'state-profile' `
    @(0x10, 0x20) `
    'object_code/common/enemies/octorok.s:shooting-state-operands'

if ($boomerangMoblinCodeSource -notmatch
        '(?ms)^@state_uninitialized:.*?ld a,SPEED_80') {
    throw 'Boomerang Moblin movement-speed profile changed.'
}
Add-EnemyBehaviorProfile 'boomerang-moblin' 'state-profile' `
    @(0x14) `
    'object_code/common/enemies/boomerangMoblin.s:@state_uninitialized'

$ropeCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\rope.s')
if ($ropeCodeSource -notmatch
        '(?ms)^@state_uninitialized:.*?ld a,SPEED_60.*?' +
        'ecom_setSpeedAndState8AndVisible' -or
    $ropeCodeSource -notmatch
        '(?ms)^rope_state_moveAround:.*?ld b,\$0a.*?' +
        'ld \(hl\),SPEED_140' -or
    $ropeCodeSource -notmatch
        '(?ms)^rope_state_chargeLink:.*?ld \(hl\),SPEED_60.*?' +
        'ld \(hl\),\$40' -or
    $ropeCodeSource -notmatch
        '(?ms)^rope_changeDirection:.*?ldbc \$18,\$70.*?add \$70') {
    throw 'Rope state-entry behavior profile changed.'
}
Add-EnemyBehaviorProfile 'rope' 'state-profile' `
    @(0x0f, 0x32, 0x0f, 0x40, 0x0a, 0x70, 0x70) `
    'object_code/common/enemies/rope.s:subid00-state-operands'

$polsVoiceCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\polsVoice.s')
if ($polsVoiceCodeSource -notmatch
        '(?ms)^enemyCode23:.*?ecom_checkHazardsNoAnimationForHoles.*?' +
        'polsVoice_checkLinkPlayingInstrument.*?ENEMYSTATUS_NO_HEALTH.*?' +
        'enemyDie' -or
    $polsVoiceCodeSource -notmatch
        '(?ms)^polsVoice_state_uninitialized:.*?' +
        'getRandomNumber_noPreserveVars.*?and \$3f\s+inc a.*?' +
        'polsVoice_setLandedAnimation' -or
    $polsVoiceCodeSource -notmatch
        '(?ms)^polsVoice_state8:.*?ld bc,\$0f1c.*?' +
        'ecom_randomBitwiseAndBCE.*?^@jumpSpeeds1:\s*' +
        'dwbb -\$128, \$0c, SPEED_80\s*^@jumpSpeeds2:\s*' +
        'dwbb -\$180, \$0c, SPEED_c0' -or
    $polsVoiceCodeSource -notmatch
        '(?ms)cp SPEED_80.*?objectGetAngleTowardEnemyTarget.*?' +
        'add \$02\s+and \$1c' -or
    $polsVoiceCodeSource -notmatch
        '(?ms)^polsVoice_state9:.*?' +
        'ecom_applyVelocityForSideviewEnemyNoHoles.*?' +
        'objectUpdateSpeedZ_paramC.*?ld \(hl\),\$20.*?' +
        'polsVoice_setLandedAnimation' -or
    $polsVoiceCodeSource -notmatch
        '(?ms)^polsVoice_checkLinkPlayingInstrument:.*?' +
        'wLinkPlayingInstrument.*?ENEMYSTATUS_NO_HEALTH') {
    throw 'Pols Voice RNG, jump, terrain, landing, or instrument response changed.'
}
Add-EnemyBehaviorProfile 'pols-voice' 'state-profile' `
    @(0x3f, 1, 0x0f, 0x1c,
      -0x128, 0x0c, 0x14,
      -0x180, 0x0c, 0x1e,
      2, 0x1c, 0x20) `
    'object_code/common/enemies/polsVoice.s:state-entry-operands'
$polsVoiceCollisionEffects = @(
    0..0x1f | ForEach-Object {
        $enemyCollisionTableValues[0x21 * 0x20 + $_]
    })
$expectedPolsVoiceCollisionEffects = @(
    0x02, 0x0f, 0x0f, 0x0f, 0x0c, 0x0d, 0x0d, 0x0e,
    0x0e, 0x0c, 0x0c, 0x09, 0x0d, 0x0c, 0x0c, 0x25,
    0x00, 0x00, 0x00, 0x0d, 0x0d, 0x0d, 0x09, 0x0d,
    0x0a, 0x0d, 0x20, 0x20, 0x0d, 0x28, 0x29, 0x00)
if (($polsVoiceCollisionEffects -join ',') -ne
    ($expectedPolsVoiceCollisionEffects -join ',')) {
    throw 'ENEMYCOLLISION_POLS_VOICE `$21 item-effect row changed.'
}
Add-EnemyBehaviorProfile 'pols-voice' 'collision-effects' `
    $polsVoiceCollisionEffects `
    'data/ages/objectCollisionTable.s:objectCollisionTable+$0420'

$moldormCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\moldorm.s')
if ($moldormCodeSource -notmatch
        '(?ms)^moldorm_state1:.*?ld b,\$03.*?' +
        'ecom_spawnUncountedEnemyWithSubid01.*?' +
        'ecom_spawnEnemyWithSubid01.*?' +
        'ecom_spawnEnemyWithSubid01.*?enemyDelete' -or
    $moldormCodeSource -notmatch
        '(?ms)^@state8:.*?ld \(hl\),\$08.*?' +
        'ld \(hl\),SPEED_100.*?ld \(hl\),\$02.*?' +
        'ecom_setRandomAngle' -or
    $moldormCodeSource -notmatch
        '(?ms)^@state9:.*?ld \(hl\),\$08.*?' +
        'add \(hl\)\s+and \$1f.*?' +
        'getRandomNumber_noPreserveVars\s+and \$0f.*?' +
        'ecom_bounceOffWallsAndHoles.*?objectApplySpeed' -or
    $moldormCodeSource -notmatch
        '(?ms)^moldorm_head_updateAnimationFromAngle:.*?' +
        'add \$02\s+and \$1c\s+rrca\s+rrca' -or
    $moldormCodeSource -notmatch
        '(?ms)^moldorm_tail:.*?^@state8:.*?' +
        'res 7,\(hl\).*?moldorm_tail_clearOffsetBuffer.*?' +
        '^@state9:.*?add \$08\s+swap a.*?add \$08\s+or b.*?' +
        'inc a\s+and \$07.*?sub \$08.*?sub \$08' -or
    $moldormCodeSource -notmatch
        '(?ms)^moldorm_tail_clearOffsetBuffer:.*?' +
        'ld b,\$02\s+ld a,\$88') {
    throw 'Moldorm spawning, steering, animation, or delayed-tail behavior changed.'
}
Add-EnemyBehaviorProfile 'moldorm' 'state-profile' `
    @(3, 8, 0x28, 2, 0x1f, 0x0f, 2, 0x1c, 8, 0x88) `
    'object_code/common/enemies/moldorm.s:state-entry-operands'
$moldormCollisionEffects = @(
    0..0x1f | ForEach-Object {
        $enemyCollisionTableValues[0x3a * 0x20 + $_]
    })
$expectedMoldormCollisionEffects = @(
    0x02, 0x10, 0x0f, 0x0f, 0x08, 0x09, 0x09, 0x0a,
    0x0a, 0x08, 0x08, 0x0a, 0x0d, 0x08, 0x08, 0x25,
    0x00, 0x00, 0x00, 0x1b, 0x00, 0x2f, 0x09, 0x1b,
    0x0a, 0x08, 0x20, 0x20, 0x08, 0x20, 0x20, 0x00)
if (($moldormCollisionEffects -join ',') -ne
    ($expectedMoldormCollisionEffects -join ',')) {
    throw 'ENEMYCOLLISION_MOLDORM `$3a item-effect row changed.'
}
Add-EnemyBehaviorProfile 'moldorm' 'collision-effects' `
    $moldormCollisionEffects `
    'data/ages/objectCollisionTable.s:objectCollisionTable+$0740'

$ghiniCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\ghini.s')
if ($ghiniCodeSource -notmatch
        '(?ms)^@state_uninitialized:.*?ld a,SPEED_80' -or
    $ghiniCodeSource -notmatch
        '(?ms)^ghini_subid00:.*?^@state8:.*?' +
        'ldbc \$18,\$7f.*?ld a,\$30') {
    throw 'Ghini state-entry behavior profile changed.'
}
Add-EnemyBehaviorProfile 'ghini' 'state-profile' `
    @(0x14, 0x30, 0x7f) `
    'object_code/common/enemies/ghini.s:ghini_subid00'

$stalfosCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\stalfos.s')
if ($stalfosCodeSource -notmatch
        '(?ms)^stalfos_moveInRandomAngle:.*?ld e,\$30.*?' +
        'ld bc,\$1f0f.*?ld a,\$20') {
    throw 'Stalfos random-walk counter profile changed.'
}
Add-EnemyBehaviorProfile 'stalfos' 'state-profile' `
    @(0x20, 0x30) `
    'object_code/common/enemies/stalfos.s:stalfos_moveInRandomAngle'

$hardhatBeetleCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\hardhatBeetle.s')
if ($hardhatBeetleCodeSource -notmatch
        '(?ms)^@state_uninitialized:.*?ld a,SPEED_60.*?' +
        'ecom_setSpeedAndState8AndVisible' -or
    $hardhatBeetleCodeSource -notmatch
        '(?ms)^@state8:.*?ecom_updateAngleTowardTarget.*?' +
        'ecom_applyVelocityForSideviewEnemyNoHoles.*?enemyAnimate') {
    throw 'Hardhat Beetle speed, tracking, or movement path changed.'
}
Add-EnemyBehaviorProfile 'hardhat-beetle' 'state-profile' `
    @(0x0f) `
    'object_code/common/enemies/hardhatBeetle.s:state-entry-operands'

$spikedBeetleCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\spikedBeetle.s')
if ($spikedBeetleCodeSource -notmatch
        '(?ms)^@state_uninitialized:.*?call @setRandomAngleAndCounter1.*?' +
        'ld a,SPEED_40.*?ecom_setSpeedAndState8AndVisible' -or
    $spikedBeetleCodeSource -notmatch
        '(?ms)^@state8:.*?ld b,\$08.*?objectCheckCenteredWithLink.*?' +
        'ecom_applyVelocityForSideviewEnemyNoHoles' -or
    $spikedBeetleCodeSource -notmatch
        '(?ms)^@state9:.*?ecom_decCounter2.*?@incSpeed.*?' +
        'ecom_applyVelocityForSideviewEnemyNoHoles.*?ld \(hl\),30' -or
    $spikedBeetleCodeSource -notmatch
        '(?ms)^@stateB:.*?ld \(hl\),SPEED_c0.*?' +
        'ENEMYCOLLISION_SPIKED_BEETLE.*?inc \(hl\).*?-\$180.*?' +
        'cp 60.*?and \$06' -or
    $spikedBeetleCodeSource -notmatch
        '(?ms)^@stateC:.*?ecom_applyVelocityForSideviewEnemyNoHoles.*?' +
        'ld c,\$18.*?objectUpdateSpeedZ_paramC.*?ld b,\$10' -or
    $spikedBeetleCodeSource -notmatch
        '(?ms)^@setRandomAngleAndCounter1:.*?ldbc \$18,\$30.*?' +
        'ecom_randomBitwiseAndBCE.*?add c' -or
    $spikedBeetleCodeSource -notmatch
        '(?ms)^@chargeLink:.*?ld \(hl\),\$09.*?' +
        'ld \(hl\),150.*?ld \(hl\),SPEED_40' -or
    $spikedBeetleCodeSource -notmatch
        '(?ms)^@incSpeed:.*?and \$03.*?cp SPEED_180.*?' +
        'add SPEED_20' -or
    $spikedBeetleCodeSource -notmatch
        '(?ms)^@knockback:.*?ld c,\$18.*?' +
        'objectUpdateSpeedZAndBounce.*?ld b,SPEED_e0' -or
    $spikedBeetleCodeSource -notmatch
        '(?ms)ENEMYCOLLISION_SPIKED_BEETLE_FLIPPED.*?ld \(hl\),180') {
    throw 'Spiked Beetle movement, combat, flip, or recovery path changed.'
}
if ($enemyCollisionTableSource -notmatch
        '(?ms); ENEMYCOLLISION_SPIKED_BEETLE \(0x18\).*?' +
        '\.db \$02 \$10 \$0f \$0f \$15 \$16 \$16 \$16 \$17 \$16' -or
    $enemyCollisionTableSource -notmatch
        '(?ms); ENEMYCOLLISION_THWOMP \(0x28\).*?' +
        '\.db \$02 \$07 \$06 \$06 \$15 \$16 \$16 \$16 \$17 \$15' -or
    $enemyCollisionTableSource -notmatch
        '(?ms); ENEMYCOLLISION_HEAD_THWOMP \(0x4a\).*?' +
        '\.db \$02 \$00 \$00 \$00 \$1b \$1b \$1b \$1b \$1b \$1b' -or
    $collisionEffectsSource -notmatch
        '(?ms)^collisionEffect15:.*?createClinkInteraction.*?' +
        'LINKDMG_10, ENEMYDMG_34' -or
    $collisionEffectsSource -notmatch
        '(?ms)^collisionEffect16:.*?createClinkInteraction.*?' +
        'LINKDMG_14, ENEMYDMG_34' -or
    $collisionEffectsSource -notmatch
        '(?ms)^collisionEffect17:.*?createClinkInteraction.*?' +
        'LINKDMG_18, ENEMYDMG_34' -or
    $collisionEffectsSource -notmatch
        '(?ms)^collisionEffect1b:.*?createClinkInteraction.*?' +
        'LINKDMG_1c, ENEMYDMG_28' -or
    $collisionEffectsSource -notmatch
        '(?m)^\s*\.db \$60 \$ec \$00 \$00 ; ENEMYDMG_28\s*$' -or
    $collisionEffectsSource -notmatch
        '(?m)^\s*\.db \$60 \$e4 \$00 \$00 ; ENEMYDMG_34\s*$') {
    throw 'Armored Thwomp/Spiked Beetle sword collision effects changed.'
}
$armoredLinkDamageMatches = @([regex]::Matches(
    $collisionEffectsSource,
    '(?m)^\s*\.db \$31 \$[0-9a-f]{2} \$(?<counter>[0-9a-f]{2}) ' +
    '\$00 ; LINKDMG_(?<label>10|14|18)\s*$'))
$armoredLinkDamageCounters = @($armoredLinkDamageMatches | ForEach-Object {
    [Convert]::ToInt32($_.Groups['counter'].Value, 16)
})
if ($armoredLinkDamageMatches.Count -ne 3 -or
    -not [string]::Equals(
        (($armoredLinkDamageMatches | ForEach-Object {
            $_.Groups['label'].Value
        }) -join ','),
        '10,14,18',
        [StringComparison]::Ordinal) -or
    ($armoredLinkDamageCounters -join ',') -ne '11,19,25') {
    throw 'Armored sword LINKDMG attacker recoil counters changed.'
}
$armoredLinkDamagePairs = @()
$armoredLinkDamageSources = @()
for ($index = 0; $index -lt $armoredLinkDamageCounters.Count; $index++) {
    $armoredLinkDamagePairs += ,@($armoredLinkDamageCounters[$index], 0)
    $armoredLinkDamageSources +=
        "code/collisionEffects.s:applyDamageToLink@damageTypeTable+" +
        ((0x10 + $index * 4).ToString('x2'))
}
Add-EnemyBehaviorPairTable `
    'common-enemy' `
    'armored-sword-attacker-knockback-frames' `
    $armoredLinkDamagePairs `
    $armoredLinkDamageSources
$spikedBeetleShakeOffsets = @(
    Read-EnemyBehaviorValues (
        Get-AssemblyLabelBody $spikedBeetleCodeSource '@xOscillationOffsets'
    ) $true)
if ($spikedBeetleShakeOffsets.Count -ne 4 -or
    -not [string]::Equals(
        ($spikedBeetleShakeOffsets -join ','),
        '1,-1,-1,1',
        [StringComparison]::Ordinal)) {
    throw 'Spiked Beetle X-oscillation table changed.'
}
Add-EnemyBehaviorValueTable 'spiked-beetle' 'shake-x-offsets' `
    $spikedBeetleShakeOffsets `
    'object_code/common/enemies/spikedBeetle.s:@xOscillationOffsets'
Add-EnemyBehaviorProfile 'spiked-beetle' 'state-profile' `
    @(0x0a, 8, 0x0a, 3, 0x05, 0x3c, 150, 30,
      180, 60, 0x18, -0x180, 0x23, 0x1e, 16, 28) `
    'object_code/common/enemies/spikedBeetle.s:state-entry-operands'

$spinyBeetleCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\spinyBeetle.s')
$bushOrRockCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\bushOrRock.s')
if ($spinyBeetleCodeSource -notmatch
        '(?ms)^@state_uninitialized:.*?ld a,SPEED_e0.*?' +
        'ld a,\$03.*?ld \(hl\),\$80' -or
    $spinyBeetleCodeSource -notmatch
        '(?ms)^@state8:.*?ld b,\$0c.*?or a\s+ret z.*?' +
        'ld a,\$01.*?ecom_getTopDownAdjacentWallsBitset' -or
    $spinyBeetleCodeSource -notmatch
        '(?ms)^@chargeAtLink:.*?ld \(hl\),\$38.*?' +
        'ld \(hl\),\$81' -or
    $spinyBeetleCodeSource -notmatch
        '(?ms)^@state9:.*?ld \(hl\),30.*?ld \(hl\),\$80' -or
    $spinyBeetleCodeSource -notmatch
        '(?ms)^@checkBushOrRockGone:.*?ld \(hl\),60.*?' +
        'ld a,\$06' -or
    $spinyBeetleCodeSource -notmatch
        '(?ms)^@stateB:.*?ld \(hl\),40.*?and \$1c' -or
    $bushOrRockCodeSource -notmatch
        '(?ms)^@collisionAndTileData:.*?' +
        'ENEMYCOLLISION_BUSH, TILEINDEX_DUNGEON_BUSH' -or
    $bushOrRockCodeSource -notmatch
        '(?ms)^@zVals:.*?\.db \$00 \$fc \$f8 \$f4') {
    throw 'Spiny Beetle cover, charge, reveal, or wander path changed.'
}
Add-EnemyBehaviorProfile 'spiny-beetle' 'state-profile' `
    @(0x23, 3, 0x0c, 0x38, 30, 60, 6, 40, 0x20, -4) `
    'object_code/common/enemies/spinyBeetle.s:state-entry-operands'

$wallmasterCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\enemies\wallmaster.s')
if ($wallmasterCodeSource -notmatch
        '(?ms)^wallmaster_state_uninitialized:.*?ld \(hl\),180' -or
    $wallmasterCodeSource -notmatch
        '(?ms)^wallmaster_state1:.*?ld \(hl\),120' -or
    $wallmasterCodeSource -notmatch
        '(?ms)^wallmaster_state8:.*?ld \(hl\),\$a0' -or
    $wallmasterCodeSource -notmatch
        '(?ms)^wallmaster_state9:.*?ld c,\$0e.*?ld \(hl\),30' -or
    $wallmasterCodeSource -notmatch
        '(?ms)^wallmaster_stateA:.*?cp 20' -or
    $wallmasterCodeSource -notmatch
        '(?ms)^wallmaster_stateB:.*?dec \(hl\)\s+dec \(hl\).*?' +
        'cp \$a0.*?ld \(hl\),120' -or
    $wallmasterCodeSource -notmatch
        '(?ms)^wallmaster_flickerVisibilityIfHighUp:.*?' +
        'cp \$b8.*?cp \$bc') {
    throw 'Wallmaster timing, motion, or visibility profile changed.'
}
Add-EnemyBehaviorProfile 'wallmaster' 'state-profile' `
    @(180, 120, -0x60, 0x0e, 30, 20, 2, 120, -0x48, -0x44) `
    'object_code/common/enemies/wallmaster.s:state-entry-operands'

$moblinBoomerangPartCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\parts\moblinBoomerang.s')
$moblinBoomerangPartData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$8e \$86 \$(?<radius>[0-9a-f]{2}) ' +
    '\$(?<damage>[0-9a-f]{2}) \$40 \$0a \$04 \$00\s*; \$21')
if ($moblinBoomerangPartCodeSource -notmatch
        '(?ms)^@state0:.*?ld \(hl\),\$2d.*?' +
        'ld \(hl\),\$06.*?ld \(hl\),\$50' -or
    $moblinBoomerangPartCodeSource -notmatch
        '(?ms)^func_541a:.*?and \$03.*?add \$05.*?cp \$50' -or
    $moblinBoomerangPartCodeSource -notmatch
        '(?ms)^func_53f5:.*?add \$04\s+cp \$09' -or
    -not $moblinBoomerangPartData.Success -or
    $moblinBoomerangPartData.Groups['radius'].Value -ne '22' -or
    $moblinBoomerangPartData.Groups['damage'].Value -ne 'fc') {
    throw 'Moblin boomerang state or collision profile changed.'
}
Add-EnemyBehaviorProfile 'moblin-boomerang-projectile' 'state-profile' `
    @(0x2d, 6, 0x50, 5, 0x4b, 3, 2, 4, 2) `
    'object_code/common/parts/moblinBoomerang.s:state-operands'

$pumpkinProjectilePartData = [regex]::Match(
    $partDataSource,
    '(?m)^\s*\.db \$a6 \$86 \$(?<radius>[0-9a-f]{2}) ' +
    '\$(?<damage>[0-9a-f]{2}) \$40 \$1e \$02 \$00\s*; \$42')
if ($pumpkinProjectileCodeSource -notmatch
        '(?ms)^@state0:.*?ld \(hl\),\$08.*?ld \(hl\),\$3c' -or
    -not $pumpkinProjectilePartData.Success -or
    $pumpkinProjectilePartData.Groups['radius'].Value -ne '42' -or
    $pumpkinProjectilePartData.Groups['damage'].Value -ne 'fc') {
    throw 'Pumpkin Head projectile state or collision profile changed.'
}
Add-EnemyBehaviorProfile 'pumpkin-head-projectile' 'state-profile' `
    @(8, 0x3c, 4, 2, 2) `
    'object_code/ages/parts/pumpkinHeadProjectile.s:state0'

Add-EnemyBehaviorProfile 'spark' 'state-profile' `
    @(0x28) `
    'object_code/common/enemies/spark.s:state-entry-operands'
Add-EnemyBehaviorProfile 'whisp' 'state-profile' `
    @(0x1e) `
    'object_code/common/enemies/whisp.s:state-entry-operands'
Add-EnemyBehaviorProfile 'thwomp' 'state-profile' `
    @(0x14, 0x30, 60, 0x80, 0x20, 0x13, 3) `
    'object_code/common/enemies/thwomp.s:state-entry-operands'
Add-EnemyBehaviorProfile 'peahat' 'state-profile' `
    @(0x7f, 0x80, 5, 0x1e, 180, 180, 210, 210, 240, 240, 0, 0) `
    'object_code/common/enemies/peahat.s:state-entry-operands'
Add-EnemyBehaviorProfile 'sword-enemy' 'state-profile' `
    @(0x14, 0x19, 0x10, 0x60, 0x28, 0x50, 0x3f, 7, 3, 0x14, 0x10, 0x0c) `
    'object_code/common/enemies/swordEnemies.s:state-entry-operands'
Add-EnemyBehaviorProfile 'color-changing-gel' 'state-profile' `
    @(150, 60, 0x32, -0x180, 0x30, 90) `
    'object_code/ages/enemies/colorChangingGel.s:state-entry-operands'

if ($enemyBehaviorRows.Count -ne 468) {
    throw "Expected 467 enemy behavior-table rows, got " +
        "$($enemyBehaviorRows.Count - 1)."
}
Write-GeneratedTable(
    (Join-Path $destination 'metadata\enemy_behavior_tables.tsv'),
    $enemyBehaviorRows)
[IO.File]::Delete(
    (Join-Path $destination 'metadata\wing_dungeon_enemy_constants.tsv'))

$legacyFairyVelocityPath =
    Join-Path $destination 'effects\item_drop_fairy_velocities.tsv'
if (Test-Path -LiteralPath $legacyFairyVelocityPath) {
    Remove-Item -LiteralPath $legacyFairyVelocityPath -Force
}
