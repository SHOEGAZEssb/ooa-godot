# Native Moblin route operands and PART_ENEMY_ARROW, selected independently per build.
foreach ($pack in @(
    @{ Name='ages'; Bytes=$romBytes; Symbols=$agesSymbols; Root=$destination; Defines='ROM_AGES;AGES_ENGINE' },
    @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Root=(Split-Path $previewDestination -Parent); Defines='ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $moblinPath = Join-Path $Disassembly 'object_code/common/enemies/moblinsAndShroudedStalfos.s'
        $code = (@((Invoke-AssemblySourceHost $connection 'NODES' $moblinPath | ConvertFrom-Json) | Where-Object { $_.IsActive } | ForEach-Object Code)) -join "`n"
        if ($code -notmatch 'enemyCode0c:\s+enemyCode20:\s+enemyCode22:' -or
            $code -notmatch 'call ecom_setRandomCardinalAngle\s+call arrowDarknut_setState8WithRandomAngleAndCounter\s+jr arrowDarknut_fireArrowEveryOtherTime') {
            throw "$($pack.Name): native Moblin dispatch aliases or route order changed."
        }
        $init = Read-MenuRomBytes $pack 'arrowDarknut_state_uninitialized' 14
        $route = Read-MenuRomBytes $pack 'arrowDarknut_setState8WithRandomAngleAndCounter' 18
        $move = Read-MenuRomBytes $pack 'moblin_state_8' 18
        if (($init[0..4] -join ',') -ne '30,144,62,20,18' -or
            ($route[3..14] -join ',') -ne '230,63,198,48,98,46,134,119,46,132,54,8' -or
            ($move[13..17] -join ',') -ne '46,134,54,8,195') { throw "$($pack.Name): compiled Moblin state-entry operands changed." }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/moblin_behavior.tsv'),@(
            '# index`tvalue`tsource',
            "0`t$($init[3])`tobject_code/common/enemies/arrowDarknut.s:arrowDarknut_state_uninitialized+`$03",
            "1`t$($route[6])`tobject_code/common/enemies/arrowDarknut.s:arrowDarknut_setState8WithRandomAngleAndCounter+`$06",
            "2`t$($route[4])`tobject_code/common/enemies/arrowDarknut.s:arrowDarknut_setState8WithRandomAngleAndCounter+`$04",
            "3`t$($move[16])`tobject_code/common/enemies/moblinsAndShroudedStalfos.s:moblin_state_8+`$10"
        ))
        $part = (Read-MenuRomBytes $pack 'partData' (0x1b * 8))[(0x1a * 8)..(0x1b * 8 - 1)]
        $normal = Read-MenuRomBytes $pack 'partCode1a@subid0@state0' 28
        $launch = Read-MenuRomBytes $pack 'partCode1a@subid1@state0' 23
        if (($normal[0..12] -join ',') -ne '98,107,52,46,208,54,80,46,203,70,46,205,78' -or
            ($launch[0..10] -join ',') -ne '98,107,52,46,198,54,8,46,208,54,80' -or
            $part[2] -ne 0x22 -or $part[3] -ne 0xfc -or $part[4] -ne 0x40) {
            throw "$($pack.Name): compiled PART`$1a properties, speed or launch delay changed."
        }
        $gfxPath = Join-Path $Disassembly "data/$($pack.Name)/objectGfxHeaders.s"
        $gfx = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$gfxPath`0objectGfxHeaderTable`0m_ObjectGfxHeader" | ConvertFrom-Json) | Where-Object IsActive)[$part[0]]
        if ($gfx.Operands.Count -ne 1) { throw "$($pack.Name): PART`$1a requires its native single graphics sheet." }
        $sprite = $gfx.Operands[0]; $at = $part[0] * 3
        $headers = Read-MenuRomBytes $pack 'objectGfxHeaderTable' ($at + 3)
        if (($headers[$at] -band 0x3f) -ne [int][Math]::Floor($pack.Symbols[$sprite] / 0x4000) -or
            (([int]$headers[$at+1] -shl 8) -bor $headers[$at+2]) -ne (($pack.Symbols[$sprite] -band 0x3fff) + 0x4000)) { throw "$($pack.Name): PART`$1a graphics pointer differs from configured source." }
        $path = Join-Path $Disassembly "data/$($pack.Name)/partAnimations.s"
        $animations = Read-NativeObjectPointerLabels $pack $path 'part1aAnimations' $connection
        $oam = Read-NativeObjectPointerLabels $pack $path 'part1aOamDataPointers' $connection
        if ($animations.Count -ne 5 -or $oam.Count -ne 4) { throw "$($pack.Name): PART`$1a native animation/OAM extent changed." }
        $poses = @($animations | ForEach-Object { (Read-NativeEnemyAnimation $pack $_ $oam $false).Encoded })
        Write-GeneratedTable((Join-Path $pack.Root 'effects/enemy_arrow.tsv'),@(
            '# sprite`ttile-base`tpalette`tdamage-quarters`tspeed-raw`tup-animation`tright-animation`tdown-animation`tleft-animation`tbounce-animation',
            "$sprite`t$($part[5])`t$($part[6] -band 7)`t$((256-$part[3])/2)`t$($normal[6])`t$($poses -join "`t")"
        ))
        Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
        $directions = Read-MenuRomBytes $pack 'partCommon_setPositionOffsetAndRadiusFromAngle@data' 16
        if (($directions -join ',') -ne '248,251,6,3,2,8,3,6,8,5,6,3,2,248,3,6') { throw "$($pack.Name): PART`$1a native offset/radius rows changed." }
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add('# table`tindex`tfirst`tsecond`tsource')
        foreach ($spec in @(@('spawn-offsets',$directions,4,0),@('collision-radii',$directions,4,2))) {
            for ($index=0; $index -lt $spec[2]; $index++) {
                $offset = $index * 4 + $spec[3]
                $first = [int]$spec[1][$offset]; $second = [int]$spec[1][$offset+1]
                if ($first -ge 128) { $first -= 256 }; if ($second -ge 128) { $second -= 256 }
                $label = 'partCommon_setPositionOffsetAndRadiusFromAngle@data'
                $rows.Add("$($spec[0])`t$index`t$first`t$second`tobject_code/common/parts/commonCode.s:${label}+`$$($offset.ToString('x2'))")
            }
        }
        $rows.Add("launch-delay`t0`t$($launch[6])`t0`tobject_code/common/parts/enemyArrow.s:partCode1a@subid1@state0+`$06")
        $rows.Add("launch-radii`t0`t$($part[2] -shr 4)`t$($part[2] -band 15)`tdata/$($pack.Name)/partData.s:partData+`$00d2")
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/enemy_arrow_behavior.tsv'),$rows)
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
