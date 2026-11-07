# Native base identities plus explicitly implemented common handler variants.
# Unimplemented rows do not infer handler-specific subid ranges.
$pack = @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent) }
$connection = Start-AssemblySourceHost 'ROM_SEASONS;SEASONS_ENGINE;REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES'
try {
    $definitions = @{}
    foreach ($relative in @('constants/common/enemies.s','constants/seasons/enemies.s')) {
        $nodes = @((Invoke-AssemblySourceHost $connection 'NODES' (Join-Path $Disassembly $relative) | ConvertFrom-Json) | Where-Object IsActive)
        foreach ($node in $nodes | Where-Object { $_.Kind -eq 'Constant' -and $_.Name -cmatch '^ENEMY_' }) {
            if ($node.Operands.Count -ne 1) { throw "${relative}: unsupported native enemy constant $($node.Code)." }
            $id = Convert-AssemblyInteger $node.Operands[0]
            if ($id -lt 0 -or $id -ge 128 -or $definitions.ContainsKey($id)) { throw "${relative}: invalid/duplicate native enemy ID $id." }
            $definitions[$id] = @{ Name = $node.Name; Source = "${relative}:$($node.Name)" }
        }
    }
    $dispatchPath = Join-Path $Disassembly 'data/enemyCodeTable.s'
    $dispatch = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$dispatchPath`0enemyCodeTable`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
    $dataPath = Join-Path $Disassembly 'data/seasons/enemyData.s'
    $data = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$dataPath`0enemyData`0m_EnemyData" | ConvertFrom-Json) | Where-Object IsActive)
    if ($definitions.Count -ne 128 -or $dispatch.Count -ne 128 -or $data.Count -ne 128) { throw "Seasons requires all `$80 native enemy constants, dispatch pointers and data rows (got $($definitions.Count)/$($dispatch.Count)/$($data.Count))." }
    $pointers = Read-MenuRomBytes $pack 'enemyCodeTable' 256
    $nativeData = Read-MenuRomBytes $pack 'enemyData' 512
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add('# id`tsubid`tcollision-mode`tclassification`thandler`tenemy-name`tsource')
    for ($id = 0; $id -lt 128; $id++) {
        $target = $dispatch[$id].Operands[0]
        if ($target -eq 'enemyCodeNil') { $label = $target; $bank = 0 }
        elseif ($target -match '^bank(?<bank>[0-9a-f]{2})\.(?<label>enemyCode[0-9a-f]{2})$') {
            $label = $Matches.label; $bank = [Convert]::ToInt32($Matches.bank,16)
        }
        else { throw "Seasons enemy `$$($id.ToString('x2')) has an unsupported dispatch operand $target." }
        $address = $seasonsSymbols[$label] -band 0x3fff
        if ($bank -ne 0) { $address += 0x4000 }
        if ($dispatch[$id].Operands.Count -ne 1 -or -not $seasonsSymbols.ContainsKey($label) -or
            [int][Math]::Floor($seasonsSymbols[$label] / 0x4000) -ne $bank -or
            [BitConverter]::ToUInt16($pointers,$id * 2) -ne $address) { throw "Seasons enemy `$$($id.ToString('x2')) has an unresolved native dispatch pointer $target." }
        $operands = $data[$id].Operands
        if ($operands.Count -notin @(3,4) -or (Convert-AssemblyInteger $operands[0]) -ne $nativeData[$id * 4] -or
            (Convert-AssemblyInteger $operands[1]) -ne $nativeData[$id * 4 + 1]) { throw "Seasons enemyData row `$$($id.ToString('x2')) differs from compiled native data." }
        $mode = $nativeData[$id * 4 + 1]
        if (($mode -band 0x7f) -ge 0x73) { throw "Seasons enemy `$$($id.ToString('x2')) collision mode is outside objectCollisionTable." }
        $definition = $definitions[$id]
        $source = "$($definition.Source);data/enemyCodeTable.s:enemyCodeTable+`$$((2 * $id).ToString('x4'))->${target};data/seasons/enemyData.s:enemyData+`$$((4 * $id).ToString('x4'))"
        $subids = switch ($id) { 0x78 { @(0,1,2,3) }; 0x35 { @(0,1,2) }; 0x70 { @(0,1) }; 0x0e { @(0,1,2,5) }; 0x0c { @(0,1) }; 0x20 { @(0,1) }; 0x22 { @(0,1) }; 0x09 { @(0,1,2) }; 0x10 { @(0,1,2,3) }; 0x17 { @(0,1,2) }; 0x34 { @(0,1) }; 0x32 { @(0,1) }; 0x51 { @(0,2,3) }; 0x2d { @(0,2,3) }; default { @(0) } }
        foreach ($subid in $subids) {
            $handler = switch ($id) { 0x78 { 'aquamentus' }; 0x35 { 'floormaster' }; 0x70 { 'brother-goriyas' }; 0x0a { 'boomerang-moblin' }; 0x0e { 'blade-trap' }; 0x08 { 'river-zora' }; 0x0c { 'arrow-moblin' }; 0x20 { 'arrow-moblin' }; 0x22 { 'arrow-moblin' }; 0x09 { 'octorok' }; 0x10 { 'rope' }; 0x16 { 'beamos' }; 0x17 { 'ghini' }; 0x34 { 'zol' }; 0x43 { 'gel' }; 0x31 { 'stalfos' }; 0x32 { 'keese' }; 0x51 { if ($subid -in @(2,3)) { 'beetle' } else { '-' } }; 0x2d { 'podoboo-tower' }; default { '-' } }
            $classification = if ($handler -eq '-') { 'unimplemented' } else { 'ordered-implemented' }
            $rows.Add("$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$($mode.ToString('x2'))`t$classification`t$handler`t$($definition.Name)`t$source")
        }
    }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/enemy_handler_registry.tsv'),$rows)
}
finally {
    [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
    $connection.WaitForExit(); $connection.Dispose()
}
