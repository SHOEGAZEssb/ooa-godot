# Link tile types and object hazards are separate source lookups. Retain native
# collision-mode order, aliases and duplicate rows: lookupCollisionTable uses
# the first matching tile (Seasons' side-view $0c occurs twice).
function Get-TilePropertyPointerOffset([byte[]]$bytes, [int]$at, [int]$table, [string]$source) {
    $pointer = [int]$bytes[$at] -bor ([int]$bytes[$at + 1] -shl 8)
    $bank = [int][Math]::Floor($table / 0x4000)
    if (($bank -eq 0 -and $pointer -ge 0x4000) -or
        ($bank -ne 0 -and ($pointer -lt 0x4000 -or $pointer -ge 0x8000))) {
        throw "${source}: invalid local table pointer `$$(($pointer).ToString('x4'))."
    }
    return $bank * 0x4000 + ($pointer -band 0x3fff)
}

function Read-CollisionTableStreams($pack, [string]$path, [string]$symbol, [int]$width) {
    if ($width -notin @(1,2) -or -not $pack.Symbols.ContainsKey($symbol)) { throw "${path}: invalid collision table $symbol/width $width." }
    $table = [int]$pack.Symbols[$symbol]
    $pointers = @(Read-AssemblyDataDirectives (Join-Path $Disassembly $path) $symbol '.dw' | ForEach-Object { $_.Operands })
    if ($pointers.Count -ne 6) { throw "${path}: expected six native collision-mode pointers." }
    $streams = @()
    for ($mode = 0; $mode -lt 6; $mode++) {
        $local = $pointers[$mode]; $source = "${path}:$symbol$local"
        $at = Get-TilePropertyPointerOffset $pack.Bytes ($table + $mode * 2) $table $source
        if (-not $pack.Symbols.ContainsKey("$symbol$local") -or $at -ne $pack.Symbols["$symbol$local"]) { throw "${source}: compiled pointer does not resolve to its source label." }
        $end = ([int][Math]::Floor($at / 0x4000) + 1) * 0x4000
        $entries = [Collections.Generic.List[object]]::new()
        while ($at -lt $end -and $pack.Bytes[$at] -ne 0) {
            if ($at + $width -gt $end -or $entries.Count -ge 256) { throw "${source}: unterminated collision stream." }
            $entries.Add([byte[]]$pack.Bytes[$at..($at + $width - 1)])
            $at += $width
        }
        if ($at -ge $end) { throw "${source}: collision terminator crossed the source bank." }
        $streams += @{ Mode = $mode; Name = $local.TrimStart('@'); Source = $source; Rows = $entries.ToArray() }
    }
    return ,$streams
}

foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Destination = $destination },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Destination = (Split-Path $previewDestination -Parent) }
)) {
    $modeRows = [Collections.Generic.List[string]]::new()
    $modeRows.Add("# mode`tname`tside-scrolling`tsource")
    foreach ($spec in @(
        @('tileTypeMappings', 'tileTypesTable', 'tile_types'),
        @('hazards', 'hazardCollisionTable', 'tile_hazards')
    )) {
        $path = "data/$($pack.Name)/tile_properties/$($spec[0]).s"
        $symbol = $spec[1]
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# mode`tindex`ttile`tvalue`tsource")
        foreach ($stream in (Read-CollisionTableStreams $pack $path $symbol 2)) {
            $mode = $stream.Mode; $source = $stream.Source
            if ($spec[0] -eq 'tileTypeMappings') {
                $sideScrolling = [int]($stream.Name -eq 'sidescrolling')
                $modeRows.Add("$mode`t$($stream.Name)`t$sideScrolling`t$source")
            }
            $index = 0
            foreach ($entry in $stream.Rows) {
                $tile = [int]$entry[0]; $value = [int]$entry[1]
                if ($spec[0] -eq 'hazards' -and $value -notin @(1, 2, 4, 0x42)) {
                    throw "${source}: unsupported native hazard `$$(($value).ToString('x2'))."
                }
                $rows.Add("$mode`t$index`t$($tile.ToString('x2'))`t$($value.ToString('x2'))`t$source+$((2 * $index).ToString('x2'))")
                $index++
            }
        }
        Write-GeneratedTable((Join-Path $pack.Destination "metadata/$($spec[2]).tsv"), $rows)
    }
    Write-GeneratedTable((Join-Path $pack.Destination 'metadata/collision_modes.tsv'), $modeRows)
}
