# Keese are the first supported enemy. Their room records use random-position
# enemy opcodes, while their attributes, animations, OAM, and graphics are in
# the shared enemy tables. Export the resolved values so runtime code never
# reparses assembly source.
$enemyDataSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\enemyData.s")
$keeseDataMatch = [regex]::Match(
    $enemyDataSource,
    '(?m)^\s*/\* 0x32 \*/ m_EnemyData \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) \$(?<extra>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
)
if (-not $keeseDataMatch.Success) { throw "Could not resolve ENEMY_KEESE (`$32) data." }
$keeseGfx = [Convert]::ToInt32($keeseDataMatch.Groups['gfx'].Value, 16)
$keeseExtraIndex = [Convert]::ToInt32($keeseDataMatch.Groups['extra'].Value, 16) -band 0x7f
$keeseGraphicFlags = [Convert]::ToInt32($keeseDataMatch.Groups['flags'].Value, 16)
if ($keeseGfx -ne 0x9d -or
    ([Convert]::ToInt32($keeseDataMatch.Groups['collision'].Value, 16) -band 0x7f) -ne 0x1f -or
    $keeseExtraIndex -ne 0x07) {
    throw "ENEMY_KEESE no longer resolves to gfx `$9d, collision mode `$1f, extra data `$07."
}

$extraEnemyBody = [regex]::Match(
    $enemyDataSource,
    '(?ms)^extraEnemyData:\r?\n(?<body>.*)'
).Groups['body'].Value
$extraEnemyRows = [regex]::Matches(
    $extraEnemyBody,
    '(?m)^\s*\.db \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<damage>[0-9a-f]{2}) \$(?<health>[0-9a-f]{2})'
)
if ($extraEnemyRows.Count -le $keeseExtraIndex) { throw "Keese extra-enemy record `$07 is missing." }
$keeseExtra = $extraEnemyRows[$keeseExtraIndex]
$keeseRadiusY = [Convert]::ToInt32($keeseExtra.Groups['y'].Value, 16)
$keeseRadiusX = [Convert]::ToInt32($keeseExtra.Groups['x'].Value, 16)
$keeseDamageByte = [Convert]::ToInt32($keeseExtra.Groups['damage'].Value, 16)
$keeseDamageQuarters = (0x100 - $keeseDamageByte) / 2
$keeseHealth = [Convert]::ToInt32($keeseExtra.Groups['health'].Value, 16)
if ($keeseRadiusY -ne 4 -or $keeseRadiusX -ne 6 -or
    $keeseDamageQuarters -ne 2 -or $keeseHealth -ne 1) {
    throw "ENEMY_KEESE extra data no longer matches radii 4x6, half-heart damage, and 1 health."
}

function Get-AssemblyLabelBody([string]$source, [string]$label) {
    $escaped = [regex]::Escape($label)
    $match = [regex]::Match(
        $source,
        "(?ms)^${escaped}:[^\r\n]*\r?\n(?<body>.*?)(?=^[A-Za-z0-9_@]+:|\z)"
    )
    if (-not $match.Success) { throw "Assembly label not found: $label" }
    return $match.Groups['body'].Value
}

$enemyAnimationSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\enemyAnimations.s")
$enemyOamSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\enemyOamData.s")
$keeseAnimationLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyAnimationSource 'enemy32Animations'),
        '(?m)^\s*\.dw\s+(?<label>enemyAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$keeseOamLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyAnimationSource 'enemy32OamDataPointers'),
        '(?m)^\s*\.dw\s+(?<label>enemyOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($keeseAnimationLabels.Count -ne 2 -or $keeseOamLabels.Count -ne 2) {
    throw "Expected two Keese animations and two Keese OAM pointers."
}

function Resolve-EnemyOam([string]$label) {
    $body = Get-AssemblyLabelBody $script:enemyOamSource $label
    $countMatch = [regex]::Match($body, '(?m)^\s*\.db\s+\$(?<count>[0-9a-f]{2})')
    if (-not $countMatch.Success) { throw "OAM count missing for $label." }
    $count = [Convert]::ToInt32($countMatch.Groups['count'].Value, 16)
    $parts = @(
        [regex]::Matches(
            $body,
            '(?m)^\s*\.db\s+\$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
        ) | ForEach-Object {
            "$([Convert]::ToInt32($_.Groups['y'].Value, 16)),$([Convert]::ToInt32($_.Groups['x'].Value, 16)),$([Convert]::ToInt32($_.Groups['tile'].Value, 16)),$([Convert]::ToInt32($_.Groups['flags'].Value, 16))"
        }
    )
    if ($parts.Count -ne $count) { throw "$label declares $count OAM parts but contains $($parts.Count)." }
    return $parts -join ';'
}

function Resolve-KeeseAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:enemyAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:keeseOamLabels.Count) {
            throw "$label references missing OAM pointer byte offset $($frame.Groups['offset'].Value)."
        }
        $frames.Add("$duration@$(Resolve-EnemyOam $script:keeseOamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}

$keeseIdleAnimation = Resolve-KeeseAnimation $keeseAnimationLabels[0]
$keeseFlyAnimation = Resolve-KeeseAnimation $keeseAnimationLabels[1]
if ($keeseIdleAnimation -ne '127@8,4,2,0' -or
    $keeseFlyAnimation -ne '4@8,0,0,0;8,8,0,32|4@8,4,2,0') {
    throw "ENEMY_KEESE animation/OAM data no longer matches the folded/flying records."
}

$keeseRows = [Collections.Generic.List[string]]::new()
$keeseRows.Add("# group`troom`tid`tsubid`tflags`tcount`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tidle-animation`tfly-animation")
$keeseAliases = [Collections.Generic.List[object]]::new()
foreach ($line in Get-Content (Join-Path $Disassembly "objects\ages\enemyData.s")) {
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
$keesePath = Join-Path $destination "objects\keese.tsv"
[IO.File]::WriteAllLines($keesePath, $keeseRows, [Text.UTF8Encoding]::new($false))

# Octoroks (`$09) use both random-position and fixed-position enemy opcodes.
# Ages room data instantiates subids `$00, `$01, and `$02: normal red, fast
# red, and blue. Export resolved per-subid attributes and all four cardinal
# animations alongside the original room-object order.
$octorokDataMatch = [regex]::Match(
    $enemyDataSource,
    '(?m)^\s*/\* 0x09 \*/ m_EnemyData \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) enemy09SubidData'
)
if (-not $octorokDataMatch.Success -or
    [Convert]::ToInt32($octorokDataMatch.Groups['gfx'].Value, 16) -ne 0x8f -or
    [Convert]::ToInt32($octorokDataMatch.Groups['collision'].Value, 16) -ne 0x90) {
    throw 'ENEMY_OCTOROK no longer resolves to gfx `$8f / standard collision mode `$10.'
}
$octorokGfx = [Convert]::ToInt32($octorokDataMatch.Groups['gfx'].Value, 16)
$octorokSubidRows = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyDataSource 'enemy09SubidData'),
        '(?m)^\s*m_EnemySubidData \$(?<extra>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
    )
)
if ($octorokSubidRows.Count -ne 5) {
    throw "Expected five ENEMY_OCTOROK subid records, got $($octorokSubidRows.Count)."
}

$enemy09AnimationStart = $enemyAnimationSource.IndexOf('enemy09Animations:', [StringComparison]::Ordinal)
$enemy0aAnimationStart = $enemyAnimationSource.IndexOf('enemy0aAnimations:', [StringComparison]::Ordinal)
$octorokAnimationLabels = @(
    [regex]::Matches(
        $enemyAnimationSource.Substring(
            $enemy09AnimationStart, $enemy0aAnimationStart - $enemy09AnimationStart),
        '(?m)^\s*\.dw\s+(?<label>enemyAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$enemy09OamStart = $enemyAnimationSource.IndexOf('enemy09OamDataPointers:', [StringComparison]::Ordinal)
$enemy0aOamStart = $enemyAnimationSource.IndexOf('enemy0aOamDataPointers:', [StringComparison]::Ordinal)
$octorokOamLabels = @(
    [regex]::Matches(
        $enemyAnimationSource.Substring($enemy09OamStart, $enemy0aOamStart - $enemy09OamStart),
        '(?m)^\s*\.dw\s+(?<label>enemyOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($octorokAnimationLabels.Count -ne 4 -or $octorokOamLabels.Count -ne 8) {
    throw 'Expected four Octorok animations and eight Octorok OAM pointers.'
}

function Resolve-OctorokAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:enemyAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:octorokOamLabels.Count) {
            throw "$label references missing Octorok OAM pointer $pointerIndex."
        }
        $frames.Add("$duration@$(Resolve-EnemyOam $script:octorokOamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}

$octorokAnimations = @($octorokAnimationLabels | ForEach-Object {
    Resolve-OctorokAnimation $_
})
if ($octorokAnimations[0] -ne '8@8,0,0,64;8,8,0,96|8@8,0,2,64;8,8,2,96' -or
    $octorokAnimations[1] -ne '8@8,0,6,32;8,8,4,32|8@8,0,10,32;8,8,8,32' -or
    $octorokAnimations[2] -ne '8@8,0,0,0;8,8,0,32|8@8,0,2,0;8,8,2,32' -or
    $octorokAnimations[3] -ne '8@8,0,4,0;8,8,6,0|8@8,0,8,0;8,8,10,0') {
    throw 'ENEMY_OCTOROK cardinal animation/OAM data no longer matches the original records.'
}

$octorokDefinitions = @{}
foreach ($subid in 0..2) {
    $subidRow = $octorokSubidRows[$subid]
    $extraIndex = [Convert]::ToInt32($subidRow.Groups['extra'].Value, 16)
    $graphicFlags = [Convert]::ToInt32($subidRow.Groups['flags'].Value, 16)
    $extra = $extraEnemyRows[$extraIndex]
    $damageByte = [Convert]::ToInt32($extra.Groups['damage'].Value, 16)
    $octorokDefinitions[$subid] = @{
        TileBase = ($graphicFlags -band 0x0f) * 2
        Palette = ($graphicFlags -shr 4) -band 7
        RadiusY = [Convert]::ToInt32($extra.Groups['y'].Value, 16)
        RadiusX = [Convert]::ToInt32($extra.Groups['x'].Value, 16)
        DamageQuarters = (0x100 - $damageByte) / 2
        Health = [Convert]::ToInt32($extra.Groups['health'].Value, 16)
        SpeedRaw = if (($subid -band 1) -ne 0) { 0x1e } else { 0x14 }
        CounterMask = if ($subid -lt 2) { 7 } else { 3 }
    }
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
foreach ($line in Get-Content (Join-Path $Disassembly 'objects\ages\enemyData.s')) {
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
$octorokPath = Join-Path $destination 'objects\octoroks.tsv'
[IO.File]::WriteAllLines($octorokPath, $octorokRows, [Text.UTF8Encoding]::new($false))

# ENEMY_STALFOS (`$31) subid `$00 is the ordinary walking Stalfos used by
# room 4:06 and 33 other source records. Other subids add weapon-evasion,
# bone projectiles, or stomp states and remain explicit unsupported variants.
$stalfosDataMatch = [regex]::Match(
    $enemyDataSource,
    '(?m)^\s*/\* 0x31 \*/ m_EnemyData \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) enemy31SubidData'
)
if (-not $stalfosDataMatch.Success -or
    [Convert]::ToInt32($stalfosDataMatch.Groups['gfx'].Value, 16) -ne 0x9b -or
    ([Convert]::ToInt32($stalfosDataMatch.Groups['collision'].Value, 16) -band 0x7f) -ne 0x7d) {
    throw 'ENEMY_STALFOS no longer resolves to gfx `$9b / undead collision mode `$7d.'
}
$stalfosGfx = [Convert]::ToInt32($stalfosDataMatch.Groups['gfx'].Value, 16)
$stalfosSubidRows = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyDataSource 'enemy31SubidData'),
        '(?m)^\s*m_EnemySubidData \$(?<extra>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
    )
)
if ($stalfosSubidRows.Count -ne 4) {
    throw "Expected four ENEMY_STALFOS subid records, got $($stalfosSubidRows.Count)."
}

$enemy31AnimationStart = $enemyAnimationSource.IndexOf('enemy31Animations:', [StringComparison]::Ordinal)
$enemy32AnimationStart = $enemyAnimationSource.IndexOf('enemy32Animations:', [StringComparison]::Ordinal)
$stalfosAnimationLabels = @(
    [regex]::Matches(
        $enemyAnimationSource.Substring(
            $enemy31AnimationStart, $enemy32AnimationStart - $enemy31AnimationStart),
        '(?m)^\s*\.dw\s+(?<label>enemyAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$enemy31OamStart = $enemyAnimationSource.IndexOf('enemy31OamDataPointers:', [StringComparison]::Ordinal)
$enemy10OamStart = $enemyAnimationSource.IndexOf('enemy10OamDataPointers:', [StringComparison]::Ordinal)
$stalfosOamLabels = @(
    [regex]::Matches(
        $enemyAnimationSource.Substring($enemy31OamStart, $enemy10OamStart - $enemy31OamStart),
        '(?m)^\s*\.dw\s+(?<label>enemyOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($stalfosAnimationLabels.Count -ne 2 -or $stalfosOamLabels.Count -ne 3) {
    throw 'Expected two Stalfos animations and three Stalfos OAM pointers.'
}

function Resolve-StalfosAnimation([string]$label) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:enemyAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $script:stalfosOamLabels.Count) {
            throw "$label references missing Stalfos OAM pointer $pointerIndex."
        }
        $frames.Add("$duration@$(Resolve-EnemyOam $script:stalfosOamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}

$stalfosAnimations = @($stalfosAnimationLabels | ForEach-Object {
    Resolve-StalfosAnimation $_
})
if ($stalfosAnimations[0] -ne '4@8,0,0,0;8,8,2,0|4@8,0,2,32;8,8,0,32' -or
    $stalfosAnimations[1] -ne '127@8,0,4,0;8,8,4,32') {
    throw 'ENEMY_STALFOS walk/jump animation OAM no longer matches the original records.'
}

$stalfosSubid0 = $stalfosSubidRows[0]
$stalfosExtraIndex = [Convert]::ToInt32($stalfosSubid0.Groups['extra'].Value, 16)
$stalfosGraphicFlags = [Convert]::ToInt32($stalfosSubid0.Groups['flags'].Value, 16)
$stalfosExtra = $extraEnemyRows[$stalfosExtraIndex]
$stalfosDamageByte = [Convert]::ToInt32($stalfosExtra.Groups['damage'].Value, 16)
$stalfosDefinition = @{
    TileBase = ($stalfosGraphicFlags -band 0x0f) * 2
    Palette = ($stalfosGraphicFlags -shr 4) -band 7
    RadiusY = [Convert]::ToInt32($stalfosExtra.Groups['y'].Value, 16)
    RadiusX = [Convert]::ToInt32($stalfosExtra.Groups['x'].Value, 16)
    DamageQuarters = (0x100 - $stalfosDamageByte) / 2
    Health = [Convert]::ToInt32($stalfosExtra.Groups['health'].Value, 16)
    SpeedRaw = 0x14
}
if ($stalfosDefinition.TileBase -ne 4 -or $stalfosDefinition.Palette -ne 1 -or
    $stalfosDefinition.RadiusY -ne 6 -or $stalfosDefinition.RadiusX -ne 6 -or
    $stalfosDefinition.DamageQuarters -ne 2 -or $stalfosDefinition.Health -ne 2) {
    throw 'ENEMY_STALFOS subid `$00 no longer matches tile base 4, palette 1, radii 6x6, half-heart damage, and two health.'
}

$stalfosRows = [Collections.Generic.List[string]]::new()
$stalfosRows.Add("# group`troom`tid`tsubid`tflags`tcount`tposition-mode`ty`tx`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tspeed-raw`twalk-animation`tjump-animation")
$stalfosAliases = [Collections.Generic.List[object]]::new()
$stalfosLastSpecificFlags = '00'
foreach ($line in Get-Content (Join-Path $Disassembly 'objects\ages\enemyData.s')) {
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
$stalfosPath = Join-Path $destination 'objects\stalfos.tsv'
[IO.File]::WriteAllLines($stalfosPath, $stalfosRows, [Text.UTF8Encoding]::new($false))

# Zols (`$34) are instantiated with both random and fixed-position enemy
# opcodes. Red Zols split into ENEMY_GEL (`$43), which also has one direct
# random-position room record. Export both definitions with animation
# parameters intact: the terminal parameters on Zol animations 0 and 3 drive
# the emerge/disappear state changes.
$zolDataMatch = [regex]::Match(
    $enemyDataSource,
    '(?m)^\s*/\* 0x34 \*/ m_EnemyData \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) enemy34SubidData'
)
if (-not $zolDataMatch.Success -or
    [Convert]::ToInt32($zolDataMatch.Groups['gfx'].Value, 16) -ne 0x97 -or
    [Convert]::ToInt32($zolDataMatch.Groups['collision'].Value, 16) -ne 0x29) {
    throw 'ENEMY_ZOL no longer resolves to gfx `$97 / collision mode `$29.'
}
$zolGfx = [Convert]::ToInt32($zolDataMatch.Groups['gfx'].Value, 16)
$zolSubidRows = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyDataSource 'enemy34SubidData'),
        '(?m)^\s*m_EnemySubidData \$(?<extra>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
    ) | Select-Object -First 2
)
if ($zolSubidRows.Count -ne 2) {
    throw "Expected two ENEMY_ZOL subid records, got $($zolSubidRows.Count)."
}

$gelDataMatch = [regex]::Match(
    $enemyDataSource,
    '(?m)^\s*/\* 0x43 \*/ m_EnemyData \$(?<gfx>[0-9a-f]{2}) \$(?<collision>[0-9a-f]{2}) \$(?<extra>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
)
if (-not $gelDataMatch.Success -or
    [Convert]::ToInt32($gelDataMatch.Groups['gfx'].Value, 16) -ne 0x97 -or
    [Convert]::ToInt32($gelDataMatch.Groups['collision'].Value, 16) -ne 0xb3 -or
    [Convert]::ToInt32($gelDataMatch.Groups['extra'].Value, 16) -ne 0x06 -or
    [Convert]::ToInt32($gelDataMatch.Groups['flags'].Value, 16) -ne 0x20) {
    throw 'ENEMY_GEL no longer resolves to gfx `$97 / collision `$b3 / extra `$06 / flags `$20.'
}

$zolAnimationLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyAnimationSource 'enemy34Animations'),
        '(?m)^\s*\.dw\s+(?<label>enemyAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$zolOamLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyAnimationSource 'enemy34OamDataPointers'),
        '(?m)^\s*\.dw\s+(?<label>enemyOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$gelAnimationLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyAnimationSource 'enemy43Animations'),
        '(?m)^\s*\.dw\s+(?<label>enemyAnimation[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
$gelOamLabels = @(
    [regex]::Matches(
        (Get-AssemblyLabelBody $enemyAnimationSource 'enemy43OamDataPointers'),
        '(?m)^\s*\.dw\s+(?<label>enemyOamData[0-9a-f]+)'
    ) | ForEach-Object { $_.Groups['label'].Value }
)
if ($zolAnimationLabels.Count -ne 6 -or $zolOamLabels.Count -ne 5 -or
    $gelAnimationLabels.Count -ne 3 -or $gelOamLabels.Count -ne 7) {
    throw 'Expected 6/5 Zol and 3/7 Gel animation/OAM records.'
}

function Resolve-ParameterizedEnemyAnimation(
    [string]$label,
    [string[]]$oamLabels
) {
    $frames = [Collections.Generic.List[string]]::new()
    foreach ($frame in [regex]::Matches(
        (Get-AssemblyLabelBody $script:enemyAnimationSource $label),
        '(?m)^\s*\.db\s+\$(?<duration>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'
    )) {
        $duration = [Convert]::ToInt32($frame.Groups['duration'].Value, 16)
        $parameter = [Convert]::ToInt32($frame.Groups['parameter'].Value, 16)
        $pointerIndex = [Convert]::ToInt32($frame.Groups['offset'].Value, 16) / 2
        if ($pointerIndex -ge $oamLabels.Count) {
            throw "$label references missing enemy OAM pointer $pointerIndex."
        }
        $frames.Add("$duration,$parameter@$(Resolve-EnemyOam $oamLabels[$pointerIndex])")
    }
    return $frames -join '|'
}

$zolAnimations = @($zolAnimationLabels | ForEach-Object {
    Resolve-ParameterizedEnemyAnimation $_ $zolOamLabels
})
$gelAnimations = @($gelAnimationLabels | ForEach-Object {
    Resolve-ParameterizedEnemyAnimation $_ $gelOamLabels
})
if ($zolAnimations[0] -ne '16,0@12,4,0,0|16,0@8,0,4,0;8,8,4,32|127,1@8,0,2,0;8,8,2,32' -or
    $zolAnimations[3] -ne '8,0@8,0,2,0;8,8,2,32|16,0@8,0,4,0;8,8,4,32|16,0@12,4,0,0|127,1@12,4,0,0' -or
    $gelAnimations[1] -ne '4,0@6,2,0,0|4,0@10,6,0,0|4,0@6,6,0,0|4,0@10,2,0,0') {
    throw "ENEMY_ZOL/ENEMY_GEL animation data no longer matches the original records: z0=$($zolAnimations[0]); z3=$($zolAnimations[3]); g1=$($gelAnimations[1])"
}

$zolDefinitions = @{}
foreach ($subid in 0..1) {
    $subidRow = $zolSubidRows[$subid]
    $extraIndex = [Convert]::ToInt32($subidRow.Groups['extra'].Value, 16)
    $graphicFlags = [Convert]::ToInt32($subidRow.Groups['flags'].Value, 16)
    $extra = $extraEnemyRows[$extraIndex]
    $damageByte = [Convert]::ToInt32($extra.Groups['damage'].Value, 16)
    $zolDefinitions[$subid] = @{
        TileBase = ($graphicFlags -band 0x0f) * 2
        Palette = ($graphicFlags -shr 4) -band 7
        RadiusY = [Convert]::ToInt32($extra.Groups['y'].Value, 16)
        RadiusX = [Convert]::ToInt32($extra.Groups['x'].Value, 16)
        DamageQuarters = (0x100 - $damageByte) / 2
        Health = [Convert]::ToInt32($extra.Groups['health'].Value, 16)
    }
}
if ($zolDefinitions[0].Health -ne 2 -or $zolDefinitions[0].Palette -ne 0 -or
    $zolDefinitions[1].Health -ne 3 -or $zolDefinitions[1].Palette -ne 2 -or
    $zolDefinitions[0].DamageQuarters -ne 2 -or
    $zolDefinitions[0].RadiusY -ne 6 -or $zolDefinitions[0].RadiusX -ne 6) {
    throw 'ENEMY_ZOL subid attributes no longer match green/red behavior.'
}

$gelExtraIndex = [Convert]::ToInt32($gelDataMatch.Groups['extra'].Value, 16)
$gelGraphicFlags = [Convert]::ToInt32($gelDataMatch.Groups['flags'].Value, 16)
$gelExtra = $extraEnemyRows[$gelExtraIndex]
$gelDamageByte = [Convert]::ToInt32($gelExtra.Groups['damage'].Value, 16)
$gelDefinition = @{
    TileBase = ($gelGraphicFlags -band 0x0f) * 2
    Palette = ($gelGraphicFlags -shr 4) -band 7
    RadiusY = [Convert]::ToInt32($gelExtra.Groups['y'].Value, 16)
    RadiusX = [Convert]::ToInt32($gelExtra.Groups['x'].Value, 16)
    DamageQuarters = (0x100 - $gelDamageByte) / 2
    Health = [Convert]::ToInt32($gelExtra.Groups['health'].Value, 16)
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
foreach ($line in Get-Content (Join-Path $Disassembly 'objects\ages\enemyData.s')) {
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
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\zols.tsv'), $zolRows, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\gels.tsv'), $gelRows, [Text.UTF8Encoding]::new($false))

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

foreach ($line in Get-Content (Join-Path $Disassembly 'objects\ages\enemyData.s')) {
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

if ($orderedObjectRows.Count -ne 1142) {
    throw "Expected 1141 ordered placement records, parsed $($orderedObjectRows.Count - 1)."
}
if (-not ($orderedObjectRows | Where-Object { $_ -match '^5\tb0\t0\tF\t1b\t01\t00\t1\t68\t38\t63\tff$' }) -or
    -not ($orderedObjectRows | Where-Object { $_ -match '^5\tb0\t1\tF\t34\t00\t00\t1\t78\t58\t75\tff$' }) -or
    -not ($orderedObjectRows | Where-Object { $_ -match '^5\tb0\t2\tR\t32\t00\t40\t2\t-1\t-1\t-1\tff$' }) -or
    -not ($orderedObjectRows | Where-Object { $_ -match '^5\tdb\t0\tI\t57\t01\t00\t1\t-1\t-1\t1d\tff$' }) -or
    -not ($orderedObjectRows | Where-Object { $_ -match '^5\t01\t0\tP\t23\t01\t00\t1\t-1\t-1\t08\tff$' })) {
    throw 'Canonical ordered enemy/fixed-enemy/item-drop/part placement records were not extracted.'
}
[IO.File]::WriteAllLines(
    (Join-Path $destination 'objects\enemy_object_stream.tsv'),
    $orderedObjectRows,
    [Text.UTF8Encoding]::new($false))

# PART_ENEMY_DESTROYED (`$02) is the common enemy death puff. Export both
# animations: animation 0 is the ordinary 20-update puff, while animation 1
# inserts the 8-update high-knockback burst selected by bit 7 of the defeated
# enemy's knockback counter.
$partDataSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\partData.s")
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

$partAnimationSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\partAnimations.s")
$partOamSource = Get-Content -Raw (Join-Path $Disassembly "data\ages\partOamData.s")
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

function Resolve-PartOam([string]$label) {
    $body = Get-AssemblyLabelBody $script:partOamSource $label
    $countMatch = [regex]::Match($body, '(?m)^\s*\.db\s+\$(?<count>[0-9a-f]{2})')
    if (-not $countMatch.Success) { throw "OAM count missing for $label." }
    $count = [Convert]::ToInt32($countMatch.Groups['count'].Value, 16)
    $parts = @(
        [regex]::Matches(
            $body,
            '(?m)^\s*\.db\s+\$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
        ) | ForEach-Object {
            "$([Convert]::ToInt32($_.Groups['y'].Value, 16)),$([Convert]::ToInt32($_.Groups['x'].Value, 16)),$([Convert]::ToInt32($_.Groups['tile'].Value, 16)),$([Convert]::ToInt32($_.Groups['flags'].Value, 16))"
        }
    )
    if ($parts.Count -ne $count) { throw "$label declares $count OAM parts but contains $($parts.Count)." }
    return $parts -join ';'
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
        $frames.Add("$duration@$(Resolve-PartOam $script:deathPuffOamLabels[$pointerIndex])")
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
New-Item -ItemType Directory -Force -Path (Split-Path $deathPuffPath -Parent) | Out-Null
[IO.File]::WriteAllLines($deathPuffPath, $deathPuffRows, [Text.UTF8Encoding]::new($false))

# INTERAC_KILLENEMYPUFF (`$08) is the non-dropping burst used when a red Zol
# splits. It is visually and semantically distinct from PART_ENEMY_DESTROYED.
$interactionDataSource = Get-Content -Raw (Join-Path $Disassembly 'data\ages\interactionData.s')
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
$interactionAnimationSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\interactionAnimations.s')
$interactionOamSource = Get-Content -Raw (
    Join-Path $Disassembly 'data\ages\interactionOamData.s')
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
function Resolve-InteractionOam([string]$label) {
    $body = Get-AssemblyLabelBody $script:interactionOamSource $label
    $countMatch = [regex]::Match($body, '(?m)^\s*\.db\s+\$(?<count>[0-9a-f]{2})')
    if (-not $countMatch.Success) { throw "OAM count missing for $label." }
    $count = [Convert]::ToInt32($countMatch.Groups['count'].Value, 16)
    $parts = @(
        [regex]::Matches(
            $body,
            '(?m)^\s*\.db\s+\$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})'
        ) | ForEach-Object {
            "$([Convert]::ToInt32($_.Groups['y'].Value, 16)),$([Convert]::ToInt32($_.Groups['x'].Value, 16)),$([Convert]::ToInt32($_.Groups['tile'].Value, 16)),$([Convert]::ToInt32($_.Groups['flags'].Value, 16))"
        }
    )
    if ($parts.Count -ne $count) { throw "$label declares $count OAM parts but contains $($parts.Count)." }
    return $parts -join ';'
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
    $killPuffFrames.Add("$duration@$(Resolve-InteractionOam $killPuffOamLabels[$pointerIndex])")
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
[IO.File]::WriteAllLines(
    (Join-Path $destination 'effects\kill_enemy_puff.tsv'),
    $killPuffRows,
    [Text.UTF8Encoding]::new($false))

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
        $frames.Add("$duration@$(Resolve-PartOam $script:part18OamLabels[$pointerIndex])")
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
[IO.File]::WriteAllLines(
    $octorokProjectilePath, $octorokProjectileRows, [Text.UTF8Encoding]::new($false))

# Preserve the complete Ages enemy item-drop selection data used by
# decideItemDrop. The fixed binary layout is 144 enemy records, eight 8-byte
# probability masks, and sixteen 32-byte item sets (720 bytes total).
$treasureDropSource = Get-Content -Raw (Join-Path $Disassembly 'code\treasureAndDrops.s')
function Get-HexBytes([string]$body) {
    $result = [Collections.Generic.List[byte]]::new()
    foreach ($dataLine in [regex]::Matches($body, '(?m)^\s*\.db\s+(?<values>[^;\r\n]+)')) {
        foreach ($value in [regex]::Matches(
            $dataLine.Groups['values'].Value, '\$(?<value>[0-9a-fA-F]{2})')) {
            $result.Add([Convert]::ToByte($value.Groups['value'].Value, 16))
        }
    }
    return $result.ToArray()
}

$itemDropTableMatch = [regex]::Match(
    $treasureDropSource,
    '(?ms)^itemDropTables:\r?\n\.ifdef ROM_AGES\r?\n(?<body>.*?)(?=^\.else)'
)
if (-not $itemDropTableMatch.Success) { throw 'Ages itemDropTables block was not found.' }
$itemDropEnemyTable = @(Get-HexBytes $itemDropTableMatch.Groups['body'].Value)
if ($itemDropEnemyTable.Count -ne 144 -or
    $itemDropEnemyTable[0x09] -ne 0x8e -or
    $itemDropEnemyTable[0x32] -ne 0xae) {
    throw "Expected 144 Ages enemy item-drop records with ENEMY_OCTOROK (`$09) = `$8e and ENEMY_KEESE (`$32) = `$ae."
}

$itemDropProbabilityBytes = [Collections.Generic.List[byte]]::new()
foreach ($probability in 0..7) {
    # Probability 0 deliberately aliases probability 1 in the original table.
    $sourceProbability = if ($probability -eq 0) { 1 } else { $probability }
    $label = "@probability${sourceProbability}:"
    $start = $treasureDropSource.IndexOf($label, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Item-drop probability label not found: $label" }
    $endLabel = if ($sourceProbability -lt 7) {
        "@probability$($sourceProbability + 1):"
    } else {
        '.ifdef ROM_SEASONS'
    }
    $end = $treasureDropSource.IndexOf(
        $endLabel, $start + $label.Length, [StringComparison]::Ordinal)
    if ($end -lt 0) { throw "End of item-drop probability $sourceProbability was not found." }
    $bytes = @(Get-HexBytes $treasureDropSource.Substring($start, $end - $start))
    if ($bytes.Count -ne 8) {
        throw "Item-drop probability $sourceProbability contains $($bytes.Count) bytes; expected 8."
    }
    foreach ($value in $bytes) { $itemDropProbabilityBytes.Add($value) }
}

$itemDropSetBytes = [Collections.Generic.List[byte]]::new()
foreach ($setIndex in 0..15) {
    $setLabel = "itemDropSet$($setIndex.ToString('X'))"
    $bytes = @(Get-HexBytes (Get-AssemblyLabelBody $treasureDropSource $setLabel))
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
[IO.File]::WriteAllBytes($itemDropSelectionPath, $itemDropSelectionBytes.ToArray())

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
$itemDropCodeSource = Get-Content -Raw (Join-Path $Disassembly 'object_code\common\parts\itemDrop.s')
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
        $frames.Add("$duration@$(Resolve-PartOam $script:part01OamLabels[$pointerIndex])")
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
[IO.File]::WriteAllLines(
    $itemDropVisualPath, $itemDropVisualRows, [Text.UTF8Encoding]::new($false))
