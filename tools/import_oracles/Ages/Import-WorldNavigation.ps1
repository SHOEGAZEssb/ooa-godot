# INTERAC_SPECIAL_WARP $1f:$00 is separate from the tile-warp tables. Its
# fourth placement byte selects a hardcoded room/position pair, while the
# third byte is converted from a packed short position into the invisible
# interaction's coordinates. The interaction only fires while Link is diving
# and touching its imported collision box.
$specialWarpObjectSource = Read-ImportLines (
    Join-Path $Disassembly 'objects\ages\mainData.s')
$specialWarpCodeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\specialWarp.s')
$specialWarpSubid0Match = [regex]::Match(
    $specialWarpCodeSource,
    '(?ms)^@subid0:\s*(?<body>.*?)(?=^; Subid 1:)')
if (-not $specialWarpSubid0Match.Success) {
    throw 'INTERAC_SPECIAL_WARP $1f:$00 implementation was not found in specialWarp.s.'
}
$specialWarpSubid0 = $specialWarpSubid0Match.Groups['body'].Value

function Read-SpecialWarpImmediate([string]$variable) {
    $match = [regex]::Match(
        $script:specialWarpSubid0,
        ('ld a,\$(?<value>[0-9a-f]{{2}})\s*\r?\n\s*ld \({0}\),a' -f
            [regex]::Escape($variable)))
    if (-not $match.Success) {
        throw "INTERAC_SPECIAL_WARP `$1f:`$00 no longer writes an immediate to $variable."
    }
    return [Convert]::ToInt32($match.Groups['value'].Value, 16)
}

$specialWarpDestGroupRaw = Read-SpecialWarpImmediate 'wWarpDestGroup'
$specialWarpDestTransition = Read-SpecialWarpImmediate 'wWarpTransition'
$specialWarpTransition2 = Read-SpecialWarpImmediate 'wWarpTransition2'
$specialWarpCollisionMatch = [regex]::Match(
    $specialWarpSubid0,
    'ld a,\$(?<value>[0-9a-f]{2})\s*\r?\n\s*call objectSetCollideRadius')
$specialWarpDataMatch = [regex]::Match(
    $specialWarpSubid0,
    '(?ms)^@@warpData:\s*(?<body>.*?)(?=^@@initialize:)')
if (-not $specialWarpCollisionMatch.Success -or
    -not $specialWarpDataMatch.Success -or
    ($specialWarpDestGroupRaw -band 0x80) -eq 0) {
    throw 'INTERAC_SPECIAL_WARP $1f:$00 initialization or direct destination data changed.'
}
$specialWarpCollisionRadius = [Convert]::ToInt32(
    $specialWarpCollisionMatch.Groups['value'].Value, 16)
$specialWarpDestGroup = $specialWarpDestGroupRaw -band 0x7f
$specialWarpDestinations = [Collections.Generic.List[object]]::new()
foreach ($destinationMatch in [regex]::Matches(
    $specialWarpDataMatch.Groups['body'].Value,
    '(?m)^\s*\.db\s+\$(?<room>[0-9a-f]{2})\s+\$(?<position>[0-9a-f]{2})\s*$')) {
    $specialWarpDestinations.Add(@{
        Room = [Convert]::ToInt32(
            $destinationMatch.Groups['room'].Value, 16)
        Position = [Convert]::ToInt32(
            $destinationMatch.Groups['position'].Value, 16)
    })
}
if ($specialWarpDestinations.Count -ne 2 -or
    $specialWarpCollisionRadius -ne 2 -or
    $specialWarpDestGroup -ne 7 -or
    $specialWarpDestTransition -ne 1 -or
    $specialWarpTransition2 -ne 3) {
    throw 'INTERAC_SPECIAL_WARP $1f:$00 clean-US constants changed.'
}

$specialWarpRows = [Collections.Generic.List[string]]::new()
$specialWarpRows.Add(
    "# source-group`tsource-room`torder`tsource-position`troute-index" +
    "`tcollision-radius`tdest-group`tdest-room`tdest-position" +
    "`tdest-transition`twarp-transition-2`tsource")
$specialWarpGroup = -1
$specialWarpRoom = -1
$specialWarpOrder = 0
foreach ($line in $specialWarpObjectSource) {
    if ($line -match '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $specialWarpGroup = [int]$Matches['group']
        $specialWarpRoom = [Convert]::ToInt32($Matches['room'], 16)
        $specialWarpOrder = 0
        continue
    }
    if ($specialWarpGroup -lt 0) { continue }
    if ($line -match '^\s*obj_End\b') {
        $specialWarpGroup = -1
        $specialWarpRoom = -1
        continue
    }
    if ($line -match '^\s*obj_Interaction\s+\$1f\s+\$00\s+\$(?<position>[0-9a-f]{2})\s+\$(?<route>[0-9a-f]{2})\s*$') {
        $route = [Convert]::ToInt32($Matches['route'], 16)
        if ($route -ge $specialWarpDestinations.Count) {
            throw "INTERAC_SPECIAL_WARP route `$$($route.ToString('x2')) in " +
                "$specialWarpGroup`:$($specialWarpRoom.ToString('x2')) is undefined."
        }
        $destinationRecord = $specialWarpDestinations[$route]
        $sourceLabel =
            "objects/ages/mainData.s:group${specialWarpGroup}Map$($specialWarpRoom.ToString('x2'))ObjectData;" +
            'object_code/ages/interactions/specialWarp.s:@subid0'
        $specialWarpRows.Add(
            "$specialWarpGroup`t$($specialWarpRoom.ToString('x2'))`t$specialWarpOrder" +
            "`t$($Matches['position'])`t$($route.ToString('x2'))" +
            "`t$specialWarpCollisionRadius`t$specialWarpDestGroup" +
            "`t$($destinationRecord.Room.ToString('x2'))" +
            "`t$($destinationRecord.Position.ToString('x2'))" +
            "`t$specialWarpDestTransition`t$specialWarpTransition2`t$sourceLabel")
    }
    if ($line -match '^\s*obj_(?!End\b)') {
        $specialWarpOrder++
    }
}
if ($specialWarpRows.Count -ne 3 -or
    -not ($specialWarpRows | Where-Object {
        $_ -like "1`t01`t0`t18`t00`t2`t7`t09`t01`t1`t3`t*"
    }) -or
    -not ($specialWarpRows | Where-Object {
        $_ -like "5`tcc`t1`t12`t01`t2`t7`t05`t03`t1`t3`t*"
    })) {
    throw 'Expected the two clean-US INTERAC_SPECIAL_WARP $1f:$00 dive records.'
}
Write-GeneratedTable(
    (Join-Path $destination 'objects\dive_warps.tsv'),
    $specialWarpRows)

# Vanilla omits duplicate room files. Resolve the labels preceding each
# m_RoomLayoutData declaration into concrete, address-independent assets.
function Export-RoomLayouts {
    $aliases = [Collections.Generic.List[string]]::new()
    $exported = [Collections.Generic.HashSet[string]]::new()
    foreach ($node in Read-AssemblyNodes (Join-Path $Disassembly 'data/ages/roomLayoutData.s')) {
        if ($node.Kind -in @('Blank', 'Comment')) { continue }
        if ($node.Kind -eq 'Label' -and $node.Name -match '^room0[0-5][0-9a-f]{2}$') {
            $aliases.Add($node.Name)
            continue
        }
        if ($node.Kind -ne 'MacroInvocation' -or $node.Name -ne 'm_RoomLayoutData' -or
            $node.Operands.Count -ne 1 -or $node.Operands[0] -notmatch '^room0[0-5][0-9a-f]{2}$') {
            throw "Unsupported room layout source: $($node.Span): $($node.Code)."
        }
        $source = $node.Operands[0]
        $kind = if ($source[5] -lt '4') { 'small' } else { 'large' }
        $aliases.Add($source)
        foreach ($alias in $aliases) {
            if ($alias[5] -ne $source[5] -or -not $exported.Add($alias)) {
                throw "Duplicate or cross-group room layout alias: $alias -> $source."
            }
            Copy-GeneratedFile "rooms/ages/$kind/$source.bin" "rooms/$kind/$alias.bin"
        }
        $aliases.Clear()
    }
    if ($aliases.Count -ne 0 -or $exported.Count -ne 1536) {
        throw "Expected 1536 resolved room layouts, imported $($exported.Count); dangling aliases: $aliases."
    }
}
Export-RoomLayouts
