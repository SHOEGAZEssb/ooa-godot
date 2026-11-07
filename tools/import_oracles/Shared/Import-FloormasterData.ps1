$floormasterSource = Read-ImportText (Join-Path $Disassembly 'object_code\common\enemies\floorMaster.s')
if ($floormasterSource -notmatch '(?ms)^enemyCode35:.*?ITEMCOLLISION_LINK.*?ld \(hl\),\$0c.*?ld \(hl\),\$fb.*?sra a.*?sra a' -or
    $floormasterSource -notmatch '(?ms)^floormaster_state_uninitialized:.*?ld \(hl\),60.*?floormaster_initSpawner' -or
    $floormasterSource -notmatch '(?ms)^floormaster_state1:.*?Enemy.var33.*?Enemy.var30.*?ret nc.*?dec \(hl\).*?ld \(hl\),\$01.*?cp \$03.*?ecom_spawnUncountedEnemyWithSubid01.*?Enemy.relatedObj1.*?inc \(hl\).*?ld \(hl\),\$80.*?decNumEnemies.*?markEnemyAsKilledInRoom.*?enemyDelete' -or
    $floormasterSource -notmatch '(?ms)^floormaster_state8:.*?call c,getRandomNumber_noPreserveVars.*?w1Link.angle.*?and \$1f.*?ld a,\$50.*?sub \$10.*?cp \$80.*?cp LARGE_ROOM_HEIGHT<<4.*?objectGetTileCollisions.*?ld \(hl\),\$20.*?add \$04.*?and \$18' -or
    $floormasterSource -notmatch '(?ms)^floormaster_stateA:.*?srl a\s+srl a.*?ld \(hl\),\$f0.*?set 7,\(hl\).*?bit 5,\(hl\).*?SPEED_60.*?SPEED_a0' -or
    $floormasterSource -notmatch '(?ms)^floormaster_stateC:.*?w1Link.substate.*?objectSetInvisible.*?w1Link.visible.*?w1Link.yh.*?w1Link.xh.*?Enemy.zh' -or
    $floormasterSource -notmatch '(?ms)^floormaster_checkLinkMoved8PixelsAway:.*?add \$08.*?cp \$10.*?add \$08.*?cp \$10' -or
    $floormasterSource -notmatch '(?ms)^floormaster_initSpawner:.*?and \$f0\s+swap a.*?and \$f0\s+swap a\s+inc a' -or
    $floormasterSource -notmatch '(?ms)^floormaster_getAdjacentWallsBitset:\s+ld a,\$02\s+jp ecom_getTopDownAdjacentWallsBitset') {
    throw 'object_code/common/enemies/floorMaster.s: spawner, position selection, grab or boundary contract changed.'
}

$linkWallmaster=Read-ImportText (Join-Path $Disassembly 'object_code/common/specialObjects/link.s')
$collision=Read-ImportText (Join-Path $Disassembly 'code/collisionEffects.s')
if($linkWallmaster -notmatch '(?ms)^linkState0c:.*?ld \(wWarpsDisabled\),a.*?SpecialObject.collisionType.*?wScrollMode.*?linkCancelAllItemUsage.*?SND_BOSS_DEAD.*?wWarpsDisabled.*?wActiveGroup.*?or \$80.*?wDungeonWallmasterDestRoom.*?TRANSITION_DEST_FALL.*?ld a,\$87.*?ld \(hl\),\$03' -or
    $collision -notmatch '(?ms)^collisionEffect37:.*?Object.invincibilityCounter.*?wWarpsDisabled.*?w1Link.state.*?LINK_STATE_NORMAL.*?Object.collisionType.*?LINK_STATE_GRABBED_BY_WALLMASTER.*?wLinkForceState.*?ENEMYDMG_1c') {
    throw 'linkState0c / collisionEffect37 native handoff changed.'
}
foreach($pack in @(
    @{Name='ages';Bytes=$romBytes;Symbols=$agesSymbols;Root=$destination},
    @{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent)}
)) {
    $hover=Read-MenuRomBytes $pack 'floormaster_stateA@zVals' 8
    $chase=Read-MenuRomBytes $pack 'floormaster_updateZPosition@zVals' 8
    if(($hover -join ',') -ne '251,252,253,253,254,254,255,255' -or
        ($chase -join ',') -ne '251,252,253,252,251,250,249,250' -or
        ((Read-MenuRomBytes $pack 'floormaster_state_uninitialized' 6) -join ',') -ne '98,46,134,54,60,107') {
        throw "$($pack.Name): compiled Floormaster initialization/height tables changed."
    }
    $enemy=Read-MenuRomBytes $pack 'enemyData' (0x36*4)
    $mode=$enemy[0x35*4+1]-band 0x7f
    $expectedMode=if($pack.Name -eq 'ages'){0x25}else{0x24}
    if($mode -ne $expectedMode){throw "$($pack.Name): ENEMY`$35 collision mode changed."}
    $effects=Read-MenuRomBytes $pack 'objectCollisionTable' (($mode+1)*32)
    $masks=Read-MenuRomBytes $pack 'enemyActiveCollisions' (0x36*4)
    $profiles=[ordered]@{
        'state-profile'=@(60,128,1,3,32,240,15,25,8,16,80,16,176,128,-5,2)
        'hover-z'=@($hover|ForEach-Object { if($_ -ge 128){[int]$_-256}else{[int]$_} })
        'chase-z'=@($chase|ForEach-Object { if($_ -ge 128){[int]$_-256}else{[int]$_} })
        'collision-effects'=@($effects[($mode*32)..($mode*32+31)]|ForEach-Object{[int]$_})
        'active-collisions'=@(0..31|ForEach-Object{([int]$masks[0x35*4+[int][Math]::Floor($_/8)] -shr ($_ -band 7))-band 1})
        'link-return'=@(1,0,12,5,0x87,3)
    }
    $rows=@("# profile`tindex`tvalue`tsource")
    foreach($profile in $profiles.GetEnumerator()) {
        for($index=0;$index -lt $profile.Value.Count;$index++) {
            $source=switch($profile.Key) {
                'collision-effects' {"data/$($pack.Name)/objectCollisionTable.s:ENEMYCOLLISION_FLOORMASTER"}
                'active-collisions' {"data/$($pack.Name)/enemyActiveCollisions.s:ENEMY_FLOORMASTER"}
                'link-return' {'object_code/common/specialObjects/link.s:linkState0c'}
                default {'object_code/common/enemies/floorMaster.s:'+ $profile.Key}
            }
            $rows+="$($profile.Key)`t$index`t$($profile.Value[$index])`t$source"
        }
    }
    if($rows.Count -ne 103){throw "$($pack.Name): Floormaster requires102 profile rows."}
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/floormaster.tsv'),$rows)
}

