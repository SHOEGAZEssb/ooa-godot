$rupeeValuesByGame = @{}
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        function Read-CollectionNodes([string]$source, [string]$label, [string]$directive = '.db') {
            $path = Join-Path $Disassembly $source
            if ($label.Contains('@')) {
                $scope, $local = $label.Split('@', 2)
                $nodes = @((Invoke-AssemblySourceHost $connection 'NODES' $path | ConvertFrom-Json) | Where-Object IsActive)
                $starts = @(for ($index = 0; $index -lt $nodes.Count; $index++) {
                    if ($nodes[$index].Kind -eq 'Label' -and $nodes[$index].Name -eq "@$local" -and
                        $nodes[$index].EnclosingGlobalLabel -eq $scope) { $index }
                })
                if ($starts.Count -ne 1) { throw "${source}:${label}: expected one active scoped label." }
                return @(for ($index = $starts[0] + 1; $index -lt $nodes.Count; $index++) {
                    $node = $nodes[$index]
                    if ($node.Kind -eq 'Label') { break }
                    if ($node.Kind -eq 'Data' -and $node.Name -eq $directive) { $node }
                })
            }
            return @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0$label`0$directive" | ConvertFrom-Json) | Where-Object IsActive)
        }
        $source = "data/$($pack.Name)/treasureCollectionBehaviours.s"
        $nodes = @(Read-CollectionNodes $source 'treasureCollectionBehaviourTable')
        if ($nodes.Count -ne 0x68 * 3) { throw "${source}: expected 104 native variable/mode/sound triples." }
        $bytes = Read-MenuRomBytes $pack 'treasureCollectionBehaviourTable' (0x68 * 3)
        $sounds = @{ SND_NONE = 0; SND_GETITEM = 0x4c; SND_GETSEED = 0x5e; MUS_GET_ESSENCE = 0x10 }
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# treasure-id`tvariable`tmode`tsound")
        for ($id = 0; $id -lt 0x68; $id++) {
            $fields = @($nodes[($id * 3)..($id * 3 + 2)] | ForEach-Object {
                if ($_.Operands.Count -ne 1) { throw "${source}: expected scalar behavior operands." }
                $_.Operands[0]
            })
            $variable = $fields[0].TrimStart('<')
            $address = if ($variable -eq '$00') { 0 } elseif ($pack.Symbols.ContainsKey($variable)) {
                if (($pack.Symbols[$variable] -band 0xff00) -ne 0xc600) { throw "${source}: $variable is outside wc600Block." }
                $pack.Symbols[$variable] -band 0xff
            } else { throw "${source}: unresolved native variable $variable." }
            $mode = Convert-AssemblyInteger $fields[1]; $sound = $fields[2]
            if (-not $sounds.ContainsKey($sound) -or $bytes[$id * 3] -ne $address -or
                $bytes[$id * 3 + 1] -ne $mode -or $bytes[$id * 3 + 2] -ne $sounds[$sound]) {
                throw "${source}: treasure `$$($id.ToString('x2')) disagrees with the compiled clean US record."
            }
            $rows.Add("$($id.ToString('x2'))`t$variable`t$($mode.ToString('x2'))`t$sound")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/treasure_behaviours.tsv'), $rows)
        foreach ($spec in @(
            @('giveTreasure_body@extraItemsToAddTable', 'treasure_extra_items.tsv', "# treasure-id`tadditional-treasure-id`tparameter", $(if ($pack.Name -eq 'ages') { 4 } else { 3 })),
            @('giveTreasure_body@itemsToRemoveTable', 'treasure_removed_items.tsv', "# treasure-id`tfirst-treasure-id`tsecond-treasure-id", $(if ($pack.Name -eq 'ages') { 0 } else { 3 }))
        )) {
            $nodes = @(Read-CollectionNodes 'code/treasureAndDrops.s' $spec[0])
            if ($nodes.Count -ne $spec[3] + 1 -or $nodes[-1].Operands.Count -ne 1 -or $nodes[-1].Operands[0] -ne '$00') {
                throw "$($pack.Name) $($spec[0]): native row/terminator boundary changed."
            }
            $bytes = Read-MenuRomBytes $pack $spec[0] ($spec[3] * 3 + 1)
            if ($bytes[-1] -ne 0) { throw "$($pack.Name) $($spec[0]): missing compiled terminator." }
            $rows = [Collections.Generic.List[string]]::new(); $rows.Add($spec[2])
            for ($index = 0; $index -lt $spec[3]; $index++) {
                if ($nodes[$index].Operands.Count -ne 3) { throw "$($pack.Name) $($spec[0]): expected native triples." }
                $values = @($bytes[($index * 3)..($index * 3 + 2)] | ForEach-Object { $_.ToString('x2') }) -join "`t"
                $rows.Add($values)
            }
            Write-GeneratedTable((Join-Path $pack.Root "metadata/$($spec[1])"), $rows)
        }
        $nodes = @(Read-CollectionNodes 'code/treasureAndDrops.s' 'checkIncreaseGashaMaturityForGettingTreasure@data')
        if ($nodes.Count -ne 5 -or $nodes[-1].Operands[0] -ne '$00') { throw "$($pack.Name): native treasure maturity table boundary changed." }
        $bytes = Read-MenuRomBytes $pack 'checkIncreaseGashaMaturityForGettingTreasure@data' 9
        if ($bytes[-1] -ne 0) { throw "$($pack.Name): missing treasure maturity terminator." }
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# treasure-id`tmode`tamount")
        for ($index = 0; $index -lt 4; $index++) {
            if ($nodes[$index].Operands.Count -ne 2) { throw "$($pack.Name): expected treasure maturity pairs." }
            $mode = if ($nodes[$index].Operands[0] -eq 'TREASURE_HEART_REFILL') { 'parameter' } else { 'fixed' }
            $rows.Add("$($bytes[$index * 2].ToString('x2'))`t$mode`t$($bytes[$index * 2 + 1])")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/treasure_gasha_maturity.tsv'), $rows)
        $nodes = @(Read-CollectionNodes 'code/bank0.s' 'getRupeeValue@rupeeValues' '.dw')
        if ($nodes.Count -ne 21) { throw "$($pack.Name) getRupeeValue: expected 21 BCD words." }
        $bytes = Read-MenuRomBytes $pack 'getRupeeValue@rupeeValues' 42
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# index`tamount")
        $amounts = [Collections.Generic.List[int]]::new()
        for ($index = 0; $index -lt 21; $index++) {
            $low = $bytes[2 * $index]; $high = $bytes[2 * $index + 1]
            if (($low -band 15) -gt 9 -or ($low -shr 4) -gt 9 -or $high -gt 9 -or
                [BitConverter]::ToUInt16($bytes, 2 * $index) -ne (Convert-AssemblyInteger $nodes[$index].Operands[0])) {
                throw "$($pack.Name) getRupeeValue: invalid/source-mismatched BCD word `$$($index.ToString('x2'))."
            }
            $amount = $high * 100 + ($low -shr 4) * 10 + ($low -band 15)
            $amounts.Add($amount)
            $rows.Add("$($index.ToString('x2'))`t$amount")
        }
        $rupeeValuesByGame[$pack.Name] = $amounts.ToArray()
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/rupee_values.tsv'), $rows)
    } finally {
        [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
        $connection.WaitForExit(); $connection.Dispose()
    }
}
