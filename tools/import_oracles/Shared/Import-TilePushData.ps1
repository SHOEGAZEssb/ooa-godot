# Input dispatch uses six collision profiles in both games; the actor's property
# selector is native collision-mode in Ages and room-group in Seasons.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE'; Profiles = 6; Selector = 'wActiveCollisions'; Entries = 115; Pushes = 51 },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE'; Profiles = 8; Selector = 'wActiveGroup'; Entries = 74; Pushes = 29 }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $propertyPath = "data/$($pack.Name)/tile_properties/pushableTiles.s"
        $symbol = 'pushableTilePropertiesTable'
        $nodes = @((Invoke-AssemblySourceHost $connection 'NODES' (Join-Path $Disassembly $propertyPath) | ConvertFrom-Json) | Where-Object IsActive)
        $pointers = @($nodes | Where-Object { $_.EnclosingGlobalLabel -eq $symbol -and $_.Name -eq 'dbrel' })
        if ($pointers.Count -ne $pack.Profiles) { throw "${propertyPath}: expected$($pack.Profiles) relative property pointers, got$($pointers.Count)." }
        $header = Read-MenuRomBytes $pack $symbol $pack.Profiles
        $propertyBytes = [byte[]]::new($pack.Profiles * 256 * 3)
        for ($index = 0; $index -lt $propertyBytes.Length; $index++) { $propertyBytes[$index] = 0xff }
        for ($profile = 0; $profile -lt $pack.Profiles; $profile++) {
            $pointer = $pointers[$profile]
            if ($pointer.Operands.Count -ne 1) { throw "${propertyPath}: invalid property pointer operands." }
            $label = "$symbol$($pointer.Operands[0])"
            $at = [int]$pack.Symbols[$symbol] + $profile + $header[$profile]
            if (-not $pack.Symbols.ContainsKey($label) -or $at -ne $pack.Symbols[$label]) { throw "${propertyPath}:${label}: compiled relative pointer differs from source." }
            $end = ([int][Math]::Floor($at / 0x4000) + 1) * 0x4000
            $records = 0
            while ($at -lt $end -and $pack.Bytes[$at] -ne 0) {
                if ($at + 4 -gt $end -or ++$records -gt 256) { throw "${propertyPath}:${label}: unterminated four-byte property stream." }
                $offset = ($profile * 256 + $pack.Bytes[$at]) * 3
                # Native lookup takes the first matching row, including fallthrough.
                if ($propertyBytes[$offset] -eq 0xff) {
                    for ($column = 0; $column -lt 3; $column++) { $propertyBytes[$offset + $column] = $pack.Bytes[$at + 1 + $column] }
                }
                $at += 4
            }
            if ($at -ge $end) { throw "${propertyPath}:${label}: property terminator crossed its source bank." }
        }
        $selector = Read-MenuRomBytes $pack 'interactionCode14@loadPushableTileProperties' 3
        $address = [int]$pack.Symbols[$pack.Selector]
        if ($selector[0] -ne 0xfa -or [BitConverter]::ToUInt16($selector,1) -ne $address) { throw "$($pack.Name): INTERAC14 no longer selects $($pack.Selector)." }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/pushblock_profiles.tsv'), @(
            '# selector`tcount`tsource'.Replace('`t',"`t"),
            "$($pack.Selector)`t$($pack.Profiles)`tobject_code/common/interactions/pushblock.s:@loadPushableTileProperties"
        ))
        $parameters = [byte[]]::new(6 * 256)
        $joined = [byte[]]::new(6 * 256 * 4)
        for ($index = 0; $index -lt $parameters.Length; $index++) { $parameters[$index] = 0xff }
        for ($index = 0; $index -lt $joined.Length; $index++) { $joined[$index] = 0xff }
        $somaria = [Collections.Generic.List[string]]::new()
        $somaria.Add("# active-collisions`ttile`tparameter`tsource")
        $entries = 0; $pushes = 0
        $path = "data/$($pack.Name)/tile_properties/interactableTiles.s"
        foreach ($stream in (Read-CollisionTableStreams $pack $path 'interactableTilesTable' 2)) {
            foreach ($row in $stream.Rows) {
                $tile = [int]$row[0]; $parameter = [int]$row[1]; $mode = $stream.Mode
                $offset = $mode * 256 + $tile
                if ($parameters[$offset] -ne 0xff) { continue }
                if (($parameter -band 15) -gt $(if ($pack.Name -eq 'ages') { 6 } else { 8 })) { throw "${path}: unsupported native handler$parameter." }
                $parameters[$offset] = $parameter; $entries++
                if (($parameter -band 15) -ne 0) { continue }
                if ($pack.Name -eq 'ages' -and $tile -eq 0xda) {
                    $local = $stream.Source.Substring($stream.Source.IndexOf('interactableTilesTable') + 'interactableTilesTable'.Length)
                    $somaria.Add("$mode`t$($tile.ToString('x2'))`t$($parameter.ToString('x2'))`t${path}:$local")
                    continue
                }
                # The joined input view follows each normal collision
                # family's matching property profile. Actor execution retains the
                # independent native group selector above (Seasons groups4/5 alias).
                $property = ($mode * 256 + $tile) * 3
                $to = $offset * 4
                $joined[$to] = $parameter
                for ($column = 0; $column -lt 3; $column++) {
                    $joined[$to + 1 + $column] = $(if ($propertyBytes[$property] -eq 0xff) { 0 } else { $propertyBytes[$property + $column] })
                }
                $pushes++
            }
        }
        if ($entries -ne $pack.Entries -or $pushes -ne $pack.Pushes -or $somaria.Count -ne $(if ($pack.Name -eq 'ages') { 7 } else { 1 })) {
            throw "$($pack.Name): invalid native tile profiles: entries$entries, pushes$pushes, Somaria rows$($somaria.Count-1)."
        }
        Write-GeneratedBytes((Join-Path $pack.Root 'metadata/interactableTiles.bin'),$parameters)
        Write-GeneratedBytes((Join-Path $pack.Root 'metadata/native_pushblock_properties.bin'),$propertyBytes)
        Write-GeneratedBytes((Join-Path $pack.Root 'metadata/pushableTiles.bin'),$joined)
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/somaria_push_tiles.tsv'),$somaria)
    }
    finally {
        [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
        $connection.WaitForExit(); $connection.Dispose()
    }
}
