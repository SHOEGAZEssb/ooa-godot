# Preserve the native ordered byte streams: pointer fallthrough/defaults are control flow.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $linkPath = Join-Path $Disassembly 'object_code/common/specialObjects/link.s'
        $dispatch = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$linkPath`0linkState0a`0.dw" | ConvertFrom-Json) | Where-Object IsActive)
        $dispatchBytes = Read-MenuRomBytes $pack 'linkState0a' 38
        if ($dispatch.Count -ne 16 -or ($dispatchBytes[@(0,3,4,5)] -join ',') -ne '250,230,15,199') { throw "$($pack.Name): linkState0a native mode dispatch changed." }
        $handlers = @{
            warpTransition0 = 'Standing'; warpTransition1 = 'SetRespawn'; warpTransition2 = 'FadeOut';
            warpTransition3 = 'Walk'; warpTransition4 = 'Instant'; warpTransition5 = 'Fall';
            warpTransition6 = $(if ($pack.Name -eq 'ages') { 'TimeWarp' } else { 'Trampoline' });
            warpTransition7 = 'HollyFall'; warpTransition8 = 'Subrosia'; warpTransition9 = 'Hole';
            warpTransitionA = 'KeepFacing'; warpTransitionB = 'SlowFall'; warpTransitionC = 'ParameterFacing';
            warpTransitionE = 'Shifted'; warpTransitionF = 'Invisible'
        }
        $handlerRows = [Collections.Generic.List[string]]::new(); $handlerRows.Add('# mode`thandler`tsource')
        for ($mode = 0; $mode -lt 16; $mode++) {
            $label = $dispatch[$mode].Operands[0]
            if (-not $handlers.ContainsKey($label) -or [BitConverter]::ToUInt16($dispatchBytes,6 + $mode * 2) -ne (($pack.Symbols[$label] -band 0x3fff) + 0x4000)) { throw "$($pack.Name): linkState0a mode $mode does not resolve to its selected native handler." }
            $handlerRows.Add("$($mode.ToString('x2'))`t$($handlers[$label])`tobject_code/common/specialObjects/link.s:linkState0a:$label")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/warp_handlers.tsv'),$handlerRows)
        $sourcePath = "data/$($pack.Name)/warpSources.s"
        $destPath = "data/$($pack.Name)/warpDestinations.s"
        $sourceMacros = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$(Join-Path $Disassembly $sourcePath)`0`0" | ConvertFrom-Json) | Where-Object IsActive)
        $destMacros = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$(Join-Path $Disassembly $destPath)`0`0" | ConvertFrom-Json) | Where-Object IsActive)
        if (@($sourceMacros | Where-Object { $_.Name -notin @('m_StandardWarp','m_PointerWarp','m_PositionWarp','m_WarpListEndNoDefault','m_WarpListEndWithDefault','m_WarpListFallThrough') }).Count -ne 0 -or
            @($destMacros | Where-Object { $_.Name -ne 'm_WarpDest' }).Count -ne 0) { throw "$($pack.Name): unsupported native warp macro." }
        $sourceRecords = @($sourceMacros | Where-Object { $_.Name -in @('m_StandardWarp','m_PointerWarp','m_PositionWarp','m_WarpListEndNoDefault') })
        $sourceBase = [int]$pack.Symbols['group0WarpSources']; $destBase = [int]$pack.Symbols['group0WarpDestTable']
        $sources = Read-MenuRomBytes $pack 'group0WarpSources' ($sourceRecords.Count * 4)
        $destinations = Read-MenuRomBytes $pack 'group0WarpDestTable' ($destMacros.Count * 3)
        $sourcePointers = Read-MenuRomBytes $pack 'warpSourcesTable' 16
        $destPointers = Read-MenuRomBytes $pack 'warpDestTable' 16
        $path = Join-Path $Disassembly 'code/bank4.s'
        $lookup = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$path`0findWarpSourceAndDest`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -in @('Instruction','MacroInvocation') }).Code -join "`n"
        $hasEndMarker = [int]($lookup -match 'cp \$ff\s+jr z,@warpSourceNotFound')
        foreach ($guard in @('bit 7,a\s+jr nz,@foundWarpSource','bit 6,a\s+jr nz,@pointerWarp','ldh a,\(<hFF8D\)\s+ld b,a\s+jr @nextWarpSource')) {
            if ($lookup -notmatch $guard) { throw "$($pack.Name): native warp source/default/pointer lookup changed ($guard)." }
        }
        $apply = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$path`0applyWarpDest_b04`0" | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -in @('Instruction','MacroInvocation') }).Code -join "`n"
        $biasGroup = -1; $biasAddress = 0
        if ($pack.Name -eq 'seasons') {
            if ($apply -notmatch 'and \$0f\s+cp \$02[\s\S]*ld a,\(wc6e5\)\s+add b') { throw 'Seasons applyWarpDest_b04: native Maku-growth destination offset changed.' }
            $biasGroup = 2; $biasAddress = [int]$pack.Symbols['wc6e5']
        }
        elseif ($apply -match 'wc6e5') { throw 'Ages applyWarpDest_b04 unexpectedly acquired a destination index offset.' }
        $quadrants = Read-MenuRomBytes $pack 'getLinkWarpQuadrant' 26
        if (($quadrants[@(0,3,4,7,9,12,14,16,17,18,19,20,22,23,24,25)] -join ',') -ne '250,71,250,254,250,48,254,120,216,60,201,254,120,216,60,201') { throw "$($pack.Name): native screen-edge quadrant instruction layout changed." }
        $groups = [Collections.Generic.List[string]]::new()
        $groups.Add('# group`tsource-start`tdestination-start`tend-marker`tbias-group`tbias-address`tsmall-groups`tsmall-edge-split`tlarge-edge-split`tsource')
        for ($group = 0; $group -lt 8; $group++) {
            $source = [int]$pack.Symbols["group${group}WarpSources"]; $dest = [int]$pack.Symbols["group${group}WarpDestTable"]
            if ([BitConverter]::ToUInt16($sourcePointers,$group * 2) -ne (($source -band 0x3fff) + 0x4000) -or
                [BitConverter]::ToUInt16($destPointers,$group * 2) -ne (($dest -band 0x3fff) + 0x4000) -or
                ($source - $sourceBase) % 4 -ne 0 -or ($dest - $destBase) % 3 -ne 0 -or
                $source -lt $sourceBase -or $source -ge $sourceBase + $sources.Length -or
                $dest -lt $destBase -or $dest -gt $destBase + $destinations.Length) { throw "$($pack.Name): group `$$($group.ToString('x1')) native warp roots do not resolve within their streams." }
            $groups.Add("$group`t$(($source - $sourceBase) / 4)`t$(($dest - $destBase) / 3)`t$hasEndMarker`t$biasGroup`t$($biasAddress.ToString('x4'))`t$([int]$quadrants[8])`t$($quadrants[15].ToString('x2'))`t$($quadrants[21].ToString('x2'))`t$sourcePath`:group${group}WarpSources;$destPath`:group${group}WarpDestTable")
        }
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add('# index`tflags`tkey`tdata-low`tdata-high`tpointer`tsource')
        for ($index = 0; $index -lt $sourceRecords.Count; $index++) {
            $at = $index * 4; $flags = [int]$sources[$at]; $pointer = -1
            $macro = $sourceRecords[$index]
            # Every source macro emits one four-byte row; end/default/fallthrough markers only modify flags.
            if ($macro.Name -eq 'm_WarpListEndNoDefault') {
                if ($hasEndMarker -ne 1 -or ($sources[$at..($at + 3)] -join ',') -ne '255,0,0,0') { throw "$($pack.Name): ${sourcePath}:$($macro.Line): native no-default marker changed." }
            }
            else {
                if (($flags -band 0x30) -ne 0 -or (($flags -band 0x40) -ne 0) -ne ($macro.Name -eq 'm_PointerWarp')) { throw "$($pack.Name): ${sourcePath}:$($macro.Line): native warp kind/flags changed." }
                if ($macro.Name -eq 'm_PointerWarp') {
                    $label = $macro.Operands[1]; $target = [int]$pack.Symbols[$label]
                    if ($sources[$at + 1] -ne (Convert-AssemblyInteger $macro.Operands[0]) -or ($flags -band 15) -ne 0) { throw "$($pack.Name): ${sourcePath}:$($macro.Line): native pointer room/key changed." }
                    if ([BitConverter]::ToUInt16($sources,$at + 2) -ne (($target -band 0x3fff) + 0x4000) -or
                        ($target - $sourceBase) % 4 -ne 0 -or $target -lt $sourceBase -or $target -ge $sourceBase + $sources.Length) { throw "$($pack.Name): ${sourcePath}:$($macro.Line): unsupported warp pointer $label." }
                    $pointer = ($target - $sourceBase) / 4
                }
                else {
                    $operands = @($macro.Operands | ForEach-Object { Convert-AssemblyInteger $_ })
                    $offset = if ($macro.Name -eq 'm_StandardWarp') { 1 } else { 0 }
                    $edge = if ($offset -eq 1) { $operands[0] } else { 0 }
                    if (($flags -band 15) -ne $edge -or $sources[$at + 1] -ne $operands[$offset] -or
                        $sources[$at + 2] -ne $operands[$offset + 1] -or $sources[$at + 3] -ne (($operands[$offset + 2] -shl 4) -bor $operands[$offset + 3])) { throw "$($pack.Name): ${sourcePath}:$($macro.Line): compiled warp operands differ from source." }
                }
            }
            $rows.Add("$index`t$($flags.ToString('x2'))`t$($sources[$at + 1].ToString('x2'))`t$($sources[$at + 2].ToString('x2'))`t$($sources[$at + 3].ToString('x2'))`t$pointer`t${sourcePath}:$($macro.Line):$($macro.EnclosingGlobalLabel)")
        }
        $destRows = [Collections.Generic.List[string]]::new(); $destRows.Add('# index`troom`tposition`tparameter`ttransition`tsource')
        for ($index = 0; $index -lt $destMacros.Count; $index++) {
            $at = $index * 3; $macro = $destMacros[$index]; $operands = @($macro.Operands | ForEach-Object { Convert-AssemblyInteger $_ })
            if ($destinations[$at] -ne $operands[0] -or $destinations[$at + 1] -ne $operands[1] -or
                $destinations[$at + 2] -ne (($operands[2] -shl 4) -bor $operands[3])) { throw "$($pack.Name): ${destPath}:$($macro.Line): compiled warp destination differs from source." }
            $destRows.Add("$index`t$($destinations[$at].ToString('x2'))`t$($destinations[$at + 1].ToString('x2'))`t$($destinations[$at + 2] -shr 4)`t$($destinations[$at + 2] -band 15)`t${destPath}:$($macro.Line):$($macro.EnclosingGlobalLabel)")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'objects/warps.tsv'),$rows)
        Write-GeneratedTable((Join-Path $pack.Root 'objects/warp_groups.tsv'),$groups)
        Write-GeneratedTable((Join-Path $pack.Root 'objects/warp_destinations.tsv'),$destRows)
        $tiles = [Collections.Generic.List[string]]::new(); $tiles.Add('# mode`tindex`ttile`tparameter`tsource')
        foreach ($stream in (Read-CollisionTableStreams $pack "data/$($pack.Name)/tile_properties/warpTiles.s" 'warpTileTable' 2)) {
            $index = 0
            foreach ($entry in $stream.Rows) {
                if ($entry[1] -gt 1) { throw "$($stream.Source): unsupported warp tile parameter." }
                $tiles.Add("$($stream.Mode)`t$index`t$($entry[0].ToString('x2'))`t$([int]$entry[1])`t$($stream.Source)+$((2 * $index).ToString('x2'))"); $index++
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/warp_tiles.tsv'),$tiles)
        $facing = [Collections.Generic.List[string]]::new(); $facing.Add('# mode`tindex`ttile`tdirection`tsource')
        foreach ($stream in (Read-CollisionTableStreams $pack "data/$($pack.Name)/tile_properties/facingDirAfterWarp.s" 'facingDirAfterWarpTable' 2)) {
            $index = 0
            foreach ($entry in $stream.Rows) {
                if ($entry[1] -gt 3) { throw "$($stream.Source): unsupported warp arrival direction." }
                $facing.Add("$($stream.Mode)`t$index`t$($entry[0].ToString('x2'))`t$([int]$entry[1])`t$($stream.Source)+$((2 * $index).ToString('x2'))"); $index++
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/warp_facing.tsv'),$facing)
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
