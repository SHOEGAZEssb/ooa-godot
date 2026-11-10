# ENEMY $09's admitted variants and PART $18 resolve each build's own property pointers.
foreach ($pack in @(
    @{ Name='ages'; Bytes=$romBytes; Symbols=$agesSymbols; Root=$destination; Defines='ROM_AGES;AGES_ENGINE' },
    @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Root=(Split-Path $previewDestination -Parent); Defines='ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $path = Join-Path $Disassembly 'object_code/common/enemies/octorok.s'
        $code = (@((Invoke-AssemblySourceHost $connection 'NODES' $path | ConvertFrom-Json) |
            Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' } | ForEach-Object Code)) -join "`n"
        foreach ($guard in @('rrca\s+ld a,SPEED_80\s+jr nc,\+\s+ld a,SPEED_c0',
            'ld b,PART_OCTOROK_PROJECTILE\s+call ecom_spawnProjectile\s+ret nz\s+ld a,SND_THROW',
            'call ecom_applyVelocityForTopDownEnemyNoHoles')) {
            if ($code -notmatch $guard) { throw "$($pack.Name): octorok.s native dispatch changed ($guard)." }
        }
        $init = Read-MenuRomBytes $pack 'octorok_state_uninitialized' 27
        $decide = Read-MenuRomBytes $pack 'octorok_state_08' 17
        $shoot = Read-MenuRomBytes $pack 'octorok_state_0b' 20
        if (($init[15..21] -join ',') -ne '15,62,20,48,2,62,30' -or
            ($decide[11..16] -join ',') -ne '54,11,46,134,54,16' -or
            ($shoot[3..10] -join ',') -ne '192,54,32,107,54,9,6,24' -or
            ($shoot[14..16] -join ',') -ne '192,62,81') { throw "$($pack.Name): compiled Octorok speed, waits or checked projectile/cue operands changed." }
        $ranges = Read-MenuRomBytes $pack 'octorok_state_uninitialized@counter1Ranges' 5
        $counters = Read-MenuRomBytes $pack 'octorok_counter1Values' 8
        $walk = Read-MenuRomBytes $pack 'octorok_walkCounterValues' 4
        if (($ranges -join ',') -ne '7,7,3,3,1' -or ($counters -join ',') -ne '30,45,60,75,45,60,75,90' -or
            ($walk -join ',') -ne '25,33,41,49') { throw "$($pack.Name): native Octorok counter tables changed." }
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add('# table`tindex`tvalue`tsource')
        foreach ($spec in @(@('counter-values',$counters,'octorok_counter1Values'),@('walk-counter-values',$walk,'octorok_walkCounterValues'))) {
            for ($index=0; $index -lt $spec[1].Length; $index++) {
                $rows.Add("$($spec[0])`t$index`t$($spec[1][$index])`tobject_code/common/enemies/octorok.s:$($spec[2])+`$$($index.ToString('x2'))")
            }
        }
        $rows.Add("state-profile`t0`t$($decide[16])`tobject_code/common/enemies/octorok.s:octorok_state_08+`$10")
        $rows.Add("state-profile`t1`t$($shoot[5])`tobject_code/common/enemies/octorok.s:octorok_state_0b+`$05")
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/octorok_behavior.tsv'),$rows)

        $data = (Read-MenuRomBytes $pack 'enemyData' 40)[36..39]
        $subids = Read-MenuRomBytes $pack 'enemy09SubidData' 10
        $pointer = (($data[2] -band 0x7f) -shl 8) -bor $data[3]
        if ($data[1] -ne 0x90 -or ($data[2] -band 0x80) -eq 0 -or
            $pointer -ne (($pack.Symbols['enemy09SubidData'] -band 0x3fff) + 0x4000) -or
            ($subids -join ',') -ne '136,32,136,32,140,16,140,16,63,48') { throw "$($pack.Name): native Octorok subid property pointer/stream changed." }
        $extra = Read-MenuRomBytes $pack 'extraEnemyData' 52
        if (($extra[32..35] -join ',') -ne '6,6,254,2' -or ($extra[48..51] -join ',') -ne '6,6,252,3') {
            throw "$($pack.Name): native Octorok radii, damage or health changed."
        }
        $gfxPath = Join-Path $Disassembly "data/$($pack.Name)/objectGfxHeaders.s"
        $gfx = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$gfxPath`0objectGfxHeaderTable`0m_ObjectGfxHeader" | ConvertFrom-Json) | Where-Object IsActive)[$data[0]]
        if ($gfx.Operands.Count -ne 1) { throw "$($pack.Name): Octorok requires its native single sprite sheet." }
        $sprite = $gfx.Operands[0]
        $headers = Read-MenuRomBytes $pack 'objectGfxHeaderTable' (($data[0] + 1) * 3); $at = $data[0] * 3
        if (($headers[$at] -band 0x3f) -ne [int][Math]::Floor($pack.Symbols[$sprite] / 0x4000) -or
            (([int]$headers[$at+1] -shl 8) -bor $headers[$at+2]) -ne (($pack.Symbols[$sprite] -band 0x3fff) + 0x4000)) { throw "$($pack.Name): compiled Octorok graphics pointer differs from source." }
        $path = Join-Path $Disassembly "data/$($pack.Name)/enemyAnimations.s"
        $animations = Read-NativeObjectPointerLabels $pack $path 'enemy09Animations' $connection
        $oam = Read-NativeObjectPointerLabels $pack $path 'enemy09OamDataPointers' $connection
        if ($animations.Count -ne 4) { throw "$($pack.Name): Octorok requires four native cardinal animations." }
        $poses = @($animations | ForEach-Object { (Read-NativeEnemyAnimation $pack $_ $oam $false).Encoded })
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add('# id`tsubid`tsprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`thealth`tspeed-raw`tcounter-mask`tup-animation`tright-animation`tdown-animation`tleft-animation`traw-damage')
        foreach ($subid in 0..$(if($pack.Name -eq 'seasons'){3}else{2})) {
            $flags = $subids[$subid*2+1]; $at = ($subids[$subid*2] -band 0x7f) * 4
            $speed = if (($subid -band 1) -ne 0) { $init[21] } else { $init[17] }
            $rows.Add("09`t$($subid.ToString('x2'))`t$sprite`t$(($flags -band 15)*2)`t$(($flags -shr 4) -band 7)`t$($extra[$at])`t$($extra[$at+1])`t$((256-$extra[$at+2])/2)`t$($extra[$at+3])`t$speed`t$($ranges[$subid])`t$($poses -join "`t")`t$($extra[$at+2].ToString('x2'))")
        }
        if ($pack.Name -eq 'ages' -and ($octorokDefinitionRows.Count -ne 4 -or
            -not [Linq.Enumerable]::SequenceEqual([string[]]@($rows | Select-Object -Skip 1),[string[]]@($octorokDefinitionRows | Select-Object -Skip 1)))) {
            throw 'Ages Octorok definitions differ from the independent compiled-pointer decoder.'
        }
        Write-GeneratedTable((Join-Path $pack.Root 'objects/octoroks.tsv'),$rows)
        Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force

        $part = (Read-MenuRomBytes $pack 'partData' (0x19*8))[(0x18*8)..(0x19*8-1)]
        $entry = Read-MenuRomBytes $pack 'partCode18@state0' 10
        $bounceEntry = Read-MenuRomBytes $pack 'partCode18@state2' 5
        if ($part[0] -ne $data[0] -or ($part[1..7] -join ',') -ne '135,34,252,64,12,3,0' -or
            ($entry[0..6] -join ',') -ne '98,107,52,46,208,54,80' -or
            ($bounceEntry -join ',') -ne '62,3,18,175,195') { throw "$($pack.Name): native PART18 graphics/properties, flight speed or bounce animation changed." }
        $partPath = Join-Path $Disassembly 'object_code/common/parts/octorokProjectile.s'
        $code = (@((Invoke-AssemblySourceHost $connection 'NODES' $partPath | ConvertFrom-Json) |
            Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' } | ForEach-Object Code)) -join "`n"
        if ($code -notmatch 'call objectCheckWithinScreenBoundary[\s\S]*call partCommon_checkTileCollisionOrOutOfBounds[\s\S]*jp objectApplySpeed' -or
            $code -notmatch 'ld a,\$03\s+ld \(de\),a\s+xor a\s+jp partCommon_bounceWhenCollisionsEnabled') { throw "$($pack.Name): native PART18 flight/bounce dispatch changed." }
        $path = Join-Path $Disassembly "data/$($pack.Name)/partAnimations.s"
        $animations = Read-NativeObjectPointerLabels $pack $path 'part18Animations' $connection
        $oam = Read-NativeObjectPointerLabels $pack $path 'part18OamDataPointers' $connection
        if ($animations.Count -ne 5 -or $oam.Count -ne 6) { throw "$($pack.Name): PART18 native animation/OAM extent changed." }
        $normal = (Read-NativeEnemyAnimation $pack $animations[0] $oam $false).Encoded
        # partCode18@state2 passes A=$00 to the common bounce helper.
        $bounce = (Read-NativeEnemyAnimation $pack $animations[0] $oam $false).Encoded
        if ($normal -ne '127@8,0,0,0;8,8,0,32' -or $bounce -ne $normal) { throw "$($pack.Name): native PART18 held poses changed." }
        Write-GeneratedTable((Join-Path $pack.Root 'effects/octorok_projectile.tsv'),@(
            '# sprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`tspeed-raw`tnormal-animation`tbounce-animation',
            "$sprite`t$($part[5])`t$($part[6] -band 7)`t$($part[2] -shr 4)`t$($part[2] -band 15)`t$((256-$part[3])/2)`t$($entry[6])`t$normal`t$bounce"
        ))
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
