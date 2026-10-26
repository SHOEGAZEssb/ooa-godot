# Resolve symbols independently: two aliases at one Ages address can have
# different Seasons addresses, so numeric interval translation is ambiguous.
$commonDestination = Join-Path (Split-Path $destination -Parent) 'common'
$names = [string[]]@(@($agesSymbols.Keys | Where-Object { $_ -cmatch '^w' -and $agesSymbols[$_] -ge 0xc000 -and $agesSymbols[$_] -le 0xdfff }) + @($seasonsSymbols.Keys | Where-Object { $_ -cmatch '^w' -and $seasonsSymbols[$_] -ge 0xc000 -and $seasonsSymbols[$_] -le 0xdfff }) | Select-Object -Unique)
[Array]::Sort($names, [StringComparer]::Ordinal)
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# symbol`tages-address`tseasons-address`tsource")
foreach ($name in $names) {
    $ages = if ($agesSymbols.ContainsKey($name)) { $agesSymbols[$name].ToString('x4') } else { '-' }
    $seasons = if ($seasonsSymbols.ContainsKey($name)) { $seasonsSymbols[$name].ToString('x4') } else { '-' }
    $rows.Add("$name`t$ages`t$seasons`tinclude/wram.s:$name")
}
Write-GeneratedTable((Join-Path $commonDestination 'metadata/wram_fields.tsv'), $rows)
