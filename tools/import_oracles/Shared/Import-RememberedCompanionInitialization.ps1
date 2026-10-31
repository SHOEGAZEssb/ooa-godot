foreach ($pack in @(
    @{ Name='ages'; Bytes=$romBytes; Symbols=$agesSymbols; Root=$destination; Defines='ROM_AGES;AGES_ENGINE' },
    @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Root=(Split-Path $previewDestination -Parent); Defines='ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection=Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $path=Join-Path $Disassembly 'code/roomInitialization.s'
        $body=@((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$path`0loadRememberedCompanion`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' }).Code -join "`n"
        if ($body -notmatch '^ld hl,wRememberedCompanionId\s+ldi a,\(hl\)\s+or a\s+ret z\s+ld c,a\s+ld a,\(wActiveGroup\)\s+cp \(hl\)\s+ret nz\s+inc l\s+ld a,\(wActiveRoom\)\s+cp \(hl\)\s+ret nz\s+ld a,\(w1Companion.enabled\)\s+or a\s+ret nz\s+ld a,c' -or
            $body -notmatch 'ld \(w1Companion.id\),a\s+ld a,\$01\s+ld \(w1Companion.enabled\),a\s+inc l') {
            throw "$($pack.Name): remembered companion admission/header write order changed."
        }
        $copyMount=0; $special=-1; $interaction=0; $subid=0; $mask=0
        if ($pack.Name -eq 'ages') {
            if ($body -notmatch 'ld a,c\s+cp SPECIALOBJECT_RAFT\s+jr z,@raft' -or
                $body -notmatch 'ldi a,\(hl\)\s+ld \(w1Companion.yh\),a\s+ldi a,\(hl\)\s+ld \(w1Companion.xh\),a\s+ret' -or
                $body -notmatch 'ld a,\(wTilesetFlags\)\s+and TILESETFLAG_PAST\s+ret z\s+call getFreeInteractionSlot\s+ret nz\s+ld \(hl\),INTERAC_RAFT\s+inc l\s+ld \(hl\),\$02\s+ld a,\(wRememberedCompanionY\)\s+ld l,Interaction.yh\s+ldi \(hl\),a\s+inc l\s+ld a,\(wRememberedCompanionX\)\s+ldi \(hl\),a\s+ret') {
                throw 'Ages remembered raft branch or coordinate writes changed.'
            }
            $special=0x13; $interaction=0xe6; $subid=2; $mask=0x80
            $start=$pack.Symbols['loadRememberedCompanion']; $end=$pack.Symbols['checkAndSpawnMaple']; $raftComparisons=0
            for ($at=$start; $at -lt $end-2; $at++) {
                if ($pack.Bytes[$at] -eq 0xfe -and $pack.Bytes[$at+1] -eq $special -and $pack.Bytes[$at+2] -eq 0x28) { $raftComparisons++ }
            }
            if ($raftComparisons -ne 1) { throw 'Ages remembered companion native SPECIALOBJECT_RAFT comparison changed.' }
        } else {
            if ($body -match 'SPECIALOBJECT_RAFT' -or
                $body -notmatch 'ldi a,\(hl\)\s+ld \(w1Companion.yh\),a\s+ld \(wLastAnimalMountPointY\),a\s+ldi a,\(hl\)\s+ld \(w1Companion.xh\),a\s+ld \(wLastAnimalMountPointX\),a\s+ret') {
                throw 'Seasons remembered companion mount-point copies changed.'
            }
            $copyMount=1
        }
        $specialCode=if ($special -lt 0) { '-1' } else { $special.ToString('x2') }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/remembered_companion_initialization.tsv'),@(
            '# copy-mount-point`tinteraction-companion-id`tinteraction-id`tinteraction-subid`tinteraction-tileset-mask`tsource',
            "$copyMount`t$specialCode`t$($interaction.ToString('x2'))`t$($subid.ToString('x2'))`t$($mask.ToString('x2'))`tcode/roomInitialization.s:loadRememberedCompanion"
        ))
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
