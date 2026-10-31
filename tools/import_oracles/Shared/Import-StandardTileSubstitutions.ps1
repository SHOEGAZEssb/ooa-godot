# Native standard substitutions retain their selected profile owner and alias order.
foreach ($pack in @(
    @{ Name = 'ages'; Root = $destination; Bytes = $romBytes; Symbols = $agesSymbols; Profiles = 6; Rows = 50; Selector = 'wActiveCollisions' },
    @{ Name = 'seasons'; Root = (Split-Path $previewDestination -Parent); Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Profiles = 8; Rows = 67; Selector = 'wActiveGroup' }
)) {
    $standardProfiles = if ($pack.Name -eq 'ages') {
        @{ Overworld = 0; Indoors = 1; Dungeons = 2; Sidescrolling = 3; Underwater = 4; Five = 5 }
    } else {
        @{ Overworld = 0; Subrosia = 1; Makutree = 2; Indoors = 3; Dungeons = @(4,5); Sidescrolling = @(6,7) }
    }
    $standardTileSubstitutionSource = Read-ImportText (Join-Path $Disassembly "data/$($pack.Name)/tile_properties/standardTileSubstitutions.s")
    $selector = Read-MenuRomBytes $pack 'applyStandardTileSubstitutions@locFunc' 3
    if ($selector[0] -ne 0xfa -or [BitConverter]::ToUInt16($selector,1) -ne $pack.Symbols[$pack.Selector]) {
        throw "$($pack.Name): standard substitutions lost native $($pack.Selector) selection."
    }
    $standardTileRows = [Collections.Generic.List[string]]::new()
    $standardTileRows.Add('# room-flag`tprofile`treplacement`toriginal`tsource')
    $activeStandardLabels = [Collections.Generic.List[hashtable]]::new()
    foreach ($line in $standardTileSubstitutionSource -split "`r?`n") {
        if ($line -match '^\s*@bit(?<bit>[01237])(?<mode>Overworld|Indoors|Dungeons|Sidescrolling|Underwater|Five|Subrosia|Makutree):') {
            $activeStandardLabels.Add(@{
                Flag = 1 -shl [Convert]::ToInt32($Matches['bit'], 10)
                Profiles = @($standardProfiles[$Matches['mode']])
                Label = "bit$($Matches['bit'])$($Matches['mode'])"
            })
            continue
        }
        if ($activeStandardLabels.Count -eq 0 -or
            $line -notmatch '^\s*\.db\s+\$(?<replacement>[0-9a-f]{2})(?:\s+\$(?<original>[0-9a-f]{2}))?') {
            continue
        }
        $replacement = [Convert]::ToInt32($Matches['replacement'], 16)
        if (-not $Matches.ContainsKey('original') -or $Matches['original'] -eq '') {
            if ($replacement -ne 0) {
                throw "Unexpected standard tile-substitution terminator `$$($replacement.ToString('x2'))."
            }
            $activeStandardLabels.Clear()
            continue
        }
        $original = [Convert]::ToInt32($Matches['original'], 16)
        foreach ($active in $activeStandardLabels) {
            foreach ($profile in $active.Profiles) {
                $standardTileRows.Add(
                    "$($active.Flag.ToString('x2'))`t$profile`t$($replacement.ToString('x2'))`t$($original.ToString('x2'))`tstandardTileSubstitutions@$($active.Label)")
            }
        }
    }

    if ($standardTileRows.Count -ne $pack.Rows + 1) {
        throw "$($pack.Name): expected$($pack.Rows) standard substitution rows, got$($standardTileRows.Count - 1)."
    }
    $native = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $bank = [int]$pack.Symbols['standardTileSubstitutions'] -band -0x4000
    foreach ($bit in @(0,1,2,3,7)) {
        $pointers = Read-MenuRomBytes $pack "standardTileSubstitutions@bit$bit" ($pack.Profiles*2)
        for ($profile = 0; $profile -lt $pack.Profiles; $profile++) {
            $cursor = $bank + ([BitConverter]::ToUInt16($pointers,$profile*2) -band 0x3fff)
            for ($count = 0; $pack.Bytes[$cursor] -ne 0; $count++) {
                if ($count -ge 256) { throw "$($pack.Name): unterminated substitution profile$profile/bit$bit." }
                $key = '{0:x2}|{1}|{2:x2}|{3:x2}' -f (1 -shl $bit),$profile,$pack.Bytes[$cursor],$pack.Bytes[$cursor+1]
                if (-not $native.Add($key)) { throw "$($pack.Name): duplicate compiled substitution $key." }
                $cursor += 2
            }
        }
    }
    foreach ($row in $standardTileRows | Select-Object -Skip 1) {
        $fields = $row.Split("`t")
        if (-not $native.Remove(($fields[0..3] -join '|'))) { throw "$($pack.Name): substitution differs from compiled source: $row." }
    }
    if ($native.Count -ne 0) { throw "$($pack.Name): omitted$($native.Count) compiled substitutions." }
    Write-GeneratedTable((Join-Path $pack.Root 'metadata/standard_tile_substitutions.tsv'),$standardTileRows)
}
