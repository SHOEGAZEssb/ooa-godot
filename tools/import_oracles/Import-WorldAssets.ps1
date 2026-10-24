# Shared background SCX waves: retain the byte coefficients, rather than
# approximating the source's quantized sine with floating-point trigonometry.
$screenWaveRows = [Collections.Generic.List[string]]::new()
$screenWaveRows.Add("# order`tcoefficient`tsource")
$readingScreenWave = $false
$screenWaveOrder = 0
foreach ($node in Read-AssemblyLabelNodes (Join-Path $Disassembly 'code/bank1.s') 'initWaveScrollValues_body') {
    if ($node.Kind -eq 'Label') {
        if ($readingScreenWave) { break }
        $readingScreenWave = $node.Name -eq '@sineWave'
        continue
    }
    if ($readingScreenWave -and $node.Kind -eq 'Data' -and $node.Name -eq '.db') {
        foreach ($operand in $node.Operands) {
            $coefficient = Convert-AssemblyInteger $operand
            $screenWaveRows.Add("$screenWaveOrder`t$coefficient`tcode/bank1.s:initWaveScrollValues_body@sineWave")
            $screenWaveOrder++
        }
    }
}
if ($screenWaveOrder -ne 32) { throw 'initWaveScrollValues_body@sineWave: expected 32 byte coefficients.' }
Write-GeneratedTable((Join-Path $destination 'metadata/screen_wave_sine.tsv'), $screenWaveRows)

# Preserve the complete Ages branch of constants/common/globalFlags.s for the
# editable debug flag menu. Within WLA-DX enums, "db" advances the value while
# ".db" is used here for range aliases, so only the former emits a flag row.
$globalFlagRows = [Collections.Generic.List[string]]::new()
$globalFlagValues = @{}
$globalFlagValue = 0
$inGlobalFlagEnum = $false
$includeGlobalFlagBranch = $true
$globalFlagBranch = ''
foreach ($line in Read-ImportLines (Join-Path $Disassembly 'constants\common\globalFlags.s')) {
    if ($line -match '^\s*\.ENUM\s+\$0') {
        $inGlobalFlagEnum = $true
        continue
    }
    if (-not $inGlobalFlagEnum) { continue }
    if ($line -match '^\s*\.ENDE') { break }
    if ($line -match '^\s*\.ifdef\s+ROM_(AGES|SEASONS)') {
        $globalFlagBranch = $Matches[1]
        $includeGlobalFlagBranch = $globalFlagBranch -eq 'AGES'
        continue
    }
    if ($line -match '^\s*\.else') {
        $includeGlobalFlagBranch = $globalFlagBranch -eq 'SEASONS'
        continue
    }
    if ($line -match '^\s*\.endif') {
        $globalFlagBranch = ''
        $includeGlobalFlagBranch = $true
        continue
    }
    if ($includeGlobalFlagBranch -and
        $line -match '^\s*(GLOBALFLAG_[A-Za-z0-9_]+)\s+db(?:\s|;)') {
        $globalFlagRows.Add("$($globalFlagValue.ToString('x2'))`t$($Matches[1])")
        $globalFlagValues[$Matches[1]] = $globalFlagValue
        $globalFlagValue++
    }
}
if ($globalFlagRows.Count -ne 0x80) {
    throw "Expected 128 Ages global flags, parsed $($globalFlagRows.Count)."
}
$globalFlagPath = Join-Path $destination 'metadata\global_flags.tsv'
Write-GeneratedTable($globalFlagPath, $globalFlagRows)

# Pollution replacement is the first substitution pass, before position-specific
# writes. The montage's func_782a uses $3a underwater; ordinary reload uses $3b.
$pollutionPath = Join-Path $Disassembly 'code\ages\tileSubstitutions.s'
$pollutionLabel = 'replacePollutionWithWaterIfPollutionFixed'
$pollutionInstructions = @(Read-AssemblyInstructions $pollutionPath $pollutionLabel)
$pollutionExpected = @('ld a,GLOBALFLAG_WATER_POLLUTION_FIXED', 'call checkGlobalFlag',
    'ret z', 'ld a,(wTilesetFlags)', 'bit TILESETFLAG_BIT_OUTDOORS,a', 'ret z',
    'ld de,@aboveWaterReplacement', 'and TILESETFLAG_UNDERWATER', 'jr z,+',
    'ld de,@belowWaterReplacement', 'jr replaceTiles')
if ($pollutionInstructions.Count -ne $pollutionExpected.Count) {
    throw "tileSubstitutions.s:${pollutionLabel}: instruction count changed."
}
for ($pollutionIndex = 0; $pollutionIndex -lt $pollutionExpected.Count; $pollutionIndex++) {
    if (($pollutionInstructions[$pollutionIndex].Code -replace '\s', '') -cne
        ($pollutionExpected[$pollutionIndex] -replace '\s', '')) {
        throw "tileSubstitutions.s:${pollutionLabel}:$($pollutionInstructions[$pollutionIndex].Line): " +
            "expected '$($pollutionExpected[$pollutionIndex])', found '$($pollutionInstructions[$pollutionIndex].Code)'."
    }
}
$pollutionCaller = @(Read-AssemblyInstructions $pollutionPath 'applyAllTileSubstitutions')
if ($pollutionCaller.Count -lt 3 -or
    $pollutionCaller[0].Code -notmatch '^call\s+replacePollutionWithWaterIfPollutionFixed$' -or
    $pollutionCaller[1].Code -notmatch '^call\s+applySingleTileChanges$' -or
    $pollutionCaller[2].Code -notmatch '^call\s+applyStandardTileSubstitutions$') {
    throw 'tileSubstitutions.s:applyAllTileSubstitutions pollution/single/standard order changed.'
}
$pollutionValues = @(Read-AssemblyLiteralValues $pollutionPath $pollutionLabel)
if (($pollutionValues -join ',') -ne '252,235,0,59,235,0') {
    throw 'tileSubstitutions.s:replacePollutionWithWaterIfPollutionFixed: expected terminated $fc/$eb and $3b/$eb pairs.'
}
$pollutionRows = @("# underwater-mask`treplacement`toriginal`tsource")
for ($pollutionIndex = 0; $pollutionIndex -lt 2; $pollutionIndex++) {
    $pollutionRows += "$((0x40 * $pollutionIndex).ToString('x2'))" +
        "`t$($pollutionValues[3 * $pollutionIndex].ToString('x2'))" +
        "`t$($pollutionValues[3 * $pollutionIndex + 1].ToString('x2'))" +
        "`ttileSubstitutions.s:$pollutionLabel"
}
Write-GeneratedTable((Join-Path $destination 'metadata\polluted_water_tile_substitutions.tsv'), $pollutionRows)

# applySingleTileChanges is a separate, ordered room-load pass. Most rows test
# a room-flag mask; $f0-$f2 are the three Ages-specific linked/completion
# predicates handled by commonTileSubstitutions.s.
$singleTileChangeSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\singleTileChanges.s')
$commonTileSubstitutionSource = Read-ImportText (
    Join-Path $Disassembly 'code\commonTileSubstitutions.s')
if ($commonTileSubstitutionSource -notmatch
        '(?ms)^applySingleTileChanges:.*?cp \$f0.*?jr z,@unlinkedOnly.*?cp \$f1.*?jr z,@linkedOnly.*?cp \$f2.*?jr z,@finishedGameOnly.*?and c.*?@match:.*?ld \(de\),a' -or
    $commonTileSubstitutionSource -notmatch
        '(?ms)^@unlinkedOnly:.*?call checkIsLinkedGame.*?^@linkedOnly:.*?call checkIsLinkedGame.*?^@finishedGameOnly:.*?GLOBALFLAG_FINISHEDGAME') {
    throw 'applySingleTileChanges linked/completion predicate behavior changed.'
}
$singleTileChangeRows = [Collections.Generic.List[string]]::new()
$singleTileChangeRows.Add("# group`troom`tmask`tposition`ttile`tsource")
$singleTileChangeRecords = [Collections.Generic.List[object]]::new()
for ($group = 0; $group -lt 8; $group++) {
    $label = "singleTileChangeGroup${group}Data"
    $block = [regex]::Match(
        $singleTileChangeSource,
        "(?ms)^${label}:\s*(?<body>.*?)(?=^singleTileChangeGroup[0-7]Data:|\z)")
    if (-not $block.Success) {
        throw "Could not parse $label."
    }
    foreach ($entry in [regex]::Matches(
        $block.Groups['body'].Value,
        '(?m)^\s*\.db\s+\$(?<room>[0-9a-f]{2})\s+\$(?<mask>[0-9a-f]{2})\s+\$(?<position>[0-9a-f]{2})\s+\$(?<tile>[0-9a-f]{2})\s*$')) {
        $room = [Convert]::ToInt32($entry.Groups['room'].Value, 16)
        $mask = [Convert]::ToInt32($entry.Groups['mask'].Value, 16)
        $position = [Convert]::ToInt32($entry.Groups['position'].Value, 16)
        $tile = [Convert]::ToInt32($entry.Groups['tile'].Value, 16)
        if ($mask -eq 0 -or ($mask -gt 0x80 -and $mask -notin 0xf0, 0xf1, 0xf2)) {
            throw "$label room `$$($room.ToString('x2')) has unsupported mask `$$($mask.ToString('x2'))."
        }
        $source = "singleTileChanges.s:$label"
        $singleTileChangeRows.Add(
            "$group`t$($room.ToString('x2'))`t$($mask.ToString('x2'))`t$($position.ToString('x2'))`t$($tile.ToString('x2'))`t$source")
        $singleTileChangeRecords.Add([pscustomobject]@{
            Group = $group
            Room = $room
            Mask = $mask
            Position = $position
            Tile = $tile
            Source = $source
        })
    }
}
if ($singleTileChangeRecords.Count -ne 56) {
    throw "Expected 56 Ages single-tile changes, parsed $($singleTileChangeRecords.Count)."
}
$singleTileChangePath = Join-Path $destination 'metadata\single_tile_changes.tsv'
Write-GeneratedTable(
    $singleTileChangePath,
    $singleTileChangeRows)

# replaceSwitchTiles runs before object parsing, independently of the PART
# and INTERACTION allocators. Preserve every ordered row in the Ages branch.
if ($commonTileSubstitutionSource -notmatch '(?ms)^replaceSwitchTiles:.*?sub NUM_SMALL_GROUPS.*?dec a\s*ret nz.*?ld a,\(wSwitchState\).*?and c.*?ld \(de\),a') {
    throw 'commonTileSubstitutions.s:replaceSwitchTiles group/mask/write flow changed.'
}
$switchReplacementRows = [Collections.Generic.List[string]]::new()
$switchReplacementRows.Add("# group`troom`tmask`tposition`ttile`tsource")
$switchTableGroup = 0
$switchTableEnds = @{}
foreach ($switchNode in Read-AssemblyNodes (Join-Path $Disassembly 'code/commonTileSubstitutions.s')) {
    if ($switchNode.EnclosingGlobalLabel -ne 'replaceSwitchTiles') { continue }
    if ($switchNode.Kind -eq 'Label') {
        if ($switchNode.Name -match '^@group(?<group>[45])SwitchData$') {
            $switchTableGroup = [int]$Matches['group']
            if ($switchTableEnds.ContainsKey($switchTableGroup)) {
                throw "$($switchNode.Path):$($switchNode.Line): repeated active switch table."
            }
            $switchTableEnds[$switchTableGroup] = $false
        }
        continue
    }
    if ($switchTableGroup -eq 0 -or $switchNode.Kind -in @('Blank','Comment')) { continue }
    if ($switchNode.Kind -eq 'Directive' -and $switchNode.Name -in @('.ifdef','.else','.endif')) { continue }
    if ($switchNode.Kind -ne 'Data' -or $switchNode.Name -ne '.db' -or $switchTableEnds[$switchTableGroup]) {
        throw "$($switchNode.Path):$($switchNode.Line): unsupported switch table node '$($switchNode.Code)'."
    }
    $switchBytes = @($switchNode.Operands | ForEach-Object { Convert-AssemblyInteger $_ })
    if ($switchBytes.Count -eq 1 -and $switchBytes[0] -eq 0) {
        $switchTableEnds[$switchTableGroup] = $true
        continue
    }
    if ($switchBytes.Count -ne 4 -or $switchBytes[0] -eq 0 -or $switchBytes[1] -eq 0 -or
        @($switchBytes | Where-Object { $_ -lt 0 -or $_ -gt 0xff }).Count -ne 0) {
        throw "$($switchNode.Path):$($switchNode.Line): expected room/mask/tile/position bytes."
    }
    $switchReplacementRows.Add("$switchTableGroup`t$($switchBytes[0].ToString('x2'))`t$($switchBytes[1].ToString('x2'))`t$($switchBytes[3].ToString('x2'))`t$($switchBytes[2].ToString('x2'))`tcommonTileSubstitutions.s:replaceSwitchTiles.@group${switchTableGroup}SwitchData")
}
if ($switchTableEnds.Count -ne 2 -or -not $switchTableEnds[4] -or -not $switchTableEnds[5]) {
    throw 'commonTileSubstitutions.s: missing terminated Ages switch tables.'
}
if ($switchReplacementRows.Count -ne 13) {
    throw "commonTileSubstitutions.s: expected12 Ages switch replacement rows, parsed$($switchReplacementRows.Count-1)."
}
Write-GeneratedTable((Join-Path $destination 'metadata/switch_tile_replacements.tsv'),$switchReplacementRows)

# Import applyRoomSpecificTileChanges as declarative
# conditions and layout operations. The dispatcher is parsed rather than
# repeating group/room IDs, so shared routines automatically expand to every
# room that calls them. Live switches, save-backed water levels and vine
# positions retain their authoritative runtime/save owners.
$roomTileChangeSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\roomSpecificTileChanges.s')
$tileChangeJumpBlock = [regex]::Match(
    $roomTileChangeSource,
    '(?ms)^\s*rst_jumpTable\s*(?<body>.*?)(?=^roomTileChangerCodeGroupTable:)')
if (-not $tileChangeJumpBlock.Success) {
    throw 'Could not parse the room tile-changer jump table.'
}
$tileChangeCodeLabels = @{}
foreach ($entry in [regex]::Matches(
    $tileChangeJumpBlock.Groups['body'].Value,
    '(?m)^\s*\.dw\s+(?<label>tileReplacement_[A-Za-z0-9]+)\s*;\s*\$(?<code>[0-9a-f]{2})')) {
    $code = [Convert]::ToInt32($entry.Groups['code'].Value, 16)
    $tileChangeCodeLabels[$code] = $entry.Groups['label'].Value
}
if ($tileChangeCodeLabels.Count -ne 56) {
    throw "Expected 56 room tile-changer routines, parsed $($tileChangeCodeLabels.Count)."
}

$tileChangeDispatch = @{}
$tileChangeDispatchCount = 0
for ($group = 0; $group -lt 8; $group++) {
    $groupBlock = [regex]::Match(
        $roomTileChangeSource,
        "(?ms)^roomTileChangerCodeGroup${group}Data:\s*(?<body>.*?)(?=^roomTileChangerCodeGroup|^;;)")
    if (-not $groupBlock.Success) {
        throw "Could not parse room tile-changer group $group data."
    }
    foreach ($entry in [regex]::Matches(
        $groupBlock.Groups['body'].Value,
        '(?m)^\s*\.db\s+\$(?<room>[0-9a-f]{2})\s+\$(?<code>[0-9a-f]{2})')) {
        $code = [Convert]::ToInt32($entry.Groups['code'].Value, 16)
        if (-not $tileChangeCodeLabels.ContainsKey($code)) {
            throw "Room tile-changer group $group references unknown code `$$($code.ToString('x2'))."
        }
        $label = $tileChangeCodeLabels[$code]
        if (-not $tileChangeDispatch.ContainsKey($label)) {
            $tileChangeDispatch[$label] = [Collections.Generic.List[object]]::new()
        }
        $tileChangeDispatch[$label].Add([pscustomobject]@{
            Group = $group
            Room = $entry.Groups['room'].Value.ToLowerInvariant()
        })
        $tileChangeDispatchCount++
    }
}
if ($tileChangeDispatchCount -ne 58) {
    throw "Expected 58 room tile-changer dispatch entries, parsed $tileChangeDispatchCount."
}

$roomTileChangeRows = [Collections.Generic.List[string]]::new()
$roomTileChangeRows.Add("# group`troom`tconditions`toperations`tsource")
$supportedTileChangeLabels = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::Ordinal)
function Add-RoomTileChangeRule(
    [string]$label,
    [string]$conditions,
    [string]$operations) {
    if (-not $tileChangeDispatch.ContainsKey($label)) {
        throw "Room tile-change rule references undispatched routine $label."
    }
    foreach ($location in $tileChangeDispatch[$label]) {
        $roomTileChangeRows.Add(
            "$($location.Group)`t$($location.Room)`t$conditions`t$operations`t$label")
    }
    [void]$supportedTileChangeLabels.Add($label)
}

function Assert-RoomTileInstructions([string]$path, [string]$label, [string[]]$expected) {
    $instructions = @(Read-AssemblyInstructions $path $label)
    if ($instructions.Count -ne $expected.Count) {
        throw "${path}:${label}: expected $($expected.Count) instructions, found $($instructions.Count)."
    }
    for ($index = 0; $index -lt $expected.Count; $index++) {
        if (($instructions[$index].Code -replace '\s', '') -cne
            ($expected[$index] -replace '\s', '')) {
            throw "${path}:${label}:$($instructions[$index].Line) " +
                "expected '$($expected[$index])', found '$($instructions[$index].Code)'."
        }
    }
}

$roomTileSourcePath = Join-Path $Disassembly 'code/ages/roomSpecificTileChanges.s'
Assert-RoomTileInstructions $roomTileSourcePath 'tileReplacement_group4Map4c' @(
    'ld hl,wSwitchState', 'bit 0,(hl)', 'ret nz', 'ld hl,wRoomLayout + $43',
    'ld a,$6a', 'ld (hl),a', 'ld l,$53', 'ld (hl),a', 'ld l,$63', 'ld (hl),a',
    'ld l,$76', 'ld a,$f4', 'jp set4Bytes')
Add-RoomTileChangeRule 'tileReplacement_group4Map4c' 'runtime_mask_eq:cdd3:01:00' `
    'set:43:6a,53:6a,63:6a|fill:76:01:04:f4'
Assert-RoomTileInstructions $roomTileSourcePath 'tileReplacement_group4Map4e' @(
    'ld hl,wSwitchState', 'bit 1,(hl)', 'ret z', 'ld hl,wRoomLayout + $36',
    'ld a,$6d', 'call set4Bytes', 'ld a,$f4', 'ld l,$42', 'ld (hl),a',
    'ld l,$52', 'ld (hl),a', 'ld l,$62', 'ld (hl),a', 'ld l,$4c', 'ld (hl),a',
    'ld l,$5c', 'ld (hl),a', 'ld l,$6c', 'ld (hl),a', 'ret')
Add-RoomTileChangeRule 'tileReplacement_group4Map4e' 'runtime_mask_eq:cdd3:02:02' `
    'fill:36:01:04:6d|set:42:f4,52:f4,62:f4,4c:f4,5c:f4,6c:f4'

# getBlackTowerProgress prioritizes $90/$40 over $ba/$40. Only stage 1
# closes these two tiles, and only in a linked file (copied to wIsLinkedGame).
$roomTileBank0Path = Join-Path $Disassembly 'code/bank0.s'
Assert-RoomTileInstructions $roomTileBank0Path 'checkIsLinkedGame' @(
    'ld a,(wIsLinkedGame)', 'or a', 'ret')
Assert-RoomTileInstructions $roomTileBank0Path 'getBlackTowerProgress' @(
    'push bc', 'ld c,$02', 'ld a,(wPresentRoomFlags+$90)', 'bit ROOMFLAG_BIT_40,a',
    'jr nz,++', 'dec c', 'ld a,(wPresentRoomFlags+$ba)', 'bit ROOMFLAG_BIT_40,a',
    'jr nz,++', 'dec c', 'ld a,c', 'pop bc', 'ret')
Assert-RoomTileInstructions $roomTileSourcePath 'tileReplacement_group0Map76' @(
    'call checkIsLinkedGame', 'ret z', 'call getBlackTowerProgress', 'dec a', 'ret nz',
    'ld hl,wRoomLayout+$54', 'ld a,$a7', 'ldi (hl),a', 'ld (hl),a', 'ret')
Add-RoomTileChangeRule 'tileReplacement_group0Map76' `
    'wram_mask_ne:c612:ff:00,wram_mask_eq:c790:40:00,room_set:0:ba:40' `
    'set:54:a7,55:a7'

$flagD3Crystals = $globalFlagValues['GLOBALFLAG_D3_CRYSTALS'].ToString('x2')
$flagMakuSaved = $globalFlagValues['GLOBALFLAG_MAKU_TREE_SAVED'].ToString('x2')
$flagSymmetryBridge = $globalFlagValues['GLOBALFLAG_SYMMETRY_BRIDGE_BUILT'].ToString('x2')
$flagIntroDone = $globalFlagValues['GLOBALFLAG_INTRO_DONE'].ToString('x2')

# CGB/secret-shop routines, including the original D1 fallthrough bug.
Add-RoomTileChangeRule 'tileReplacement_group1Map58' 'always' 'set:35:de'
Add-RoomTileChangeRule 'tileReplacement_group2Map7e' 'wram_mask_eq:c642:0f:0f' `
    'fill:13:03:06:a0|set:25:f1,27:f1,32:a0'
Add-RoomTileChangeRule 'tileReplacement_group4Map1b' 'current_room_set:80' `
    'set:1a:09,1c:09'
Add-RoomTileChangeRule 'tileReplacement_group4Map1b' `
    'current_room_set:80,wram_mask_eq:c642:0f:0f' `
    'fill:13:03:06:a0|set:25:f1,27:f1,32:a0'

# Current-room flag changes.
Add-RoomTileChangeRule 'tileReplacement_group4Mapc9' 'current_room_set:40' `
    'fill:27:01:04:6d'
if ($roomTileChangeSource -notmatch '(?ms)^tileReplacement_group4Mapc7:\s*ld hl,wSwitchState\s*bit 0,\(hl\)\s*ret nz\s*ld hl,wRoomLayout \+ \$27\s*ld a,\$6d\s*call set4Bytes\s*ld a,\$f4\s*ld l,\$3d\s*ld \(hl\),a\s*ld l,\$4d\s*ld \(hl\),a\s*ld l,\$5d\s*ld \(hl\),a\s*ld l,\$6d\s*ld \(hl\),a\s*ret') {
    throw 'tileReplacement_group4Mapc7: Hero''s Cave switch bit0/bridge layout changed.'
}
Add-RoomTileChangeRule 'tileReplacement_group4Mapc7' 'runtime_mask_eq:cdd3:01:00' `
    'fill:27:01:04:6d|set:3d:f4,4d:f4,5d:f4,6d:f4'
# replaceTiles source pairs are new/old; generated operations are old/new.
foreach ($torchRoom in @('tileReplacement_group4Map59','tileReplacement_group5Map43')) {
    $torchBody = [regex]::Match($roomTileChangeSource,"(?ms)^${torchRoom}:(?<body>.*?)(?=^tileReplacement_|\z)").Groups['body'].Value
    if ($torchBody -notmatch '(?ms)ld de,@(?:replacementTiles|tilesToReplace)\s*jp replaceTiles.*?\.db \$09 \$08[^\r\n]*\s*\.db \$00') {
        throw "${torchRoom}: unsupported persistent unlit-to-lit torch replacement."
    }
}
Add-RoomTileChangeRule 'tileReplacement_group4Map59' 'current_room_set:80' 'replace:08:09'
Add-RoomTileChangeRule 'tileReplacement_group5Map38' 'current_room_set:40' `
    'set:39:6a,49:6a,59:6a,69:6a'
Add-RoomTileChangeRule 'tileReplacement_group5Map25' 'current_room_clear:40' `
    'fill:17:09:04:a6|fill:1b:09:01:b3|fill:16:09:01:b1'
Add-RoomTileChangeRule 'tileReplacement_group5Map43' 'current_room_clear:40' `
    'fill:17:09:04:a7|fill:1b:09:01:b3|fill:16:09:01:b1'
Add-RoomTileChangeRule 'tileReplacement_group5Map43' 'current_room_set:40' 'replace:08:09'
Add-RoomTileChangeRule 'tileReplacement_group5Map95' 'current_room_clear:40' `
    'set:4d:b4,4e:b2|fill:5e:05:01:a7|fill:5d:05:01:b1'
Add-RoomTileChangeRule 'tileReplacement_group5Mapc3' 'current_room_set:40' `
    'set:31:a2,32:a1,33:a2,34:a1,35:a2,41:a1,42:a2,43:a1,44:a2,45:a1,51:a2,52:a1,53:a2,54:a1,55:a2'
Add-RoomTileChangeRule 'tileReplacement_group7Map4a' 'current_room_set:80' `
    'fill:0d:0a:01:18'

# Jabu's water byte drives six dispatch entries through four routines. Read
# their rectangles in source order and preserve the floor comparison rather
# than hard-coding the two upper-floor aliases to a particular water level.
$jabuRoomChangePath = Join-Path $Disassembly 'code\ages\roomSpecificTileChanges.s'
foreach ($jabuSpec in @(
    @{ Label = 'tileReplacement_group5Map4c'; Lower = $true; Stair = $true },
    @{ Label = 'tileReplacement_group5Map4d'; Lower = $true; Stair = $false },
    @{ Label = 'tileReplacement_group5Map5c'; Lower = $false; Stair = $false },
    @{ Label = 'tileReplacement_group5Map5d'; Lower = $false; Stair = $false })) {
    $jabuInstructions = @(Read-AssemblyInstructions $jabuRoomChangePath $jabuSpec.Label)
    $jabuExpected = if ($jabuSpec.Lower) {
        @('ld a,(wJabuWaterLevel)', 'and $07', 'ret z', 'ld hl,@rect') +
            $(if ($jabuSpec.Stair) {
                @('call fillRectInRoomLayout', 'ld l,$57', 'ld (hl),$45', 'ret')
            } else { @('jp fillRectInRoomLayout') })
    } else {
        @('ld a,(wDungeonFloor)', 'ld b,a', 'ld a,(wJabuWaterLevel)',
            'and $07', 'cp b', 'ret nz', 'ld de,@platformRect', 'jp drawRectInRoomLayout')
    }
    if ($jabuInstructions.Count -ne $jabuExpected.Count) {
        throw "roomSpecificTileChanges.s:$($jabuSpec.Label) instruction count changed."
    }
    for ($jabuIndex = 0; $jabuIndex -lt $jabuExpected.Count; $jabuIndex++) {
        if (($jabuInstructions[$jabuIndex].Code -replace '\s', '') -cne
            ($jabuExpected[$jabuIndex] -replace '\s', '')) {
            throw "roomSpecificTileChanges.s:$($jabuSpec.Label):$($jabuInstructions[$jabuIndex].Line) " +
                "expected '$($jabuExpected[$jabuIndex])', found '$($jabuInstructions[$jabuIndex].Code)'."
        }
    }
    $jabuRect = @(Read-AssemblyLiteralValues $jabuRoomChangePath $jabuSpec.Label)
    $jabuRectCount = if ($jabuSpec.Lower) { 4 } else { 28 }
    if ($jabuRect.Count -ne $jabuRectCount -or $jabuRect[1] -ne 5 -or $jabuRect[2] -ne 5) {
        throw "roomSpecificTileChanges.s:$($jabuSpec.Label) expected a complete five-by-five rectangle."
    }
    $jabuOperation = if ($jabuSpec.Lower) {
        'fill:' + (($jabuRect | ForEach-Object { $_.ToString('x2') }) -join ':')
    } else {
        'draw:' + (($jabuRect[0..2] | ForEach-Object { $_.ToString('x2') }) -join ':') + ':' +
            (($jabuRect[3..27] | ForEach-Object { $_.ToString('x2') }) -join ',')
    }
    if ($jabuSpec.Stair) { $jabuOperation += '|set:57:45' }
    $jabuCondition = if ($jabuSpec.Lower) { 'wram_mask_ne:c6e9:07:00' }
        else { 'wram_mask_eq_floor:c6e9:07' }
    Add-RoomTileChangeRule $jabuSpec.Label $jabuCondition $jabuOperation
}

$jabuSubstitutionPath = Join-Path $Disassembly 'code\ages\tileSubstitutions.s'
$jabuSubstitutionLabel = 'replaceJabuTilesIfUnderwater'
$jabuSubstitutionInstructions = @(Read-AssemblyInstructions $jabuSubstitutionPath $jabuSubstitutionLabel)
$jabuSubstitutionExpected = @('ld a,(wDungeonIndex)', 'cp $07', 'ret nz',
    'ld a,(wTilesetFlags)', 'and TILESETFLAG_SIDESCROLL', 'ret nz',
    'ld a,(wDungeonFloor)', 'ld b,a', 'ld a,(wJabuWaterLevel)', 'and $07',
    'cp b', 'ret nz', 'ld de,@data1', 'call replaceTiles', 'ld de,@data2', 'jp replaceTiles')
if ($jabuSubstitutionInstructions.Count -ne $jabuSubstitutionExpected.Count) {
    throw 'tileSubstitutions.s:replaceJabuTilesIfUnderwater instruction count changed.'
}
for ($jabuIndex = 0; $jabuIndex -lt $jabuSubstitutionExpected.Count; $jabuIndex++) {
    if (($jabuSubstitutionInstructions[$jabuIndex].Code -replace '\s', '') -cne
        ($jabuSubstitutionExpected[$jabuIndex] -replace '\s', '')) {
        throw "tileSubstitutions.s:${jabuSubstitutionLabel}:$($jabuSubstitutionInstructions[$jabuIndex].Line) " +
            "expected '$($jabuSubstitutionExpected[$jabuIndex])', found '$($jabuSubstitutionInstructions[$jabuIndex].Code)'."
    }
}
$jabuSubstitutionValues = @(Read-AssemblyLiteralValues $jabuSubstitutionPath $jabuSubstitutionLabel)
if ($jabuSubstitutionValues.Count -ne 20 -or $jabuSubstitutionValues[10] -ne 0 -or $jabuSubstitutionValues[19] -ne 0) {
    throw 'tileSubstitutions.s:replaceJabuTilesIfUnderwater expected five and four terminated replacement pairs.'
}
$jabuSubstitutionRows = [Collections.Generic.List[string]]::new()
$jabuSubstitutionRows.Add("# index`treplacement`toriginal`tsource")
$jabuPair = 0
foreach ($jabuOffset in @(0, 2, 4, 6, 8, 11, 13, 15, 17)) {
    $jabuSubstitutionRows.Add("$jabuPair`t$($jabuSubstitutionValues[$jabuOffset].ToString('x2'))" +
        "`t$($jabuSubstitutionValues[$jabuOffset + 1].ToString('x2'))`ttileSubstitutions.s:$jabuSubstitutionLabel")
    $jabuPair++
}
Write-GeneratedTable((Join-Path $destination 'metadata\jabu_water_tile_substitutions.tsv'), $jabuSubstitutionRows)
Add-RoomTileChangeRule 'tileReplacement_group0Map5c' 'current_room_set:80' `
    'set:34:3a,43:3a,44:3a,45:3a'
Add-RoomTileChangeRule 'tileReplacement_group0Map73' 'current_room_set:80' `
    'set:73:3a,74:10,75:11,76:12,77:3a'
Add-RoomTileChangeRule 'tileReplacement_group0Mapac' 'current_room_clear:80' `
    'set:33:af,34:af,43:af,44:af'
if ($roomTileChangeSource -notmatch '(?ms)^tileReplacement_group0Map54:\s+xor a\s+ld \(wSwitchState\),a\s+call getThisRoomFlags\s+and \$40\s+ret z') {
    throw 'roomSpecificTileChanges.s:tileReplacement_group0Map54 must clear wSwitchState before its flag-$40 gate.'
}
Add-RoomTileChangeRule 'tileReplacement_group0Map54' 'always' 'runtime_set:cdd3:00'
Add-RoomTileChangeRule 'tileReplacement_group0Map54' 'current_room_set:40' `
    'set:43:1d,44:1d,45:1d,53:1e,54:1e,55:1e,68:9e'
Add-RoomTileChangeRule 'tileReplacement_group5Mapc2' 'current_room_set:80' `
    'fill:56:01:04:6d'
Add-RoomTileChangeRule 'tileReplacement_group5Mape3' 'current_room_set:80' `
    'fill:26:01:03:6d'
Add-RoomTileChangeRule 'tileReplacement_group2Map90' 'current_room_set:02' `
    'draw:42:02:06:dd,de,df,ed,ee,ef,b9,ba,bb,bc,bd,be'
Add-RoomTileChangeRule 'tileReplacement_group1Map8c' 'current_room_set:80' `
    'set:04:30,05:32,14:3a,15:3a,34:02,35:3a'
if ($roomTileChangeSource -notmatch '(?ms)^tileReplacement_group2Map9e:\s+xor a\s+ld \(wToggleBlocksState\),a\s+call getThisRoomFlags\s+and \$40\s+ret z') {
    throw 'roomSpecificTileChanges.s:tileReplacement_group2Map9e must clear wToggleBlocksState before its flag-$40 gate.'
}
Add-RoomTileChangeRule 'tileReplacement_group2Map9e' 'always' 'runtime_set:cdd2:00'
Add-RoomTileChangeRule 'tileReplacement_group2Map9e' 'current_room_set:40' `
    'fill:13:01:06:6d'
Add-RoomTileChangeRule 'tileReplacement_group4Mapea' 'current_room_set:40' `
    'fill:33:01:03:a3|fill:39:01:03:a3|fill:43:01:03:b7|fill:49:01:03:b7|fill:53:01:03:88|fill:59:01:03:88'

# Global-flag changes.
Add-RoomTileChangeRule 'tileReplacement_group4Map60' "global_set:$flagD3Crystals" `
    'fill:34:05:07:a0|set:34:1d,3a:1d,74:1d,7a:1d'
Add-RoomTileChangeRule 'tileReplacement_group4Map60' `
    "global_set:$flagD3Crystals,current_room_clear:20" 'set:57:f1'
Add-RoomTileChangeRule 'tileReplacement_group4Map60' `
    "global_set:$flagD3Crystals,current_room_set:20" 'set:57:f0'
Add-RoomTileChangeRule 'tileReplacement_group4Map52' "global_set:$flagD3Crystals" `
    'copy_original:60'
Add-RoomTileChangeRule 'tileReplacement_group0Map38' "global_set:$flagMakuSaved" `
    'fill:73:01:04:f9'
Add-RoomTileChangeRule 'tileReplacement_group1Map38' 'current_room_set:80' `
    'fill:73:01:04:f9'
Add-RoomTileChangeRule 'tileReplacement_group0Map48' "global_set:$flagMakuSaved" `
    'fill:03:01:04:3a'
Add-RoomTileChangeRule 'tileReplacement_group0Map25' "global_set:$flagSymmetryBridge" `
    'set:50:1d,51:1d,52:1d,60:1e,61:1e,62:1e'
Add-RoomTileChangeRule 'tileReplacement_group0Map3a' "global_set:$flagIntroDone" `
    'set:23:ee'

# Flags belonging to another room table.
Add-RoomTileChangeRule 'tileReplacement_group0Map0b' 'room_set:0:0a:40' 'set:43:dd'
Add-RoomTileChangeRule 'tileReplacement_group1Map27' 'room_set:1:15:80' `
    'set:33:3a,43:02'
Add-RoomTileChangeRule 'tileReplacement_group1Map27' 'room_set:1:17:80' `
    'set:34:3a,24:02'
Add-RoomTileChangeRule 'tileReplacement_group1Map27' 'room_set:1:35:80' `
    'set:35:3a,45:02'
Add-RoomTileChangeRule 'tileReplacement_group1Map27' 'room_set:1:37:80' `
    'set:36:3a,26:02'
Add-RoomTileChangeRule 'tileReplacement_group0Mapa5' 'room_set:1:a5:80' `
    'set:22:ee,23:ef'

# Room 0:98 retains its diggable dirt only while Ricky has explained the
# missing gloves, they have not been returned, and treasure $48 is absent.
# The source branches to the same $24/$3a write for each failed predicate.
$rickyGloveTileBlock = [regex]::Match(
    $roomTileChangeSource,
    '(?ms)^tileReplacement_group0Map98:\s+ld a,\(wRickyState\)\s+bit 5,a\s+jr nz,@removeDirt\s+and \$01\s+jr z,@removeDirt\s+ld a,TREASURE_RICKY_GLOVES\s+call checkTreasureObtained\s+ret nc\s+^@removeDirt:\s+ld a,\$3a\s+ld \(wRoomLayout\+\$24\),a\s+ret')
if (-not $rickyGloveTileBlock.Success) {
    throw "Room 0:98 Ricky's Gloves dirt replacement changed."
}
Add-RoomTileChangeRule 'tileReplacement_group0Map98' `
    'wram_mask_eq:c646:20:20' 'set:24:3a'
Add-RoomTileChangeRule 'tileReplacement_group0Map98' `
    'wram_mask_eq:c646:01:00' 'set:24:3a'
Add-RoomTileChangeRule 'tileReplacement_group0Map98' `
    'treasure_set:48' 'set:24:3a'

# Rolling Ridge and Tokay Island's past ENEMY_VINE_SPROUT positions are read while the
# paired present rooms load. A matching packed position grows the full vine in
# both vertical rooms. A mismatch in the lower room leaves tile $8c at the
# live sprout position when the underlying metatile has zero collision.
$vineHelperContract =
    $roomTileChangeSource -match '(?ms)^replaceVineTiles:.*?ld de,@vineReplacements.*?call replaceTiles.*?ld a,\$d6.*?call findTileInRoom.*?ret nz.*?ld a,l.*?add \$10.*?ld l,a.*?ld \(hl\),\$8d' -and
    $roomTileChangeSource -match '(?ms)^@vineReplacements:.*?\.db \$d4 \$05.*?\.db \$d5 \$8e.*?\.db \$d6 \$8f.*?\.db \$00' -and
    $roomTileChangeSource -match '(?ms)^setTileToWitheredVine:.*?ld l,\(hl\).*?call retrieveTileCollisionValue.*?or a\s*ret nz\s*ld \(hl\),\$8c' -and
    $roomTileChangeSource -match '(?ms)^getVinePosition:.*?ld hl,wVinePositions.*?cp c\s*ret'
if (-not $vineHelperContract) {
    throw 'Tokay Island present-vine helper behavior changed.'
}

$tokayVineSpecs = @(
    @{ Label = 'tileReplacement_group0Map2c'; SubId = 0; Target = 0x17;
       Edge = '06:5d,08:60'; Extra = ''; Withered = $true;
       Instructions = @('ld bc,$0017', 'call getVinePosition', 'jp nz,setTileToWitheredVine',
           'ld l,$06', 'ld de,@vineEdgeReplacements', 'jp replaceVineTiles'); Tiles = @(0x5d,0x60,0) },
    @{ Label = 'tileReplacement_group0Map1c'; SubId = 0; Target = 0x17;
       Edge = '66:5b,68:45,76:4d,78:55'; Extra = ''; Withered = $false;
       Instructions = @('ld bc,$0017', 'call getVinePosition', 'ret nz',
           'ld l,$66', 'ld de,@vineEdgeReplacements', 'jp replaceVineTiles'); Tiles = @(0x5b,0x45,0x4d,0x55,0) },
    @{ Label = 'tileReplacement_group0Mapba'; SubId = 2; Target = 0x18;
       Edge = '07:61,09:60'; Extra = '|set:18:8b'; Withered = $true;
       Pattern = '(?ms)^tileReplacement_group0Mapba:\s*ld bc,\$0218\s*call getVinePosition\s*jp nz,setTileToWitheredVine\s*ld l,\$07.*?\.db \$61 \$60\s*\.db \$00' },
    @{ Label = 'tileReplacement_group0Mapaa'; SubId = 2; Target = 0x18;
       Edge = '77:46,79:45'; Extra = ''; Withered = $false;
       Pattern = '(?ms)^tileReplacement_group0Mapaa:\s*ld bc,\$0218\s*call getVinePosition\s*ret nz\s*ld l,\$77.*?\.db \$46 \$45\s*\.db \$00' },
    @{ Label = 'tileReplacement_group0Mapcc'; SubId = 3; Target = 0x11;
       Edge = '00:61,02:5d'; Extra = ''; Withered = $true;
       Pattern = '(?ms)^tileReplacement_group0Mapcc:\s*ld bc,\$0311\s*call getVinePosition\s*jp nz,setTileToWitheredVine\s*ld l,\$00.*?\.db \$61 \$5d\s*\.db \$00' },
    @{ Label = 'tileReplacement_group0Mapbc'; SubId = 3; Target = 0x11;
       Edge = '70:46,72:5c'; Extra = ''; Withered = $false;
       Pattern = '(?ms)^tileReplacement_group0Mapbc:\s*ld bc,\$0311\s*call getVinePosition\s*ret nz\s*ld l,\$70.*?\.db \$46 \$5c\s*\.db \$00' },
    @{ Label = 'tileReplacement_group0Mapda'; SubId = 4; Target = 0x18;
       Edge = '07:61,09:60'; Extra = ''; Withered = $true;
       Pattern = '(?ms)^tileReplacement_group0Mapda:\s*ld bc,\$0418\s*call getVinePosition\s*jp nz,setTileToWitheredVine\s*ld l,\$07.*?\.db \$61 \$60\s*\.db \$00' },
    @{ Label = 'tileReplacement_group0Mapca'; SubId = 4; Target = 0x18;
       Edge = '77:46,79:45'; Extra = ''; Withered = $false;
       Pattern = '(?ms)^tileReplacement_group0Mapca:\s*ld bc,\$0418\s*call getVinePosition\s*ret nz\s*ld l,\$77.*?\.db \$46 \$45\s*\.db \$00' }
)
$vineGrowthOperations =
    'replace:05:d4|replace:8e:d5|replace:8f:d6|set_below_last:d6:8d'
foreach ($spec in $tokayVineSpecs) {
    if ($spec.ContainsKey('Instructions')) {
        Assert-RoomTileInstructions $roomTileSourcePath $spec.Label $spec.Instructions
        $tiles = @(Read-AssemblyLiteralValues $roomTileSourcePath $spec.Label)
        if (($tiles -join ',') -cne ($spec.Tiles -join ',')) {
            throw "${roomTileSourcePath}:$($spec.Label): vine edge replacements changed."
        }
    }
    elseif ($roomTileChangeSource -notmatch $spec.Pattern) {
        throw "Tokay Island present-vine routine $($spec.Label) changed."
    }
    $address = 0xc8f0 + $spec.SubId
    $addressText = $address.ToString('x4')
    $targetText = $spec.Target.ToString('x2')
    Add-RoomTileChangeRule $spec.Label `
        "wram_mask_eq:${addressText}:ff:${targetText}" `
        "set:$($spec.Edge)|${vineGrowthOperations}$($spec.Extra)"
    if ($spec.Withered) {
        Add-RoomTileChangeRule $spec.Label `
            "wram_mask_ne:${addressText}:ff:${targetText}" `
            "set_wram_position_if_clear:${addressText}:8c"
    }
}

# Talus Peaks uses one sprout ($62:$01), selecting either $22 or $27.
# Unlike replaceVineTiles, these routines write only the selected column.
# Verify every instruction and branch through the shared assembly model before
# emitting the existing conditional/set operations; an extra opcode must fail.
$talusVineSpecs = @(
    @{ Label = 'tileReplacement_group0Map61'; Branch = '@vine';
       Miss = 'jp nz,setTileToWitheredVine';
       Left = @('01:56', '02:d5', '03:4d', '11:61', '12:d6', '13:5d', '22:8d');
       Right = @('06:4d', '07:d5', '08:55', '16:5d', '17:d6', '18:60', '27:8d') },
    @{ Label = 'tileReplacement_group0Map51'; Branch = '@vines'; Miss = 'ret nz';
       Left = @('71:46', '72:d4', '73:5c');
       Right = @('76:5b', '77:d4', '78:45') }
)
foreach ($spec in $talusVineSpecs) {
    $expected = [Collections.Generic.List[string]]::new()
    foreach ($instruction in @('ld bc,$0122', 'call getVinePosition',
        "jr z,$($spec.Branch)1", 'ld bc,$0127', 'call getVinePosition', $spec.Miss)) {
        $expected.Add($instruction)
    }
    # The second-position branch falls through; the first jumps past it.
    foreach ($branch in @(@{ Id = 2; Writes = $spec.Right }, @{ Id = 1; Writes = $spec.Left })) {
        $expected.Add("$($spec.Branch)$($branch.Id):")
        $previousPosition = -1
        foreach ($write in $branch.Writes) {
            $positionText, $tileText = $write.Split(':')
            $position = [Convert]::ToInt32($positionText, 16)
            if ($previousPosition -lt 0) {
                $expected.Add('ld hl,wRoomLayout + $' + $positionText)
            }
            elseif ($position -eq $previousPosition + 1) {
                $expected.Add('inc l')
            }
            else {
                $expected.Add('ld l,$' + $positionText)
            }
            $expected.Add('ld (hl),$' + $tileText)
            $previousPosition = $position
        }
        $expected.Add('ret')
    }
    $nodes = @(Read-AssemblyLabelNodes `
        (Join-Path $Disassembly 'code\ages\roomSpecificTileChanges.s') $spec.Label |
        Where-Object { $_.Kind -notin 'Blank', 'Comment' })
    if ($nodes.Count -ne $expected.Count) {
        throw "roomSpecificTileChanges.s:$($spec.Label) instruction/label count changed."
    }
    for ($index = 0; $index -lt $nodes.Count; $index++) {
        if (($nodes[$index].Code -replace '\s', '') -cne
            ($expected[$index] -replace '\s', '')) {
            throw "roomSpecificTileChanges.s:$($spec.Label):$($nodes[$index].Line) " +
                "expected '$($expected[$index])', found '$($nodes[$index].Code)'."
        }
    }
    Add-RoomTileChangeRule $spec.Label 'wram_mask_eq:c8f1:ff:22' `
        ('set:' + ($spec.Left -join ','))
    Add-RoomTileChangeRule $spec.Label 'wram_mask_eq:c8f1:ff:27' `
        ('set:' + ($spec.Right -join ','))
    if ($spec.Miss -eq 'jp nz,setTileToWitheredVine') {
        Add-RoomTileChangeRule $spec.Label `
            'wram_mask_ne:c8f1:ff:22,wram_mask_ne:c8f1:ff:27' `
            'set_wram_position_if_clear:c8f1:8c'
    }
}

# Essence-backed changes.
Add-RoomTileChangeRule 'tileReplacement_group5Mapc3' 'essence_set:4' `
    'set:06:b0,07:b0,08:b0,09:b0,16:ef,19:ef,26:ef,29:ef,36:b4,37:b2,38:b2,39:b2'
Add-RoomTileChangeRule 'tileReplacement_group5Mapb9' 'essence_set:3' `
    'set:41:a1,42:a1,43:a1,44:ef,45:a1,51:a2,52:ef,53:a2,54:a2,55:a2'
Add-RoomTileChangeRule 'tileReplacement_group0Mape0' 'essence_set:4' 'set:46:dd'
Add-RoomTileChangeRule 'tileReplacement_group0Mape1' 'essence_set:0' 'set:26:dd'
Add-RoomTileChangeRule 'tileReplacement_group0Mape1' 'essence_set:1' 'set:53:dd'
Add-RoomTileChangeRule 'tileReplacement_group0Mape2' 'essence_set:2' 'set:54:dd'

# The Goron's wall is inserted before interaction $66:$07 initializes. Item
# collection takes precedence over both progress flags and the session-local
# seed-tree refill bit. Preserve the source's five-column, four-row insertion.
$goronWallLabel = 'tileReplacement_group2Mapf7'
$goronWallNodes = @(Read-AssemblyLabelNodes `
    (Join-Path $Disassembly 'code\ages\roomSpecificTileChanges.s') $goronWallLabel |
    Where-Object { $_.Kind -notin 'Blank', 'Comment' })
$goronWallExpected = @'
call getThisRoomFlags
bit ROOMFLAG_BIT_ITEM,(hl)
jr z,++
ld a,TILEINDEX_CHEST_OPENED
ld c,$14
call setTile
ld a,TILEINDEX_CHEST_OPENED
ld c,$16
jp setTile
++
ld a,(hl)
and $c0
cp $c0
ret z
bit 6,(hl)
jr z,+
ld a,(wSeedTreeRefilledBitset)
bit 0,a
ret nz
+
ld hl,@wallInsertion
ld bc,wRoomLayout + $03
ld a,$04
---
ldh (<hFF8D),a
ld a,$05
--
ldh (<hFF8C),a
ldi a,(hl)
ld (bc),a
inc bc
ldh a,(<hFF8C)
dec a
jr nz,--
ld a,$0b
call addAToBc
ldh a,(<hFF8D)
dec a
jr nz,---
ret
@wallInsertion:
'@ -split '\r?\n'
if ($goronWallNodes.Count -ne $goronWallExpected.Count + 4) {
    throw "roomSpecificTileChanges.s:$goronWallLabel instruction/data count changed."
}
for ($index = 0; $index -lt $goronWallExpected.Count; $index++) {
    if (($goronWallNodes[$index].Code -replace '\s', '').TrimEnd(':') -cne
        ($goronWallExpected[$index] -replace '\s', '').TrimEnd(':')) {
        throw "roomSpecificTileChanges.s:${goronWallLabel}:$($goronWallNodes[$index].Line) " +
            "expected '$($goronWallExpected[$index])', found '$($goronWallNodes[$index].Code)'."
    }
}
$goronWallTiles = [Collections.Generic.List[string]]::new()
foreach ($node in $goronWallNodes[$goronWallExpected.Count..($goronWallNodes.Count - 1)]) {
    if ($node.Kind -ne 'Data' -or $node.Name -ine '.db' -or $node.Operands.Count -ne 5) {
        throw "roomSpecificTileChanges.s:$goronWallLabel expected four five-byte wall rows."
    }
    foreach ($operand in $node.Operands) {
        $goronWallTiles.Add(([byte](Convert-AssemblyInteger $operand)).ToString('x2'))
    }
}
$goronWallOperation = 'draw:03:04:05:' + ($goronWallTiles -join ',')
Add-RoomTileChangeRule $goronWallLabel 'current_room_set:20' 'set:14:f0,16:f0'
Add-RoomTileChangeRule $goronWallLabel 'current_room_clear:20,current_room_clear:40' `
    $goronWallOperation
Add-RoomTileChangeRule $goronWallLabel `
    'current_room_clear:20,current_room_set:40,current_room_clear:80,runtime_mask_eq:cc4d:01:00' `
    $goronWallOperation

# Every flag/essence-backed routine must be imported above.
$flagTileChangeBlocks = [regex]::Matches(
    $roomTileChangeSource,
    '(?ms)^(?<label>tileReplacement_[A-Za-z0-9]+):(?<body>.*?)(?=^tileReplacement_|\z)')
$flagTileChangeCount = 0
foreach ($block in $flagTileChangeBlocks) {
    if ($block.Groups['body'].Value -notmatch
        'checkGlobalFlag|getThisRoomFlags|w(?:Present|Past|Group[0-9])RoomFlags|wEssencesObtained') {
        continue
    }
    $flagTileChangeCount++
    $label = $block.Groups['label'].Value
    if (-not $supportedTileChangeLabels.Contains($label)) {
        throw "Flag-backed room tile-change routine $label was not imported."
    }
}
if ($flagTileChangeCount -ne 34 -or $supportedTileChangeLabels.Count -ne 55) {
    throw "Expected 34 flag-backed and 55 total supported tile-change routines; " +
        "found $flagTileChangeCount and $($supportedTileChangeLabels.Count)."
}
$roomTileChangePath = Join-Path $destination 'metadata\room_tile_changes.tsv'
Write-GeneratedTable(
    $roomTileChangePath, $roomTileChangeRows)

# initializeRoom calls roomSpecificCode index $06 once before object parsing.
# Preserve its room table; the output retains its legacy filename.
$roomSpecificCodeSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\roomSpecificCode.s')
$roomEntryPath = Join-Path $Disassembly 'code/ages/roomSpecificCode.s'
$roomEntrySpecs = @{
    0x04 = @{ Label = 'roomSpecificCode4'; Instructions = @(
        'ld a,$06', 'ld (wMinimapRoom),a', 'ld hl,wPastRoomFlags+$06', 'set 4,(hl)', 'ret') }
    0x05 = @{ Label = 'roomSpecificCode5'; Instructions = @(
        'ld a,GLOBALFLAG_SAVED_NAYRU', 'call checkGlobalFlag', 'ret nz',
        'ld a,MUS_SADNESS', 'ld (wActiveMusic2),a', 'ret') }
    0x08 = @{ Label = 'roomSpecificCode8'; Instructions = @(
        'ld a,(wScreenTransitionDirection)', 'cp DIR_RIGHT', 'ret nz',
        'ld a,GLOBALFLAG_RALPH_ENTERED_PORTAL', 'call checkGlobalFlag', 'ret nz',
        'ld a,MUS_RALPH', 'ld (wActiveMusic2),a', 'ret') }
    0x09 = @{ Label = 'roomSpecificCode9'; Instructions = @(
        'ld a,GLOBALFLAG_FINISHEDGAME', 'call checkGlobalFlag', 'ret z',
        'ld a,MUS_NAYRU', 'ld (wActiveMusic2),a', 'ret') }
    0x0a = @{ Label = 'roomSpecificCodeA'; Instructions = @(
        'ld hl,wMinimapGroup', 'ld (hl),$00', 'inc l', 'ld (hl),$3c', 'ret') }
    0x0b = @{ Label = 'roomSpecificCodeB'; Instructions = @(
        'ld hl,wMinimapGroup', 'ld (hl),$01', 'inc l', 'ld (hl),$3c', 'ret') }
    0x0c = @{ Label = 'roomSpecificCodeC'; Instructions = @(
        'ld hl,wActiveMusic', 'ld a,(hl)', 'or a', 'ret nz', 'ld (hl),$ff', 'ret') }
}
$roomEntryDispatch = @(Read-AssemblyDataDirectives $roomEntryPath 'runRoomSpecificCode' '.dw' |
    ForEach-Object { $_.Operands })
if ($roomEntryDispatch.Count -ne 13) {
    throw "${roomEntryPath}:runRoomSpecificCode: expected 13 dispatch targets."
}
foreach ($code in @($roomEntrySpecs.Keys | Sort-Object)) {
    $spec = $roomEntrySpecs[$code]
    if ($roomEntryDispatch[$code] -cne $spec.Label) {
        throw "${roomEntryPath}:runRoomSpecificCode: handler `$$($code.ToString('x2')) " +
            "expected '$($spec.Label)', found '$($roomEntryDispatch[$code])'."
    }
    Assert-RoomTileInstructions $roomEntryPath $spec.Label $spec.Instructions
}
$roomEntryRows = [Collections.Generic.List[string]]::new()
$roomEntryRows.Add('# group`troom`tcode`tsource')
$continuousRespawnRows = [Collections.Generic.List[string]]::new()
$continuousRespawnRows.Add("# group`troom")
for ($group = 0; $group -lt 8; $group++) {
    $block = [regex]::Match(
        $roomSpecificCodeSource,
        "(?ms)^roomSpecificCodeGroup${group}Table:\s*(?<body>.*?)(?=^roomSpecificCodeGroup[0-7]Table:|^;;)")
    if (-not $block.Success) {
        throw "Could not parse roomSpecificCode group $group table."
    }
    foreach ($entry in [regex]::Matches(
        $block.Groups['body'].Value,
        '(?m)^\s*\.db\s+\$(?<room>[0-9a-f]{2})\s+\$(?<code>[0-9a-f]{2})')) {
        $entryCode = [Convert]::ToInt32($entry.Groups['code'].Value, 16)
        if ($roomEntrySpecs.ContainsKey($entryCode)) {
            $roomEntryRows.Add("$group`t$($entry.Groups['room'].Value.ToLowerInvariant())`t" +
                "$($entryCode.ToString('x2'))`tcode/ages/roomSpecificCode.s:$($roomEntrySpecs[$entryCode].Label)")
        }
        if ([Convert]::ToInt32($entry.Groups['code'].Value, 16) -eq 0x06) {
            $continuousRespawnRows.Add(
                "$group`t$($entry.Groups['room'].Value.ToLowerInvariant())")
        }
    }
}
if ($continuousRespawnRows.Count -ne 3) {
    throw "Expected 2 continuous death-respawn rooms, parsed $($continuousRespawnRows.Count - 1)."
}
$continuousRespawnPath = Join-Path $destination 'metadata\continuous_death_respawn_rooms.tsv'
Write-GeneratedTable(
    $continuousRespawnPath, $continuousRespawnRows)
if ($roomEntryRows.Count -ne 8) {
    throw "${roomEntryPath}: expected 7 room-entry side-effect bindings, found $($roomEntryRows.Count - 1)."
}
Write-GeneratedTable((Join-Path $destination 'metadata/room_entry_effects.tsv'), $roomEntryRows)

# Parse the 103 non-stub tileset records. The runtime needs the room-layout
# group and resolved six-palette block. Shared layouts are expanded by the
# vanilla-tilesets stage into the runtime's per-tileset asset format.
$tilesetSource = Read-ImportText (Join-Path $Disassembly "data\ages\tilesets.s")
$tilesetPattern = '(?ms);\s*0x(?<id>[0-9a-f]{2})\s*\r?\n' +
    '\s*\.db\s+\$(?<properties>[0-9a-f]{2}),\s*\$(?<flags>[0-9a-f]{2})[^\r\n]*\r?\n' +
    '\s*\.db[^\r\n]+\r?\n' +
    '\s*\.db[^\r\n]+\r?\n' +
    '\s*\.db\s+(?<palette>[A-Za-z0-9_]+)[^\r\n]*\r?\n' +
    '\s*\.db\s+\$(?<layout>[0-9a-f]{2}),\s*\$(?<group>[0-9a-f]{2}),\s*\$(?<animation>[0-9a-f]{2})'
$tilesets = [regex]::Matches($tilesetSource, $tilesetPattern)
if ($tilesets.Count -ne 103) {
    throw "Expected 103 concrete tileset records, parsed $($tilesets.Count)."
}

$paletteHeaderSource = Read-ImportText (Join-Path $Disassembly "data\ages\paletteHeaders.s")
$paletteBlocks = [regex]::Matches(
    $paletteHeaderSource,
    '(?ms)^m_PaletteHeaderStart\s+\$(?<id>[0-9a-f]{2}),[ \t]*(?<symbol>[A-Za-z0-9_]+)(?<body>.*?)(?=^m_PaletteHeaderStart|\z)'
)
$paletteHeaders = @{}
foreach ($block in $paletteBlocks) {
    $background = [regex]::Match(
        $block.Groups['body'].Value,
        'm_PaletteHeaderBg\s+2,\s*6,\s*(?<label>paletteData[0-9a-f]+)'
    )
    if ($background.Success) {
        $paletteHeaders[$block.Groups['symbol'].Value] = @{
            Id = [Convert]::ToByte($block.Groups['id'].Value, 16)
            Label = $background.Groups['label'].Value
            BackgroundRows = @([regex]::Matches($block.Groups['body'].Value,
                'm_PaletteHeaderBg\s+(?<first>[0-7]),\s*(?<count>[1-8]),\s*(?<label>paletteData[0-9a-f]+)'))
        }
    }
}

$paletteDataSource = Read-ImportText (Join-Path $Disassembly "data\ages\paletteData.s")

function Read-WorldPaletteBytes(
    [string]$label,
    [int]$colorCount
) {
    $labelIndex = $paletteDataSource.IndexOf(
        "${label}:", [StringComparison]::Ordinal)
    if ($labelIndex -lt 0) {
        throw "Gameplay palette data label not found: $label"
    }
    $nextLabel = $paletteDataSource.IndexOf(
        'paletteData', $labelIndex + $label.Length,
        [StringComparison]::Ordinal)
    if ($nextLabel -lt 0) { $nextLabel = $paletteDataSource.Length }
    $block = $paletteDataSource.Substring(
        $labelIndex, $nextLabel - $labelIndex)
    $colors = [regex]::Matches(
        $block,
        'm_RGB16\s+\$(?<r>[0-9a-f]{2})\s+\$(?<g>[0-9a-f]{2})\s+\$(?<b>[0-9a-f]{2})')
    if ($colors.Count -lt $colorCount) {
        throw "$label contains $($colors.Count) colors; expected $colorCount."
    }

    $bytes = [byte[]]::new($colorCount * 3)
    for ($color = 0; $color -lt $colorCount; $color++) {
        $bytes[$color * 3] = [Convert]::ToByte(
            $colors[$color].Groups['r'].Value, 16)
        $bytes[$color * 3 + 1] = [Convert]::ToByte(
            $colors[$color].Groups['g'].Value, 16)
        $bytes[$color * 3 + 2] = [Convert]::ToByte(
            $colors[$color].Groups['b'].Value, 16)
    }
    return $bytes
}

# initializeGame loads PALH_0f before every gameplay room. Besides the standard
# sprite palettes, that header installs paletteData48e0 as background palette
# 0. Special metatiles such as the closed/open chest ($f1/$f0) select that
# palette directly instead of one of the tileset-specific palettes 2-7.
$commonBgPaletteLabel = 'paletteData48e0'
$commonBgPaletteBytes = Read-WorldPaletteBytes $commonBgPaletteLabel 4
$commonBgPalettePath = Join-Path $destination 'metadata\commonBgPalette0.bin'
Write-GeneratedBytes($commonBgPalettePath, $commonBgPaletteBytes)

# initTextbox loads one of PALH_0e, PALH_0d, or PALH_bd into live BG slot 1.
# Keep the three source palettes in ordinary, ALTPALETTE1, ALTPALETTE2 order.
$textboxBgPaletteBytes = [Collections.Generic.List[byte]]::new()
foreach ($textboxPaletteLabel in @(
    'paletteData4920',
    'paletteData4928',
    'paletteData4930'
)) {
    foreach ($component in (Read-WorldPaletteBytes $textboxPaletteLabel 4)) {
        $textboxBgPaletteBytes.Add([byte]$component)
    }
}
Write-GeneratedBytes(
    (Join-Path $destination 'metadata\textboxBgPalette1.bin'),
    $textboxBgPaletteBytes.ToArray())

$tilesetRecordSize = 8
# checkTilesetOverride checks past room $48 bit 0 before loading present
# room $38. Vanilla increments tileset $22 twice to $24; only its layout
# group differs, so retain the runtime's layout-only override contract.
$makuOverrideSource = Read-ImportText (Join-Path $Disassembly 'code\ages\loadTilesetData.s')
$makuOverride = [regex]::Match($makuOverrideSource,
    '(?ms)^@checkMakuTreeSaved:.*?wActiveGroup.*?or a\s+ret nz.*?cp <ROOM_AGES_(?<room>038).*?wPastRoomFlags \+ \(<ROOM_AGES_(?<flagroom>148)\).*?and \$(?<mask>01)\s+ret z.*?ld hl,hFF8D\s+inc \(hl\)\s+inc \(hl\)\s+scf')
if (-not $makuOverride.Success) { throw 'checkTilesetOverride:@checkMakuTreeSaved layout gate changed.' }
$jabuFloorMasks = @(Read-AssemblyLiteralValues (Join-Path $Disassembly 'code\ages\loadTilesetData.s') '@@jabuBitset')
if ($jabuFloorMasks.Count -ne 3 -or ($jabuFloorMasks -join ',') -ne '0,1,3' -or
    $makuOverrideSource -notmatch '(?ms)^@checkJabuFlooded:.*?wDungeonIndex.*?cp \$07.*?TILESETFLAG_SIDESCROLL.*?wJabuWaterLevel.*?and \$07.*?wDungeonFloor.*?and \(hl\).*?ret z.*?ldh a,\(<hFF8D\)\s+inc a\s+ldh \(<hFF8D\),a') {
    throw 'loadTilesetData.s:@checkJabuFlooded floor masks or tileset increment changed.'
}
Write-GeneratedBytes((Join-Path $destination 'metadata\jabu_flooded_floors.bin'), [byte[]]$jabuFloorMasks)
$makuBase = @($tilesets | Where-Object { $_.Groups['id'].Value -eq '22' })[0]
$makuSaved = @($tilesets | Where-Object { $_.Groups['id'].Value -eq '24' })[0]
foreach ($field in @('properties', 'flags', 'palette', 'layout', 'animation')) {
    if ($makuBase.Groups[$field].Value -ne $makuSaved.Groups[$field].Value) {
        throw "Maku tilesets `$22/`$24 differ in $field; layout-only override is insufficient."
    }
}
Write-GeneratedTable((Join-Path $destination 'metadata\maku_tree_layout_override.tsv'), @(
    "# group`troom`tflag-group`tflag-room`tflag-mask`tlayout-group",
    "0`t$($makuOverride.Groups['room'].Value.Substring(1))`t1`t$($makuOverride.Groups['flagroom'].Value.Substring(1))`t$($makuOverride.Groups['mask'].Value)`t$($makuSaved.Groups['group'].Value)"))
$tilesetMetadata = [byte[]]::new(128 * $tilesetRecordSize)
$usedTilesets = [Collections.Generic.HashSet[int]]::new()

foreach ($tileset in $tilesets) {
    $id = [Convert]::ToInt32($tileset.Groups['id'].Value, 16)
    $layout = [Convert]::ToInt32($tileset.Groups['layout'].Value, 16)
    $layoutGroup = [Convert]::ToInt32($tileset.Groups['group'].Value, 16)
    $animation = [Convert]::ToInt32($tileset.Groups['animation'].Value, 16)
    $properties = [Convert]::ToInt32($tileset.Groups['properties'].Value, 16)
    $flags = [Convert]::ToInt32($tileset.Groups['flags'].Value, 16)
    $paletteSymbol = $tileset.Groups['palette'].Value
    if (-not $paletteHeaders.ContainsKey($paletteSymbol)) {
        throw "Could not resolve background palette for tileset $($id.ToString('x2')) ($paletteSymbol)."
    }

    $paletteHeader = $paletteHeaders[$paletteSymbol]
    $tilesetMetadata[$id * $tilesetRecordSize] = [byte]$layout
    $tilesetMetadata[$id * $tilesetRecordSize + 1] = [byte]$layoutGroup
    $tilesetMetadata[$id * $tilesetRecordSize + 2] = [byte]$paletteHeader.Id
    $tilesetMetadata[$id * $tilesetRecordSize + 3] = 1
    $tilesetMetadata[$id * $tilesetRecordSize + 4] = [byte]$animation
    $tilesetMetadata[$id * $tilesetRecordSize + 5] = if (($flags -band 0x08) -ne 0) {
        [byte]($properties -band 0x0f)
    } else {
        [byte]0xff
    }
    $tilesetMetadata[$id * $tilesetRecordSize + 6] = [byte](($properties -shr 4) -band 0x07)
    $tilesetMetadata[$id * $tilesetRecordSize + 7] = [byte]$flags
    [void]$usedTilesets.Add($id)

    $paletteBytes = [byte[]]::new(24 * 3)
    # loadPaletteHeader applies every row in original order. For example,
    # PALH_TILESET_ROOM_OF_RITES_ICE ($67) overwrites BG6 after its six-slot
    # base load; retaining only the first row gives the ice the lava colors.
    foreach($row in $paletteHeader.BackgroundRows) {
        $first=[int]::Parse($row.Groups['first'].Value)
        $count=[int]::Parse($row.Groups['count'].Value)
        if($first -lt 2 -or $first+$count -gt 8){throw "${paletteSymbol}: BG row outside tileset slots 2..7."}
        $bytes=Read-WorldPaletteBytes $row.Groups['label'].Value ($count*4)
        [Array]::Copy($bytes,0,$paletteBytes,($first-2)*12,$bytes.Length)
    }
    $palettePath = Join-Path $destination "metadata\palette$($id.ToString('x2')).bin"
    Write-GeneratedBytes($palettePath, $paletteBytes)

}

$metadataPath = Join-Path $destination "metadata\tilesets.bin"
Write-GeneratedBytes($metadataPath, $tilesetMetadata)

# Join the two original collision-mode-indexed tables used by
# interactWithTileBeforeLink and INTERAC_PUSHBLOCK. Each generated record is
# four bytes: interactable parameter, source replacement tile, destination
# tile, and property flags. $ff in the first byte means that the metatile does
# not use the ordinary pushblock interaction in that collision mode.
function Read-LocalHexByteTable([string]$path, [string]$tableLabel, [string]$pointerDirective) {
    $bytes = [Collections.Generic.List[byte]]::new()
    $labels = @{}
    $pointers = [Collections.Generic.List[string]]::new()
    foreach ($node in Read-AssemblyLabelNodes $path $tableLabel) {
        if ($node.Kind -eq 'Label' -and $node.Name.StartsWith('@')) {
            $labels[$node.Name] = $bytes.Count
        }
        if ($node.Name -ieq $pointerDirective -and
            $node.Operands.Count -gt 0) {
            $pointers.Add($node.Operands[0])
            continue
        }
        if ($node.Kind -eq 'Data' -and $node.Name -ieq '.db') {
            foreach ($operand in $node.Operands) {
                $bytes.Add([byte](Convert-AssemblyInteger $operand))
            }
        }
    }
    if ($pointers.Count -ne 6) {
        throw "$tableLabel should have 6 collision-mode pointers, got $($pointers.Count)."
    }
    return @{ Bytes = $bytes; Labels = $labels; Pointers = $pointers }
}

$interactableTable = Read-LocalHexByteTable `
    (Join-Path $Disassembly 'data\ages\tile_properties\interactableTiles.s') `
    'interactableTilesTable' '.dw'
$pushableTable = Read-LocalHexByteTable `
    (Join-Path $Disassembly 'data\ages\tile_properties\pushableTiles.s') `
    'pushableTilePropertiesTable' 'dbrel'
$pushableBytes = [byte[]]::new(6 * 256 * 4)
$nativePushableBytes = [byte[]]::new(6 * 256 * 3)
for ($i = 0; $i -lt $nativePushableBytes.Length; $i++) { $nativePushableBytes[$i] = 0xff }
for ($i = 0; $i -lt $pushableBytes.Length; $i++) { $pushableBytes[$i] = 0xff }
$interactableBytes = [byte[]]::new(6 * 256)
for ($i = 0; $i -lt $interactableBytes.Length; $i++) { $interactableBytes[$i] = 0xff }
$interactableRecords = 0
$joinedPushableRecords = 0
$somariaPushRows = [Collections.Generic.List[string]]::new()
$somariaPushRows.Add("# active-collisions`ttile`tparameter`tsource")
for ($mode = 0; $mode -lt 6; $mode++) {
    $interactable = @{}
    if (-not $interactableTable.Labels.ContainsKey($interactableTable.Pointers[$mode])) {
        throw "interactableTilesTable: unresolved collision-mode $mode pointer $($interactableTable.Pointers[$mode])."
    }
    $offset = $interactableTable.Labels[$interactableTable.Pointers[$mode]]
    # Labels record positions in the complete physical byte stream. In
    # particular @indoors falls through @dungeons/@five and @sidescrolling;
    # @overworld/@underwater are aliases. Do not stop at the next label.
    while ($offset -lt $interactableTable.Bytes.Count -and $interactableTable.Bytes[$offset] -ne 0) {
        if ($offset + 1 -ge $interactableTable.Bytes.Count) {
            throw "interactableTilesTable: truncated parameter for collision mode $mode at offset $offset."
        }
        $tile = $interactableTable.Bytes[$offset]
        $parameter = $interactableTable.Bytes[$offset + 1]
        $lookupOffset = $mode * 256 + $tile
        if ($interactableBytes[$lookupOffset] -ne 0xff -or ($parameter -band 15) -gt 6) {
            throw "interactableTilesTable: duplicate tile or unsupported Ages handler in mode $mode, tile `$$($tile.ToString('x2')), parameter `$$($parameter.ToString('x2'))."
        }
        $interactableBytes[$lookupOffset] = $parameter
        $interactableRecords++
        if (($parameter -band 0x0f) -eq 0) { $interactable[$tile] = $parameter }
        $offset += 2
    }
    if ($offset -ge $interactableTable.Bytes.Count) {
        throw "interactableTilesTable: collision mode $mode has no zero terminator."
    }

    $properties = @{}
    $offset = $pushableTable.Labels[$pushableTable.Pointers[$mode]]
    while ($pushableTable.Bytes[$offset] -ne 0) {
        $tile = $pushableTable.Bytes[$offset]
        $properties[$tile] = @(
            $pushableTable.Bytes[$offset + 1],
            $pushableTable.Bytes[$offset + 2],
            $pushableTable.Bytes[$offset + 3])
        # Script-created INTERAC $14 also reads properties for tiles absent
        # from nextToPushableBlock's player-input dispatch, such as palace $02.
        $nativeOffset = ($mode * 256 + $tile) * 3
        for ($column = 0; $column -lt 3; $column++) {
            $nativePushableBytes[$nativeOffset + $column] = $properties[$tile][$column]
        }
        $offset += 4
    }

    foreach ($tile in $interactable.Keys) {
        # The Somaria block ($da) uses its dedicated item object and therefore
        # intentionally has no INTERAC_PUSHBLOCK property record.
        if (-not $properties.ContainsKey($tile)) {
            if ($tile -eq 0xda) {
                $somariaPushRows.Add("$mode`t$($tile.ToString('x2'))`t$($interactable[$tile].ToString('x2'))`tdata/ages/tile_properties/interactableTiles.s:$($interactableTable.Pointers[$mode])")
                continue
            }
            # INTERAC_PUSHBLOCK@loadPushableTileProperties returns at the
            # zero terminator on a miss. The freshly allocated var32..var34
            # stay zero; the native dispatch still moves/deletes INTERAC$14.
            # Collision modes04/05 intentionally point to an empty table.
            if ($mode -notin @(4, 5)) {
                throw "data/ages/tile_properties/pushableTiles.s:$($pushableTable.Pointers[$mode]): unexpected property lookup miss for collision mode $mode, tile `$$($tile.ToString('x2'))."
            }
            $properties[$tile] = @(0, 0, 0)
        }
        $recordOffset = ($mode * 256 + $tile) * 4
        $pushableBytes[$recordOffset] = $interactable[$tile]
        $pushableBytes[$recordOffset + 1] = $properties[$tile][0]
        $pushableBytes[$recordOffset + 2] = $properties[$tile][1]
        $pushableBytes[$recordOffset + 3] = $properties[$tile][2]
        $joinedPushableRecords++
    }
}
if ($joinedPushableRecords -ne 51) {
    throw "Expected 51 collision-mode pushblock records including 18 native zero-property rows, joined $joinedPushableRecords."
}
$pushablePath = Join-Path $destination 'metadata\pushableTiles.bin'
Write-GeneratedBytes($pushablePath, $pushableBytes)
Write-GeneratedBytes((Join-Path $destination 'metadata/native_pushblock_properties.bin'), $nativePushableBytes)
if ($interactableRecords -ne 115) { throw "interactableTilesTable: expected115 collision-mode/tile entries, got$interactableRecords." }
Write-GeneratedBytes((Join-Path $destination 'metadata/interactableTiles.bin'), $interactableBytes)
if ($somariaPushRows.Count -ne 7) { throw 'Expected Somaria push dispatch in all six collision modes.' }
Write-GeneratedTable((Join-Path $destination 'metadata/somaria_push_tiles.tsv'), $somariaPushRows)

# Source graphics initialization owns wInShop, independently of an NPC's
# item restrictions. Preserve the physical local-label stream: group$03
# deliberately falls through into the empty group$04 terminator.
$roomGfxPath = Join-Path $Disassembly 'code/ages/roomGfxChanges.s'
$roomGfxSource = Read-ImportText $roomGfxPath
if ($roomGfxSource -notmatch '(?ms)^roomTileChangesAfterLoad04:\s*ld hl,wInShop\s*set 1,\(hl\)\s*ld a,TREE_GFXH_03\s*jp loadTreeGfx') {
    throw 'roomGfxChanges.s:roomTileChangesAfterLoad04 must set wInShop bit$01 before loading price graphics.'
}
$afterGfxBytes = [Collections.Generic.List[byte]]::new()
$afterGfxLabels = @{}
$afterGfxPointers = [Collections.Generic.List[string]]::new()
$captureAfterGfxPointers = $false
foreach ($node in Read-AssemblyLabelNodes $roomGfxPath 'applyRoomSpecificTileChangesAfterGfxLoad') {
    if ($node.Kind -eq 'Label') {
        if ($node.Name -eq '@tileChangesGroupTable') { $captureAfterGfxPointers = $true }
        elseif ($node.Name -match '^@group[0-7]$') {
            $captureAfterGfxPointers = $false
            $afterGfxLabels[$node.Name] = $afterGfxBytes.Count
        }
    }
    if ($captureAfterGfxPointers -and $node.Kind -eq 'Data' -and $node.Name -ieq '.dw') {
        foreach ($operand in $node.Operands) { $afterGfxPointers.Add($operand) }
    }
    if ($node.Kind -eq 'Data' -and $node.Name -ieq '.db') {
        foreach ($operand in $node.Operands) { $afterGfxBytes.Add([byte](Convert-AssemblyInteger $operand)) }
    }
}
if ($afterGfxPointers.Count -ne 8) {
    throw "roomGfxChanges.s:@tileChangesGroupTable requires8 pointers, got$($afterGfxPointers.Count)."
}
$shopFlags = [byte[]]::new(8 * 256)
$afterGfxRecords = 0
$shopRooms = 0
for ($group = 0; $group -lt 8; $group++) {
    $pointer = $afterGfxPointers[$group]
    if (-not $afterGfxLabels.ContainsKey($pointer)) {
        throw "roomGfxChanges.s:@tileChangesGroupTable group$group points to missing$pointer."
    }
    $cursor = [int]$afterGfxLabels[$pointer]
    $seenRooms = @{}
    while ($true) {
        if ($cursor -ge $afterGfxBytes.Count) { throw "roomGfxChanges.s:$pointer lost its zero terminator." }
        $room = [int]$afterGfxBytes[$cursor++]
        if ($room -eq 0) { break }
        if ($cursor -ge $afterGfxBytes.Count) { throw "roomGfxChanges.s:${pointer}: truncated room `$$($room.ToString('x2')) handler." }
        $handler = [int]$afterGfxBytes[$cursor++]
        if ($handler -gt 0x0a -or $seenRooms.ContainsKey($room)) {
            throw "roomGfxChanges.s:${pointer}: invalid/duplicate room `$$($room.ToString('x2')), handler `$$($handler.ToString('x2'))."
        }
        $seenRooms[$room] = $true
        $afterGfxRecords++
        if ($handler -eq 4) { $shopFlags[$group * 256 + $room] = 2; $shopRooms++ }
    }
}
if ($afterGfxRecords -ne 35 -or $shopRooms -ne 4) {
    throw "roomGfxChanges.s: expected35 after-load room rows/four shop initializations, got$afterGfxRecords/$shopRooms."
}
Write-GeneratedBytes((Join-Path $destination 'metadata/roomShopFlags.bin'), $shopFlags)

# Transformation rings replace Link with special objects $03-$07. Export the
# eight source GFX/OAM combinations for each disguise instead of reconstructing
# their tile offsets or mirroring rules in gameplay code.
$specialObjectAnimationSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')
$specialObjectOamSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\specialObjectOamData.s')
$specialObjectDamageSource = Read-ImportText (
    Join-Path $Disassembly 'code\specialObjectAnimationsAndDamage.s')
if ($specialObjectDamageSource -notmatch
        '(?ms)^@ringToID:\s*\.db OCTO_RING\s+SPECIALOBJECT_LINK_AS_OCTOROK\s*\.db MOBLIN_RING\s+SPECIALOBJECT_LINK_AS_MOBLIN\s*\.db LIKE_LIKE_RING\s+SPECIALOBJECT_LINK_AS_LIKELIKE\s*\.db SUBROSIAN_RING\s+SPECIALOBJECT_LINK_AS_SUBROSIAN\s*\.db FIRST_GEN_RING\s+SPECIALOBJECT_LINK_AS_RETRO\s*\.db \$00' -or
    $specialObjectAnimationSource -notmatch
        '(?ms)^animationData1a1a6:\s*\.db \$02 \$00 \$00\s*animationLoop1a1a9:\s*\.db \$06 \$04 \$84\s*\.db \$06 \$00 \$00\s*m_AnimationLoop animationLoop1a1a9') {
    throw 'Transformed-Link ring mapping or 2/6/6 animation cadence changed.'
}
$specialObjectOamPointerBlock = [regex]::Match(
    $specialObjectAnimationSource,
    '(?ms)^specialObject03OamDataPointers:\s*(?:specialObject0[4-9]OamDataPointers:\s*)*(?<body>(?:\s*\.dw\s+oamData4c[0-9a-f]+\s*)+?)(?=^specialObject02GfxPointers:)')
if (-not $specialObjectOamPointerBlock.Success) {
    throw 'Could not parse the shared transformed-Link OAM pointer table.'
}
$specialObjectOamLabels = @(
    [regex]::Matches(
        $specialObjectOamPointerBlock.Groups['body'].Value,
        '(?m)^\s*\.dw\s+(?<label>oamData4c[0-9a-f]+)') |
        ForEach-Object { $_.Groups['label'].Value })
if ($specialObjectOamLabels.Count -lt 6) {
    throw 'The transformed-Link OAM pointer table has fewer than six entries.'
}

function Resolve-TransformedLinkOam([int]$index) {
    if ($index -lt 0 -or $index -ge $specialObjectOamLabels.Count) {
        throw "Transformed-Link OAM index `$$($index.ToString('x2')) is out of range."
    }
    $label = $specialObjectOamLabels[$index]
    $block = [regex]::Match(
        $specialObjectOamSource,
        ('(?ms)^{0}:\s*\.db \$(?<count>[0-9a-f]{{2}})(?<body>.*?)(?=^oamData4c[0-9a-f]+:|\z)' -f
            [regex]::Escape($label)))
    if (-not $block.Success) { throw "Could not parse $label." }
    $count = [Convert]::ToInt32($block.Groups['count'].Value, 16)
    $entries = @(
        [regex]::Matches(
            $block.Groups['body'].Value,
            '\.db \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2}) \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2})') |
            ForEach-Object {
                $oamEntry = $_
                $values = @('y', 'x', 'tile', 'flags') | ForEach-Object {
                    [Convert]::ToInt32($oamEntry.Groups[$_].Value, 16)
                }
                $values -join ','
            })
    if ($entries.Count -ne $count) {
        throw "$label declares $count OAM entries but contains $($entries.Count)."
    }
    return $entries -join ';'
}

$transformedRingBySpecialObject = @{
    2 = 0xff # SPECIALOBJECT_LINK_AS_BABY; no transformation ring
    3 = 0x2d # SUBROSIAN_RING
    4 = 0x2e # FIRST_GEN_RING
    5 = 0x2a # OCTO_RING
    6 = 0x2b # MOBLIN_RING
    7 = 0x2c # LIKE_LIKE_RING
}
$transformedLinkRows = [Collections.Generic.List[string]]::new()
$transformedLinkRows.Add(
    "# ring`tspecial-object`tdirection`tframe`tsprite`ttile-base`toam`tinitial-duration`tloop-duration")
foreach ($specialObject in 2..7) {
    $block = [regex]::Match(
        $specialObjectAnimationSource,
        "(?ms)^specialObject0${specialObject}GfxPointers:\s*(?<body>(?:\s*m_SpecialObjectGfxPointer[^\r\n]+\r?\n){8})")
    if (-not $block.Success) {
        throw "Could not parse specialObject0${specialObject}GfxPointers."
    }
    $entries = @([regex]::Matches(
        $block.Groups['body'].Value,
        'm_SpecialObjectGfxPointer \$(?<oam>[0-9a-f]{2}) (?<sprite>[A-Za-z0-9_]+) \$(?<offset>[0-9a-f]{4}) \$(?<tiles>[0-9a-f]{2})'))
    if ($entries.Count -ne 8) {
        throw "Expected eight transformed-Link GFX entries for special object `$$($specialObject.ToString('x2'))."
    }
    for ($index = 0; $index -lt 8; $index++) {
        $entry = $entries[$index]
        $oamIndex = [Convert]::ToInt32($entry.Groups['oam'].Value, 16)
        $offset = [Convert]::ToInt32($entry.Groups['offset'].Value, 16)
        $tiles = [Convert]::ToInt32($entry.Groups['tiles'].Value, 16)
        if ($tiles -ne 4 -or ($offset % 16) -ne 0) {
            throw "Special object `$$($specialObject.ToString('x2')) has an unsupported GFX span."
        }
        $transformedLinkRows.Add(
            "$($transformedRingBySpecialObject[$specialObject].ToString('x2'))`t$($specialObject.ToString('x2'))`t$($index % 4)`t$([Math]::Floor($index / 4))`t$($entry.Groups['sprite'].Value)`t$($offset / 16)`t$(Resolve-TransformedLinkOam $oamIndex)`t2`t6")
    }
}
$transformedLinkPath = Join-Path $destination 'metadata\transformed_link.tsv'
Write-GeneratedTable(
    $transformedLinkPath, $transformedLinkRows)
$babyPath = Join-Path $Disassembly 'object_code/common/specialObjects/transformedLink.s'
$baby = (Read-ImportText $babyPath) -replace '(?m);[^\r\n]*',''
if ($baby -notmatch '(?ms)^@state0:.*?dropLinkHeldItem.*?clearAllParentItems.*?SPECIALOBJECT_LINK_AS_BABY.*?counter1\s*ld \(hl\),\$e0\s*inc l\s*ld \(hl\),\$01.*?SND_BECOME_BABY.*?@disableTransformationForBaby:\s*ld a,SND_MAGIC_POWDER' -or
    $baby -notmatch '(?ms)^@state1:.*?wLinkForceState.*?wPaletteThread_mode.*?wScrollMode.*?updateLinkDamageTaken.*?wLinkDeathTrigger.*?retIfTextIsActive.*?wDisabledObjects.*?and \$81.*?decPegasusSeedCounter.*?decHlRef16WithCap.*?@disableTransformationForBaby.*?updateLinkSpeed_standard.*?srl \(hl\)' -or
    $specialObjectAnimationSource -notmatch '(?ms)^animationData1a121:\s*\.db \$02 \$00 \$00\s*animationLoop1a124:\s*\.db \$06 \$04 \$84\s*\.db \$06 \$00 \$00\s*m_AnimationLoop animationLoop1a124') {
    throw 'transformedLink.s: baby initialization, 480-update lifetime, gates, half speed or 2/6/6 animation changed.'
}
Write-GeneratedTable((Join-Path $destination 'metadata/baby_link.tsv'),@(
    "# duration`tspeed-shift`tstep-parameter`tsource",
    "480`t1`t84`tobject_code/common/specialObjects/transformedLink.s;data/ages/specialObjectAnimationData.s:animationData1a121"
))


$tilesetAssignmentSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tilesetAssignments.s')
if ($tilesetAssignmentSource -notmatch
        '(?ms)^group4Tilesets:.*?^group6Tilesets:\s*\r?\n\s*\.incbin "rooms/ages/group4Tilesets\.bin"' -or
    $tilesetAssignmentSource -notmatch
        '(?ms)^group5Tilesets:.*?^group7Tilesets:\s*\r?\n\s*\.incbin "rooms/ages/group5Tilesets\.bin"') {
    throw 'Side-scrolling tileset aliases no longer map group 6/7 to group 4/5.'
}
$tilesetSourceGroups = @(0, 1, 2, 3, 4, 5, 4, 5)
foreach ($group in 0..7) {
    $sourceGroup = $tilesetSourceGroups[$group]
    Copy-GeneratedFile "rooms\ages\group${sourceGroup}Tilesets.bin" `
        "groups\group${group}Tilesets.bin"
}
Copy-GeneratedFile "rooms\ages\roomPacksPresent.bin" "groups\roomPacksPresent.bin"
Copy-GeneratedFile "rooms\ages\roomPacksPast.bin" "groups\roomPacksPast.bin"
Copy-GeneratedFile "gfx\common\spr_link.png" "gfx\spr_link.png"
Copy-GeneratedFile "gfx\common\spr_swords.png" "gfx\spr_swords.png"
Copy-GeneratedFile "gfx\ages\spr_boomerang.png" "gfx\spr_boomerang.png"
Copy-GeneratedFile "gfx\ages\spr_seed_shooter.png" "gfx\spr_seed_shooter.png"
Copy-GeneratedFile "gfx\ages\spr_switch_hook.png" "gfx\spr_switch_hook.png"
Copy-GeneratedFile "gfx\ages\spr_cane_of_somaria.png" "gfx\spr_cane_of_somaria.png"
Copy-GeneratedFile "gfx\ages\spr_subrosian.png" "gfx\spr_subrosian.png"
Copy-GeneratedFile "gfx\common\spr_link_retro.png" "gfx\spr_link_retro.png"
Copy-GeneratedFile "gfx\common\spr_octorok_leever_tektite_zora.png" "gfx\spr_octorok_leever_tektite_zora.png"
Copy-GeneratedFile "gfx\common\spr_moblin.png" "gfx\spr_moblin.png"
Copy-GeneratedFile "gfx\common\spr_ballandchain_likelike.png" "gfx\spr_ballandchain_likelike.png"
Copy-GeneratedFile "gfx_compressible\common\spr_common_sprites.png" "gfx\spr_common_sprites.png"
Copy-GeneratedFile "gfx_compressible\ages\spr_syrup_teenager.png" "gfx\spr_syrup_teenager.png"
Copy-GeneratedFile "gfx_compressible\ages\gfx_hud.png" "gfx\gfx_hud.png"
Copy-GeneratedFile "gfx\ages\spr_item_icons_1.png" "gfx\spr_item_icons_1.png"
Copy-GeneratedFile "gfx\ages\spr_item_icons_2.png" "gfx\spr_item_icons_2.png"
Copy-GeneratedFile "gfx\ages\spr_item_icons_3.png" "gfx\spr_item_icons_3.png"
Copy-GeneratedFile "gfx_compressible\ages\spr_quest_items_4.png" "gfx\spr_quest_items_4.png"
Copy-GeneratedFile "gfx_compressible\ages\spr_common_items.png" "gfx\spr_common_items.png"
Copy-GeneratedFile "gfx\common\gfx_font.png" "gfx\gfx_font.png"
Copy-GeneratedFile "gfx\common\gfx_font_jp.png" "gfx\gfx_font_jp.png"
Copy-GeneratedFile "gfx\ages\gfx_font_tradeitems.png" "gfx\gfx_font_tradeitems.png"
Copy-GeneratedFile "gfx\common\gfx_font_heartpiece.png" "gfx\gfx_font_heartpiece.png"
