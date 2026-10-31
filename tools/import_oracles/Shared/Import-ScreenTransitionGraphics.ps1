# bank1.s:updateTilesetUniqueGfx consumes one native header entry per update.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Tilesets = 103; Headers = 21 },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Tilesets = 99; Headers = 41 }
)) {
    foreach ($label in @('tilesetData', 'uniqueGfxHeaderTable')) {
        if (-not $pack.Symbols.ContainsKey($label)) { throw "$($pack.Name): missing $label." }
    }
    $table = [int]$pack.Symbols['uniqueGfxHeaderTable']
    $bankBase = [int][Math]::Floor($table / 0x4000) * 0x4000 - 0x4000
    $uploads = [Collections.Generic.List[string]]::new()
    $uploads.Add("# unique-gfx`torder`tvram-address`ttiles`tpalette-header`tdata`tsource")
    $counts = @{}
    for ($id = 0; $id -lt $pack.Headers; $id++) {
        $pointer = [BitConverter]::ToUInt16($pack.Bytes, $table + $id * 2)
        $label = 'uniqueGfxHeader' + $id.ToString('x2')
        if (-not $pack.Symbols.ContainsKey($label) -or $pack.Symbols[$label] -ne $bankBase + $pointer) {
            throw "$($pack.Name): uniqueGfxHeaderTable[$($id.ToString('x2'))] disagrees with $label."
        }
        $order = 0
        do {
            if ($pointer -lt 0x4000 -or $pointer -gt 0x7ffa -or $order -ge 16) { throw "$($pack.Name): invalid $label entry $order." }
            $entry = $bankBase + $pointer
            $bankMode = [int]$pack.Bytes[$entry]
            if ($bankMode -eq 0) {
                $uploads.Add(("{0:x2}`t{1}`t0000`t0`t{2:x2}`t-`tdata/{3}/uniqueGfxHeaders.s:{4}" -f $id, $order, $pack.Bytes[$entry + 1], $pack.Name, $label))
                $order++; break
            }
            $address = ([int]$pack.Bytes[$entry + 3] -shl 8) -bor ($pack.Bytes[$entry + 4] -band 0xf0)
            $tiles = ($pack.Bytes[$entry + 5] -band 0x7f) + 1
            if (($pack.Bytes[$entry + 4] -band 0x0f) -ne 1 -or $address -lt 0x8800 -or $address + $tiles * 16 -gt 0x9800) {
                throw "$($pack.Name): $label writes outside BG bank 1."
            }
            $source = (($bankMode -band 0x3f) * 0x4000) + (([int]$pack.Bytes[$entry + 1] -band 0x3f) -shl 8) + $pack.Bytes[$entry + 2]
            $data = Expand-TransitionGraphics $pack.Bytes $source $tiles ($bankMode -shr 6)
            $uploads.Add(("{0:x2}`t{1}`t{2:x4}`t{3}`t-`t{4}`tdata/{5}/uniqueGfxHeaders.s:{6}@{7:x4}" -f $id, $order, $address, $tiles, ([BitConverter]::ToString($data)).Replace('-', ''), $pack.Name, $label, $pointer))
            $more = $pack.Bytes[$entry + 5] -band 0x80
            $pointer += 6; $order++
        } while ($more -ne 0)
        $counts[$id] = $order
    }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/screen_transition_uploads.tsv'), $uploads)
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# tileset`tstate-modifier`tunique-gfx`tentries`tpalette-header`tsource")
    for ($tileset = 0; $tileset -lt $pack.Tilesets; $tileset++) {
        $at = [int]$pack.Symbols['tilesetData'] + $tileset * 8
        $seasonal = $pack.Bytes[$at] -eq 0xff
        if ($seasonal -and $pack.Name -ne 'seasons') { throw 'Ages has no native seasonal tilesets.' }
        $states = if ($seasonal) { @(0..3) } else { @(-1) }
        if ($seasonal) {
            $pointer = [BitConverter]::ToUInt16($pack.Bytes, $at + 1)
            $label = 'tileset' + $tileset.ToString('x2') + 'Seasons'
            $at = [int][Math]::Floor($at / 0x4000) * 0x4000 + ($pointer -band 0x3fff)
            if (-not $pack.Symbols.ContainsKey($label) -or $at -ne $pack.Symbols[$label] -or $pointer -lt 0x4000 -or $pointer -gt 0x7fe0) {
                throw "Seasons tileset $($tileset.ToString('x2')): invalid seasonal pointer."
            }
        }
        foreach ($state in $states) {
            $index = if ($seasonal) { $state * 8 } else { 0 }
            $unique = [int]$pack.Bytes[$at + $index + 2]
            if (-not $counts.ContainsKey($unique)) { throw "$($pack.Name): tileset $($tileset.ToString('x2')) has unknown unique header $($unique.ToString('x2'))." }
            $rows.Add(("{0:x2}`t{1}`t{2:x2}`t{3}`t{4:x2}`tdata/{5}/tilesets.s:tilesetData/{0:x2}" -f $tileset, $state, $unique, $counts[$unique], $pack.Bytes[$at + $index + 4], $pack.Name))
        }
    }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/screen_transition_graphics.tsv'), $rows)
}
