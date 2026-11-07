# Common ENEMY$0a/$0e and PART$21 data, independently resolved for each game.
$moblinSource=Read-ImportText (Join-Path $Disassembly 'object_code/common/enemies/boomerangMoblin.s')
$partSource=Read-ImportText (Join-Path $Disassembly 'object_code/common/parts/moblinBoomerang.s')
$trapSource=Read-ImportText (Join-Path $Disassembly 'object_code/common/enemies/bladeAndFlameTrap.s')
if($moblinSource -notmatch '(?ms)^@state_uninitialized:\s+ld a,SPEED_80.*?^@state_9:.*?@gotoState8WithRandomAngleAndCounter.*?PART_MOBLIN_BOOMERANG.*?ecom_spawnProjectile\s+ret nz.*?ld \(hl\),\$0a' -or
    $moblinSource -notmatch '(?ms)^@gotoState8WithRandomAngleAndCounter:.*?and \$03.*?@counterVals.*?ecom_setRandomCardinalAngle' -or
    $partSource -notmatch '(?ms)^@state0:.*?ld \(hl\),\$2d.*?ld \(hl\),\$06.*?ld \(hl\),\$50' -or
    $partSource -notmatch '(?ms)^func_541a:.*?and \$03.*?add \$05.*?cp \$50' -or
    $partSource -notmatch '(?ms)^func_542a:.*?ld \(hl\),\$06.*?sub \$05' -or
    $partSource -notmatch '(?ms)^func_53f5:.*?add \$04\s+cp \$09.*?add \$04\s+cp \$09') {
    throw 'Boomerang Moblin route/allocation or PART$21 speed/catch contract changed.'
}
if($trapSource -notmatch '(?ms)^bladeTrap_subid01:\s+bladeTrap_subid02:.*?SPEED_180.*?SPEED_300.*?ld b,\$0d.*?bladeTrap_checkObstructionsToTarget.*?ecom_getTopDownAdjacentWallsBitset.*?SND_UNKNOWN5.*?^@stateA:.*?LARGE_ROOM_HEIGHT/2.*?LARGE_ROOM_WIDTH/2.*?add \$07\s+cp \$0f.*?SPEED_c0.*?SND_CLINK.*?^@stateB:.*?ld \(hl\),\$10' -or
    $trapSource -notmatch '(?ms)^bladeTrap_subid00:.*?SPEED_c0.*?ld b,\$0e.*?ld \(hl\),\$18.*?SND_MOVEBLOCK.*?^@stateA:.*?rrca\s+call c,ecom_applyVelocityForTopDownEnemyNoHoles' -or
    $trapSource -notmatch '(?ms)^bladeTrap_subid05:.*?SPEED_200.*?ld b,\$0e.*?SND_UNKNOWN5.*?@stateA:.*?SPEED_100.*?SND_CLINK.*?@stateB:.*?ld \(hl\),\$10') {
    throw 'Blade-trap alignment, center limit, retraction or subtype speed contract changed.'
}
foreach($pack in @(
    @{Name='ages';Bytes=$romBytes;Symbols=$agesSymbols;Root=$destination;Defines='ROM_AGES;AGES_ENGINE'},
    @{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent);Defines='ROM_SEASONS;SEASONS_ENGINE'}
)) {
    $speed=Read-MenuRomBytes $pack 'enemyCode0a@state_uninitialized' 2
    $routes=Read-MenuRomBytes $pack 'enemyCode0a@counterVals' 4
    $partInit=Read-MenuRomBytes $pack 'partCode21@state0' 14
    $blueInit=Read-MenuRomBytes $pack 'bladeTrap_subid02@state8' 16
    if(($speed -join ',') -ne '62,20' -or ($routes -join ',') -ne '48,64,80,96' -or
        ($partInit -join ',') -ne '98,107,52,46,198,54,45,44,54,6,46,208,54,80' -or
        ($blueInit -join ',') -ne '98,107,52,46,130,126,61,62,60,40,2,62,120,46,176,119') {
        throw "$($pack.Name): compiled Moblin/boomerang/blue-gold initialization differs from selected source."
    }
    $part=Read-MenuRomBytes $pack 'partData' (0x22*8)
    $partAt=0x21*8
    if($part[$partAt+2] -ne 0x22 -or $part[$partAt+3] -ne 0xfc) {throw "$($pack.Name): PART$21 collision properties changed."}
    $rows=@("# index`tvalue`tsource")
    $values=@([int]$speed[1])+@($routes|ForEach-Object{[int]$_})+@(
        [int]$partInit[6],[int]$partInit[9],[int]$partInit[13],5,0x4b,3,
        ([int]$part[$partAt+2]-band 15),4,((256-[int]$part[$partAt+3])/2))
    for($index=0;$index -lt $values.Count;$index++) {
        $source=if($index -eq 0){'enemies/boomerangMoblin.s:@state_uninitialized'}elseif($index -lt 5){'enemies/boomerangMoblin.s:@counterVals'}else{'parts/moblinBoomerang.s:state-operands;partData.s:$21'}
        $rows+="$index`t$($values[$index])`tobject_code/common/$source"
    }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/boomerang_moblin.tsv'),$rows)
    $armor=Read-MenuRomBytes $pack 'applyDamageToLink@damageTypeTable' 28
    if((@($armor[18],$armor[22],$armor[26]) -join ',') -ne '11,19,25') {throw "$($pack.Name): armored LINKDMG recoil counters changed."}
    $profiles=[ordered]@{
        blue=@(8,13,[int]$blueInit[8],0x1e,0x78,0x58,7,16)
        red=@(0x1e,14,24)
        green=@(14,0x50,0x28,16)
        gold=@([int]$blueInit[12])
        armor=@([int]$armor[18],[int]$armor[22],[int]$armor[26])
    }
    $rows=@("# profile`tindex`tvalue`tsource")
    foreach($profile in $profiles.GetEnumerator()) {
        for($index=0;$index -lt $profile.Value.Count;$index++) {
            $source=if($profile.Key -eq 'armor'){'code/collisionEffects.s:applyDamageToLink@damageTypeTable'}else{'object_code/common/enemies/bladeAndFlameTrap.s:'+ $profile.Key+'-state-operands'}
            $rows+="$($profile.Key)`t$index`t$($profile.Value[$index])`t$source"
        }
    }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/blade_trap.tsv'),$rows)
    $connection=Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $path=Join-Path $Disassembly "data/$($pack.Name)/partAnimations.s"
        $animations=Read-NativeObjectPointerLabels $pack $path 'part21Animations' $connection
        $oam=Read-NativeObjectPointerLabels $pack $path 'part21OamDataPointers' $connection
        if($animations.Count -ne 1 -or $oam.Count -ne 4){throw "$($pack.Name): PART$21 animation/OAM extent changed."}
        $animation=Read-NativeEnemyAnimation $pack $animations[0] $oam $false
        if($animation.Durations.Count -ne 4){throw "$($pack.Name): PART$21 requires four spinning frames."}
        $headers=@((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$(Join-Path $Disassembly "data/$($pack.Name)/objectGfxHeaders.s")`0objectGfxHeaderTable`0m_ObjectGfxHeader"|ConvertFrom-Json)|Where-Object IsActive)
        $sprite=$headers[[int]$part[$partAt]].Operands[0]
        Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
        $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($animation.Encoded))
        $inverted=[int](Get-ImportSpriteSourceGrayscaleInverted $sprite)
        Write-GeneratedTable((Join-Path $pack.Root 'effects/moblin_boomerang.tsv'),@(
            "# sprites`ttile-base`tpalette`tsource-grayscale-inverted`tanimations-base64",
            "$sprite`t$($part[$partAt+5])`t$($part[$partAt+6]-band 7)`t$inverted`t$encoded"
        ))
    }
    finally{[void](Invoke-AssemblySourceHost $connection 'QUIT' '');$connection.WaitForExit();$connection.Dispose()}
}
