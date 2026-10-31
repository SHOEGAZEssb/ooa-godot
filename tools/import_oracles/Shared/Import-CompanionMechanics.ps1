# Common animal mechanics read each configured ROM, independently of story scripts.
foreach ($pack in @(
    @{ Name='ages'; Bytes=$romBytes; Symbols=$agesSymbols; Root=$destination },
    @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Root=(Split-Path $previewDestination -Parent) }
)) {
    $oam=Read-MenuRomBytes $pack 'specialObjectSetOamVariables@data' (0x14*2)
    $oamRows=[Collections.Generic.List[string]]::new()
    $oamRows.Add('# id`ttile-base`tflags`tsource')
    for($id=0;$id -lt 0x14;$id++){
        $oamRows.Add("$($id.ToString('x2'))`t$($oam[$id*2].ToString('x2'))`t$($oam[$id*2+1].ToString('x2'))`tobject_code/common/specialObjects/commonCode.s:specialObjectSetOamVariables@data")
    }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/special_object_oam.tsv'),$oamRows)
    $probes=[Collections.Generic.List[string]]::new()
    $probes.Add('# kind`tindex`tx`ty`tsource')
    foreach ($spec in @(
        @{ Kind='carry'; Label='dimitriTileOffsets'; Count=4; Cumulative=$false; Source='object_code/common/specialObjects/dimitri.s' },
        @{ Kind='cliff'; Label='companionCheckHopDownCliff@directionOffsets'; Count=4; Cumulative=$false; Source='object_code/common/specialObjects/commonCode.s' },
        @{ Kind='collision'; Label='companionCalculateAdjacentWallsBitset@offsets'; Count=8; Cumulative=$true; Source='object_code/common/specialObjects/commonCode.s' }
    )) {
        $bytes=Read-MenuRomBytes $pack $spec.Label ($spec.Count*2)
        $x=0; $y=0
        for ($index=0; $index -lt $spec.Count; $index++) {
            $dy=[int]$bytes[$index*2]; $dx=[int]$bytes[$index*2+1]
            if ($dy -ge 128) { $dy-=256 }; if ($dx -ge 128) { $dx-=256 }
            if ($spec.Cumulative) { $x+=$dx; $y+=$dy } else { $x=$dx; $y=$dy }
            $probes.Add("$($spec.Kind)`t$index`t$x`t$y`t$($spec.Source):$($spec.Label)")
        }
    }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/companion_terrain_probes.tsv'),$probes)
    $maskLabel='checkCollisionPosition_disallowSmallBridges@specialCollisions'
    $bytes=Read-MenuRomBytes $pack $maskLabel 16
    $masks=[Collections.Generic.List[string]]::new()
    $masks.Add('# index`tmask`tsource')
    for ($index=0; $index -lt 16; $index++) {
        $masks.Add("$index`t$($bytes[$index].ToString('x2'))`tcode/bank0.s:${maskLabel}")
    }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/companion_collision_masks.tsv'),$masks)
    $tiles=Read-MenuRomBytes $pack 'checkCollisionForCompanion' 19
    $vine=if($pack.Name -eq 'ages'){0xd4}else{0xdd}
    foreach($operand in @(@(4,0xfe),@(5,($vine+2)),@(8,0xfe),@(9,($vine+1)),@(12,0xfe),@(13,$vine),@(14,0x3e),@(15,3),@(16,0xca))){
        if($tiles[$operand[0]] -ne $operand[1]){throw "$($pack.Name): checkCollisionForCompanion vine operand changed at byte$($operand[0])."}
    }
    $cliff=Read-MenuRomBytes $pack 'companionCheckHopDownCliff' 41
    if($cliff[39] -ne 0xfe -or $cliff[40] -ne $tiles[13]){throw "$($pack.Name): native cliff and collision vine identities differ."}
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/companion_tiles.tsv'),@(
        '# vine-top`tvine-middle`tvine-bottom`tvine-top-collision`tsource',
        "$($tiles[13].ToString('x2'))`t$($tiles[9].ToString('x2'))`t$($tiles[5].ToString('x2'))`t$($tiles[15].ToString('x2'))`tobject_code/common/specialObjects/commonCode.s:checkCollisionForCompanion;companionCheckHopDownCliff"))
    $attributes=Read-MenuRomBytes $pack 'itemAttributes' (0x2c*4)
    $weapons=[Collections.Generic.List[string]]::new()
    $weapons.Add('# item`tcollision-type`tdamage`tsource')
    foreach ($item in @(0x28,0x2a,0x2b)) {
        $at=$item*4
        if (($attributes[$at] -band 0x80) -eq 0 -or $attributes[$at+3] -ne 0) {
            throw "$($pack.Name): data/$($pack.Name)/itemAttributes.s: ITEM_$( $item.ToString('x2')) construction flags changed."
        }
        $type=$attributes[$at] -band 0x7f
        $damage=256-[int]$attributes[$at+2]
        $weapons.Add("$($item.ToString('x2'))`t$($type.ToString('x2'))`t$damage`tdata/$($pack.Name)/itemAttributes.s:ITEM_$( $item.ToString('x2'))")
    }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/companion_weapons.tsv'),$weapons)
    $hover=Read-MenuRomBytes $pack 'mooshState8Substate1' 36
    foreach ($instruction in @(@(3,0xfe),@(13,0x2e),@(15,0x36),@(16,5),@(17,6),@(18,0x9f),
        @(22,0x2d),@(23,0x7e),@(24,0xd6),@(26,0x77),@(27,0x2e),@(29,0x1e),@(31,0x3e),@(33,0x77),@(34,0x12),@(35,0xc9))) {
        if ($hover[$instruction[0]] -ne $instruction[1]) { throw "$($pack.Name): mooshState8Substate1 native water-hover construction changed at byte$($instruction[0])." }
    }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/moosh_hover.tsv'),@(
        '# water-hazard`tframes`texclamation-z-offset`tsource',
        "$($hover[4])`t$($hover[32])`t$(-[int]$hover[25])`tobject_code/common/specialObjects/moosh.s:mooshState8Substate1"))
}
