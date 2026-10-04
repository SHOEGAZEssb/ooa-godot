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
