# Common handlers retain each build's native properties, graphics and collision IDs.
$ropeProfile = @(15,50,15,64,10,112,112,56,256,8,14)
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $enemyData = Read-MenuRomBytes $pack 'enemyData' 512
        $zoraPath=Join-Path $Disassembly 'object_code/common/enemies/riverZora.s'
        $zoraCode=(@((Invoke-AssemblySourceHost $connection 'NODES' $zoraPath | ConvertFrom-Json) | Where-Object {
            $_.IsActive -and $_.Kind -eq 'Instruction'
        } | ForEach-Object Code) -join "`n")
        foreach($guard in @('cp \(SCREEN_WIDTH<<4\)-8','res 7,a','ld \(hl\),48','and \$1f\s+add \$18')) {
            if($zoraCode -notmatch $guard){throw "$($pack.Name): native River Zora behavior changed: $guard."}
        }
        $water=Read-MenuRomBytes $pack 'checkTileAtPositionIsWater' 8
        $expectedWater=if($pack.Name -eq 'ages'){'214,249,254,5,201'}else{'214,250,254,4,201'}
        if(($water[3..7] -join ',') -ne $expectedWater){throw "$($pack.Name): River Zora water tile range changed."}
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/river_zora_behavior.tsv'),@(
            "# spawn-x-limit`tspawn-y-mask`tsurfacing-frames`thidden-mask`thidden-base`twater-base`twater-count`tsource",
            "152`t127`t48`t31`t24`t$($water[4])`t$($water[6])`tobject_code/common/enemies/riverZora.s;code/bank0.s:checkTileAtPositionIsWater"))
        $selection = Read-MenuRomBytes $pack 'itemDrop_spawnEnemy@enemiesToSpawn' 8
        $path = Join-Path $Disassembly 'object_code/common/parts/itemDrop.s'
        $code = (@((Invoke-AssemblySourceHost $connection 'NODES' $path | ConvertFrom-Json) | Where-Object {
            $_.IsActive -and $_.Kind -eq 'Instruction' -and $_.EnclosingGlobalLabel -eq 'itemDrop_spawnEnemy'
        } | ForEach-Object Code) -join "`n")
        if ($code -notmatch 'ld c,a\s+ld a,\(wDiggingUpEnemiesForbidden\)\s+or a\s+jr nz,@delete' -or
            $code -notmatch 'call getFreeEnemySlot\s+jr nz,@delete\s+ld \(hl\),b\s+call objectCopyPosition' -or
            ($pack.Name -eq 'seasons' -and $code -notmatch 'ld b,ENEMY_PODOBOO_TOWER\s+ld a,\(wTilesetFlags\)\s+cp \(TILESETFLAG_SUBROSIA\|TILESETFLAG_OUTDOORS\)') -or
            ($pack.Name -eq 'ages' -and $code -match 'ENEMY_PODOBOO_TOWER')) { throw "$($pack.Name): native digging gate/allocation/override changed." }
        $counters = Read-MenuRomBytes $pack 'beetle_chooseRandomAngleAndCounter1@counter1Vals' 8
        if (($selection -join ',') -ne '16,16,16,81,81,81,81,81' -or ($counters -join ',') -ne '15,30,30,60,60,60,90,90') {
            throw "$($pack.Name): native digging selection or Beetle counters changed."
        }
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add('# index`tenemy-id`tcollision-mode`tsource')
        for ($index = 0; $index -lt 8; $index++) {
            $id = $selection[$index]
            $rows.Add("$index`t$($id.ToString('x2'))`t$($enemyData[$id * 4 + 1].ToString('x2'))`tobject_code/common/parts/itemDrop.s:itemDrop_spawnEnemy@enemiesToSpawn+$index")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/digging_enemies.tsv'),$rows)
        Write-GeneratedBytes((Join-Path $pack.Root 'metadata/beetle_counters.bin'),$counters)
        $flags = if ($pack.Name -eq 'seasons') { 129 } else { -1 }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/digging_enemy_override.tsv'),@(
            '# flags`tenemy-id`tcollision-mode`tsource',
            "$flags`t2d`t$($enemyData[0x2d * 4 + 1].ToString('x2'))`tobject_code/common/parts/itemDrop.s:itemDrop_spawnEnemy/ROM_SEASONS"
        ))

        $ropePath = Join-Path $Disassembly 'object_code/common/enemies/rope.s'
        $rope = (@((Invoke-AssemblySourceHost $connection 'NODES' $ropePath | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -in @('Instruction','MacroInvocation') } | ForEach-Object Code) -join "`n")
        foreach ($guard in @('ld a,SPEED_60','ld b,\$0a','ld \(hl\),SPEED_140','ld \(hl\),\$40','and \$38\s+inc a','ld c,\$08\s+jp ecom_setZAboveScreen','ld c,\$0e','ldbc \$18,\$70','add \$70')) {
            if ($rope -notmatch $guard) { throw "$($pack.Name): native Rope state operand changed ($guard)." }
        }
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add('# index`tvalue`tsource')
        for ($index = 0; $index -lt $ropeProfile.Count; $index++) { $rows.Add("$index`t$($ropeProfile[$index])`tobject_code/common/enemies/rope.s:subid00+subid01-state-operands") }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/rope_behavior.tsv'),$rows)

        $radii = Read-MenuRomBytes $pack 'enemyCode2d@data' 15
        $path = Join-Path $Disassembly 'object_code/common/enemies/podobooTower.s'
        $code = (@((Invoke-AssemblySourceHost $connection 'NODES' $path | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' } | ForEach-Object Code) -join "`n")
        $counts = @([regex]::Matches($code,'ld \(hl\),(60|150|180)\b') | ForEach-Object { $_.Groups[1].Value })
        if (($counts -join ',') -ne '60,150,180,150,60,180,60' -or $code -notmatch 'cp \$b4' -or
            $code -notmatch 'ld a,\(wFrameCounter\)\s+and \$03') { throw "$($pack.Name): native Podoboo Tower counter or projectile gate changed." }
        if (($radii -join ',') -ne '6,4,0,8,4,249,11,4,247,15,4,244,18,4,242') { throw "$($pack.Name): native Podoboo Tower radii changed." }
        $collision = Read-MenuRomBytes $pack 'objectCollisionTable' (16 * 32)
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add('# table`tindex`tvalue`tsource')
        foreach ($spec in @(@('state-profile',@(60,150,180,180,3),'object_code/common/enemies/podobooTower.s:states8-D'),
            @('radii',$radii,'object_code/common/enemies/podobooTower.s:@data'),
            @('collision-effects',$collision[(15 * 32)..(16 * 32 - 1)],"data/$($pack.Name)/objectCollisionTable.s:objectCollisionTable+01e0"))) {
            for ($index = 0; $index -lt $spec[1].Count; $index++) { $rows.Add("$($spec[0])`t$index`t$($spec[1][$index])`t$($spec[2])") }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/podoboo_tower_behavior.tsv'),$rows)

        # PART $19 and $31 share fireProjectiles.s and the common sprite sheet.
        $part = Read-MenuRomBytes $pack 'partData' (0x32 * 8)
        foreach ($id in @(0x19,0x31)) {
            $at = $id * 8
            if ($part[$at + 3] -ne 0xfc -or $part[$at + 5] -ne 28 -or $part[$at + 6] -ne 10) { throw "$($pack.Name): fire projectile properties changed." }
        }
        $path = Join-Path $Disassembly "data/$($pack.Name)/partAnimations.s"
        $animations = Read-NativeObjectPointerLabels $pack $path 'part19Animations' $connection
        $oam = Read-NativeObjectPointerLabels $pack $path 'part19OamDataPointers' $connection
        if ($animations.Count -ne 1) { throw "$($pack.Name): fire projectile animation extent changed." }
        $animation = (Read-NativeEnemyAnimation $pack $animations[0] $oam $false).Encoded
        $row = "spr_common_sprites`t28`t2`t2`t2`t2`t60`t$animation`tobject_code/common/parts/fireProjectiles.s:partCode19"
        if ($pack.Name -ne 'ages') {
            Write-GeneratedTable((Join-Path $pack.Root 'effects/zora_fire.tsv'),@('# sprite`ttile-base`tpalette`tradius-y`tradius-x`tdamage-quarters`tspeed-raw`tanimation`tsource',$row))
        }
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
