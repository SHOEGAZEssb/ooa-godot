# ITEM_BOMB shares code, while graphics, item columns and hazard signals are native pack data.
function Read-BombAnimation($pack, [string]$label, [string[]]$oam, [int]$count, [int]$terminal) {
    $bytes = Read-MenuRomBytes $pack $label ($count * 3)
    $frames = for ($frame = 0; $frame -lt $count; $frame++) {
        $at = $frame * 3; $duration = [int]$bytes[$at]; $index = [int]$bytes[$at + 1]; $parameter = [int]$bytes[$at + 2]
        if ($duration -eq 0 -or $duration -eq 0xff -or ($index -band 1) -ne 0 -or $index / 2 -ge $oam.Count -or
            ($frame -eq $count - 1 -and $parameter -ne $terminal)) { throw "$($pack.Name): ${label}: invalid native Bomb frame $frame." }
        $target = $oam[$index / 2]; $parts = [int](Read-MenuRomBytes $pack $target 1)[0]
        if ($parts -lt 1 -or $parts -gt 8) { throw "$($pack.Name): ${target}: invalid Bomb OAM extent." }
        $cells = Read-MenuRomBytes $pack $target (1 + $parts * 4)
        $encoded = @(for ($part = 0; $part -lt $parts; $part++) { @($cells[(1 + $part * 4)..(4 + $part * 4)] | ForEach-Object { [int]$_ }) -join ',' }) -join ';'
        "$duration,$parameter@$encoded"
    }
    return $frames -join '|'
}
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE'; HazardGroup = 0; HazardRoom = 0x50; Column = 0x18 },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE'; HazardGroup = 4; HazardRoom = 0xef; Column = 0x17 }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $data = Read-MenuRomBytes $pack 'itemData' 12
        $attrs = Read-MenuRomBytes $pack 'itemAttributes' 16
        if ($data[10] -ne 0x10 -or $data[11] -ne 4 -or $attrs[12] -ne $pack.Column -or $attrs[13] -ne 0x44 -or $attrs[14] -ne 0xfc) { throw "$($pack.Name): native ITEM_BOMB graphics/attributes changed." }
        $gfxIndex = [int]$data[9]
        $path = Join-Path $Disassembly "data/$($pack.Name)/objectGfxHeaders.s"
        $headers = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$path`0objectGfxHeaderTable`0m_ObjectGfxHeader" | ConvertFrom-Json) | Where-Object IsActive)
        if ($headers[$gfxIndex].Operands.Count -ne 1 -or $headers[$gfxIndex].Operands[0] -ne 'spr_common_items') { throw "$($pack.Name): Bomb uses an unsupported native graphics stream." }
        $gfx = Read-MenuRomBytes $pack 'objectGfxHeaderTable' (($gfxIndex + 1) * 3)
        if (($gfx[$gfxIndex * 3] -band 0x3f) -ne [int][Math]::Floor($pack.Symbols['spr_common_items'] / 0x4000) -or
            (([int]$gfx[$gfxIndex * 3 + 1] -shl 8) -bor $gfx[$gfxIndex * 3 + 2]) -ne (($pack.Symbols['spr_common_items'] -band 0x3fff) + 0x4000)) { throw "$($pack.Name): compiled Bomb graphics header differs from selected source." }
        $path = Join-Path $Disassembly 'data/itemAnimations.s'
        $animations = Read-NativeObjectPointerLabels $pack $path 'item03Animations' $connection
        if ($pack.Symbols['item03OamDataPointers'] -ne $pack.Symbols['item11OamDataPointers']) { throw "$($pack.Name): Bomb OAM table alias changed." }
        $oam = Read-NativeObjectPointerLabels $pack $path 'item11OamDataPointers' $connection
        if ($animations.Count -ne 2 -or $oam.Count -ne 7) { throw "$($pack.Name): native Bomb animation/OAM pointers changed." }
        $fuse = Read-BombAnimation $pack $animations[0] $oam 11 1
        $explosion = Read-BombAnimation $pack $animations[1] $oam 7 0xff
        $weights = Read-MenuRomBytes $pack 'itemWeights' 4
        if (($weights -join ',') -ne '28,16,60,100') { throw "$($pack.Name): native Bomb weight-zero motion changed." }
        $speeds = Read-MenuRomBytes $pack 'bounceSpeedReductionMapping' 50
        $bounce = @(for ($at = 0; $at -lt 50; $at += 2) { "$([int]$speeds[$at]):$([int]$speeds[$at + 1])" }) -join ';'
        if (($speeds[48..49] -join ',') -ne '0,0') { throw "$($pack.Name): Bomb bounce mapping lost its native terminal pair." }
        $edgeBytes = Read-MenuRomBytes $pack 'bombEdgeOffsets' 8
        $edges = @(for ($direction = 0; $direction -lt 4; $direction++) { @(for ($coordinate = 0; $coordinate -lt 2; $coordinate++) { $value = [int]$edgeBytes[$direction * 2 + $coordinate]; if ($value -ge 128) { $value -= 256 }; $value }) -join ',' }) -join ';'
        $probeBytes = Read-MenuRomBytes $pack 'explosionTryToBreakNextTile@data' 27
        $probes = @(for ($probe = 0; $probe -lt 9; $probe++) { @(for ($coordinate = 0; $coordinate -lt 3; $coordinate++) { $value = [int]$probeBytes[$probe * 3 + $coordinate]; if ($value -ge 128) { $value -= 256 }; $value }) -join ',' }) -join ';'
        if ($edges -ne '-3,0;0,3;7,0;0,-3' -or $probes -ne '-8,-13,-13;-8,12,-13;-8,12,12;-8,-13,12;-12,0,-13;-12,12,0;-12,0,12;-12,-13,0;-14,0,0') { throw "$($pack.Name): native Bomb edge/explosion probe order changed." }
        $path = Join-Path $Disassembly "data/$($pack.Name)/specialObjectAnimationData.s"
        $pointers = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0specialObject09AnimationDataPointers`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
        $nativePointers = Read-MenuRomBytes $pack 'specialObject00AnimationDataPointers' 112
        if ($pointers.Count -ne 56) { throw "$($pack.Name): Bomb Link animation pointer extent changed." }
        foreach ($index in @(0x12,0x15,0x16)) {
            $label = $pointers[$index].Operands[0]
            if ([BitConverter]::ToUInt16($nativePointers,$index * 2) -ne (($pack.Symbols[$label] -band 0x3fff) + 0x4000)) { throw "$($pack.Name): unresolved Bomb Link animation pointer." }
        }
        $creationLabel = $pointers[0x12].Operands[0]; $liftLabel = $pointers[0x15].Operands[0]
        $creation = Read-MenuRomBytes $pack $creationLabel 12
        $lift = Read-MenuRomBytes $pack $liftLabel 3
        $throw = Read-MenuRomBytes $pack $pointers[0x16].Operands[0] 6
        if (($creation[@(0,2,3,5,6,8,9,11)] -join ',') -ne '4,0,4,4,2,8,127,255' -or
            $pack.Symbols[$liftLabel] + 3 -ne $pack.Symbols[$creationLabel] -or $lift[0] -ne 3 -or $lift[1] -ne $creation[1] -or $lift[2] -ne 0 -or
            $throw[0] -ne 8 -or ($throw[5] -band 0x80) -eq 0) { throw "$($pack.Name): native Bomb lift/throw markers or fallthrough changed." }
        $path = Join-Path $Disassembly 'object_code/common/items/bombs.s'
        $nodes = @((Invoke-AssemblySourceHost $connection 'NODES' $path | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' })
        $code = $nodes.Code -join "`n"
        foreach ($guard in @('ld a,PEACE_RING','ld a,BLAST_RING','ld a,\$0a','ld \(hl\),\$0c','ld \(hl\),\$08','ld a,SND_EXPLOSION')) {
            if ($code -notmatch $guard) { throw "$($pack.Name): native Bomb instruction contract changed ($guard)." }
        }
        $signal = [int]$pack.Symbols['wTmpcfc0.bombUpgradeCutscene.state']
        $hazard = Read-MenuRomBytes $pack 'bombUpdateThrowingVerticallyAndCheckDelete' 96
        $expected = [byte[]]@(0x01,$pack.HazardRoom,$pack.HazardGroup)
        if ($code -notmatch $(if ($pack.Name -eq 'ages') { 'ld bc,ROOM_AGES_050' } else { 'ld bc,ROOM_SEASONS_4ef' }) -or
            ([BitConverter]::ToString($hazard)).IndexOf([BitConverter]::ToString($expected),[StringComparison]::Ordinal) -lt 0 -or $signal -ne 0xcfc0) { throw "$($pack.Name): native landed-hazard signal changed." }
        $rows = @(
            "# item`ttreasure-id`tsprite`ttile-base`tpalette`tcollision`tradius-y`tradius-x`tbase-damage`texplosion-sprite`texplosion-tile-base`texplosion-oam-flags`tpickup-sound`tthrow-sound`tlanding-sound`texplosion-sound`tgravity`tinitial-speed-z`tspeed-raw`ttoss-speed-raw`tconveyor-speed-raw`tlift-low-frames`tlift-mid-frames`tlift-high-frames`tthrow-frames`tedge-offsets`tbounce-speeds`tbreak-probes`tfuse-animation`texplosion-animation`tcreation-lift-low-frames`thazard-group`thazard-room`thazard-signal`tsource",
            "03`t03`tspr_common_items`t10`t04`t$($pack.Column.ToString('x2'))`t4`t4`t4`tspr_common_sprites`t0c`t0a`t$($soundIds['SND_PICKUP'].ToString('x2'))`t$($soundIds['SND_THROW'].ToString('x2'))`t$($soundIds['SND_BOMB_LAND'].ToString('x2'))`t$($soundIds['SND_EXPLOSION'].ToString('x2'))`t$([int]$weights[0])`t$([int]$weights[1] - 256)`t$($weights[2].ToString('x2'))`t$($weights[3].ToString('x2'))`t14`t$([int]$lift[0] + [int]$creation[0])`t$([int]$creation[3])`t$([int]$creation[6])`t$([int]$throw[0])`t$edges`t$bounce`t$probes`t$fuse`t$explosion`t$([int]$creation[0])`t$($pack.HazardGroup.ToString('x2'))`t$($pack.HazardRoom.ToString('x2'))`t$($signal.ToString('x4'))`tobject_code/common/items/bombs.s:itemCode03;data/$($pack.Name)/itemData.s;data/$($pack.Name)/specialObjectAnimationData.s:$creationLabel"
        )
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/bomb.tsv'),$rows)
        foreach ($sprite in @('spr_common_items','spr_common_sprites')) { Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force }

    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
