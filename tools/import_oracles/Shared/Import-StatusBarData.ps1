# bank2.s: native money selection, key eligibility and retained HUD tile replacement.
foreach($pack in @(
    @{Name='ages';Bytes=$romBytes;Symbols=$agesSymbols;Root=$destination},
    @{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent)}
)) {
    $heart=Read-MenuRomBytes $pack 'updateStatusBar_body@updateHeartDisplay' 32 'updateStatusBar_body'
    if($heart[14] -ne 0x36 -or $heart[15] -ne 9){throw "$($pack.Name): native HUD money tile changed."}
    $dungeonMask=8; $noKeyMask=0; $oreMask=0; $keyTile=9
    $oreSelector='-'; $selectorAddress='-'; $replacement='-'; $keyGraphics='-'; $oreGraphics='-'
    $rupeeSelector=([int]$pack.Symbols['wNumRupees'] -band 0xff).ToString('x2')
    if($pack.Name -eq 'ages') {
        if($heart[19] -ne 0xcb -or $heart[20] -ne 0x67 -or $heart[23] -ne 0xcb -or $heart[24] -ne 0x5f -or $heart[27] -ne 0x34){
            throw 'Ages: native large-indoors/dungeon key gate changed.'
        }
        $noKeyMask=1 -shl (($heart[20] -shr 3) -band 7)
        $dungeonMask=1 -shl (($heart[24] -shr 3) -band 7)
        $keyTile++
    } else {
        if($heart[19] -ne 0xcb -or $heart[20] -ne 0x5f){throw 'Seasons: native dungeon key gate changed.'}
        $init=Read-MenuRomBytes $pack 'loadCommonGraphics_body' 73
        foreach($operand in @(@(17,0xaf),@(18,0x0e),@(20,0xcb),@(21,0x58),@(24,0xcb),@(25,0x78),@(28,0x3e),@(29,0x10),@(30,0x0e),@(33,0x21),@(36,0xd7),@(37,0x11),@(40,1),@(42,0),@(48,0x21),@(51,0xbe),@(52,0x71),@(55,0x69),@(56,0x26),@(57,0xc6))){
            if($init[$operand[0]] -ne $operand[1]){throw "Seasons: loadCommonGraphics_body currency operand changed at byte$($operand[0])."}
        }
        if($init[19] -ne ($pack.Symbols['wNumRupees'] -band 0xff) -or $init[31] -ne ($pack.Symbols['wNumOreChunks'] -band 0xff) -or
            [BitConverter]::ToUInt16($init,49) -ne $pack.Symbols['wDisplayedMoneyAddress'] -or
            [BitConverter]::ToUInt16($init,38) -ne 0x9090 -or
            [BitConverter]::ToUInt16($init,34) -ne (($pack.Symbols['gfx_key_orechunk'] -band 0x3fff)+0x4000) -or
            $init[41] -ne [int][Math]::Floor($pack.Symbols['gfx_key_orechunk']/0x4000)){
            throw 'Seasons: native money fields or key/ore upload target changed.'
        }
        $dungeonMask=1 -shl (($init[21] -shr 3) -band 7)
        $oreMask=1 -shl (($init[25] -shr 3) -band 7)
        $rupeeSelector=$init[19].ToString('x2'); $oreSelector=$init[31].ToString('x2')
        $selectorAddress=([int]$pack.Symbols['wDisplayedMoneyAddress']).ToString('x4')
        $replacement='09'; $keyGraphics='hud/gfx_key.bin'; $oreGraphics='hud/gfx_ore_chunk.bin'
        $icons=Read-MenuRomBytes $pack 'gfx_key_orechunk' 32
        Write-GeneratedBytes((Join-Path $pack.Root $keyGraphics),[byte[]]$icons[0..15])
        Write-GeneratedBytes((Join-Path $pack.Root $oreGraphics),[byte[]]$icons[16..31])
    }
    Write-GeneratedTable((Join-Path $pack.Root 'hud/currency.tsv'),@(
        '# rupee-selector`tore-selector`tselector-address`tdungeon-mask`tno-key-mask`tore-mask`tkey-tile`treplacement-tile`tkey-graphics`tore-graphics`tsource',
        "$rupeeSelector`t$oreSelector`t$selectorAddress`t$($dungeonMask.ToString('x2'))`t$($noKeyMask.ToString('x2'))`t$($oreMask.ToString('x2'))`t$($keyTile.ToString('x2'))`t$replacement`t$keyGraphics`t$oreGraphics`tcode/bank2.s:loadCommonGraphics_body;updateStatusBar_body@updateHeartDisplay"))
}
