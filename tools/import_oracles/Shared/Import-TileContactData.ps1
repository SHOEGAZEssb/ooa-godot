# Native contact data shares handlers; the selected pack supplies collision
# profiles, text control flow and tile substitutions.
$agesTexts = $allTexts
$agesFallthrough = $allTextFallthroughIds
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Texts = $agesTexts; Fallthrough = $agesFallthrough },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Texts = $seasonTileInteractionTexts; Fallthrough = @{} }
)) {
    $destination = $pack.Root
    $allTexts = $pack.Texts
    $allTextFallthroughIds = $pack.Fallthrough
    $standardTileSubstitutionSource = Read-ImportText (Join-Path $Disassembly "data/$($pack.Name)/tile_properties/standardTileSubstitutions.s")
    $interactableTileDataSource = Read-ImportText (Join-Path $Disassembly "data/$($pack.Name)/tile_properties/interactableTiles.s")
    $bank0Source = Read-ImportText (Join-Path $Disassembly 'code/bank0.s')
    $keyDoorOpenTiles = @{}
    foreach ($entry in [regex]::Matches(
        $standardTileSubstitutionSource,
        '(?m)^\s*\.db \$(?<open>[0-9a-f]{2}) \$(?<closed>7[0-7])(?:\s|;)')) {
        $closedTile = $entry.Groups['closed'].Value
        if ($keyDoorOpenTiles.ContainsKey($closedTile)) {
            throw "Duplicate standard dungeon-key door substitution for `$$closedTile."
        }
        $keyDoorOpenTiles[$closedTile] = $entry.Groups['open'].Value
    }
    $keyDoorFlags = @{}
    foreach ($entry in [regex]::Matches(
        $bank0Source,
        '(?m)^\s*\.db \$(?<room>[0-9a-f]{2}) \$(?<offset>[0-9a-f]{2}) \$(?<opposite>[0-9a-f]{2}) \$00 ; Key door going (?<direction>up|right|down|left)\s*$')) {
        $directionName = $entry.Groups['direction'].Value
        if ($keyDoorFlags.ContainsKey($directionName)) {
            throw "Duplicate _adjacentRoomsData key-door direction $directionName."
        }
        $keyDoorFlags[$directionName] = @(
            $entry.Groups['room'].Value,
            $entry.Groups['opposite'].Value)
    }
    $keyDoorRows = [Collections.Generic.List[string]]::new()
    $dungeonKeyActiveCollisions = if ($pack.Name -eq 'ages') { '1,2,5' } else { '3,4' }
    $keyDoorRows.Add(
        "# closed-tile`tdirection`tkey-kind`tkey-graphic`topen-tile`troom-flag`topposite-room-flag`tpush-counter`tdoor-frame-wait`tdoor-sound`tkey-sound`tno-key-text-id`tno-key-utf8-base64`tactive-collisions")
    $noKeyTexts = @{
        small = [Convert]::ToBase64String(
            [Text.Encoding]::UTF8.GetBytes($allTexts[0x5100]))
        boss = [Convert]::ToBase64String(
            [Text.Encoding]::UTF8.GetBytes($allTexts[0x5101]))
    }
    foreach ($entry in [regex]::Matches(
        $interactableTileDataSource,
        '(?m)^\s*\.db \$(?<tile>7[0-7]) \$(?<parameter>[0-7])2\s*$')) {
        $tile = $entry.Groups['tile'].Value
        $parameter = [Convert]::ToInt32($entry.Groups['parameter'].Value, 16)
        $direction = $parameter -band 3
        $keyKind = if ($parameter -ge 4) { 'boss' } else { 'small' }
        $keyGraphic = if ($keyKind -eq 'boss') { '43' } else { '42' }
        $noKeyTextId = if ($keyKind -eq 'boss') { '5101' } else { '5100' }
        if (-not $keyDoorOpenTiles.ContainsKey($tile)) {
            throw "Dungeon-key door `$$tile has no standard opened-tile substitution."
        }
        $directionName = @('up', 'right', 'down', 'left')[$direction]
        if (-not $keyDoorFlags.ContainsKey($directionName)) {
            throw "Small-key door `$$tile has no _adjacentRoomsData flags for $directionName."
        }
        $roomFlag, $oppositeFlag = $keyDoorFlags[$directionName]
        $keyDoorRows.Add(
            "$tile`t$directionName`t$keyKind`t$keyGraphic`t$($keyDoorOpenTiles[$tile])`t$roomFlag`t$oppositeFlag`t20`t6`t112`t94`t$noKeyTextId`t$($noKeyTexts[$keyKind])`t$dungeonKeyActiveCollisions")
    }
    if ($keyDoorRows.Count -ne 9 -or
        -not ($keyDoorRows -contains "73`tleft`tsmall`t42`ta0`t08`t02`t20`t6`t112`t94`t5100`t$($noKeyTexts.small)`t$dungeonKeyActiveCollisions") -or
        -not ($keyDoorRows -contains "75`tright`tboss`t43`ta0`t02`t08`t20`t6`t112`t94`t5101`t$($noKeyTexts.boss)`t$dungeonKeyActiveCollisions")) {
        throw "Expected eight imported dungeon-key doors `$70-`$77, parsed $($keyDoorRows.Count - 1)."
    }

    $keyBlockText = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[0x5102]))
    $keyBlockRows = @(
        "# closed-tile`tkey-graphic`topen-tile`troom-flag`tpush-counter`topen-sound`tkey-sound`tno-key-text-id`tno-key-utf8-base64`tpuff-sound`tactive-collisions`tsource"
        "1e`t42`ta0`t80`t20`t$($soundIds['SND_OPENCHEST'])`t$($soundIds['SND_GETSEED'])`t5102`t$keyBlockText`t$($soundIds['SND_POOF'])`t$dungeonKeyActiveCollisions`tinteractableTiles.s:nextToKeyBlock"
    )

    # Shared showInfoTextForTile masks are intentionally shared between distinct
    # tile kinds. Preserve source order and formatting through the text resolver.
    function Resolve-TileInfoText([int]$textId,[Collections.Generic.HashSet[int]]$visited) {
        if (-not $allTexts.ContainsKey($textId) -or -not $visited.Add($textId)) {
            throw "showInfoTextForTile: missing/recursive TX_$($textId.ToString('x4'))."
        }
        $message = [string]$allTexts[$textId]
        while ($true) {
            $call = [regex]::Match($message,'\\call\(TX_(?<id>[0-9a-f]{4})\)')
            if (-not $call.Success) { break }
            $target = [Convert]::ToInt32($call.Groups['id'].Value,16)
            $message = $message.Substring(0,$call.Index) + (Resolve-TileInfoText $target $visited) +
                $message.Substring($call.Index + $call.Length)
        }
        $jump = [regex]::Match($message,'\\jump\(TX_(?<id>[0-9a-f]{4})\)')
        if ($jump.Success) {
            $target = [Convert]::ToInt32($jump.Groups['id'].Value,16)
            $message = $message.Substring(0,$jump.Index) + (Resolve-TileInfoText $target $visited)
        } elseif ($allTextFallthroughIds.ContainsKey($textId)) {
            if ($message.EndsWith('\n',[StringComparison]::Ordinal)) {
                $message = $message.Substring(0,$message.Length - 2) + "`n"
            }
            $message += Resolve-TileInfoText ([int]$allTextFallthroughIds[$textId]) $visited
        }
        [void]$visited.Remove($textId)
        return $message
    }
    $tileInfoRows = [Collections.Generic.List[string]]::new()
    $tileInfoRows.Add("# index`tmask`ttext-id`tmessage-utf8-base64`tsource")
    $tileInfoNodes = @(Read-AssemblyNodes (Join-Path $Disassembly 'code/interactableTiles.s') |
        Where-Object { $_.EnclosingGlobalLabel -eq 'showInfoTextForTile' -and $_.Kind -eq 'Data' })
    $tileInfoIndex = 0
    foreach ($node in $tileInfoNodes) {
        if ($node.Name -ne '.db' -or $node.Operands.Count -ne 2 -or
            $node.Operands[0] -notmatch '^\$(?<mask>[0-9a-f]{2})$') {
            throw "showInfoTextForTile@data: unsupported row at $($node.Path):$($node.Line)."
        }
        $mask = $Matches['mask']
        if ($node.Operands[1] -notmatch '^<TX_(?<id>[0-9a-f]{4})$') {
            throw "showInfoTextForTile@data: unsupported text operand $($node.Operands[1])."
        }
        $textId = [Convert]::ToInt32($Matches['id'],16)
        if (-not $allTexts.ContainsKey($textId)) { throw "showInfoTextForTile: unresolved TX_$($textId.ToString('x4'))." }
        $resolved = Resolve-TileInfoText $textId ([Collections.Generic.HashSet[int]]::new())
        $message = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($resolved))
        $tileInfoRows.Add("$tileInfoIndex`t$mask`t$($textId.ToString('x4'))`t$message`tcode/interactableTiles.s:showInfoTextForTile@data+$tileInfoIndex")
        $tileInfoIndex++
    }
    if ($tileInfoIndex -ne 10) { throw "showInfoTextForTile@data: expected10 rows, got$tileInfoIndex." }
    Write-GeneratedTable((Join-Path $destination 'metadata/tile_info_texts.tsv'),$tileInfoRows)
    $nativeInfo = Read-MenuRomBytes $pack 'showInfoTextForTile@data' 20
    for ($index = 0; $index -lt 10; $index++) {
        $fields = $tileInfoRows[$index + 1].Split("`t")
        if ([Convert]::ToInt32($fields[1],16) -ne $nativeInfo[$index*2] -or
            ([Convert]::ToInt32($fields[2],16) -band 255) -ne $nativeInfo[$index*2+1]) {
            throw "$($pack.Name): compiled showInfoTextForTile row$index differs from source."
        }
    }
    Write-GeneratedTable((Join-Path $destination 'objects/dungeon_key_doors.tsv'),$keyDoorRows)
    Write-GeneratedTable((Join-Path $destination 'objects/dungeon_key_blocks.tsv'),$keyBlockRows)
}
