# initializeGame and loadDeathRespawnBufferPreset share their mechanism, but
# each native build supplies its own shop checks and masked preset bytes.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE'; Shop = 0x03fe; Outside = 0x0158 },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE'; Shop = 0x03af; Outside = 0x00c5 }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $path = Join-Path $Disassembly 'code/bank0.s'
        $initialization = @((Invoke-AssemblySourceHost $connection 'LABEL_NODES' "$path`0initializeRoom`0" | ConvertFrom-Json) | Where-Object IsActive)
        $roomCode = @($initialization | Where-Object { $_.OperandText -eq 'roomSpecificCode.runRoomSpecificCode' })
        if ($roomCode.Count -ne $(if ($pack.Name -eq 'ages') { 1 } else { 0 })) {
            throw "$($pack.Name) bank0.initializeRoom: native room-specific checkpoint dispatch changed."
        }
        $checkpointRooms = if ($pack.Name -eq 'ages') { $continuousRespawnRows } else { @("# group`troom") }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/continuous_death_respawn_rooms.tsv'), $checkpointRooms)
        function Read-RespawnNodes([string]$command, [string]$label, [string]$name = '') {
            $path = Join-Path $Disassembly 'code/bank1.s'
            $json = Invoke-AssemblySourceHost $connection $command "$path`0$label`0$name"
            return @(($json | ConvertFrom-Json) | Where-Object IsActive)
        }
        $instructions = @(Read-RespawnNodes 'INSTRUCTIONS' 'initializeGame')
        $comparisons = @($instructions | Where-Object { $_.Name -eq 'ld' -and $_.OperandText -match '^bc,\s*\$[0-9a-f]{4}$' })
        if ($comparisons.Count -ne 2 -or
            (Convert-AssemblyInteger $comparisons[0].Operands[1]) -ne $pack.Shop -or
            (Convert-AssemblyInteger $comparisons[1].Operands[1]) -ne $pack.Outside) {
            throw "$($pack.Name) initializeGame: native shop checkpoint comparisons changed."
        }
        $minimum = -1; $preset = -1
        for ($index = 0; $index -lt $instructions.Count - 1; $index++) {
            $node = $instructions[$index]; $next = $instructions[$index + 1]
            if ($node.Name -eq 'ld' -and ($node.OperandText -replace '\s', '') -eq 'a,(wDeathRespawnBuffer.x)' -and $next.Name -eq 'cp') {
                $minimum = Convert-AssemblyInteger $next.Operands[0]
            }
            if ($node.Name -eq 'ld' -and $node.Operands[0] -eq 'c' -and $next.Name -eq 'call' -and $next.Operands[0] -eq 'loadDeathRespawnBufferPreset') {
                $preset = Convert-AssemblyInteger $node.Operands[1]
            }
        }
        if ($minimum -ne 0x40 -or $preset -ne 3) { throw "$($pack.Name) initializeGame: unsupported shop threshold or repair preset." }
        $presetNodes = @(Read-RespawnNodes 'DATA_DIRECTIVES' '@respawnBuffers' '.db')
        if ($presetNodes.Count -ne 4) { throw "$($pack.Name): expected four native death-respawn presets." }
        $label = 'loadDeathRespawnBufferPreset@respawnBuffers'
        if (-not $pack.Symbols.ContainsKey($label)) { throw "$($pack.Name): missing compiled $label." }
        $start = [int]$pack.Symbols[$label]
        if ($start -lt 0 -or $start + 32 -gt $pack.Bytes.Length -or ($start -band 0x3fff) -gt 0x3fe0) {
            throw "$($pack.Name): checkpoint preset stream crosses its source bank."
        }
        $presets = [Collections.Generic.List[string]]::new(); $presets.Add("# index`tmask`tvalues`tsource")
        $values = @()
        for ($index = 0; $index -lt 4; $index++) {
            $node = $presetNodes[$index]
            if ($node.Operands.Count -ne 8) { throw "$($pack.Name): checkpoint preset $index must contain eight source bytes." }
            $bytes = @($node.Operands | ForEach-Object { Convert-AssemblyInteger $_ })
            for ($offset = 0; $offset -lt 8; $offset++) {
                if ($bytes[$offset] -ne $pack.Bytes[$start + $index * 8 + $offset]) { throw "$($pack.Name): compiled checkpoint preset $index differs from source byte $offset." }
            }
            if (($bytes[0] -band 1) -ne 0) { throw "$($pack.Name): checkpoint preset $index has no terminating mask bit." }
            $fields = @($bytes | ForEach-Object { $_.ToString('x2') })
            $presets.Add("$index`t$($fields[0])`t$($fields[1..7] -join ',')`tcode/bank1.s:loadDeathRespawnBufferPreset@respawnBuffers")
            if ($index -eq $preset) { $values = $fields }
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/death_respawn_presets.tsv'), $presets)
        $repairs = [Collections.Generic.List[string]]::new()
        $repairs.Add("# source_group`tsource_room`tminimum_x`twrite_mask`tgroup`troom`tmodifier`tfacing`ty`tx`tcompanion_id`tsource")
        for ($index = 0; $index -lt 2; $index++) {
            $room = Convert-AssemblyInteger $comparisons[$index].Operands[1]
            $threshold = if ($index -eq 0) { 0 } else { $minimum }
            $repairs.Add(("{0:x2}`t{1:x2}`t{2:x2}`t{3}`tcode/bank1.s:initializeGame/@respawnBuffers[03]" -f ($room -shr 8), ($room -band 0xff), $threshold, ($values -join "`t")))
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/death_respawn_initialization.tsv'), $repairs)
    }
    finally {
        [void](Invoke-AssemblySourceHost $connection 'QUIT' '')
        $connection.WaitForExit(); $connection.Dispose()
    }
}
