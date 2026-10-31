# Common body/item mechanics; Ages' stateA departure is a separate content record.
$code=Read-ImportText (Join-Path $Disassembly 'object_code/common/specialObjects/ricky.s')
foreach ($guard in @(
    '(?s)rickyState5Substate0:.*?ld a,\$10.*?SPEED_c0.*?ld bc,-\$180.*?\$08.*?SPEED_200.*?ld c,\$19',
    '(?s)rickyState8:.*?ld c,\$40.*?SND_UNKNOWN5.*?ld c,\$13.*?cp \$1e.*?SND_CHARGE_SWORD.*?SND_SWORDSPIN.*?rickyStartPunch:.*?ld c,\$09.*?SND_SWORDSLASH',
    '(?s)rickyState3:.*?ld c,\$40.*?SND_RICKY.*?ld c,\$20',
    '(?s)rickySetJumpSpeed:.*?-\$300.*?\$08.*?SPEED_140.*?\$0f',
    '(?s)rickyState4:.*?cp \$0e.*?ld a,\$0d',
    '(?s)ld hl,rickyHoleCheckOffsets.*?cp TILEINDEX_HOLE.*?cp TILEINDEX_FD'
)) { if ($code -notmatch $guard) { throw "ricky.s: shared native body contract changed ($guard)." } }
function Convert-RickyVectorRows([byte[]]$bytes,[int]$width) {
    return (@(for ($at=0; $at -lt $bytes.Length; $at+=$width) {
        (@(for ($offset=0;$offset -lt $width;$offset++) {
            $value=[int]$bytes[$at+$offset]
            if (($width -eq 2 -or $offset -ge 2) -and $value -ge 128) { $value-=256 }
            $value
        }) -join ',')
    }) -join ';')
}
foreach ($pack in @(
    @{ Name='ages'; Bytes=$romBytes; Symbols=$agesSymbols; Root=$destination; Defines='ROM_AGES;AGES_ENGINE' },
    @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Root=(Split-Path $previewDestination -Parent); Defines='ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection=Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $idle=Read-MenuRomBytes $pack 'rickyState1' 36 'specialObjectCode_ricky'
        if ($idle[25] -ne 0x0e -or $idle[26] -ne 0x40 -or $idle[30] -ne 1 -or
            $idle[31] -ne 0 -or $idle[32] -ne 0xff) { throw "$($pack.Name): specialObjectCode_ricky:rickyState1 idle-hop/gravity operands changed: $([BitConverter]::ToString($idle))." }
        $cliff=Read-MenuRomBytes $pack 'companionCheckHopDownCliff' 98
        $vine=if($pack.Name -eq 'ages'){0xd4}else{0xdd}
        if ($cliff[39] -ne 0xfe -or $cliff[40] -ne $vine) { throw "$($pack.Name): companionCheckHopDownCliff native vine comparison changed." }
        $masks=Read-MenuRomBytes $pack 'checkCollisionPosition_disallowSmallBridges@specialCollisions' 16
        $maskText=($masks | ForEach-Object {$_.ToString('x2')}) -join ','
        $punch=Convert-RickyVectorRows (Read-MenuRomBytes $pack 'itemCode28@rickyData' 16) 4
        $tornadoOffsets=Convert-RickyVectorRows (Read-MenuRomBytes $pack 'itemCode2a@offsets' 8) 2
        $holes=Convert-RickyVectorRows (Read-MenuRomBytes $pack 'rickyHoleCheckOffsets' 8) 2
        $up=Convert-RickyVectorRows (Read-MenuRomBytes $pack 'rickyCheckHopUpCliff@cliffOffset_oneUp_right' 8) 2
        $landing=Convert-RickyVectorRows (Read-MenuRomBytes $pack 'rickyBreakTilesOnLanding@offsets' 8) 2
        $attributes=Read-MenuRomBytes $pack 'itemAttributes' (0x2b*4)
        $items=Read-MenuRomBytes $pack 'itemData' (0x2b*3)
        if ($attributes[0x28*4] -ne 0x99 -or $attributes[0x2a*4] -ne 0x99 -or
            $attributes[0x28*4+2] -ne 0xfc -or $attributes[0x2a*4+1] -ne 0x66 -or $attributes[0x2a*4+2] -ne 0xfc -or
            $items[0x2a*3] -ne 0 -or $items[0x2a*3+1] -ne 0x28 -or ($items[0x2a*3+2] -band 7) -ne 1) {
            throw "$($pack.Name): ITEM`$28/`$2a native attributes/graphics changed."
        }
        $path=Join-Path $Disassembly 'data/itemAnimations.s'
        $animations=Read-NativeObjectPointerLabels $pack $path 'item2aAnimations' $connection
        $oam=Read-NativeObjectPointerLabels $pack $path 'item2aOamDataPointers' $connection
        if($animations.Count -ne 1 -or $oam.Count -ne 2){throw "$($pack.Name): ITEM`$2a animation/OAM extent changed."}
        $animation=Read-NativeItemAnimation $pack $animations[0] $oam 'loop'
        if($animation.EndsWith('~0')){$animation=$animation.Substring(0,$animation.Length-2)}
        if($animation -ne '8,0@8,0,0,0;8,8,2,0|8,0@8,0,2,32;8,8,0,32'){throw "$($pack.Name): ITEM`$2a native animation changed."}
        $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($animation))
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/ricky_behavior.tsv'),@(
            '# idle-animation`tcancel-animation`tpunch-animation`tcharge-animation`thop-animation`tground-speed`thop-delay`thop-speed-z`thop-gravity`thop-speed`tlanding-delay`tpunch-lifetime`tpunch-damage`tpunch-boxes`tcharge-updates`ttornado-speed`ttornado-radius-y`ttornado-radius-x`ttornado-damage`ttornado-offsets`ttornado-sprite`ttornado-tile-base`ttornado-palette`ttornado-animation-base64`tjump-sound`tcharge-sound`tsword-spin-sound`tsword-slash-sound`tpunch-cue-sound`tlong-jump-animation`tlong-jump-speed-z`tlong-jump-delay`tlong-jump-speed`tcliff-down-delay`tcliff-down-speed-z`tvine-top-tile`tcompanion-collision-masks`thole-tiles`thole-offsets`tcliff-up-probes`tlanding-probes`twater-animation`thole-animation`tidle-hop-speed-z`tidle-gravity`tsource',
            "20`t05`t09`t13`t19`t1e`t16`t-384`t40`t50`t8`t20`t$(256-$attributes[0x28*4+2])`t$punch`t30`t78`t$($attributes[0x2a*4+1] -shr 4)`t$($attributes[0x2a*4+1] -band 15)`t$(256-$attributes[0x2a*4+2])`t$tornadoOffsets`tspr_common_sprites`t$($items[0x2a*3+1].ToString('x2'))`t$($items[0x2a*3+2] -band 7)`t$encoded`t53`t4f`t6b`t74`t75`t0f`t-768`t8`t32`t20`t-704`t$($cliff[40].ToString('x2'))`t$maskText`tf3,fd`t$holes`t$up`t$landing`t0e`t0d`t$([BitConverter]::ToInt16($idle,31))`t$($idle[26].ToString('x2'))`tobject_code/common/specialObjects/ricky.s;commonCode.s;items/rickyMooshAttack.s;items/rickyTornado.s;data/$($pack.Name)/itemAttributes.s;data/itemAnimations.s"))
    }
    finally{[void](Invoke-AssemblySourceHost $connection 'QUIT' '');$connection.WaitForExit();$connection.Dispose()}
}
