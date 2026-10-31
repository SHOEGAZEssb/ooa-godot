# Retain source macro boundaries and compare their compressed native encoding.
foreach ($pack in @(
    @{ Name='ages'; Bytes=$romBytes; Symbols=$agesSymbols; Root=$destination; Defines='ROM_AGES;AGES_ENGINE' },
    @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Root=(Split-Path $previewDestination -Parent); Defines='ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $bank = [int][Math]::Floor($pack.Symbols['parseObjectData'] / 0x4000)
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add('# address`tnext`topcode`theader`tflags`tid`tsubid`ty`tx`tposition`tvar03`ttarget`tmask`tsource')
        $labels = [Collections.Generic.List[string]]::new()
        $labels.Add('# label`taddress`tsource')
        $addresses = [Collections.Generic.HashSet[int]]::new()
        $sourceInstructions = @{}
        $last = 17; $fixedFlags = 0; $itemFlags = 0
        foreach ($file in @('extraData1','enemyData','extraData2','mainData','extraData3','extraData4')) {
            $relative = "objects/$($pack.Name)/$file.s"
            $path = Join-Path $Disassembly $relative
            if (-not (Test-Path -LiteralPath $path)) { continue }
            $cursor = -1
            foreach ($node in @((Invoke-AssemblySourceHost $connection 'NODES' $path | ConvertFrom-Json) | Where-Object IsActive)) {
                if ($node.Kind -eq 'Label') {
                    $labelName=if ($node.Name.StartsWith('@')) { "$($node.EnclosingGlobalLabel)$($node.Name)" } else { $node.Name }
                    if (-not $pack.Symbols.ContainsKey($labelName)) { throw "$relative missing compiled label $labelName." }
                    $physical = $pack.Symbols[$labelName]
                    if ([int][Math]::Floor($physical / 0x4000) -ne $bank) { throw "$relative label $($node.Name) escaped its object bank." }
                    $cursor = ($physical -band 0x3fff) + 0x4000
                    $labels.Add("$labelName`t$($cursor.ToString('x4'))`t$relative`:$labelName")
                    continue
                }
                if ($node.Kind -eq 'Data') {
                    if ($node.Name -ne '.dw') { throw "$relative unsupported data directive $($node.Code)." }
                    $cursor += 2 * $node.Operands.Count
                    continue
                }
                if ($node.Kind -ne 'MacroInvocation') { continue }
                $source = "$relative`:$($node.Line):$($node.EnclosingGlobalLabel)"
                if ($cursor -lt 0x4000) { throw "$source has no native address." }
                $values = @(); $opcode = -1; $flags = 0; $id = 0; $subid = 0; $y = 0; $x = 0; $position = 0; $var03 = 0; $target = 0; $mask = 0xff
                $header = $false; $encoded = [Collections.Generic.List[byte]]::new()
                if ($node.Name -in @('obj_Pointer','obj_BeforeEvent','obj_AfterEvent')) {
                    $targetName=$node.Operands[0]
                    if ($targetName.StartsWith('@')) { $targetName="$($node.EnclosingGlobalLabel)$targetName" }
                    if ($node.Operands.Count -ne 1 -or -not $pack.Symbols.ContainsKey($targetName)) { throw "$source invalid pointer $targetName." }
                    $physical = $pack.Symbols[$targetName]
                    if ([int][Math]::Floor($physical / 0x4000) -ne $bank) { throw "$source has a cross-bank pointer." }
                    $target = ($physical -band 0x3fff) + 0x4000
                    $opcode = @{ obj_Pointer=3; obj_BeforeEvent=4; obj_AfterEvent=5 }[$node.Name]
                    $header = $opcode -ne 5 -or $last -ne 5
                    $values = @(($target -band 0xff),($target -shr 8))
                } else {
                    $values = @($node.Operands | ForEach-Object { Convert-AssemblyInteger $_ })
                    switch ($node.Name) {
                        'obj_Condition' { $opcode=0; $mask=$values[0]; $header=$last -ne 0 }
                        'obj_Interaction' {
                            $id=$values[0]; $subid=$values[1]
                            if ($values.Count -eq 2) { $opcode=1 }
                            elseif ($values.Count -eq 4) { $opcode=2; $y=$values[2]; $x=$values[3] }
                            elseif ($values.Count -eq 5) { $opcode=9; $y=$values[2]; $x=$values[3]; $var03=$values[4]; $values=@(0,$id,$subid,$var03,$y,$x) }
                            else { throw "$source invalid interaction operands." }
                            $header=$last -ne $opcode
                        }
                        'obj_RandomEnemy' { if ($values.Count -ne 3) { throw "$source invalid random enemy." }; $opcode=6; $flags=$values[0]; $id=$values[1]; $subid=$values[2]; $header=$true }
                        'obj_SpecificEnemyA' {
                            $opcode=7; $header=$values.Count -eq 5
                            if ($header) { $fixedFlags=$values[0]; $id=$values[1]; $subid=$values[2]; $y=$values[3]; $x=$values[4] }
                            elseif ($values.Count -eq 4 -and $last -eq 7) { $id=$values[0]; $subid=$values[1]; $y=$values[2]; $x=$values[3] }
                            else { throw "$source invalid fixed-enemy continuation." }
                            $flags=$fixedFlags
                        }
                        'obj_SpecificEnemyB' { if ($values.Count -ne 5) { throw "$source invalid parameter enemy." }; $opcode=9; $id=$values[0]; $subid=$values[1]; $y=$values[2]; $x=$values[3]; $var03=$values[4]; $values=@(1,$id,$subid,$var03,$y,$x); $header=$last -ne 9 }
                        'obj_Part' {
                            $id=$values[0]; $subid=$values[1]
                            if ($values.Count -eq 3) { $opcode=8; $position=$values[2] }
                            elseif ($values.Count -eq 5) { $opcode=9; $y=$values[2]; $x=$values[3]; $var03=$values[4]; $values=@(2,$id,$subid,$var03,$y,$x) }
                            else { throw "$source invalid part operands." }
                            $header=$last -ne $opcode
                        }
                        'obj_ItemDrop' {
                            if ($pack.Name -ne 'ages') { throw "$source has an Ages-only item opcode." }
                            $opcode=10; $header=$values.Count -eq 3
                            if ($header) { $itemFlags=$values[0]; $id=$values[1]; $position=$values[2] }
                            elseif ($values.Count -eq 2 -and $last -eq 10) { $id=$values[0]; $position=$values[1] }
                            else { throw "$source invalid item continuation." }
                            $flags=$itemFlags
                        }
                        'obj_End' { $opcode=0xff; $header=$true }
                        'obj_EndPointer' { $opcode=0xfe; $header=$true }
                        'obj_Garbage' { $opcode=0xfd }
                        default { throw "$source unsupported object macro $($node.Name)." }
                    }
                }
                if ($opcode -in @(0,3,4,5) -and -not $header) { throw "$source has unsupported compressed control flow." }
                if ($header) { $encoded.Add($(if ($opcode -ge 0xfe) { $opcode } else { 0xf0 -bor $opcode })) }
                foreach ($value in $values) {
                    if ($value -lt 0 -or $value -gt 255) { throw "$source operand is not a byte: $value." }
                    $encoded.Add([byte]$value)
                }
                for ($index=0; $index -lt $encoded.Count; $index++) {
                    if ($pack.Bytes[$bank*0x4000+$cursor-0x4000+$index] -ne $encoded[$index]) { throw "$source compressed encoding differs at byte $index, native address `$$(($cursor+$index).ToString('x4'))." }
                }
                if (-not $addresses.Add($cursor)) { throw "$source duplicate instruction address." }
                $next=$cursor+$encoded.Count
                if ($opcode -eq 9) { $flags=$values[0] }
                $fields=@($cursor.ToString('x4'),$next.ToString('x4'),$opcode.ToString('x2'),[int]$header,$flags.ToString('x2'),$id.ToString('x2'),$subid.ToString('x2'),$y.ToString('x2'),$x.ToString('x2'),$position.ToString('x2'),$var03.ToString('x2'),$target.ToString('x4'),$mask.ToString('x2'),$source)
                $sourceInstructions["$relative`:$($node.Line)"] = $fields
                $rows.Add($fields -join "`t")
                $cursor=$next
                if ($opcode -in @(0xfe,0xff)) { $last=17 } elseif ($opcode -ne 0xfd) { $last=$opcode }
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'objects/room_object_streams.tsv'),$rows)
        Write-GeneratedTable((Join-Path $pack.Root 'objects/room_object_labels.tsv'),$labels)
        $bindings = [Collections.Generic.List[string]]::new()
        $bindings.Add('# group`troom`torder`taddress`tsource')
        if ($pack.Name -eq 'ages') {
            foreach ($row in $orderedObjectRows | Select-Object -Skip 1) {
                $columns = $row -split "`t"
                $key = "$($columns[0]):$($columns[1]):$($columns[2])"
                $sourceLine = $orderedObjectSources[$key]
                if (-not $sourceLine -or -not $sourceInstructions.ContainsKey($sourceLine)) {
                    throw "Room placement $key has no compiled instruction for source $sourceLine."
                }
                $native = $sourceInstructions[$sourceLine]
                $nativeOpcode = switch ($columns[3]) { 'R' { '06' } 'F' { '07' } 'P' { '08' } 'Q' { '09' } 'B' { '09' } 'I' { '0a' } default { throw "Room placement $key has unknown kind $($columns[3])." } }
                $nativeId = if ($columns[3] -eq 'I') { $columns[5] } else { $columns[4] }
                if ($native[2] -ne $nativeOpcode -or $native[5] -ne $nativeId -or
                    ($columns[3] -ne 'I' -and $native[6] -ne $columns[5])) {
                    throw "Room placement $key differs from its native opcode/identity at $sourceLine."
                }
                if (($columns[3] -in @('R','F','I') -and $native[4] -ne $columns[6]) -or
                    ($columns[3] -eq 'R' -and [int]$columns[7] -ne (([Convert]::ToInt32($native[4],16) -shr 5) -band 7)) -or
                    ($columns[3] -in @('F','B','Q') -and ($native[7] -ne $columns[8] -or $native[8] -ne $columns[9])) -or
                    ($columns[3] -in @('P','I') -and $native[9] -ne $columns[10]) -or
                    ($columns[3] -in @('B','Q') -and $native[10] -ne $columns[12]) -or
                    ($columns[3] -eq 'B' -and $native[4] -ne '01') -or
                    ($columns[3] -eq 'Q' -and $native[4] -ne '02')) {
                    throw "Room placement $key differs from its compiled flags/count/operands at $sourceLine."
                }
                $bindings.Add("$($columns[0])`t$($columns[1])`t$($columns[2])`t$($native[0])`t$sourceLine")
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'objects/legacy_placement_bindings.tsv'),$bindings)
        $roots=[Collections.Generic.List[string]]::new(); $roots.Add('# group`troom`taddress`tsource')
        $path=Join-Path $Disassembly "objects/$($pack.Name)/pointers.s"
        $groups=@((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0objectDataGroupTable`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
        if ($groups.Count -ne 8) { throw "$path must declare eight group pointers." }
        for ($group=0; $group -lt 8; $group++) {
            $table=$groups[$group].Operands[0]
            $tableAddress=($pack.Symbols[$table] -band 0x3fff)+0x4000
            if ([BitConverter]::ToUInt16($pack.Bytes,$pack.Symbols['objectDataGroupTable']+$group*2) -ne $tableAddress) { throw "$path group$group selector differs from compiled source." }
            $entries=@((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0$table`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
            if ($entries.Count -ne 256) { throw "$path $table must declare256 room pointers." }
            for ($room=0; $room -lt 256; $room++) {
                $label=$entries[$room].Operands[0]; $target=($pack.Symbols[$label] -band 0x3fff)+0x4000
                $at=$pack.Symbols[$table]+$room*2
                if ([BitConverter]::ToUInt16($pack.Bytes,$at) -ne $target -or -not $addresses.Contains($target)) { throw "$path $table room$room has invalid compiled source target $label." }
                $roots.Add("$group`t$($room.ToString('x2'))`t$($target.ToString('x4'))`tobjects/$($pack.Name)/pointers.s:$table`:$label")
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'objects/room_object_roots.tsv'),$roots)
        $gatePath=Join-Path $Disassembly 'code/objectLoading.s'
        $gate=@((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$gatePath`0checkSkipPointer`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' }).Code -join "`n"
        $loadMask=if ($gate -match '^ld a,\(wcc05\)\s+bit 1,a\s+ret z') { 2 } else { 0 }
        if ($loadMask -ne $(if ($pack.Name -eq 'ages') { 2 } else { 0 })) { throw "$($pack.Name) native pointer gate changed." }
        $bank1Path=Join-Path $Disassembly 'code/bank1.s'
        $initialize=@((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$bank1Path`0initializeGame`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' }).Code -join "`n"
        $complete=@((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$bank1Path`0cutscene00`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' }).Code -join "`n"
        $initialFlags=0; $restoreMask=0
        if ($loadMask -ne 0) {
            if ($initialize -notmatch 'ld a,\$ff\s+ld \(wActiveMusic\),a\s+ld \(wcc05\),a' -or
                $complete -notmatch 'ld a,\(wScrollMode\)\s+cp \$01\s+ret nz' -or
                $complete -notmatch 'ld a,\(wcc05\)\s+bit 7,a\s+jr z,\+\s+ld a,\$ff\s+ld \(wcc05\),a') {
                throw 'Ages initializeGame/cutscene00 object-load flag lifetime changed.'
            }
            $initialFlags=255; $restoreMask=128
        } elseif ($initialize -match 'wcc05' -or $complete -match 'wcc05') {
            throw 'Seasons unexpectedly acquired Ages object-load flags.'
        }
        $roomInitPath=Join-Path $Disassembly 'code/bank0.s'
        $roomInit=@((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$roomInitPath`0initializeRoom`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -in @('Instruction','MacroInvocation') }).Code -join "`n"
        $rootMask=0; $rememberedMask=0; $mapleMask=0
        if ($loadMask -ne 0) {
            if ($roomInit -notmatch 'ld a,\(wcc05\)\s+bit 0,a\s+call nz,objectData.parseObjectData' -or
                $roomInit -notmatch 'ld a,\(wcc05\)\s+bit 2,a\s+call nz,roomInitialization.loadRememberedCompanion' -or
                $roomInit -notmatch 'ld a,\(wcc05\)\s+bit 3,a\s+call nz,roomInitialization.checkAndSpawnMaple') {
                throw 'Ages initializeRoom root/remembered-companion/Maple gates changed.'
            }
            $rootMask=1; $rememberedMask=4; $mapleMask=8
        } elseif ($roomInit -match 'wcc05' -or
            $roomInit -notmatch 'callfrombank0 roomInitialization.loadRememberedCompanion\s+call\s+roomInitialization.checkAndSpawnMaple' -or
            $roomInit -notmatch 'callfrombank0 objectData.parseObjectData') {
            throw 'Seasons initializeRoom root/remembered-companion/Maple dispatch changed.'
        }
        if ($roomInit -notmatch '(?s)objectData.parseObjectData.*callfrombank0 staticObjects.parseStaticObjects') {
            throw "$($pack.Name) initializeRoom static objects must follow the ordinary root parse."
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/object_stream_policy.tsv'),@('# pointer-load-mask`tinitial-load-flags`trestore-load-mask`troot-load-mask`tremembered-companion-load-mask`tmaple-load-mask`tsource',"$($loadMask.ToString('x2'))`t$($initialFlags.ToString('x2'))`t$($restoreMask.ToString('x2'))`t$($rootMask.ToString('x2'))`t$($rememberedMask.ToString('x2'))`t$($mapleMask.ToString('x2'))`tcode/objectLoading.s:checkSkipPointer;code/bank1.s:initializeGame,cutscene00;code/bank0.s:initializeRoom"))
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
