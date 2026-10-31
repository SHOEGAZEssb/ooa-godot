function Read-CompanionGraphicsRows($pack,$macros,[string]$label,[string]$sprite) {
    $rows=@($macros | Where-Object {
        $pack.Symbols.ContainsKey($_.EnclosingGlobalLabel) -and
        $pack.Symbols[$_.EnclosingGlobalLabel] -eq $pack.Symbols[$label]
    })
    if ($rows.Count -eq 0) { throw "$($pack.Name): ${label}: missing native graphics rows, including aliases." }
    $bytes=Read-MenuRomBytes $pack $label ($rows.Count*3)
    $result=foreach ($index in 0..($rows.Count-1)) {
        $operands=$rows[$index].Operands
        $oam=Convert-AssemblyInteger $operands[0]
        $offset=0; $size=0; $word=0
        if ($operands.Count -eq 4 -and $operands[1] -eq $sprite) {
            $offset=Convert-AssemblyInteger $operands[2]; $size=Convert-AssemblyInteger $operands[3]
            $bank=[int][Math]::Floor($pack.Symbols[$sprite]/0x4000)-[int][Math]::Floor($pack.Symbols['gfxDataBank1a']/0x4000)
            if ($bank -notin @(0,1) -or ($offset -band 0x1f) -ne 0 -or $size -lt 2 -or $size -gt 0x1e -or ($size -band 1) -ne 0) {
                throw "$($pack.Name): ${label}: invalid native graphics range at row$index."
            }
            $word=(($pack.Symbols[$sprite] -band 0x3fff)+0x4000+$offset) -bor $size -bor $bank
        } elseif ($operands.Count -ne 2 -or $operands[1] -ne '$0000') {
            throw "$($pack.Name): ${label}: unsupported native graphics row$index."
        }
        if ($bytes[$index*3] -ne $oam -or [BitConverter]::ToUInt16($bytes,$index*3+1) -ne $word) {
            throw "$($pack.Name): ${label}: source/compiled graphics row$index differs."
        }
        @{ Oam=$oam; Offset=$offset; Size=$size }
    }
    return ,@($result)
}
function Read-CompanionOam($pack,[string]$label) {
    $count=[int](Read-MenuRomBytes $pack $label 1)[0]
    if ($count -lt 1 -or $count -gt 32) { throw "$($pack.Name): ${label}: invalid native OAM extent$count." }
    $bytes=Read-MenuRomBytes $pack $label (1+$count*4)
    return ,@(for ($part=0; $part -lt $count; $part++) {
        ,([int[]]$bytes[(1+$part*4)..(4+$part*4)])
    })
}
foreach ($pack in @(
    @{ Name='ages'; Bytes=$romBytes; Symbols=$agesSymbols; Root=$destination; Defines='ROM_AGES;AGES_ENGINE' },
    @{ Name='seasons'; Bytes=$seasonsRomBytes; Symbols=$seasonsSymbols; Root=(Split-Path $previewDestination -Parent); Defines='ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection=Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $source="data/$($pack.Name)/specialObjectAnimationData.s"; $path=Join-Path $Disassembly $source
        $nativeFrames=[Collections.Generic.List[string]]::new()
        $nativeFrames.Add('# id`tanimation`tframe`tcursor`tgfx`tsource')
        $nativeGraphics=[Collections.Generic.List[string]]::new()
        $nativeGraphics.Add('# id`tgfx`toam-address`tsource')
        $macros=@((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$path`0`0m_SpecialObjectGfxPointer" | ConvertFrom-Json) | Where-Object IsActive)
        foreach ($animal in @(
            @{ Name='ricky'; Id=0x0b; Palette=3; Graphics=52; Animations=37 },
            @{ Name='dimitri'; Id=0x0c; Palette=2; Graphics=54; Animations=40 },
            @{ Name='moosh'; Id=0x0d; Palette=1; Graphics=63; Animations=27 }
        )) {
        $prefix='specialObject'+$animal.Id.ToString('x2')
        $sprite='spr_'+$animal.Name
        $graphics=Read-CompanionGraphicsRows $pack $macros ($prefix+'GfxPointers') $sprite
        $animations=Read-NativeObjectPointerLabels $pack $path ($prefix+'AnimationDataPointers') $connection
        $oamLabels=Read-NativeObjectPointerLabels $pack $path ($prefix+'OamDataPointers') $connection
        if ($graphics.Count -ne $animal.Graphics -or $animations.Count -ne $animal.Animations) { throw "$($pack.Name): $prefix native graphics/animation shape changed." }
        $oam=@($oamLabels | ForEach-Object { ,(Read-CompanionOam $pack $_) })
        for($gfxIndex=0;$gfxIndex -lt $graphics.Count;$gfxIndex++) {
            if($graphics[$gfxIndex].Oam -ge $oamLabels.Count){throw "$($pack.Name): ${prefix}: graphics row$gfxIndex references missing native OAM."}
            $oamLabel=$oamLabels[$graphics[$gfxIndex].Oam]
            $oamAddress=[int]$pack.Symbols[$oamLabel] -band 0x3fff
            $nativeGraphics.Add("$($animal.Id.ToString('x2'))`t$($gfxIndex.ToString('x2'))`t$($oamAddress.ToString('x4'))`t${source}:${prefix};$oamLabel")
        }
        $encodedAnimations=[Collections.Generic.List[string]]::new(); $sourceRows=[Collections.Generic.List[string]]::new()
        $animationIndex=0
        foreach ($label in $animations) {
            $frames=[Collections.Generic.List[string]]::new(); $offsets=[Collections.Generic.List[string]]::new(); $starts=[Collections.Generic.List[int]]::new()
            $vram=[int[]]::new(256); for ($tile=0; $tile -lt 256; $tile++) { $vram[$tile]=-1 }
            $at=0; $loop=-1; $previous=-1; $states=@{}
            for ($visit=0; $visit -lt 256; $visit++) {
                $bytes=Read-MenuRomBytes $pack $label ($at+1); $duration=[int]$bytes[$at]
                if ($duration -eq 0xff) {
                    $bytes=Read-MenuRomBytes $pack $label ($at+2)
                    $target=$at+1+[int]$bytes[$at+1]-256
                    if (-not $starts.Contains($target)) { throw "$($pack.Name): ${label}: native loop targets a non-frame or preceding entry." }
                    $at=$target
                    $bytes=Read-MenuRomBytes $pack $label ($at+1); $duration=[int]$bytes[$at]
                    if ($duration -eq 0xff) { throw "$($pack.Name): ${label}: unsupported chained native animation loop." }
                }
                $bytes=Read-MenuRomBytes $pack $label ($at+3); $gfx=[int]$bytes[$at+1]; $parameter=[int]$bytes[$at+2]
                if ($duration -eq 0 -or $gfx -ge $graphics.Count) { throw "$($pack.Name): ${label}: invalid native frame at byte$at." }
                $row=$graphics[$gfx]
                if ($row.Oam -ge $oam.Count) { throw "$($pack.Name): ${label}: graphics row$gfx references missing OAM." }
                if ($gfx -ne $previous) {
                    for ($tile=0; $tile -lt $row.Size; $tile++) { $vram[$tile]=$row.Offset/16+$tile }
                    $previous=$gfx
                }
                # A loop can revisit its entry with different retained tiles.
                # Cache the complete post-upload state, not just the bytecode offset.
                $state="$at/$previous/"+($vram -join ',')
                if ($states.ContainsKey($state)) { $loop=$states[$state]; break }
                $states[$state]=$frames.Count
                $parts=@(foreach ($cell in $oam[$row.Oam]) {
                    $tile=$cell[2]
                    if ($tile -ge 255 -or $vram[$tile] -lt 0 -or $vram[$tile+1] -ne $vram[$tile]+1) {
                        throw "$($pack.Name): ${label}: graphics row$gfx references unresolved VRAM tile$tile."
                    }
                    ,([int[]]@($cell[0],$cell[1],$vram[$tile],$cell[3]))
                })
                $base=[int]($parts | ForEach-Object { $_[2] } | Measure-Object -Minimum).Minimum
                if (($base -band 1) -ne 0) { throw "$($pack.Name): ${label}: odd sprite source base$base." }
                $cells=@(foreach ($cell in $parts) {
                    $relative=$cell[2]-$base
                    if ($relative -gt 254) { throw "$($pack.Name): ${label}: source tiles exceed one OAM byte." }
                    "$($cell[0]),$($cell[1]),$relative,$($cell[3])"
                }) -join ';'
                $metadata=if ($parameter -eq 0) { "$duration" } else { "$duration,$parameter" }
                $cursor=([int]$pack.Symbols[$label] -band 0x3fff)+0x4000+$at+3
                if($cursor -gt 0x7fff){throw "$($pack.Name): ${label}: native animation cursor crosses its ROM bank."}
                $nativeFrames.Add("$($animal.Id.ToString('x2'))`t$($animationIndex.ToString('x2'))`t$($frames.Count)`t$($cursor.ToString('x4'))`t$($gfx.ToString('x2'))`t${source}:$label")
                $frames.Add("$metadata@$cells"); $offsets.Add(($base*16).ToString('x')); $starts.Add($at); $at+=3
                # Native handlers consume Moosh bit7 and Ricky $ff before another animation advance.
                if (($animal.Id -eq 0x0d -and ($parameter -band 0x80) -ne 0) -or
                    ($animal.Id -eq 0x0b -and $parameter -eq 0xff)) { $loop=0; break }
            }
            if ($loop -lt 0 -or $frames.Count -eq 0) { throw "$($pack.Name): ${label}: missing bounded native animation loop or handler terminal signal." }
            $encoded=$frames -join '|'; if ($loop -gt 0) { $encoded+="~$loop" }
            $encodedAnimations.Add($encoded); $sourceRows.Add(($offsets -join ','))
            $animationIndex++
        }
        $linkGraphics=Read-CompanionGraphicsRows $pack $macros 'specialObject09GfxPointers' 'spr_link'
        $linkOamLabels=Read-NativeObjectPointerLabels $pack $path 'specialObject09OamDataPointers' $connection
        if ($linkGraphics.Count -ne 0x33 -or $linkGraphics[0x32].Size -ne 0 -or $linkOamLabels.Count -ne 0x30) {
            throw "$($pack.Name): native riding Link graphics/retained row/OAM shape changed."
        }
        $linkFrames=[Collections.Generic.List[string]]::new(); $linkOffsets=[Collections.Generic.List[string]]::new()
        for ($index=0; $index -lt $linkGraphics.Count; $index++) {
            $row=$linkGraphics[$index]
            # Native parameter$32 follows$2f while keeping its uploaded Link tiles.
            $upload=if ($index -eq 0x32) { $linkGraphics[0x2f] } else { $row }
            if ($row.Oam -ge $linkOamLabels.Count) { throw "$($pack.Name): riding Link graphics$index has no OAM." }
            $cells=@(foreach ($cell in (Read-CompanionOam $pack $linkOamLabels[$row.Oam])) {
                if ($cell[2]+1 -ge $upload.Size) { throw "$($pack.Name): riding Link graphics$index has an unresolved tile." }
                $cell -join ','
            }) -join ';'
            $linkFrames.Add("127@$cells"); $linkOffsets.Add($upload.Offset.ToString('x4'))
        }
        $attributes=Read-MenuRomBytes $pack 'specialObjectSetOamVariables@data' (($animal.Id+1)*2)
        if ($attributes[$animal.Id*2] -ne 0x60 -or $attributes[$animal.Id*2+1] -ne (8+$animal.Palette)) { throw "$($pack.Name): $prefix native OAM attributes changed." }
        $base64={ param([string]$value) [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($value)) }
        Write-GeneratedTable((Join-Path $pack.Root "metadata/$($animal.Name)_visual.tsv"),@(
            '# sprite`tpalette`tanimations-base64`tanimation-source-offsets-base64`tlink-frames-base64`tlink-source-offsets`tsource',
            "$sprite`t$($animal.Palette)`t$(& $base64 ($encodedAnimations -join "`n"))`t$(& $base64 ($sourceRows -join "`n"))`t$(& $base64 ($linkFrames -join "`n"))`t$($linkOffsets -join ',')`t${source}:$prefix/specialObject09"))
        Copy-GeneratedFile "gfx/common/$sprite.png" "gfx/$sprite.png" $pack.Root
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/companion_native_animation.tsv'),$nativeFrames)
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/companion_native_graphics.tsv'),$nativeGraphics)
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
