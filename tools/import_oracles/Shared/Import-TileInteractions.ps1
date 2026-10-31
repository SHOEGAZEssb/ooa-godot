# Native pointer tables select the executed group streams, including aliases.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE'; Texts = $allTexts; Treasures = $treasureObjectRecords; Signs = 42; Chests = 133 },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE'; Texts = $seasonTileInteractionTexts; Treasures = $seasonTreasureObjectRecords; Signs = 56; Chests = 108 }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        foreach ($spec in @(
            @{ File = 'signText'; Table = 'signTextGroupTable'; Prefix = 'signTextGroup'; Kind = 'signs'; Width = 3; End = 0; Count = $pack.Signs; Header = '# group`troom`tposition`ttext-id`tutf8-base64' },
            @{ File = 'chestData'; Table = 'chestDataGroupTable'; Prefix = 'chestGroup'; Kind = 'chests'; Width = 4; End = 255; Count = $pack.Chests; Header = '# group`troom`tposition`ttreasure-object`ttreasure-id`tsubid`tparameter`ttext-id`tgraphic`tamount`tutf8-base64' }
        )) {
            $path = Join-Path $Disassembly "data/$($pack.Name)/$($spec.File).s"
            $nodes = @((Invoke-AssemblySourceHost $connection 'NODES' $path | ConvertFrom-Json) | Where-Object IsActive)
            $pointerNodes = @($nodes | Where-Object { $_.Kind -eq 'Data' -and $_.Name -eq '.dw' -and $_.EnclosingGlobalLabel -eq $spec.Table })
            $pointers = Read-MenuRomBytes $pack $spec.Table 16
            if ($pointerNodes.Count -ne 8) { throw "${path}: expected eight native group pointers." }
            $tableBank = [int]$pack.Symbols[$spec.Table] -band -0x4000
            $rows = [Collections.Generic.List[string]]::new()
            $rows.Add($spec.Header.Replace('`t', "`t"))
            foreach ($group in 0..7) {
                $label = "$($spec.Prefix)$($group)Data"
                $pointer = [BitConverter]::ToUInt16($pointers, $group * 2)
                $start = $tableBank + ($pointer -band 0x3fff)
                if ($pointerNodes[$group].Operands.Count -ne 1 -or $pointerNodes[$group].Operands[0] -ne $label -or
                    -not $pack.Symbols.ContainsKey($label) -or $start -ne $pack.Symbols[$label] -or $pointer -lt 0x4000 -or $pointer -ge 0x8000) {
                    throw "${path}:${label}: group pointer differs from the clean US ROM."
                }
                $starts = @(for ($index = 0; $index -lt $nodes.Count; $index++) {
                    if ($nodes[$index].Kind -eq 'Label' -and $nodes[$index].Name -eq $label) { $index }
                })
                if ($starts.Count -ne 1) { throw "${path}:${label}: expected one active source label." }
                $block = [Collections.Generic.List[object]]::new()
                for ($index = $starts[0] + 1; $index -lt $nodes.Count; $index++) {
                    $node = $nodes[$index]
                    if ($node.Kind -in @('Label', 'Blank', 'Comment')) { continue }
                    if ($node.Kind -notin @('Data', 'MacroInvocation')) { throw "${path}:$($node.Line): unsupported table source '$($node.Code)'." }
                    $block.Add($node)
                    if ($node.Kind -eq 'Data' -and $node.Name -eq '.db' -and $node.Operands.Count -eq 1) { break }
                }
                if ($block.Count -eq 0) { throw "${path}:${label}: missing source stream, including aliases and fallthrough." }
                $at = $start
                for ($index = 0; $index -lt $block.Count; $index++) {
                    $node = $block[$index]
                    if ($node.Kind -eq 'Data' -and $node.Name -eq '.db' -and $node.Operands.Count -eq 1) {
                        if ($index -ne $block.Count - 1 -or (Convert-AssemblyInteger $node.Operands[0]) -ne $spec.End -or
                            $pack.Bytes[$at] -ne $spec.End) { throw "${path}:$($node.Line): invalid native stream terminator." }
                        continue
                    }
                    if ($index -eq $block.Count - 1 -or
                        ($spec.Kind -eq 'signs' -and ($node.Kind -ne 'Data' -or $node.Name -ne '.db')) -or
                        ($spec.Kind -eq 'chests' -and ($node.Kind -ne 'MacroInvocation' -or $node.Name -ne 'm_ChestData')) -or
                        $node.Operands.Count -ne 3 -or $at + $spec.Width -ge $tableBank + 0x4000) {
                        throw "${path}:$($node.Line): unsupported native $($spec.Kind) row '$($node.Code)'."
                    }
                    $position = Convert-AssemblyInteger $node.Operands[0]
                    $room = Convert-AssemblyInteger $node.Operands[1]
                    if ($spec.Kind -eq 'signs') {
                        if ($node.Operands[2] -notmatch '^<TX_2e([0-9a-f]{2})$') { throw "${path}:$($node.Line): invalid sign text operand." }
                        $text = [Convert]::ToInt32($Matches[1], 16)
                        $expected = @($position, $room, $text)
                        $id = 0x2e00 + $text
                        if (-not $pack.Texts.ContainsKey($id)) { throw "${path}:$($node.Line): unresolved TX_$($id.ToString('x4'))." }
                        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pack.Texts[$id]))
                        $rows.Add("$group`t$($room.ToString('x2'))`t$($position.ToString('x2'))`t$($text.ToString('x2'))`t$encoded")
                    } else {
                        $name = $node.Operands[2]
                        if (-not $pack.Treasures.ContainsKey($name)) { throw "${path}:$($node.Line): unresolved $name." }
                        $treasure = $pack.Treasures[$name]
                        $expected = @($position, $room, $treasure.Treasure, $treasure.Subid)
                        $amount = if ($treasure.Treasure -eq 0x28) { $rupeeValuesByGame[$pack.Name][$treasure.Parameter] } else { 0 }
                        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($treasure.Message))
                        $rows.Add("$group`t$($room.ToString('x2'))`t$($position.ToString('x2'))`t$name`t$($treasure.Treasure.ToString('x2'))`t$($treasure.Subid.ToString('x2'))`t$($treasure.Parameter.ToString('x2'))`t$($treasure.TextId.ToString('x2'))`t$($treasure.Graphic.ToString('x2'))`t$amount`t$encoded")
                    }
                    for ($byte = 0; $byte -lt $spec.Width; $byte++) {
                        if ($expected[$byte] -lt 0 -or $expected[$byte] -gt 255 -or $pack.Bytes[$at + $byte] -ne $expected[$byte]) {
                            throw "${path}:$($node.Line): row differs from clean US ROM byte $byte."
                        }
                    }
                    $at += $spec.Width
                }
            }
            if ($rows.Count -ne $spec.Count + 1) { throw "$($pack.Name): expected $($spec.Count) native $($spec.Kind) rows, got $($rows.Count - 1)." }
            Write-GeneratedTable((Join-Path $pack.Root "objects/$($spec.Kind).tsv"), $rows)
        }

        $sourcePath = Join-Path $Disassembly 'code/interactableTiles.s'
        $source = @((Invoke-AssemblySourceHost $connection 'NODES' $sourcePath | ConvertFrom-Json) | Where-Object { $_.IsActive -and $_.Kind -eq 'Instruction' })
        $fallbacks = [Collections.Generic.List[string]]::new()
        $fallbacks.Add('# kind`ttext-id`ttreasure-object`ttreasure-id`tsubid`tparameter`tgraphic`tamount`tmessage-base64`tsource'.Replace('`t', "`t"))
        foreach ($fallback in @(
            @('chest-wrong-side', 0x510d, 'nextToChestTile'),
            @('sign-wrong-side', 0x510e, 'nextToSignTile'),
            @('sign-no-match', 0x0901, 'nextToSignTile')
        )) {
            $id = [int]$fallback[1]; $label = $fallback[2]
            $operand = "bc,TX_$($id.ToString('x4'))"
            if (@($source | Where-Object { $_.EnclosingGlobalLabel -eq $label -and $_.Name -eq 'ld' -and $_.OperandText.Replace(' ', '') -eq $operand }).Count -ne 1) {
                throw "$($pack.Name): ${label} no longer selects TX_$($id.ToString('x4'))."
            }
            $bytes = Read-MenuRomBytes $pack $label 160
            $hex = [BitConverter]::ToString($bytes)
            if (-not $hex.Contains(('01-{0:x2}-{1:x2}' -f ($id -band 255), ($id -shr 8)).ToUpperInvariant()) -or -not $pack.Texts.ContainsKey($id)) {
                throw "$($pack.Name): ${label} fallback differs from the native ROM/text."
            }
            $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pack.Texts[$id]))
            $scope = if ($fallback[0] -eq 'sign-no-match') { '@noMatch' } else { '' }
            $fallbacks.Add("$($fallback[0])`t$($id.ToString('x4'))`t`t`t`t`t`t`t$encoded`tcode/interactableTiles.s:$label$scope/TX_$($id.ToString('x4'))")
        }
        $defaultBytes = Read-MenuRomBytes $pack 'getChestData@chestNotFound' 3
        $record = $pack.Treasures['TREASURE_OBJECT_RUPEES_00']
        if (($defaultBytes -join ',') -ne '1,0,40' -or $record.Treasure -ne 0x28 -or $record.Subid -ne 0) {
            throw "$($pack.Name): getChestData no longer defaults to contents `$2800."
        }
        $amount = $rupeeValuesByGame[$pack.Name][$record.Parameter]
        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($record.Message))
        $fallbacks.Add("chest-no-match`t$($record.TextId.ToString('x4'))`tTREASURE_OBJECT_RUPEES_00`t28`t00`t$($record.Parameter.ToString('x2'))`t$($record.Graphic.ToString('x2'))`t$amount`t$encoded`tcode/bank0.s:getChestData@chestNotFound+data/$($pack.Name)/treasureObjectData.s:TREASURE_OBJECT_RUPEES_00")
        Write-GeneratedTable((Join-Path $pack.Root 'objects/tile_interaction_fallbacks.tsv'), $fallbacks)
    } finally {
        [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
        $connection.WaitForExit(); $connection.Dispose()
    }
}
