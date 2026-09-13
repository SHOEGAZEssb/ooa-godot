$platformSource = Read-ImportText (Join-Path $Disassembly 'data/ages/movingPlatformScriptTable.s')
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# dungeon`tscript`tcommands`tsource")
foreach ($first in @('00', '02')) {
    $next = if ($first -eq '00') { '02' } else { '05' }
    $body = [regex]::Match($platformSource, "(?ms)^@dungeon${first}:\s*(?<body>.*?)(?=^@dungeon${next}:)").Groups['body'].Value
    $aliases = @($first) + @([regex]::Matches($body, '(?m)^@dungeon([0-9a-f]{2}):') | ForEach-Object { $_.Groups[1].Value })
    $pointers = @([regex]::Matches($body, '(?m)^\s*\.dw (@@platform[0-9]+)') | ForEach-Object { $_.Groups[1].Value })
    $expected = if ($first -eq '00') { 2 } else { 6 }
    if ($pointers.Count -ne $expected -or $aliases.Count -ne $(if ($first -eq '00') { 2 } else { 3 })) {
        throw "movingPlatformScriptTable.s @dungeon${first}: unexpected aliases or platform pointers."
    }
    for ($index = 0; $index -lt $pointers.Count; $index++) {
        $label = $pointers[$index]
        $scriptBody = [regex]::Match($body, "(?ms)^${label}:\s*(?<body>.*?)(?=^@@platform|\z)").Groups['body'].Value
        $commands = [Collections.Generic.List[string]]::new()
        $anonymous = -1
        foreach ($rawLine in $scriptBody -split '\r?\n') {
            $line = ($rawLine -split ';')[0].Trim()
            if (-not $line) { continue }
            if ($line -eq '--') { $anonymous = $commands.Count; continue }
            if ($line -match '^plat_jump\s+(?<label>@@platform[0-9]+|--)$') {
                $target = if ($Matches['label'] -eq '--') { $anonymous } elseif ($Matches['label'] -eq $label) { 0 } else { -1 }
                if ($target -lt 0) { throw "${label}: unresolved jump $line." }
                $commands.Add("04:$($target.ToString('x2'))")
            }
            elseif ($line -match '^plat_(?<op>wait|up|right|down|left)\s+\$(?<value>[0-9a-f]{2})$') {
                $opcode = @{ wait = '00'; up = '08'; right = '09'; down = '0a'; left = '0b' }[$Matches['op']]
                $commands.Add("${opcode}:$($Matches['value'])")
            }
            else { throw "movingPlatformScriptTable.s ${label}: unsupported source command $line." }
        }
        if ($commands.Count -lt 5 -or $commands[0] -ne '00:08' -or $commands[-1] -notmatch '^04:') {
            throw "${label}: missing initial wait or terminal native loop."
        }
        foreach ($dungeon in $aliases) {
            $rows.Add("$dungeon`t$($index.ToString('x2'))`t$($commands -join ',')`tdata/ages/movingPlatformScriptTable.s:@dungeon$first/$label")
        }
    }
}
Write-GeneratedTable((Join-Path $destination 'objects/moving_platform_scripts.tsv'), $rows)
