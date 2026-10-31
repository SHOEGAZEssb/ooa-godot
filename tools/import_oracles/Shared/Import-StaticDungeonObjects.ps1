foreach ($pack in @(
    @{ Name='ages'; Root=$destination; Bytes=$romBytes; Symbols=$agesSymbols },
    @{ Name='seasons'; Root=(Split-Path $previewDestination -Parent); Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols }
)) {
    $source = Read-ImportText (Join-Path $Disassembly "data/$($pack.Name)/staticDungeonObjects.s")
    $table = [regex]::Match($source, '(?m)^staticDungeonObjects:\s*(?<pointers>(?:\s*\.dw \w+\s*)+)')
    $labels = @([regex]::Matches($table.Groups['pointers'].Value, '\.dw (\w+)') | ForEach-Object { $_.Groups[1].Value })
    if (-not $table.Success -or $labels.Count -ne $(if ($pack.Name -eq 'ages') { 16 } else { 12 })) {
        throw "$($pack.Name): staticDungeonObjects pointer count changed."
    }
    $bytes = $pack.Bytes; $symbols = $pack.Symbols
    $clear = [int]$symbols['clearStaticObjects']; $start = [int]$symbols['wStaticObjects']
    if ($bytes[$clear] -ne 0x21 -or $bytes[$clear+1] -ne ($start -band 255) -or
        $bytes[$clear+2] -ne ($start -shr 8) -or $bytes[$clear+3] -ne 6 -or
        $bytes[$clear+4] -ne $(if ($pack.Name -eq 'ages') { 0x40 } else { 0x80 })) {
        throw "$($pack.Name): clearStaticObjects buffer address/span changed."
    }
    $slots = [int]$bytes[$clear+4] / 8
    Write-GeneratedTable((Join-Path $pack.Root 'objects/static_object_layout.tsv'), @(
        '# start`tslots`tdungeons`tsource',
        "$($start.ToString('x4'))`t$slots`t$($labels.Count)`tinclude/wram.s:wStaticObjects;code/bank0.s:clearStaticObjects"
    ))
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add('# dungeon`trecords-base64`tsource')
    $pointerTable = [int]$symbols['staticDungeonObjects']
    for ($dungeon = 0; $dungeon -lt $labels.Count; $dungeon++) {
        $at = $pointerTable + $dungeon * 2
        $pointer = [int]$bytes[$at] -bor ([int]$bytes[$at+1] -shl 8)
        if ($pointer -lt 0x4000 -or $pointer -ge 0x8000) { throw "$($pack.Name): invalid static object pointer. $dungeon" }
        $at = ($pointerTable -band -16384) + ($pointer -band 0x3fff)
        if ($at -ne $symbols[$labels[$dungeon]]) { throw "$($pack.Name): static dungeon pointer/source mismatch. $dungeon" }
        $records = [Collections.Generic.List[byte]]::new()
        while ($true) {
            if ($at -ge $bytes.Length -or ($at -shr 14) -ne ($pointerTable -shr 14)) {
                throw "$($pack.Name): static object list escaped its ROM bank. $dungeon"
            }
            if ($bytes[$at] -eq 0xff) { break }
            if ($records.Count -ge $slots * 6 -or $at+5 -ge $bytes.Length -or
                (($at+5) -shr 14) -ne ($pointerTable -shr 14) -or $bytes[$at] -notin @(1,2,3,4,5)) {
                throw "$($pack.Name): unsupported/overflowing static object record. $dungeon at $at"
            }
            for ($offset=0; $offset -lt 6; $offset++) { $records.Add($bytes[$at+$offset]) }
            $at += 6
        }
        $encoded = if ($records.Count -eq 0) { '-' } else { [Convert]::ToBase64String($records.ToArray()) }
        $rows.Add("$($dungeon.ToString('x2'))`t$encoded`tdata/$($pack.Name)/staticDungeonObjects.s:$($labels[$dungeon])")
    }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/dungeon_static_objects.tsv'), $rows)
}
