# INTERAC_ERA_OR_SEASON_INFO uses one motion handler and pack-selected graphics/gates.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE'; Count = 2 },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE'; Count = 4 }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $path = Join-Path $Disassembly 'object_code/common/interactions/eraOrSeasonInfo.s'
        $nodes = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$path`0interactionCodee0`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -in @('Instruction','MacroInvocation') })
        $code = $nodes.Code -join "`n"
        foreach ($guard in @('ld \(de\),a\s+ld a,', 'call interactionInitGraphics\s+call interactionSetAlwaysUpdateBit',
            'ld l,Interaction.yh\s+ld \(hl\),\$0a\s+ld l,Interaction.xh\s+ld \(hl\),\$b0',
            'sub \$04\s+ld \(hl\),a\s+cp \$10', 'ld l,Interaction.counter1\s+ld \(hl\),40',
            'call interactionDecCounter1\s+ret nz', 'ld \(hl\),\$06', 'sub \$06\s+ld \(hl\),a', 'jp interactionDelete')) {
            if ($code -notmatch $guard) { throw "$($pack.Name): eraOrSeasonInfo.s motion/state-zero contract changed ($guard)." }
        }
        $displayPath = Join-Path $Disassembly 'code/bank1.s'
        $display = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$displayPath`0checkDisplayEraOrSeasonInfo`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -in @('Instruction','MacroInvocation') }).Code -join "`n"
        if ($display -notmatch 'call getFreeInteractionSlot\s+ret nz\s+ld \(hl\),INTERAC_ERA_OR_SEASON_INFO') { throw "$($pack.Name): native banner allocation changed." }
        $displayBytes = Read-MenuRomBytes $pack 'checkDisplayEraOrSeasonInfo' 2
        if ($displayBytes[0] -ne 0x3e) { throw "$($pack.Name): banner suppression flag operand changed." }
        $suppressFlag = $displayBytes[1]
        if ($pack.Name -eq 'ages') {
            if ($code -notmatch 'ld a,\(wTilesetFlags\)\s+and TILESETFLAG_PAST\s+rlca' -or
                $display -notmatch 'ld a,\(wSentBackByStrangeForce\)\s+dec a\s+ret z' -or
                $display -notmatch 'bit TILESETFLAG_BIT_LARGE_INDOORS,a\s+ret nz\s+bit 0,a\s+ret z' -or $suppressFlag -ne 0x16) { throw 'Ages native era banner predicates changed.' }
            $variant = 'tileset-past'; $predicate = 'outdoors'; $sentBack = [int]$pack.Symbols['wSentBackByStrangeForce']
            $masks = "01`t10`t80"
        }
        else {
            if ($code -notmatch 'ld a,\(wLoadingRoomPack\)\s+inc a\s+jr z,\+\s+ld a,\(wRoomStateModifier\)' -or
                $display -notmatch 'ld a,\(wActiveGroup\)\s+or a\s+ret nz' -or $suppressFlag -ne 0x2f) { throw 'Seasons native season banner predicates changed.' }
            $variant = 'room-pack-season'; $predicate = 'group-zero'; $sentBack = 0
            $masks = "00`t00`t00"
        }
        $profile = @('# variant-source`tdisplay-source`tsuppress-global-flag`toutdoors-mask`tlarge-indoors-mask`tpast-mask`tsent-back-address`tsource',
            "$variant`t$predicate`t$($suppressFlag.ToString('x2'))`t$masks`t$($sentBack.ToString('x4'))`tcode/bank1.s:checkDisplayEraOrSeasonInfo;object_code/common/interactions/eraOrSeasonInfo.s:state0")
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/era_info.tsv'),$profile)
        $graphics = Read-MenuRomBytes $pack 'interactione0SubidData' ($pack.Count * 3)
        $headerPath = Join-Path $Disassembly "data/$($pack.Name)/objectGfxHeaders.s"
        $headers = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$headerPath`0`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Name -eq 'm_ObjectGfxHeader' })
        $presentation = Read-InteractionPresentation $pack.Name
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add('# subid`tsprite`ttile-base`tpalette`tanimation`tstart-y`tstart-x`tenter-step`ttarget-x`thold-updates`texit-step`texit-updates`tsource')
        for ($subid = 0; $subid -lt $pack.Count; $subid++) {
            $gfx = $graphics[$subid * 3]; $tile = $graphics[$subid * 3 + 1] -band 0x7f; $flags = $graphics[$subid * 3 + 2]
            if ($gfx -ge $headers.Count -or ($flags -band 15) -ne 0) { throw "$($pack.Name): INTERAC `$e0 graphics/default animation changed." }
            $sprite = $headers[$gfx].Operands[0]
            $animation = Resolve-InteractionPresentation $presentation 0xe0 ($flags -band 15)
            if ($animation -ne '127@8,248,0,0;8,0,2,0;8,8,4,0;8,16,6,0') { throw "$($pack.Name): native banner OAM/animation changed." }
            $sourceSprite = Find-ImportSpriteSource $sprite
            Copy-Item -LiteralPath $sourceSprite.FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
            $rows.Add("$($subid.ToString('x2'))`t$sprite`t$($tile.ToString('x2'))`t$($flags -shr 4)`t$animation`t0a`tb0`t4`t10`t40`t6`t6`tdata/$($pack.Name)/interactionData.s:interactione0SubidData;object_code/common/interactions/eraOrSeasonInfo.s")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'effects/era_info.tsv'),$rows)
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
