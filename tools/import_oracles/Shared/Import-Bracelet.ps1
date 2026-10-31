# Shared parent/child mechanics retain the configured native build's branches.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $path = Join-Path $Disassembly 'object_code/common/itemParents/bombsBraceletParent.s'
        $nodes = @((Invoke-AssemblySourceHost $connection 'NODES' $path | ConvertFrom-Json) | Where-Object IsActive)
        $code = $nodes.Code -join "`n"
        foreach ($guard in @('call checkLinkOnGround','lda BREAKABLETILESOURCE_BRACELET','call tryToBreakTile','ld a,LINK_ANIM_MODE_LIFT_4','ld a,SND_PICKUP','ld a,SND_THROW','ld a,LINK_ANIM_MODE_LIFT_2')) {
            if ($code -notmatch $guard) { throw "$($pack.Name): native Bracelet parent contract changed ($guard)." }
        }
        $stump = [int]($code -match 'cp TILETYPE_STUMP')
        if ($stump -ne [int]($pack.Name -eq 'seasons')) { throw "$($pack.Name): native Bracelet stump gate changed." }
        $wall = Read-MenuRomBytes $pack 'parentItemCode_bracelet@checkWallInFrontOfLink@data' 12
        if (($wall -join ',') -ne '192,251,0,3,0,7,48,7,0,12,0,248') { throw "$($pack.Name): Bracelet wall geometry changed." }
        $path = Join-Path $Disassembly 'object_code/common/items/bracelet.s'
        $nodes = @((Invoke-AssemblySourceHost $connection 'NODES' $path | ConvertFrom-Json) | Where-Object IsActive)
        $code = $nodes.Code -join "`n"
        foreach ($guard in @('call itemMimicBgTile','call itemBeginThrow','call itemUpdateThrowingLaterally','call itemUpdateThrowingVertically','callab bank6.itemMakeInteractionForBreakableTile')) {
            if ($code -notmatch $guard) { throw "$($pack.Name): native Bracelet child contract changed ($guard)." }
        }
        $bouncingTile = -1
        if ($pack.Name -eq 'seasons') {
            if ($code -notmatch 'cp \$d7' -or $code -notmatch 'call objectCreatePuff' -or $code -notmatch 'call itemBounce') { throw 'Seasons: Bracelet $d7 bounce/puff branch changed.' }
            $bouncingTile = 0xd7
        } elseif ($code -match 'cp \$d7') { throw 'Ages: unexpected Bracelet bounce/puff branch.' }
        $attributes = Read-MenuRomBytes $pack 'itemAttributes' (0x2c * 4)
        if ($attributes[0x16 * 4] -ne $(if ($pack.Name -eq 'ages') { 0x16 } else { 0x14 }) -or
            $attributes[0x16 * 4 + 1] -ne 0 -or $attributes[0x16 * 4 + 2] -ne 0xfd) { throw "$($pack.Name): Bracelet native collision/damage changed." }
        $weight = Read-MenuRomBytes $pack 'itemWeights' 4
        $path = Join-Path $Disassembly "data/$($pack.Name)/specialObjectAnimationData.s"
        $pointers = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0specialObject09AnimationDataPointers`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
        if ($pointers.Count -ne 56) { throw "$($pack.Name): Link animation pointer extent changed." }
        $native = Read-MenuRomBytes $pack 'specialObject00AnimationDataPointers' 112
        foreach ($index in @(0x12,0x13,0x14,0x15,0x16)) {
            if ([BitConverter]::ToUInt16($native,$index * 2) -ne (($pack.Symbols[$pointers[$index].Operands[0]] -band 0x3fff) + 0x4000)) { throw "$($pack.Name): unresolved Bracelet Link animation pointer." }
        }
        $creation = Read-MenuRomBytes $pack $pointers[0x12].Operands[0] 12
        $lever = Read-MenuRomBytes $pack $pointers[0x13].Operands[0] 12
        $pull = Read-MenuRomBytes $pack $pointers[0x14].Operands[0] 9
        $lift = Read-MenuRomBytes $pack $pointers[0x15].Operands[0] 3
        $throw = Read-MenuRomBytes $pack $pointers[0x16].Operands[0] 6
        if (($creation[@(0,2,3,5,6,8,9,11)] -join ',') -ne '4,0,4,4,2,8,127,255' -or
            $pack.Symbols[$pointers[0x15].Operands[0]] + 3 -ne $pack.Symbols[$pointers[0x12].Operands[0]] -or
            ($lift[@(0,2)] -join ',') -ne '3,0' -or ($pull[@(0,2,3,5,6,8)] -join ',') -ne '1,0,10,0,110,255' -or
            ($lever[@(0,2,3,5,6,8,9)] -join ',') -ne '1,0,40,1,20,0,255' -or
            $throw[0] -ne 8 -or ($throw[5] -band 0x80) -eq 0) { throw "$($pack.Name): Bracelet Link lift/pull/lever/throw timing changed." }
        $path = Join-Path $Disassembly 'object_code/common/interactions/pushblock.s'
        $nodes = @((Invoke-AssemblySourceHost $connection 'NODES' $path | ConvertFrom-Json) | Where-Object IsActive)
        $code = $nodes.Code -join "`n"
        if ($code -notmatch 'ldbc SPEED_80, \$20') { throw "$($pack.Name): native Bracelet push speed changed." }
        $powerGlove = $code -match 'ldbc SPEED_c0, \$15'
        if ($powerGlove -ne ($pack.Name -eq 'ages')) { throw "$($pack.Name): native Power Glove push branch changed." }
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# item`tpickup-sound`tthrow-sound`tdamage`tradius-y`tradius-x`tcollision-z-radius`tgravity`tinitial-speed-z`tspeed-raw`ttoss-speed-raw`tpush-speed-raw`tpush-frames`tpower-glove-push-speed-raw`tpower-glove-push-frames`theavy-property-mask`tgrab-pull-frames`tlift-low-frames`tlift-mid-frames`tlift-high-frames`tthrow-frames`tlever-initial-frames`tlever-pull-frames`tlever-rest-frames`tstump-blocked`tbouncing-tile`tsource")
        $gloveSpeed = if ($powerGlove) { '1e' } else { '14' }; $gloveFrames = if ($powerGlove) { 21 } else { 32 }
        $rows.Add("16`t9c`t51`t$([int](256 - $attributes[0x16 * 4 + 2]))`t6`t6`t7`t$([int]$weight[0])`t$([int]$weight[1] - 256)`t$($weight[2].ToString('x2'))`t$($weight[3].ToString('x2'))`t14`t32`t$gloveSpeed`t$gloveFrames`t20`t$([int]$pull[0] + $pull[3])`t$([int]$lift[0] + $creation[0])`t$([int]$creation[3])`t$([int]$creation[6])`t$([int]$throw[0])`t$([int]$lever[0])`t$([int]$lever[3])`t$([int]$lever[6])`t$stump`t$bouncingTile`tobject_code/common/itemParents/bombsBraceletParent.s:parentItemCode_bracelet;object_code/common/items/bracelet.s")
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/bracelet.tsv'),$rows)
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
