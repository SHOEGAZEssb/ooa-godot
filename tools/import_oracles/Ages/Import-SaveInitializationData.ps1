
# File creation applies the common writes, then one variant; standard falls
# through into the hero defaults before its first terminator.
$fileInitPath = Join-Path $Disassembly 'code/fileManagement.s'
$fileInitWram = Read-ImportText (Join-Path $Disassembly 'include/wram.s')
$fileInitStruct = [regex]::Match((Read-ImportText (Join-Path $Disassembly 'include/structs.s')),
    '(?ms)^\.STRUCT DeathRespawnStruct\s*(?<body>.*?)^\.ENDST')
$fileInitMembers = @([regex]::Matches($fileInitStruct.Groups['body'].Value, '(?m)^\s*(\w+)\s+db\b') | ForEach-Object { $_.Groups[1].Value })
$fileInitConstants = @{}
foreach ($constantPath in @('treasure', 'items')) {
    $enum = [regex]::Match((Read-ImportText (Join-Path $Disassembly "constants/common/$constantPath.s")), '(?ims)^\.enum 0\s*(?<body>.*?)^\.ende')
    $enumIndex = 0
    foreach ($row in [regex]::Matches($enum.Groups['body'].Value, '(?m)^\s*(?<name>\w+)\s+db\b')) {
        $fileInitConstants[$row.Groups['name'].Value] = $enumIndex++
    }
}
if ($fileInitConstants['TREASURE_PUNCH'] -ne 2 -or $fileInitConstants['TREASURE_SWORD'] -ne 5 -or $fileInitConstants['ITEM_SWORD'] -ne 5) {
    throw 'fileManagement.s initialization treasure/item enum contract changed.'
}
$fileInitRows = [Collections.Generic.List[string]]::new()
$fileInitRows.Add("# variant`tindex`taddress`tvalue`tsource")
foreach ($variant in @('common', 'standardGame', 'heroGame', 'linkedGame')) {
    $label = if ($variant -eq 'common') { 'initialFileVariables' } else { "initialFileVariables_$variant" }
    $nodes = @(Read-AssemblyDataDirectives $fileInitPath $label '.db')
    if ($variant -eq 'standardGame') { $nodes += @(Read-AssemblyDataDirectives $fileInitPath 'initialFileVariables_heroGame' '.db') }
    $index = 0; $terminated = $false
    foreach ($node in $nodes) {
        if ($terminated) { throw "fileManagement.s:$label data after terminator." }
        if ($node.Operands.Count -eq 1 -and (Convert-AssemblyInteger $node.Operands[0]) -eq 0) { $terminated = $true; continue }
        if ($node.Operands.Count -ne 2 -or $node.Operands[0] -notmatch '^<(?<symbol>\w+)(?:\.(?<member>\w+)|\+(?<offset>\d+))?$') {
            throw "fileManagement.s:$label unsupported initialization row $($node.OperandText)."
        }
        $symbol = $Matches['symbol']; $member = $Matches['member']; $offset = [int]$Matches['offset']
        if ($member) {
            $offset = [Array]::IndexOf($fileInitMembers, $member)
            if ($symbol -ne 'wDeathRespawnBuffer' -or $offset -lt 0) { throw "fileManagement.s:$label unknown member $symbol.$member." }
        }
        $addressMatch = [regex]::Match($fileInitWram, '(?m)^' + [regex]::Escape($symbol) + ':\s*;\s*\$(?<address>c6[0-9a-f]{2})\b')
        if (-not $addressMatch.Success) { throw "fileManagement.s:$label unresolved WRAM symbol $symbol." }
        $address = [Convert]::ToInt32($addressMatch.Groups['address'].Value, 16) + $offset
        $operand = $node.Operands[1] -replace '\s', ''
        if ($operand -eq 'ITEM_SWORD') { $value = $fileInitConstants['ITEM_SWORD'] }
        elseif ($operand -in @('1<<TREASURE_PUNCH', '(1<<TREASURE_PUNCH)|(1<<TREASURE_SWORD)')) {
            $value = 1 -shl $fileInitConstants['TREASURE_PUNCH']
            if ($operand.Contains('TREASURE_SWORD')) { $value = $value -bor (1 -shl $fileInitConstants['TREASURE_SWORD']) }
        }
        else { $value = Convert-AssemblyInteger $operand }
        $fileInitRows.Add("$variant`t$index`t$($address.ToString('x4'))`t$(([int]$value).ToString('x2'))`tcode/fileManagement.s:$label / include/wram.s:$symbol")
        $index++
    }
    if (-not $terminated -or $index -eq 0) { throw "fileManagement.s:$label missing initialization rows or terminator." }
}
Write-GeneratedTable((Join-Path $destination 'metadata/new_file_initialization.tsv'), $fileInitRows)
