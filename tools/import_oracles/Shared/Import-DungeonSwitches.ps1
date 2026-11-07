# PART$05, INTERAC$78 and their pre-object room-entry writes share wSwitchState.
$code=Read-ImportText (Join-Path $Disassembly 'code/collisionEffects.s')
$switchSource=Read-ImportText (Join-Path $Disassembly 'object_code/common/parts/switch.s')
$height=[regex]::Match($switchSource,'(?ms)^@state0:.*?ld l,Part.zh\s+ld \(hl\),\$(?<height>[0-9a-f]{2})')
if(-not $height.Success -or
    $code -notmatch '(?ms)^collisionEffect26:\s+ldhl LINKDMG_1c, ENEMYDMG_34\s+jr applyDamageToBothObjects' -or
    $code -notmatch '\.db \$60 \$e4 \$00 \$00 ; ENEMYDMG_34' -or
    $code -notmatch '(?ms)^collisionEffect20:.*?cp ITEM_28.*?res 7,\(hl\).*?call func_07_47b7.*?ldhl LINKDMG_24, ENEMYDMG_44' -or
    $code -notmatch '\.db \$50 \$00 \$00 \$00 ; ENEMYDMG_44') {
    throw 'PART$05 initialization/collision-effect/lockout contract changed.'
}
$tiles=@{}
foreach($constant in Read-AssemblyConstants (Join-Path $Disassembly 'constants/common/tileIndices.s')) {
    if($constant.Name -in @('TILEINDEX_DUNGEON_SWITCH_OFF','TILEINDEX_DUNGEON_SWITCH_ON','TILEINDEX_OVERWORLD_SWITCH_ON')) {
        $tiles[$constant.Name]=Convert-AssemblyInteger $constant.Operands[0]
    }
}
$sounds=Read-ImportText (Join-Path $Disassembly 'constants/common/music.s')
if($tiles.Count -ne 3 -or $sounds -notmatch '(?m)^\s*SND_SWITCH\s+db\s+; \$7e') {throw 'Missing common switch tile/sound constants.'}
foreach($pack in @(
    @{Name='ages';Bytes=$romBytes;Symbols=$agesSymbols;Root=$destination},
    @{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent)}
)) {
    $part=Read-MenuRomBytes $pack 'partData' 48
    if(($part[40..47]-join ',') -ne '0,131,68,255,64,8,0,0') {throw "$($pack.Name): PART$05 properties changed."}
    $active=Read-ImportText (Join-Path $Disassembly "data/$($pack.Name)/partActiveCollisions.s")
    $mask=[regex]::Match($active,'(?m)^\s*dbrev (?<bits>%[01]{8} %[01]{8} %[01]{8} %[01]{8}) ; 0x05\s*$')
    if(-not $mask.Success) {throw "$($pack.Name): missing PART$05 active mask."}
    $bits=$mask.Groups['bits'].Value.Replace('%','').Replace(' ','')
    $effects=Read-MenuRomBytes $pack 'objectCollisionTable' 128
    $rows=@("# item`tenabled`teffect`tlockout`tsource")
    for($item=0;$item -lt 32;$item++) {
        $effect=[int]$effects[0x60+$item]
        if($bits[$item] -eq '1' -and $effect -notin @(0x20,0x26)) {throw "$($pack.Name): unsupported switch collision$item/effect$effect."}
        $lockout=if($effect -eq 0x26){'1c'}else{'00'}
        $rows+="$($item.ToString('x2'))`t$($bits[$item])`t$($effect.ToString('x2'))`t$lockout`tdata/$($pack.Name)/partActiveCollisions.s:partActiveCollisions+0014;data/$($pack.Name)/objectCollisionTable.s:objectCollisionTable+$((0x60+$item).ToString('x4'))"
    }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/part_switch_collisions.tsv'),$rows)
    $z=[Convert]::ToInt32($height.Groups['height'].Value,16);if($z -ge 128){$z-=256}
    Write-GeneratedTable((Join-Path $pack.Root 'objects/part_switch_behavior.tsv'),@(
        "# key`tvalue","radius-y`t$($part[42]-shr 4)","radius-x`t$($part[42]-band 15)","collision-z`t$z",
        "off-tile`t$($tiles['TILEINDEX_DUNGEON_SWITCH_OFF'])","on-tile`t$($tiles['TILEINDEX_DUNGEON_SWITCH_ON'])",
        "overworld-on-tile`t$($tiles['TILEINDEX_OVERWORLD_SWITCH_ON'])","sound`t126"
    ))
    $pairs=Read-MenuRomBytes $pack 'interactionCode78@tileReplacement' 48
    $rows=@("# index`toff`ton")
    for($index=0;$index -lt 24;$index++){$rows+="$index`t$($pairs[$index*2].ToString('x2'))`t$($pairs[$index*2+1].ToString('x2'))"}
    Write-GeneratedTable((Join-Path $pack.Root 'objects/switch_tile_toggler.tsv'),$rows)
    $rows=@("# group`troom`tmask`tposition`ttile`tsource")
    foreach($group in @(4,5)) {
        $label="replaceSwitchTiles@group${group}SwitchData"
        for($index=0;;$index++) {
            if($index -ge 64){throw "$($pack.Name): unterminated $label."}
            $bytes=Read-MenuRomBytes $pack $label ($index*4+1)
            if($bytes[$index*4] -eq 0){break}
            $bytes=Read-MenuRomBytes $pack $label ($index*4+4)
            $offset=$index*4
            if($bytes[$offset+1] -eq 0){throw "$($pack.Name): $label row$index has a zero switch mask."}
            $rows+="$group`t$($bytes[$offset].ToString('x2'))`t$($bytes[$offset+1].ToString('x2'))`t$($bytes[$offset+3].ToString('x2'))`t$($bytes[$offset+2].ToString('x2'))`tcommonTileSubstitutions.s:replaceSwitchTiles.@group${group}SwitchData"
        }
    }
    if($rows.Count -ne $(if($pack.Name -eq 'ages'){13}else{19})){throw "$($pack.Name): switch-entry table extent changed."}
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/switch_tile_replacements.tsv'),$rows)
}
