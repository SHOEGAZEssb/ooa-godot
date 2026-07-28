function Find-CutsceneCommandSourceLine {
    param(
        [string]$source,
        [int]$bodyStart,
        [int]$bodyEnd,
        [string]$pattern,
        [string]$script,
        [int]$occurrence = 0)
    $path = Resolve-AssemblySourceTextPath $source
    if ($null -eq $path) { throw "$script uses untracked assembly source." }
    $matches = @(Read-AssemblyNodes $path | Where-Object {
        $_.Offset -ge $bodyStart -and $_.Offset -lt $bodyEnd -and
        $_.Code -match $pattern
    })
    if ($occurrence -lt 0 -or $occurrence -ge $matches.Count) {
        throw "Could not locate $script command source occurrence $occurrence matching: $pattern"
    }
    return $matches[$occurrence].Line
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

function Test-CutsceneSchemaFinite {
    param([string]$value)

    $parsed = [single]0
    return [single]::TryParse(
        $value,
        [Globalization.NumberStyles]::Float,
        [Globalization.CultureInfo]::InvariantCulture,
        [ref]$parsed) -and
        -not [single]::IsNaN($parsed) -and
        -not [single]::IsInfinity($parsed)
}

function Test-CutsceneSchemaScalar {
    param([string]$shape, [string]$value)

    switch ($shape) {
        'none' { return $value.Length -eq 0 }
        'optional' { return $true }
        'required' { return -not [string]::IsNullOrWhiteSpace($value) }
        'hex' {
            $parsed = 0
            return [int]::TryParse(
                $value,
                [Globalization.NumberStyles]::AllowHexSpecifier,
                [Globalization.CultureInfo]::InvariantCulture,
                [ref]$parsed)
        }
        'decimal' {
            $parsed = 0
            return [int]::TryParse(
                $value,
                [Globalization.NumberStyles]::AllowLeadingSign,
                [Globalization.CultureInfo]::InvariantCulture,
                [ref]$parsed)
        }
        'positive-decimal' {
            $parsed = 0
            return [int]::TryParse(
                $value,
                [Globalization.NumberStyles]::None,
                [Globalization.CultureInfo]::InvariantCulture,
                [ref]$parsed) -and $parsed -gt 0
        }
        'text-variants' {
            return ($value.ToCharArray() | Where-Object { $_ -eq [char]0 }).Count -eq 1
        }
        'memory-jump-table' {
            $sections = $value.Split('|')
            if ($sections.Count -ne 2 -or
                [string]::IsNullOrWhiteSpace($sections[0])) {
                return $false
            }
            $targets = $sections[1].Split(',')
            if ($targets.Count -eq 0) {
                return $false
            }
            foreach ($target in $targets) {
                if (-not (Test-CutsceneSchemaScalar 'decimal' $target)) {
                    return $false
                }
            }
            return $true
        }
        'translation' {
            $values = $value.Split(',')
            return $values.Count -eq 3 -and
                (Test-CutsceneSchemaFinite $values[0]) -and
                (Test-CutsceneSchemaFinite $values[1]) -and
                $values[2] -in @('0', '1')
        }
        'parallel-translation' {
            $lanes = $value.Split('|')
            if ($lanes.Count -ne 3 -or
                [string]::IsNullOrWhiteSpace($lanes[1])) {
                return $false
            }
            $first = $lanes[0].Split(',')
            $second = $lanes[2].Split(',')
            return $first.Count -eq 2 -and
                $second.Count -eq 2 -and
                (Test-CutsceneSchemaFinite $first[0]) -and
                (Test-CutsceneSchemaFinite $first[1]) -and
                (Test-CutsceneSchemaFinite $second[0]) -and
                (Test-CutsceneSchemaFinite $second[1])
        }
        'native-block' {
            return -not [string]::IsNullOrWhiteSpace($value.Split([char]0, 2)[0])
        }
        default { throw "Unknown cutscene command field shape '$shape'." }
    }
}

function Test-GeneratedCutsceneCommandStreams {
    param(
        [string]$destination,
        [hashtable]$schemas
    )

    $header = "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64"
    $commandFileCount = 0
    $commandRowCount = 0
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    foreach ($file in Get-ChildItem (
        Join-Path $destination 'cutscenes') -File -Filter '*.tsv') {
        $lines = @(Get-Content -LiteralPath $file.FullName)
        if ($lines.Count -eq 0 -or $lines[0] -ne $header) {
            continue
        }
        $commandFileCount++
        for ($lineIndex = 1; $lineIndex -lt $lines.Count; $lineIndex++) {
            if ([string]::IsNullOrWhiteSpace($lines[$lineIndex])) {
                continue
            }
            $columns = $lines[$lineIndex].Split([char]"`t")
            if ($columns.Count -ne 9) {
                throw "$($file.FullName):$($lineIndex + 1): cutscene command row " +
                    "has $($columns.Count) columns instead of 9."
            }
            $opcode = $columns[4]
            if (-not $schemas.ContainsKey($opcode)) {
                throw "$($file.FullName):$($lineIndex + 1): emitted cutscene " +
                    "opcode '$opcode' has no command schema entry."
            }
            try {
                $payload = $utf8.GetString(
                    [Convert]::FromBase64String($columns[8]))
            }
            catch {
                throw "$($file.FullName):$($lineIndex + 1): emitted cutscene " +
                    "opcode '$opcode' has invalid UTF-8 base64 payload: $_"
            }
            $schema = $schemas[$opcode]
            $fields = @(
                @('actor', $schema.ActorShape, $columns[5]),
                @('arg0', $schema.Arg0Shape, $columns[6]),
                @('arg1', $schema.Arg1Shape, $columns[7]),
                @('payload', $schema.PayloadShape, $payload)
            )
            foreach ($field in $fields) {
                if (-not (Test-CutsceneSchemaScalar $field[1] $field[2])) {
                    $shown = if ($field[2].Length -eq 0) {
                        '<empty>'
                    } else {
                        $field[2].Replace(([char]0).ToString(), '\0')
                    }
                    throw "$($file.FullName):$($lineIndex + 1): emitted opcode " +
                        "'$opcode' field '$($field[0])' has '$shown'; expected " +
                        "schema shape '$($field[1])'."
                }
            }
            $commandRowCount++
        }
    }
    if ($commandFileCount -eq 0 -or $commandRowCount -eq 0) {
        throw 'No generated cutscene command streams were available for schema validation.'
    }
}

function Read-AssemblyCutsceneCommands {
    param(
        [string]$path,
        [string]$script,
        [Collections.Generic.HashSet[string]]$supportedOpcodes,
        [string]$endLabel = '')

    $label = $script
    $commands = [Collections.Generic.List[object]]::new()
    $nodes = if ([string]::IsNullOrEmpty($endLabel)) {
        @(Read-AssemblyLabelNodes $path $script)
    } else {
        $start = @(Read-AssemblyLabels $path $script)
        $end = @(Read-AssemblyLabels $path $endLabel)
        if ($start.Count -ne 1 -or $end.Count -ne 1 -or
            $end[0].Offset -le $start[0].Offset) {
            throw "$path`: invalid $script -> $endLabel command range."
        }
        @(Read-AssemblyNodes $path | Where-Object {
            $_.Offset -gt $start[0].Offset -and
            $_.Offset -lt $end[0].Offset
        })
    }
    foreach ($node in $nodes) {
        if ($node.Kind -eq 'Label') {
            $label = $node.Name
            continue
        }
        if ($node.Kind -in @(
            'Blank', 'Comment', 'Constant', 'Data', 'Directive')) {
            continue
        }
        if ($node.Kind -notin @('MacroInvocation', 'Instruction')) {
            throw "$($node.Path):$($node.Line):$($node.Column): " +
                "malformed $script assembly node '$($node.Code)'."
        }
        $opcode = $node.Name.ToLowerInvariant()
        if (-not $supportedOpcodes.Contains($opcode)) {
            throw "$($node.Path):$($node.Line):$($node.Column): " +
                "unsupported $script opcode '$opcode' at label '$label'."
        }
        $commands.Add([pscustomobject]@{
            Script = $script
            Label = $label
            Index = $commands.Count
            Line = $node.Line
            Opcode = $opcode
            Operands = $node.OperandText
        })
    }
    if ($commands.Count -eq 0) {
        throw "$path`: $script contains no commands."
    }
    return $commands
}

function Get-AssemblySourceLine {
    param([string]$source, [string]$pattern, [string]$description)
    $path = Resolve-AssemblySourceTextPath $source
    $node = @(Read-AssemblyNodes $path | Where-Object {
        $_.Code -match $pattern
    } | Select-Object -First 1)
    if ($node.Count -eq 0) {
        throw "Could not locate $description source label."
    }
    return $node[0].Line
}

# ITEM_HARP ($11) uses the complete LINK_ANIM_MODE_HARP_2 sequence. Export
# the parent-item contract and TX_5110 so playback, song effects, and the
# no-effect message remain source-derived.
$harpParentSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\harpFluteParent.s')
$harpAnimationSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')
if ($harpParentSource -notmatch '(?ms)^parentItemCode_harp:.*?ld a,\$ff ~ DISABLE_LINK ~ DISABLE_ALL_BUT_INTERACTIONS.*?and \$1f.*?objectCreateFloatingMusicNote.*?ld c,\$80.*?ld c,\$40' -or
    $harpParentSource -notmatch '(?ms)^@harp:.*?and \(TILESETFLAG_UNDERWATER\|TILESETFLAG_SIDESCROLL\|TILESETFLAG_LARGE_INDOORS\|TILESETFLAG_DUNGEON\|TILESETFLAG_INDOORS\|TILESETFLAG_MAKU\).*?\.dw @tuneOfEchoes\s+\.dw @tuneOfCurrents\s+\.dw @tuneOfAges' -or
    $harpParentSource -notmatch '(?ms)^@tuneOfEchoes:.*?ROOMFLAG_BIT_PORTALSPOT_DISCOVERED.*?@tuneOfCurrents:.*?TILESETFLAG_BIT_PAST.*?@tuneOfAges:.*?CUTSCENE_TIMEWARP' -or
    $harpParentSource -notmatch '(?ms)^@sfxList:.*?SND_FILLED_HEART_CONTAINER.*?SND_TUNE_OF_ECHOES.*?SND_TUNE_OF_CURRENTS.*?SND_TUNE_OF_AGES') {
    throw 'ITEM_HARP parent behavior no longer matches the imported playback contract.'
}
if (-not $allTexts.ContainsKey(0x5110)) {
    throw 'ITEM_HARP no-effect text TX_5110 was not decoded.'
}
$harpAnimationMatch = [regex]::Match(
    $harpAnimationSource,
    '(?ms)^animationData19faa:\s*(?<body>.*?)^animationData19fdd:')
if (-not $harpAnimationMatch.Success) {
    throw 'Could not isolate LINK_ANIM_MODE_HARP_2 animationData19faa.'
}
$harpAnimationRows = @([regex]::Matches(
    $harpAnimationMatch.Groups['body'].Value,
    '(?m)^\s*\.db \$(?<duration>[0-9a-f]{2}) \$(?<graphic>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})\s*$'))
$expectedHarpAnimation = @(
    '14:34:00', '14:35:00', '0c:34:00',
    '14:36:01', '14:37:01', '0c:36:01',
    '14:34:00', '14:35:00', '0c:34:00',
    '14:36:01', '14:37:01', '0c:36:01',
    '14:36:01', '14:37:01', '0c:36:01',
    '01:36:81', '7f:1c:ff')
if ($harpAnimationRows.Count -ne $expectedHarpAnimation.Count) {
    throw "LINK_ANIM_MODE_HARP_2 expected 17 frames, parsed $($harpAnimationRows.Count)."
}
for ($index = 0; $index -lt $expectedHarpAnimation.Count; $index++) {
    $actual = @(
        $harpAnimationRows[$index].Groups['duration'].Value,
        $harpAnimationRows[$index].Groups['graphic'].Value,
        $harpAnimationRows[$index].Groups['parameter'].Value) -join ':'
    if ($actual -ne $expectedHarpAnimation[$index]) {
        throw "LINK_ANIM_MODE_HARP_2 frame $index changed from $($expectedHarpAnimation[$index])."
    }
}
$harpAnimationParameters = @($harpAnimationRows | ForEach-Object {
    $_.Groups['parameter'].Value
}) -join ','
if ($treasureIds['TREASURE_HARP'] -ne 0x11 -or
    $treasureIds['TREASURE_TUNE_OF_ECHOES'] -ne 0x25 -or
    $treasureIds['TREASURE_TUNE_OF_CURRENTS'] -ne 0x26 -or
    $treasureIds['TREASURE_TUNE_OF_AGES'] -ne 0x27 -or
    $soundIds['SND_FILLED_HEART_CONTAINER'] -ne 0x8b -or
    $soundIds['SND_TUNE_OF_ECHOES'] -ne 0xad -or
    $soundIds['SND_TUNE_OF_CURRENTS'] -ne 0xae -or
    $soundIds['SND_TUNE_OF_AGES'] -ne 0xaf) {
    throw 'Harp item, tune treasure, or song sound constants changed.'
}
$harpItemRows = @(
    "# item`tharp-treasure`techoes-treasure`tcurrents-treasure`tages-treasure`tsong-frames`tempty-song-frames`tnote-interval`tprohibited-tileset-mask`tpast-mask`tportal-room-flag`tempty-sound`techoes-sound`tcurrents-sound`tages-sound`tanimation-parameters`tno-effect-text",
    "11`t$($treasureIds['TREASURE_HARP'].ToString('x2'))`t$($treasureIds['TREASURE_TUNE_OF_ECHOES'].ToString('x2'))`t$($treasureIds['TREASURE_TUNE_OF_CURRENTS'].ToString('x2'))`t$($treasureIds['TREASURE_TUNE_OF_AGES'].ToString('x2'))`t260`t261`t32`t7e`t80`t08`t$($soundIds['SND_FILLED_HEART_CONTAINER'].ToString('x2'))`t$($soundIds['SND_TUNE_OF_ECHOES'].ToString('x2'))`t$($soundIds['SND_TUNE_OF_CURRENTS'].ToString('x2'))`t$($soundIds['SND_TUNE_OF_AGES'].ToString('x2'))`t$harpAnimationParameters`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x5110])))"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\harpItem.tsv'),
    $harpItemRows)

# INTERAC_TIMEPORTAL_SPAWNER ($e1) is a scenery interaction rather than an
# NPC, but it uses the same interaction graphics, animation, and OAM tables.
# Export every placed portal spot so runtime activation stays data-driven.
$portalSpawnerSource = Read-ImportText (
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
Write-GeneratedTable($portalPath, $portalRows)

# Direct Tune of Currents/Ages warps create INTERAC_TIMEPORTAL ($de) at the
# arrival position. Unlike the placed $e1 spawner, it uses common sprites,
# remains visible, cycles OBJ palettes, and is restored from wPortalPos when
# its room is parsed again.
$temporaryPortalSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\timeportal.s')
$timewarpEntryTileSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tile_properties\timewarpEntryTileReplacement.s')
$timewarpReturnTileSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tile_properties\timewarpReturnTileReplacement.s')
$temporaryPortalGraphic = $interactionGraphics['222:0']
if ($null -eq $temporaryPortalGraphic) {
    throw 'Could not resolve INTERAC_TIMEPORTAL graphics.'
}
$temporaryPortalAnimation = Resolve-NpcAnimation `
    0xde $temporaryPortalGraphic.DefaultAnimation
if ($temporaryPortalGraphic.Gfx -ne 0 -or
    $temporaryPortalGraphic.TileBase -ne 0x4a -or
    $temporaryPortalGraphic.Palette -ne 1 -or
    -not $temporaryPortalAnimation -or
    $temporaryPortalSource -notmatch '(?ms)^interactionCodede:.*?ld a,\$03\s+call objectSetCollideRadius.*?ld a,\(wPortalPos\).*?ld a,\$ff\s+ld \(wPortalGroup\),a' -or
    $temporaryPortalSource -notmatch '(?ms)^timeportal_updatePalette:.*?and \$01.*?inc a\s+and \$0b.*?interactionAnimate') {
    throw 'INTERAC_TIMEPORTAL graphics, persistence, collision, or palette behavior changed.'
}
$readTimewarpTileReplacements = {
    param([string]$source, [string]$label)
    $block = [regex]::Match(
        $source,
        ('(?ms)^' + [regex]::Escape($label) +
            ':\s*(?<body>.*?)^\s*\.db \$00\s*$'))
    if (-not $block.Success) {
        throw "Could not isolate $label."
    }
    $rows = @([regex]::Matches(
        $block.Groups['body'].Value,
        '(?m)^\s*\.db \$(?<source>[0-9a-f]{2}) \$(?<replacement>[0-9a-f]{2})(?:\s*;.*)?$'))
    if ($rows.Count -eq 0) {
        throw "$label contains no replacement rows."
    }
    return @($rows | ForEach-Object {
        "$($_.Groups['source'].Value):$($_.Groups['replacement'].Value)"
    }) -join ','
}
$entryTileReplacements = & $readTimewarpTileReplacements `
    $timewarpEntryTileSource 'timewarpEntryTileReplacementDict'
$returnTileReplacements = & $readTimewarpTileReplacements `
    $timewarpReturnTileSource 'timewarpReturnTileReplacementDict'
if ($entryTileReplacements -ne 'c5:3a,c8:3a,04:3a' -or
    $returnTileReplacements -ne
        'c0:3a,c3:3a,c5:3a,c8:3a,ce:3a,db:3a,f2:3a,cd:3a,04:3a') {
    throw 'Time-warp entry/return breakable-tile dictionaries changed.'
}
$temporaryPortalRows = @(
    "# sprite`ttile-base`tpalette`tcontact-radius`tanimation`tentry-tile-replacements`treturn-tile-replacements",
    "spr_common_sprites`t$($temporaryPortalGraphic.TileBase)`t$($temporaryPortalGraphic.Palette)`t9`t$temporaryPortalAnimation`t$entryTileReplacements`t$returnTileReplacements"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\temporaryTimePortal.tsv'),
    $temporaryPortalRows)

# CUTSCENE_TIMEWARP uses INTERAC_TIMEWARP ($dd), PART_TIMEWARP_ANIMATION
# ($2b), and INTERAC_SPARKLE ($84:$01) after a portal spawner transfers Link
# to its center. Export the complete source/destination sprite records and the
# two PALH_c1/PALH_c2 beam palettes; runtime should not approximate the effect
# with a full-screen color fade.
$timeWarpSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\timewarp.s')
$timeWarpCutsceneSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$linkWarpSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\link.s')
$partDataSourceForTimeWarp = Read-ImportText (
    Join-Path $Disassembly 'data\ages\partData.s')
$timeWarpPartSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\parts\timewarpAnimation.s')
$sparkleSourceForTimeWarp = Read-ImportText (
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
Write-GeneratedBytes($timeWarpPalettePath, $timeWarpPalette)

$timeWarpSprite = $gfxNames[0x6a]
$sparkleSprite = $gfxNames[0x6b]
Copy-GeneratedFile "gfx_compressible\ages\$timeWarpSprite.png" "gfx\$timeWarpSprite.png"
Copy-GeneratedFile "gfx_compressible\ages\$sparkleSprite.png" "gfx\$sparkleSprite.png"
$timeWarpRows = @(
    "# timewarp-sprite`tcommon-sprite`tsparkle-sprite`tprimary-tile-base`tprimary-palette`tbeam-palette`ttrail-tile-base`ttrail-palette`tparticle-tile-base`tparticle-palette`tsparkle-tile-base`tsparkle-palette`tprimary-priority`tbeam-priority`ttrail-priority`tparticle-priority`tsparkle-priority`tdissolve-frames`tsource-effect-frames`tsource-trail-frames`tarrival-wait-frames`tarrival-effect-frames`tarrival-flicker-frames`texpand-animation`tcontract-animation`tbeam-intro-animation`tbeam-loop-animation`tbeam-contract-animation`ttrail-animation`tsparkle-animation`tparticles",
    "$timeWarpSprite`tspr_common_sprites`t$sparkleSprite`t$($timeWarpGraphics[0].TileBase)`t$($timeWarpGraphics[0].Palette)`t$($timeWarpBeamGraphic.Palette)`t$($timeWarpTrailGraphic.TileBase)`t$($timeWarpTrailGraphic.Palette)`t$([Convert]::ToInt32($timeWarpPart.Groups['tile'].Value, 16))`t$([Convert]::ToInt32($timeWarpPart.Groups['flags'].Value, 16) -band 7)`t$($sparkleGraphic.TileBase)`t$($sparkleGraphic.Palette)`t$($timeWarpPriorities -join "`t")`t48`t120`t60`t30`t16`t30`t$($timeWarpAnimations[0])`t$($timeWarpAnimations[1])`t$($timeWarpAnimations[2])`t$($timeWarpAnimations[3])`t$($timeWarpAnimations[4])`t$($timeWarpAnimations[5])`t$sparkleAnimation`t$($particleRows -join '|')"
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\timeWarpEffects.tsv'),
    $timeWarpRows)

# The first present Maku Tree visit is interaction $87 subid $01, selected
# from room 0:38's $87:$00 object while wMakuTreeState and GLOBALFLAG_0c are
# both clear. Export its complete simulated-input/script timing, all five tree
# animations, text, hardcoded destination, initial PALH_8f load, and four
# cycling background-palette states instead of encoding disassembly-only
# details in runtime code.
$makuTreeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\makuTree.s')
$makuScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$makuCutsceneSource = Read-ImportText (
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
$objectGfxSource = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\maku_tree_cutscene.tsv'),
    $makuEventRows)

$makuMusicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
$makuStopSound = [regex]::Match(
    $makuMusicSource,
    '(?m)^\.define\s+SNDCTRL_STOPMUSIC\s+\$(?<value>[0-9a-f]{2})')
$makuDisappearSound = [regex]::Match(
    $makuMusicSource,
    '(?m)^\s*SND_MAKUDISAPPEAR\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
$makuCutsceneConstants = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\maku_tree_commands.tsv'),
    $makuCommandRows)

# Immediately after the young Maku Tree is saved, wMakuTreeState=$02 selects
# the adult-tree script in present room 0:38. Export the complete looping
# script, including its choice branch and the persistent falling Seed Satchel,
# rather than reducing the event to a one-shot dialogue/reward.
$makuSavedTextIds = @(0x0542..0x0550) + @(0x0561)
foreach ($textId in $makuSavedTextIds) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve saved Maku Tree text TX_$($textId.ToString('x4'))."
    }
    if (-not $allTextPositions.ContainsKey($textId) -or
        $allTextPositions[$textId] -ne 2) {
        throw "Expected saved Maku Tree TX_$($textId.ToString('x4')) to use \\pos(2)."
    }
}
if ($allTexts[0x054a] -notmatch '\\opt\(\).*\\opt\(\)') {
    throw 'Saved Maku Tree TX_054a no longer contains its Yes/No options.'
}

$makuSavedOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'asm15', 'setmusic', 'setcollisionradii', 'makeabuttonsensitive',
    'jumpifroomflagset', 'checkabutton', 'disableinput',
    'showtextlowindex', 'wait', 'jumpiftextoptioneq', 'setglobalflag',
    'writememory', 'enableinput', 'scriptjump')) {
    [void]$makuSavedOpcodes.Add($opcode)
}
$makuSavedParsed = @(Read-AssemblyCutsceneCommands `
    (Join-Path $Disassembly 'scripts\ages\scriptHelper.s') `
    'makuTree_subid02Script_body' $makuSavedOpcodes)
if ($makuSavedParsed.Count -ne 68) {
    throw "Expected 68 saved Maku Tree commands, parsed $($makuSavedParsed.Count)."
}
$makuSavedTargets = @{}
foreach ($command in $makuSavedParsed) {
    if (-not $makuSavedTargets.ContainsKey($command.Label)) {
        $makuSavedTargets[$command.Label] = $command.Index
    }
}
if ($makuSavedTargets['@explainAgain'] -ne 26 -or
    $makuSavedTargets['@npcLoop'] -ne 60) {
    throw 'Saved Maku Tree branch labels no longer begin at commands 26 and 60.'
}

$makuTreeMusicMatch = [regex]::Match(
    $makuMusicSource,
    '(?m)^\s*MUS_MAKU_TREE\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
$makuSolveSoundMatch = [regex]::Match(
    $makuMusicSource,
    '(?m)^\s*SND_SOLVEPUZZLE\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
$makuLandingSoundMatch = [regex]::Match(
    $makuMusicSource,
    '(?m)^\s*SND_DROPESSENCE\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
$globalFlagSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\globalFlags.s')
$makuAdviceFlagMatch = [regex]::Match(
    $globalFlagSource,
    '(?m)^\s*GLOBALFLAG_MAKU_GIVES_ADVICE_FROM_PRESENT_MAP\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $makuTreeMusicMatch.Success -or
    $makuTreeMusicMatch.Groups['value'].Value -ne '1e' -or
    -not $makuSolveSoundMatch.Success -or
    $makuSolveSoundMatch.Groups['value'].Value -ne '4d' -or
    -not $makuLandingSoundMatch.Success -or
    $makuLandingSoundMatch.Groups['value'].Value -ne '77' -or
    -not $makuAdviceFlagMatch.Success -or
    $makuAdviceFlagMatch.Groups['value'].Value -ne '3e') {
    throw 'Could not resolve saved Maku Tree music, Satchel sounds, or advice flag.'
}

$makuSavedCommandRows = [Collections.Generic.List[string]]::new()
$makuSavedCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
foreach ($command in $makuSavedParsed) {
    $opcode = $command.Opcode
    $actor = ''
    $arg0 = ''
    $arg1 = ''
    $payload = ''
    switch ($command.Opcode) {
        'asm15' {
            if ($command.Operands -match '^makuTree_setAnimation,\s*\$(?<animation>[0-4][0-9a-f]?)$') {
                $animation = [Convert]::ToInt32($Matches['animation'], 16)
                if ($animation -gt 4) { throw "Invalid Maku Tree animation at source line $($command.Line)." }
                $opcode = 'setanimationcontinue'
                $actor = 'MakuTree'
                $arg0 = $animation.ToString('x2')
                $payload = $makuAnimations[$animation]
            }
            elseif ($command.Operands -eq 'makuTree_checkSpawnSeedSatchel') {
                $opcode = 'native'
                $payload = 'makuTree_checkSpawnSeedSatchel'
            }
            elseif ($command.Operands -eq 'makuTree_dropSeedSatchel') {
                $opcode = 'native'
                $payload = 'makuTree_dropSeedSatchel'
            }
            else {
                throw "Unsupported saved Maku Tree asm15 '$($command.Operands)' at source line $($command.Line)."
            }
        }
        'setmusic' {
            if ($command.Operands -ne 'MUS_MAKU_TREE') {
                throw "Unexpected saved Maku Tree music '$($command.Operands)'."
            }
            $arg0 = $makuTreeMusicMatch.Groups['value'].Value
        }
        'setcollisionradii' {
            if ($command.Operands -notmatch '^\$(?<y>[0-9a-f]{2}),\s*\$(?<x>[0-9a-f]{2})$') {
                throw "Malformed saved Maku Tree collision radii at source line $($command.Line)."
            }
            $actor = 'MakuTree'
            $arg0 = $Matches['y']
            $arg1 = $Matches['x']
        }
        'makeabuttonsensitive' { $actor = 'MakuTree' }
        'checkabutton' { $actor = 'MakuTree' }
        'jumpifroomflagset' {
            if ($command.Operands -notmatch '^\$(?<flag>[0-9a-f]{2}),\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed saved Maku Tree room-flag branch at source line $($command.Line)."
            }
            $arg0 = $Matches['flag']
            $arg1 = $makuSavedTargets[$Matches['target']].ToString()
        }
        'showtextlowindex' {
            if ($command.Operands -notmatch '^<TX_(?<id>[0-9a-f]{4})$') {
                throw "Malformed saved Maku Tree text at source line $($command.Line)."
            }
            $textId = [Convert]::ToInt32($Matches['id'], 16)
            if (-not $makuSavedTextIds.Contains($textId)) {
                throw "Unexpected saved Maku Tree text TX_$($Matches['id'])."
            }
            $opcode = 'showtext'
            $arg0 = $Matches['id']
            $payload = $allTexts[$textId]
        }
        'wait' { $arg0 = [int]$command.Operands }
        'jumpiftextoptioneq' {
            if ($command.Operands -notmatch '^\$(?<value>[0-9a-f]{2}),\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed saved Maku Tree text-option branch at source line $($command.Line)."
            }
            $arg0 = $Matches['value']
            $arg1 = $makuSavedTargets[$Matches['target']].ToString()
        }
        'setglobalflag' {
            if ($command.Operands -ne 'GLOBALFLAG_MAKU_GIVES_ADVICE_FROM_PRESENT_MAP') {
                throw "Unexpected saved Maku Tree global flag '$($command.Operands)'."
            }
            $arg0 = $makuAdviceFlagMatch.Groups['value'].Value
        }
        'writememory' {
            if ($command.Operands -ne 'wMakuMapTextPresent, <TX_054f') {
                throw "Unexpected saved Maku Tree WRAM write '$($command.Operands)'."
            }
            $arg0 = '4f'
            $payload = 'wMakuMapTextPresent'
        }
        'scriptjump' {
            if (-not $makuSavedTargets.ContainsKey($command.Operands)) {
                throw "Unknown saved Maku Tree branch target '$($command.Operands)'."
            }
            $arg0 = $makuSavedTargets[$command.Operands].ToString()
        }
    }
    $makuSavedCommandRows.Add((New-CutsceneCommandRow `
        $command.Script $command.Index $command.Label $command.Line `
        $opcode $actor "$arg0" "$arg1" $payload))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\maku_tree_saved_commands.tsv'),
    $makuSavedCommandRows)

$makuHelpers = $makuScriptSource.Substring(
    $makuScriptSource.IndexOf('makuTree_dropSeedSatchel:'),
    $makuScriptSource.IndexOf('makuTree_spawnMakuSeed:') -
        $makuScriptSource.IndexOf('makuTree_dropSeedSatchel:'))
if ($makuHelpers -notmatch '(?ms)makuTree_dropSeedSatchel:.*?bit 7,a.*?set 7,\(hl\).*?TREASURE_SEED_SATCHEL.*?ld \(hl\),\$02.*?ld \(hl\),\$60.*?ld b,\$50.*?cp \$64.*?cp \$3c.*?ld b,\$40.*?cp \$50.*?ld b,\$60.*?wMakuTreeSeedSatchelXPosition' -or
    $makuHelpers -notmatch '(?ms)makuTree_checkSpawnSeedSatchel:.*?bit 5,a.*?bit 7,a.*?TREASURE_SEED_SATCHEL.*?ld \(hl\),\$03.*?ld a,\$58.*?wMakuTreeSeedSatchelXPosition') {
    throw 'Saved Maku Tree Seed Satchel drop/respawn helpers changed.'
}
$seedSatchel02 = $treasureObjectRecords['TREASURE_OBJECT_SEED_SATCHEL_02']
$seedSatchel03 = $treasureObjectRecords['TREASURE_OBJECT_SEED_SATCHEL_03']
if ($null -eq $seedSatchel02 -or $null -eq $seedSatchel03 -or
    $seedSatchel02.Treasure -ne 0x19 -or $seedSatchel03.Treasure -ne 0x19 -or
    $seedSatchel02.Graphic -ne 0x20 -or $seedSatchel03.Graphic -ne 0x20) {
    throw 'Could not resolve both Seed Satchel treasure-object records.'
}
$treasureObjectSourceText = $treasureObjectSource -join "`n"
if ($treasureObjectSourceText -notmatch 'm_TreasureSubid \$29, \$00, \$2d, \$20, TREASURE_OBJECT_SEED_SATCHEL_02' -or
    $treasureObjectSourceText -notmatch 'm_TreasureSubid \$09, \$00, \$2d, \$20, TREASURE_OBJECT_SEED_SATCHEL_03') {
    throw 'Seed Satchel falling/respawn treasure modes changed.'
}
$treasureInteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\treasure.s')
if ($treasureInteractionSource -notmatch '(?ms)^@spawnMode2:.*?@@substate0:.*?ld \(hl\),40.*?SND_SOLVEPUZZLE.*?@@substate1:.*?ld \(hl\),\$02\s+inc l\s+ld \(hl\),\$02.*?objectGetZAboveScreen.*?@@substate2:.*?ld c,\$10\s+call objectUpdateSpeedZ_paramC.*?SND_DROPESSENCE.*?interactionDecCounter1.*?ld bc,-\$aa' -or
    $treasureInteractionSource -notmatch '(?ms)^@grabMode1:\s*ldbc \$80,\$fc.*?ld b,\$f2\s+call objectTakePositionWithOffset') {
    throw 'INTERAC_TREASURE falling spawn mode $02 or one-hand grab mode $01 changed.'
}
$objectMathSource = Read-ImportText (Join-Path $Disassembly 'code\bank0.s')
if ($objectMathSource -notmatch '(?ms)^objectGetZAboveScreen:.*?ldh a,\(<hCameraY\)\s+sub b\s+sub \$08\s+cp \$80\s+ret nc\s+ld a,\$80\s+ret') {
    throw 'objectGetZAboveScreen no longer uses cameraY-Y-$08 clamped to -$80.'
}
# Room 0:38 is one screen tall, so hCameraY is zero. Y=$60 therefore starts
# at signed Z -$68, immediately above the screen as the native helper specifies.
$makuSatchelInitialZ = [Math]::Max(-0x80, -0x60 - 0x08)
$makuSavedEventRows = @(
    "# group`troom`tid`tsubid`tanimation0`tanimation1`tanimation2`tanimation3`tanimation4`textra-sprite`ttextbox-position`tmusic`tadvice-flag`tmap-text-low`tfalling-object`trespawn-object`tdrop-y`trespawn-y`tdefault-x`tlower-bound`tmiddle-bound`tupper-bound`tlower-band-x`tupper-band-x`tinitial-z`tdrop-delay`tbounce-count`tgravity`tbounce-speed`tspawn-sound`tlanding-sound",
    (@(
        '0', '38', '87', '00',
        $makuAnimations[0], $makuAnimations[1], $makuAnimations[2],
        $makuAnimations[3], $makuAnimations[4], $makuExtraSprite,
        '2', $makuTreeMusicMatch.Groups['value'].Value,
        $makuAdviceFlagMatch.Groups['value'].Value, '4f',
        'TREASURE_OBJECT_SEED_SATCHEL_02',
        'TREASURE_OBJECT_SEED_SATCHEL_03',
        '60', '58', '50', '3c', '50', '64', '60', '40',
        $makuSatchelInitialZ.ToString(), '40', '2', '10', '-170',
        $makuSolveSoundMatch.Groups['value'].Value,
        $makuLandingSoundMatch.Groups['value'].Value
    ) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\maku_tree_saved_event.tsv'),
    $makuSavedEventRows)

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
Write-GeneratedBytes(
    (Join-Path $destination 'metadata\maku_tree_disappear_palettes.bin'),
    $makuPaletteBytes.ToArray())

# Ralph's first portal departure is INTERAC_RALPH ($37) subid $0d in room
# 0:39. Export the entry-direction guard, complete script timing/movement,
# animations, one-shot global flag, and text from their original records.
$ralphSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\ralph.s')
$ralphScriptSource = Read-ImportText (
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
$speedSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$speedMatch = [regex]::Match(
    $speedSource,
    '(?m)^\s*SPEED_100\s+dsb\s+(?<count>\d+)\s*;\s*0x(?<value>[0-9a-f]{2})')
if (-not $speedMatch.Success -or $speedMatch.Groups['value'].Value -ne '28') {
    throw 'SPEED_100 no longer resolves to original object speed $28.'
}
$globalFlagSource = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\ralph_portal_event.tsv'),
    $ralphEventRows)

# Emit the active path as typed command records. Command rows retain the
# assembly script/label, normalized command index, and physical source line.
# The recognized flicker loop remains one native-effect command, while its
# counter byte and frame mask stay explicit operands.
$ralphMusicSource = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\ralph_portal_commands.tsv'),
    $ralphCommandRows)

# The first arrival in the past is INTERAC_MALE_VILLAGER ($3a:$0d) in room
# 1:39. Its leading wait advances while TRANSITION_DEST_TIMEWARP finishes, so
# export the script counters, jump physics, speeds, path, animations, text,
# sound, completion flag, and expected arrival overlap as one checked record.
$enterPastVillagerSource = Read-ImportText (
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

$enterPastHelperSource = Read-ImportText (
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
$enterPastMusicSource = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\enter_past_event.tsv'),
    $enterPastEventRows)

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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\enter_past_commands.tsv'),
    $enterPastCommandRows)

# The children in present room 0:7b implement the Spirit's Grave ghost scene
# as three native interaction state machines synchronized through cfd1. Keep
# this as native event metadata: the scripts run concurrently, and their
# source-order RNG calls and same-update signal handoffs are observable.
$graveyardBoySource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\boy.s')
$graveyardBoy2Source = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\boy2.s')
$graveyardScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$graveyardHelperSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\ralph.s')
$graveyardOscillationSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\impaInCutscene.s')

if ($mainObjectSource -notmatch '(?ms)^group0Map7bObjectData:\s+obj_Interaction \$3c \$03 \$48 \$48\s+obj_Interaction \$3c \$04 \$48 \$68\s+obj_Interaction \$3f \$02 \$38 \$58\s+obj_Interaction \$b6 \$03 \$28 \$68\s+obj_End') {
    throw 'Room 0:7b child/Gasha object ordering changed.'
}
if ($graveyardBoySource -notmatch '(?ms)^@initSubid03:.*?getThisRoomFlags\s+bit 6,a\s+jp nz,interactionDelete.*?ld \(wDisabledObjects\),a\s+ld \(wMenuDisabled\),a.*?^@setRedPaletteAndLoadScript:\s+ld a,\$02\s+ld e,Interaction\.oamFlags' -or
    $graveyardBoySource -notmatch '(?ms)^@initSubid04:.*?getThisRoomFlags\s+bit 6,a\s+jp nz,interactionDelete.*?ld a,\$3c\s+ld \(de\),a\s+xor a\s+call interactionSetAnimation\s*^@saveXToVar3d:' -or
    $graveyardBoySource -notmatch '(?ms)^boyRunSubid03:.*?interactionRunScript.*?Interaction\.var39.*?interactionAnimateBasedOnSpeed.*?objectCheckWithinScreenBoundary.*?ld \(wDisabledObjects\),a\s+ld \(wMenuDisabled\),a\s+call getThisRoomFlags\s+set 6,\(hl\)\s+jp interactionDelete' -or
    $graveyardBoySource -notmatch '(?ms)^boyRunSubid04:.*?@substate0:.*?interactionAnimate.*?interactionDecCounter1.*?interactionIncSubstate\s+jp startJump.*?@substate1:.*?ld c,\$20\s+call objectUpdateSpeedZ_paramC.*?interactionIncSubstate\s+jp boyLoadScript') {
    throw 'INTERAC_BOY $3c:$03/$04 ghost-scene native states changed.'
}
if ($graveyardBoy2Source -notmatch '(?ms)^@subid2:.*?getThisRoomFlags\s+bit 6,a\s+jp nz,interactionDelete.*?Interaction\.var3d.*?Interaction\.xh.*?^@@substate0:.*?interactionAnimate.*?cp \$01.*?interactionIncSubstate\s+jpab agesInteractionsBank08\.startJump.*?^@@substate1:.*?ld c,\$20\s+call objectUpdateSpeedZ_paramC.*?interactionIncSubstate\s+call @initializeScript.*?^@@substate2:\s+jpab agesInteractionsBank08\.boyRunSubid03') {
    throw 'INTERAC_BOY_2 $3f:$02 ghost-scene native states changed.'
}

$graveyardRedScript = [regex]::Match(
    $graveyardScriptSource,
    '(?ms)^boySubid03Script:(?<body>.*?)(?=^; Cutscene where kids talk about how they''re scared of a ghost \(green kid\))')
$graveyardGreenScript = [regex]::Match(
    $graveyardScriptSource,
    '(?ms)^boySubid04Script:(?<body>.*?)(?=^; Cutscene where kid is restored from stone)')
$graveyardBlueScript = [regex]::Match(
    $graveyardScriptSource,
    '(?ms)^boy2Subid2Script:(?<body>.*?)(?=^; =+\s*^; INTERAC_SOLDIER)')
if (-not $graveyardRedScript.Success -or -not $graveyardGreenScript.Success -or
    -not $graveyardBlueScript.Success) {
    throw 'Could not isolate the three Spirit''s Grave child scripts.'
}
$graveyardRedBody = $graveyardRedScript.Groups['body'].Value -replace '(?m);.*$', ''
$graveyardGreenBody = $graveyardGreenScript.Groups['body'].Value -replace '(?m);.*$', ''
$graveyardBlueBody = $graveyardBlueScript.Groups['body'].Value -replace '(?m);.*$', ''
$graveyardRedCommands = [regex]::Match(
    $graveyardRedBody,
    '(?ms)^\s*checkmemoryeq wTmpcfc0\.genericCutscene\.cfd1, \$02\s+writeobjectbyte Interaction\.var39, \$01\s+wait (?<freeze1>\d+)\s+showtext TX_(?<red1>[0-9a-f]{4})\s+wait (?<post1>\d+)\s+setanimation \$(?<left>[0-9a-f]{2})\s+wait (?<freeze2>\d+)\s+showtext TX_(?<red2>[0-9a-f]{4})\s+wait (?<post2>\d+)\s+setanimation \$(?<up>[0-9a-f]{2})\s+wait (?<freeze3>\d+)\s+showtext TX_(?<red3>[0-9a-f]{4})\s+wait (?<final>\d+)\s+writememory wTmpcfc0\.genericCutscene\.cfd1, \$03\s+^boyShakeWithFearThenRun:\s+writeobjectbyte Interaction\.var39, \$01\s+writeobjectbyte Interaction\.var38, (?<shake>\d+)\s+^@shake:\s+asm15 scriptHelp\.oscillateXRandomly\s+addobjectbyte Interaction\.var38, -1\s+jumpifobjectbyteeq Interaction\.var38, \$00, @runAway\s+wait 1\s+scriptjump @shake\s+^@runAway:\s+playsound SND_THROW\s+writeobjectbyte Interaction\.var39, \$00\s+setspeed SPEED_200\s+moveright \$(?<flee>[0-9a-f]{2})\s+scriptend\s*$')
$graveyardGreenCommands = [regex]::Match(
    $graveyardGreenBody,
    '(?ms)^\s*wait (?<pre>\d+)\s+showtext TX_(?<text>[0-9a-f]{4})\s+wait (?<post>\d+)\s+writememory\s+wTmpcfc0\.genericCutscene\.cfd1, \$01\s+checkmemoryeq wTmpcfc0\.genericCutscene\.cfd1, \$03\s+scriptjump boyShakeWithFearThenRun\s*$')
$graveyardBlueCommands = [regex]::Match(
    $graveyardBlueBody,
    '(?ms)^\s*wait (?<pre>\d+)\s+showtextlowindex <TX_(?<text>[0-9a-f]{4})\s+writememory\s+wTmpcfc0\.genericCutscene\.cfd1, \$02\s+checkmemoryeq wTmpcfc0\.genericCutscene\.cfd1, \$03\s+scriptjump boyShakeWithFearThenRun\s*$')
if (-not $graveyardRedCommands.Success -or
    -not $graveyardGreenCommands.Success -or
    -not $graveyardBlueCommands.Success -or
    $graveyardRedCommands.Groups['freeze1'].Value -ne '32' -or
    $graveyardRedCommands.Groups['freeze2'].Value -ne '32' -or
    $graveyardRedCommands.Groups['freeze3'].Value -ne '32' -or
    $graveyardRedCommands.Groups['post1'].Value -ne '30' -or
    $graveyardRedCommands.Groups['post2'].Value -ne '30' -or
    $graveyardRedCommands.Groups['final'].Value -ne '60' -or
    $graveyardRedCommands.Groups['shake'].Value -ne '120' -or
    $graveyardGreenCommands.Groups['pre'].Value -ne '30' -or
    $graveyardGreenCommands.Groups['post'].Value -ne '30' -or
    $graveyardBlueCommands.Groups['pre'].Value -ne '30') {
    throw 'The Spirit''s Grave child script waits or synchronization sequence changed.'
}

$graveyardJump = [regex]::Match(
    $graveyardHelperSource,
    '(?ms)^startJump:\s+ld bc,-\$(?<speed>[0-9a-f]{3})\s+call objectSetSpeedZ\s+ld a,SND_JUMP\s+jp playSound')
$graveyardOscillation = [regex]::Match(
    $graveyardOscillationSource,
    '(?ms)^interactionOscillateXRandomly:\s+call getRandomNumber\s+and \$01\s+sub \$01\s+ld h,d\s+ld l,Interaction\.var3d\s+add \(hl\)\s+ld l,Interaction\.xh\s+ld \(hl\),a\s+ret')
$graveyardFastSpeed = [regex]::Match(
    $speedSource,
    '(?m)^\s*SPEED_200\s+dsb\s+\d+\s*;\s*0x(?<value>[0-9a-f]{2})')
$graveyardMusicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
$graveyardJumpSound = [regex]::Match(
    $graveyardMusicSource,
    '(?m)^\s*SND_JUMP\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
$graveyardFleeSound = [regex]::Match(
    $graveyardMusicSource,
    '(?m)^\s*SND_THROW\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $graveyardJump.Success -or
    [Convert]::ToInt32($graveyardJump.Groups['speed'].Value, 16) -ne 0x1c0 -or
    -not $graveyardOscillation.Success -or
    -not $graveyardFastSpeed.Success -or
    $graveyardFastSpeed.Groups['value'].Value -ne '50' -or
    -not $graveyardJumpSound.Success -or
    $graveyardJumpSound.Groups['value'].Value -ne '53' -or
    -not $graveyardFleeSound.Success -or
    $graveyardFleeSound.Groups['value'].Value -ne '51') {
    throw 'The Spirit''s Grave jump, shake RNG, speed, or sound constants changed.'
}

$graveyardNpcRows = @($npcRows | Where-Object { $_ -match '^0\t7b\t' })
if ($graveyardNpcRows.Count -ne 3 -or
    -not ($graveyardNpcRows | Where-Object { $_ -match '^0\t7b\t3c\t03\t48\t48\t' }) -or
    -not ($graveyardNpcRows | Where-Object { $_ -match '^0\t7b\t3c\t04\t48\t68\t' }) -or
    -not ($graveyardNpcRows | Where-Object { $_ -match '^0\t7b\t3f\t02\t38\t58\t' })) {
    throw 'The three positioned room 0:7b child NPC records changed.'
}
$graveyardTextIds = @(
    [Convert]::ToInt32($graveyardGreenCommands.Groups['text'].Value, 16),
    [Convert]::ToInt32($graveyardBlueCommands.Groups['text'].Value, 16),
    [Convert]::ToInt32($graveyardRedCommands.Groups['red1'].Value, 16),
    [Convert]::ToInt32($graveyardRedCommands.Groups['red2'].Value, 16),
    [Convert]::ToInt32($graveyardRedCommands.Groups['red3'].Value, 16)
)
if (($graveyardTextIds -join ',') -ne '9489,10513,9490,9491,9492' -or
    @($graveyardTextIds | Where-Object { -not $allTexts.ContainsKey($_) }).Count -ne 0 -or
    @($graveyardTextIds | Where-Object { $allTextPositions.ContainsKey($_) }).Count -ne 0) {
    throw 'The Spirit''s Grave dialogue IDs or automatic textbox positions changed.'
}

$graveyardEventRows = @(
    "# group`troom`troom-flag`tred-id`tred-subid`tred-palette`tred-initial-animation`tgreen-id`tgreen-subid`tgreen-initial-animation`tblue-id`tblue-subid`tblue-initial-animation`tgreen-initial-wait`tjump-speed-z`tjump-gravity`tjump-sound`tpost-jump-wait`tgreen-post-text-wait`tred-freeze-wait`tred-post-text-wait`tred-left-animation`tred-up-animation`tred-final-wait`tshake-frames`tflee-speed`tflee-counter`tflee-angle`tflee-animation`tflee-sound",
    (@(
        '0', '7b', '40', '3c', '03', '02', '02', '3c', '04', '00',
        '3f', '02', '02', '60', '-448', '32',
        $graveyardJumpSound.Groups['value'].Value,
        $graveyardGreenCommands.Groups['pre'].Value,
        $graveyardGreenCommands.Groups['post'].Value,
        $graveyardRedCommands.Groups['freeze1'].Value,
        $graveyardRedCommands.Groups['post1'].Value,
        $graveyardRedCommands.Groups['left'].Value,
        $graveyardRedCommands.Groups['up'].Value,
        $graveyardRedCommands.Groups['final'].Value,
        $graveyardRedCommands.Groups['shake'].Value,
        $graveyardFastSpeed.Groups['value'].Value,
        $graveyardRedCommands.Groups['flee'].Value,
        '08', '01', $graveyardFleeSound.Groups['value'].Value
    ) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\graveyard_ghost_kids_event.tsv'),
    $graveyardEventRows)

$graveyardTextActors = @('Green', 'Blue', 'Red', 'Red', 'Red')
$graveyardTextRows = [Collections.Generic.List[string]]::new()
$graveyardTextRows.Add("# order`tactor`ttext-id`ttext-base64")
for ($index = 0; $index -lt $graveyardTextIds.Count; $index++) {
    $textId = $graveyardTextIds[$index]
    $graveyardTextRows.Add((@(
        $index.ToString(), $graveyardTextActors[$index], $textId.ToString('x4'),
        [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$textId]))
    ) -join "`t"))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\graveyard_ghost_kids_text.tsv'),
    $graveyardTextRows)

# The first Impa encounter is INTERAC_IMPA_IN_CUTSCENE ($31:$00) in present
# room $6a. It creates three fake Octoroks from extra object data, replaces
# Link with linkCutscene1, runs impaScript0, and finally installs Impa as the
# 16-entry delayed follower. Export every event counter, actor record,
# animation, text, and possessed PALH_97 sprite color used by that slice.
$impaSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\impaInCutscene.s')
$impaFakeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\fakeOctorok.s')
$impaLinkSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\specialObjects\linkInCutscene.s')
$impaScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$impaExtraObjects = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\impa_intro_event.tsv'),
    $impaEventRows)

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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\impa_intro_commands.tsv'),
    $impaCommandRows)

# Room 0:59 continues the same retained INTERAC_IMPA_IN_CUTSCENE object.
# Export the complete two-object handshake: Impa subid $00/linkCutscene2,
# INTERAC_TRIFORCE_STONE ($34:$00), the post-move PART_TRIFORCE_STONE
# ($5a:$5a), and both Impa scripts on either side of the push.
$impaStoneSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\triforceStone.s')
$impaStonePartSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\parts\triforceStone.s')
$impaScriptHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$musicConstantSource = Read-ImportText (
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
    (Read-ImportText $impaStoneSpriteProperties),
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\impa_stone_event.tsv'),
    @("# $stoneHeader", ($stoneColumns -join "`t")))

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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\impa_stone_prepush_commands.tsv'),
    $impaMoveAwayCommandRows)

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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\impa_stone_postpush_commands.tsv'),
    $impaRockMovedCommandRows)

Export-PaletteBlock 'paletteData4428' 4 'metadata\impa_stone_palette.bin'
Copy-Item -LiteralPath $impaStoneSpriteSource.FullName -Destination (
    Join-Path $destination 'gfx\spr_triforcestone.png') -Force

# Room $7a's unpositioned INTERAC_MISCELLANEOUS_1 ($6b:$00) owns the
# "HELLLLP!!!" edge trigger immediately before the Impa encounter. Export its
# edge check, textbox gate, post-text counter, simulated input, and separate
# room flag instead of folding them into room $6a's interaction.
$impaHelpSource = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\impa_help_event.tsv'),
    $impaHelpRows)

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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\impa_help_commands.tsv'),
    $impaHelpCommandRows)

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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\impa_intro_octoroks.tsv'),
    $impaFakeRows)

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
Write-GeneratedBytes(
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

# This is the single normalized command contract consumed by the importer,
# runtime decoder, runner, actor preflight, and validation. Source aliases and
# byte shapes retain the assembly origin even when one source operation expands
# into a controller-native runtime command.
$cutsceneVocabularyRows = @(
    "# opcode`tsource-aliases`tbyte-shape`tcommand-type`tactor-shape`targ0-shape`targ1-shape`tpayload-shape`tresults`tactor-members`tcapabilities`tdescription",
    "disableinput`tdisableinput`t1`tCutsceneDisableInputCommand`tnone`tnone`tnone`tnone`tcontinue`t-`tinput`tDisable Link and the menu.",
    "disablemenu`tdisablemenu`t1`tCutsceneDisableMenuCommand`tnone`tnone`tnone`tnone`tcontinue`t-`tmenu`tDisable the menu.",
    "setdisabledobjects`tdisableallobjects`t1`tCutsceneSetDisabledObjectsCommand`tnone`thex`tnone`tnone`tyield`t-`tdisabled-objects`tWrite the disabled-object mask and yield.",
    "setdisabledobjectscontinue`tnative:wDisabledObjects`truntime`tCutsceneSetDisabledObjectsContinueCommand`tnone`thex`tnone`tnone`tcontinue`t-`tdisabled-objects`tWrite the disabled-object mask and continue.",
    "setcounter`tsetcounter1`t2`tCutsceneSetCounterCommand`tnone`tdecimal`tnone`tnone`tcontinue`t-`tcounter`tInstall counter1 and continue.",
    "waitpreloadedcounter`tnative:counter1`truntime`tCutsceneWaitPreloadedCounterCommand`tnone`tnone`tnone`tnone`tblock|continue`t-`tcounter`tDecrement an already installed counter1.",
    "wait`twait`t1-or-more`tCutsceneWaitCommand`tnone`tdecimal`tnone`tnone`tblock|continue`t-`tcounter`tInstall and wait on script counter1.",
    "waitframes`tcontroller:waitframes`truntime`tCutsceneWaitFramesCommand`tnone`tpositive-decimal`tnone`tnone`tblock|yield`t-`tcounter`tWait a controller-owned fixed-update duration.",
    "showtext`tshowtext`t2-or-3`tCutsceneShowTextCommand`tnone`thex`tnone`toptional`tyield`t-`tdialogue`tOpen interaction-script text.",
    "showloadedtext`tshowloadedtext`t1`tCutsceneShowLoadedTextCommand`tnone`tnone`tnone`tnone`tyield`t-`tdialogue`tOpen the interaction's currently loaded text.",
    "checktext`tchecktext`t1`tCutsceneCheckTextCommand`tnone`tnone`tnone`tnone`tblock|continue`t-`tdialogue`tHold until interaction text is inactive.",
    "dialogue`tcontroller:dialogue`truntime`tCutsceneDialogueCommand`tnone`thex`tnone`toptional`tblock|yield`t-`tdialogue`tOpen controller text and retain its close boundary.",
    "showtextdifferentforlinked`tshowtextdifferentforlinked`t4`tCutsceneShowTextVariantsCommand`tnone`thex`thex`ttext-variants`tyield`t-`tdialogue|linked-state`tSelect linked or unlinked text.",
    "setanimation`tsetanimation`t2-or-3`tCutsceneSetAnimationCommand`trequired`thex`tnone`toptional`tyield`tActorId`tactor-animation`tSelect a literal or encoded actor animation.",
    "setanimationcontinue`tasm15:setanimation`truntime`tCutsceneSetAnimationContinueCommand`trequired`thex`tnone`trequired`tcontinue`tActorId`tactor-animation`tSelect an actor animation and continue.",
    "setcollisionradii`tsetcollisionradii`t3`tCutsceneSetCollisionRadiiCommand`trequired`thex`thex`tnone`tyield`tActorId`tactor-collision`tWrite actor collision radii.",
    "makeabuttonsensitive`tmakeabuttonsensitive`t1`tCutsceneMakeAButtonSensitiveCommand`trequired`tnone`tnone`tnone`tcontinue`tActorId`tactor-button`tRegister an actor as a talk target.",
    "initcollisions`tinitcollisions`t1`tCutsceneInitCollisionsCommand`trequired`tnone`tnone`tnone`tcontinue`tActorId`tactor-collision|actor-button`tInstall standard radii and register a talk target.",
    "checkabutton`tcheckabutton`t1`tCutsceneCheckAButtonCommand`trequired`tnone`tnone`tnone`tblock|continue`tActorId`tactor-button`tHold until the actor consumes an A press.",
    "gate`tcontroller:gate`truntime`tCutsceneGateCommand`tnone`tnone`tnone`trequired`tblock|yield`t-`tgate-read`tHold on a named controller gate.",
    "checkmemoryeq`tcheckmemoryeq`t4`tCutsceneMemoryGateCommand`tnone`thex`tnone`trequired`tblock|yield`t-`tmemory-read`tHold until a WRAM binding equals the operand.",
    "jumpifmemoryeq`tjumpifmemoryeq`t6`tCutsceneMemoryBranchCommand`tnone`thex`tdecimal`trequired`tcontinue`t-`tmemory-read`tConditionally branch on a WRAM binding.",
    "jumptablememory`tjumptable_objectbyte`t2+table`tCutsceneMemoryJumpTableCommand`tnone`tnone`tnone`tmemory-jump-table`tcontinue`t-`tmemory-read`tIndex a normalized branch table with a binding.",
    "jumpifroomflagset`tjumpifroomflagset`t4`tCutsceneRoomFlagBranchCommand`tnone`thex`tdecimal`tnone`tcontinue`t-`troom-flag-read`tBranch when a room flag is set.",
    "jumpiftradeitemeq`tjumpiftradeitemeq`t4`tCutsceneTradeItemBranchCommand`tnone`thex`tdecimal`tnone`tcontinue`t-`ttrade-item-read`tBranch when the obtained trade item matches.",
    "jumpiftextoptioneq`tjumpiftextoptioneq`t4`tCutsceneTextOptionBranchCommand`tnone`thex`tdecimal`tnone`tcontinue`t-`ttext-option-read`tBranch on the selected text option.",
    "scriptjump`tscriptjump`t2`tCutsceneBranchCommand`tnone`tdecimal`tnone`tnone`tcontinue`t-`t-`tJump and continue dispatch.",
    "callscript`tcallscript`t3`tCutsceneCallCommand`tnone`tdecimal`tnone`tnone`tyield`t-`tcall-stack`tStore a return address and transfer next update.",
    "return`tretscript`t1`tCutsceneReturnCommand`tnone`tnone`tnone`tnone`tyield`t-`tcall-stack`tRestore a return address next update.",
    "setspeed`tsetspeed`t2`tCutsceneSetSpeedCommand`trequired`thex`tnone`tnone`tyield`tActorId`tactor-registers`tWrite the actor speed register.",
    "setangle`tsetangle`t2`tCutsceneSetAngleCommand`trequired`thex`tnone`tnone`tyield`tActorId`tactor-registers`tWrite the actor angle register.",
    "applyspeed`tapplyspeed`t1-or-2`tCutsceneApplySpeedCommand`trequired`thex`tnone`tnone`tblock|yield`tActorId`tactor-movement`tApply registered speed and angle while counter2 is nonzero.",
    "move`tmoveup|moveright|movedown|moveleft`t2`tCutsceneMoveCommand`trequired`thex`thex`trequired`tblock|yield`tActorId`tactor-animation|actor-movement`tRun a cardinal actor movement command.",
    "jump`tcallscript:jumpAndWaitUntilLanded`truntime`tCutsceneJumpCommand`trequired`tdecimal`thex`thex`tblock|yield`tActorId`tactor-z|sound`tRun the typed jump-and-land subscript.",
    "writeobjectbyte`twriteobjectbyte`t3`tCutsceneWriteObjectByteCommand`trequired`thex`thex`tnone`tyield`tActorId`tactor-object-write`tWrite an Interaction byte.",
    "writememory`twritememory`t4`tCutsceneWriteMemoryCommand`tnone`thex`tnone`trequired`tcontinue`t-`tmemory-write`tWrite one WRAM byte and continue.",
    "giveitem`tgiveitem`t3`tCutsceneGiveItemCommand`tnone`thex`thex`tnone`tyield`t-`titem-give`tCreate a treasure for immediate collection.",
    "playsound`tscriptCmd_playsound`t2`tCutscenePlaySoundCommand`tnone`thex`tnone`tnone`tyield`t-`tsound`tQueue a sound effect.",
    "setmusic`tsetmusic`t2`tCutsceneSetMusicCommand`tnone`thex`tnone`tnone`tyield`t-`tmusic`tSelect the active music track.",
    "flicker`tasm15:objectFlickerVisibility`truntime`tCutsceneFlickerCommand`trequired`thex`thex`tnone`tblock|continue`tActorId`tactor-visible|frame-counter`tRun the recognized visibility flicker loop.",
    "translate`tcontroller:translate`truntime`tCutsceneTranslateCommand`trequired`tpositive-decimal`tdecimal`ttranslation`tblock|yield`tActorId`tactor-position|actor-animation`tTranslate one actor over fixed updates.",
    "paralleltranslate`tcontroller:paralleltranslate`truntime`tCutsceneParallelTranslateCommand`trequired`tpositive-decimal`tpositive-decimal`tparallel-translation`tblock|yield`tActorId|Actor2Id`tactor-position`tTranslate two actors in stable order.",
    "deleteactor`tcontroller:deleteactor`truntime`tCutsceneDeleteActorCommand`trequired`tnone`tnone`tnone`tend`tActorId`tactor-delete`tDelete an actor and end the stream.",
    "setglobalflag`tcontroller:setglobalflag`truntime`tCutsceneSetGlobalFlagCommand`tnone`thex`tnone`tnone`tcontinue`t-`tglobal-flag-write`tSet a global flag and continue.",
    "orroomflag`torroomflag`t2`tCutsceneOrRoomFlagCommand`tnone`thex`tnone`tnone`tyield`t-`troom-flag-write`tOR the room flags and yield.",
    "orroomflagcontinue`tnative:orRoomFlags`truntime`tCutsceneOrRoomFlagContinueCommand`tnone`thex`tnone`tnone`tcontinue`t-`troom-flag-write`tOR the room flags and continue.",
    "native`tasm15`t3-or-4`tCutsceneNativeCommand`tnone`tnone`tnone`trequired`tcontinue`t-`tnative`tRun a native handler and continue.",
    "nativeyield`tasm15:yield`truntime`tCutsceneNativeYieldCommand`tnone`tnone`tnone`trequired`tyield`t-`tnative`tRun a native handler and yield.",
    "nativeblock`tasm15:block`truntime`tCutsceneNativeBlockingCommand`toptional`tpositive-decimal`tnone`tnative-block`tblock|yield`tActor?`tnative-block`tUpdate an event-specific native handler until complete.",
    "enableinput`tenableinput`t1`tCutsceneEnableInputCommand`tnone`tnone`tnone`tnone`tcontinue`t-`tinput`tEnable Link and the menu.",
    "scriptend`tscriptend`t1`tCutsceneEndCommand`tnone`tnone`tnone`tnone`tend`t-`tscript-end`tEnd the interaction script."
)
$cutsceneCommandSchemas = @{}
foreach ($row in $cutsceneVocabularyRows | Select-Object -Skip 1) {
    $columns = $row.Split([char]"`t")
    if ($columns.Count -ne 12) {
        throw "Cutscene command schema row '$($columns[0])' has " +
            "$($columns.Count) columns instead of 12."
    }
    $opcode = $columns[0]
    if ($cutsceneCommandSchemas.ContainsKey($opcode)) {
        throw "Duplicate cutscene command schema opcode '$opcode'."
    }
    $cutsceneCommandSchemas.Add($opcode, [pscustomobject]@{
        ActorShape = $columns[4]
        Arg0Shape = $columns[5]
        Arg1Shape = $columns[6]
        PayloadShape = $columns[7]
    })
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\script_command_vocabulary.tsv'),
    $cutsceneVocabularyRows)

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
    'nayruScript00_part1',
    'nayruScript00_part2',
    'ralphSubid00Script',
    'ghostVeranSubid1Script_part2'
)
foreach ($lane in $nayruLaneSpecs) {
    $parsedLane = @(Read-AssemblyCutsceneCommands `
        $nayruScriptPath $lane $supportedNayruOpcodes)
    if ($parsedLane[-1].Opcode -ne 'scriptend') {
        throw "$nayruScriptPath`:$($parsedLane[-1].Line): $lane does not terminate in scriptend."
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\nayru_intro_commands.tsv'),
    $nayruCommandRows)

# Room 1:75's pre-Black Tower sequence is seven synchronized interaction
# lanes. Export the original per-actor scripts independently; runtime advances
# them in placement order and preserves their cfc0/cfd0 gates.
$preBlackTowerMainScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$preBlackTowerHelperScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
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
        [string]$nextLabel,
        [string]$outputName)

    $parsed = @(Read-AssemblyCutsceneCommands `
        $path $script $preBlackTowerOpcodes $nextLabel)
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
    Write-GeneratedTable(
        (Join-Path $destination "cutscenes\$outputName"),
        $rows)
}

$preBlackTowerLaneSpecs = @(
    @('ralphSubid0aScript_unlinked', 'Ralph', $preBlackTowerMainScriptPath, 'ralphSubid0aScript_linked', 'pre_black_tower_ralph_unlinked.tsv'),
    @('ralphSubid0aScript_linked', 'Ralph', $preBlackTowerMainScriptPath, 'ralphSubid0bScript', 'pre_black_tower_ralph_linked.tsv'),
    @('impaScript4', 'Impa', $preBlackTowerHelperScriptPath, 'impaScript5', 'pre_black_tower_impa_unlinked.tsv'),
    @('impaScript5', 'Impa', $preBlackTowerHelperScriptPath, 'impaScript7', 'pre_black_tower_impa_linked.tsv'),
    @('nayruScript09', 'Nayru', $preBlackTowerMainScriptPath, 'nayruScript0a', 'pre_black_tower_nayru_unlinked.tsv'),
    @('nayruScript0a', 'Nayru', $preBlackTowerMainScriptPath, 'nayruScript10', 'pre_black_tower_nayru_linked.tsv'),
    @('zeldaSubid04Script', 'Zelda', $preBlackTowerMainScriptPath, 'zeldaSubid05Script', 'pre_black_tower_zelda_linked.tsv')
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\pre_black_tower_event.tsv'),
    $preBlackTowerEventRows)

# Room 1:86's guard starts stage 0 of CUTSCENE_BLACK_TOWER_EXPLANATION, then
# resumes at @cutsceneAftermath after the cutscene's same-room transition $0c.
$blackTowerScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$blackTowerScriptSource = Read-ImportText $blackTowerScriptPath
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\black_tower_guard_first.tsv'),
    $blackTowerFirstRows)

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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\black_tower_guard_aftermath.tsv'),
    $blackTowerAfterRows)

$blackTowerCutsceneSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
if ($blackTowerCutsceneSource -notmatch '(?ms)^blackTowerExplanationCutsceneHandler:.*?^@@table_6625:\s+\.db GFXH_BLACK_TOWER_STAGE_1_LAYOUT, GFXH_BLACK_TOWER_BASE' -or
    $blackTowerCutsceneSource -notmatch '(?ms)^func_6ef7:.*?and \$1f.*?call getRandomNumber.*?and \$07.*?SND_LIGHTNING' -or
    $blackTowerCutsceneSource -notmatch '(?ms)^func_6f44:.*?oamData_714c') {
    throw 'Black Tower explanation stage-0 presentation changed.'
}
$blackTowerOamSource = Read-ImportText (Join-Path $Disassembly 'ages.s')
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\black_tower_stage_0_oam.tsv'),
    $blackTowerOamRows)

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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\black_tower_entrance_event.tsv'),
    $blackTowerEventRows)

# Room $1:$76 contains INTERAC_MISCELLANEOUS_2 $dc:$10 rather than a visible
# NPC. It opens the two entrance metatiles and arms a collision rectangle that
# selects one of two hardcoded Black Tower rooms from this room's bit $01.
# Keep the placement, state-machine inputs, flag predicate, raw warp bytes, and
# sound tied to their disassembly definitions instead of encoding them in the
# runtime controller.
$towerDoorObjectSource = Read-ImportText (
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

$towerDoorSource = Read-ImportText (
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

$linkSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\link.s')
$linkRadii = [regex]::Match(
    $linkSource,
    '(?ms); Set collisionRadiusY,X\s*inc l\s*ld a,\$(?<radius>[0-9a-f]{2})\s*ldi \(hl\),a\s*ldi \(hl\),a')
$musicConstantSource = Read-ImportText (
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
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\black_tower_doorway_event.tsv'),
    $towerDoorRows)

# Room $1:$38 is the first Maku Sprout rescue. Its placed sprout creates a
# native controller, which in turn creates two scripted Moblin interactions;
# those actors replace themselves with ordinary masked-Moblin enemies. Keep
# all four source script lanes distinct so their original object update order
# and shared wTmpcfc0/wccd4 synchronization remain observable at runtime.
$makuObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\mainData.s')
$makuPlacement = [regex]::Match(
    $makuObjectSource,
    '(?ms)^group1Map38ObjectData:\s*obj_Interaction \$88 \$00 \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})\s*obj_Interaction \$6b \$15 \$(?<statuey>[0-9a-f]{2}) \$(?<statuex>[0-9a-f]{2})')
$makuObjectDataSource = Read-ImportText (
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
    $makuPlacement.Groups['statuey'].Value -ne '40' -or
    $makuPlacement.Groups['statuex'].Value -ne '84' -or
    $makuMoblins.Groups['y'].Value -ne '30' -or
    $makuMoblins.Groups['leftx'].Value -ne '68' -or
    $makuMoblins.Groups['rightx'].Value -ne '38') {
    throw 'Room 1:38 Maku Sprout/Moblin placements changed.'
}

$makuScriptsSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$makuHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$makuInteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\makuSprout.s')
$makuGateSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\makuGateOpening.s')
$makuMiscellaneousSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous1.s')
if ($makuScriptsSource -notmatch '(?ms)^makuSprout_subid01Script:.*?GLOBALFLAG_MAKU_TREE_SAVED.*?INTERAC_MISCELLANEOUS_1, \$04, \$40, \$50.*?TX_05d5' -or
    $makuScriptsSource -notmatch '(?ms)^moblin_subid00Script:.*?moblin_spawnEnemyHere.*?^moblin_subid01Script:' -or
    $makuHelperSource -notmatch '(?ms)^interaction6b_subid04Script:.*?wDisableScreenTransitions, \$01.*?INTERAC_MAKU_GATE_OPENING.*?GLOBALFLAG_MAKU_TREE_SAVED.*?wDisableScreenTransitions, \$00') {
    throw 'Room 1:38 rescue script ownership or completion predicates changed.'
}
if ($makuInteractionSource -notmatch '(?ms)^@initSubid0:.*?\.dw @state00.*?\.dw @state10' -or
    $makuInteractionSource -notmatch '(?ms)^@state03:\s*@state04:\s*@state05:\s*ldbc \$01, <TX_0570' -or
    $makuInteractionSource -notmatch '(?ms)^@state06:\s*ldbc \$00, <TX_0576.*?^@state07:\s*ldbc \$00, <TX_0578.*?^@state08:\s*ldbc \$02, <TX_057a' -or
    $makuInteractionSource -notmatch '(?ms)^@state09:\s*ldbc \$01, <TX_057c.*?^@state0a:\s*ldbc \$01, <TX_057e.*?^@state0b:\s*ldbc \$00, <TX_0580' -or
    $makuInteractionSource -notmatch '(?ms)^@state0c:\s*ldbc \$00, <TX_0582.*?^@state0d:\s*ldbc \$01, <TX_0584.*?^@state0e:\s*ldbc \$01, <TX_0586' -or
    $makuInteractionSource -notmatch '(?ms)^@state0f:\s*ldbc \$02, <TX_0588.*?^@state10:.*?checkIsLinkedGame.*?ldbc \$00, <TX_058a.*?ldbc \$01, <TX_058c' -or
    $makuInteractionSource -notmatch '(?ms)^@initializeMakuSprout:.*?interactionSetAlwaysUpdateBit' -or
    $makuInteractionSource -notmatch '(?ms)^@loadScriptAndInitGraphics:.*?>TX_0500') {
    throw 'INTERAC_MAKU_SPROUT state, text, or always-update behavior changed.'
}
if ($makuHelperSource -notmatch '(?ms)^makuSprout_subid00Script_body:.*?@mode00_showDifferentTextFirstTime_distressedAnim:.*?makuSprout_setAnimation, \$02.*?makuTree_showTextWithOffsetAndUpdateMapText, \$00.*?makuTree_showTextWithOffsetAndUpdateMapText, \$01' -or
    $makuHelperSource -notmatch '(?ms)^@mode01_happyAnimationWhileTalking:.*?makuSprout_setAnimation, \$00.*?makuSprout_setAnimation, \$01.*?makuTree_showTextWithOffsetAndUpdateMapText, \$00.*?wait 1.*?makuSprout_setAnimation, \$00' -or
    $makuHelperSource -notmatch '(?ms)^@mode02_showDifferentTextFirstTime:.*?makuSprout_setAnimation, \$00.*?makuTree_showTextWithOffsetAndUpdateMapText, \$00.*?makuTree_showTextWithOffsetAndUpdateMapText, \$01' -or
    $makuHelperSource -notmatch '(?ms)^makuTree_checkLinkedAndUpdateMapText:.*?wMakuMapTextPresent.*?ld \(hl\),c' -or
    $makuHelperSource -notmatch '(?ms)^makuTree_textOffsetsForLinked:\s*\.db \$20, \$20, \$10') {
    throw 'Maku Sprout advice script modes or linked/map-text helper changed.'
}
if ($makuMiscellaneousSource -notmatch '(?ms)^interaction6b_subid15:.*?GLOBALFLAG_FINISHEDGAME.*?wRoomCollisions.*?\$0f.*?interaction6b_subid0e@state0' -or
    $makuMiscellaneousSource -notmatch '(?ms)^interaction6b_subid0e:.*?TILESETFLAG_PAST.*?PALH_c7.*?ld bc,\$080a.*?cp \$f9.*?ld a,\$04.*?interactionAnimateAsNpc') {
    throw 'Room 1:38 postgame Link-statue behavior changed.'
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
$makuStatueGraphic = $interactionGraphics['107:21']
if ($null -eq $makuStatueGraphic -or
    $makuStatueGraphic.Gfx -ne 0x6d -or
    $gfxNames[0x6d] -ne 'spr_linkstatue' -or
    $makuStatueGraphic.TileBase -ne 0 -or
    $makuStatueGraphic.Palette -ne 6 -or
    $makuStatueGraphic.DefaultAnimation -ne 4) {
    throw 'INTERAC_MISCELLANEOUS_1 $6b:$15 graphics changed.'
}
$makuStatueAnimations = @(4..5 | ForEach-Object {
    Resolve-NpcAnimation 0x6b $_
})
$makuStatueProperties = Read-ImportText (
    Join-Path $Disassembly 'gfx_compressible\ages\spr_linkstatue.properties')
if ($makuStatueProperties -notmatch '(?m)^invert:\s*false\s*$') {
    throw 'spr_linkstatue.properties no longer selects non-inverted grayscale.'
}
if (-not $globalFlagValues.ContainsKey('GLOBALFLAG_FINISHEDGAME') -or
    $globalFlagValues['GLOBALFLAG_FINISHEDGAME'] -ne 0x14 -or
    $paletteHeaderSource -notmatch '(?ms)m_PaletteHeaderStart \$c7, PALH_c7\s*m_PaletteHeaderSpr 6, 1, paletteData44f0') {
    throw 'Room 1:38 finished-game flag or past Link-statue palette changed.'
}
if (-not $allTextPositions.ContainsKey(0x05d4) -or
    $allTextPositions[0x05d4] -ne 2) {
    throw 'TX_05d4 no longer explicitly selects textbox position 2.'
}

$makuAdviceDefinitions = @(
    [pscustomobject]@{ State=0x00; StandardMode=0; StandardBase=0x0500; LinkedMode=0; LinkedBase=0x0520 },
    [pscustomobject]@{ State=0x03; StandardMode=1; StandardBase=0x0570; LinkedMode=1; LinkedBase=0x0590 },
    [pscustomobject]@{ State=0x04; StandardMode=1; StandardBase=0x0570; LinkedMode=1; LinkedBase=0x0590 },
    [pscustomobject]@{ State=0x05; StandardMode=1; StandardBase=0x0570; LinkedMode=1; LinkedBase=0x0590 },
    [pscustomobject]@{ State=0x06; StandardMode=0; StandardBase=0x0576; LinkedMode=0; LinkedBase=0x0596 },
    [pscustomobject]@{ State=0x07; StandardMode=0; StandardBase=0x0578; LinkedMode=0; LinkedBase=0x0598 },
    [pscustomobject]@{ State=0x08; StandardMode=2; StandardBase=0x057a; LinkedMode=2; LinkedBase=0x059a },
    [pscustomobject]@{ State=0x09; StandardMode=1; StandardBase=0x057c; LinkedMode=1; LinkedBase=0x059c },
    [pscustomobject]@{ State=0x0a; StandardMode=1; StandardBase=0x057e; LinkedMode=1; LinkedBase=0x059e },
    [pscustomobject]@{ State=0x0b; StandardMode=0; StandardBase=0x0580; LinkedMode=0; LinkedBase=0x05a0 },
    [pscustomobject]@{ State=0x0c; StandardMode=0; StandardBase=0x0582; LinkedMode=0; LinkedBase=0x05a2 },
    [pscustomobject]@{ State=0x0d; StandardMode=1; StandardBase=0x0584; LinkedMode=1; LinkedBase=0x05a4 },
    [pscustomobject]@{ State=0x0e; StandardMode=1; StandardBase=0x0586; LinkedMode=1; LinkedBase=0x05a6 },
    [pscustomobject]@{ State=0x0f; StandardMode=2; StandardBase=0x0588; LinkedMode=2; LinkedBase=0x05a8 },
    [pscustomobject]@{ State=0x10; StandardMode=1; StandardBase=0x058c; LinkedMode=0; LinkedBase=0x05aa }
)
function Get-MakuAdviceRepeatId([int]$mode, [int]$base) {
    if ($mode -eq 1) { return $base }
    return $base + 1
}
function Get-MakuAdvicePosition([int]$textId) {
    if (-not $allTextPositions.ContainsKey($textId) -or
        $allTextPositions[$textId] -ne 2) {
        throw "Maku Sprout advice TX_$($textId.ToString('x4')) no longer selects textbox position 2."
    }
    return $allTextPositions[$textId]
}
$makuAdviceRows = [Collections.Generic.List[string]]::new()
$makuAdviceRows.Add(
    "# state`tstandard-mode`tlinked-mode`tstandard-first-text-id`tstandard-first-position`tstandard-first-text-base64`tstandard-repeat-text-id`tstandard-repeat-position`tstandard-repeat-text-base64`tlinked-first-text-id`tlinked-first-position`tlinked-first-text-base64`tlinked-repeat-text-id`tlinked-repeat-position`tlinked-repeat-text-base64")
foreach ($definition in $makuAdviceDefinitions) {
    $standardFirst = [int]$definition.StandardBase
    $standardRepeat = Get-MakuAdviceRepeatId $definition.StandardMode $standardFirst
    $linkedFirst = [int]$definition.LinkedBase
    $linkedRepeat = Get-MakuAdviceRepeatId $definition.LinkedMode $linkedFirst
    foreach ($textId in @($standardFirst, $standardRepeat, $linkedFirst, $linkedRepeat)) {
        if (-not $allTexts.ContainsKey($textId)) {
            throw "Missing Maku Sprout advice text TX_$($textId.ToString('x4'))."
        }
    }
    $makuAdviceRows.Add((@(
        $definition.State.ToString('x2'),
        $definition.StandardMode.ToString(),
        $definition.LinkedMode.ToString(),
        $standardFirst.ToString('x4'),
        (Get-MakuAdvicePosition $standardFirst).ToString(),
        (ConvertTo-CutsceneCommandPayload $allTexts[$standardFirst]),
        $standardRepeat.ToString('x4'),
        (Get-MakuAdvicePosition $standardRepeat).ToString(),
        (ConvertTo-CutsceneCommandPayload $allTexts[$standardRepeat]),
        $linkedFirst.ToString('x4'),
        (Get-MakuAdvicePosition $linkedFirst).ToString(),
        (ConvertTo-CutsceneCommandPayload $allTexts[$linkedFirst]),
        $linkedRepeat.ToString('x4'),
        (Get-MakuAdvicePosition $linkedRepeat).ToString(),
        (ConvertTo-CutsceneCommandPayload $allTexts[$linkedRepeat])
    ) -join "`t"))
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\maku_sprout_advice.tsv'),
    $makuAdviceRows)

$makuRoomRows = @(
    "# group`troom`tsprout-id`tsprout-subid`tsprout-y`tsprout-x`tsprout-sprite`tsprout-tile-base`tsprout-palette`tsprout-animation-0`tsprout-animation-1`tsprout-animation-2`tsprout-radius-y`tsprout-radius-x`tsaved-flag`tsaved-text-id`tsaved-text-position`tsaved-text-base64`tfinished-flag`tstatue-id`tstatue-subid`tstatue-y`tstatue-x`tstatue-packed-position`tstatue-collision`tstatue-radius-y`tstatue-radius-x`tstatue-appearance-tile`tstatue-normal-animation`tstatue-alternate-animation`tstatue-sprite`tstatue-tile-base`tstatue-palette`tstatue-source-inverted`tsource",
    (@(
        '1','38','88','00',
        $makuPlacement.Groups['y'].Value,
        $makuPlacement.Groups['x'].Value,
        $gfxNames[$makuSproutGraphic.Gfx],
        $makuSproutGraphic.TileBase.ToString(),
        $makuSproutGraphic.Palette.ToString(),
        $makuSproutAnimations[0],
        $makuSproutAnimations[1],
        $makuSproutAnimations[2],
        '08','08','12','05d5','0',
        (ConvertTo-CutsceneCommandPayload $allTexts[0x05d5]),
        $globalFlagValues['GLOBALFLAG_FINISHEDGAME'].ToString('x2'),
        '6b','15',
        $makuPlacement.Groups['statuey'].Value,
        $makuPlacement.Groups['statuex'].Value,
        '48','0f','08','0a','f9',
        $makuStatueAnimations[0],
        $makuStatueAnimations[1],
        $gfxNames[$makuStatueGraphic.Gfx],
        $makuStatueGraphic.TileBase.ToString(),
        $makuStatueGraphic.Palette.ToString(),
        '0',
        'mainData.s:group1Map38ObjectData; makuSprout.s:interactionCode88; miscellaneous1.s:interaction6b_subid15'
    ) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'objects\maku_sprout_room.tsv'),
    $makuRoomRows)
Copy-GeneratedFile `
    'gfx_compressible\ages\spr_linkstatue.png' `
    'gfx\spr_linkstatue.png'
Export-PaletteBlock `
    'paletteData44f0' 4 'objects\maku_sprout_statue_palette.bin'

$makuActorRows = @(
    "# actor`tid`tsubid`ty`tx`tsprite`ttile-base`tpalette`tup-animation`tright-animation`tdown-animation`tleft-animation",
    (@('Sprout','88','00','28','50',$gfxNames[0x67],$makuSproutGraphic.TileBase,$makuSproutGraphic.Palette,
        $makuSproutAnimations[0],$makuSproutAnimations[0],$makuSproutAnimations[0],$makuSproutAnimations[0]) -join "`t"),
    (@('MoblinLeft','96','00','30','68',$gfxNames[0x90],$makuMoblinGraphic.TileBase,$makuMoblinGraphic.Palette,
        $makuMoblinAnimations[0],$makuMoblinAnimations[1],$makuMoblinAnimations[2],$makuMoblinAnimations[3]) -join "`t"),
    (@('MoblinRight','96','01','30','38',$gfxNames[0x90],$makuMoblinGraphic.TileBase,$makuMoblinGraphic.Palette,
        $makuMoblinAnimations[0],$makuMoblinAnimations[1],$makuMoblinAnimations[2],$makuMoblinAnimations[3]) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\maku_sprout_rescue_actors.tsv'),
    $makuActorRows)

$makuEventRows = @(
    "# group`troom`tsprout-id`tsprout-subid`tcontroller-y`tcontroller-x`tmoblin-id`tmoblin-y`tleft-x`tright-x`tinitial-gate-position`tclear-tile`tgate-left`tgate-inner-left`tgate-inner-right`tgate-right`troom-flag`tadvice-flag`tsaved-flag`tstate-min`tstate-max`tmap-text-low`ttrigger-radius-y`ttrigger-radius-x`tjump-speed-z`tjump-gravity`tjump-sound`tgate-counter`tshake-counter`tfinal-text-position`tpost-text-id`tpost-text-base64",
    (@('1','38','88','00','40','50','96','30','68','38','52','f9','73','74','75','76','80','3f','12','01','02','d6','04','50','-512','30','53','30','06',
        $allTextPositions[0x05d4].ToString(),'05d5',
        (ConvertTo-CutsceneCommandPayload $allTexts[0x05d5])) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\maku_sprout_rescue_event.tsv'),
    $makuEventRows)

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
    Write-GeneratedTable(
        (Join-Path $destination "cutscenes\$file"),
        $rows)
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

# Room 0:5c's INTERAC_MISCELLANEOUS_2 $dc:$01 waits on the signal written by
# nextToOverworldKeyhole, then runs a compact script around two native tile
# removal helpers. Keep the command boundaries sourced from scripts.s while
# preserving the helper's ordered ordinary/interleaved writes and puff spawns.
$graveyardScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$graveyardScriptSource = Read-ImportText $graveyardScriptPath
$graveyardHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$graveyardControllerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous2.s')
$graveyardObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\mainData.s')

if ($graveyardControllerSource -notmatch '(?ms)^interactiondc_subid01:.*?checkInteractionState\s+jp nz,interactionRunScript.*?getThisRoomFlags\s+and \$80\s+jp nz,interactionDelete.*?mainScripts\.interactiondcSubid01Script.*?interactionSetAlwaysUpdateBit' -or
    $graveyardObjectSource -notmatch '(?ms)^group0Map5cObjectData:\s+obj_Interaction \$71 \$05 \$40 \$98\s+obj_Interaction \$dc \$01\s+obj_End' -or
    $graveyardHelperSource -notmatch '(?ms)^interactiondc_removeGraveyardGateTiles1:\s+ld a,\$0a\s+call setScreenShakeCounter\s+ld a,\$3a\s+ld c,\$34\s+call setTile\s+ld a,\$3a\s+ld c,\$44\s+call setTile.*?^@interleavedTiles:\s+\.db \$33 \$3a \$89 \$01\s+\.db \$35 \$3a \$89 \$03\s+\.db \$43 \$98 \$ec \$01\s+\.db \$45 \$9a \$ec \$03' -or
    $graveyardHelperSource -notmatch '(?ms)^interactiondc_removeGraveyardGateTiles2:\s+ld a,\$0a\s+call setScreenShakeCounter\s+ld a,\$3a\s+ld c,\$33\s+call setTile\s+ld a,\$3a\s+ld c,\$35\s+call setTile\s+ld a,\$3a\s+ld c,\$43\s+call setTile\s+ld a,\$3a\s+ld c,\$45\s+call setTile\s+ld bc,\$4830\s+call interactiondc_spawnPuff\s+ld bc,\$4860\s+jp interactiondc_spawnPuff' -or
    $graveyardHelperSource -notmatch '(?ms)^interactiondc_spawnPuff:.*?INTERAC_PUFF.*?Interaction\.yh\s+ld \(hl\),b.*?Interaction\.xh\s+ld \(hl\),c') {
    throw 'Room 0:5c graveyard-gate controller, object order, tile phases, or puff helper changed.'
}

$graveyardSupportedOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'checkcfc0bit', 'setmusic', 'wait', 'asm15', 'resetmusic',
    'playsound', 'enableinput', 'scriptend')) {
    [void]$graveyardSupportedOpcodes.Add($opcode)
}
$graveyardParsed = @(Read-AssemblyCutsceneCommands `
    $graveyardScriptPath 'interactiondcSubid01Script' $graveyardSupportedOpcodes)
$graveyardExpected = @(
    @('checkcfc0bit', '0'),
    @('setmusic', 'SNDCTRL_STOPMUSIC'),
    @('wait', '60'),
    @('asm15', 'scriptHelp.interactiondc_removeGraveyardGateTiles1'),
    @('wait', '45'),
    @('asm15', 'scriptHelp.interactiondc_removeGraveyardGateTiles2'),
    @('wait', '60'),
    @('resetmusic', ''),
    @('playsound', 'SND_SOLVEPUZZLE'),
    @('enableinput', ''),
    @('scriptend', '')
)
if ($graveyardParsed.Count -ne $graveyardExpected.Count) {
    throw "interactiondcSubid01Script expected 11 commands, parsed $($graveyardParsed.Count)."
}
for ($index = 0; $index -lt $graveyardExpected.Count; $index++) {
    $actualOperands = if ($null -eq $graveyardParsed[$index].Operands) {
        ''
    } else {
        ([string]$graveyardParsed[$index].Operands).Trim()
    }
    if ($graveyardParsed[$index].Opcode -ne $graveyardExpected[$index][0] -or
        $actualOperands -ne $graveyardExpected[$index][1]) {
        throw "interactiondcSubid01Script command $index changed from $($graveyardExpected[$index] -join ' ')."
    }
}

$graveyardEventRows = @(
    "# group`troom`tid`tsubid`troom-flag`tclear-tile`tshake-frames`tphase1-ordinary`tphase1-interleaved`tphase1-puffs`tphase2-ordinary`tphase2-puffs`tsource"
    "0`t5c`tdc`t01`t80`t3a`t10`t34,44`t33:3a:89:1,35:3a:89:3,43:98:ec:1,45:9a:ec:3`t48:40,48:50`t33,35,43,45`t48:30,48:60`tinteractiondcSubid01Script;scriptHelper.s:interactiondc_removeGraveyardGateTiles1/2"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\graveyard_gate_event.tsv'),
    $graveyardEventRows)

$graveyardCommandRows = [Collections.Generic.List[string]]::new()
$graveyardCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
$graveyardSpecs = @(
    @('setmusic', '', 'f0', '', ''),
    @('wait', '', '60', '', ''),
    @('native', '', '', '', 'RemoveGateTiles1'),
    @('wait', '', '45', '', ''),
    @('native', '', '', '', 'RemoveGateTiles2'),
    @('wait', '', '60', '', ''),
    @('setmusic', '', 'ff', '', ''),
    @('playsound', '', '4d', '', ''),
    @('enableinput', '', '', '', ''),
    @('scriptend', '', '', '', '')
)
for ($index = 0; $index -lt $graveyardSpecs.Count; $index++) {
    # Skip the leading checkcfc0bit: the room event remains armed until the
    # reusable keyhole controller supplies that exact signal.
    $sourceCommand = $graveyardParsed[$index + 1]
    $spec = $graveyardSpecs[$index]
    $graveyardCommandRows.Add((New-CutsceneCommandRow `
        'interactiondcSubid01Script' $index $sourceCommand.Label `
        $sourceCommand.Line $spec[0] $spec[1] $spec[2] $spec[3] $spec[4]))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\graveyard_gate_commands.tsv'),
    $graveyardCommandRows)

# Present room 0:83's $dc:$02 watches the unique $c3 Bracelet rock. Once
# Link reaches grab state $83, it runs the native Wing Dungeon collapse,
# including the 6x6 BG maps and the persistent 3x3 layout/collision rewrite.
$wingInteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous2.s')
$wingCutsceneSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$wingRoomGfxSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\roomGfxChanges.s')
$wingGfxHeaderSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\gfxHeaders.s')
$wingSingleTileSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\singleTileChanges.s')
$wingObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\mainData.s')
$wingExtraObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\extraData4.s')

if ($wingObjectSource -notmatch '(?ms)^group0Map83ObjectData:\s+obj_Interaction \$d5 \$00 \$28 \$58\s+obj_Interaction \$dc \$02 \$48 \$38\s+obj_End') {
    throw 'Room 0:83 Wing Dungeon object order changed.'
}
if ($wingInteractionSource -notmatch '(?ms)^interactiondc_subid02:.*?interactionDeleteAndRetIfEnabled02.*?objectGetTileAtPosition.*?TILEINDEX_OVERWORLD_STANDARD_GROUND.*?TILEINDEX_OVERWORLD_DUG_DIRT.*?wLinkGrabState.*?cp \$83.*?DIR_RIGHT.*?ld a,30.*?DISABLE_LINK.*?resetLinkInvincibility.*?SNDCTRL_STOPMUSIC.*?ld \(hl\),60.*?ld a,60.*?ld bc,\$f800.*?objectCreateExclamationMark.*?clearAllParentItems.*?dropLinkHeldItem.*?ld a,\$28.*?setScreenShakeCounter.*?CUTSCENE_D2_COLLAPSE') {
    throw 'Room 0:83 interactiondc_subid02 changed.'
}
if ($wingCutsceneSource -notmatch '(?ms)^func_7168:.*?getThisRoomFlags.*?set 7,\(hl\).*?ROOM_AGES_073.*?set 7,\(hl\).*?ld a,\$3c.*?reloadTileMap.*?INTERAC_97.*?ld \(hl\),\$2c.*?ld \(hl\),\$58.*?GFXH_WING_DUNGEON_COLLAPSING_1.*?ld a,\$0f.*?setScreenShakeCounter.*?SND_DOORCLOSE.*?GFXH_WING_DUNGEON_COLLAPSING_2.*?GFXH_WING_DUNGEON_COLLAPSING_3.*?drawCollapsedWingDungeon.*?objectData7e69.*?wDisabledObjects.*?wMenuDisabled.*?wActiveMusic') {
    throw 'CUTSCENE_D2_COLLAPSE changed.'
}
if ($wingRoomGfxSource -notmatch '(?ms)^roomTileChangesAfterLoad00:.*?and \$80.*?ret z.*?^drawCollapsedWingDungeon:.*?GFXH_WING_DUNGEON_COLLAPSED.*?^@tileReplacement:.*?\.db \$06 \$06.*?w3VramTiles\+\$08.*?w2TmpGfxBuffer.*?^@layoutReplacement:.*?wRoomLayout\+\$04.*?\.db \$03 \$03.*?\.db \$3b \$00 \$3b \$00 \$3b \$00.*?\.db \$3b \$00 \$3b \$00 \$3b \$00.*?\.db \$00 \$05 \$00 \$0f \$00 \$0a') {
    throw 'drawCollapsedWingDungeon changed.'
}
if ($wingSingleTileSource -notmatch '(?m)^\s*\.db \$83 \$80 \$43 \$1c\s*$') {
    throw 'Room 0:83 persistent single-tile change changed.'
}
if ($wingExtraObjectSource -notmatch '(?ms)^objectData7e69:\s+obj_Interaction \$8a \$00 \$00 \$00 \$01\s+obj_End') {
    throw 'Wing Dungeon post-collapse remote Maku objectData7e69 changed.'
}

$wingGfxSpecs = @(
    @(0, 0x50, 'map_wing_dungeon_collapsing_1'),
    @(1, 0x51, 'map_wing_dungeon_collapsing_2'),
    @(2, 0x52, 'map_wing_dungeon_collapsing_3'),
    @(3, 0x53, 'map_wing_dungeon_collapsed')
)
$wingMapRows = [Collections.Generic.List[string]]::new()
$wingMapRows.Add("# phase`tgfx-header`ttile-ids`tsource")
foreach ($spec in $wingGfxSpecs) {
    $phase = [int]$spec[0]
    $header = [int]$spec[1]
    $name = [string]$spec[2]
    $headerPattern =
        "(?ms)^m_GfxHeaderStart \`$$($header.ToString('x2')), GFXH_" +
        "(?:WING_DUNGEON_COLLAPSING_$($phase + 1)|WING_DUNGEON_COLLAPSED)" +
        "\s+m_GfxHeader $name, w2TmpGfxBuffer\s+m_GfxHeaderEnd"
    if ($wingGfxHeaderSource -notmatch $headerPattern) {
        throw "Could not verify Wing Dungeon GFX header `$$($header.ToString('x2'))."
    }
    $mapPath = Join-Path $Disassembly "gfx_compressible\ages\$name.bin"
    $bytes = [IO.File]::ReadAllBytes($mapPath)
    if ($bytes.Length -ne 192) {
        throw "$mapPath expected 192 bytes, got $($bytes.Length)."
    }
    $tileIds = [Collections.Generic.List[string]]::new()
    for ($row = 0; $row -lt 6; $row++) {
        for ($column = 0; $column -lt 6; $column++) {
            $tileIds.Add($bytes[$row * 32 + $column].ToString('x2'))
        }
    }
    $wingMapRows.Add(
        "$phase`t$($header.ToString('x2'))`t$($tileIds -join ',')`t" +
        "gfxHeaders.s:GFXH_$($header.ToString('x2'));$name.bin")
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\wing_dungeon_collapse_maps.tsv'),
    $wingMapRows)

$wingExclamationGraphic = $interactionGraphics['159:0']
$wingExclamationAnimation = Resolve-NpcAnimation 0x9f 0
if ($null -eq $wingExclamationGraphic -or
    -not $gfxNames.ContainsKey($wingExclamationGraphic.Gfx) -or
    -not $wingExclamationAnimation) {
    throw 'Could not resolve INTERAC_EXCLAMATION_MARK graphics for room 0:83.'
}
$wingEventRows = @(
    "# group`troom`tid`tsubid`ty`tx`trock-position`trock-tile`tground-tile`tdug-tile`troom-flag`tlinked-room`tlinked-room-flag`tpickup-wait`texclamation-frames`tpre-collapse-shake`tcollapse-initial-wait`tphase-wait`tfinal-wait`tcollapse-shake`tdust-y`tdust-x`tdust-frames`tdust-interval`texclamation-id`texclamation-subid`texclamation-sprite`texclamation-tile-base`texclamation-palette`texclamation-animation`tfacade-position`tfacade-width`tfacade-height`tfinal-tiles`tfinal-collisions`tsource",
    (@(
        '0', '83', 'dc', '02', '48', '38', '43', 'c3', '3a', '1c',
        '80', '73', '80', '30', '60', '40', '60', '30', '60', '15',
        '2c', '58', '106', '3', '9f', '00',
        $gfxNames[$wingExclamationGraphic.Gfx],
        $wingExclamationGraphic.TileBase.ToString(),
        $wingExclamationGraphic.Palette.ToString(),
        $wingExclamationAnimation,
        '04', '3', '3',
        '3b,3b,3b,3b,3b,3b,00,00,00',
        '00,00,00,00,00,00,05,0f,0a',
        'miscellaneous2.s:interactiondc_subid02;miscCutscenes.s:CUTSCENE_D2_COLLAPSE;roomGfxChanges.s:drawCollapsedWingDungeon'
    ) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\wing_dungeon_collapse_event.tsv'),
    $wingEventRows)

# Present-day INTERAC_REMOTE_MAKU_CUTSCENE $8a:$00 preserves sprite palette 0
# while the background fades to black, runs the $62 confetti emitter, reports
# the next objective through TX_05b0-$05bb (TX_05c0-$05cb in a linked game),
# then updates the Maku map/state bytes. Import the supported room 0:8d first-
# Essence, room 0:83 Wing Dungeon, and room 0:3a post-Harp lanes from the
# shared script while retaining each native var03 predicate.
$remoteMakuScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$remoteMakuScriptSource = Read-ImportText $remoteMakuScriptPath
$remoteMakuHelperPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$remoteMakuHelperSource = Read-ImportText $remoteMakuHelperPath
$remoteMakuInteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\remoteMakuCutscene.s')
$makuConfettiSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\makuConfetti.s')
$sparkleSourceForRemoteMaku = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\sparkle.s')
$remoteMakuObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\mainData.s')

if ($remoteMakuObjectSource -notmatch '(?ms)^group0Map8dObjectData:\s+obj_Interaction \$8a \$00 \$00 \$00 \$00\s+obj_End' -or
    $remoteMakuObjectSource -notmatch '(?ms)^group0Map3aObjectData:.*?obj_Interaction \$8a \$00 \$00 \$00 \$02.*?obj_End' -or
    $remoteMakuInteractionSource -notmatch '(?ms)^@state0:.*?returnIfScrollMode01Unset.*?^@checkConditionsAndSetText:.*?^@val00:\s+xor a\s+call @checkEssenceObtained\s+jp z,@deleteSelfAndReturn\s+ldbc \$00, <TX_05b0.*?^@checkEssenceObtained:\s+ld hl,wEssencesObtained\s+jp checkFlag' -or
    $remoteMakuInteractionSource -notmatch '(?ms)^@val02:\s+ld a,TREASURE_HARP\s+call checkTreasureObtained\s+jp nc,@deleteSelfAndReturn\s+ldbc \$00, <TX_05b2\s+jp @setTextForScript' -or
    $remoteMakuInteractionSource -notmatch '(?ms)^@state0:.*?getThisRoomFlags\s+and \$40\s+jp nz,interactionDelete.*?^@scriptTable:\s+\.dw mainScripts\.remoteMakuCutsceneScript' -or
    $remoteMakuHelperSource -notmatch '(?ms)^remoteMakuCutscene_fadeoutToBlackWithDelay:.*?fadeoutToBlackWithDelay.*?ld a,\$ff\s+ld \(wDirtyFadeBgPalettes\),a\s+ld \(wFadeBgPaletteSources\),a\s+ld a,\$01\s+ld \(wDirtyFadeSprPalettes\),a\s+ld a,\$fe\s+ld \(wFadeSprPaletteSources\),a' -or
    $remoteMakuHelperSource -notmatch '(?ms)^makuTree_modifyTextIndexForLinked:.*?checkIsLinkedGame.*?^@getLinkedTextOffset:.*?INTERAC_REMOTE_MAKU_CUTSCENE.*?dec a.*?INTERAC_MAKU_TREE.*?^makuTree_textOffsetsForLinked:\s+\.db \$20, \$20, \$10') {
    throw 'Remote-Maku placement, predicate, palette mask, or linked-text offset changed.'
}

$remoteMakuSupportedOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'disableinput', 'writememory', 'setmusic', 'wait', 'asm15',
    'checkpalettefadedone', 'jumpifobjectbyteeq', 'spawninteraction',
    'scriptjump', 'resetmusic', 'orroomflag', 'enableinput', 'scriptend')) {
    [void]$remoteMakuSupportedOpcodes.Add($opcode)
}
$remoteMakuParsed = @(Read-AssemblyCutsceneCommands `
    $remoteMakuScriptPath 'remoteMakuCutsceneScript' $remoteMakuSupportedOpcodes)
$remoteMakuExpected = @(
    @('disableinput', ''),
    @('writememory', 'wTextboxFlags, TEXTBOXFLAG_ALTPALETTE1'),
    @('setmusic', 'MUS_MAKU_TREE'),
    @('wait', '40'),
    @('writememory', 'wDontUpdateStatusBar, $77'),
    @('asm15', 'hideStatusBar'),
    @('asm15', 'scriptHelp.remoteMakuCutscene_fadeoutToBlackWithDelay, $02'),
    @('checkpalettefadedone', ''),
    @('jumpifobjectbyteeq', 'Interaction.subid, $01, @past'),
    @('spawninteraction', 'INTERAC_MAKU_CONFETTI, $00, $00, $00'),
    @('wait', '240'),
    @('wait', '180'),
    @('scriptjump', '++'),
    @('spawninteraction', 'INTERAC_MAKU_CONFETTI, $01, $00, $00'),
    @('wait', '240'),
    @('wait', '60'),
    @('asm15', 'scriptHelp.makuTree_showTextWithOffsetAndUpdateMapText, $00'),
    @('wait', '1'),
    @('asm15', 'showStatusBar'),
    @('asm15', 'clearFadingPalettes'),
    @('asm15', 'scriptHelp.remoteMakuCutscene_checkinitUnderwaterWaves'),
    @('asm15', 'fadeinFromWhiteWithDelay, $02'),
    @('checkpalettefadedone', ''),
    @('resetmusic', ''),
    @('orroomflag', '$40'),
    @('asm15', 'incMakuTreeState'),
    @('jumpifobjectbyteeq', 'Interaction.var03, $07, @spawnGoronAfterCrownDungeon'),
    @('enableinput', ''),
    @('scriptend', ''),
    @('spawninteraction', 'INTERAC_GORON, $03, $58, $a8'),
    @('scriptend', '')
)
if ($remoteMakuParsed.Count -ne $remoteMakuExpected.Count) {
    throw "remoteMakuCutsceneScript expected 31 commands, parsed $($remoteMakuParsed.Count)."
}
for ($index = 0; $index -lt $remoteMakuExpected.Count; $index++) {
    $actualOperands = if ($null -eq $remoteMakuParsed[$index].Operands) {
        ''
    } else {
        ([string]$remoteMakuParsed[$index].Operands).Trim()
    }
    if ($remoteMakuParsed[$index].Opcode -ne $remoteMakuExpected[$index][0] -or
        $actualOperands -ne $remoteMakuExpected[$index][1]) {
        throw "remoteMakuCutsceneScript command $index changed from $($remoteMakuExpected[$index] -join ' ')."
    }
}

$confettiData = [regex]::Match(
    $makuConfettiSource,
    '(?ms)^@initialPositionsAndAccelerations:\s*(?<positions>(?:\s*dbbww[^\r\n]+\r?\n){5}).*?^@spawnDelayValues:\s+\.db \$01 \$32 \$14 \$1e \$28 \$1e.*?^@yOffset:\s+\.dw \$00c0')
$confettiPositions = @([regex]::Matches(
    $confettiData.Groups['positions'].Value,
    'dbbww \$(?<y>[0-9a-f]{2}), \$(?<x>[0-9a-f]{2}), \$(?<ay>[0-9a-f]{4}), \$(?<ax>[0-9a-f]{4})'))
if (-not $confettiData.Success -or $confettiPositions.Count -ne 5 -or
    $makuConfettiSource -notmatch '(?ms)^@state1:.*?Interaction\.counter2.*?180.*?SND_MAGIC_POWDER.*?cp \$05.*?interactionDelete' -or
    $makuConfettiSource -notmatch '(?ms)^@state2:.*?Interaction\.var3a.*?\$18.*?@makeSparkle.*?Interaction\.yh.*?cp \$88.*?cp \$d8.*?speedY > \$100.*?speedX > \$200.*?interactionSetAnimation' -or
    $makuConfettiSource -notmatch '(?ms)^@makeSparkle:.*?INTERAC_SPARKLE.*?\$02.*?objectCopyPosition' -or
    $sparkleSourceForRemoteMaku -notmatch '(?ms)^@initSubid02:.*?objectSetVisible82.*?^@runSubid02:.*?objectApplyComponentSpeed.*?Interaction\.animParameter.*?cp \$ff.*?interactionDelete.*?interactionAnimate') {
    throw 'Present Maku confetti positions, counters, movement, sparkle, or deletion rules changed.'
}

$confettiGraphic = $interactionGraphics['98:0']
$confettiAnimations = @(0..1 | ForEach-Object { Resolve-NpcAnimation 0x62 $_ })
$remoteMakuSparkleGraphic = $interactionGraphics['132:2']
$remoteMakuSparkleAnimation = Resolve-NpcAnimation 0x84 $remoteMakuSparkleGraphic.DefaultAnimation
if ($null -eq $confettiGraphic -or $confettiGraphic.Gfx -ne 0x6c -or
    $confettiGraphic.TileBase -ne 4 -or $confettiGraphic.Palette -ne 2 -or
    ($confettiAnimations | Where-Object { -not $_ }).Count -ne 0 -or
    $null -eq $remoteMakuSparkleGraphic -or
    $remoteMakuSparkleGraphic.Gfx -ne 0x6b -or
    $remoteMakuSparkleGraphic.TileBase -ne 0x0a -or
    $remoteMakuSparkleGraphic.Palette -ne 0 -or
    $remoteMakuSparkleGraphic.DefaultAnimation -ne 1 -or
    -not $remoteMakuSparkleAnimation) {
    throw 'INTERAC_MAKU_CONFETTI or its $84:$02 sparkle graphics changed.'
}
if (-not $allTexts.ContainsKey(0x05b0) -or
    -not $allTexts.ContainsKey(0x05c0) -or
    -not $allTexts.ContainsKey(0x05b1) -or
    -not $allTexts.ContainsKey(0x05c1) -or
    -not $allTexts.ContainsKey(0x05b2) -or
    -not $allTexts.ContainsKey(0x05c2)) {
    throw 'Remote Maku text TX_05b0-TX_05b2/TX_05c0-TX_05c2 was not imported.'
}

$positionPayload = @($confettiPositions | ForEach-Object {
    $y = [Convert]::ToInt32($_.Groups['y'].Value, 16)
    if ($y -ge 0x80) { $y -= 0x100 }
    $x = [Convert]::ToInt32($_.Groups['x'].Value, 16)
    $ay = [Convert]::ToInt32($_.Groups['ay'].Value, 16)
    $ax = [Convert]::ToInt32($_.Groups['ax'].Value, 16)
    "$y`:$x`:$ay`:$ax"
}) -join ','
$remoteMakuEventRows = @(
    "# group`troom`tid`tsubid`tvar03`tessence-mask`trequired-treasure`troom-flag`tstandard-text-id`tlinked-text-id`tstandard-map-text`tlinked-map-text`tmusic`thud-lock-byte`tfade-delay`tfade-frames`tinitial-wait`tconfetti-hold1`tconfetti-hold2`tpost-text-wait`tconfetti-pieces`tspawn-delays`tpositions-and-accelerations`ty-offset-fixed`tsparkle-initial-delay`tsparkle-repeat-delay`tsound-counter`tsound`ty-speed-limit`tx-speed-limit`tdelete-y"
    "0`t8d`t8a`t00`t00`t01`tff`t40`t05b0`t05c0`tb0`tc0`t1e`t77`t2`t65`t40`t240`t180`t1`t5`t1,50,20,30,40,30`t$positionPayload`t192`t16`t24`t180`t83`t256`t512`t136"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_first_essence_event.tsv'),
    $remoteMakuEventRows)
$remoteMakuWingEventRows = @(
    $remoteMakuEventRows[0]
    "0`t83`t8a`t00`t01`t00`tff`t40`t05b1`t05c1`tb1`tc1`t1e`t77`t2`t65`t40`t240`t180`t1`t5`t1,50,20,30,40,30`t$positionPayload`t192`t16`t24`t180`t83`t256`t512`t136"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_wing_dungeon_event.tsv'),
    $remoteMakuWingEventRows)
$remoteMakuHarpEventRows = @(
    $remoteMakuEventRows[0]
    "0`t3a`t8a`t00`t02`t00`t$($treasureIds['TREASURE_HARP'].ToString('x2'))`t40`t05b2`t05c2`tb2`tc2`t1e`t77`t2`t65`t40`t240`t180`t1`t5`t1,50,20,30,40,30`t$positionPayload`t192`t16`t24`t180`t83`t256`t512`t136"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_harp_event.tsv'),
    $remoteMakuHarpEventRows)

$remoteMakuVisualRows = @(
    "# key`tsprite`ttile-base`tpalette`tanimation"
    "confetti-left`t$($gfxNames[$confettiGraphic.Gfx])`t$($confettiGraphic.TileBase)`t$($confettiGraphic.Palette)`t$($confettiAnimations[0])"
    "confetti-right`t$($gfxNames[$confettiGraphic.Gfx])`t$($confettiGraphic.TileBase)`t$($confettiGraphic.Palette)`t$($confettiAnimations[1])"
    "sparkle`t$($gfxNames[$remoteMakuSparkleGraphic.Gfx])`t$($remoteMakuSparkleGraphic.TileBase)`t$($remoteMakuSparkleGraphic.Palette)`t$remoteMakuSparkleAnimation"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_first_essence_visuals.tsv'),
    $remoteMakuVisualRows)
Copy-GeneratedFile `
    "gfx_compressible\ages\$($gfxNames[$confettiGraphic.Gfx]).png" `
    "gfx\$($gfxNames[$confettiGraphic.Gfx]).png"
Copy-GeneratedFile `
    "gfx_compressible\ages\$($gfxNames[$remoteMakuSparkleGraphic.Gfx]).png" `
    "gfx\$($gfxNames[$remoteMakuSparkleGraphic.Gfx]).png"

$remoteMakuCommandRows = [Collections.Generic.List[string]]::new()
$remoteMakuCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
$remoteMakuCommandSpecs = @(
    @($remoteMakuParsed[0],  'disableinput', '', '', '', ''),
    @($remoteMakuParsed[1],  'writememory', '', '04', '', 'TextboxFlags'),
    @($remoteMakuParsed[2],  'setmusic', '', '1e', '', ''),
    @($remoteMakuParsed[3],  'wait', '', '40', '', ''),
    @($remoteMakuParsed[4],  'writememory', '', '77', '', 'DontUpdateStatusBar'),
    @($remoteMakuParsed[5],  'native', '', '', '', 'HideHud'),
    @($remoteMakuParsed[6],  'nativeblock', '', '65', '', "FadeOutBlack`0"),
    @($remoteMakuParsed[9],  'native', '', '', '', 'SpawnPresentConfetti'),
    @($remoteMakuParsed[10], 'wait', '', '240', '', ''),
    @($remoteMakuParsed[11], 'wait', '', '180', '', ''),
    @($remoteMakuParsed[16], 'showtextdifferentforlinked', '', '05b0', '05c0',
        "$($allTexts[0x05b0])`0$($allTexts[0x05c0])"),
    @($remoteMakuParsed[17], 'wait', '', '1', '', ''),
    @($remoteMakuParsed[18], 'native', '', '', '', 'ShowHud'),
    @($remoteMakuParsed[19], 'native', '', '', '', 'ClearFadingPalettes'),
    @($remoteMakuParsed[21], 'nativeblock', '', '65', '', "FadeInWhite`0"),
    @($remoteMakuParsed[23], 'native', '', '', '', 'ResetMusic'),
    @($remoteMakuParsed[24], 'orroomflag', '', '40', '', ''),
    @($remoteMakuParsed[25], 'native', '', '', '', 'IncMakuTreeState'),
    @($remoteMakuParsed[27], 'enableinput', '', '', '', ''),
    @($remoteMakuParsed[28], 'scriptend', '', '', '', '')
)
for ($index = 0; $index -lt $remoteMakuCommandSpecs.Count; $index++) {
    $spec = $remoteMakuCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $remoteMakuCommandRows.Add((New-CutsceneCommandRow `
        'remoteMakuCutsceneScript' $index $sourceCommand.Label `
        $sourceCommand.Line $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_first_essence_commands.tsv'),
    $remoteMakuCommandRows)

$remoteMakuWingCommandRows = [Collections.Generic.List[string]]::new()
$remoteMakuWingCommandRows.Add($remoteMakuCommandRows[0])
for ($index = 0; $index -lt $remoteMakuCommandSpecs.Count; $index++) {
    $spec = $remoteMakuCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $opcode = $spec[1]
    $actor = $spec[2]
    $arg0 = $spec[3]
    $arg1 = $spec[4]
    $payload = $spec[5]
    if ($index -eq 10) {
        $arg0 = '05b1'
        $arg1 = '05c1'
        $payload = "$($allTexts[0x05b1])`0$($allTexts[0x05c1])"
    }
    $remoteMakuWingCommandRows.Add((New-CutsceneCommandRow `
        'remoteMakuCutsceneScript' $index $sourceCommand.Label `
        $sourceCommand.Line $opcode $actor $arg0 $arg1 $payload))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_wing_dungeon_commands.tsv'),
    $remoteMakuWingCommandRows)
$remoteMakuHarpCommandRows = [Collections.Generic.List[string]]::new()
$remoteMakuHarpCommandRows.Add($remoteMakuCommandRows[0])
for ($index = 0; $index -lt $remoteMakuCommandSpecs.Count; $index++) {
    $spec = $remoteMakuCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $opcode = $spec[1]
    $actor = $spec[2]
    $arg0 = $spec[3]
    $arg1 = $spec[4]
    $payload = $spec[5]
    if ($index -eq 10) {
        $arg0 = '05b2'
        $arg1 = '05c2'
        $payload = "$($allTexts[0x05b2])`0$($allTexts[0x05c2])"
    }
    $remoteMakuHarpCommandRows.Add((New-CutsceneCommandRow `
        'remoteMakuCutsceneScript' $index $sourceCommand.Label `
        $sourceCommand.Line $opcode $actor $arg0 $arg1 $payload))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\remote_maku_harp_commands.tsv'),
    $remoteMakuHarpCommandRows)

# Room 3:ae contains INTERAC_HARP_OF_AGES_SPAWNER $b3:$00. It creates the
# static Harp treasure and its attached $84:$0c sparkle, then hands the
# post-pickup sequence to the native $36:$07 Nayru wrapper and
# mainScripts.nayruScript07. Export the wrapper constants and a typed expansion
# of that script; INTERAC_PLAY_HARP_SONG remains one bounded native command
# because it drives SPECIALOBJECT_LINK's exact animation state.
$harpSpawnerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\harpOfAgesSpawner.s')
$harpNayruSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\nayru.s')
$harpSparkleSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\sparkle.s')
$harpSongSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\playHarpSong.s')
$harpScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$harpScriptSource = Read-ImportText $harpScriptPath
$harpMusicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')

if ($mainObjectSource -notmatch
        '(?ms)^group3MapaeObjectData:\s+obj_Interaction \$b3 \$00 \$28 \$58\s+obj_End' -or
    $harpSpawnerSource -notmatch
        '(?ms)^@state0:.*?ROOMFLAG_BIT_ITEM.*?INTERAC_TREASURE.*?TREASURE_HARP.*?ld \(hl\),\$38.*?ld \(hl\),\$58.*?INTERAC_SPARKLE.*?ld \(hl\),\$0c.*?^@state1:.*?SNDCTRL_STOPMUSIC.*?DISABLE_ALL_BUT_INTERACTIONS.*?^@state2:.*?wTextIsActive.*?w1Link\.direction.*?^@state3:.*?set 0,\(hl\).*?ld \(hl\),40.*?fadeoutToBlackWithDelay.*?wDirtyFadeBgPalettes.*?wDirtyFadeSprPalettes.*?hideStatusBar.*?^@state4:.*?wPaletteThread_mode.*?interactionDecCounter1.*?INTERAC_NAYRU.*?ld \(hl\),\$07.*?objectCopyPosition' -or
    $harpNayruSource -notmatch
        '(?ms)^@init07:.*?ld a,\$1e.*?interactionLoadExtraGraphics.*?interactionSetAlwaysUpdateBit.*?^nayruSubid07:.*?interactionDecCounter1.*?xor \$80.*?MUS_NAYRU.*?mainScripts\.nayruScript07.*?rrca.*?cp \$07.*?createMusicNotes.*?wActiveMusic2.*?fadeinFromWhiteWithDelay.*?showStatusBar' -or
    $harpSparkleSource -notmatch
        '(?ms)^@initSubid0c:.*?relatedObj1.*?Interaction\.var38' -or
    $harpSparkleSource -notmatch
        '(?ms)^@runSubid0c:.*?Interaction\.var38.*?interactionDelete.*?objectTakePosition.*?cfc0.*?bit 0,a.*?animateAndFlicker' -or
    $harpSongSource -notmatch
        '(?ms)^@state0:.*?setLinkForceStateToState08.*?ld a,\$04.*?^@state1:.*?interactionDecCounter1.*?ld \(hl\),52.*?LINK_ANIM_MODE_HARP_2.*?^@sounds:\s+\.db SND_TUNE_OF_ECHOES.*?^@state2:.*?^@state4:.*?wFrameCounter.*?and \$1f.*?ld bc,\$f8f8.*?objectCreateFloatingMusicNote.*?^@state3:.*?^@state5:.*?ld bc,\$f808.*?^@state6:.*?set 7,\(hl\).*?LINK_ANIM_MODE_WALK') {
    throw 'Room 3:ae Harp spawner, sparkle, Nayru wrapper, or response-song state machine changed.'
}

$harpOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'wait', 'writememory', 'showtext', 'setanimation',
    'writeobjectbyte', 'asm15', 'xorcfc0bit', 'spawninteraction',
    'checkcfc0bit', 'giveitem', 'scriptend')) {
    [void]$harpOpcodes.Add($opcode)
}
$harpParsed = @(Read-AssemblyCutsceneCommands `
    $harpScriptPath 'nayruScript07' $harpOpcodes)
$harpExpected = @(
    @('wait', '12'),
    @('writememory', 'wTextboxFlags, TEXTBOXFLAG_ALTPALETTE1'),
    @('showtext', 'TX_1d10'),
    @('wait', '16'),
    @('setanimation', '$07'),
    @('writeobjectbyte', 'Interaction.direction, $07'),
    @('asm15', 'playSound, SND_TUNE_OF_ECHOES'),
    @('wait', '210'),
    @('xorcfc0bit', '0'),
    @('wait', '75'),
    @('xorcfc0bit', '0'),
    @('setanimation', '$02'),
    @('writeobjectbyte', 'Interaction.direction, $02'),
    @('wait', '16'),
    @('writememory', 'wTextboxFlags, TEXTBOXFLAG_ALTPALETTE1'),
    @('showtext', 'TX_1d11'),
    @('spawninteraction', 'INTERAC_PLAY_HARP_SONG, $00, $00, $00'),
    @('checkcfc0bit', '7'),
    @('wait', '36'),
    @('writememory', 'wTextboxFlags, TEXTBOXFLAG_ALTPALETTE1'),
    @('giveitem', 'TREASURE_TUNE_OF_ECHOES, $00'),
    @('wait', '16'),
    @('scriptend', '')
)
if ($harpParsed.Count -ne $harpExpected.Count) {
    throw "nayruScript07 expected 23 commands, parsed $($harpParsed.Count)."
}
for ($index = 0; $index -lt $harpExpected.Count; $index++) {
    $actualOperands = if ($null -eq $harpParsed[$index].Operands) {
        ''
    } else {
        ([string]$harpParsed[$index].Operands).Trim()
    }
    if ($harpParsed[$index].Opcode -ne $harpExpected[$index][0] -or
        $actualOperands -ne $harpExpected[$index][1]) {
        throw "nayruScript07 command $index changed from $($harpExpected[$index] -join ' ')."
    }
}

$harpTreasure = $treasureObjectRecords['TREASURE_OBJECT_HARP_00']
$echoTreasure = $treasureObjectRecords['TREASURE_OBJECT_TUNE_OF_ECHOES_00']
$harpNayruGraphic = $interactionGraphics['54:0']
$harpSparkleGraphic = $interactionGraphics['132:12']
$harpNayruExtraSprite = $gfxNames[$harpNayruGraphic.Gfx + 1]
$harpNayruIdleAnimation = Resolve-NpcAnimation 0x36 0x02
$harpNayruSingingAnimation = Resolve-NpcAnimation 0x36 0x07
$harpSparkleAnimation = Resolve-NpcAnimation 0x84 0x00
$harpSparkleGfxHeader = [regex]::Match(
    $objectGfxSource,
    '(?m)^\s*/\* \$3a \*/ m_ObjectGfxHeader spr_link, \$(?<continue>[0-9a-f]{2}), \$(?<source>[0-9a-f]{4})')
$harpSparkleSourceOffset = if ($harpSparkleGfxHeader.Success) {
    [Convert]::ToInt32(
        $harpSparkleGfxHeader.Groups['source'].Value, 16)
} else {
    -1
}
if ($null -eq $harpTreasure -or $harpTreasure.Treasure -ne 0x11 -or
    $harpTreasure.Subid -ne 0 -or $harpTreasure.Parameter -ne 0 -or
    $harpTreasure.TextId -ne 0x71 -or $harpTreasure.Graphic -ne 0x68 -or
    $null -eq $echoTreasure -or $echoTreasure.Treasure -ne 0x25 -or
    $echoTreasure.Subid -ne 0 -or $echoTreasure.Parameter -ne 0 -or
    $echoTreasure.TextId -ne 0x72 -or $echoTreasure.Graphic -ne 0x69 -or
    $null -eq $harpNayruGraphic -or $harpNayruGraphic.Gfx -ne 0x26 -or
    $harpNayruGraphic.TileBase -ne 0 -or $harpNayruGraphic.Palette -ne 1 -or
    $harpNayruGraphic.DefaultAnimation -ne 2 -or
    $harpNayruExtraSprite -ne 'spr_nayru_2' -or
    $objectGfxSource -notmatch
        '/\* \$27 \*/ m_ObjectGfxHeader spr_nayru_2, 1' -or
    [string]::IsNullOrWhiteSpace($harpNayruIdleAnimation) -or
    [string]::IsNullOrWhiteSpace($harpNayruSingingAnimation) -or
    $null -eq $harpSparkleGraphic -or $harpSparkleGraphic.Gfx -ne 0x3a -or
    $harpSparkleGraphic.TileBase -ne 0 -or $harpSparkleGraphic.Palette -ne 0 -or
    $harpSparkleGraphic.DefaultAnimation -ne 0 -or
    -not $harpSparkleGfxHeader.Success -or
    $harpSparkleGfxHeader.Groups['continue'].Value -ne '00' -or
    $harpSparkleSourceOffset -ne 0x1c00 -or
    [string]::IsNullOrWhiteSpace($harpSparkleAnimation) -or
    -not $allTexts.ContainsKey(0x1d10) -or
    -not $allTexts.ContainsKey(0x1d11) -or
    $harpMusicSource -notmatch '(?m)^\s*SND_TUNE_OF_ECHOES\s+db ; \$ad') {
    throw 'Room 3:ae Harp/Tune treasures, Nayru/sparkle visuals, text, or music changed.'
}

$harpEventRows = @(
    "# group`troom`tspawner-id`tspawner-subid`tspawner-y`tspawner-x`tharp-y`tharp-x`troom-flag`tharp-treasure`tharp-object`tsparkle-id`tsparkle-subid`tfade-delay`tfade-frames`tblack-hold`tnayru-id`tnayru-subid`tnayru-flicker`tnayru-music`ttextbox-flags`tsong-sound`tsong-initial-delay`tsong-phase-frames`tsong-phases`tsong-native-frames`tfinal-fade-delay`tfinal-fade-frames`techoes-treasure`techoes-object",
    "3`tae`tb3`t00`t28`t58`t38`t58`t20`t11`tTREASURE_OBJECT_HARP_00`t84`t0c`t2`t65`t40`t36`t07`t30`t08`t04`tad`t4`t52`t4`t214`t4`t129`t25`tTREASURE_OBJECT_TUNE_OF_ECHOES_00"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\harp_of_ages_event.tsv'),
    $harpEventRows)

$harpVisualRows = @(
    "# key`tid`tsubid`tsprite`textra-sprite`ttile-base`tpalette`tsource-offset`tanimation-0`tanimation-2`tanimation-7",
    "Nayru`t36`t07`t$($gfxNames[$harpNayruGraphic.Gfx])`t$harpNayruExtraSprite`t$($harpNayruGraphic.TileBase)`t$($harpNayruGraphic.Palette)`t0000`t`t$harpNayruIdleAnimation`t$harpNayruSingingAnimation",
    "Sparkle`t84`t0c`t$($gfxNames[$harpSparkleGraphic.Gfx])`t`t$($harpSparkleGraphic.TileBase)`t$($harpSparkleGraphic.Palette)`t$($harpSparkleSourceOffset.ToString('x4'))`t$harpSparkleAnimation`t`t"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\harp_of_ages_visuals.tsv'),
    $harpVisualRows)

$harpCommandRows = [Collections.Generic.List[string]]::new()
$harpCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
$harpCommandSpecs = @(
    @($harpParsed[0],  'wait', '', '12', '', ''),
    @($harpParsed[1],  'writememory', '', '04', '', 'TextboxFlags'),
    @($harpParsed[2],  'showtext', '', '1d10', '', $allTexts[0x1d10]),
    @($harpParsed[3],  'wait', '', '16', '', ''),
    @($harpParsed[4],  'setanimation', 'Nayru', '07', '', $harpNayruSingingAnimation),
    @($harpParsed[5],  'writeobjectbyte', 'Nayru', '08', '07', ''),
    @($harpParsed[6],  'playsound', '', 'ad', '', ''),
    @($harpParsed[7],  'wait', '', '210', '', ''),
    @($harpParsed[8],  'native', '', '', '', 'ToggleNayruAnimation'),
    @($harpParsed[9],  'wait', '', '75', '', ''),
    @($harpParsed[10], 'native', '', '', '', 'ToggleNayruAnimation'),
    @($harpParsed[11], 'setanimation', 'Nayru', '02', '', $harpNayruIdleAnimation),
    @($harpParsed[12], 'writeobjectbyte', 'Nayru', '08', '02', ''),
    @($harpParsed[13], 'wait', '', '16', '', ''),
    @($harpParsed[14], 'writememory', '', '04', '', 'TextboxFlags'),
    @($harpParsed[15], 'showtext', '', '1d11', '', $allTexts[0x1d11]),
    @($harpParsed[16], 'nativeblock', '', '214', '', 'PlayHarpSong'),
    @($harpParsed[18], 'wait', '', '36', '', ''),
    @($harpParsed[19], 'writememory', '', '04', '', 'TextboxFlags'),
    @($harpParsed[20], 'giveitem', '', '25', '00', ''),
    @($harpParsed[21], 'wait', '', '16', '', ''),
    @($harpParsed[22], 'scriptend', '', '', '', '')
)
for ($index = 0; $index -lt $harpCommandSpecs.Count; $index++) {
    $spec = $harpCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $harpCommandRows.Add((New-CutsceneCommandRow `
        'nayruScript07' $index $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\harp_of_ages_commands.tsv'),
    $harpCommandRows)

# Room 0:56 comedian trade. INTERAC_COMEDIAN is a script-owned NPC whose
# native wrapper initializes the script twice on its first update, then turns
# horizontally toward Link and animates after every later script update.
$comedianScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$comedianScriptSource = Read-ImportText $comedianScriptPath
$comedianOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'asm15', 'jumpifroomflagset', 'setanimation', 'scriptjump',
    'initcollisions', 'checkabutton', 'disableinput',
    'jumptable_objectbyte', 'showtextlowindex', 'wait',
    'jumpiftradeitemeq', 'jumpiftextoptioneq', 'giveitem', 'enableinput')) {
    [void]$comedianOpcodes.Add($opcode)
}
$comedianCommands = Read-AssemblyCutsceneCommands `
    $comedianScriptPath 'comedianScript' $comedianOpcodes
if ($comedianCommands.Count -ne 34) {
    throw "comedianScript expected 34 commands, parsed $($comedianCommands.Count)."
}

$comedianTargets = @{}
foreach ($command in $comedianCommands) {
    if (-not $comedianTargets.ContainsKey($command.Label)) {
        $comedianTargets[$command.Label] = $command.Index
    }
}
$expectedComedianTargets = @{
    '@hasMustache' = 5
    '@initNpc' = 7
    '@npcLoop' = 8
    '@beforeBeatD2' = 12
    '@afterBeatD2' = 14
    '@afterBeatMoonlitGrotto' = 16
    '@promptForTrade' = 20
    '@noTrade' = 23
    '@acceptedTrade' = 25
    '@alreadyGaveMustache' = 30
    '@enableInput' = 31
}
foreach ($entry in $expectedComedianTargets.GetEnumerator()) {
    if (-not $comedianTargets.ContainsKey($entry.Key) -or
        $comedianTargets[$entry.Key] -ne $entry.Value) {
        throw "comedianScript label $($entry.Key) moved from command $($entry.Value)."
    }
}
$comedianProgressTargets = @(
    Read-AssemblyDataDirectives `
        $comedianScriptPath 'comedianScript' '.dw' |
        ForEach-Object { $_.Operands[0] })
if (($comedianProgressTargets -join ',') -ne
    '@beforeBeatD2,@afterBeatD2,@afterBeatMoonlitGrotto') {
    throw 'comedianScript progress jump table changed.'
}

$comedianNativePath = Join-Path $Disassembly 'object_code\ages\interactions\comedian.s'
$comedianNativeSource = Read-ImportText $comedianNativePath
if ($comedianNativeSource -notmatch
        '(?ms)^@state0:.*?@loadScriptAndInitGraphics.*?interactionRunScript.*?interactionRunScript.*?interactionAnimateAsNpc' -or
    $comedianNativeSource -notmatch
        '(?ms)^@state1:.*?interactionRunScript.*?comedian_turnToFaceLink.*?interactionAnimateAsNpc' -or
    $comedianNativeSource -notmatch
        '(?ms)^@loadScriptAndInitGraphics:.*?interactionInitGraphics.*?objectMarkSolidPosition.*?>TX_0b00.*?mainScripts\.comedianScript') {
    throw 'INTERAC_COMEDIAN native initialization, facing, or update order changed.'
}
if ($comedianScriptSource -notmatch
        '(?ms)^comedian_checkGameProgress:.*?wEssencesObtained.*?getHighestSetBit.*?cp \$03.*?ld a,\$02.*?Interaction\.var3f' -or
    $comedianScriptSource -notmatch
        '(?ms)^comedian_enableMustache:.*?ld a,\$04.*?^comedian_disableMustache:.*?ld a,\$00.*?Interaction\.var37.*?Interaction\.var3e.*?\$ff' -or
    $comedianScriptSource -notmatch
        '(?ms)^comedian_turnToFaceLink:.*?w1Link\.xh.*?cp \(hl\).*?ld a,\$01.*?Interaction\.var37.*?interactionSetAnimation') {
    throw 'Comedian progress, moustache, or horizontal-facing helpers changed.'
}
if ($mainObjectSource -notmatch
    '(?ms)^group0Map56ObjectData:\s+obj_Interaction \$3a \$04 \$48 \$68\s+obj_Interaction \$65 \$00 \$48 \$78\s+obj_End') {
    throw 'Room 0:56 comedian/sidekick object order or coordinates changed.'
}

$comedianAnimations = @{
    0 = Resolve-NpcAnimation 0x65 0
    1 = Resolve-NpcAnimation 0x65 1
    4 = Resolve-NpcAnimation 0x65 4
    5 = Resolve-NpcAnimation 0x65 5
}
foreach ($animation in @(0, 1, 4, 5)) {
    if ([string]::IsNullOrWhiteSpace($comedianAnimations[$animation])) {
        throw "Could not resolve INTERAC_COMEDIAN animation `$$($animation.ToString('x2'))."
    }
}
foreach ($textId in 0x0b2c..0x0b32) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve comedian text TX_$($textId.ToString('x4'))."
    }
}
$comedianTreasure = $treasureObjectRecords['TREASURE_OBJECT_TRADEITEM_07']
if ($null -eq $comedianTreasure -or
    $comedianTreasure.Treasure -ne 0x41 -or
    $comedianTreasure.SubId -ne 0x07 -or
    $comedianTreasure.Parameter -ne 0x07 -or
    $comedianTreasure.TextId -ne 0x0061 -or
    $comedianTreasure.Graphic -ne 0x77) {
    throw 'TREASURE_OBJECT_TRADEITEM_07 no longer grants the Funny Joke.'
}
$roomFlagSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\roomFlags.s')
$tradeItemSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\tradeitems.s')
if ($roomFlagSource -notmatch '\.define ROOMFLAG_ITEM\s+\$20' -or
    $tradeItemSource -notmatch 'TRADEITEM_CHEESY_MUSTACHE\s+db ; \$06' -or
    $tradeItemSource -notmatch 'TRADEITEM_FUNNY_JOKE\s+db ; \$07') {
    throw 'Comedian room flag or trade-item constants changed.'
}

$comedianCommandRows = [Collections.Generic.List[string]]::new()
$comedianCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
foreach ($command in $comedianCommands) {
    $opcode = $command.Opcode
    $actor = ''
    $arg0 = ''
    $arg1 = ''
    $payload = ''
    switch ($command.Opcode) {
        'asm15' {
            switch ($command.Operands) {
                'comedian_checkGameProgress' {
                    $opcode = 'native'
                    $payload = 'comedian_checkGameProgress'
                }
                'comedian_disableMustache' {
                    $opcode = 'native'
                    $payload = 'comedian_disableMustache'
                }
                'comedian_enableMustache' {
                    $opcode = 'native'
                    $payload = 'comedian_enableMustache'
                }
                default {
                    throw "Unsupported comedian asm15 '$($command.Operands)' at source line $($command.Line)."
                }
            }
        }
        'jumpifroomflagset' {
            if ($command.Operands -notmatch
                '^ROOMFLAG_ITEM,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed comedian room-flag branch at source line $($command.Line)."
            }
            $arg0 = '20'
            $arg1 = $comedianTargets[$Matches['target']].ToString()
        }
        'setanimation' {
            if ($command.Operands -notmatch '^\$(?<animation>0[15])$') {
                throw "Unexpected comedian animation '$($command.Operands)'."
            }
            $animation = [Convert]::ToInt32($Matches['animation'], 16)
            $actor = 'Comedian'
            $arg0 = $Matches['animation']
            $payload = $comedianAnimations[$animation]
        }
        'scriptjump' {
            if (-not $comedianTargets.ContainsKey($command.Operands)) {
                throw "Unknown comedian branch target '$($command.Operands)'."
            }
            $arg0 = $comedianTargets[$command.Operands].ToString()
        }
        'initcollisions' { $actor = 'Comedian' }
        'checkabutton' { $actor = 'Comedian' }
        'jumptable_objectbyte' {
            if ($command.Operands -ne 'Interaction.var3f') {
                throw "Unexpected comedian jump-table binding '$($command.Operands)'."
            }
            $opcode = 'jumptablememory'
            $payload = "ComedianProgress|$($comedianTargets['@beforeBeatD2']),$($comedianTargets['@afterBeatD2']),$($comedianTargets['@afterBeatMoonlitGrotto'])"
        }
        'showtextlowindex' {
            if ($command.Operands -notmatch '^<TX_(?<id>0b(?:2[c-f]|3[0-2]))$') {
                throw "Unexpected comedian text '$($command.Operands)'."
            }
            $textId = [Convert]::ToInt32($Matches['id'], 16)
            $opcode = 'showtext'
            $arg0 = $Matches['id']
            $payload = $allTexts[$textId]
        }
        'wait' {
            if ($command.Operands -ne '30') {
                throw "Unexpected comedian wait '$($command.Operands)'."
            }
            $arg0 = '30'
        }
        'jumpiftradeitemeq' {
            if ($command.Operands -notmatch
                '^TRADEITEM_CHEESY_MUSTACHE,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed comedian trade-item branch at source line $($command.Line)."
            }
            $arg0 = '06'
            $arg1 = $comedianTargets[$Matches['target']].ToString()
        }
        'jumpiftextoptioneq' {
            if ($command.Operands -notmatch
                '^\$00,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed comedian choice branch at source line $($command.Line)."
            }
            $arg0 = '00'
            $arg1 = $comedianTargets[$Matches['target']].ToString()
        }
        'giveitem' {
            if ($command.Operands -ne 'TREASURE_TRADEITEM,$07') {
                throw "Unexpected comedian reward '$($command.Operands)'."
            }
            $arg0 = '41'
            $arg1 = '07'
        }
    }
    $comedianCommandRows.Add((New-CutsceneCommandRow `
        $command.Script $command.Index $command.Label $command.Line `
        $opcode $actor "$arg0" "$arg1" $payload))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\comedian_commands.tsv'),
    $comedianCommandRows)

$comedianEventRows = @(
    "# group`troom`tid`tsubid`tanimation0`tanimation1`tanimation4`tanimation5`tcollision-y`tcollision-x`troom-flag`tprogress-binding`trequired-trade`treward-treasure`treward-parameter`treward-object`tinitial-script-updates"
    (@(
        '0', '56', '65', '00',
        $comedianAnimations[0], $comedianAnimations[1],
        $comedianAnimations[4], $comedianAnimations[5],
        '06', '06', '20', 'ComedianProgress', '06', '41', '07',
        'TREASURE_OBJECT_TRADEITEM_07', '2'
    ) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\comedian_event.tsv'),
    $comedianEventRows)

# The remaining InteractionController-owned interactionRunScript loops.
# These command streams retain their source command boundaries while their
# native presentation, secret generation, and giveitem handoffs stay in
# dedicated runtime hosts.

# linkedGameNpcScript is shared by the linked Ghini and Great Fairy. Their
# visibility/spawn predicates are already imported with the NPC records, so
# this stream begins at initcollisions and retains the complete talk loop.
$linkedNpcScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$linkedNpcOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'asm15', 'jumpifmemoryset', 'initcollisions', 'checkabutton',
    'disableinput', 'showloadedtext', 'wait', 'jumpiftextoptioneq',
    'addobjectbyte', 'enableinput', 'scriptjump')) {
    [void]$linkedNpcOpcodes.Add($opcode)
}
$linkedNpcCommands = @(Read-AssemblyCutsceneCommands `
    $linkedNpcScriptPath 'linkedGameNpcScript' $linkedNpcOpcodes `
    'plenSubid0Script')
$linkedNpcExpected = @(
    @('asm15', 'scriptHelp.linkedNpc_checkShouldSpawn'),
    @('jumpifmemoryset', 'wcddb, $80, stubScript'),
    @('initcollisions', ''),
    @('asm15', 'scriptHelp.linkedNpc_initHighTextIndex'),
    @('asm15', 'scriptHelp.linkedNpc_calcLowTextIndex, $00'),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('showloadedtext', ''),
    @('wait', '20'),
    @('jumpiftextoptioneq', '$00, @answeredYes'),
    @('addobjectbyte', 'Interaction.textID, $01'),
    @('showloadedtext', ''),
    @('enableinput', ''),
    @('scriptjump', '@offerSecret'),
    @('asm15', 'scriptHelp.linkedNpc_checkHasExtraTextBox'),
    @('jumpifmemoryset', 'wcddb, $80, @generateSecret'),
    @('asm15', 'scriptHelp.linkedNpc_calcLowTextIndex, $02'),
    @('showloadedtext', ''),
    @('wait', '20'),
    @('jumpiftextoptioneq', '$01, @showExtraText'),
    @('asm15', 'scriptHelp.linkedNpc_generateSecret'),
    @('asm15', 'scriptHelp.linkedNpc_calcLowTextIndex, $03'),
    @('showloadedtext', ''),
    @('wait', '20'),
    @('jumpiftextoptioneq', '$01, @tellSecret'),
    @('asm15', 'scriptHelp.linkedNpc_calcLowTextIndex, $04'),
    @('showloadedtext', ''),
    @('enableinput', ''),
    @('asm15', 'scriptHelp.linkedNpc_checkHasExtraTextBox'),
    @('jumpifmemoryset', 'wcddb, $80, @offerSecret'),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('scriptjump', '@answeredYes')
)
if ($linkedNpcCommands.Count -ne $linkedNpcExpected.Count) {
    throw "linkedGameNpcScript expected 33 commands, parsed $($linkedNpcCommands.Count)."
}
for ($index = 0; $index -lt $linkedNpcExpected.Count; $index++) {
    $operands = ([string]$linkedNpcCommands[$index].Operands).Trim()
    if ($linkedNpcCommands[$index].Opcode -ne $linkedNpcExpected[$index][0] -or
        $operands -ne $linkedNpcExpected[$index][1]) {
        throw "linkedGameNpcScript command $index changed from " +
            "$($linkedNpcExpected[$index] -join ' ')."
    }
}

$linkedNpcCommandSpecs = @(
    @($linkedNpcCommands[2],  'initcollisions', 'LinkedNpc', '', '', ''),
    @($linkedNpcCommands[3],  'native', '', '', '', 'linkedNpc_initHighTextIndex'),
    @($linkedNpcCommands[4],  'native', '', '', '', 'linkedNpc_selectOffer'),
    @($linkedNpcCommands[5],  'checkabutton', 'LinkedNpc', '', '', ''),
    @($linkedNpcCommands[6],  'disableinput', '', '', '', ''),
    @($linkedNpcCommands[7],  'showloadedtext', '', '', '', ''),
    @($linkedNpcCommands[8],  'wait', '', '20', '', ''),
    @($linkedNpcCommands[9],  'jumpiftextoptioneq', '', '00', '12', ''),
    @($linkedNpcCommands[10], 'native', '', '', '', 'linkedNpc_selectRefusal'),
    @($linkedNpcCommands[11], 'showloadedtext', '', '', '', ''),
    @($linkedNpcCommands[12], 'enableinput', '', '', '', ''),
    @($linkedNpcCommands[13], 'scriptjump', '', '2', '', ''),
    @($linkedNpcCommands[14], 'native', '', '', '', 'linkedNpc_checkHasExtraTextBox'),
    @($linkedNpcCommands[15], 'jumpifmemoryeq', '', '00', '18', 'LinkedNpcHasExtraText'),
    @($linkedNpcCommands[16], 'native', '', '', '', 'linkedNpc_selectExplanation'),
    @($linkedNpcCommands[17], 'showloadedtext', '', '', '', ''),
    @($linkedNpcCommands[18], 'wait', '', '20', '', ''),
    @($linkedNpcCommands[19], 'jumpiftextoptioneq', '', '01', '14', ''),
    @($linkedNpcCommands[20], 'native', '', '', '', 'linkedNpc_generateSecret'),
    @($linkedNpcCommands[21], 'native', '', '', '', 'linkedNpc_selectSecret'),
    @($linkedNpcCommands[22], 'showloadedtext', '', '', '', ''),
    @($linkedNpcCommands[23], 'wait', '', '20', '', ''),
    @($linkedNpcCommands[24], 'jumpiftextoptioneq', '', '01', '20', ''),
    @($linkedNpcCommands[25], 'native', '', '', '', 'linkedNpc_selectFinal'),
    @($linkedNpcCommands[26], 'showloadedtext', '', '', '', ''),
    @($linkedNpcCommands[27], 'enableinput', '', '', '', ''),
    @($linkedNpcCommands[28], 'native', '', '', '', 'linkedNpc_checkHasExtraTextBox'),
    @($linkedNpcCommands[29], 'jumpifmemoryeq', '', '00', '2', 'LinkedNpcHasExtraText'),
    @($linkedNpcCommands[30], 'checkabutton', 'LinkedNpc', '', '', ''),
    @($linkedNpcCommands[31], 'disableinput', '', '', '', ''),
    @($linkedNpcCommands[32], 'scriptjump', '', '12', '', '')
)
$linkedNpcCommandRows = [Collections.Generic.List[string]]::new()
$linkedNpcCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
for ($index = 0; $index -lt $linkedNpcCommandSpecs.Count; $index++) {
    $spec = $linkedNpcCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $linkedNpcCommandRows.Add((New-CutsceneCommandRow `
        'linkedGameNpcScript' $index $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\linked_game_npc_commands.tsv'),
    $linkedNpcCommandRows)

# Past Bipin's script lives in scriptHelper.s and uses the ordinary Gasha Seed
# treasure object. Preserve wait 1 followed by checktext as two distinct
# source commands.
$pastBipinScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$pastBipinOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'initcollisions', 'enableinput', 'checkabutton', 'disableinput',
    'jumpifroomflagset', 'showtext', 'giveitem', 'wait', 'checktext',
    'scriptjump')) {
    [void]$pastBipinOpcodes.Add($opcode)
}
$pastBipinCommands = @(Read-AssemblyCutsceneCommands `
    $pastBipinScriptPath 'bipinScript3' $pastBipinOpcodes `
    'setNextChildStage')
$pastBipinExpected = @(
    @('initcollisions', ''),
    @('enableinput', ''),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('jumpifroomflagset', '$20, @alreadyGaveSeed'),
    @('showtext', 'TX_4311'),
    @('giveitem', 'TREASURE_GASHA_SEED, $08'),
    @('wait', '1'),
    @('checktext', ''),
    @('showtext', 'TX_4312'),
    @('scriptjump', '@loop'),
    @('showtext', 'TX_4313'),
    @('scriptjump', '@loop')
)
if ($pastBipinCommands.Count -ne $pastBipinExpected.Count) {
    throw "bipinScript3 expected 13 commands, parsed $($pastBipinCommands.Count)."
}
for ($index = 0; $index -lt $pastBipinExpected.Count; $index++) {
    $operands = ([string]$pastBipinCommands[$index].Operands).Trim()
    if ($pastBipinCommands[$index].Opcode -ne $pastBipinExpected[$index][0] -or
        $operands -ne $pastBipinExpected[$index][1]) {
        throw "bipinScript3 command $index changed from " +
            "$($pastBipinExpected[$index] -join ' ')."
    }
}
foreach ($textId in 0x4311..0x4313) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Bipin text TX_$($textId.ToString('x4'))."
    }
}
$pastBipinTreasure = $treasureObjectRecords['TREASURE_OBJECT_GASHA_SEED_08']
if ($null -eq $pastBipinTreasure -or
    $pastBipinTreasure.Treasure -ne 0x34 -or
    $pastBipinTreasure.SubId -ne 0x08 -or
    $pastBipinTreasure.Parameter -ne 0x01 -or
    $pastBipinTreasure.TextId -ne 0x004b -or
    $pastBipinTreasure.Graphic -ne 0x0d) {
    throw 'TREASURE_OBJECT_GASHA_SEED_08 no longer matches bipinScript3.'
}
$pastBipinCommandSpecs = @(
    @($pastBipinCommands[0],  'initcollisions', 'PastBipin', '', '', ''),
    @($pastBipinCommands[1],  'enableinput', '', '', '', ''),
    @($pastBipinCommands[2],  'checkabutton', 'PastBipin', '', '', ''),
    @($pastBipinCommands[3],  'disableinput', '', '', '', ''),
    @($pastBipinCommands[4],  'jumpifroomflagset', '', '20', '11', ''),
    @($pastBipinCommands[5],  'showtext', '', '4311', '', $allTexts[0x4311]),
    @($pastBipinCommands[6],  'giveitem', '', '34', '08', ''),
    @($pastBipinCommands[7],  'wait', '', '1', '', ''),
    @($pastBipinCommands[8],  'checktext', '', '', '', ''),
    @($pastBipinCommands[9],  'showtext', '', '4312', '', $allTexts[0x4312]),
    @($pastBipinCommands[10], 'scriptjump', '', '1', '', ''),
    @($pastBipinCommands[11], 'showtext', '', '4313', '', $allTexts[0x4313]),
    @($pastBipinCommands[12], 'scriptjump', '', '1', '', '')
)
$pastBipinCommandRows = [Collections.Generic.List[string]]::new()
$pastBipinCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
for ($index = 0; $index -lt $pastBipinCommandSpecs.Count; $index++) {
    $spec = $pastBipinCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $pastBipinCommandRows.Add((New-CutsceneCommandRow `
        'bipinScript3' $index $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\past_bipin_commands.tsv'),
    $pastBipinCommandRows)

# INTERAC_HARDHAT_WORKER $58:$00 selects its var03 branch through a source
# jump table, grants the Shovel, and restores animation $04 before returning
# to checkabutton.
$hardhatScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$hardhatOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'initcollisions', 'checkabutton', 'disableinput', 'asm15',
    'jumptable_objectbyte', 'jumpifroomflagset', 'showtextlowindex',
    'wait', 'giveitem', 'scriptjump', 'setanimation', 'enableinput')) {
    [void]$hardhatOpcodes.Add($opcode)
}
$hardhatCommands = @(Read-AssemblyCutsceneCommands `
    $hardhatScriptPath 'hardhatWorkerSubid00Script' $hardhatOpcodes `
    'hardhatWorkerSubid01Script')
$hardhatExpected = @(
    @('initcollisions', ''),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('asm15', 'scriptHelp.turnToFaceLink'),
    @('jumptable_objectbyte', 'Interaction.var03'),
    @('jumpifroomflagset', '$20, @alreadyGaveShovel'),
    @('showtextlowindex', '<TX_1001'),
    @('wait', '30'),
    @('giveitem', 'TREASURE_SHOVEL, $00'),
    @('wait', '30'),
    @('showtextlowindex', '<TX_1002'),
    @('scriptjump', '@enableInput'),
    @('showtextlowindex', '<TX_1000'),
    @('setanimation', '$04'),
    @('enableinput', ''),
    @('scriptjump', '@npcLoop')
)
if ($hardhatCommands.Count -ne $hardhatExpected.Count) {
    throw "hardhatWorkerSubid00Script expected 16 commands, parsed $($hardhatCommands.Count)."
}
for ($index = 0; $index -lt $hardhatExpected.Count; $index++) {
    $operands = ([string]$hardhatCommands[$index].Operands).Trim()
    if ($hardhatCommands[$index].Opcode -ne $hardhatExpected[$index][0] -or
        $operands -ne $hardhatExpected[$index][1]) {
        throw "hardhatWorkerSubid00Script command $index changed from " +
            "$($hardhatExpected[$index] -join ' ')."
    }
}
$hardhatJumpTargets = @(
    Read-AssemblyDataDirectives `
        $hardhatScriptPath 'hardhatWorkerSubid00Script' '.dw' |
        ForEach-Object { $_.Operands[0] })
if (($hardhatJumpTargets -join ',') -ne
    '@givesShovel,@doesntGiveShovel') {
    throw 'hardhatWorkerSubid00Script var03 jump table changed.'
}
$hardhatAnimation4 = Resolve-NpcAnimation 0x58 4
if ([string]::IsNullOrWhiteSpace($hardhatAnimation4)) {
    throw 'Could not resolve INTERAC_HARDHAT_WORKER animation $04.'
}
foreach ($textId in @(0x1000, 0x1001, 0x1002)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve hardhat text TX_$($textId.ToString('x4'))."
    }
}
$hardhatTreasure = $treasureObjectRecords['TREASURE_OBJECT_SHOVEL_00']
if ($null -eq $hardhatTreasure -or
    $hardhatTreasure.Treasure -ne 0x15 -or
    $hardhatTreasure.SubId -ne 0x00 -or
    $hardhatTreasure.Parameter -ne 0x00 -or
    $hardhatTreasure.TextId -ne 0x0025) {
    throw 'TREASURE_OBJECT_SHOVEL_00 no longer matches hardhatWorkerSubid00Script.'
}
$hardhatCommandSpecs = @(
    @($hardhatCommands[0],  'initcollisions', 'Hardhat', '', '', ''),
    @($hardhatCommands[1],  'checkabutton', 'Hardhat', '', '', ''),
    @($hardhatCommands[2],  'disableinput', '', '', '', ''),
    @($hardhatCommands[3],  'native', '', '', '', 'turnToFaceLink'),
    @($hardhatCommands[4],  'jumptablememory', '', '', '', 'HardhatVar03|5,12'),
    @($hardhatCommands[5],  'jumpifroomflagset', '', '20', '10', ''),
    @($hardhatCommands[6],  'showtext', '', '1001', '', $allTexts[0x1001]),
    @($hardhatCommands[7],  'wait', '', '30', '', ''),
    @($hardhatCommands[8],  'giveitem', '', '15', '00', ''),
    @($hardhatCommands[9],  'wait', '', '30', '', ''),
    @($hardhatCommands[10], 'showtext', '', '1002', '', $allTexts[0x1002]),
    @($hardhatCommands[11], 'scriptjump', '', '13', '', ''),
    @($hardhatCommands[12], 'showtext', '', '1000', '', $allTexts[0x1000]),
    @($hardhatCommands[13], 'setanimation', 'Hardhat', '04', '', $hardhatAnimation4),
    @($hardhatCommands[14], 'enableinput', '', '', '', ''),
    @($hardhatCommands[15], 'scriptjump', '', '1', '', '')
)
$hardhatCommandRows = [Collections.Generic.List[string]]::new()
$hardhatCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
for ($index = 0; $index -lt $hardhatCommandSpecs.Count; $index++) {
    $spec = $hardhatCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $hardhatCommandRows.Add((New-CutsceneCommandRow `
        'hardhatWorkerSubid00Script' $index $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\hardhat_shovel_commands.tsv'),
    $hardhatCommandRows)

# Rooms 0:7c and 2:2e Poe encounters. All placed INTERAC_POE records use the
# same poeScript; Interaction.var03 selects the first, tomb, or final meeting.
# The native state-0 visibility predicates are imported with ordinary NPC
# metadata, but the selected actor becomes script-owned after initialization
# and must not be refreshed when poeScript sets the room's $40 flag.
$poeScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$poeScriptSource = Read-ImportText $poeScriptPath
$poeOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'initcollisions', 'checkabutton', 'disableinput',
    'jumptable_objectbyte', 'showtext', 'orroomflag', 'wait',
    'playsound', 'writeobjectbyte', 'asm15', 'jumpifmemoryset',
    'scriptjump', 'enableinput', 'setspeed', 'setanimation',
    'setangle', 'applyspeed', 'giveitem', 'scriptend')) {
    [void]$poeOpcodes.Add($opcode)
}
$poeCommands = @(Read-AssemblyCutsceneCommands `
    $poeScriptPath 'poeScript' $poeOpcodes)
$poeExpectedCommands = @(
    @('initcollisions', ''),
    @('checkabutton', ''),
    @('disableinput', ''),
    @('jumptable_objectbyte', 'Interaction.var03'),
    @('showtext', 'TX_0b00'),
    @('orroomflag', '$40'),
    @('wait', '40'),
    @('playsound', 'SND_POOF'),
    @('writeobjectbyte', 'Interaction.var3e, 30'),
    @('asm15', 'poe_decCounterAndFlickerVisibility'),
    @('jumpifmemoryset', 'wcddb, $80, @end'),
    @('scriptjump', '@disappearLoop'),
    @('enableinput', ''),
    @('scriptend', ''),
    @('showtext', 'TX_0b01'),
    @('orroomflag', '$40'),
    @('wait', '30'),
    @('writeobjectbyte', 'Interaction.var3f, $01'),
    @('setspeed', 'SPEED_100'),
    @('setanimation', '$02'),
    @('setangle', '$10'),
    @('applyspeed', '$49'),
    @('setanimation', '$01'),
    @('setangle', '$08'),
    @('applyspeed', '$39'),
    @('scriptjump', '@disappear'),
    @('showtext', 'TX_0b02'),
    @('wait', '30'),
    @('giveitem', 'TREASURE_TRADEITEM, $00'),
    @('scriptjump', '@disappear')
)
if ($poeCommands.Count -ne $poeExpectedCommands.Count) {
    throw "poeScript expected 30 commands, parsed $($poeCommands.Count)."
}
for ($index = 0; $index -lt $poeExpectedCommands.Count; $index++) {
    $operands = if ($null -eq $poeCommands[$index].Operands) {
        ''
    } else {
        ([string]$poeCommands[$index].Operands).Trim()
    }
    if ($poeCommands[$index].Opcode -ne $poeExpectedCommands[$index][0] -or
        $operands -ne $poeExpectedCommands[$index][1]) {
        throw "poeScript command $index changed from $($poeExpectedCommands[$index] -join ' ')."
    }
}

$poeNativePath = Join-Path $Disassembly 'object_code\ages\interactions\poe.s'
$poeNativeSource = Read-ImportText $poeNativePath
$poeInteractionDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\interactionData.s')
if ($poeNativeSource -notmatch
        '(?ms)^@initSubid00:.*?getThisRoomFlags.*?bit 6,\(hl\).*?wPresentRoomFlags\+\$2e.*?bit 6,\(hl\).*?jr @init' -or
    $poeNativeSource -notmatch
        '(?ms)^@initSubid02:.*?getThisRoomFlags.*?ROOMFLAG_BIT_ITEM.*?bit 6,\(hl\).*?wPresentRoomFlags\+\$2e.*?bit 6,\(hl\).*?jr @init' -or
    $poeNativeSource -notmatch
        '(?ms)^@initSubid01:.*?wPresentRoomFlags\+\$7c.*?bit 6,\(hl\).*?getThisRoomFlags.*?bit 6,\(hl\).*?^@init:' -or
    $poeNativeSource -notmatch
        '(?ms)^@init:.*?@loadScriptAndInitGraphics\s+^@state1:.*?interactionRunScript.*?Interaction\.var3e.*?ret nz.*?Interaction\.var3f.*?npcFaceLinkAndAnimate.*?interactionAnimate.*?objectSetPriorityRelativeToLink_withTerrainEffects' -or
    $poeNativeSource -notmatch
        '(?ms)^@loadScriptAndInitGraphics:.*?interactionInitGraphics.*?objectMarkSolidPosition.*?interactionSetScript.*?interactionIncState.*?^@scriptTable:\s+\.dw mainScripts\.poeScript' -or
    $poeInteractionDataSource -notmatch
        '(?m)^\s*/\* \$59 \*/ m_InteractionData \$5d \$00 \$02\s*$') {
    throw 'INTERAC_POE entry predicates, initialization, or update order changed.'
}
if ($poeScriptSource -notmatch
        '(?ms)^poe_decCounterAndFlickerVisibility:\s+ld h,d\s+ld l,Interaction\.var3e\s+ld a,\(hl\)\s+or a\s+call writeFlagsTocddb\s+jr z,@setVisible\s+dec \(hl\)\s+ld a,\(wFrameCounter\)\s+rrca\s+rrca\s+jp nc,objectSetInvisible\s+^@setVisible:\s+jp objectSetVisible') {
    throw 'Poe disappearance counter or frame-mask flicker helper changed.'
}
if ($mainObjectSource -notmatch
    '(?ms)^group0Map7cObjectData:\s+obj_Interaction \$59 \$00 \$38 \$68 \$00\s+obj_Interaction \$59 \$00 \$38 \$68 \$02\s+obj_Pointer group0Map7cEnemyObjectData\s+obj_End') {
    throw 'Room 0:7c Poe object order, coordinates, or variants changed.'
}
if ($mainObjectSource -notmatch
    '(?ms)^group2Map2eObjectData:\s+obj_Interaction \$59 \$00 \$20 \$50 \$01\s+obj_End') {
    throw 'Room 2:2e Poe object order, coordinates, or variant changed.'
}

$poeAnimations = @{}
foreach ($animation in 0..3) {
    $poeAnimations[$animation] = Resolve-NpcAnimation 0x59 $animation
    if ([string]::IsNullOrWhiteSpace($poeAnimations[$animation])) {
        throw "Could not resolve INTERAC_POE animation `$$($animation.ToString('x2'))."
    }
}
foreach ($textId in 0x0b00..0x0b02) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Poe text TX_$($textId.ToString('x4'))."
    }
}
$poeTreasure = $treasureObjectRecords['TREASURE_OBJECT_TRADEITEM_00']
if ($null -eq $poeTreasure -or
    $poeTreasure.Treasure -ne 0x41 -or
    $poeTreasure.SubId -ne 0x00 -or
    $poeTreasure.Parameter -ne 0x00 -or
    $poeTreasure.TextId -ne 0x005a -or
    $poeTreasure.Graphic -ne 0x70) {
    throw 'TREASURE_OBJECT_TRADEITEM_00 no longer grants the Poe Clock.'
}
$poeSpeedSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\objectSpeeds.s')
$poeMusicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
if ($roomFlagSource -notmatch '\.define ROOMFLAG_ITEM\s+\$20' -or
    $tradeItemSource -notmatch 'TRADEITEM_POE_CLOCK\s+db ; \$00' -or
    $poeSpeedSource -notmatch 'SPEED_100\s+dsb 5 ; 0x28' -or
    $poeMusicSource -notmatch 'SND_POOF\s+db ; \$98') {
    throw 'Poe room flag, trade-item, speed, or sound constants changed.'
}

# Collapse the recognized asm15/cddb/scriptjump disappearance loop into the
# typed flicker command while retaining its source handler line and operands.
$poeCommandSpecs = @(
    @($poeCommands[0],  'initcollisions', 'Poe', '', '', ''),
    @($poeCommands[1],  'checkabutton', 'Poe', '', '', ''),
    @($poeCommands[2],  'disableinput', '', '', '', ''),
    @($poeCommands[3],  'jumptablememory', '', '', '', 'PoeVariant|4,12,24'),
    @($poeCommands[4],  'showtext', '', '0b00', '', $allTexts[0x0b00]),
    @($poeCommands[5],  'orroomflag', '', '40', '', ''),
    @($poeCommands[6],  'wait', '', '40', '', ''),
    @($poeCommands[7],  'playsound', '', '98', '', ''),
    @($poeCommands[8],  'writeobjectbyte', 'Poe', '3e', '1e', ''),
    @($poeCommands[9],  'flicker', 'Poe', '3e', '02', ''),
    @($poeCommands[12], 'enableinput', '', '', '', ''),
    @($poeCommands[13], 'scriptend', '', '', '', ''),
    @($poeCommands[14], 'showtext', '', '0b01', '', $allTexts[0x0b01]),
    @($poeCommands[15], 'orroomflag', '', '40', '', ''),
    @($poeCommands[16], 'wait', '', '30', '', ''),
    @($poeCommands[17], 'writeobjectbyte', 'Poe', '3f', '01', ''),
    @($poeCommands[18], 'setspeed', 'Poe', '28', '', ''),
    @($poeCommands[19], 'setanimation', 'Poe', '02', '', $poeAnimations[2]),
    @($poeCommands[20], 'setangle', 'Poe', '10', '', ''),
    @($poeCommands[21], 'applyspeed', 'Poe', '49', '', ''),
    @($poeCommands[22], 'setanimation', 'Poe', '01', '', $poeAnimations[1]),
    @($poeCommands[23], 'setangle', 'Poe', '08', '', ''),
    @($poeCommands[24], 'applyspeed', 'Poe', '39', '', ''),
    @($poeCommands[25], 'scriptjump', '', '6', '', ''),
    @($poeCommands[26], 'showtext', '', '0b02', '', $allTexts[0x0b02]),
    @($poeCommands[27], 'wait', '', '30', '', ''),
    @($poeCommands[28], 'giveitem', '', '41', '00', ''),
    @($poeCommands[29], 'scriptjump', '', '6', '', '')
)
$poeCommandRows = [Collections.Generic.List[string]]::new()
$poeCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
for ($index = 0; $index -lt $poeCommandSpecs.Count; $index++) {
    $spec = $poeCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $poeCommandRows.Add((New-CutsceneCommandRow `
        'poeScript' $index $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\poe_commands.tsv'),
    $poeCommandRows)

$poeEventRows = @(
    "# group`troom`tid`tsubid`tfirst-var03`ttomb-var03`tfinal-var03`tprogress-flag`titem-flag`ttomb-group`ttomb-room`tcollision-y`tcollision-x`tdisappear-wait`tflicker-count`tflicker-address`tflicker-mask`tpoof-sound`treward-treasure`treward-parameter`treward-object`tspeed-100`tinitial-script-updates`tanimation0`tanimation1`tanimation2`tanimation3"
    (@(
        '0', '7c', '59', '00', '00', '01', '02', '40', '20', '2', '2e',
        '06', '06', '40', '30', '3e', '02', '98', '41', '00',
        'TREASURE_OBJECT_TRADEITEM_00', '28', '1',
        $poeAnimations[0], $poeAnimations[1],
        $poeAnimations[2], $poeAnimations[3]
    ) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\poe_event.tsv'),
    $poeEventRows)

# Room 2:e6 Mask Salesman trade. INTERAC_MASK_SALESMAN is a script-owned NPC
# whose native wrapper runs the script once on its initialization update,
# enables always-update behavior, then animates after every later script update.
$maskSalesmanScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$maskSalesmanOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'setcollisionradii', 'makeabuttonsensitive', 'checkabutton',
    'disableinput', 'jumpifroomflagset', 'setanimation', 'showtext',
    'wait', 'jumpiftradeitemeq', 'scriptjump', 'jumpiftextoptioneq',
    'giveitem', 'enableinput')) {
    [void]$maskSalesmanOpcodes.Add($opcode)
}
$maskSalesmanCommands = Read-AssemblyCutsceneCommands `
    $maskSalesmanScriptPath 'maskSalesmanScript' $maskSalesmanOpcodes
if ($maskSalesmanCommands.Count -ne 44) {
    throw "maskSalesmanScript expected 44 commands, parsed $($maskSalesmanCommands.Count)."
}

$maskSalesmanTargets = @{}
foreach ($command in $maskSalesmanCommands) {
    if (-not $maskSalesmanTargets.ContainsKey($command.Label)) {
        $maskSalesmanTargets[$command.Label] = $command.Index
    }
}
$expectedMaskSalesmanTargets = @{
    '@npcLoop' = 2
    '@promptForTrade' = 19
    '@acceptedTrade' = 24
    '@alreadyGaveDoggieMask' = 41
    '@enableInput' = 42
}
foreach ($entry in $expectedMaskSalesmanTargets.GetEnumerator()) {
    if (-not $maskSalesmanTargets.ContainsKey($entry.Key) -or
        $maskSalesmanTargets[$entry.Key] -ne $entry.Value) {
        throw "maskSalesmanScript label $($entry.Key) moved from command $($entry.Value)."
    }
}

$maskSalesmanNativePath = Join-Path $Disassembly `
    'object_code\ages\interactions\maskSalesman.s'
$maskSalesmanNativeSource = Read-ImportText $maskSalesmanNativePath
$maskSalesmanWrapperPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$maskSalesmanWrapperSource = Read-ImportText $maskSalesmanWrapperPath
$maskSalesmanInteractionDataSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\interactionData.s')
if ($maskSalesmanNativeSource -notmatch
        '(?ms)^@state0:\s+call @loadScriptAndInitGraphics\s+call interactionSetAlwaysUpdateBit\s+^@state1:\s+call interactionRunScript\s+jp c,interactionDelete\s+jp interactionAnimateAsNpc' -or
    $maskSalesmanNativeSource -notmatch
        '(?ms)^@loadScriptAndInitGraphics:\s+call interactionInitGraphics.*?interactionSetScript\s+jp interactionIncState.*?^@scriptTable:\s+\.dw mainScripts\.maskSalesmanScript' -or
    $maskSalesmanWrapperSource -notmatch
        '(?ms)^maskSalesmanScript:\s+loadscript scriptHelp\.maskSalesmanScript' -or
    $maskSalesmanInteractionDataSource -notmatch
        '(?m)^\s*/\* \$5c \*/ m_InteractionData \$5e \$00 \$00\s*$') {
    throw 'INTERAC_MASK_SALESMAN native initialization, update order, or script wrapper changed.'
}
if ($mainObjectSource -notmatch
    '(?ms)^group2Mape6ObjectData:\s+obj_Interaction \$5c \$00 \$38 \$70\s+obj_End') {
    throw 'Room 2:e6 Mask Salesman object stream or coordinates changed.'
}

$maskSalesmanAnimations = @{
    0 = Resolve-NpcAnimation 0x5c 0
    1 = Resolve-NpcAnimation 0x5c 1
}
foreach ($animation in @(0, 1)) {
    if ([string]::IsNullOrWhiteSpace($maskSalesmanAnimations[$animation])) {
        throw "Could not resolve INTERAC_MASK_SALESMAN animation `$$($animation.ToString('x2'))."
    }
}
$maskSalesmanTextIds = @(0x0b0d..0x0b15) + 0x0b45
foreach ($textId in $maskSalesmanTextIds) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Mask Salesman text TX_$($textId.ToString('x4'))."
    }
}
$maskSalesmanTreasure = $treasureObjectRecords['TREASURE_OBJECT_TRADEITEM_04']
if ($null -eq $maskSalesmanTreasure -or
    $maskSalesmanTreasure.Treasure -ne 0x41 -or
    $maskSalesmanTreasure.SubId -ne 0x04 -or
    $maskSalesmanTreasure.Parameter -ne 0x04 -or
    $maskSalesmanTreasure.TextId -ne 0x005e -or
    $maskSalesmanTreasure.Graphic -ne 0x74) {
    throw 'TREASURE_OBJECT_TRADEITEM_04 no longer grants the Doggie Mask.'
}
if ($roomFlagSource -notmatch '\.define ROOMFLAG_ITEM\s+\$20' -or
    $tradeItemSource -notmatch 'TRADEITEM_TASTY_MEAT\s+db ; \$03' -or
    $tradeItemSource -notmatch 'TRADEITEM_DOGGIE_MASK\s+db ; \$04') {
    throw 'Mask Salesman room flag or trade-item constants changed.'
}

$maskSalesmanCommandRows = [Collections.Generic.List[string]]::new()
$maskSalesmanCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
foreach ($command in $maskSalesmanCommands) {
    $opcode = $command.Opcode
    $actor = ''
    $arg0 = ''
    $arg1 = ''
    $payload = ''
    switch ($command.Opcode) {
        'setcollisionradii' {
            if ($command.Operands -notmatch '^\$04,\s*\$06$') {
                throw "Unexpected Mask Salesman collision radii '$($command.Operands)'."
            }
            $actor = 'MaskSalesman'
            $arg0 = '04'
            $arg1 = '06'
        }
        'makeabuttonsensitive' { $actor = 'MaskSalesman' }
        'checkabutton' { $actor = 'MaskSalesman' }
        'jumpifroomflagset' {
            if ($command.Operands -notmatch
                '^ROOMFLAG_ITEM,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed Mask Salesman room-flag branch at source line $($command.Line)."
            }
            $arg0 = '20'
            $arg1 = $maskSalesmanTargets[$Matches['target']].ToString()
        }
        'setanimation' {
            if ($command.Operands -notmatch '^\$(?<animation>0[01])$') {
                throw "Unexpected Mask Salesman animation '$($command.Operands)'."
            }
            $animation = [Convert]::ToInt32($Matches['animation'], 16)
            $actor = 'MaskSalesman'
            $arg0 = $Matches['animation']
            $payload = $maskSalesmanAnimations[$animation]
        }
        'showtext' {
            if ($command.Operands -notmatch
                '^TX_(?<id>0b(?:0[d-f]|1[0-5]|45))$') {
                throw "Unexpected Mask Salesman text '$($command.Operands)'."
            }
            $textId = [Convert]::ToInt32($Matches['id'], 16)
            $arg0 = $Matches['id']
            $payload = $allTexts[$textId]
        }
        'wait' {
            if ($command.Operands -notin @('15', '30')) {
                throw "Unexpected Mask Salesman wait '$($command.Operands)'."
            }
            $arg0 = $command.Operands
        }
        'jumpiftradeitemeq' {
            if ($command.Operands -notmatch
                '^TRADEITEM_TASTY_MEAT,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed Mask Salesman trade-item branch at source line $($command.Line)."
            }
            $arg0 = '03'
            $arg1 = $maskSalesmanTargets[$Matches['target']].ToString()
        }
        'scriptjump' {
            if (-not $maskSalesmanTargets.ContainsKey($command.Operands)) {
                throw "Unknown Mask Salesman branch target '$($command.Operands)'."
            }
            $arg0 = $maskSalesmanTargets[$command.Operands].ToString()
        }
        'jumpiftextoptioneq' {
            if ($command.Operands -notmatch
                '^\$00,\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed Mask Salesman choice branch at source line $($command.Line)."
            }
            $arg0 = '00'
            $arg1 = $maskSalesmanTargets[$Matches['target']].ToString()
        }
        'giveitem' {
            if ($command.Operands -ne 'TREASURE_TRADEITEM,$04') {
                throw "Unexpected Mask Salesman reward '$($command.Operands)'."
            }
            $arg0 = '41'
            $arg1 = '04'
        }
    }
    $maskSalesmanCommandRows.Add((New-CutsceneCommandRow `
        $command.Script $command.Index $command.Label $command.Line `
        $opcode $actor "$arg0" "$arg1" $payload))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\mask_salesman_commands.tsv'),
    $maskSalesmanCommandRows)

$maskSalesmanEventRows = @(
    "# group`troom`tid`tsubid`tanimation0`tanimation1`tinitial-animation`tcollision-y`tcollision-x`troom-flag`trequired-trade`treward-treasure`treward-parameter`treward-object`tinitial-script-updates`talways-update"
    (@(
        '2', 'e6', '5c', '00',
        $maskSalesmanAnimations[0], $maskSalesmanAnimations[1],
        '00', '04', '06', '20', '03', '41', '04',
        'TREASURE_OBJECT_TRADEITEM_04', '1', '1'
    ) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\mask_salesman_event.tsv'),
    $maskSalesmanEventRows)

# Fairies' Woods hide-and-seek. INTERAC_FAIRY_HIDING_MINIGAME ($6c) owns
# three interaction scripts, while INTERAC_FOREST_FAIRY ($49) implements the
# exact table-driven flight used by both the introduction and each reveal.
$fairyMinigamePath = Join-Path $Disassembly `
    'object_code\ages\interactions\fairyHidingMinigame.s'
$fairyMinigameSource = Read-ImportText $fairyMinigamePath
$forestFairyPath = Join-Path $Disassembly `
    'object_code\ages\interactions\forestFairy.s'
$forestFairySource = Read-ImportText $forestFairyPath
$fairyScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$fairyScriptSource = Read-ImportText $fairyScriptPath
$forestFairyScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$forestFairyScriptSource = Read-ImportText $forestFairyScriptPath
$forestTransitionPath = Join-Path $Disassembly 'code\bank1.s'
$forestTransitionSource = Read-ImportText $forestTransitionPath
$paletteFadePath = Join-Path $Disassembly 'code\bank0.s'
$paletteFadeSource = Read-ImportText $paletteFadePath
$miscCutscenePath = Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s'
$miscCutsceneSource = Read-ImportText $miscCutscenePath
$roomSpecificPath = Join-Path $Disassembly 'code\ages\roomSpecificCode.s'
$roomSpecificSource = Read-ImportText $roomSpecificPath

if ($fairyMinigameSource -notmatch
        '(?ms)^@table:\s+\.db \$25 \$03\s+\.db \$54 \$04\s+\.db \$32 \$05\s+\.db \$00' -or
    $fairyMinigameSource -notmatch
        '(?ms)^@state1:.*?objectGetTileAtPosition.*?Interaction\.var38.*?interactionDecCounter1.*?fairyHidingMinigame_checkBeginCutscene.*?wDisableScreenTransitions' -or
    $fairyMinigameSource -notmatch
        '(?ms)^fairyHidingMinigame_subid00:.*?^@state1:\s+call fairyHidingMinigame_checkBeginCutscene\s+ret nc\s+ld a,\(wScreenTransitionDirection\)\s+ld \(w1Link\.direction\),a\s+ld a,\$01\s+ld \(wTmpcfc0\.fairyHideAndSeek\.active\),a' -or
    $fairyMinigameSource -notmatch
        '(?ms)^@warpDestination:\s+m_HardcodedWarpA ROOM_AGES_082, \$00, \$64, \$03' -or
    $fairyMinigameSource -notmatch
        '(?ms)^fairyHidingMinigame_subid02:.*?interactionRunScript.*?ld hl,wTmpcfc0\.fairyHideAndSeek\.active\s+ld b,\$10\s+call clearMemory' -or
    $forestFairySource -notmatch
        '(?ms)^forestFairy_subid00State1:.*?sub c\s+add \$04\s+cp \$09.*?sub b\s+add \$04\s+cp \$09.*?Interaction\.var3a.*?\$5a.*?INTERAC_SPARKLE, \$02.*?objectGetRelativeAngleWithTempVars\s+call objectNudgeAngleTowards.*?objectApplySpeed.*?SND_MAGIC_POWDER' -or
    $forestFairySource -notmatch
        '(?ms)^forestFairy_subid00State1:.*?ld e,Interaction\.subid\s+ld a,\(de\)\s+cp \$03\s+jr nc,@label_09_160\s+ld \(hl\),c\s+ld l,Interaction\.yh\s+ld \(hl\),b\s+ld l,Interaction\.state\s+inc \(hl\).*?^forestFairy_subid00State2:\s+ld a,\(wTmpcfc0\.fairyHideAndSeek\.cfd2\)\s+or a\s+jr nz,forestFairy_animate\s+ld e,Interaction\.var03\s+ld a,\(de\)\s+cp \$06\s+jr nc,@createPuffAndDelete.*?^@createPuffAndDelete:\s+call objectCreatePuff\s+jr forestFairy_deleteSelf' -or
    $miscCutsceneSource -notmatch
        '(?ms)^@spawnForestFairy:\s+call getFreeInteractionSlot\s+ret nz\s+ld \(hl\),INTERAC_FOREST_FAIRY\s+ld l,Interaction\.var03\s+ld \(hl\),b\s+jp fairyCutscene_incState' -or
    $forestFairySource -notmatch
        '(?ms)^forestFairy_discoveredPositions:\s+\.db \$48 \$38\s+\.db \$48 \$68\s+\.db \$28 \$50') {
    throw "Fairies' Woods minigame state, cutscene spawn, tile-entry puff, flight, or discovered-fairy behavior changed."
}
if ($mainObjectSource -notmatch
        '(?ms)^group0Map80ObjectData:\s+obj_Interaction \$6c \$01 \$58 \$48' -or
    $mainObjectSource -notmatch
        '(?ms)^group0Map81ObjectData:\s+obj_Interaction \$6c \$01 \$28 \$58' -or
    $mainObjectSource -notmatch
        '(?ms)^group0Map82ObjectData:\s+obj_Interaction \$6c \$00' -or
    $mainObjectSource -notmatch
        '(?ms)^group0Map91ObjectData:\s+obj_Interaction \$6c \$01 \$38 \$28' -or
    $mainObjectSource -notmatch
        '(?ms)^group0Map92ObjectData:\s+obj_Interaction \$6c \$02 \$28 \$9f') {
    throw "Fairies' Woods $6c controller placements changed."
}
if ($fairyScriptSource -notmatch
        '(?ms)^fairyHidingMinigame_subid00Script:\s+asm15 fairyHidingMinigame_spawnForestFairyIndex, \$00\s+wait 20\s+asm15 fairyHidingMinigame_spawnForestFairyIndex, \$01\s+wait 20\s+asm15 fairyHidingMinigame_spawnForestFairyIndex, \$02\s+checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$03\s+wait 20\s+showtext TX_1100\s+wait 8\s+showtext TX_1101\s+wait 8\s+showtext TX_1102\s+wait 8\s+showtext TX_1103\s+checktext\s+writememory\s+wTmpcfc0\.fairyHideAndSeek\.cfd2, \$00\s+checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$03\s+scriptend' -or
    $fairyScriptSource -notmatch
        '(?ms)^fairyHidingMinigame_subid01Script:\s+checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$01\s+wait 20\s+asm15 fairyHidingMinigame_showFairyFoundText\s+writememory\s+wTmpcfc0\.fairyHideAndSeek\.cfd2, \$00\s+checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$01\s+scriptend' -or
    $fairyScriptSource -notmatch
        '(?ms)^fairyHidingMinigame_subid02Script:\s+setcollisionradii \$20, \$01\s+makeabuttonsensitive\s+^@checkLinkLeaving:\s+checkcollidedwithlink_ignorez\s+showtext TX_110c\s+jumpiftextoptioneq \$00, @leave\s+asm15 fairyHidingMinigame_moveLinkBackLeft\s+wait 10\s+scriptjump @checkLinkLeaving\s+^@leave:\s+scriptend' -or
    $forestFairyScriptSource -notmatch
        '(?ms)^forestFairyScript_firstDiscovered:\s+makeabuttonsensitive\s+^@npcLoop:\s+checkabutton\s+showtext TX_1108\s+scriptjump @npcLoop\s+.*?^forestFairyScript_secondDiscovered:\s+makeabuttonsensitive\s+^@npcLoop:\s+checkabutton\s+showtext TX_1109\s+scriptjump @npcLoop') {
    throw "Fairies' Woods interaction scripts changed."
}
if ($forestTransitionSource -notmatch
        '(?ms)^screenTransitionForestScrambler:.*?GLOBALFLAG_FOREST_UNSCRAMBLED.*?^@forestScramblerTable:\s+\.db \$00 \$71 \$90 \$00\s+\.db \$00 \$82 \$91 \$80\s+\.db \$00 \$00 \$92 \$82\s+\.db \$72 \$82 \$80 \$00\s+\.db \$80 \$82 \$82 \$71\s+\.db \$70 \$71 \$82 \$71\s+\.db \$81 \$92 \$00 \$00\s+\.db \$72 \$91 \$00 \$92\s+\.db \$82 \$00 \$00 \$92' -or
    $miscCutsceneSource -notmatch
        '(?ms)CUTSCENE_FAIRIES_HIDE.*?ROOM_AGES_081.*?ROOM_AGES_080.*?ROOM_AGES_091.*?ROOM_AGES_082' -or
    $miscCutsceneSource -notmatch
        '(?ms)^fairyCutscene_cfd1is07:.*?TX_110a.*?^@state1:.*?\$0c.*?wTmpcbb6.*?SND_MYSTERY_SEED.*?fastFadeinFromWhite.*?^@state2:.*?wTmpcbb6.*?jr @state1.*?^@state3:.*?wTmpcbb6.*?\(\$cfd0\).*?SND_MYSTERY_SEED.*?\$08.*?fadeinFromWhiteWithDelay.*?TX_110b.*?GLOBALFLAG_WON_FAIRY_HIDING_GAME.*?GLOBALFLAG_FOREST_UNSCRAMBLED' -or
    $roomSpecificSource -notmatch
        '(?ms)^roomSpecificCodeGroup0Table:\s+\.db \$93 \$00.*?^roomSpecificCode0:\s+ld a,GLOBALFLAG_WON_FAIRY_HIDING_GAME.*?ld hl,\$cfd0\s+ld b,\$10\s+jp clearMemory') {
    throw 'Fairies'' Woods forest transition, hiding cutscene, completion, or room $0:$93 reset changed.'
}

$normalFadeOutMatch = [regex]::Match(
    $paletteFadeSource,
    '(?ms)^fadeoutToWhite:\s+ld a,\$01\s+ld \(wPaletteThread_mode\),a\s+ld a,\$(?<speed>[0-9a-f]{2})\s+^\+\+\s+ld \(wPaletteThread_speed\),a\s+xor a\s+ld \(wPaletteThread_fadeOffset\),a')
$normalFadeInMatch = [regex]::Match(
    $paletteFadeSource,
    '(?ms)^fadeinFromWhite:\s+ld a,\$02\s+ld \(wPaletteThread_mode\),a\s+ld a,\$(?<speed>[0-9a-f]{2})\s+^\+\+\s+ld \(wPaletteThread_speed\),a\s+ld a,\$20\s+ld \(wPaletteThread_fadeOffset\),a')
$fastFadeInMatch = [regex]::Match(
    $paletteFadeSource,
    '(?ms)^fastFadeinFromWhite:\s+ld a,\$02\s+ld \(wPaletteThread_mode\),a\s+ld a,\$(?<speed>[0-9a-f]{2})\s+jr \+\+')
$delayedFadeMatch = [regex]::Match(
    $miscCutsceneSource,
    '(?ms)^fairyCutscene_cfd1is07:.*?^@state3:.*?ld a,\$(?<refill>[0-9a-f]{2})\s+jp fadeinFromWhiteWithDelay')
if (-not $normalFadeOutMatch.Success -or
    -not $normalFadeInMatch.Success -or
    -not $fastFadeInMatch.Success -or
    -not $delayedFadeMatch.Success -or
    $paletteFadeSource -notmatch
        '(?ms)^setPaletteThreadDelay:\s+ld \(wPaletteThread_counterRefill\),a\s+ld a,\$01\s+ld \(wPaletteThread_counter\),a' -or
    $forestTransitionSource -notmatch
        '(?ms)^paletteFadeHandler01:.*?wPaletteThread_speed.*?wPaletteThread_fadeOffset.*?add c\s+cp \$20\s+jp nc,paletteThread_stop' -or
    $forestTransitionSource -notmatch
        '(?ms)^paletteFadeHandler02:.*?wPaletteThread_speed.*?wPaletteThread_fadeOffset.*?sub c\s+jr c,paletteThread_stop' -or
    $forestTransitionSource -notmatch
        '(?ms)^paletteThread_decCounter:\s+ld hl,wPaletteThread_counter\s+dec \(hl\)\s+ret nz\s+ld a,\(wPaletteThread_counterRefill\)\s+ld \(wPaletteThread_counter\),a') {
    throw "Fairies' Woods palette fade initialization or update cadence changed."
}
$normalFadeOutSpeed = [Convert]::ToInt32(
    $normalFadeOutMatch.Groups['speed'].Value, 16)
$normalFadeInSpeed = [Convert]::ToInt32(
    $normalFadeInMatch.Groups['speed'].Value, 16)
$fastFadeInSpeed = [Convert]::ToInt32(
    $fastFadeInMatch.Groups['speed'].Value, 16)
$delayedFadeRefill = [Convert]::ToInt32(
    $delayedFadeMatch.Groups['refill'].Value, 16)
if ($normalFadeOutSpeed -ne $normalFadeInSpeed) {
    throw "Fairies' Woods normal white fade speeds no longer match."
}
$normalFadeOutDuration = [int][Math]::Ceiling(
    32.0 / $normalFadeOutSpeed)
$normalFadeInDuration =
    [int][Math]::Floor(32.0 / $normalFadeInSpeed) + 1
$fastFadeInDuration =
    [int][Math]::Floor(32.0 / $fastFadeInSpeed) + 1
$delayedFadeInDuration =
    1 + (($normalFadeInDuration - 1) * $delayedFadeRefill)
if (($normalFadeOutSpeed, $normalFadeInSpeed, $fastFadeInSpeed,
     $delayedFadeRefill, $normalFadeOutDuration, $normalFadeInDuration,
     $fastFadeInDuration, $delayedFadeInDuration) -join ',' -ne
    '1,1,3,8,32,33,11,257') {
    throw "Fairies' Woods white fade constants diverged from the supported ROM."
}

$fairyMovementMatch = [regex]::Match(
    $forestFairySource,
    '(?ms)^@data:\s*(?<rows>(?:\s*\.db \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2} \$[0-9a-f]{2}){22})')
if (-not $fairyMovementMatch.Success) {
    throw 'Could not locate the 22-row forestFairy movement table.'
}
$fairyMovementRows = @([regex]::Matches(
    $fairyMovementMatch.Groups['rows'].Value,
    '\.db \$(?<b0>[0-9a-f]{2}) \$(?<b1>[0-9a-f]{2}) \$(?<b2>[0-9a-f]{2}) \$(?<b3>[0-9a-f]{2})'))
if ($fairyMovementRows.Count -ne 22) {
    throw "Expected 22 forestFairy movement rows, got $($fairyMovementRows.Count)."
}

$forestFairyGraphic = $interactionGraphics['73:0']
$forestSparkleGraphic = $interactionGraphics['132:2']
if ($null -eq $forestFairyGraphic -or
    $forestFairyGraphic.Gfx -ne 0x4c -or
    $forestFairyGraphic.TileBase -ne 0x16 -or
    $null -eq $forestSparkleGraphic -or
    $forestSparkleGraphic.Gfx -ne 0x6b -or
    $forestSparkleGraphic.TileBase -ne 0x0a -or
    $forestSparkleGraphic.Palette -ne 0 -or
    $forestSparkleGraphic.DefaultAnimation -ne 1) {
    throw 'Forest fairy or $84:$02 sparkle graphics changed.'
}
$forestFairyAnimation0 = Resolve-NpcAnimation 0x49 0
$forestFairyAnimation1 = Resolve-NpcAnimation 0x49 1
$forestSparkleAnimation = Resolve-NpcAnimation 0x84 1
if (-not $forestFairyAnimation0 -or -not $forestFairyAnimation1 -or
    -not $forestSparkleAnimation) {
    throw 'Could not resolve forest fairy or sparkle animations.'
}

$fairyTextRows = [Collections.Generic.List[string]]::new()
$fairyTextRows.Add("# text-id`ttext-base64")
foreach ($textId in 0x1100..0x110c) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Fairies' Woods text TX_$($textId.ToString('x4'))."
    }
    $fairyTextRows.Add(
        "$($textId.ToString('x4'))`t$(ConvertTo-CutsceneCommandPayload $allTexts[$textId])")
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_text.tsv'),
    $fairyTextRows)

$introBody = [regex]::Match(
    $fairyScriptSource,
    '(?ms)^fairyHidingMinigame_subid00Script:(?<body>.*?)(?=^; Hiding spot for fairy revealed)')
if (-not $introBody.Success) { throw 'Could not locate fairy intro script body.' }
function Add-FairyCommand(
    [Collections.Generic.List[string]]$rows,
    [string]$script,
    [int]$index,
    [string]$label,
    [string]$pattern,
    [string]$opcode,
    [string]$actor,
    [string]$arg0,
    [string]$arg1,
    [string]$payload,
    [int]$occurrence = 0) {
    $line = Find-CutsceneCommandSourceLine `
        $fairyScriptSource `
        $introBody.Groups['body'].Index `
        ($introBody.Groups['body'].Index + $introBody.Groups['body'].Length) `
        $pattern $script $occurrence
    $rows.Add((New-CutsceneCommandRow `
        $script $index $label $line $opcode $actor $arg0 $arg1 $payload))
}

$fairyIntroRows = [Collections.Generic.List[string]]::new()
$fairyIntroRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 0 `
    'fairyHidingMinigame_subid00Script' `
    '^\s*asm15 fairyHidingMinigame_spawnForestFairyIndex, \$00\s*$' `
    'nativeyield' '' '' '' 'SpawnForestFairy:0'
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 1 `
    'fairyHidingMinigame_subid00Script' '^\s*wait 20\s*$' `
    'wait' '' '20' '' ''
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 2 `
    'fairyHidingMinigame_subid00Script' `
    '^\s*asm15 fairyHidingMinigame_spawnForestFairyIndex, \$01\s*$' `
    'nativeyield' '' '' '' 'SpawnForestFairy:1'
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 3 `
    'fairyHidingMinigame_subid00Script' '^\s*wait 20\s*$' `
    'wait' '' '20' '' '' 1
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 4 `
    'fairyHidingMinigame_subid00Script' `
    '^\s*asm15 fairyHidingMinigame_spawnForestFairyIndex, \$02\s*$' `
    'nativeyield' '' '' '' 'SpawnForestFairy:2'
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 5 `
    'fairyHidingMinigame_subid00Script' `
    '^\s*checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$03\s*$' `
    'checkmemoryeq' '' '03' '' 'FairySignal'
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 6 `
    'fairyHidingMinigame_subid00Script' '^\s*wait 20\s*$' `
    'wait' '' '20' '' '' 2
$introIndex = 7
$introWait8Occurrence = 0
foreach ($textId in 0x1100..0x1103) {
    Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' `
        $introIndex 'fairyHidingMinigame_subid00Script' `
        "^\s*showtext TX_$($textId.ToString('x4'))\s*$" `
        'showtext' '' $textId.ToString('x4') '' $allTexts[$textId]
    $introIndex++
    if ($textId -ne 0x1103) {
        Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' `
            $introIndex 'fairyHidingMinigame_subid00Script' `
            '^\s*wait 8\s*$' 'wait' '' '8' '' '' $introWait8Occurrence
        $introWait8Occurrence++
        $introIndex++
    }
}
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 14 `
    'fairyHidingMinigame_subid00Script' `
    '^\s*writememory\s+wTmpcfc0\.fairyHideAndSeek\.cfd2, \$00\s*$' `
    'writememory' '' '00' '' 'FairySignal'
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 15 `
    'fairyHidingMinigame_subid00Script' `
    '^\s*checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$03\s*$' `
    'checkmemoryeq' '' '03' '' 'FairySignal' 1
Add-FairyCommand $fairyIntroRows 'fairyHidingMinigame_subid00Script' 16 `
    'fairyHidingMinigame_subid00Script' '^\s*scriptend\s*$' `
    'scriptend' '' '' '' ''
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_intro_commands.tsv'),
    $fairyIntroRows)

$fairyRevealRows = [Collections.Generic.List[string]]::new()
$fairyRevealRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
$revealBody = [regex]::Match(
    $fairyScriptSource,
    '(?ms)^fairyHidingMinigame_subid01Script:(?<body>.*?)(?=^; Checks for Link leaving)')
if (-not $revealBody.Success) { throw 'Could not locate fairy reveal script body.' }
$revealPatterns = @(
    @('checkmemoryeq', '', '01', '', 'FairySignal',
        '^\s*checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$01\s*$'),
    @('wait', '', '20', '', '', '^\s*wait 20\s*$'),
    @('nativeyield', '', '', '', 'ShowFairyFoundText',
        '^\s*asm15 fairyHidingMinigame_showFairyFoundText\s*$'),
    @('writememory', '', '00', '', 'FairySignal',
        '^\s*writememory\s+wTmpcfc0\.fairyHideAndSeek\.cfd2, \$00\s*$'),
    @('checkmemoryeq', '', '01', '', 'FairySignal',
        '^\s*checkmemoryeq wTmpcfc0\.fairyHideAndSeek\.cfd2, \$01\s*$'),
    @('scriptend', '', '', '', '', '^\s*scriptend\s*$')
)
for ($index = 0; $index -lt $revealPatterns.Count; $index++) {
    $entry = $revealPatterns[$index]
    $occurrence = if ($index -eq 4) { 1 } else { 0 }
    $line = Find-CutsceneCommandSourceLine `
        $fairyScriptSource $revealBody.Groups['body'].Index `
        ($revealBody.Groups['body'].Index + $revealBody.Groups['body'].Length) `
        $entry[5] 'fairyHidingMinigame_subid01Script' $occurrence
    $fairyRevealRows.Add((New-CutsceneCommandRow `
        'fairyHidingMinigame_subid01Script' $index `
        'fairyHidingMinigame_subid01Script' $line `
        $entry[0] $entry[1] $entry[2] $entry[3] $entry[4]))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_reveal_commands.tsv'),
    $fairyRevealRows)

$fairyExitRows = [Collections.Generic.List[string]]::new()
$fairyExitRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
$exitBody = [regex]::Match(
    $fairyScriptSource,
    '(?ms)^fairyHidingMinigame_subid02Script:(?<body>.*?)(?=^; =+)')
if (-not $exitBody.Success) { throw 'Could not locate fairy exit script body.' }
$exitCommands = @(
    @('fairyHidingMinigame_subid02Script', 'setcollisionradii', 'FairyExit', '20', '01', '',
        '^\s*setcollisionradii \$20, \$01\s*$'),
    @('fairyHidingMinigame_subid02Script', 'makeabuttonsensitive', 'FairyExit', '', '', '',
        '^\s*makeabuttonsensitive\s*$'),
    @('@checkLinkLeaving', 'nativeblock', 'FairyExit', '1', '', 'WaitForExitCollision',
        '^\s*checkcollidedwithlink_ignorez\s*$'),
    @('@checkLinkLeaving', 'showtext', '', '110c', '', $allTexts[0x110c],
        '^\s*showtext TX_110c\s*$'),
    @('@checkLinkLeaving', 'jumpiftextoptioneq', '', '00', '8', '',
        '^\s*jumpiftextoptioneq \$00, @leave\s*$'),
    @('@checkLinkLeaving', 'nativeyield', '', '', '', 'MoveLinkBackLeft',
        '^\s*asm15 fairyHidingMinigame_moveLinkBackLeft\s*$'),
    @('@checkLinkLeaving', 'wait', '', '10', '', '', '^\s*wait 10\s*$'),
    @('@checkLinkLeaving', 'scriptjump', '', '2', '', '',
        '^\s*scriptjump @checkLinkLeaving\s*$'),
    @('@leave', 'scriptend', '', '', '', '', '^\s*scriptend\s*$')
)
for ($index = 0; $index -lt $exitCommands.Count; $index++) {
    $entry = $exitCommands[$index]
    $line = Find-CutsceneCommandSourceLine `
        $fairyScriptSource $exitBody.Groups['body'].Index `
        ($exitBody.Groups['body'].Index + $exitBody.Groups['body'].Length) `
        $entry[6] 'fairyHidingMinigame_subid02Script'
    $fairyExitRows.Add((New-CutsceneCommandRow `
        'fairyHidingMinigame_subid02Script' $index $entry[0] $line `
        $entry[1] $entry[2] $entry[3] $entry[4] $entry[5]))
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_exit_commands.tsv'),
    $fairyExitRows)

$fairyEventRows = @(
    "# group`tstart-room`texit-room`treset-room`tessence-treasure`tactive-address`tfound-address`tsignal-address`tcompletion-flag`tunscrambled-flag`thidden-delay`texit-y`texit-x`texit-radius-y`texit-radius-x`tmagic-sound`tpuff-sound`tmystery-sound`tnormal-fade-out`tnormal-fade-in`tfast-fade-in`tcompletion-hold`tdelayed-fade-in`tnormal-fade-speed`tfast-fade-speed`tdelayed-fade-refill`tforest-fairy-sprite`tfairy-tile-base`tanimation0`tanimation1`tsparkle-sprite`tsparkle-tile-base`tsparkle-palette`tsparkle-animation"
    (@(
        '0', '82', '92', '93', '40', 'cfd0', 'cfd1', 'cfd2',
        '0e', '2b', '12', '28', '9f', '20', '01',
        '83', '98', '7b',
        $normalFadeOutDuration, $normalFadeInDuration, $fastFadeInDuration,
        '12', $delayedFadeInDuration,
        $normalFadeInSpeed, $fastFadeInSpeed, $delayedFadeRefill,
        $gfxNames[$forestFairyGraphic.Gfx], $forestFairyGraphic.TileBase,
        $forestFairyAnimation0, $forestFairyAnimation1,
        $gfxNames[$forestSparkleGraphic.Gfx], $forestSparkleGraphic.TileBase,
        $forestSparkleGraphic.Palette, $forestSparkleAnimation
    ) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_event.tsv'),
    $fairyEventRows)

$fairyMovementOutput = [Collections.Generic.List[string]]::new()
$fairyMovementOutput.Add(
    "# index`tinitial-y`tinitial-x`tangle`tcounter`ttarget-y`ttarget-x`tdirection`tpalette`tsource")
for ($index = 0; $index -lt $fairyMovementRows.Count; $index++) {
    $row = $fairyMovementRows[$index]
    $b0 = [Convert]::ToInt32($row.Groups['b0'].Value, 16)
    $b1 = [Convert]::ToInt32($row.Groups['b1'].Value, 16)
    $b2 = [Convert]::ToInt32($row.Groups['b2'].Value, 16)
    $b3 = [Convert]::ToInt32($row.Groups['b3'].Value, 16)
    $fairyMovementOutput.Add(@(
        $index,
        (($b0 -band 0xf8).ToString('x2')),
        (($b1 -band 0xf8).ToString('x2')),
        ((($b0 -band 7) * 4).ToString('x2')),
        (($b1 -band 7) + 1),
        (($b2 -band 0xf8).ToString('x2')),
        (($b3 -band 0xf8).ToString('x2')),
        ($b2 -band 1),
        ($b3 -band 7),
        "forestFairy.s:@data+$($index.ToString('x2'))"
    ) -join "`t")
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_movement.tsv'),
    $fairyMovementOutput)

# SPEED_200 is raw speed byte $50. getPositionOffsetForVelocity swaps its
# nibbles and indexes objectSpeedTable-$50, producing row offset $04b0. The
# clean US ROM places bank3.objectSpeedTable at file offset $00c09b; assert its
# first eight signed sine words before consuming it.
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
$speed200Offset = $speedTableRomOffset + 0x04b0
if ($forestFairySource -notmatch
        '(?ms)^forestFairy_subid00State0:.*?Interaction\.speed\s+ld \(hl\),SPEED_200' -or
    $forestTransitionSource.Length -eq 0 -or
    $romBytes.Length -le $speed200Offset + 0x4e) {
    throw 'Could not verify SPEED_200 forest-fairy velocity source.'
}
$fairyVelocityRows = [Collections.Generic.List[string]]::new()
$fairyVelocityRows.Add("# angle`ty-fixed`tx-fixed`tsource")
for ($angle = 0; $angle -lt 32; $angle++) {
    $offset = $speed200Offset + $angle * 2
    $y = [BitConverter]::ToInt16($romBytes, $offset)
    $x = [BitConverter]::ToInt16($romBytes, $offset + 0x10)
    $fairyVelocityRows.Add(
        "$($angle.ToString('x2'))`t$y`t$x`tbank3.objectSpeedTable:SPEED_200")
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_velocity.tsv'),
    $fairyVelocityRows)

$fairyHiddenRows = @(
    "# room`tpacked-position`tfairy-index`tsource"
    "81`t25`t03`tfairyHidingMinigame.s:@table"
    "80`t54`t04`tfairyHidingMinigame.s:@table"
    "91`t32`t05`tfairyHidingMinigame.s:@table"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_hidden_spots.tsv'),
    $fairyHiddenRows)

$fairyHidingRoomRows = @(
    "# index`troom`tpreset`tsource"
    "0`t81`t0c`tmiscCutscenes.s:CUTSCENE_FAIRIES_HIDE"
    "1`t80`t0d`tmiscCutscenes.s:CUTSCENE_FAIRIES_HIDE"
    "2`t91`t0e`tmiscCutscenes.s:CUTSCENE_FAIRIES_HIDE"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_hiding_rooms.tsv'),
    $fairyHidingRoomRows)

$fairyDiscoveredRows = @(
    "# index`ty`tx`tpalette`tanimation`tsource"
    "0`t48`t38`t1`t$forestFairyAnimation0`tforestFairy.s:forestFairy_discoveredPositions"
    "1`t48`t68`t2`t$forestFairyAnimation1`tforestFairy.s:forestFairy_discoveredPositions"
    "2`t28`t50`t3`t$forestFairyAnimation1`tforestFairy.s:forestFairy_discoveredPositions"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_discovered.tsv'),
    $fairyDiscoveredRows)

$scramblerRooms = @(0x70, 0x71, 0x72, 0x80, 0x81, 0x82, 0x90, 0x91, 0x92)
$scramblerValues = @(
    @(0x00, 0x71, 0x90, 0x00),
    @(0x00, 0x82, 0x91, 0x80),
    @(0x00, 0x00, 0x92, 0x82),
    @(0x72, 0x82, 0x80, 0x00),
    @(0x80, 0x82, 0x82, 0x71),
    @(0x70, 0x71, 0x82, 0x71),
    @(0x81, 0x92, 0x00, 0x00),
    @(0x72, 0x91, 0x00, 0x92),
    @(0x82, 0x00, 0x00, 0x92)
)
$fairyScramblerRows = [Collections.Generic.List[string]]::new()
$fairyScramblerRows.Add("# room`tup`tright`tdown`tleft`tsource")
for ($index = 0; $index -lt $scramblerRooms.Count; $index++) {
    $values = $scramblerValues[$index]
    $fairyScramblerRows.Add(
        "$($scramblerRooms[$index].ToString('x2'))`t" +
        (($values | ForEach-Object { $_.ToString('x2') }) -join "`t") +
        "`tbank1.s:@forestScramblerTable")
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\fairies_woods_scrambler.tsv'),
    $fairyScramblerRows)

# Present indoor room 2:e9 is the Lynna shooting gallery. INTERAC_SHOOTING_GALLERY
# $30:$00 owns the typed conversation and cleanup scripts; its dynamically
# created $30:$03 controller and PART_BALL $38 retain their native state
# machines. Export the complete script streams and every native table they
# consume instead of reconstructing layouts, scoring, or reward thresholds in
# runtime code.
$shootingGalleryScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$shootingGalleryHelperPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$shootingGalleryNativePath = Join-Path $Disassembly (
    'object_code\ages\interactions\shootingGallery.s')
$shootingGalleryBallPath = Join-Path $Disassembly 'object_code\ages\parts\ball.s'
$shootingGalleryDebrisPath = Join-Path $Disassembly (
    'object_code\ages\interactions\fallingRock.s')
$shootingGalleryScriptSource = Read-ImportText $shootingGalleryScriptPath
$shootingGalleryHelperSource = Read-ImportText $shootingGalleryHelperPath
$shootingGalleryNativeSource = Read-ImportText $shootingGalleryNativePath
$shootingGalleryBallSource = Read-ImportText $shootingGalleryBallPath
$shootingGalleryDebrisSource = Read-ImportText $shootingGalleryDebrisPath

if ($mainObjectSource -notmatch
        '(?ms)^group2Mape9ObjectData:\s+obj_Interaction \$30 \$00 \$68 \$88\s+obj_End' -or
    $shootingGalleryNativeSource -notmatch
        '(?ms)^shootingGalleryGame:.*?ld b,\$0a\s+call shootingGallery_initializeGameRounds.*?ld \(hl\),\$78.*?SND_WHISTLE.*?ld \(hl\),\$28.*?SND_BASEBALL.*?ld \(hl\),\$0a.*?ld \(hl\),\$5a.*?cp \$0a' -or
    $shootingGalleryNativeSource -notmatch
        '(?ms)^shootingGallery_getNextTargetLayout:.*?remainingRounds.*?call getRandomNumber.*?wShootingGalleryTileLayoutsToShow' -or
    $shootingGalleryBallSource -notmatch
        '(?ms)^partCode38:.*?and \$0f.*?ld \(hl\),\$64.*?SND_THROW.*?ld \(hl\),\$3c.*?SND_FALLINHOLE.*?SND_CLINK.*?ld \(hl\),\$78' -or
    $shootingGalleryBallSource -notmatch
        '(?ms)^table_6bab:\s+\.db \$d9\s+\.db \$d7\s+\.db \$dc\s+\.db \$d8' -or
    $shootingGalleryBallSource -notmatch
        '(?ms)^func_6bca:.*?ld a,\$04.*?INTERAC_FALLING_ROCK \$04.*?cp \$02.*?INTERAC_FALLING_ROCK \$05.*?objectCreateInteraction.*?dec a\s+ld \(hl\),a\s+jr nz,--' -or
    $shootingGalleryDebrisSource -notmatch
        '(?ms)^fallingRock_subid04:\s*^fallingRock_subid05:.*?fallingRock_initGraphicsAndIncState.*?interactionSetAlwaysUpdateBit.*?ld \(hl\),\$0c\s+jr fallingRock_initDiagonalAngle' -or
    $shootingGalleryHelperSource -notmatch
        '(?ms)^shootingGallery_equipSword:.*?wInventoryA.*?ITEM_SWORD.*?shootingGallery_changeEquips' -or
    $shootingGalleryHelperSource -notmatch
        '(?ms)^shootingGallery_initLinkPosition:\s+ld a,\$00\s+ldbc \$60,\$50.*?^shootingGallery_initLinkPositionAfterGame:\s+ld a,\$01\s+ldbc \$68,\$68' -or
    $shootingGalleryHelperSource -notmatch
        '(?ms)^@positions:\s+\.db \$e0 \$e1.*?\.db \$c6 \$c6') {
    throw 'Room 2:e9 shooting-gallery actor, controller, ball, or setup behavior changed.'
}

$shootingGalleryMainOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'setcollisionradii', 'makeabuttonsensitive', 'checkabutton',
    'disableinput', 'showtext', 'wait', 'jumpiftextoptioneq',
    'enableinput', 'writeobjectbyte', 'scriptjump', 'asm15',
    'jumpifmemoryset', 'checkpalettefadedone', 'setmusic',
    'enableallobjects', 'scriptend')) {
    [void]$shootingGalleryMainOpcodes.Add($opcode)
}
$shootingGalleryCleanupOpcodes =
    [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'disableinput', 'wait', 'asm15', 'checkpalettefadedone',
    'resetmusic', 'jumpifmemoryset', 'jumpifitemobtained',
    'jumpifglobalflagset', 'scriptjump', 'showtext', 'giveitem',
    'scriptend')) {
    [void]$shootingGalleryCleanupOpcodes.Add($opcode)
}
$shootingGalleryMainCommands = Read-AssemblyCutsceneCommands `
    $shootingGalleryScriptPath 'shootingGalleryScript_humanNpc' `
    $shootingGalleryMainOpcodes 'shootingGalleryScript_goronNpc'
$shootingGalleryCleanupCommands = Read-AssemblyCutsceneCommands `
    $shootingGalleryHelperPath 'shootingGalleryScript_humanNpc_gameDone' `
    $shootingGalleryCleanupOpcodes
if ($shootingGalleryMainCommands.Count -ne 48 -or
    $shootingGalleryCleanupCommands.Count -ne 55) {
    throw "Expected 48 shooting-gallery main and 55 cleanup commands, got " +
        "$($shootingGalleryMainCommands.Count) and " +
        "$($shootingGalleryCleanupCommands.Count)."
}

function Convert-ShootingGalleryCommandRows(
    [Collections.Generic.List[object]]$commands,
    [string]$kind
) {
    $targets = @{}
    foreach ($command in $commands) {
        if (-not $targets.ContainsKey($command.Label)) {
            $targets[$command.Label] = $command.Index
        }
    }
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add(
        "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
    foreach ($command in $commands) {
        $opcode = $command.Opcode
        $actor = ''
        $arg0 = ''
        $arg1 = ''
        $payload = ''
        switch ($command.Opcode) {
            'setcollisionradii' {
                if ($command.Operands -notmatch
                    '^\$(?<y>[0-9a-f]{2}),\s*\$(?<x>[0-9a-f]{2})$') {
                    throw "Malformed shooting-gallery collision radii at line $($command.Line)."
                }
                $actor = 'GalleryKeeper'
                $arg0 = $Matches['y']
                $arg1 = $Matches['x']
            }
            'makeabuttonsensitive' { $actor = 'GalleryKeeper' }
            'checkabutton' { $actor = 'GalleryKeeper' }
            'showtext' {
                if ($command.Operands -notmatch '^TX_(?<id>[0-9a-f]{4})$') {
                    throw "Malformed shooting-gallery text at line $($command.Line)."
                }
                $textId = [Convert]::ToInt32($Matches['id'], 16)
                if (-not $allTexts.ContainsKey($textId)) {
                    throw "Missing shooting-gallery text TX_$($Matches['id'])."
                }
                $arg0 = $Matches['id']
                $payload = $allTexts[$textId]
            }
            'wait' {
                if ($command.Operands -notmatch '^(?<frames>[0-9]+)$') {
                    throw "Malformed shooting-gallery wait at line $($command.Line)."
                }
                $arg0 = $Matches['frames']
            }
            'jumpiftextoptioneq' {
                if ($command.Operands -notmatch
                    '^\$(?<value>[0-9a-f]{2}),\s*(?<target>@[A-Za-z0-9_]+)$' -or
                    -not $targets.ContainsKey($Matches['target'])) {
                    throw "Malformed shooting-gallery text branch at line $($command.Line)."
                }
                $arg0 = $Matches['value']
                $arg1 = $targets[$Matches['target']].ToString()
            }
            'writeobjectbyte' {
                if ($command.Operands -notmatch
                    '^Interaction\.var(?<address>[0-9a-f]{2}),\s*\$(?<value>[0-9a-f]{2})$') {
                    throw "Malformed shooting-gallery object write at line $($command.Line)."
                }
                $actor = 'GalleryKeeper'
                $arg0 = $Matches['address']
                $arg1 = $Matches['value']
            }
            'scriptjump' {
                if (-not $targets.ContainsKey($command.Operands)) {
                    throw "Unknown shooting-gallery branch '$($command.Operands)'."
                }
                $arg0 = $targets[$command.Operands].ToString()
            }
            'jumpifmemoryset' {
                if ($command.Operands -notmatch
                    '^wcddb,\s*\$80,\s*(?<target>@[A-Za-z0-9_]+)$' -or
                    -not $targets.ContainsKey($Matches['target'])) {
                    throw "Malformed shooting-gallery flags branch at line $($command.Line)."
                }
                $opcode = 'jumpifmemoryeq'
                $arg0 = '01'
                $arg1 = $targets[$Matches['target']].ToString()
                $payload = 'Condition'
            }
            'jumpifitemobtained' {
                if ($command.Operands -notmatch
                    '^TREASURE_FLUTE,\s*(?<target>@[A-Za-z0-9_]+)$' -or
                    -not $targets.ContainsKey($Matches['target'])) {
                    throw "Malformed shooting-gallery Flute branch at line $($command.Line)."
                }
                $opcode = 'jumpifmemoryeq'
                $arg0 = '01'
                $arg1 = $targets[$Matches['target']].ToString()
                $payload = 'HasFlute'
            }
            'jumpifglobalflagset' {
                if ($command.Operands -notmatch
                    '^GLOBALFLAG_CAN_BUY_FLUTE,\s*(?<target>@[A-Za-z0-9_]+)$' -or
                    -not $targets.ContainsKey($Matches['target'])) {
                    throw "Malformed shooting-gallery global branch at line $($command.Line)."
                }
                $opcode = 'jumpifmemoryeq'
                $arg0 = '01'
                $arg1 = $targets[$Matches['target']].ToString()
                $payload = 'CanBuyFlute'
            }
            'checkpalettefadedone' {
                $opcode = 'gate'
                $payload = 'PaletteFade'
            }
            'setmusic' {
                if ($command.Operands -ne 'MUS_MINIGAME') {
                    throw "Unexpected shooting-gallery music '$($command.Operands)'."
                }
                $arg0 = '02'
            }
            'enableallobjects' {
                $opcode = 'native'
                $payload = 'EnableAllObjects'
            }
            'resetmusic' {
                $opcode = 'native'
                $payload = 'ResetMusic'
            }
            'giveitem' {
                if ($command.Operands -notmatch
                    '^TREASURE_(?<name>FLUTE|GASHA_SEED),\s*\$(?<parameter>[0-9a-f]{2})$') {
                    throw "Unexpected shooting-gallery reward '$($command.Operands)'."
                }
                $treasureName = "TREASURE_$($Matches['name'])"
                if (-not $treasureIds.ContainsKey($treasureName)) {
                    throw "Missing shooting-gallery treasure constant $treasureName."
                }
                $arg0 = $treasureIds[$treasureName].ToString('x2')
                $arg1 = $Matches['parameter']
            }
            'asm15' {
                $handler = switch -Regex ($command.Operands) {
                    '^scriptHelp\.shootingGallery_checkLinkHasRupees,\s*RUPEEVAL_10$' {
                        'CheckRupees10'; break
                    }
                    '^removeRupeeValue,\s*RUPEEVAL_10$' {
                        'RemoveRupees10'; break
                    }
                    '^fadeoutToWhite$' { 'BeginFadeOutWhite'; break }
                    '^fadeinFromWhite$' { 'BeginFadeInWhite'; break }
                    '^scriptHelp\.shootingGallery_equipSword$' {
                        'EquipSword'; break
                    }
                    '^clearAllItemsAndPutLinkOnGround$' {
                        'ClearItems'; break
                    }
                    '^scriptHelp\.shootingGallery_initLinkPosition$' {
                        'InitLinkForGame'; break
                    }
                    '^scriptHelp\.shootingGallery_setEntranceTiles,\s*\$02$' {
                        'RemoveEntrance'; break
                    }
                    '^scriptHelp\.shootingGallery_beginGame$' {
                        'SpawnGame'; break
                    }
                    '^shootingGallery_restoreEquips$' {
                        'RestoreEquips'; break
                    }
                    '^shootingGallery_setEntranceTiles,\s*\$00$' {
                        'RestoreEntrance'; break
                    }
                    '^shootingGallery_removeAllTargets$' {
                        'RemoveTargets'; break
                    }
                    '^shootingGallery_initLinkPositionAfterGame$' {
                        'InitLinkAfterGame'; break
                    }
                    '^shootingGallery_checkIsNotLinkedGame$' {
                        'CheckNotLinked'; break
                    }
                    '^shootingGallery_cpScore,\s*\$0(?<score>[0-3])$' {
                        "CheckScore$($Matches['score'])"; break
                    }
                    '^shootingGallery_giveRandomRingToLink$' {
                        'GiveRandomRing'; break
                    }
                    '^giveRupees,\s*RUPEEVAL_30$' {
                        'GiveThirtyRupees'; break
                    }
                    '^shootingGallery_giveOneHeart$' {
                        'GiveOneHeart'; break
                    }
                    default {
                        throw "Unsupported $kind shooting-gallery asm15 " +
                            "'$($command.Operands)' at line $($command.Line)."
                    }
                }
                $opcode = 'native'
                $payload = $handler
            }
        }
        $rows.Add((New-CutsceneCommandRow `
            $command.Script $command.Index $command.Label $command.Line `
            $opcode $actor $arg0 $arg1 $payload))
    }
    return $rows
}

$shootingGalleryMainRows = Convert-ShootingGalleryCommandRows `
    $shootingGalleryMainCommands 'main'
$shootingGalleryCleanupRows = Convert-ShootingGalleryCommandRows `
    $shootingGalleryCleanupCommands 'cleanup'
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_main.tsv'),
    $shootingGalleryMainRows)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_cleanup.tsv'),
    $shootingGalleryCleanupRows)

$shootingGalleryRetryIndex = (
    $shootingGalleryMainCommands |
        Where-Object Label -eq '@tryAgain' |
        Select-Object -First 1).Index
if ($shootingGalleryRetryIndex -ne 12) {
    throw "shootingGalleryScript_humanNpc retry entry moved from command 12."
}

$shootingGalleryPositionsMatch = [regex]::Match(
    $shootingGalleryNativeSource,
    '(?ms)^shootingGallery_targetPositions_lynna:\s*(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)')
$shootingGalleryLayoutsMatch = [regex]::Match(
    $shootingGalleryNativeSource,
    '(?ms)^shootingGallery_targetTiles_lynna:\s*(?<body>.*?)(?=^shootingGallery_targetTiles_goron:)')
$shootingGalleryPositions = @(
    [regex]::Matches(
        $shootingGalleryPositionsMatch.Groups['body'].Value, '\$([0-9a-f]{2})') |
        ForEach-Object { [Convert]::ToInt32($_.Groups[1].Value, 16) })
$shootingGalleryLayoutTiles = @(
    [regex]::Matches(
        $shootingGalleryLayoutsMatch.Groups['body'].Value, '\$([0-9a-f]{2})') |
        ForEach-Object { [Convert]::ToInt32($_.Groups[1].Value, 16) })
if ($shootingGalleryPositions.Count -ne 10 -or
    $shootingGalleryLayoutTiles.Count -ne 100) {
    throw 'Lynna shooting-gallery target position or layout table changed size.'
}
$shootingGalleryTargetRows = [Collections.Generic.List[string]]::new()
$shootingGalleryTargetRows.Add("# index`tpacked-position`tsource")
for ($index = 0; $index -lt 10; $index++) {
    $shootingGalleryTargetRows.Add(
        "$index`t$($shootingGalleryPositions[$index].ToString('x2'))`t" +
        'shootingGallery.s:shootingGallery_targetPositions_lynna')
}
$shootingGalleryLayoutRows = [Collections.Generic.List[string]]::new()
$shootingGalleryLayoutRows.Add(
    "# index`ttile0`ttile1`ttile2`ttile3`ttile4`ttile5`ttile6`ttile7`ttile8`ttile9`tsource")
for ($layout = 0; $layout -lt 10; $layout++) {
    $tiles = for ($index = 0; $index -lt 10; $index++) {
        $shootingGalleryLayoutTiles[$layout * 10 + $index].ToString('x2')
    }
    $shootingGalleryLayoutRows.Add(
        "$layout`t$($tiles -join "`t")`t" +
        'shootingGallery.s:shootingGallery_targetTiles_lynna')
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_targets.tsv'),
    $shootingGalleryTargetRows)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_layouts.tsv'),
    $shootingGalleryLayoutRows)

$shootingGalleryScoreMatch = [regex]::Match(
    $shootingGalleryNativeSource,
    '(?ms)^@scores:\s*(?<body>(?:\s*\.dw \$[0-9a-f]{4}[^\r\n]*\r?\n){21})')
$shootingGalleryBcdScores = @(
    [regex]::Matches(
        $shootingGalleryScoreMatch.Groups['body'].Value, '\.dw \$(?<score>[0-9a-f]{4})') |
        ForEach-Object { [Convert]::ToInt32($_.Groups['score'].Value, 16) })
if ($shootingGalleryBcdScores.Count -ne 21) {
    throw 'Shooting-gallery score table no longer contains 21 entries.'
}
function Convert-ShootingGalleryBcd([int]$value) {
    $subtract = ($value -band 1) -ne 0
    if ($subtract) { $value = $value -band 0xfffe }
    $decimal = (($value -shr 12) -band 0x0f) * 1000 +
        (($value -shr 8) -band 0x0f) * 100 +
        (($value -shr 4) -band 0x0f) * 10 +
        ($value -band 0x0f)
    if ($subtract) { return -$decimal }
    return $decimal
}

$shootingGalleryHitTable = [regex]::Match(
    $shootingGalleryNativeSource,
    '(?ms)^shootingGalleryHitScriptTable:(?<body>.*?)(?=^;;|\z)')
$shootingGalleryHitLabels = @(
    [regex]::Matches(
        $shootingGalleryHitTable.Groups['body'].Value,
        '\.dw mainScripts\.(?<label>[A-Za-z0-9_]+)') |
        ForEach-Object { $_.Groups['label'].Value })
if ($shootingGalleryHitLabels.Count -ne 22) {
    throw 'shootingGalleryHitScriptTable no longer contains 22 scripts.'
}
$shootingGalleryResultRows = [Collections.Generic.List[string]]::new()
$shootingGalleryResultRows.Add(
    "# index`tscore-delta`ttext-id`tsource-line`tutf8-base64`tsource")
for ($index = 0; $index -lt $shootingGalleryHitLabels.Count; $index++) {
    $label = $shootingGalleryHitLabels[$index]
    $textMatch = [regex]::Match(
        $shootingGalleryScriptSource,
        "(?m)^$([regex]::Escape($label)):\s*\r?\n\s*showtext TX_(?<id>[0-9a-f]{4})")
    if (-not $textMatch.Success) {
        throw "Could not resolve shooting-gallery result script $label."
    }
    $textId = [Convert]::ToInt32($textMatch.Groups['id'].Value, 16)
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Missing shooting-gallery result text TX_$($textMatch.Groups['id'].Value)."
    }
    $sourceLine = [regex]::Matches(
        $shootingGalleryScriptSource.Substring(0, $textMatch.Index), "`n").Count + 2
    $scoreDelta = if ($index -lt 20) {
        Convert-ShootingGalleryBcd $shootingGalleryBcdScores[$index]
    } elseif ($index -eq 20) {
        0
    } else {
        Convert-ShootingGalleryBcd $shootingGalleryBcdScores[20]
    }
    $shootingGalleryResultRows.Add(
        "$index`t$scoreDelta`t$($textId.ToString('x4'))`t$sourceLine`t" +
        "$(ConvertTo-CutsceneCommandPayload $allTexts[$textId])`t" +
        "scripts.s:$label")
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_results.tsv'),
    $shootingGalleryResultRows)

$shootingGalleryPrintBody = [regex]::Match(
    $shootingGalleryScriptSource,
    '(?ms)^shootingGallery_printTotalPoints:(?<body>.*?)(?=^shootingGalleryScript_humanNpc_gameDone:)')
if (-not $shootingGalleryPrintBody.Success) {
    throw 'Could not locate shootingGallery_printTotalPoints.'
}
$shootingGalleryPrintStart = $shootingGalleryPrintBody.Groups['body'].Index
$shootingGalleryPrintEnd = $shootingGalleryPrintStart +
    $shootingGalleryPrintBody.Groups['body'].Length
$shootingGalleryPrintRows = @(
    "# wait-line`tbranch-line`tongoing-text-line`tongoing-enable-line`tongoing-end-line`tfinal-text-line`tfinal-enable-line`tfinal-end-line`tongoing-text-id`tfinal-text-id`tongoing-utf8-base64`tfinal-utf8-base64",
    (@(
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*wait 15\s*$' 'shootingGallery_printTotalPoints'),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*jumpifobjectbyteeq Interaction\.var3f, 10, @gameDone.*$' `
            'shootingGallery_printTotalPoints'),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*showtext TX_0813\s*$' 'shootingGallery_printTotalPoints'),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*enableallobjects\s*$' 'shootingGallery_printTotalPoints' 0),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*scriptend\s*$' 'shootingGallery_printTotalPoints' 0),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*showtext TX_0814\s*$' 'shootingGallery_printTotalPoints'),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*enableallobjects\s*$' 'shootingGallery_printTotalPoints' 1),
        (Find-CutsceneCommandSourceLine $shootingGalleryScriptSource `
            $shootingGalleryPrintStart $shootingGalleryPrintEnd `
            '(?m)^\s*scriptend\s*$' 'shootingGallery_printTotalPoints' 1),
        '0813',
        '0814',
        (ConvertTo-CutsceneCommandPayload $allTexts[0x0813]),
        (ConvertTo-CutsceneCommandPayload $allTexts[0x0814])
    ) -join "`t")
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_result_script.tsv'),
    $shootingGalleryPrintRows)

$shootingGalleryGraphic = $interactionGraphics['149:0']
$shootingGalleryBallAnimation = Resolve-NpcAnimation 0x95 0
if ($null -eq $shootingGalleryGraphic -or
    -not $gfxNames.ContainsKey($shootingGalleryGraphic.Gfx) -or
    -not $shootingGalleryBallAnimation) {
    throw 'Could not resolve PART_BALL-compatible INTERAC_BALL visual data.'
}
$shootingGalleryBallSprite = $gfxNames[$shootingGalleryGraphic.Gfx]
$shootingGalleryDebrisBlueGraphic = $interactionGraphics['146:4']
$shootingGalleryDebrisRedGraphic = $interactionGraphics['146:5']
$shootingGalleryDebrisAnimation = Resolve-NpcAnimation 0x92 1
if ($null -eq $shootingGalleryDebrisBlueGraphic -or
    $null -eq $shootingGalleryDebrisRedGraphic -or
    $shootingGalleryDebrisBlueGraphic.Gfx -ne 0 -or
    $shootingGalleryDebrisRedGraphic.Gfx -ne 0 -or
    $shootingGalleryDebrisBlueGraphic.TileBase -ne 2 -or
    $shootingGalleryDebrisRedGraphic.TileBase -ne 2 -or
    $shootingGalleryDebrisBlueGraphic.Palette -ne 1 -or
    $shootingGalleryDebrisRedGraphic.Palette -ne 2 -or
    $shootingGalleryDebrisBlueGraphic.DefaultAnimation -ne 1 -or
    $shootingGalleryDebrisRedGraphic.DefaultAnimation -ne 1 -or
    -not $shootingGalleryDebrisAnimation) {
    throw 'Could not resolve shooting-gallery $92:$04/$05 target debris.'
}
$shootingGalleryDebrisRows = @(
    "# sprite`ttile-base`tblue-palette`tred-palette`tanimation`tcount`tlifetime`tspeed`tangle0`tangle1`tangle2`tangle3`tsource",
    "spr_common_sprites`t2`t1`t2`t$shootingGalleryDebrisAnimation`t4`t12`t40`t04`t0c`t14`t1c`tball.s:func_6bca;fallingRock.s:fallingRock_subid04/subid05"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_debris.tsv'),
    $shootingGalleryDebrisRows)

$shootingGalleryEventRows = @(
    "# group`troom`tid`tsubid`tcost`trounds`tretry-command`tcontroller-y`tcontroller-x`tinitial-delay`tpitch-delay`tpuff-delay`tlayout-delay`tbetween-round-delay`tentrance0`tentrance1`topen0`topen1`tclosed0`tclosed1`tfloor`ttarget-blue`ttarget-fairy`ttarget-red`ttarget-imp`tball-fast`tball-slow`tball-reflected`tball-angle`tball-radius-y`tball-radius-x`tball-sprite`tball-tile-base`tball-palette`tball-animation`tfade-frames`tminigame-music`twhistle-sound`tbaseball-sound`tthrow-sound`tslow-sound`tclink-sound`tswitch-sound`terror-sound`tstrike-sound`tpoof-sound`tcan-buy-flute-flag`tflute-score`tring-score`tgasha-score`trupee-score`theart-score`tflute-object`tflute-object-parameter`tgasha-object`tgasha-object-parameter`tsource",
    "2`te9`t30`t00`t10`t10`t$shootingGalleryRetryIndex`t2a`t50`t120`t40`t10`t90`t20`t74`t75`te0`te1`tc6`tc6`ta0`td9`td7`tdc`td8`t64`t3c`t78`t10`t02`t02`t$shootingGalleryBallSprite`t$($shootingGalleryGraphic.TileBase)`t$($shootingGalleryGraphic.Palette)`t$shootingGalleryBallAnimation`t32`t02`tcc`t99`t51`t59`t50`t7e`t5a`ta6`t98`t1d`t50`t350`t250`t150`t50`tTREASURE_OBJECT_FLUTE_00`t0b`tTREASURE_OBJECT_GASHA_SEED_00`t01`tmainData.s:group2Mape9ObjectData;shootingGallery.s;ball.s;fallingRock.s;scripts.s;scriptHelper.s"
)
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_event.tsv'),
    $shootingGalleryEventRows)

$shootingGalleryRingMatch = [regex]::Match(
    $shootingGalleryHelperSource,
    '(?ms)^shootingGallery_giveRandomRingToLink:.*?^@ringList:\s*(?<body>(?:\s*\.db [^\r\n]+\r?\n)+)')
$shootingGalleryRingNames = @(
    [regex]::Matches(
        $shootingGalleryRingMatch.Groups['body'].Value,
        '\b(?<name>[A-Z][A-Z0-9_]+_RING(?:_L2)?)\b') |
        ForEach-Object { $_.Groups['name'].Value })
$ringConstantsSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\rings.s')
$shootingGalleryRingValues = @{}
foreach ($match in [regex]::Matches(
    $ringConstantsSource,
    '(?m)^\s*(?<name>[A-Z][A-Z0-9_]+)\s+db\s+;\s+\$(?<value>[0-9a-f]{2})')) {
    $shootingGalleryRingValues[$match.Groups['name'].Value] =
        [Convert]::ToInt32($match.Groups['value'].Value, 16)
}
if ($shootingGalleryRingNames.Count -ne 16) {
    throw 'Shooting-gallery random ring list no longer contains 16 entries.'
}
$shootingGalleryRingRows = [Collections.Generic.List[string]]::new()
$shootingGalleryRingRows.Add("# index`tring`tsource")
for ($index = 0; $index -lt 16; $index++) {
    $name = $shootingGalleryRingNames[$index]
    if (-not $shootingGalleryRingValues.ContainsKey($name)) {
        throw "Could not resolve shooting-gallery ring constant $name."
    }
    $shootingGalleryRingRows.Add(
        "$index`t$($shootingGalleryRingValues[$name].ToString('x2'))`t" +
        'scriptHelper.s:shootingGallery_giveRandomRingToLink@ringList')
}
Write-GeneratedTable(
    (Join-Path $destination 'cutscenes\shooting_gallery_rings.tsv'),
    $shootingGalleryRingRows)

# Every normalized command row emitted above must conform to the same schema
# that runtime startup consumes.
Test-GeneratedCutsceneCommandStreams $destination $cutsceneCommandSchemas
