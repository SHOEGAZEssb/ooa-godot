# INTERAC_TREASURE overwrites subid with the catalog's graphic, then uses
# interaction60SubidData and its own animation/OAM tables in the selected game.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE'; Catalog = $treasureObjectRecords; Count = 84 },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE'; Catalog = $seasonTreasureObjectRecords; Count = 73 }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $source = "data/$($pack.Name)/interactionData.s"
        $path = Join-Path $Disassembly $source
        $entries = [Collections.Generic.List[object]]::new()
        $inTable = $false
        $terminated = $false
        foreach ($node in @((Invoke-AssemblySourceHost $connection 'NODES' $path | ConvertFrom-Json) | Where-Object IsActive)) {
            if (-not $inTable) {
                if ($node.Kind -eq 'Label' -and $node.Name -eq 'interaction60SubidData') { $inTable = $true }
                continue
            }
            if ($node.Kind -ne 'MacroInvocation') { continue }
            if ($node.Name -eq 'm_InteractionSubidDataEnd') { $terminated = $true; break }
            if ($node.Name -ne 'm_InteractionSubidData') { throw "${source}: unexpected macro $($node.Name) in native treasure graphics." }
            $entries.Add($node)
        }
        if (-not $terminated) { throw "${source}: native treasure graphics lack their continue-bit terminator." }
        if ($entries.Count -ne 0x7c) { throw "${source}: expected all `$7c native treasure graphics rows." }
        $bytes = Read-MenuRomBytes $pack 'interaction60SubidData' ($entries.Count * 3)
        for ($index = 0; $index -lt $entries.Count; $index++) {
            $values = @($entries[$index].Operands | ForEach-Object { Convert-AssemblyInteger $_ })
            if ($values.Count -ne 3 -or $bytes[$index * 3] -ne $values[0] -or
                ($bytes[$index * 3 + 1] -band 0x7f) -ne $values[1] -or $bytes[$index * 3 + 2] -ne $values[2]) {
                throw "${source}: treasure graphic `$$($index.ToString('x2')) differs from compiled data."
            }
        }
        $gfxPath = Join-Path $Disassembly "data/$($pack.Name)/objectGfxHeaders.s"
        $headers = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$gfxPath`0objectGfxHeaderTable`0m_ObjectGfxHeader" | ConvertFrom-Json) | Where-Object IsActive)
        $headerBytes = Read-MenuRomBytes $pack 'objectGfxHeaderTable' ($headers.Count * 3)
        $presentation = Read-InteractionPresentation $pack.Name
        $animationLabels = $presentation.Tables['interaction60Animations']
        $animationPointers = Read-MenuRomBytes $pack 'interaction60Animations' ($animationLabels.Count * 2)
        $graphics = @($pack.Catalog.Values | ForEach-Object { [int]$_.Graphic } | Sort-Object -Unique)
        if ($graphics.Count -ne $pack.Count) { throw "$($pack.Name): native treasure graphic catalog extent changed." }
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add("# graphic`tsprite`ttile-base`tpalette`tdefault-animation`tanimation`tsource-grayscale-inverted")
        foreach ($graphic in $graphics) {
            if ($graphic -ge $entries.Count) { throw "${source}: treasure graphic `$$($graphic.ToString('x2')) is outside its native table." }
            $at = $graphic * 3
            $gfx = [int]$bytes[$at]
            $tile = $bytes[$at + 1] -band 0x7f
            $palette = ($bytes[$at + 2] -shr 4) -band 7
            $animationIndex = $bytes[$at + 2] -band 15
            if ($gfx -ge $headers.Count -or $headers[$gfx].Operands.Count -ne 1) { throw "${source}: unresolved treasure graphics header `$$($gfx.ToString('x2'))." }
            $sprite = $headers[$gfx].Operands[0]
            if (-not $pack.Symbols.ContainsKey($sprite)) { throw "${source}: unresolved treasure sprite $sprite." }
            $header = $gfx * 3
            if (($headerBytes[$header] -band 0x3f) -ne ($pack.Symbols[$sprite] -shr 14) -or
                (([int]$headerBytes[$header + 1] -shl 8) -bor $headerBytes[$header + 2]) -ne (($pack.Symbols[$sprite] -band 0x3fff) + 0x4000)) {
                throw "${source}: compiled treasure graphics header differs from $sprite."
            }
            $animation = Resolve-InteractionPresentation $presentation 0x60 $animationIndex
            if ([string]::IsNullOrWhiteSpace($animation) -or $animation -notmatch '^127(?:,[0-9]+)?@[^|]+$') {
                throw "${source}: treasure graphic `$$($graphic.ToString('x2')) has no represented static native frame."
            }
            $animationLabel = $animationLabels[$animationIndex]
            if ([BitConverter]::ToUInt16($animationPointers, $animationIndex * 2) -ne (($pack.Symbols[$animationLabel] -band 0x3fff) + 0x4000)) {
                throw "${source}: native treasure animation pointer differs from $animationLabel."
            }
            $frame = Read-MenuRomBytes $pack $animationLabel 3
            $pointerOffset = [int]$frame[1]
            if ($frame[0] -ne 0x7f -or ($pointerOffset -band 1) -ne 0) { throw "${source}:${animationLabel}: expected one native static frame with an aligned OAM pointer." }
            $pointerIndex = $presentation.Starts['interaction60OamDataPointers'] + $pointerOffset / 2
            $oamLabel = $presentation.Pointers[$pointerIndex]
            $oamPointers = Read-MenuRomBytes $pack 'interaction60OamDataPointers' ($pointerOffset + 2)
            if ([BitConverter]::ToUInt16($oamPointers, $pointerOffset) -ne (($pack.Symbols[$oamLabel] -band 0x3fff) + 0x4000)) {
                throw "${source}: compiled treasure OAM pointer differs from $oamLabel."
            }
            $count = [int](Read-MenuRomBytes $pack $oamLabel 1)[0]
            if ($count -lt 1 -or $count -gt 40) { throw "${source}:${oamLabel}: invalid treasure OAM count $count." }
            $oam = Read-MenuRomBytes $pack $oamLabel (1 + $count * 4)
            $cells = @(for ($cell = 0; $cell -lt $count; $cell++) { $start = 1 + $cell * 4; $oam[$start..($start + 3)] -join ',' }) -join ';'
            $metadata = if ($frame[2] -eq 0) { '127' } else { "127,$($frame[2])" }
            if ($animation -ne "$metadata@$cells") { throw "${source}:${animationLabel}: imported treasure frame differs from compiled OAM bytes." }
            $inverted = [int](Get-ImportSpriteSourceGrayscaleInverted $sprite)
            $rows.Add("$($graphic.ToString('x2'))`t$sprite`t$($tile.ToString('x2'))`t$($palette.ToString('x2'))`t$($animationIndex.ToString('x2'))`t$animation`t$inverted")
            $spriteSource = Find-ImportSpriteSource $sprite
            Copy-Item -LiteralPath $spriteSource.FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/treasure_object_visuals.tsv'), $rows)
    }
    finally {
        [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
        $connection.WaitForExit(); $connection.Dispose()
    }
}
