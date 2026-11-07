# PART$04 is the common counted tail of enemyBoss_dead in both games.
foreach($pack in @(
    @{Name='ages';Bytes=$romBytes;Symbols=$agesSymbols;Root=$destination;Defines='ROM_AGES;AGES_ENGINE'},
    @{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent);Defines='ROM_SEASONS;SEASONS_ENGINE'}
)) {
    $part=Read-MenuRomBytes $pack 'partData' 40
    if(($part[32..39]-join ',') -ne '0,0,0,0,64,12,10,0'){throw "$($pack.Name): PART`$04 properties changed."}
    $connection=Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $path=Join-Path $Disassembly "data/$($pack.Name)/partAnimations.s"
        $animations=Read-NativeObjectPointerLabels $pack $path 'part04Animations' $connection
        $oam=Read-NativeObjectPointerLabels $pack $path 'part04OamDataPointers' $connection
        if($animations.Count -ne 1 -or $oam.Count -ne 13){throw "$($pack.Name): PART`$04 pointer extent changed."}
        $animation=Read-NativeEnemyAnimation $pack $animations[0] $oam $true
        if($animation.Durations.Count -ne 13 -or ($animation.Durations|Measure-Object -Sum).Sum -ne 78){throw "$($pack.Name): PART`$04 requires thirteen frames/78 updates."}
        Write-GeneratedTable((Join-Path $pack.Root 'effects/boss_death_explosion.tsv'),@(
            "# tile-base`tpalette`tanimation","12`t2`t$($animation.Encoded)"))
    } finally {[void](Invoke-AssemblySourceHost $connection 'QUIT' '');$connection.WaitForExit();$connection.Dispose()}
}
