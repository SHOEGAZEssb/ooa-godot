$source = Read-ImportText (Join-Path $Disassembly 'data/ages/staticDungeonObjects.s')
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# dungeon`tslot`troom`ty`tx`tsource")
$counts = @(0, 0, 3, 0, 4, 0, 0, 0, 2, 0, 0, 1, 0, 0, 0, 0)
for ($dungeon = 0; $dungeon -lt 16; $dungeon++) {
    $label = 'dungeon' + $dungeon.ToString('x1') + 'StaticObjects'
    if ($source -notmatch ('(?m)^\s*\.dw ' + $label + '\s*$')) {
        throw "staticDungeonObjects: missing pointer for dungeon $dungeon."
    }
    # Empty labels alias the final $ff; preserve fallthrough until the terminator.
    $body = [regex]::Match($source, '(?ms)^' + $label + ':\s*(?<body>.*?)^\s*\.db \$ff\s*(?:;[^\r\n]*)?$')
    if (-not $body.Success) { throw "${label}: missing static object terminator." }
    $slot = 0
    foreach ($line in $body.Groups['body'].Value -split '\r?\n') {
        $line = ($line -split ';')[0].Trim()
        if (-not $line -or $line -match '^dungeon[0-9a-f]StaticObjects:$') { continue }
        if ($line -notmatch '^\.db \$03,\s*\$(?<room>[0-9a-f]{2}),\s*INTERAC_MINECART,\s*\$00,\s*\$(?<y>[0-9a-f]{2}),\s*\$(?<x>[0-9a-f]{2})$') {
            throw "${label}: unsupported static object: $line"
        }
        $rows.Add("$($dungeon.ToString('x2'))`t$slot`t$($Matches['room'])`t$($Matches['y'])`t$($Matches['x'])`tdata/ages/staticDungeonObjects.s:$label")
        $slot++
    }
    if ($slot -ne $counts[$dungeon]) { throw "${label}: unexpected static object count $slot." }
}
Write-GeneratedTable((Join-Path $destination 'objects/dungeon_static_minecarts.tsv'), $rows)
