# One common species producer; collision tables remain owned by the selected pack.
function Add-GhiniProfile([string]$owner,[string]$name,[int[]]$values,[string]$source) {
    for ($index=0;$index -lt $values.Count;$index++) {
        $rows.Add("$owner`t$name`t$index`t$($values[$index])`t0`t$source+$($index.ToString('x2'))")
    }
}
$commonRows=$null
foreach ($pack in @(
    @{ Name='ages'; Bytes=$romBytes; Symbols=$agesSymbols },
    @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols }
)) {
    $rows=[Collections.Generic.List[string]]::new()
    $rows.Add("# owner`ttable`tindex`tvalue-a`tvalue-b`tsource")
    $ghiniCodeSource = Read-ImportText (
        Join-Path $Disassembly 'object_code\common\enemies\ghini.s')
    if ($ghiniCodeSource -notmatch
            '(?ms)^@state_uninitialized:.*?ld a,SPEED_80' -or
        $ghiniCodeSource -notmatch
            '(?ms)^ghini_subid00:.*?^@state8:.*?' +
            'ldbc \$18,\$7f.*?ld a,\$30') {
        throw 'Ghini state-entry behavior profile changed.'
    }
    Add-GhiniProfile 'ghini' 'state-profile' `
        @(0x14, 0x30, 0x7f) `
        'object_code/common/enemies/ghini.s:ghini_subid00'

    $targetGhiniSource = $ghiniCodeSource -replace '(?m);[^\r\n]*',''
    if ($targetGhiniSource -notmatch '(?ms)^ghini_subid01:.*?@state8:.*?and \$01.*?@state9:.*?wFrameCounter.*?cp \$08.*?ldbc \$1f,\$1f.*?@stateA:.*?cp \$80.*?@gotoStateC:.*?\$7f.*?SPEED_20' -or
        $targetGhiniSource -notmatch '(?ms)^@state_uninitialized:.*?ld \(hl\),60.*?ld \(hl\),\$10') {
        throw 'ghini.s: subid $01 fade/wander/speed-cycle source changed.'
    }
    Add-GhiniProfile 'ghini' 'spawn-state-profile' @(60,0x10,0x7f,0x80,8,0x1f,0x2a,0x54) `
        'object_code/common/enemies/ghini.s:ghini_subid01'
    foreach ($pattern in @(
        '^enemyCode17:.*?ENEMYSTATUS_NO_HEALTH.*?ecom_updateKnockbackNoSolidity.*?@stunned:.*?Enemy.stunCounter.*?ret nz.*?Enemy.zh\s+ld a,\$fe',
        '^@state_uninitialized:\s+ld a,SPEED_80\s+call ecom_setSpeedAndState8\s+ld l,Enemy.zh\s+ld \(hl\),\$fe.*?objectSetVisiblec1',
        '^ghini_subid02:.*?@state8:.*?inc \(hl\).*?ld \(hl\),SPEED_40.*?ld \(hl\),\$24\s+call ghini_chooseTargetPosition\s+@state9:\s+call ecom_decCounter1.*?jr @stateA.*?and \$07.*?add SPEED_20',
        '^ghini_subid02:.*?@stateA:.*?Enemy.var30\s+call ecom_readPositionVars\s+sub c\s+inc a\s+cp \$03.*?ldh a,\(<hFF8F\)\s+sub b\s+inc a\s+cp \$03.*?ld \(hl\),\$0b.*?ld \(hl\),\$1c\s+jr @stateB.*?call ecom_moveTowardPosition\s+call ghini_updateAnimationFromAngle.*?jp enemyAnimate',
        '^ghini_subid02:.*?@stateB:\s+call ecom_decCounter1\s+jr z,@gotoState8.*?and \$07.*?sub SPEED_20.*?call objectApplySpeed.*?@gotoState8:.*?ld \(hl\),\$08\s+jr @state8',
        '^ghini_chooseTargetPosition:\s+ldbc \$70,\$70\s+call ecom_randomBitwiseAndBCE\s+ld a,b\s+sub \$20\s+jr nc,\+\s+xor a.*?wRoomEdgeY.*?srl a\s+add b\s+sub \$28.*?srl a\s+add c\s+sub \$38.*?Enemy.var30\s+ld \(hl\),b\s+inc l\s+ld \(hl\),c'
    )) {
        if ($targetGhiniSource -notmatch ('(?ms)' + $pattern)) { throw "ghini.s: unsupported ENEMY`$17:`$02 source flow: $pattern" }
    }
    Add-GhiniProfile 'target-ghini' 'state-profile' @(0x14,0x0a,0x05,0x24,0x1c,7,0x70,0x20,0x28,0x38,-0x200,3) `
        'object_code/common/enemies/ghini.s:state_uninitialized/ghini_subid02/ghini_chooseTargetPosition'
    if ($ghiniCodeSource -match '(?m)^\s*\.if' -or
        $ghiniCodeSource -notmatch '(?ms)^ghini_updateMovementAndSetSpeedFromCounter1:.*?cp \$2a.*?cp \$54') {
        throw 'ghini.s: shared speed-cycle thresholds or build selection changed.'
    }
    $initial=Read-MenuRomBytes $pack 'enemyCode17@state_uninitialized' 2
    $speeds=Read-MenuRomBytes $pack 'ghini_updateMovementAndSetSpeedFromCounter1@speeds' 3
    if (($initial -join ',') -ne '62,20' -or ($speeds -join ',') -ne '20,10,5') {
        throw "$($pack.Name): compiled Ghini initial/cycle speeds changed."
    }
    Add-GhiniProfile 'ghini' 'spawn-speeds' $speeds 'object_code/common/enemies/ghini.s:ghini_updateMovementAndSetSpeedFromCounter1@speeds'
    $status=Read-MenuRomBytes $pack 'applyDamageToEnemyOrPart@damageTypeTable' 60
    foreach ($damage in @(0x2c,0x38)) {
        Add-GhiniProfile 'target-ghini' "seed-status-$($damage.ToString('x2'))" $status[$damage..($damage+3)] "code/collisionEffects.s:ENEMYDMG_$($damage.ToString('x2'))"
    }
    if ($rows.Count -ne 35) { throw "$($pack.Name): expected34 Ghini mechanism/status rows." }
    if ($null -eq $commonRows) { $commonRows=$rows.ToArray() }
    elseif (-not [Linq.Enumerable]::SequenceEqual([string[]]$commonRows,[string[]]$rows.ToArray())) { throw 'Ghini native mechanism differs between supported builds.' }
}
Write-GeneratedTable((Join-Path (Split-Path $destination -Parent) 'common/metadata/ghini_behavior.tsv'),$commonRows)
