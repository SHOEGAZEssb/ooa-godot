# tryToBreakTile and cliff movement use the same native pointer machinery;
# collision modes, fallthrough and flag/maturity rows are selected by pack.
$itemCommon = Read-ImportText (Join-Path $Disassembly 'object_code/common/items/commonCode1.s')
if ($itemCommon -notmatch '(?s)itemCheckCanPassSolidTile:.*?Item.var3c.*?ret z.*?checkTileIsPassableFromDirection.*?Item.var3e.*?add \(hl\).*?ld \(hl\),a.*?and \$80.*?Item.var3c.*?\$ff') {
    throw 'object_code/common/items/commonCode1.s: shared item cliff elevation/cache contract changed.'
}
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $commonPath = Join-Path $Disassembly 'object_code/common/specialObjects/commonCode.s'
        $floorLabel = 'linkApplyTileTypes@tileType_crackedFloor'
        $floorCode = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$commonPath`0@tileType_crackedFloor`0" | ConvertFrom-Json) |
            Where-Object Kind -eq 'Instruction').Code -join "`n"
        if ($floorCode -notmatch '(?s)ld a,ROCS_RING\s+call cpActiveRing\s+jr z,@tileType_normal.*?cp 32\s+jr c,@notSwimming.*?ld c,a\s+ld a,TILEINDEX_HOLE\s+call breakCrackedFloor\s+xor a\s+ld \(wStandingOnTileCounter\),a') {
            throw "$($pack.Name): commonCode.s:$floorLabel native cracked-floor contract changed."
        }
        $floor = Read-MenuRomBytes $pack $floorLabel 27
        if ($floor[10] -ne 0xfe -or $floor[11] -ne 32 -or $floor[18] -ne 0x3e -or $floor[19] -ne 0xf3) {
            throw "$($pack.Name): compiled cracked-floor threshold/tile operands changed."
        }
        $breakPath = Join-Path $Disassembly 'code/bank0.s'
        $breakCode = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$breakPath`0breakCrackedFloor`0" | ConvertFrom-Json) |
            Where-Object Kind -eq 'Instruction').Code -join "`n"
        if ($breakCode -notmatch '(?s)push bc\s+call setTile\s+pop bc\s+ld a,SND_RUMBLE\s+call playSound\s+call getFreeInteractionSlot\s+ret nz\s+ld \(hl\),INTERAC_FALLDOWNHOLE.*?ld \(hl\),\$80.*?jp setShortPosition_paramC') {
            throw "$($pack.Name): bank0.s:breakCrackedFloor native ordering/allocation contract changed."
        }
        $break = Read-MenuRomBytes $pack 'breakCrackedFloor' 20
        if ($break[5] -ne 0x3e -or $break[6] -ne $soundIds['SND_RUMBLE'] -or $break[14] -ne 0x36 -or
            $break[15] -ne 0x0f -or $break[17] -ne 0x36 -or $break[18] -ne 0x80) {
            throw "$($pack.Name): compiled cracked-floor rumble/effect operands changed."
        }
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# kind`tupdates`treplacement`tsource")
        $rows.Add("floor`t$($floor[11])`t$($floor[19].ToString('x2'))`tobject_code/common/specialObjects/commonCode.s:$floorLabel")
        $iceLabel = 'linkApplyTileTypes@tileType_cracked_ice'
        $ice = Read-MenuRomBytes $pack $iceLabel $(if ($pack.Name -eq 'seasons') { 16 } else { 1 })
        if ($pack.Name -eq 'seasons') {
            if ($ice[3] -ne 0xfe -or $ice[4] -ne 0x20 -or $ice[11] -ne 0x3e -or $ice[12] -ne 0xfd) {
                throw 'Seasons commonCode.s: compiled cracked-ice threshold/water operands changed.'
            }
            $iceCode = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$commonPath`0@tileType_cracked_ice`0" | ConvertFrom-Json) |
                Where-Object Kind -eq 'Instruction').Code -join "`n"
            if ($iceCode -notmatch '(?s)cp \$20\s+jr c,@tileType_ice.*?ld c,a\s+ld a,\$fd\s+call setTile\s*$') {
                throw 'Seasons commonCode.s: cracked ice must fall through to swimming without another effect.'
            }
            $rows.Add("ice`t$($ice[4])`t$($ice[12].ToString('x2'))`tobject_code/common/specialObjects/commonCode.s:$iceLabel")
        } elseif ($ice[0] -ne 0xc9) { throw 'Ages commonCode.s: cracked-ice handler must return.' }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/link_floor_break.tsv'),$rows)
        $path = Join-Path $Disassembly "data/$($pack.Name)/tile_properties/breakableTiles.s"
        $modes = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$path`0breakableTileModes`0m_BreakableTileData" | ConvertFrom-Json) | Where-Object IsActive)
        if ($modes.Count -ne $(if ($pack.Name -eq 'ages') { 51 } else { 48 })) { throw "${path}: unsupported native breakable mode count $($modes.Count)." }
        $native = Read-MenuRomBytes $pack 'breakableTileModes' ($modes.Count * 5)
        $definitions = @()
        for ($index = 0; $index -lt $modes.Count; $index++) {
            $args = $modes[$index].Operands
            if ($args.Count -ne 6 -or $args[0] -notmatch '^%[01]{8}$' -or $args[1] -notmatch '^%[01]{8}$' -or $args[2] -notmatch '^%[01]{4}$') { throw "${path}: unsupported breakable mode `$$($index.ToString('x2'))." }
            $bits = $args[0].Substring(1) + $args[1].Substring(1) + $args[2].Substring(1)
            $mask = 0
            for ($bit = 0; $bit -lt 20; $bit++) { if ($bits[$bit] -eq '1') { $mask = $mask -bor (1 -shl $bit) } }
            $drop = Convert-AssemblyInteger $args[3]; $effect = Convert-AssemblyInteger $args[4]; $replacement = Convert-AssemblyInteger $args[5]
            $offset = $index * 5
            $packed = [int]$native[$offset] -bor ([int]$native[$offset + 1] -shl 8) -bor (([int]$native[$offset + 2] -band 15) -shl 16)
            if ($mask -ne $packed -or $drop -ne ($native[$offset + 2] -shr 4) -or $effect -ne $native[$offset + 3] -or $replacement -ne $native[$offset + 4]) { throw "${path}: mode `$$($index.ToString('x2')) differs from compiled clean US bytes." }
            $definitions += @{ Mask = $mask; Drop = $drop; Effect = $effect; Replacement = $replacement }
        }
        $flags = Read-CollisionTableStreams $pack "data/$($pack.Name)/tile_properties/breakableTileRoomFlags.s" 'tileUpdateRoomFlagsOnBreakTable' 2
        $maturity = Read-CollisionTableStreams $pack "data/$($pack.Name)/tile_properties/breakableTileGashaMaturity.s" 'tileIncreaseGashaMaturityOnBreakTable' 2
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# active-collisions`ttile`tmode`tsource-mask`tdrop`teffect`treplacement`troom-flag-action`tgasha-maturity")
        foreach ($stream in (Read-CollisionTableStreams $pack "data/$($pack.Name)/tile_properties/breakableTiles.s" 'breakableTileCollisionTable' 2)) {
            $seen = [Collections.Generic.HashSet[int]]::new()
            foreach ($entry in $stream.Rows) {
                $tile = [int]$entry[0]; $mode = [int]$entry[1]
                if (-not $seen.Add($tile) -or $mode -ge $definitions.Count) { throw "$($stream.Source): duplicate tile or unsupported breakable mode `$$($tile.ToString('x2'))/`$$($mode.ToString('x2'))." }
                $record = $definitions[$mode]
                $flag = @($flags[$stream.Mode].Rows | Where-Object { $_[0] -eq $tile } | Select-Object -First 1)
                $gasha = @($maturity[$stream.Mode].Rows | Where-Object { $_[0] -eq $tile } | Select-Object -First 1)
                $flagValue = if ($flag.Count) { [int]$flag[0][1] } else { 0xff }
                $gashaValue = if ($gasha.Count) { [int]$gasha[0][1] } else { 0 }
                $rows.Add("$($stream.Mode)`t$($tile.ToString('x2'))`t$($mode.ToString('x2'))`t$($record.Mask.ToString('x5'))`t$($record.Drop.ToString('x1'))`t$($record.Effect.ToString('x2'))`t$($record.Replacement.ToString('x2'))`t$($flagValue.ToString('x2'))`t$gashaValue")
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/breakable_tiles.tsv'), $rows)
        $itemPath = "data/$($pack.Name)/tile_properties/itemPassableTiles.s"
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# collision-set`torder`ttile`tsource")
        foreach ($stream in (Read-CollisionTableStreams $pack $itemPath 'itemPassableTilesTable' 1)) {
            $index = 0
            foreach ($entry in $stream.Rows) {
                $rows.Add("$($stream.Mode)`t$index`t$($entry[0].ToString('x2'))`t$($stream.Source)")
                $index++
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/item_passable_tiles.tsv'), $rows)
        $symbol = 'itemPassableCliffTilesTable'
        $table = [int]$pack.Symbols[$symbol]
        $bank = [int][Math]::Floor($table / 0x4000) * 0x4000
        $pointers = Read-MenuRomBytes $pack $symbol 12
        $labels = @(Read-AssemblyDataDirectives (Join-Path $Disassembly $itemPath) $symbol '.dw' | ForEach-Object { $_.Operands })
        if ($labels.Count -ne 6) { throw "${itemPath}: expected six item cliff pointer streams." }
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# collision-set`tdirection`torder`ttile`televation-delta`tsource")
        for ($mode = 0; $mode -lt 6; $mode++) {
            $family = "$symbol$($labels[$mode])"
            $pointer = [BitConverter]::ToUInt16($pointers, $mode * 2)
            if ($pointer -lt 0x4000 -or $pointer -ge 0x8000 -or -not $pack.Symbols.ContainsKey($family) -or $pack.Symbols[$family] -ne $bank + ($pointer -band 0x3fff)) { throw "${itemPath}:${family}: unresolved item cliff family." }
            $start = [int]$pack.Symbols[$family]
            $relative = Read-MenuRomBytes $pack $family 5
            if ($start + $relative[0] -ne $start + 4 + $relative[4]) { throw "${itemPath}:${family}: final diagonal pointer must wrap to Up." }
            for ($direction = 0; $direction -lt 4; $direction++) {
                $at = $start + $direction + $relative[$direction]
                $source = "${itemPath}:${family}+$($direction.ToString('x2'))"
                $index = 0
                while ($at -lt $bank + 0x4000 -and $pack.Bytes[$at] -ne 0) {
                    if ($at + 1 -ge $bank + 0x4000 -or $index -ge 256 -or $pack.Bytes[$at + 1] -notin @(1,255)) { throw "${source}: invalid or unterminated item cliff tile/delta stream." }
                    $rows.Add("$mode`t$direction`t$index`t$($pack.Bytes[$at].ToString('x2'))`t$($pack.Bytes[$at + 1].ToString('x2'))`t$source")
                    $at += 2; $index++
                }
                if ($at -ge $bank + 0x4000) { throw "${source}: item cliff terminator crossed its bank." }
            }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/item_passable_cliffs.tsv'), $rows)
        foreach ($spec in @(
            @('cliffTiles', 'cliffTilesTable', 'ledge_cliff_tiles', 2, "# active-collisions`ttile`tangle"),
            @('landableTilesFromCliffs', 'landableTileFromCliffExceptions', 'ledge_landable_tiles', 1, "# active-collisions`ttile")
        )) {
            $rows = [Collections.Generic.List[string]]::new(); $rows.Add($spec[4])
            foreach ($stream in (Read-CollisionTableStreams $pack "data/$($pack.Name)/tile_properties/$($spec[0]).s" $spec[1] $spec[3])) {
                foreach ($entry in $stream.Rows) {
                    $fields = @($entry | ForEach-Object { $_.ToString('x2') }) -join "`t"
                    $rows.Add("$($stream.Mode)`t$fields")
                }
            }
            Write-GeneratedTable((Join-Path $pack.Root "metadata/$($spec[2]).tsv"), $rows)
        }
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# direction`tangle`twall-mask`tprobe1-y`tprobe1-x`tprobe2-y`tprobe2-x")
        $probes = Read-MenuRomBytes $pack 'checkLinkJumpingOffCliff@wallDirections' 20
        for ($direction = 0; $direction -lt 4; $direction++) {
            $offset = $direction * 5
            $signed = @(1..4 | ForEach-Object { $value = [int]$probes[$offset + $_]; if ($value -ge 128) { $value - 256 } else { $value } })
            $rows.Add("$direction`t$(($direction * 8).ToString('x2'))`t$($probes[$offset].ToString('x2'))`t$($signed -join "`t")")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/ledge_jump_directions.tsv'), $rows)
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# length`tspeed-raw")
        $speeds = Read-MenuRomBytes $pack 'linkState12@cliffSpeedTable' 11
        for ($index = 0; $index -lt 11; $index++) { $rows.Add("$($index + 1)`t$($speeds[$index].ToString('x2'))") }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/ledge_jump_speeds.tsv'), $rows)
        $linkPath = Join-Path $Disassembly 'object_code/common/specialObjects/link.s'
        $instructions = @((Invoke-AssemblySourceHost $connection 'NODES' $linkPath | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' })
        $code = ($instructions | ForEach-Object Code) -join "`n"
        foreach ($guard in @('ld bc,-\$1c0', 'ld c,\$20\s+call objectUpdateSpeedZ_paramC', 'ldi a,\(hl\)\s+add \$05', 'cp \$0b', 'ld \(hl\),\$ff')) {
            if ($code -notmatch $guard) { throw "${linkPath}: native ledge physics/scan contract changed ($guard)." }
        }
        $pointers = Read-MenuRomBytes $pack 'specialObject00AnimationDataPointers' (0x19 * 2)
        $pointer = [BitConverter]::ToUInt16($pointers, 0x18 * 2)
        $start = [int][Math]::Floor($pack.Symbols['specialObject00AnimationDataPointers'] / 0x4000) * 0x4000 + ($pointer -band 0x3fff)
        $jump = [byte[]]$pack.Bytes[$start..($start + 11)]
        if ($jump[0] -ne 9 -or $jump[3] -ne 9 -or $jump[6] -ne 6 -or $jump[9] -ne 0x7f -or $jump[11] -ne 0xff) { throw "$($pack.Name): native LINK_ANIM_MODE_JUMP timing changed." }
        $constants = @("# key`tvalue", "initial-speed-z`t-448", "transition-speed-z`t-256", "gravity`t32", "jump-sound`t$($soundIds['SND_JUMP'])", "land-sound`t$($soundIds['SND_LAND'])", "feet-offset`t5", "scan-step`t8", "max-speed-length`t11", "animation-phase-0`t9", "animation-phase-1`t9", "animation-phase-2`t6")
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/ledge_jump_constants.tsv'), $constants)
        if ($pack.Name -eq 'seasons') {
            $stumps = [Collections.Generic.List[string]]::new()
            $stumps.Add("# phase`tdirection`tprobe-y`tprobe-x`tspeed-raw`tspeed-z`ttarget-y`ttarget-x`tsource")
            $target = Read-MenuRomBytes $pack 'data_6012' 2
            if (($target -join ',') -ne '2,8') { throw 'Seasons stump landing target offsets changed.' }
            foreach ($spec in @(@('enter','checkLinkPushingAgainstTreeStump'),@('leave','seasonsFunc_05_5ed3'))) {
                $probes = Read-MenuRomBytes $pack "$($spec[1])@relativeTile" 8
                $speeds = Read-MenuRomBytes $pack "$($spec[1])@speedValues" 12
                for ($direction = 0; $direction -lt 4; $direction++) {
                    $y = [int]$probes[$direction * 2]; if ($y -ge 128) { $y -= 256 }
                    $x = [int]$probes[$direction * 2 + 1]; if ($x -ge 128) { $x -= 256 }
                    $speedZ = [BitConverter]::ToInt16($speeds,$direction * 3 + 1)
                    $stumps.Add("$($spec[0])`t$direction`t$y`t$x`t$($speeds[$direction * 3].ToString('x2'))`t$speedZ`t$([int]$target[0])`t$([int]$target[1])`tobject_code/common/specialObjects/link.s:$($spec[1])")
                }
            }
            Write-GeneratedTable((Join-Path $pack.Root 'metadata/stump_jumps.tsv'),$stumps)
        }
    } finally {
        [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
        $connection.WaitForExit(); $connection.Dispose()
    }
}
