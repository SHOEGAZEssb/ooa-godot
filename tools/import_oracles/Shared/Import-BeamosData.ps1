# ENEMY $16 and PART $29 share code, but retain each build's native graphics.
foreach ($pack in @(
    @{ Name='ages'; Bytes=$romBytes; Symbols=$agesSymbols; Root=$destination; Defines='ROM_AGES;AGES_ENGINE' },
    @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Root=(Split-Path $previewDestination -Parent); Defines='ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $init = Read-MenuRomBytes $pack 'enemyCode16@state_uninitialized' 10
        $reset = Read-MenuRomBytes $pack 'enemyCode16@state9' 37
        $charge = Read-MenuRomBytes $pack 'enemyCode16@checkFireBeam' 23
        $beam = Read-MenuRomBytes $pack 'partCode29@state0' 30
        if (($init[3..6] -join ',') -ne '46,134,54,5' -or
            ($reset[5..12] -join ',') -ne '54,5,44,54,40,107,53,201' -or
            ($reset[13..17] -join ',') -ne '126,254,11,62,164' -or
            ($reset[21..24] -join ',') -ne '208,6,41,205' -or
            ($reset[27..36] -join ',') -ne '192,30,134,26,230,1,46,194,119,201' -or
            ($charge[3..22] -join ',') -ne '98,46,137,150,60,254,2,208,46,134,54,20,46,171,54,20,46,132,52,201' -or
            ($beam[0..14] -join ',') -ne '98,107,52,46,198,54,2,46,201,78,6,80,62,4,205') {
            throw "$($pack.Name): beamos.s/beam.s native rotation, charge, checked spawn or scaled velocity operands changed."
        }
        $enemyPath = Join-Path $Disassembly 'object_code/common/enemies/beamos.s'
        $beamPath = Join-Path $Disassembly 'object_code/common/parts/beam.s'
        foreach ($spec in @(@($enemyPath,'enemyCode16'),@($beamPath,'partCode29'))) {
            $code = (@((Invoke-AssemblySourceHost $connection 'NODES' $spec[0] | ConvertFrom-Json) |
                Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' } | ForEach-Object Code)) -join "`n"
            $guards = if ($spec[1] -eq 'enemyCode16') {
                @('call @updateAngle\s+call ecom_decCounter2\s+ret nz','call ecom_spawnProjectile\s+ret nz','jp objectMakeTileSolid')
            } else {
                @('cp \$83\s+jp z,partDelete','call partCommon_decCounter1IfNonzero','call func_5758\s+call partCommon_checkTileCollisionOrOutOfBounds\s+jp c,partDelete')
            }
            foreach ($guard in $guards) { if ($code -notmatch $guard) { throw "$($pack.Name): $($spec[0]):$($spec[1]) native dispatch contract changed ($guard)." } }
        }
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add('# owner`ttable`tindex`tvalue`tsource')
        $state = @($init[6],$reset[9],$reset[15],$charge[14],$charge[18],$charge[9],$reset[32])
        $beamState = @($beam[6],$beam[11],$beam[13])
        $stateSources = @('enemyCode16@state_uninitialized+$06','enemyCode16@state9+$09','enemyCode16@state9+$0f',
            'enemyCode16@checkFireBeam+$0e','enemyCode16@checkFireBeam+$12','enemyCode16@checkFireBeam+$09','enemyCode16@state9+$20') |
            ForEach-Object { 'object_code/common/enemies/beamos.s:' + $_ }
        $beamSources = @('partCode29@state0+$06','partCode29@state0+$0b','partCode29@state0+$0d') |
            ForEach-Object { 'object_code/common/parts/beam.s:' + $_ }
        foreach ($spec in @(
            @('beamos','state-profile',$state,$stateSources),
            @('beamos','angle-to-animation',(Read-MenuRomBytes $pack 'enemyCode16@angleToAnimation' 32),'object_code/common/enemies/beamos.s:@angleToAnimation'),
            @('beamos-beam','state-profile',$beamState,$beamSources),
            @('beamos-beam','angle-to-animation',(Read-MenuRomBytes $pack 'partCode29@table_5737' 16),'object_code/common/parts/beam.s:@table_5737')
        )) {
            for ($index=0; $index -lt $spec[2].Count; $index++) {
                $source = if ($spec[3] -is [array]) { $spec[3][$index] } else { "$($spec[3])+`$$($index.ToString('x2'))" }
                $rows.Add("$($spec[0])`t$($spec[1])`t$index`t$($spec[2][$index])`t$source")
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/beamos_behavior.tsv'),$rows)

        $parts = Read-MenuRomBytes $pack 'partData' (0x2a * 8)
        $part = $parts[(0x29 * 8)..(0x2a * 8 - 1)]
        $mask = Read-MenuRomBytes $pack 'partActiveCollisions' (0x2a * 4)
        $collision = Read-MenuRomBytes $pack 'objectCollisionTable' (8 * 32)
        if (($part[1..7] -join ',') -ne '135,51,252,64,8,4,0' -or
            ($mask[(0x29 * 4)..(0x2a * 4 - 1)] -join ',') -ne '9,0,0,0' -or
            $collision[7 * 32] -ne 0x3c -or $collision[7 * 32 + 3] -ne 0x1f) {
            throw "$($pack.Name): PART_BEAM `$29 native properties, Link or L3-shield collision contract changed."
        }
        $gfxPath = Join-Path $Disassembly "data/$($pack.Name)/objectGfxHeaders.s"
        $gfx = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$gfxPath`0objectGfxHeaderTable`0m_ObjectGfxHeader" | ConvertFrom-Json) | Where-Object IsActive)[$part[0]]
        if ($gfx.Operands.Count -ne 1 -or $gfx.Operands[0] -ne 'spr_projectiles_3') { throw "$($pack.Name): PART_BEAM `$29 native sprite header changed." }
        $sprite = $gfx.Operands[0]
        $headers = Read-MenuRomBytes $pack 'objectGfxHeaderTable' (($part[0] + 1) * 3)
        $at = $part[0] * 3
        if (($headers[$at] -band 0x3f) -ne [int][Math]::Floor($pack.Symbols[$sprite] / 0x4000) -or
            (([int]$headers[$at + 1] -shl 8) -bor $headers[$at + 2]) -ne (($pack.Symbols[$sprite] -band 0x3fff) + 0x4000)) {
            throw "$($pack.Name): PART_BEAM `$29 compiled graphics pointer differs from configured source."
        }
        $path = Join-Path $Disassembly "data/$($pack.Name)/partAnimations.s"
        $animations = Read-NativeObjectPointerLabels $pack $path 'part29Animations' $connection
        $oam = Read-NativeObjectPointerLabels $pack $path 'part29OamDataPointers' $connection
        if ($animations.Count -ne 8) { throw "$($pack.Name): PART_BEAM `$29 requires eight native held poses." }
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add('# animation-index`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`tanimation`tsource')
        for ($index=0; $index -lt 8; $index++) {
            $animation = Read-NativeEnemyAnimation $pack $animations[$index] $oam $false
            if ($animation.Durations.Count -ne 1) { throw "$($pack.Name): PART_BEAM `$29 animation $index no longer holds one pose." }
            $rows.Add("$index`t$sprite`t$($part[5])`t$($part[6] -band 7)`t$($part[2] -shr 4)`t$($part[2] -band 15)`t$((256 - $part[3]) / 2)`t$($animation.Encoded)`tdata/$($pack.Name)/partAnimations.s:$($animations[$index])")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'effects/beamos_beam.tsv'),$rows)
        Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
