$code = Read-ImportText (Join-Path $Disassembly 'object_code/common/items/boomerang.s')
foreach ($pattern in @(
    'ld bc,\(SPEED_1a0<<8\)\|\$28', 'ld bc,\(SPEED_260<<8\)\|\$78',
    '(?s)@state2:.*?ld bc,\$140a.*?itemCheckWithinRangeOfLink',
    '(?s)@state3:.*?ld bc,\$0402.*?ld \(hl\),\$04',
    '(?s)magicBoomerangTryToBreakTile:.*?Item.subid.*?ret z.*?BREAKABLETILESOURCE_07.*?itemTryToBreakTile'
)) { if ($code -notmatch $pattern) { throw "Boomerang native contract changed: $pattern" } }
foreach ($pack in @(
    @{ Name='ages'; Defines='ROM_AGES'; Root=$destination; Bytes=$romBytes; Symbols=$agesSymbols; Variants=1 },
    @{ Name='seasons'; Defines='ROM_SEASONS'; Root=(Split-Path $previewDestination -Parent); Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Variants=2 }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $attributes = Read-MenuRomBytes $pack 'itemAttributes' 28
        $gfx = Read-MenuRomBytes $pack 'itemData' 21
        $start = [int]$pack.Symbols['itemCode06@state0']; $end = [int]$pack.Symbols['itemCode06@state1']
        if ($end -le $start -or ($start -shr 14) -ne (($end-1) -shr 14)) { throw "$($pack.Name): invalid Boomerang initializer extent." }
        $initializer = ([byte[]]$pack.Bytes[$start..($end-1)] | ForEach-Object { $_.ToString('x2') }) -join ' '
        $normal = if ($pack.Name -eq 'ages') { '2e 10 36 41 2e 06 36 28' } else { '01 28 41' }
        if ($initializer -notmatch [regex]::Escape($normal) -or
            ($pack.Variants -eq 2 -and ($initializer -notmatch '01 78 5f' -or $initializer -notmatch '3e 0c 22 22'))) {
            throw "$($pack.Name): compiled Boomerang speed/timeout or magic palette initialization changed."
        }
        if ($gfx[19] -ne 0x4e -or $attributes[25] -ne 0x66 -or $attributes[26] -ne 0xfe) {
            throw "$($pack.Name): ITEM$06 graphics/attributes changed."
        }
        $headers = Read-ImportText (Join-Path $Disassembly "data/$($pack.Name)/uncmpGfxHeaders.s")
        if ($headers -notmatch 'm_GfxHeader spr_boomerang, \$84e1, \$04' -or
            ($pack.Variants -eq 2 -and $headers -notmatch 'm_GfxHeader spr_boomerang, \$84e1, \$04, \$40')) {
            throw "$($pack.Name): Boomerang native weapon uploads changed."
        }
        $path = Join-Path $Disassembly 'data/itemAnimations.s'
        $animations = Read-NativeObjectPointerLabels $pack $path 'item06Animations' $connection
        $oam = Read-NativeObjectPointerLabels $pack $path 'item06OamDataPointers' $connection
        if ($animations.Count -ne 1) { throw "$($pack.Name): Boomerang animation count changed." }
        $encoded = Read-NativeItemAnimation $pack $animations[0] $oam 'loop'
        if (($encoded -split '\|').Count -ne 4) { throw "$($pack.Name): Boomerang rotation must retain four frames." }
        if ($encoded.EndsWith('~0')) { $encoded = $encoded.Substring(0,$encoded.Length-2) }
        $pixels = Read-MenuRomBytes $pack 'spr_boomerang' ($pack.Variants * 64)
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add('# subid`tspeed-raw`toutward-updates`tcatch-updates`tnear-radius`tcatch-radius`tsound`tcollision`tradius-y`tradius-x`tdamage`toam-flags`tbreak-source`tanimation`tsource')
        for ($subid=0; $subid -lt $pack.Variants; $subid++) {
            $speed = if ($subid -eq 0) { 0x41 } else { 0x5f }
            $timeout = if ($subid -eq 0) { 0x28 } else { 0x78 }
            $collision = if ($subid -eq 0) { [int]$attributes[24] } else { 0x96 }
            $flags = if ($subid -eq 0) { [int]$gfx[20] } else { 0x0c }
            $break = if ($subid -eq 0) { -1 } else { 7 }
            $rows.Add("$subid`t$speed`t$timeout`t4`t10`t2`t$($soundIds['SND_BOOMERANG'].ToString('x2'))`t$($collision.ToString('x2'))`t6`t6`tfe`t$($flags.ToString('x2'))`t$break`t$encoded`tobject_code/common/items/boomerang.s;data/itemAnimations.s:$($animations[0])")
            [IO.File]::WriteAllBytes((Join-Path $pack.Root "metadata/boomerang_gfx_$subid.bin"), [byte[]]$pixels[($subid*64)..($subid*64+63)])
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/boomerang.tsv'), $rows)
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
