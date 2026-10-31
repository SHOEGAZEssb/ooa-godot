# checkTileValidForEnemySpawn follows the selected collision-mode pointers,
# including aliases and the list falling through to the side-view terminator.
foreach ($pack in @(
    @{ Name='ages'; Bytes=$romBytes; Symbols=$agesSymbols; Root=$destination },
    @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Root=(Split-Path $previewDestination -Parent) }
)) {
    $path = "data/$($pack.Name)/tile_properties/enemyUnspawnableTiles.s"
    $bytes = [byte[]]::new(6 * 256)
    $count = 0
    foreach ($stream in (Read-CollisionTableStreams $pack $path 'enemyUnspawnableTilesTable' 2)) {
        foreach ($row in $stream.Rows) {
            $at = $stream.Mode * 256 + $row[0]
            if ($row[1] -ne 1 -or $bytes[$at] -ne 0) { throw "$($stream.Source): invalid or duplicate native unspawnable tile `$$(($row[0]).ToString('x2'))." }
            $bytes[$at] = 1; $count++
        }
    }
    $expected = if ($pack.Name -eq 'ages') { 63 } else { 52 }
    if ($count -ne $expected) { throw "$($pack.Name): expected $expected native unspawnable tile records, got $count." }
    $target = Join-Path $pack.Root 'metadata/enemyUnspawnableTiles.bin'
    if ($pack.Name -eq 'ages' -and -not [Linq.Enumerable]::SequenceEqual([byte[]][IO.File]::ReadAllBytes($target),$bytes)) {
        throw 'Ages unspawnable tiles differ from the independent compiled-pointer decoder.'
    }
    Write-GeneratedBytes($target,$bytes)
}
