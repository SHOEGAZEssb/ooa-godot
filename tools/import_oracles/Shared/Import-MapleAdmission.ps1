# Keep the common admission mechanism separate from Maple's content/state machines.
foreach ($pack in @(
    @{ Name='ages'; Bytes=$romBytes; Symbols=$agesSymbols; Root=$destination; Defines='ROM_AGES;AGES_ENGINE' },
    @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Root=(Split-Path $previewDestination -Parent); Defines='ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection=Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $path=Join-Path $Disassembly 'code/roomInitialization.s'
        $body=@((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$path`0checkAndSpawnMaple`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' }).Code -join "`n"
        if ($body -notmatch '^xor a\s+ld \(wIsMaplePresent\),a\s+ld a,\(wcc85\)\s+or a\s+ret nz' -or
            $body -notmatch 'ld a,\(w1Companion.enabled\)\s+or a\s+ret nz\s+ld a,\(wAnimalCompanion\)' -or
            $body -notmatch 'ld a,MAPLES_RING\s+call cpActiveRing\s+ld e,30\s+jr nz,\+\s+srl e' -or
            $body -notmatch 'ld hl,wMapleKillCounter\s+ld a,\(hl\)\s+cp e\s+ret c\s+ld \(hl\),\$00\s+ld hl,w1Companion.enabled\s+ld a,\$01\s+ld \(wcc85\),a\s+ld \(wIsMaplePresent\),a\s+ldi \(hl\),a\s+ld \(hl\),\$0e\s+ld l,<w1Companion.yh\s+ld \(hl\),\$18\s+ld l,<w1Companion.xh\s+ld \(hl\),\$b8\s+ret') {
            throw "$($pack.Name): common checkAndSpawnMaple admission/write order changed."
        }
        $past=-1; $zero=0
        if ($pack.Name -eq 'ages') {
            if ($body -notmatch 'ld a,\(wActiveGroup\)\s+ld hl,maplePastLocations\s+dec a\s+jr z,@startCheck\s+inc a\s+ret nz' -or
                $body -notmatch 'ld a,\(wAnimalCompanion\)\s+or a\s+jr z,\+\s+sub SPECIALOBJECT_RICKY') {
                throw 'Ages Maple past-group bypass/zero-companion selector changed.'
            }
            $past=1; $zero=1
        } elseif ($body -notmatch 'ld a,\(wActiveGroup\)\s+or a\s+ret nz' -or
            $body -notmatch 'ld a,\(wAnimalCompanion\)\s+sub SPECIALOBJECT_RICKY') {
            throw 'Seasons Maple group/raw-companion selector changed.'
        }
        $start=$pack.Symbols['checkAndSpawnMaple']; $table=$pack.Symbols['maplePresentLocationsTable']
        $bank=[int][Math]::Floor($start/0x4000)
        $ringCall=$pack.Symbols['cpActiveRing']; $ringMatches=0; $ring=-1
        for ($at=$start; $at -lt $table-6; $at++) {
            if ($pack.Bytes[$at] -eq 0x3e -and $pack.Bytes[$at+2] -eq 0xcd -and
                [BitConverter]::ToUInt16($pack.Bytes,$at+3) -eq $ringCall -and
                $pack.Bytes[$at+5] -eq 0x1e -and $pack.Bytes[$at+6] -eq 30) {
                $ring=$pack.Bytes[$at+1]; $ringMatches++
            }
        }
        if ($ringMatches -ne 1 -or $ring -ne 15) { throw "$($pack.Name): native MAPLES_RING immediate changed." }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/maple_admission.tsv'),@(
            '# past-group`tzero-companion-is-ricky`tid`ty`tx`tring`tkill-threshold`tsource',
            "$past`t$zero`t0e`t18`tb8`t$($ring.ToString('x2'))`t30`tcode/roomInitialization.s:checkAndSpawnMaple"
        ))
        $bitmaps=[Collections.Generic.List[string]]::new(); $bitmaps.Add('# index`tblocked-bits`tsource')
        $targets=@{}
        $labels=@('maplePresentLocationsRickyCompanion','maplePresentLocationsDimitriCompanion','maplePresentLocationsMooshCompanion')
        if ($past -ge 0) { $labels+=@('maplePastLocations') }
        $locationPath=Join-Path $Disassembly "data/$($pack.Name)/mapleLocations.s"
        for ($index=0; $index -lt $labels.Count; $index++) {
            $label=$labels[$index]; $values=[Collections.Generic.List[byte]]::new()
            foreach ($node in Read-AssemblyMacroInvocations $locationPath $label 'dbrev') {
                if ($node.Operands.Count -ne 2) { throw "$($node.Path):$($node.Line): malformed Maple bitmap." }
                foreach ($operand in $node.Operands) {
                    if ($operand -notmatch '^%([01]{8})$') { throw "$($node.Path):$($node.Line): malformed dbrev byte." }
                    $bits=$Matches[1]; $reversed=-join $bits.ToCharArray()[7..0]
                    $values.Add([Convert]::ToByte($reversed,2))
                }
            }
            $physical=$pack.Symbols[$label]; $target=($physical -band 0x3fff)+0x4000
            if ($values.Count -ne 32 -or [int][Math]::Floor($physical/0x4000) -ne $bank) { throw "${label}: native Maple bitmap shape/bank changed." }
            for ($offset=0; $offset -lt 32; $offset++) {
                if ($values[$offset] -ne $pack.Bytes[$physical+$offset]) { throw "$label byte$offset differs from compiled US data." }
            }
            if ($index -lt 3) {
                if ([BitConverter]::ToUInt16($pack.Bytes,$table+$index*2) -ne $target) { throw "${label}: compiled selector order changed." }
            }
            $targets[$target]=$index
            $hex=[BitConverter]::ToString($values.ToArray()).Replace('-','').ToLowerInvariant()
            $bitmaps.Add("$index`t$hex`tdata/$($pack.Name)/mapleLocations.s:$label")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/maple_location_bitmaps.tsv'),$bitmaps)
        if ([int][Math]::Floor(($table+511)/0x4000) -ne $bank) { throw 'Maple raw selector span crosses its native bank.' }
        $selectors=[Collections.Generic.List[string]]::new(); $selectors.Add('# companion`ttarget`tbitmap-index`tsource')
        for ($raw=0; $raw -lt 256; $raw++) {
            $index=if ($zero -ne 0 -and $raw -eq 0) { 0 } else { ($raw-0x0b) -band 255 }
            $target=[BitConverter]::ToUInt16($pack.Bytes,$table+$index*2)
            $bitmap=if ($targets.ContainsKey([int]$target)) { $targets[[int]$target] } else { -1 }
            $selectors.Add("$($raw.ToString('x2'))`t$($target.ToString('x4'))`t$bitmap`tcode/roomInitialization.s:checkAndSpawnMaple;data/$($pack.Name)/mapleLocations.s:maplePresentLocationsTable")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/maple_location_selectors.tsv'),$selectors)
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
