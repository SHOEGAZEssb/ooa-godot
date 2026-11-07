$pack=@{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent)}
$source=Read-ImportText (Join-Path $Disassembly 'object_code/seasons/enemies/aquamentus.s')
$partSource=Read-ImportText (Join-Path $Disassembly 'object_code/seasons/parts/aquamentusProjectile.s')
$reward=Read-ImportText (Join-Path $Disassembly 'scripts/seasons/dungeonScripts.s')
if($reward -notmatch '(?ms)^dungeonScript_bossDeath:\s+jumpifroomflagset \$80, spawnHeartContainerCenterOfRoom\s+checknoenemies\s+orroomflag \$80\s+spawnHeartContainerCenterOfRoom:\s+stopifitemflagset\s+setcoords \$58, \$78\s+spawnitem TREASURE_HEART_CONTAINER, \$00\s+writememory wDisableLinkCollisionsAndMenu, \$00\s+scriptend') {
    throw 'Seasons dungeonScript_bossDeath heart/release sequence changed.'
}
foreach($pattern in @(
    '(?ms)^aquamentus_state_uninitialized:.*?ld c,\$20.*?ecom_setZAboveScreen.*?PALH_SPR_AQUAMENTUS.*?enemyBoss_initializeRoom',
    '(?ms)^aquamentus_state_spawner:.*?wcc93.*?ld b,\$03.*?checkBEnemySlotsAvailable.*?ecom_spawnUncountedEnemyWithSubid01.*?ecom_spawnUncountedEnemyWithSubid01.*?ecom_spawnUncountedEnemyWithSubid01.*?jp enemyDelete',
    '(?ms)^aquamentus_body_state8:.*?ld bc,\$020c.*?ENEMYCOLLISION_AQUAMENTUS_BODY.*?ld a,\$50.*?ld \(hl\),\$c0.*?ld \(hl\),90',
    '(?ms)^aquamentus_body_stateA:.*?ld \(hl\),60.*?ld \(hl\),\$00.*?ld \(hl\),\$04.*?MUS_BOSS.*?ld a,\$20',
    '(?ms)^aquamentus_body_stateB:.*?ld \(hl\),150.*?ld \(hl\),\$04.*?ld \(hl\),\$18',
    '(?ms)^aquamentus_body_stateE:.*?cp \$1c.*?cp 148.*?ld a,70.*?ld \(hl\),240.*?ld \(hl\),60.*?ld \(hl\),\$f8',
    '(?ms)^aquamentus_body_stateF:.*?cp \$e8.*?ld b,\$80.*?cp 210.*?ld b,\$c0.*?ld \(hl\),30',
    '(?ms)^aquamentus_decideNextAttack:.*?and \$07.*?rlca\s+rlca\s+and \$03.*?checkFlag.*?ld a,30.*?SPEED_1c0.*?SPEED_40',
    '(?ms)^aquamentus_horn_updateAnimation:.*?inc a.*?^@animations:\s+\.db \$06 \$05 \$05 \$05 \$05 \$06 \$06 \$06\s+\.db \$07 \$08'
)) {if($source -notmatch $pattern){throw "Aquamentus source control contract changed: $pattern"}}
if($partSource -notmatch '(?ms)^@state0:.*?ld \(hl\),\$28.*?ld \(hl\),\$50.*?^@state1:.*?ld bc,\$f0f0.*?^@state2:.*?xor d.*?and \$03' -or
    $partSource -notmatch '(?ms)^func_7081:.*?ld c,\$03.*?ld c,\$fd.*?getFreePartSlot.*?and \$1f') {
    throw 'PART_AQUAMENTUS_PROJECTILE $40 delay, movement or checked spread allocation changed.'
}
$profiles=[ordered]@{}
foreach($entry in @(
    @('falling-speeds','aquamentus_fallingSpeeds',16),@('fire-delays','aquamentus_projectileFireDelayCounters',5),
    @('charge-probabilities','aquamentus_chargeProbabilities',4),@('collision-boxes','aquamentus_collisionBoxSizes',12),
    @('body-animations','aquamentus_animations',6),@('horn-animations','aquamentus_horn_updateAnimation@animations',10),
    @('horn-offsets','aquamentus_hornXYOffsets',18)
)) {$profiles[$entry[0]]=Read-MenuRomBytes $pack $entry[1] $entry[2]}
$palette=Read-MenuRomBytes $pack 'paletteData4890' 8
$profiles['palette']=@(0..3|ForEach-Object{[int]$palette[$_*2]-bor ([int]$palette[$_*2+1]-shl 8)})
$armor=Read-MenuRomBytes $pack 'applyDamageToLink@damageTypeTable' 28
$profiles['attacker-recoil']=@([int]$armor[18],[int]$armor[22],[int]$armor[26])
$paletteSource=Read-ImportText (Join-Path $Disassembly 'data/seasons/paletteHeaders.s')
if($paletteSource -notmatch 'm_PaletteHeaderStart \$80, PALH_SPR_AQUAMENTUS\s+m_PaletteHeaderSpr 6, 1, paletteData4890') {
    throw 'Aquamentus palette header $80 no longer selects sprite palette$06.'
}
$rows=@("# profile`tindex`tvalue`tsource")
foreach($entry in $profiles.GetEnumerator()) {
    for($index=0;$index -lt $entry.Value.Count;$index++) {
        $rows+="$($entry.Key)`t$index`t$($entry.Value[$index])`tobject_code/seasons/enemies/aquamentus.s:$($entry.Key)"
    }
}
if($rows.Count -ne 79){throw 'Aquamentus requires 78 scalar native table entries.'}
Write-GeneratedTable((Join-Path $pack.Root 'metadata/aquamentus.tsv'),$rows)
$part=Read-MenuRomBytes $pack 'partData' (0x41*8)
if(($part[(0x40*8)..(0x40*8+7)]-join ',') -ne '167,134,34,252,1,24,2,0' -or
    ($part[56..63]-join ',') -ne '143,0,0,0,64,0,0,0') {throw 'Seasons Aquamentus projectile/shadow native properties changed.'}
$connection=Start-AssemblySourceHost 'ROM_SEASONS;SEASONS_ENGINE;REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES'
try {
    $path=Join-Path $Disassembly 'data/seasons/partAnimations.s'
    $headers=@((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$(Join-Path $Disassembly 'data/seasons/objectGfxHeaders.s')`0objectGfxHeaderTable`0m_ObjectGfxHeader"|ConvertFrom-Json)|Where-Object IsActive)
    $animations=Read-NativeObjectPointerLabels $pack $path 'part40Animations' $connection
    $oam=Read-NativeObjectPointerLabels $pack $path 'part40OamDataPointers' $connection
    $animation=Read-NativeEnemyAnimation $pack $animations[0] $oam $false
    $sprite=$headers[0xa7].Operands[0]
    Write-GeneratedTable((Join-Path $pack.Root 'effects/aquamentus_projectile.tsv'),@(
        "# sprites`ttile-base`tpalette`tsource-grayscale-inverted`tanimations-base64",
        "$sprite`t24`t2`t$([int](Get-ImportSpriteSourceGrayscaleInverted $sprite))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($animation.Encoded)))"))
    $animations=Read-NativeObjectPointerLabels $pack $path 'part07Animations' $connection
    $oam=Read-NativeObjectPointerLabels $pack $path 'part07OamDataPointers' $connection
    if($animations.Count -ne 5 -or $oam.Count -ne 4){throw 'Seasons PART_SHADOW requires its four static poses in the shared five-pointer stream.'}
    $frames=@(foreach($label in $animations[0..3]){
        $encoded=(Read-NativeEnemyAnimation $pack $label $oam $false).Encoded
        if($encoded -notmatch '^127@(?<oam>[^|~]+)$'){throw "PART_SHADOW $label must be one static looped frame."}
        $Matches.oam
    })
    $sprite=$headers[0x8f].Operands[0]
    Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
    Write-GeneratedTable((Join-Path $pack.Root 'effects/boss_shadow.tsv'),@(
        "# sprite`ttile-base`tpalette`tanimation-0`tanimation-1`tanimation-2`tanimation-3",
        "$sprite`t0`t0`t$($frames -join "`t")"))
} finally {[void](Invoke-AssemblySourceHost $connection 'QUIT' '');$connection.WaitForExit();$connection.Dispose()}
