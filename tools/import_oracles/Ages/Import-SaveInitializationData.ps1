# bank1.initializeGame repairs a non-GBA checkpoint through preset $03.
# Preserve the preset's partial-write mask: modifier/companion bytes survive.
$saveInitializationSource = Read-ImportText (Join-Path $Disassembly 'code\bank1.s')
$saveInitializationBranch = [regex]::Match($saveInitializationSource,
    '(?ms)^initializeGame:.*?ldh a,\(<hGameboyType\).*?rlca\s+jr c,\+\+\+.*?\.ifdef ROM_AGES\s+ld bc,\$(?<shop>[0-9a-f]{4}).*?\.ifdef ROM_AGES\s+ld bc,\$(?<outside>[0-9a-f]{4}).*?ld a,\(wDeathRespawnBuffer.x\)\s+cp \$(?<minimum>[0-9a-f]{2})\s+jr c,\+\+\+\s+@fixRespawnForGbc:\s+ld c,\$(?<preset>[0-9a-f]{2})\s+call loadDeathRespawnBufferPreset')
$respawnPresetBranch = [regex]::Match($saveInitializationSource,
    '(?ms)^@respawnBuffers:\s*\.ifdef ROM_AGES\s*(?<body>.*?)^\.else')
if (-not $saveInitializationBranch.Success -or -not $respawnPresetBranch.Success -or
    $saveInitializationSource -notmatch '(?ms)^loadDeathRespawnBufferPreset:.*?call multiplyABy8.*?ld de,wDeathRespawnBuffer-1.*?sla b\s+jr nc,\+\s+ld \(de\),a.*?jr nz,--') {
    throw 'code/bank1.s:initializeGame checkpoint correction or masked preset dispatch changed.'
}
$respawnPresetRows = [regex]::Matches($respawnPresetBranch.Groups['body'].Value, '(?m)^\s*\.db(?<bytes>(?:\s+\$[0-9a-f]{2}){8})\s*$')
$respawnPresetIndex = [Convert]::ToInt32($saveInitializationBranch.Groups['preset'].Value, 16)
if ($respawnPresetRows.Count -ne 4 -or $respawnPresetIndex -ne 3) {
    throw 'code/bank1.s:@respawnBuffers expected four Ages presets and repair preset $03.'
}
$respawnRepairBytes = @([regex]::Matches($respawnPresetRows[$respawnPresetIndex].Groups['bytes'].Value, '\$([0-9a-f]{2})') |
    ForEach-Object { $_.Groups[1].Value })
$respawnRepairRows = [Collections.Generic.List[string]]::new()
$respawnRepairRows.Add("# source_group`tsource_room`tminimum_x`twrite_mask`tgroup`troom`tmodifier`tfacing`ty`tx`tcompanion_id`tsource")
foreach ($respawnRepairKey in @('shop', 'outside')) {
    $respawnRepairPair = $saveInitializationBranch.Groups[$respawnRepairKey].Value
    $respawnRepairMinimum = if ($respawnRepairKey -eq 'shop') { '00' } else { $saveInitializationBranch.Groups['minimum'].Value }
    $respawnRepairRows.Add(($respawnRepairPair.Substring(0, 2), $respawnRepairPair.Substring(2, 2),
        $respawnRepairMinimum, ($respawnRepairBytes -join "`t"), 'code/bank1.s:initializeGame/@respawnBuffers[03]') -join "`t")
}
Write-GeneratedTable((Join-Path $destination 'metadata\death_respawn_initialization.tsv'), $respawnRepairRows)
$presetRows=[Collections.Generic.List[string]]::new();$presetRows.Add("# index`tmask`tvalues`tsource")
for($index=0;$index -lt $respawnPresetRows.Count;$index++) {
    $bytes=@([regex]::Matches($respawnPresetRows[$index].Groups['bytes'].Value,'\$([0-9a-f]{2})')|ForEach-Object {$_.Groups[1].Value})
    $presetRows.Add("$index`t$($bytes[0])`t$($bytes[1..7] -join ',')`tcode/bank1.s:loadDeathRespawnBufferPreset@respawnBuffers")
}
Write-GeneratedTable((Join-Path $destination 'metadata/death_respawn_presets.tsv'),$presetRows)

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
