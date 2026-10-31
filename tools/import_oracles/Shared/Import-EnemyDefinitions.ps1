# Native properties and animation streams, independent of species producers.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $enemyData = Read-MenuRomBytes $pack 'enemyData' 512
        $rows = [Collections.Generic.List[string]]::new()
        $rows.Add('# id`tsubid`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tradius-y`tradius-x`tdamage-quarters`thealth`tanimations-base64`traw-damage')
        $dataPath = Join-Path $Disassembly "data/$($pack.Name)/enemyData.s"
        $dataRows = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$dataPath`0enemyData`0m_EnemyData" | ConvertFrom-Json) | Where-Object IsActive)
        $extraRows = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$dataPath`0extraEnemyData`0.db" | ConvertFrom-Json) | Where-Object IsActive)
        if ($dataRows.Count -ne 128 -or $extraRows.Count -eq 0 -or @($extraRows | Where-Object { $_.Operands.Count -ne 4 }).Count -ne 0) {
            throw "$($pack.Name): invalid native enemy property-table extent."
        }
        $extra = Read-MenuRomBytes $pack 'extraEnemyData' ($extraRows.Count * 4)
        $gfxPath = Join-Path $Disassembly "data/$($pack.Name)/objectGfxHeaders.s"
        $gfxRows = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$gfxPath`0objectGfxHeaderTable`0m_ObjectGfxHeader" | ConvertFrom-Json) | Where-Object IsActive)
        foreach ($spec in @(@(0x10,@(0,1,2,3)),@(0x51,@(2,3)),@(0x2d,@(0)),@(0x16,@(0)),@(0x0c,@(0,1)),@(0x20,@(0,1)),@(0x22,@(0,1)),@(0x17,@(0,1,2)),@(0x34,@(0,1)),@(0x43,@(0)),@(0x08,@(0)))) {
            $id = $spec[0]; $hex = $id.ToString('x2'); $at = $id * 4
            $nativeRow = $dataRows[$id]
            if ($nativeRow.Operands.Count -notin @(3,4)) { throw "$($pack.Name): ENEMY `$$hex has an unsupported property row." }
            if ((Convert-AssemblyInteger $nativeRow.Operands[0]) -ne $enemyData[$at] -or
                (Convert-AssemblyInteger $nativeRow.Operands[1]) -ne $enemyData[$at + 1]) {
                throw "$($pack.Name): ENEMY `$$hex graphics/collision row differs from configured source."
            }
            $propertyRows = $null
            if (($enemyData[$at + 2] -band 0x80) -ne 0) {
                $label = $nativeRow.Operands[2]
                $pointer = (($enemyData[$at + 2] -band 0x7f) -shl 8) -bor $enemyData[$at + 3]
                if ($nativeRow.Operands.Count -ne 3 -or -not $pack.Symbols.ContainsKey($label) -or
                    $pointer -ne (($pack.Symbols[$label] -band 0x3fff) + 0x4000)) {
                    throw "$($pack.Name): ENEMY `$$hex subid pointer differs from configured source."
                }
                $sourceRows = @()
                foreach ($alias in @($pack.Symbols.Keys | Where-Object { $_ -match '^enemy[0-9a-f]{2}SubidData$' -and $pack.Symbols[$_] -eq $pack.Symbols[$label] })) {
                    $sourceRows = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$dataPath`0$alias`0m_EnemySubidData" | ConvertFrom-Json) | Where-Object IsActive)
                    if ($sourceRows.Count -eq 0) { continue }
                    $ends = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$dataPath`0$alias`0m_EnemySubidDataEnd" | ConvertFrom-Json) | Where-Object IsActive)
                    if ($ends.Count -eq 0) { throw "$($pack.Name): ${label}: aliased subid rows require a native terminator." }
                    # Unlabelled unused tables may follow the first terminator.
                    $endLine = ($ends | Sort-Object Line | Select-Object -First 1).Line
                    $sourceRows = @($sourceRows | Where-Object { $_.Line -lt $endLine })
                    break
                }
                if ($sourceRows.Count -eq 0) { throw "$($pack.Name): ${label}: no configured native subid rows, including aliases." }
                $propertyRows = Read-MenuRomBytes $pack $label (2 * $sourceRows.Count)
                for ($index = 0; $index -lt $sourceRows.Count; $index++) {
                    $operands = $sourceRows[$index].Operands
                    if ($operands.Count -ne 2) { throw "$($pack.Name): ${label}: unsupported subid row $index." }
                    $continuation = if ($index + 1 -lt $sourceRows.Count) { 0x80 } else { 0 }
                    if (((Convert-AssemblyInteger $operands[0]) -bor $continuation) -ne $propertyRows[$index * 2] -or
                        (Convert-AssemblyInteger $operands[1]) -ne $propertyRows[$index * 2 + 1]) {
                        throw "$($pack.Name): ${label}: compiled subid continuation/properties differ from configured row $index."
                    }
                }
            } elseif ($nativeRow.Operands.Count -ne 4 -or
                (Convert-AssemblyInteger $nativeRow.Operands[2]) -ne $enemyData[$at + 2] -or
                (Convert-AssemblyInteger $nativeRow.Operands[3]) -ne $enemyData[$at + 3]) {
                throw "$($pack.Name): ENEMY `$$hex inline properties differ from configured source."
            }
            $gfxId = $enemyData[$at]; $gfx = $gfxRows[$gfxId]
            if ($gfx.Operands.Count -ne 1) { throw "$($pack.Name): ENEMY `$$hex has an unsupported composite graphics header." }
            $sprite = $gfx.Operands[0]
            $header = Read-MenuRomBytes $pack 'objectGfxHeaderTable' (($gfxId + 1) * 3)
            if (($header[$gfxId * 3] -band 0x3f) -ne [int][Math]::Floor($pack.Symbols[$sprite] / 0x4000) -or
                (([int]$header[$gfxId * 3 + 1] -shl 8) -bor $header[$gfxId * 3 + 2]) -ne (($pack.Symbols[$sprite] -band 0x3fff) + 0x4000)) {
                throw "$($pack.Name): ENEMY `$$hex compiled graphics pointer differs from selected source."
            }
            $animationPath = Join-Path $Disassembly "data/$($pack.Name)/enemyAnimations.s"
            $animations = Read-NativeObjectPointerLabels $pack $animationPath "enemy${hex}Animations" $connection
            $oam = Read-NativeObjectPointerLabels $pack $animationPath "enemy${hex}OamDataPointers" $connection
            if ($id -eq 0x51) {
                # Supported subids $02/$03 retain animation $00. Later pointer
                # rows belong to the distinct, unimplemented $00/$01 handlers.
                if ($animations.Count -ne 4) { throw "$($pack.Name): native Beetle animation pointer extent changed." }
                $animations = @($animations[0])
            }
            if ($id -eq 0x16) {
                # Beamos uses the first eight poses of a table aliased by other species.
                if ($animations.Count -lt 8) { throw "$($pack.Name): native Beamos poses are missing from the shared pointer stream." }
                $animations = @($animations | Select-Object -First 8)
            }
            if ($id -in @(0x0c,0x20,0x22)) {
                # Moblin routes select cardinal poses $00-$03. Seasons aliases
                # a fifth pose used by other handlers sharing this pointer stream.
                $extent = if ($pack.Name -eq 'seasons') { 5 } else { 4 }
                if ($animations.Count -ne $extent) { throw "$($pack.Name): native Moblin animation pointer extent changed." }
                $animations = @($animations | Select-Object -First 4)
            }
            $encoded = @(for ($animation = 0; $animation -lt $animations.Count; $animation++) {
                # Podoboo's sinking handler consumes parameter $ff and stops
                # advancing this stream until it selects animation $00 again.
                $stop = if($id -eq 0x34 -and $animation -in @(0,3)){1}else{-1}
                (Read-NativeEnemyAnimation $pack $animations[$animation] $oam $false ($id -in @(0x08,0x2d) -and $animation -eq 1) $stop).Encoded
            })
            if ($encoded.Count -ne $(if ($id -eq 0x51) { 1 } elseif ($id -eq 0x16) { 8 } elseif ($id -in @(0x0c,0x20,0x22)) { 4 } elseif ($id -eq 0x34) { 6 } elseif ($id -eq 0x43) { 3 } else { 2 })) { throw "$($pack.Name): ENEMY `$$hex animation extent changed." }
            $base64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($encoded -join "`n"))
            $inverted = [int](Get-ImportSpriteSourceGrayscaleInverted $sprite)
            foreach ($subid in $spec[1]) {
                $propertyIndex = $enemyData[$at + 2]; $flags = $enemyData[$at + 3]
                if ($null -ne $propertyRows) {
                    # The loader stops at the first cleared continuation bit;
                    # higher subids reuse that last row rather than following data.
                    $row = 0
                    while ($row -lt $subid -and ($propertyRows[$row * 2] -band 0x80) -ne 0) { $row++ }
                    $propertyIndex = $propertyRows[$row * 2] -band 0x7f
                    $flags = $propertyRows[$row * 2 + 1]
                }
                if ($propertyIndex -ge $extraRows.Count) { throw "$($pack.Name): ENEMY `$${hex}:`$$($subid.ToString('x2')) extra property index is outside extraEnemyData." }
                $property = $propertyIndex * 4
                $damage = (256 - $extra[$property + 2]) / 2
                $rows.Add("$hex`t$($subid.ToString('x2'))`t$sprite`t$(($flags -band 15) * 2)`t$(($flags -shr 4) -band 7)`t$inverted`t$($extra[$property])`t$($extra[$property + 1])`t$damage`t$($extra[$property + 3])`t$base64`t$($extra[$property + 2].ToString('x2'))")
            }
            Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
        }
        if ($pack.Name -eq 'ages') {
            # The existing complete Ages producer owns this catalog. Verify its
            # rows against the independent compiled-pointer decoder before reuse.
            $existing = $commonEnemyRows
            foreach ($row in $rows | Select-Object -Skip 1) {
                if ($existing -cnotcontains $row) { throw "Ages native enemy definition differs from compiled US data: $($row.Split("`t")[0..1] -join ':')." }
            }
        } else { Write-GeneratedTable((Join-Path $pack.Root 'objects/common_enemies.tsv'),$rows) }

    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
