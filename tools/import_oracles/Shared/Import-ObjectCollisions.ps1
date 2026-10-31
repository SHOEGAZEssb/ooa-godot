# Native target masks and mode rows belong to the selected pack, not an item.
foreach ($pack in @(
    @{ Name = 'ages'; Bytes = $romBytes; Symbols = $agesSymbols; Root = $destination; Defines = 'ROM_AGES;AGES_ENGINE'; Modes = 0x7d; Parts = 0x5a },
    @{ Name = 'seasons'; Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Root = (Split-Path $previewDestination -Parent); Defines = 'ROM_SEASONS;SEASONS_ENGINE'; Modes = 0x73; Parts = 0x54 }
)) {
    $connection = Start-AssemblySourceHost "$($pack.Defines);REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES"
    try {
        $path = "data/$($pack.Name)/objectCollisionTable.s"
        $nodes = @((Invoke-AssemblySourceHost $connection 'DATA_DIRECTIVES' "$(Join-Path $Disassembly $path)`0objectCollisionTable`0.db" | ConvertFrom-Json) | Where-Object IsActive)
        $values = @($nodes | ForEach-Object { $_.Operands } | ForEach-Object {
            if ($_ -notmatch '^\$[0-9a-f]{2}$') { throw "${path}: unsupported collision operand $_." }
            [Convert]::ToByte($_.Substring(1),16)
        })
        $bytes = Read-MenuRomBytes $pack 'objectCollisionTable' ($pack.Modes * 32)
        if ($nodes.Count -ne $pack.Modes * 2 -or $values.Count -ne $bytes.Length -or
            -not [Linq.Enumerable]::SequenceEqual([byte[]]$values,[byte[]]$bytes)) { throw "${path}: source/compiled collision row extent differs." }
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# mode`teffects`tsource")
        for ($mode = 0; $mode -lt $pack.Modes; $mode++) {
            $rows.Add("$($mode.ToString('x2'))`t$([BitConverter]::ToString([byte[]]$bytes[($mode * 32)..($mode * 32 + 31)]).Replace('-','').ToLowerInvariant())`t${path}:objectCollisionTable+$((32 * $mode).ToString('x4'))")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/object_collision_effects.tsv'),$rows)
        foreach ($kind in @('enemy','part')) {
            $path = "data/$($pack.Name)/${kind}ActiveCollisions.s"; $label = "${kind}ActiveCollisions"
            $nodes = @((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$(Join-Path $Disassembly $path)`0$label`0dbrev" | ConvertFrom-Json) | Where-Object IsActive)
            $count = if ($kind -eq 'enemy') { 128 } else { $pack.Parts }
            $bytes = Read-MenuRomBytes $pack $label ($count * 4)
            if ($nodes.Count -ne $count) { throw "${path}: expected `$$(($count).ToString('x2')) native masks." }
            $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# id`tmask`tsource")
            for ($id = 0; $id -lt $count; $id++) {
                $operands = $nodes[$id].Operands
                if ($operands.Count -ne 4) { throw "${path}: malformed dbrev at `$$(($id).ToString('x2'))." }
                for ($index = 0; $index -lt 4; $index++) {
                    if ($operands[$index] -notmatch '^%[01]{8}$') { throw "${path}: malformed mask operand." }
                    $bits = $operands[$index].Substring(1).ToCharArray(); [Array]::Reverse($bits)
                    if ([Convert]::ToByte(($bits -join ''),2) -ne $bytes[$id * 4 + $index]) { throw "${path}: source/compiled mask differs at `$$(($id).ToString('x2'))." }
                }
                $rows.Add("$($id.ToString('x2'))`t$([BitConverter]::ToString([byte[]]$bytes[($id * 4)..($id * 4 + 3)]).Replace('-','').ToLowerInvariant())`t${path}:${label}+$((4 * $id).ToString('x4'))")
            }
            Write-GeneratedTable((Join-Path $pack.Root "metadata/object_collision_${kind}_masks.tsv"),$rows)
        }
        $bytes = Read-MenuRomBytes $pack 'enemyData' (128 * 4)
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# id`tmode`tsource")
        for ($id = 0; $id -lt 128; $id++) {
            $mode = [int]$bytes[$id * 4 + 1] -band 0x7f
            if ($mode -ge $pack.Modes) { throw "$($pack.Name): enemy `$$(($id).ToString('x2')) has no native collision row." }
            $rows.Add("$($id.ToString('x2'))`t$($mode.ToString('x2'))`tdata/$($pack.Name)/enemyData.s:enemyData+$((4 * $id).ToString('x4'))")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/object_collision_enemy_modes.tsv'),$rows)
        $bytes = Read-MenuRomBytes $pack 'itemAttributes' (0x2c * 4)
        $rows = [Collections.Generic.List[string]]::new(); $rows.Add("# id`ttype`tsource")
        for ($id = 0; $id -lt 0x2c; $id++) {
            $type = [int]$bytes[$id * 4] -band 0x7f
            if ($type -ge 32) { throw "$($pack.Name): ITEM `$$(($id).ToString('x2')) has no native collision column." }
            $rows.Add("$($id.ToString('x2'))`t$($type.ToString('x2'))`tdata/$($pack.Name)/itemAttributes.s:itemAttributes+$((4 * $id).ToString('x4'))")
        }
        Write-GeneratedTable((Join-Path $pack.Root 'metadata/object_collision_item_types.tsv'),$rows)
        $status = Read-MenuRomBytes $pack 'applyDamageToEnemyOrPart@damageTypeTable' 0x2c
        if (($status[0x24..0x2b] -join ',') -ne '41,240,0,120,96,236,0,0') { throw "$($pack.Name): native Boomerang damage status changed." }
    }
    finally { [void](Invoke-AssemblySourceHost $connection 'QUIT' ''); $connection.WaitForExit(); $connection.Dispose() }
}
Write-GeneratedTable((Join-Path (Split-Path $destination -Parent) 'common/metadata/boomerang_status.tsv'),@(
    "# damage-type`tflags`tinvincibility`tknockback`tstun`tsource",
    "24`t29`tf0`t00`t78`tcode/collisionEffects.s:applyDamageToEnemyOrPart@damageTypeTable+24",
    "28`t60`tec`t00`t00`tcode/collisionEffects.s:applyDamageToEnemyOrPart@damageTypeTable+28"
))
