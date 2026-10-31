# Both engines use the same floor-cell navigation and eight-byte metadata.
# Retain aliases/source order and verify the selected clean-US pointer streams.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Count = 16 },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Count = 12 }
)) {
    $blocks = [Collections.Generic.List[object]]::new()
    $cells = [Collections.Generic.List[int]]::new()
    foreach ($node in Read-AssemblyNodes (Join-Path $Disassembly "data/$($pack.Name)/dungeonLayouts.s")) {
        if ($node.Kind -in @('Blank','Comment')) { continue }
        if ($node.Kind -eq 'Label') {
            if ($node.Name -eq 'dungeonLayoutDataStart') { continue }
            if ($node.Name -notmatch '^dungeon(?<index>[0-9a-f]{2})Layout$') { throw "$($pack.Name): unsupported dungeon layout label $($node.Name)." }
            if ($cells.Count -gt 0) { $cells = [Collections.Generic.List[int]]::new() }
            $blocks.Add(@{ Index = [Convert]::ToInt32($Matches.index,16); Label = $node.Name; Cells = $cells })
        } elseif ($node.Kind -eq 'Data' -and $node.Name -eq '.db' -and $blocks.Count -gt 0) {
            foreach ($operand in $node.Operands) { $cells.Add((Convert-AssemblyInteger $operand)) }
        } else { throw "$($pack.Name): unsupported dungeon layout source $($node.Span): $($node.Code)." }
    }
    $macros = @(Read-AssemblyMacroInvocations (Join-Path $Disassembly "data/$($pack.Name)/dungeonData.s") '' 'm_DungeonData')
    if ($blocks.Count -ne $pack.Count -or $macros.Count -ne $pack.Count) { throw "$($pack.Name): native dungeon/layout extent changed." }
    $pointers = Read-MenuRomBytes $pack 'dungeonDataTable' ($pack.Count * 2)
    $metadata = @{}
    $properties = @{}
    foreach ($group in @(4,5)) {
        $properties[$group] = [IO.File]::ReadAllBytes((Join-Path $Disassembly "rooms/$($pack.Name)/group${group}DungeonProperties.bin"))
        $compiled = Read-MenuRomBytes $pack "dungeonRoomPropertiesGroup${group}Data" 256
        if (($properties[$group] -join ',') -ne ($compiled -join ',')) { throw "$($pack.Name): dungeon group $group properties differ from compiled US data." }
    }
    foreach ($macro in $macros) {
        $label = $macro.EnclosingGlobalLabel
        if ($label -notmatch '^dungeonData(?<index>[0-9a-f]{2})$' -or $macro.Operands.Count -ne 8) { throw "$($pack.Name): unsupported dungeon metadata $($macro.Code)." }
        $index = [Convert]::ToInt32($Matches.index,16)
        $args = $macro.Operands
        if ($args[0] -notmatch '^>wGroup(?<group>[45])RoomFlags$') { throw "$($pack.Name): unsupported dungeon room-flag owner $($args[0])." }
        $group = [int]$Matches.group
        $data = Read-MenuRomBytes $pack $label 8
        $layout = ($pack.Symbols[$args[2]] - $pack.Symbols['dungeonLayoutDataStart']) / 64
        if ([BitConverter]::ToUInt16($pointers,$index * 2) -ne (($pack.Symbols[$label] -band 0x3fff) + 0x4000) -or
            $data[0] -ne ($pack.Symbols["wGroup${group}RoomFlags"] -shr 8) -or $data[2] -ne $layout) { throw "$($pack.Name): $label native pointer/flag/layout identity changed." }
        foreach ($column in @(1,3,4,5,6,7)) {
            if ($data[$column] -ne (Convert-AssemblyInteger $args[$column])) { throw "$($pack.Name): $label compiled metadata operand $column differs from source." }
        }
        $metadata[$index] = @{ Group = $group; Bytes = $data; Layout = $args[2] }
    }
    $adjacency = [Collections.Generic.List[string]]::new(); $adjacency.Add('# dungeon`troom`tdirection`tneighbor')
    $maps = [Collections.Generic.List[string]]::new(); $maps.Add('# dungeon`tgroup`twallmaster-destination`tfloors`tbase-floor`tcompass-floors`tfloor`tx`ty`troom`tproperties')
    foreach ($block in $blocks) {
        $index = $block.Index; $entry = $metadata[$index]; $data = $entry.Bytes; $cells = $block.Cells
        $compiled = Read-MenuRomBytes $pack $entry.Layout ($data[3] * 64)
        if ($entry.Layout -ne $block.Label -or $cells.Count -ne $data[3] * 64 -or ($compiled -join ',') -ne ($cells -join ',')) {
            throw "$($pack.Name): $($block.Label) does not match its complete compiled floor layout."
        }
        for ($at = 0; $at -lt $cells.Count; $at++) {
            $room = $cells[$at]; if ($room -eq 0) { continue }
            $x = $at % 8; $y = [int][Math]::Floor(($at % 64) / 8); $floor = [int][Math]::Floor($at / 64)
            $maps.Add("$index`t$($entry.Group)`t$($data[1].ToString('x2'))`t$($data[3])`t$($data[4])`t$($data[5].ToString('x2'))`t$floor`t$x`t$y`t$($room.ToString('x2'))`t$($properties[$entry.Group][$room].ToString('x2'))")
            foreach ($edge in @(@('up',0,-1),@('right',1,0),@('down',0,1),@('left',-1,0))) {
                $nx = $x + $edge[1]; $ny = $y + $edge[2]
                if ($nx -lt 0 -or $nx -ge 8 -or $ny -lt 0 -or $ny -ge 8) { continue }
                $neighbor = $cells[$floor * 64 + $ny * 8 + $nx]
                if ($neighbor -ne 0) { $adjacency.Add("$index`t$($room.ToString('x2'))`t$($edge[0])`t$($neighbor.ToString('x2'))") }
            }
        }
    }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/dungeon_adjacency.tsv'),$adjacency)
    Write-GeneratedTable((Join-Path $pack.Root 'objects/dungeon_maps.tsv'),$maps)
}
