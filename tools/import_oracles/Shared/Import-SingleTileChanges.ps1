# Ordered room-flag writes precede standard substitutions in both games.
$singleTileChangeRecords = $null
foreach ($pack in @(
    @{ Name = 'ages'; Root = $destination; Bytes = $romBytes; Symbols = $agesSymbols; Rows = 56 },
    @{ Name = 'seasons'; Root = (Split-Path $previewDestination -Parent); Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Rows = 41 }
)) {
    $path = Join-Path $Disassembly "data/$($pack.Name)/singleTileChanges.s"
    $bytes = [Collections.Generic.List[byte]]::new()
    $labels = @{}
    $pointers = [Collections.Generic.List[string]]::new()
    foreach ($node in Read-AssemblyNodes $path) {
        if ($node.Kind -eq 'Label' -and $node.Name -match '^singleTileChangeGroup[0-7]Data$') {
            $labels[$node.Name] = $bytes.Count
        }
        if ($node.Kind -ne 'Data') { continue }
        if ($node.Name -eq '.dw' -and $node.EnclosingGlobalLabel -eq 'singleTileChangeGroupTable') {
            foreach ($operand in $node.Operands) { $pointers.Add($operand) }
        } elseif ($node.Name -eq '.db' -and $node.Operands.Count -in @(2,4)) {
            foreach ($operand in $node.Operands) { $bytes.Add([byte](Convert-AssemblyInteger $operand)) }
        } else { throw "${path}: unsupported single-tile data row $($node.Code)." }
    }
    if ($labels.Count -ne 8 -or $pointers.Count -ne 8) { throw "${path}: expected eight group pointers and labels." }
    $table = [int]$pack.Symbols['singleTileChangeGroupTable']
    $bank = $table -band -0x4000
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# group`troom`tmask`tposition`ttile`tsource")
    $records = [Collections.Generic.List[object]]::new()
    for ($group = 0; $group -lt 8; $group++) {
        $label = "singleTileChangeGroup${group}Data"
        $pointer = [BitConverter]::ToUInt16($pack.Bytes,$table+$group*2)
        $native = $bank + ($pointer -band 0x3fff)
        if ($pointers[$group] -ne $label -or $pointer -lt 0x4000 -or $pointer -ge 0x8000 -or
            $native -ne $pack.Symbols[$label]) { throw "${path}: compiled group$group pointer differs from source." }
        $cursor = [int]$labels[$label]
        for ($index = 0; ; $index++) {
            if ($index -gt 256 -or $cursor+1 -ge $bytes.Count) { throw "${path}: unterminated $label." }
            if ($bytes[$cursor] -eq 0 -and $bytes[$cursor+1] -eq 0) {
                if ($pack.Bytes[$native] -ne 0 -or $pack.Bytes[$native+1] -ne 0) { throw "${path}: compiled $label terminator differs." }
                break
            }
            if ($cursor+3 -ge $bytes.Count) { throw "${path}: truncated $label row$index." }
            for ($part = 0; $part -lt 4; $part++) {
                if ($bytes[$cursor+$part] -ne $pack.Bytes[$native+$part]) { throw "${path}: compiled $label row$index differs." }
            }
            $room = [int]$bytes[$cursor]; $mask = [int]$bytes[$cursor+1]
            $position = [int]$bytes[$cursor+2]; $tile = [int]$bytes[$cursor+3]
            if ($mask -eq 0 -or $pack.Name -eq 'ages' -and $mask -gt 0x80 -and $mask -notin @(0xf0,0xf1,0xf2)) {
                throw "${path}: invalid $label room$($room.ToString('x2')) mask$($mask.ToString('x2'))."
            }
            $source = "singleTileChanges.s:$label"
            $rows.Add("$group`t$($room.ToString('x2'))`t$($mask.ToString('x2'))`t$($position.ToString('x2'))`t$($tile.ToString('x2'))`t$source")
            $records.Add([pscustomobject]@{ Group=$group; Room=$room; Mask=$mask; Position=$position; Tile=$tile; Source=$source })
            $cursor += 4; $native += 4
        }
    }
    if ($records.Count -ne $pack.Rows) { throw "${path}: expected$($pack.Rows) single-tile rows, got$($records.Count)." }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/single_tile_changes.tsv'),$rows)
    if ($pack.Name -eq 'ages') { $singleTileChangeRecords = $records }
}
