foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE' },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE' }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $source = "data/$($pack.Name)/itemUsageTables.s"
        foreach ($spec in @(
            @('itemUsageParameterTable', 'parent_item_usage.tsv', "# item-id`tusage`tinput`tsource"),
            @('linkItemAnimationTable', 'parent_item_animation_flags.tsv', "# item-id`tflags`tsource")
        )) {
            $path = Join-Path $Disassembly $source
            $nodes = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$path`0$($spec[0])`0.db" | ConvertFrom-Json) | Where-Object IsActive)
            if ($nodes.Count -ne 32) { throw "${source}:$($spec[0]): expected 32 native item rows." }
            $bytes = Read-MenuRomBytes $pack $spec[0] 64
            $rows = [Collections.Generic.List[string]]::new(); $rows.Add($spec[2])
            for ($index = 0; $index -lt 32; $index++) {
                $node = $nodes[$index]
                if ($node.Operands.Count -ne 2) { throw "${source}:$($spec[0]): expected two-byte rows." }
                $value = Convert-AssemblyInteger $node.Operands[0]
                if ($value -ne $bytes[$index * 2]) { throw "${source}:$($spec[0]): clean US item `$$($index.ToString('x2')) differs from source." }
                $trace = "${source}:$($spec[0])+$((2 * $index).ToString('x2'))"
                if ($spec[0] -eq 'itemUsageParameterTable') {
                    $field = $node.Operands[1].TrimStart('<')
                    if ($field -notin @('wGameKeysPressed', 'wGameKeysJustPressed') -or
                        $bytes[$index * 2 + 1] -ne ($pack.Symbols[$field] -band 0xff) -or ($value -band 15) -gt 5) {
                        throw "${trace}: unsupported native allocation selector/input binding."
                    }
                    $inputKind = if ($field -eq 'wGameKeysJustPressed') { 'just-pressed' } else { 'held' }
                    $rows.Add("$($index.ToString('x2'))`t$($value.ToString('x2'))`t$inputKind`t$trace")
                } else {
                    if ($node.Operands[1] -notmatch '^LINK_ANIM_MODE_\w+$') { throw "${trace}: unsupported animation mode operand." }
                    $rows.Add("$($index.ToString('x2'))`t$($value.ToString('x2'))`t$trace")
                }
            }
            Write-GeneratedTable((Join-Path $pack.Root "metadata/$($spec[1])"), $rows)
        }
    } finally {
        [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
        $connection.WaitForExit(); $connection.Dispose()
    }
}
