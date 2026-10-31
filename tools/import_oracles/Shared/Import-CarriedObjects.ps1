# Native weight and held-position tables serve Bomb, Bracelet and other carried items.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $path = Join-Path $Disassembly 'object_code/common/items/commonBombAndBraceletCode.s'
        $selected = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0itemWeights`0.db" | ConvertFrom-Json) | Where-Object IsActive)
        if ($selected.Count -ne 6 -or @($selected | Where-Object { $_.Operands.Count -ne 4 }).Count -ne 0) { throw "$($pack.Name): native itemWeights must contain six four-byte records." }
        $bytes = Read-MenuRomBytes $pack 'itemWeights' 24
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# weight`tgravity`tinitial-speed-z`tspeed-raw`ttoss-speed-raw`tsource")
        for ($weight = 0; $weight -lt 6; $weight++) {
            $at = $weight * 4
            $rows.Add("$weight`t$([int]$bytes[$at])`t$([int]$bytes[$at + 1] - 256)`t$([int]$bytes[$at + 2])`t$([int]$bytes[$at + 3])`tobject_code/common/items/commonBombAndBraceletCode.s:itemWeights+$at")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/bracelet_weights.tsv'),$rows)
        $path = Join-Path $Disassembly 'object_code/common/itemParents/commonCode.s'
        $selected = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0@liftedObjectPositions`0.db" | ConvertFrom-Json) | Where-Object IsActive)
        if ($selected.Count -ne 20 -or @($selected | Where-Object { $_.Operands.Count -ne 8 }).Count -ne 0) { throw "$($pack.Name): native held positions require five weights, four frames and four Z/X pairs." }
        $bytes = Read-MenuRomBytes $pack 'updateGrabbedObjectPosition@liftedObjectPositions' 160
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# weight`tframe`tdirection`tz`tx`tsource")
        for ($weight = 0; $weight -lt 5; $weight++) {
            for ($frame = 0; $frame -lt 4; $frame++) {
                for ($direction = 0; $direction -lt 4; $direction++) {
                    $at = $weight * 32 + $frame * 8 + $direction * 2
                    $z = [int]$bytes[$at]; $x = [int]$bytes[$at + 1]
                    if ($z -ge 128) { $z -= 256 }; if ($x -ge 128) { $x -= 256 }
                    $rows.Add("$weight`t$frame`t$direction`t$z`t$x`tobject_code/common/itemParents/commonCode.s:@liftedObjectPositions+$at")
                }
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/bracelet_weight_offsets.tsv'),$rows)
        $offsets = Read-MenuRomBytes $pack '_getLinkPositionPlusDirectionOffset@positionOffsets' 8
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# direction`tx`ty`tz-subtract`tz-radius`tsource")
        for ($direction = 0; $direction -lt 4; $direction++) {
            $y = [int]$offsets[$direction * 2]; $x = [int]$offsets[$direction * 2 + 1]
            if ($x -ge 128) { $x -= 256 }; if ($y -ge 128) { $y -= 256 }
            $rows.Add("$direction`t$x`t$y`t3`t7`tcode/bank0.s:_getLinkPositionPlusDirectionOffset;_checkCollisionWithHAndD")
        }
        $path = Join-Path $Disassembly 'code/bank0.s'
        foreach ($spec in @(@('_getLinkPositionPlusDirectionOffset','sub \$03'),@('_checkCollisionWithHAndD','bit 7,\(hl\)[\s\S]*add \$07[\s\S]*cp \$0e'))) {
            $selected = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$path`0$($spec[0])`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' })
            if (($selected.Code -join "`n") -notmatch $spec[1]) { throw "$($pack.Name): native carried-object pickup geometry changed." }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/bracelet_grab_geometry.tsv'),$rows)
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
