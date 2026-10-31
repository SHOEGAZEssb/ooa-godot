# WLA enum db entries advance the flag index; .db entries are range aliases.
$globalFlagValues = @{}; $seasonsGlobalFlagValues = @{}
foreach ($pack in @(
    @{ Name = 'ages'; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE'; Flags = $globalFlagValues },
    @{ Name = 'seasons'; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE'; Flags = $seasonsGlobalFlagValues }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $path = Join-Path $Disassembly 'constants/common/globalFlags.s'
        $json = Invoke-AssemblySourceHost $connection 'NODES' $path
        $nodes = @(($json | ConvertFrom-Json) | Where-Object IsActive)
        $inside = $false; $ended = $false; $value = 0
        $rows = [Collections.Generic.List[string]]::new()
        foreach ($node in $nodes) {
            if ($node.Name -ieq '.enum') {
                if ($inside -or $ended -or $node.Operands.Count -ne 1 -or (Convert-AssemblyInteger $node.Operands[0]) -ne 0) { throw "$($pack.Name): unsupported global flag enum." }
                $inside = $true; continue
            }
            if (-not $inside) { continue }
            if ($node.Name -ieq '.ende') { $inside = $false; $ended = $true; continue }
            if ($node.Kind -in @('Blank', 'Comment', 'Directive')) { continue }
            if ($node.Kind -ne 'MacroInvocation' -or $node.Name -notmatch '^GLOBALFLAG_[A-Za-z0-9_]+$' -or $node.Operands.Count -ne 1) {
                throw "$($pack.Name) constants/common/globalFlags.s:$($node.Line): unsupported enum row '$($node.Code)'."
            }
            if ($node.Operands[0] -eq '.db') { continue }
            if ($node.Operands[0] -ne 'db' -or $pack.Flags.ContainsKey($node.Name)) { throw "$($pack.Name): invalid or duplicate global flag $($node.Name)." }
            $pack.Flags.Add($node.Name, $value)
            $rows.Add("$($value.ToString('x2'))`t$($node.Name)")
            $value++
        }
        if (-not $ended -or $value -ne 0x80) { throw "$($pack.Name): expected a terminated enum with 128 native global flag bytes." }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/global_flags.tsv'), $rows)
    }
    finally {
        [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
        $connection.WaitForExit(); $connection.Dispose()
    }
}
